using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests.Memory;

public class HashTableTests
{
    private sealed record Table(FakeWorld World, nint CountAt, nint BucketsAt, List<long> Nodes)
    {
        public FakeMemory Memory => World.Memory;
    }

    private static Table Build(uint bucketCount, params uint[] keys)
    {
        var world = new FakeWorld();
        var header = world.Alloc(16);
        var buckets = new long[bucketCount];
        var nodes = new List<long>();
        foreach (var key in keys)
        {
            var node = world.Alloc(0x20);
            var bucket = key % bucketCount;
            world.Memory.Write(node + Offsets.NodeKey, key);
            world.Memory.Write(node + Offsets.NodeEntity, 0x5000L + key);
            world.Memory.Write(node + Offsets.NodeNext, buckets[bucket]);
            buckets[bucket] = node;
            nodes.Add(node);
        }

        var array = world.Alloc(buckets.Length * 8);
        world.Memory.WriteArray(array, buckets);
        world.Memory.Write(header, bucketCount);
        world.Memory.Write(header + 8, array);
        return new Table(world, (nint)header, (nint)(header + 8), nodes);
    }

    private static bool Find(Table table, uint key, out nint value) => table.Memory.TryFindHashValue(table.CountAt, table.BucketsAt, key, out value);

    [Fact]
    public void BucketsReadEveryHeadUpToTheirMax()
    {
        var table = Build(4, 1, 2, 5);

        Assert.True(table.Memory.TryReadBuckets(table.CountAt, table.BucketsAt, 4, out var buckets));

        Assert.Equal([0, (nint)table.Nodes[2], (nint)table.Nodes[1], 0], buckets);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(5u)]
    [InlineData(uint.MaxValue)]
    public void ZeroOrTooManyBucketsAreRejected(uint count)
    {
        var table = Build(4, 1, 2);
        table.Memory.Write(table.CountAt, count);

        Assert.False(table.Memory.TryReadBuckets(table.CountAt, table.BucketsAt, 4, out var buckets));
        Assert.Empty(buckets);
        Assert.False(table.Memory.TryFindHashNode(table.CountAt, table.BucketsAt, 4, 1, Offsets.NodeNext, _ => true, out _));
    }

    [Fact]
    public void MoreBucketsThanMaxBucketsAreNoTableForAValueLookup()
    {
        var table = Build(4, 1);
        table.Memory.Write(table.CountAt, (uint)Offsets.MaxBuckets + 1);

        Assert.False(Find(table, 1, out _));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x7777_0000_0000L)]
    public void ANullOrUnmappedBucketArrayIsRejected(long array)
    {
        var table = Build(4, 1);
        table.Memory.Write(table.BucketsAt, array);

        Assert.False(table.Memory.TryReadBuckets(table.CountAt, table.BucketsAt, 4, out _));
        Assert.False(Find(table, 1, out _));
    }

    [Fact]
    public void ValuesAreFoundByKeyAcrossCollidingChains()
    {
        var table = Build(3, [.. Enumerable.Range(1, 10).Select(i => (uint)i)]);

        for (var key = 1u; key <= 10; key++)
        {
            Assert.True(Find(table, key, out var value));
            Assert.Equal((nint)(0x5000 + key), value);
        }
        Assert.False(Find(table, 11, out _));
    }

    [Fact]
    public void ANullValueIsNotFound()
    {
        var table = Build(3, 1);
        table.Memory.Write(table.Nodes[0] + Offsets.NodeEntity, 0L);

        Assert.False(Find(table, 1, out _));
    }

    [Fact]
    public void AChainAtTheLimitIsWalkedAndOneLongerIsNot()
    {
        var atLimit = Build(1, [.. Enumerable.Range(1, Offsets.MaxChain).Select(i => (uint)i)]);
        var tooLong = Build(1, [.. Enumerable.Range(1, Offsets.MaxChain + 1).Select(i => (uint)i)]);

        Assert.True(Find(atLimit, 1, out _));
        Assert.False(Find(tooLong, 1, out _));
        Assert.True(Find(tooLong, 2, out _));
    }

    [Fact]
    public void ACyclicChainEnds()
    {
        var table = Build(1, 1, 2);
        table.Memory.Write(table.Nodes[0] + Offsets.NodeNext, table.Nodes[1]);

        Assert.False(Find(table, 3, out _));
    }

    [Fact]
    public void AnyNodeLayoutIsMatchedByThePredicate()
    {
        var table = Build(3, 1, 4, 7);

        Assert.True(table.Memory.TryFindHashNode(table.CountAt, table.BucketsAt, 3, 4, Offsets.NodeNext,
            node => table.Memory.TryRead(node + Offsets.NodeEntity, out long entity) && entity == 0x5004, out var found));
        Assert.Equal((nint)table.Nodes[1], found);
    }

    private static readonly nint ModuleBase = FakeWorld.Module;

    private static FakeMemory GObjects(params (uint Index, long Object, int Serial)[] items)
    {
        var memory = new FakeMemory();
        const long chunks = 0x10_0000_0000;
        memory.Map(ModuleBase + Offsets.GObjectsGlobalRva, 8);
        memory.Write(ModuleBase + Offsets.GObjectsGlobalRva, chunks);
        memory.Map(chunks, (Offsets.MaxGObjectChunks + 1) * 8);
        foreach (var (index, obj, serial) in items)
        {
            var chunk = 0x20_0000_0000L + index / Offsets.GObjectsPerChunk * 0x100_0000L;
            var item = chunk + index % Offsets.GObjectsPerChunk * Offsets.GObjectsItemSize;
            memory.Write(chunks + index / Offsets.GObjectsPerChunk * 8, chunk);
            memory.Map(item, Offsets.GObjectsItemSize);
            memory.Write(item + Offsets.GObjectsItemObject, obj);
            memory.Write(item + Offsets.GObjectsItemSerial, serial);
        }

        return memory;
    }

    [Fact]
    public void AGObjectAndItsSerialAreReadFromAnyChunk()
    {
        var later = (uint)(3 * Offsets.GObjectsPerChunk + 7);
        var memory = GObjects((5, 0x3000, 11), (later, 0x4000, 12));

        Assert.True(memory.TryReadGObject(ModuleBase, 5, out var obj, out var serial));
        Assert.Equal(((nint)0x3000, 11), (obj, serial));
        Assert.True(memory.TryReadGObject(ModuleBase, later, out obj, out serial));
        Assert.Equal(((nint)0x4000, 12), (obj, serial));
    }

    [Fact]
    public void AGObjectPastTheChunkCapIsRejectedEvenWhenItsChunkReads()
    {
        var lastChunk = (uint)((Offsets.MaxGObjectChunks - 1) * Offsets.GObjectsPerChunk);
        var pastCap = (uint)(Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk);
        var memory = GObjects((lastChunk, 0x3000, 1), (pastCap, 0x4000, 1));

        Assert.True(memory.TryReadGObject(ModuleBase, lastChunk, out _, out _));
        Assert.False(memory.TryReadGObject(ModuleBase, pastCap, out _, out _));
        Assert.False(memory.TryReadGObject(ModuleBase, uint.MaxValue, out _, out _));
    }

    [Fact]
    public void ANullGObjectOrAMissingChunkFails()
    {
        var memory = GObjects((5, 0, 1));

        Assert.False(memory.TryReadGObject(ModuleBase, 5, out _, out _));
        Assert.False(memory.TryReadGObject(ModuleBase, 6, out _, out _));
        Assert.False(memory.TryReadGObject(ModuleBase, (uint)Offsets.GObjectsPerChunk, out _, out _));
    }
}
