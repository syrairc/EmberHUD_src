using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using Aember;
using Aember.Testing;

namespace Mcp;

public sealed class MemoryToolsTests
{
    static readonly Tool[] Tools = MemoryTools.Create(() => 64);

    static nint Block(int size = 128)
    {
        TestMemory.Clear();
        var at = TestMemory.Alloc(size);
        TestMemory.Write(at, 0);
        return at;
    }

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
        TestMemory.WriteBytes(at, "Hello, Aember!!!"u8);
        var result = Wire.Run(Tools, "read_bytes", Args(at, ("length", 16)));
        Check.Equal(16, (int)result["read"]!);
        var dump = (string)result["dump"]!;
        Check.That(dump.Contains("48 65 6C 6C 6F 2C 20 41 65 6D 62 65 72 21 21 21") && dump.EndsWith("Hello, Aember!!!"), dump);
        Check.That(dump.StartsWith(Hex.Of(at)), "line starts with the address");
        Check.Equal(2, ((string)Wire.Run(Tools, "read_bytes", Args(at, ("length", 17)))["dump"]!).Split('\n').Length);
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

    static readonly SceneEntity[] Listed =
    [
        new(1, 48, new Vector3(30, 0, 0), true, EntityKind.Monster, "") { Address = 0x1000 },
        new(2, 77, new Vector3(10, 0, 5), false, null, "EMapGodOfWar") { Address = 0x2000 },
        new(3, 66, null, true, null, ""),
        new(4, 48, new Vector3(20, 0, 0), true, EntityKind.Monster, ""),
    ];

    static JsonObject ListScene(JsonObject? args = null, Player? player = null)
    {
        TestGame.Set(new GameState("playing", 12, player, [], null));
        TestGame.UseScene(() => Listed);
        try
        {
            return Wire.Run(Tools, "scene_entities", args);
        }
        finally
        {
            TestGame.UseScene(null);
        }
    }

    static string Ids(JsonObject result) => string.Join(',', result["entities"]!.AsArray().Select(row => (int)row!["id"]!));

    [Test]
    public void SceneEntitiesListsNearestFirstWithEveryField()
    {
        var result = ListScene(player: new Player(Vector3.Zero, Vector2.UnitX));
        Check.Equal(4, (int)result["total"]!);
        Check.Equal(4, (int)result["returned"]!);
        Check.Equal("2,4,1,3", Ids(result));
        var statue = result["entities"]![0]!;
        Check.Equal(77, (int)statue["cfgId"]!);
        Check.Equal("EMapGodOfWar", (string?)statue["class"]);
        Check.That(statue["kind"] is null, "unkinded is null");
        Check.Equal(false, (bool)statue["alive"]!);
        Check.Equal(5d, (double)statue["z"]!);
        Check.Equal(10d, (double)statue["distance"]!);
        Check.Equal("0x2000", (string?)statue["address"]);
        Check.Equal("Monster", (string?)result["entities"]![1]!["kind"]);
        var loose = result["entities"]![3]!;
        Check.That(loose["x"] is null && loose["distance"] is null, "no position");
    }

    [Test]
    public void SceneEntitiesFiltersAndLimits()
    {
        var player = new Player(Vector3.Zero, Vector2.UnitX);
        Check.Equal("4,1", Ids(ListScene(new JsonObject { ["cfgId"] = 48 }, player)));
        Check.Equal("2", Ids(ListScene(new JsonObject { ["class"] = "godof" }, player)));
        Check.Equal("2,4", Ids(ListScene(new JsonObject { ["radius"] = 25 }, player)));
        var limited = ListScene(new JsonObject { ["limit"] = 1 }, player);
        Check.Equal(4, (int)limited["total"]!);
        Check.Equal(1, (int)limited["returned"]!);
        Check.Equal("1,2,3,4", Ids(ListScene()));
        Check.That(Wire.Fails(Tools, "scene_entities", new JsonObject { ["limit"] = 0 }).Contains("between"), "limit range");
    }

    [Test]
    public void SceneEntitiesWithoutAGameIsEmpty()
    {
        TestGame.UseScene(null);
        TestGame.Set(GameState.Empty);
        var result = Wire.Run(Tools, "scene_entities", new JsonObject { ["radius"] = 10 });
        Check.Equal(0, (int)result["total"]!);
        Check.Equal(0, result["entities"]!.AsArray().Count);
    }
}
