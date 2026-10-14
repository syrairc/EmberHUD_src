using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class HashTableTests
{
    private sealed record Table(FakeWorld World, nint CountAt, nint BucketsAt, List<nint> Nodes)
    {
        public FakeMemory Memory => World.Memory;
    }

    private static Table Build(uint bucketCount, params uint[] keys)
    {
        var world = new FakeWorld();
        var header = world.Alloc(16);
        var (array, nodes) = world.WriteChains((int)bucketCount, keys.Select(key => (key, (nint)(0x5000 + key))));
        world.Memory.Write(header, bucketCount);
        world.Memory.Write(header + 8, array);
        return new Table(world, header, header + 8, nodes);
    }

    private static bool Find(Table table, uint key, out nint value) => table.Memory.TryFindHashValue(table.CountAt, table.BucketsAt, key, out value);

    private static List<nint> Walk(Table table, int maxBuckets, out bool whole, int maxNodes = int.MaxValue)
    {
        List<nint> visited = [];
        whole = table.Memory.TryWalkBuckets(table.CountAt, table.BucketsAt, maxBuckets, Offsets.NodeSize, Offsets.NodeNext, maxNodes, (node, _) =>
        {
            visited.Add(node);
            return true;
        });
        return visited;
    }

    [Fact]
    public void EveryNodeOfEveryBucketIsVisitedInBucketOrder()
    {
        var table = Build(4, 1, 2, 5);

        var visited = Walk(table, 4, out var whole);

        Assert.True(whole);
        Assert.Equal([table.Nodes[2], table.Nodes[0], table.Nodes[1]], visited);
    }

    [Fact]
    public void TheVisitorGetsTheWholeNodeBlockInOneRead()
    {
        var table = Build(4, 1, 2, 5);
        table.Memory.Reads.Clear();

        Walk(table, 4, out _);

        foreach (var node in table.Nodes)
            Assert.Single(table.Memory.Reads, r => r.Address == node && r.Length == Offsets.NodeSize);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(5u)]
    [InlineData(uint.MaxValue)]
    public void ZeroOrTooManyBucketsAreRejected(uint count)
    {
        var table = Build(4, 1, 2);
        table.Memory.Write(table.CountAt, count);

        Assert.Empty(Walk(table, 4, out var whole));
        Assert.False(whole);
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

        Walk(table, 4, out var whole);

        Assert.False(whole);
        Assert.False(Find(table, 1, out _));
    }

    [Fact]
    public void AChainAtTheLimitIsWholeAndOneLongerIsTornAfterTheLimitOfNodes()
    {
        var atLimit = Build(1, [.. Enumerable.Range(1, Offsets.MaxChain).Select(i => (uint)i)]);
        var tooLong = Build(1, [.. Enumerable.Range(1, Offsets.MaxChain + 1).Select(i => (uint)i)]);

        Assert.Equal(Offsets.MaxChain, Walk(atLimit, 1, out var atLimitWhole).Count);
        Assert.True(atLimitWhole);
        Assert.Equal(Offsets.MaxChain, Walk(tooLong, 1, out var tooLongWhole).Count);
        Assert.False(tooLongWhole);
    }

    [Fact]
    public void MoreNodesThanTheNodeCapIsTornAndAnUnmappedNodeIsTorn()
    {
        var table = Build(3, 1, 2, 3, 4);
        Assert.Equal(3, Walk(table, 3, out var capped, maxNodes: 3).Count);
        Assert.False(capped);

        table.Memory.Write(table.Nodes[0] + Offsets.NodeNext, FakeWorld.Unmapped);
        Walk(table, 3, out var unmapped);
        Assert.False(unmapped);
    }

    [Fact]
    public void ACyclicChainIsTorn()
    {
        var table = Build(1, 1, 2);
        table.Memory.Write(table.Nodes[0] + Offsets.NodeNext, table.Nodes[1]);

        Walk(table, 1, out var whole);

        Assert.False(whole);
    }

    [Fact]
    public void AVisitorThatStopsEndsTheWalkWithoutCallingItATear()
    {
        var table = Build(4, 1, 2, 5);
        var seen = 0;

        var whole = table.Memory.TryWalkBuckets(table.CountAt, table.BucketsAt, 4, Offsets.NodeSize, Offsets.NodeNext, int.MaxValue, (_, _) => ++seen < 2);

        Assert.True(whole);
        Assert.Equal(2, seen);
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
        Assert.Equal(table.Nodes[1], found);
    }
}
