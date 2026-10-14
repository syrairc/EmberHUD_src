using System.Linq;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class MemoryToolsTests
{
    static readonly Tool[] Tools = MemoryTools.Create(() => 64);

    static JsonObject Args(nint at, params (string Name, JsonNode Value)[] more)
    {
        var args = new JsonObject { ["address"] = Hex.Of(at) };
        foreach (var (name, value) in more) args[name] = value;
        return args;
    }

    [Test]
    public void NotAttachedSaysSo()
    {
        TestMemory.Clear();
        foreach (var (name, args) in new (string, JsonObject)[]
        {
            ("memory_globals", new JsonObject()),
            ("read_bytes", new JsonObject { ["address"] = "0x1000", ["length"] = 4 }),
            ("read_typed", new JsonObject { ["address"] = "0x1000", ["type"] = "i32" }),
            ("find_pointer_offset", new JsonObject { ["base"] = "0x1000", ["target"] = "0x2000" }),
        })
            Check.That(Wire.Fails(Tools, name, args).Contains("not attached"), name);
    }

    [Test]
    public void GlobalsListsNamedAddresses()
    {
        var at = Block();
        TestMemory.Global("FightMgr", at);
        TestMemory.Global("AMgr", at + 8);
        var result = Wire.Run(Tools, "memory_globals");
        Check.Equal(Hex.Of(at), (string?)result["globals"]!["FightMgr"]);
        Check.Equal("AMgr,FightMgr", string.Join(',', result["globals"]!.AsObject().Select(pair => pair.Key)));
        Check.That(((string?)result["moduleBase"])!.StartsWith("0x"), "module base is hex");
    }

    [Test]
    public void ReadBytesDumpsHexAndAscii()
    {
        var at = Block();
        TestMemory.WriteBytes(at, "Hello, EmberHUD!"u8);
        var result = Wire.Run(Tools, "read_bytes", Args(at, ("length", 16)));
        var dump = (string)result["dump"]!;
        Check.That(dump.Contains("48 65 6C 6C 6F 2C 20 45 6D 62 65 72 48 55 44 21") && dump.EndsWith("Hello, EmberHUD!"), dump);
        Check.That(dump.StartsWith(Hex.Of(at)), "line starts with the address");
        Check.Equal(2, ((string)Wire.Run(Tools, "read_bytes", Args(at, ("length", 17)))["dump"]!).Split('\n').Length);
    }

    [Test]
    public void APartialLastRowIsPaddedSoTheAsciiColumnLinesUp()
    {
        var at = Block();
        TestMemory.WriteBytes(at, "Hello, EmberHUD!X"u8);
        var lines = ((string)Wire.Run(Tools, "read_bytes", Args(at, ("length", 17)))["dump"]!).Split('\n');
        Check.Equal(2, lines.Length);
        Check.Equal(lines[0].Length - 16, lines[1].Length - 1);
        Check.That(lines[1].EndsWith("  X") && lines[1].Contains("  58 "), lines[1]);
    }

    [Test]
    public void ReadBytesRespectsTheCaps()
    {
        var at = Block();
        Check.That(Wire.Fails(Tools, "read_bytes", Args(at, ("length", 65))).Contains("MaxReadBytes"), "setting cap");
        Check.That(Wire.Fails(Tools, "read_bytes", Args(at, ("length", 0))).Contains("between"), "min");
        Check.That(Wire.Fails(Tools, "read_bytes", Args(at, ("length", 70000))).Contains("between"), "hard max");
        Check.That(Wire.Fails(Tools, "read_bytes", Args(at)).Contains("length is required"), "required");
    }

    [Test]
    public void UnreadableMemoryIsAToolError()
    {
        Block();
        Check.That(Wire.Fails(Tools, "read_bytes", new JsonObject { ["address"] = "0x10", ["length"] = 8 }).Contains("nothing readable"), "bytes");
        Check.That(Wire.Fails(Tools, "find_pointer_offset", new JsonObject { ["base"] = "0x10", ["target"] = "0x20" }).Contains("nothing readable"), "scan");
        Check.That(Wire.Fails(Tools, "read_bytes", new JsonObject { ["address"] = "not hex", ["length"] = 8 }).Contains("hex"), "bad address");
        var typed = Wire.Run(Tools, "read_typed", new JsonObject { ["address"] = "0x10", ["type"] = "i32", ["count"] = 2 });
        Check.That(typed["values"]![0] is null && typed["values"]![1] is null, "unreadable values are null");
    }

    [Test]
    public void ReadTypedDecodesEveryType()
    {
        var at = Block();
        TestMemory.Write(at, -7);
        TestMemory.Write(at + 4, 1.5f);
        TestMemory.Write(at + 8, long.MaxValue - 5);
        TestMemory.Write(at + 16, at);
        TestMemory.Write(at + 24, (byte)200);
        TestMemory.Write(at + 25, (byte)1);
        TestMemory.Write(at + 32, 2.25);
        TestMemory.Write(at + 40, (short)-300);
        TestMemory.Write(at + 42, (ushort)65000);
        TestMemory.Write(at + 44, uint.MaxValue);
        TestMemory.Write(at + 48, ulong.MaxValue);
        TestMemory.Write(at + 56, (sbyte)-5);
        JsonNode? Read(int offset, string type) => Wire.Run(Tools, "read_typed", Args(at + offset, ("type", type)))["values"]![0];
        Check.Equal(-7, (int)Read(0, "i32")!);
        Check.Equal(1.5, (double)Read(4, "f32")!);
        Check.Equal((long.MaxValue - 5).ToString(), (string?)Read(8, "i64"));
        Check.Equal(Hex.Of(at), (string?)Read(16, "ptr"));
        Check.Equal(200, (int)Read(24, "u8")!);
        Check.Equal(true, (bool)Read(25, "bool")!);
        Check.Equal(2.25, (double)Read(32, "f64")!);
        Check.Equal(-300, (int)Read(40, "i16")!);
        Check.Equal(65000, (int)Read(42, "u16")!);
        Check.Equal(uint.MaxValue.ToString(), ((long)Read(44, "u32")!).ToString());
        Check.Equal(ulong.MaxValue.ToString(), (string?)Read(48, "u64"));
        Check.Equal(-5, (int)Read(56, "i8")!);
    }

    [Test]
    public void ReadTypedStridesAndRejectsBadInput()
    {
        var at = Block();
        for (var i = 0; i < 4; i++) TestMemory.Write(at + i * 16, i + 10);
        var values = Wire.Run(Tools, "read_typed", Args(at, ("type", "i32"), ("count", 4), ("stride", 16)))["values"]!.AsArray();
        Check.Equal("10,11,12,13", string.Join(',', values.Select(value => (int)value!)));
        Check.That(Wire.Fails(Tools, "read_typed", Args(at, ("type", "int"))).Contains("i32"), "valid types listed");
        Check.That(Wire.Fails(Tools, "read_typed", Args(at, ("type", "i32"), ("count", 5000))).Contains("between"), "count cap");
    }

    [Test]
    public void NonFiniteFloatsBecomeNull()
    {
        var at = Block();
        TestMemory.Write(at, float.NaN);
        TestMemory.Write(at + 8, double.PositiveInfinity);
        Check.That(Wire.Run(Tools, "read_typed", Args(at, ("type", "f32")))["values"]![0] is null, "f32");
        Check.That(Wire.Run(Tools, "read_typed", Args(at + 8, ("type", "f64")))["values"]![0] is null, "f64");
    }

    [Test]
    public void FindPointerOffsetFindsEveryHit()
    {
        var at = Block(256);
        var target = at + 0x80;
        TestMemory.Write(at + 0x10, target);
        TestMemory.Write(at + 0x40, target + 4);
        TestMemory.Write(at + 0x48, target);
        var exact = Wire.Run(Tools, "find_pointer_offset", new JsonObject { ["base"] = Hex.Of(at), ["target"] = Hex.Of(target), ["range"] = 0x100 });
        Check.Equal("0x10,0x48", string.Join(',', exact["offsets"]!.AsArray().Select(offset => (string?)offset)));
        var near = Wire.Run(Tools, "find_pointer_offset", new JsonObject { ["base"] = Hex.Of(at), ["target"] = Hex.Of(target), ["range"] = 0x100, ["within"] = 8 });
        Check.Equal("0x10,0x40,0x48", string.Join(',', near["offsets"]!.AsArray().Select(offset => (string?)offset)));
    }
}
