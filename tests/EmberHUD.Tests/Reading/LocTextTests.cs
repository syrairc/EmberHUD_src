using System.Text;
using EmberHUD.Host;
using EmberHUD.Host.Memory;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class LocTextTests
{
    static LocText ReaderFor(FakeWorld world) => new(world.Memory, unchecked((nint)FakeWorld.ModuleBase));

    [Theory]
    [InlineData("", 0u)]
    [InlineData("a", 0x115EA782u)]
    [InlineData("map_modifier|description|2320000", 0xC7BF7932u)]
    public void HashIsSuperFastHash(string text, uint expected) => Assert.Equal(expected, LocText.Hash(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void FindsTheCurrentLanguagesText()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("map_modifier|description|2320000", "There are Coffins awaiting escort in this stage"), ("level|name|4403", "Rainforest of Divine Legacy"));
        var loc = ReaderFor(world);
        Assert.Equal("There are Coffins awaiting escort in this stage", loc.Get("map_modifier|description|2320000"));
        Assert.Equal("Rainforest of Divine Legacy", loc.Get("level|name|4403"));
        Assert.Null(loc.Get("level|name|1"));
    }

    [Fact]
    public void SameHashDifferentKeyIsSkipped()
    {
        var world = new FakeWorld();
        var hash = LocText.Hash(Encoding.UTF8.GetBytes("wanted"));
        world.SetLanguageTable(2, 3, [("wanted", "right", hash), ("impostor", "wrong", hash)]);
        Assert.Equal("right", ReaderFor(world).Get("wanted"));
    }

    [Fact]
    public void MissIsNotCached()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "first"));
        var loc = ReaderFor(world);
        Assert.Null(loc.Get("b"));
        world.SetLanguageTable(2, 7, ("a", "first"), ("b", "second"));
        Assert.Equal("second", loc.Get("b"));
    }

    [Fact]
    public void HitsAreCached()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "first"));
        var loc = ReaderFor(world);
        Assert.Equal("first", loc.Get("a"));
        var reads = world.Memory.Reads.Count;
        Assert.Equal("first", loc.Get("a"));
        Assert.Equal(reads + 1, world.Memory.Reads.Count);
    }

    [Fact]
    public void LanguageChangeDropsTheCache()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "english"));
        var loc = ReaderFor(world);
        Assert.Equal("english", loc.Get("a"));
        world.SetLanguageTable(5, 7, ("a", "deutsch"));
        Assert.Equal("deutsch", loc.Get("a"));
    }

    [Fact]
    public void NoTableGivesNull()
    {
        var world = new FakeWorld();
        Assert.Null(ReaderFor(world).Get("a"));
        Assert.Empty(ReaderFor(world).Find("a", 50).Matches);
    }

    [Fact]
    public void FindMatchesTextBySubstringAcrossBucketsIgnoringCase()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("level|name|4403", "Rainforest of Divine Legacy"), ("level|name|4404", "Withered RAINFOREST"), ("level|name|1", "Steel Forge"), ("x", "rain"));
        var found = ReaderFor(world).Find("rainforest", 50).Matches;
        Assert.Equal(["level|name|4403", "level|name|4404"], found.Select(f => f.Key).Order());
        Assert.Contains(("level|name|4404", "Withered RAINFOREST"), found);
    }

    [Fact]
    public void FindStopsAtMax()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "forest one"), ("b", "forest two"), ("c", "forest three"));
        Assert.Equal(2, ReaderFor(world).Find("forest", 2).Matches.Length);
    }

    [Fact]
    public void FindSurvivesACyclicChain()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 1, ("a", "forest one"), ("b", "forest two"));
        var head = BucketHead(world, 0);
        world.Memory.Write(head + Offsets.LocNodeNext, head);
        Assert.Equal([("b", "forest two")], ReaderFor(world).Find("forest", 50).Matches);
    }

    [Fact]
    public void FindGivesNothingForAnOverCapBucketCount()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "forest"));
        world.Memory.Write(LanguageMap(world) + Offsets.LocMapBucketCount, (uint)Offsets.MaxLocBuckets + 1);
        Assert.Empty(ReaderFor(world).Find("forest", 50).Matches);
    }

    [Fact]
    public void FindSaysWhenItStoppedAtTheScanCap()
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7, ("a", "forest one"));
        Assert.False(ReaderFor(world).Find("forest", 50).Truncated);

        var nodes = Offsets.MaxLocScan + 1;
        var stride = Offsets.LocNodeNext + 8;
        var chains = (nodes + Offsets.MaxChain - 1) / Offsets.MaxChain;
        var at = world.Alloc(nodes * stride);
        var fields = new long[nodes * stride / 8];
        for (var i = 0; i + 1 < nodes; i++)
            if ((i + 1) % Offsets.MaxChain != 0)
                fields[(i * stride + Offsets.LocNodeNext) / 8] = at + (i + 1) * stride;
        world.Memory.WriteArray(at, fields);
        var buckets = world.Alloc(chains * 8);
        world.Memory.WriteArray(buckets, Enumerable.Range(0, chains).Select(c => at + (long)c * Offsets.MaxChain * stride).ToArray());
        world.Memory.Write(LanguageMap(world), buckets);
        world.Memory.Write(LanguageMap(world) + Offsets.LocMapBucketCount, (uint)chains);

        var (matches, truncated) = ReaderFor(world).Find("forest", 50);
        Assert.Empty(matches);
        Assert.True(truncated);
    }

    static long LanguageMap(FakeWorld world)
    {
        world.Memory.TryReadPtr(unchecked((nint)(FakeWorld.ModuleBase + Offsets.LanguageBucketsRva)), out var languageBuckets);
        world.Memory.TryReadPtr(languageBuckets, out var languages);
        return languages + Offsets.LanguageNodeMap;
    }

    static long BucketHead(FakeWorld world, int bucket)
    {
        world.Memory.TryReadPtr((nint)LanguageMap(world), out var buckets);
        world.Memory.TryReadPtr(buckets + bucket * 8, out var head);
        return head;
    }
}
