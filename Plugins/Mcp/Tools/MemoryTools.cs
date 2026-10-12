using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Aember;

namespace Mcp;

internal static class MemoryTools
{
    const int HardMax = 65536;
    static readonly Dictionary<string, int> Sizes = new() { ["i8"] = 1, ["u8"] = 1, ["bool"] = 1, ["i16"] = 2, ["u16"] = 2, ["i32"] = 4, ["u32"] = 4, ["f32"] = 4, ["i64"] = 8, ["u64"] = 8, ["ptr"] = 8, ["f64"] = 8 };

    public static Tool[] Create(Func<int> maxRead) =>
    [
        new("memory_globals", "Named addresses the host resolved (FNamePool, GUObjectArray, manager singletons, ...) plus the game module base, all hex.", Schema.Of([]), _ => Globals()),
        new("read_bytes", "Hex + ASCII dump of game memory. length is capped by the MaxReadBytes setting.",
            Schema.Of(["address", "length"], ("address", "string", "hex address"), ("length", "integer", "bytes to read, 1 to 65536")), args => ReadBytes(args, maxRead())),
        new("read_typed", "Read count values of one type at address, stride bytes apart (default the type size). Types: i8 u8 bool i16 u16 i32 u32 f32 i64 u64 ptr f64. 64-bit integers come back as decimal strings, ptr as hex. Unreadable slots are null.",
            Schema.Of(["address", "type"], ("address", "string", "hex address"), ("type", "string", "i8 u8 bool i16 u16 i32 u32 f32 i64 u64 ptr f64"), ("count", "integer", "1 to 4096, default 1"), ("stride", "integer", "bytes between values, 1 to 65536")), ReadTyped),
        new("find_pointer_offset", "Scan range bytes from base for 8-byte aligned pointers equal to target (or inside target..target+within). Returns the offsets, hex.",
            Schema.Of(["base", "target"], ("base", "string", "hex address to scan from"), ("target", "string", "hex pointer value to look for"), ("range", "integer", "bytes to scan, 8 to 65536, default 4096"), ("within", "integer", "also match pointers up to this many bytes past target, default 0")), FindPointerOffset),
    ];

    static void RequireAttached()
    {
        if (!Memory.Attached) throw new ToolError("memory is not attached - run Aember live with the game open");
    }

    static JsonObject Globals()
    {
        RequireAttached();
        return new JsonObject
        {
            ["moduleBase"] = Hex.Of(Memory.ModuleBase),
            ["globals"] = new JsonObject(Memory.Globals.OrderBy(global => global.Key, StringComparer.Ordinal).Select(global => KeyValuePair.Create(global.Key, (JsonNode?)Hex.Of(global.Value)))),
        };
    }

    static JsonObject ReadBytes(Args args, int limit)
    {
        RequireAttached();
        var at = args.Addr("address");
        var length = args.RequiredInt("length", 1, HardMax);
        if (length > limit) throw new ToolError($"length {length} is over the MaxReadBytes setting {limit}");
        var buffer = new byte[length];
        var read = Memory.Read(at, buffer);
        if (read == 0) throw new ToolError($"nothing readable at {Hex.Of(at)}");
        return new JsonObject { ["address"] = Hex.Of(at), ["requested"] = length, ["read"] = read, ["dump"] = Dump(at, buffer.AsSpan(0, read)) };
    }

    static string Dump(nint at, ReadOnlySpan<byte> bytes)
    {
        var lines = new List<string>();
        for (var row = 0; row < bytes.Length; row += 16)
        {
            var chunk = bytes.Slice(row, Math.Min(16, bytes.Length - row)).ToArray();
            var hex = string.Join(' ', chunk.Select(value => value.ToString("X2")));
            var ascii = new string(chunk.Select(value => value is >= 0x20 and < 0x7F ? (char)value : '.').ToArray());
            lines.Add($"{Hex.Of(at + row)}  {hex,-47}  {ascii}");
        }
        return string.Join('\n', lines);
    }

    static JsonObject ReadTyped(Args args)
    {
        RequireAttached();
        var at = args.Addr("address");
        var type = args.Str("type");
        if (!Sizes.TryGetValue(type, out var size)) throw new ToolError($"type must be one of {string.Join(", ", Sizes.Keys)}");
        var count = args.Int("count", 1, 1, 4096);
        var stride = args.Int("stride", size, 1, 65536);
        var values = new JsonArray();
        var scratch = new byte[8];
        for (var i = 0; i < count; i++)
            values.Add(Memory.Read(at + i * stride, scratch.AsSpan(0, size)) == size ? Decode(type, scratch) : null);
        return new JsonObject { ["address"] = Hex.Of(at), ["type"] = type, ["stride"] = stride, ["values"] = values };
    }

    static JsonNode? Decode(string type, byte[] bytes) => type switch
    {
        "i8" => (int)(sbyte)bytes[0],
        "u8" => (int)bytes[0],
        "bool" => bytes[0] != 0,
        "i16" => (int)BitConverter.ToInt16(bytes),
        "u16" => (int)BitConverter.ToUInt16(bytes),
        "i32" => BitConverter.ToInt32(bytes),
        "u32" => (long)BitConverter.ToUInt32(bytes),
        "f32" => Finite(BitConverter.ToSingle(bytes)),
        "f64" => Finite(BitConverter.ToDouble(bytes)),
        "i64" => BitConverter.ToInt64(bytes).ToString(CultureInfo.InvariantCulture),
        "u64" => BitConverter.ToUInt64(bytes).ToString(CultureInfo.InvariantCulture),
        _ => Hex.Of((nint)BitConverter.ToInt64(bytes)),
    };

    static JsonNode? Finite(double value) => double.IsFinite(value) ? JsonValue.Create(value) : null;

    static JsonObject FindPointerOffset(Args args)
    {
        RequireAttached();
        var start = args.Addr("base");
        var target = (long)args.Addr("target");
        var range = args.Int("range", 4096, 8, 65536);
        var within = args.Int("within", 0, 0, 65536);
        var buffer = new byte[range];
        var read = Memory.Read(start, buffer);
        if (read == 0) throw new ToolError($"nothing readable at {Hex.Of(start)}");
        var offsets = new JsonArray();
        for (var offset = 0; offset + 8 <= read; offset += 8)
        {
            var value = BitConverter.ToInt64(buffer, offset);
            if (value >= target && value <= target + within) offsets.Add(Hex.Of(offset));
        }
        return new JsonObject { ["base"] = Hex.Of(start), ["scanned"] = read, ["offsets"] = offsets };
    }
}
