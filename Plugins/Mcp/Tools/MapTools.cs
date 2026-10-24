using System;
using System.Linq;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal static class MapTools
{
    const int MaxNodes = 20000;
    const int MaxScan = 400000;
    const int MaxBuckets = 1 << 20;

    public static Tool[] Create() =>
    [
        new("fight_component", "Walk FightMgr -> fight world -> entity map -> entity -> component and return the addresses (hex): world, entity, component. component is the entity component type id, 0 returns just the entity. Re-walks from the FightMgr global on every call, so it survives area changes. Empty component address = the entity has none of that type.",
            Schema.Of(["entity"], ("entity", "integer", "fight entity id, e.g. the player id from scene_entities"), ("component", "integer", "component type id, default 0 = entity only")), FightComponent),
        new("walk_map", "Walk a chained hash map in game memory. address is the map header: pointer to the bucket array at bucketsOffset (default 0), u32 bucket count at countOffset (default 12). Nodes chain through the pointer at nextOffset. Per node: address, key (u32 at +0) and the first dump bytes as hex. match keeps only nodes holding that u32 anywhere in the dumped bytes and lists the offsets. limit defaults to 50, max 2000, skip pages.",
            Schema.Of(["address", "nextOffset"], ("address", "string", "hex address of the map header"), ("nextOffset", "integer", "offset of the next-node pointer inside a node"), ("bucketsOffset", "integer", "offset of the bucket array pointer, default 0"), ("countOffset", "integer", "offset of the u32 bucket count, default 12"), ("dump", "integer", "bytes of each node to return, 8 to 512, default 64"), ("match", "integer", "only nodes holding this u32"), ("limit", "integer", "nodes to return, 1 to 2000"), ("skip", "integer", "matching nodes to skip")), WalkMap),
        new("scan_rows", "Scan every row of a chained hash map for an i32 inside a row array, in-process (one call instead of one read per row). Node: key u32 at +0, row pointer at valueOffset (default 8), next at nextOffset. The row holds an array pointer at arrayOffset with a u32 count at countOffset; entries are stride bytes and match is compared with the i32 at the start of each entry. Returns the keys of rows that hold match, up to limit (default 100, max 2000). Example: the modifier table rows (arrayOffset 128, countOffset 136, stride 16) holding an affix class id.",
            Schema.Of(["address", "nextOffset", "arrayOffset", "countOffset", "stride", "match"], ("address", "string", "hex address of the map header"), ("nextOffset", "integer", "next-node pointer offset"), ("valueOffset", "integer", "row pointer offset inside a node, default 8"), ("arrayOffset", "integer", "array pointer offset inside a row"), ("countOffset", "integer", "u32 count offset inside a row"), ("stride", "integer", "bytes per array entry, 4 to 256"), ("match", "integer", "i32 to look for"), ("limit", "integer", "keys to return, 1 to 2000")), ScanRows),
    ];

    static JsonObject FightComponent(Args args)
    {
        Live.Require();
        var id = args.RequiredInt("entity", 0, int.MaxValue);
        var component = args.Int("component", 0, 0, 255);
        if (!Memory.Globals.TryGetValue("FightMgr", out var global)) throw new ToolError("FightMgr global is not resolved");
        var manager = Deref(global, "FightMgr");
        var world = Deref(Deref(Deref(manager + 0x78, "FightMgr+0x78") + 0x38, "+0x38") + 0x30, "+0x30");
        var buckets = Deref(world + 0x200, "entity buckets");
        if (!Memory.TryRead(world + 0x20C, out uint count) || count == 0 || count > MaxBuckets) throw new ToolError("entity map has no buckets");
        var node = Deref(buckets + (nint)(id % count) * 8, "entity bucket");
        for (var hops = 0; node != 0 && hops < 64; hops++)
        {
            if (Memory.TryRead(node, out uint key) && key == id)
            {
                var entity = Deref(node + 8, "entity");
                var result = new JsonObject { ["world"] = Hex.Of(world), ["entity"] = Hex.Of(entity) };
                if (component > 0) result["component"] = Hex.Of(ComponentOf(entity, component));
                return result;
            }
            Memory.TryRead(node + 24, out node);
        }
        throw new ToolError($"no fight entity {id}");
    }

    static JsonObject ScanRows(Args args)
    {
        Live.Require();
        var header = args.Addr("address");
        var next = args.RequiredInt("nextOffset", 0, 4096);
        var valueAt = args.Int("valueOffset", 8, 0, 4096);
        var arrayAt = args.RequiredInt("arrayOffset", 0, 4096);
        var countAt = args.RequiredInt("countOffset", 0, 4096);
        var stride = args.RequiredInt("stride", 4, 256);
        var match = args.RequiredInt("match", int.MinValue, int.MaxValue);
        var limit = args.Int("limit", 100, 1, 2000);
        var buckets = Deref(header, "bucket array");
        if (!Memory.TryRead(header + 12, out uint count) || count == 0 || count > MaxBuckets) throw new ToolError("map has no buckets");

        var keys = new JsonArray();
        var visited = 0;
        for (var bucket = 0; bucket < count && keys.Count < limit && visited < MaxScan; bucket++)
        {
            if (!Memory.TryRead(buckets + bucket * 8, out nint node)) continue;
            for (var hops = 0; node != 0 && hops < 64 && visited < MaxScan; hops++, visited++)
            {
                if (Memory.TryRead(node, out uint key) && Memory.TryRead(node + valueAt, out nint row) && row != 0 && RowHolds(row, arrayAt, countAt, stride, match) && keys.Count < limit) keys.Add((long)key);
                if (!Memory.TryRead(node + next, out node)) break;
            }
        }
        return new JsonObject { ["visited"] = visited, ["truncated"] = visited >= MaxScan, ["keys"] = keys };
    }

    static bool RowHolds(nint row, int arrayAt, int countAt, int stride, int match)
    {
        if (!Memory.TryRead(row + arrayAt, out nint array) || array == 0 || !Memory.TryRead(row + countAt, out uint entries) || entries > 256) return false;
        for (var i = 0; i < entries; i++)
            if (Memory.TryRead(array + i * stride, out int value) && value == match) return true;
        return false;
    }

    static nint ComponentOf(nint entity, int type)
    {
        var index = Deref(entity + 0x60, "component index");
        var slots = Deref(entity + 0x68, "component slots");
        return Memory.TryRead(index + type, out byte slot) && slot != 0 && Memory.TryRead(slots + slot * 8, out nint address) ? address : 0;
    }

    static nint Deref(nint at, string what) => Memory.TryRead(at, out nint value) ? value : throw new ToolError($"nothing readable at {what} ({Hex.Of(at)})");

    static JsonObject WalkMap(Args args)
    {
        Live.Require();
        var header = args.Addr("address");
        var next = args.RequiredInt("nextOffset", 0, 4096);
        var dump = args.Int("dump", 64, 8, 512);
        var limit = args.Int("limit", 50, 1, 2000);
        var skip = args.Int("skip", 0, 0, int.MaxValue);
        var match = args.OptInt("match");
        var buckets = Deref(header + args.Int("bucketsOffset", 0, 0, 4096), "bucket array");
        if (!Memory.TryRead(header + args.Int("countOffset", 12, 0, 4096), out uint count) || count == 0 || count > MaxBuckets) throw new ToolError("map has no buckets");

        var rows = new JsonArray();
        var matched = 0;
        var visited = 0;
        var bytes = new byte[dump];
        for (var bucket = 0; bucket < count && rows.Count < limit && visited < MaxNodes; bucket++)
        {
            if (!Memory.TryRead(buckets + bucket * 8, out nint node)) continue;
            for (var hops = 0; node != 0 && hops < 64 && rows.Count < limit && visited < MaxNodes; hops++, visited++)
            {
                if (Memory.Read(node, bytes) && Offsets(bytes, match) is { } hits && matched++ >= skip)
                    rows.Add(new JsonObject
                    {
                        ["node"] = Hex.Of(node),
                        ["bucket"] = bucket,
                        ["key"] = (long)BitConverter.ToUInt32(bytes),
                        ["hits"] = hits.Length == 0 ? null : new JsonArray([.. hits.Select(offset => (JsonNode)offset)]),
                        ["bytes"] = Convert.ToHexString(bytes),
                    });
                if (!Memory.TryRead(node + next, out node)) break;
            }
        }
        return new JsonObject { ["buckets"] = (long)count, ["visited"] = visited, ["returned"] = rows.Count, ["truncated"] = visited >= MaxNodes, ["nodes"] = rows };
    }

    static int[]? Offsets(byte[] bytes, int? match)
    {
        if (match is not { } value) return [];
        var hits = Enumerable.Range(0, bytes.Length / 4).Where(i => BitConverter.ToInt32(bytes, i * 4) == value).Select(i => i * 4).ToArray();
        return hits.Length == 0 ? null : hits;
    }
}
