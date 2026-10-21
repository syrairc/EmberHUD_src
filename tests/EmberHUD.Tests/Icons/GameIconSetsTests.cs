using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class GameIconSetsTests : IDisposable
{
    readonly TempDir _temp = new();
    readonly LogCapture _log = new();
    readonly FakeTextures _textures = new();

    public void Dispose()
    {
        _log.Dispose();
        _temp.Dispose();
    }

    GameIconSets Open() => new(_temp.Path, _textures.Upload, _textures.Resolve, inline: true);

    void WriteSet(string folder, string icon)
    {
        var directory = Path.Combine(_temp.Path, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, icon + ".png"), PngFixture.Rgba(2, 2, PngFixture.Gradient(2, 2), [0]));
        File.WriteAllText(Path.Combine(directory, "index.json"), "{\"icons\":{\"" + icon + "\":{\"file\":\"" + icon + ".png\"}}}");
    }

    [Fact]
    public void Probe_FollowsFolders()
    {
        WriteSet("", "UI_MiniMap_Portal");
        WriteSet("Items", "UI_Item_A");
        var sets = Open();

        Assert.True(sets.Probe(AssetKind.Minimap));
        Assert.True(sets.Probe(AssetKind.Items));
        Assert.False(sets.Probe(AssetKind.Skills));
        Assert.False(sets.Probe(AssetKind.Buffs));
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void RescanOpensAFolderThatAppearedAndRaisesChangedOnce()
    {
        var sets = Open();
        var changed = 0;
        sets.Changed += () => changed++;
        Assert.False(sets.Probe(AssetKind.Skills));

        WriteSet("Skills", "UI_Skill_A");
        sets.Rescan();
        sets.Rescan();

        Assert.True(sets.Probe(AssetKind.Skills));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void RescanReopensAStoreWhoseIndexChanged()
    {
        WriteSet("Items", "UI_Item_A");
        var sets = Open();
        sets.Resolve(AssetKind.Items, "UI_Item_A");
        sets.Pump();
        sets.Pump();
        Assert.NotNull(sets.Resolve(AssetKind.Items, "UI_Item_A"));

        WriteSet("Items", "UI_Item_B");
        File.SetLastWriteTimeUtc(Path.Combine(_temp.Path, "Items", "index.json"), DateTime.UtcNow.AddMinutes(5));
        sets.Rescan();

        sets.Resolve(AssetKind.Items, "UI_Item_B");
        sets.Pump();
        sets.Pump();
        Assert.NotNull(sets.Resolve(AssetKind.Items, "UI_Item_B"));
        Assert.Null(sets.Resolve(AssetKind.Items, "UI_Item_A"));
    }

    [Fact]
    public void ResolveIsNullFirstThenHasSpriteAfterTwoPumps()
    {
        WriteSet("Items", "UI_Item_A");
        var sets = Open();

        Assert.Null(sets.Resolve(AssetKind.Items, "UI_Item_A"));
        sets.Pump();
        sets.Pump();

        Assert.NotNull(sets.Resolve(AssetKind.Items, "UI_Item_A"));
    }

    [Fact]
    public void SameNameInTwoKindsKeepsSeparateTextures()
    {
        WriteSet("Skills", "Same");
        WriteSet("Buffs", "Same");
        var sets = Open();

        sets.Resolve(AssetKind.Skills, "Same");
        sets.Resolve(AssetKind.Buffs, "Same");
        sets.Pump();
        sets.Pump();

        Assert.Equal(2, _textures.Uploaded.Count);
    }

    [Fact]
    public void AnUnknownNameOrMissingStoreGivesNullWithoutALogLine()
    {
        WriteSet("Items", "UI_Item_A");
        var sets = Open();

        Assert.Null(sets.Resolve(AssetKind.Items, "Nope"));
        Assert.Null(sets.Resolve(AssetKind.Buffs, "UI_Item_A"));
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void ItemNameOnlyInTheSkillsStoreResolvesForItems()
    {
        WriteSet("Items", "UI_Item_A");
        WriteSet("Skills", "UI_Skill_B");
        var sets = Open();

        Assert.Null(sets.Resolve(AssetKind.Items, "UI_Skill_B"));
        sets.Pump();
        sets.Pump();

        Assert.NotNull(sets.Resolve(AssetKind.Items, "UI_Skill_B"));
        Assert.Null(sets.Resolve(AssetKind.Items, "Nope"));
    }

    [Fact]
    public void ItemNameInBothStoresPrefersItems()
    {
        WriteSet("Items", "Same");
        WriteSet("Skills", "Same");
        var sets = Open();

        sets.Resolve(AssetKind.Items, "Same");
        sets.Pump();
        sets.Pump();

        Assert.NotNull(sets.Resolve(AssetKind.Items, "Same"));
        Assert.Single(_textures.Uploaded);
        Assert.StartsWith("items/", _textures.Uploaded.Keys.Single());
    }
}
