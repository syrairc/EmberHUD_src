using System;
using System.Linq;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class MapToolsTests
{
    static readonly Tool[] Tools = MapTools.Create();

    static nint Map(params (uint Key, uint Marker, int Bucket)[] nodes)
    {
        var header = Block(64);
        var buckets = TestMemory.Alloc(8 * 4);
        TestMemory.Write(header, buckets);
        TestMemory.Write(header + 12, 4u);
        var heads = new nint[4];
        foreach (var (key, marker, bucket) in nodes)
        {
            var node = TestMemory.Alloc(48);
            TestMemory.Write(node, key);
            TestMemory.Write(node + 8, marker);
            TestMemory.Write(node + 16, heads[bucket]);
            heads[bucket] = node;
            TestMemory.Write(buckets + bucket * 8, node);
        }
        return header;
    }

    [Test]
    public void WalkMapListsChainedNodes()
    {
        var header = Map((1, 101, 0), (5, 7, 1), (9, 101, 1));
        var result = Wire.Run(Tools, "walk_map", new JsonObject { ["address"] = Hex.Of(header), ["nextOffset"] = 16, ["dump"] = 16 });
        Check.Equal(3, (int)result["returned"]!);
        Check.Equal(9L, (long)result["nodes"]![1]!["key"]!);
        Check.Equal(5L, (long)result["nodes"]![2]!["key"]!);
    }

    [Test]
    public void MatchKeepsNodesHoldingTheValueAndShowsWhere()
    {
        var header = Map((1, 101, 0), (5, 7, 1), (9, 101, 1));
        var result = Wire.Run(Tools, "walk_map", new JsonObject { ["address"] = Hex.Of(header), ["nextOffset"] = 16, ["dump"] = 16, ["match"] = 101 });
        Check.Equal(2, (int)result["returned"]!);
        Check.Equal(8, (int)result["nodes"]![0]!["hits"]![0]!);
        var paged = Wire.Run(Tools, "walk_map", new JsonObject { ["address"] = Hex.Of(header), ["nextOffset"] = 16, ["dump"] = 16, ["match"] = 101, ["skip"] = 1, ["limit"] = 1 });
        Check.Equal(9L, (long)paged["nodes"]![0]!["key"]!);
    }

    [Test]
    public void FightComponentWalksToTheComponent()
    {
        var component = TestMemory.Alloc(16);
        var slots = TestMemory.Alloc(16);
        TestMemory.Write(slots + 8, component);
        var index = TestMemory.Alloc(16);
        TestMemory.Write(index + 12, (byte)1);
        var entity = TestMemory.Alloc(0x80);
        TestMemory.Write(entity + 0x60, index);
        TestMemory.Write(entity + 0x68, slots);
        var node = TestMemory.Alloc(32);
        TestMemory.Write(node, 12u);
        TestMemory.Write(node + 8, entity);
        var buckets = TestMemory.Alloc(8 * 5);
        TestMemory.Write(buckets + 2 * 8, node);
        var world = TestMemory.Alloc(0x220);
        TestMemory.Write(world + 0x200, buckets);
        TestMemory.Write(world + 0x20C, 5u);
        var third = TestMemory.Alloc(0x40);
        TestMemory.Write(third + 0x30, world);
        var second = TestMemory.Alloc(0x40);
        TestMemory.Write(second + 0x38, third);
        var manager = TestMemory.Alloc(0x80);
        TestMemory.Write(manager + 0x78, second);
        var global = TestMemory.Alloc(8);
        TestMemory.Write(global, manager);
        TestMemory.Global("FightMgr", global);

        var result = Wire.Run(Tools, "fight_component", new JsonObject { ["entity"] = 12, ["component"] = 12 });
        Check.Equal(Hex.Of(entity), (string?)result["entity"]);
        Check.Equal(Hex.Of(component), (string?)result["component"]);
        Check.Equal(Hex.Of(0), (string?)Wire.Run(Tools, "fight_component", new JsonObject { ["entity"] = 12, ["component"] = 3 })["component"]);
        Check.That(Wire.Fails(Tools, "fight_component", new JsonObject { ["entity"] = 13 }).Contains("no fight entity"), "missing entity");
    }

    [Test]
    public void ScanRowsFindsKeysWhoseRowHoldsTheValue()
    {
        var header = Block(64);
        var buckets = TestMemory.Alloc(8 * 4);
        TestMemory.Write(header, buckets);
        TestMemory.Write(header + 12, 4u);
        (uint Key, int[] Entries)[] rows = [(1, [5, 7]), (2, [9]), (3, [7, 3])];
        foreach (var (key, entries) in rows)
        {
            var array = TestMemory.Alloc(entries.Length * 16);
            for (var i = 0; i < entries.Length; i++) TestMemory.Write(array + i * 16, entries[i]);
            var row = TestMemory.Alloc(48);
            TestMemory.Write(row + 16, array);
            TestMemory.Write(row + 24, (uint)entries.Length);
            var node = TestMemory.Alloc(40);
            TestMemory.Write(node, key);
            TestMemory.Write(node + 8, row);
            TestMemory.Write(node + 24, 0L);
            TestMemory.Write(buckets + (int)(key % 4) * 8, node);
        }
        var result = Wire.Run(Tools, "scan_rows", new JsonObject { ["address"] = Hex.Of(header), ["nextOffset"] = 24, ["arrayOffset"] = 16, ["countOffset"] = 24, ["stride"] = 16, ["match"] = 7 });
        Check.Equal("1,3", string.Join(',', result["keys"]!.AsArray().Select(key => (long)key!)));
    }
}
