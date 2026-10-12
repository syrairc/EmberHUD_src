using System.Linq;
using System.Text.Json.Nodes;
using Aember.Testing;

namespace Mcp;

public sealed class UeToolsTests
{
    static readonly Tool[] Tools = UeTools.Create();

    sealed class World
    {
        public readonly TestUe Ue = new();
        public readonly nint Actor;
        public readonly nint Prop;
        public readonly nint Hero;
        public readonly nint Villain;
        public readonly nint Rock;
        public readonly nint DefaultActor;
        public readonly nint Vector;

        public World()
        {
            Vector = Ue.ScriptStruct("Vec2", 8, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4));
            Actor = Ue.Class("Actor", 0, 0x70, new TestProp("Health", "FloatProperty", 0x30), new TestProp("Target", "ObjectProperty", 0x38, 8), new TestProp("Spot", "StructProperty", 0x40, 8, 0, Vector), new TestProp("Alive", "BoolProperty", 0x48, 1, 0x02));
            Prop = Ue.Class("Prop", 0, 0x40);
            Hero = Ue.Object("Hero", Actor);
            Villain = Ue.Object("Villain", Actor);
            Rock = Ue.Object("Rock", Prop);
            DefaultActor = Ue.Object("Default__Actor", Actor);
            TestMemory.Write(Hero + 0x30, 12.5f);
            TestMemory.Write(Hero + 0x38, Villain);
            TestMemory.Write(Hero + 0x40, 3f);
            TestMemory.Write(Hero + 0x44, 4f);
            TestMemory.Write(Hero + 0x48, (byte)0x02);
            TestMemory.Write(Villain + 0x38, Hero);
        }
    }

    static JsonObject Args(params (string Name, JsonNode Value)[] pairs)
    {
        var args = new JsonObject();
        foreach (var (name, value) in pairs) args[name] = value;
        return args;
    }

    [Test]
    public void NotAttachedSaysSo()
    {
        TestMemory.Clear();
        foreach (var (name, args) in new (string, JsonObject)[] { ("ue_find", new JsonObject()), ("ue_class", Args(("class", "Actor"))), ("ue_object", Args(("address", "0x1000"))), ("ue_name", Args(("index", 1))) })
            Check.That(Wire.Fails(Tools, name, args).Contains("not attached"), name);
    }

    [Test]
    public void FindFiltersByClassNameAndDefaults()
    {
        var world = new World();
        string Names(JsonObject result) => string.Join(',', result["objects"]!.AsArray().Select(row => (string?)row!["name"]).OrderBy(name => name));
        Check.Equal("Hero,Villain", Names(Wire.Run(Tools, "ue_find", Args(("class", "Actor")))));
        Check.Equal("Default__Actor,Hero,Villain", Names(Wire.Run(Tools, "ue_find", Args(("class", "Actor"), ("includeDefaults", true)))));
        Check.Equal("Villain", Names(Wire.Run(Tools, "ue_find", Args(("name", "ILL")))));
        Check.Equal("Rock", Names(Wire.Run(Tools, "ue_find", Args(("class", "Prop"), ("name", "rock")))));
        var row = Wire.Run(Tools, "ue_find", Args(("name", "Hero")))["objects"]![0]!;
        Check.Equal(Hex.Of(world.Hero), (string?)row["address"]);
        Check.Equal("Actor", (string?)row["class"]);
        Check.Equal(Hex.Of(world.Actor), (string?)row["classAddress"]);
        Check.That(((string?)row["fullName"])!.Contains("Hero"), "full name");
    }

    [Test]
    public void FindHonoursItsLimit()
    {
        _ = new World();
        var result = Wire.Run(Tools, "ue_find", Args(("limit", 2)));
        Check.Equal(2, result["objects"]!.AsArray().Count);
        Check.That((int)result["total"]! > 2, "total counts everything");
        Check.That(Wire.Fails(Tools, "ue_find", Args(("limit", 0))).Contains("between"), "limit range");
    }

    [Test]
    public void FindSaysWhenThereAreNoObjects()
    {
        TestMemory.Clear();
        TestMemory.Write(TestMemory.Alloc(8), 0);
        Check.That(Wire.Fails(Tools, "ue_find", new JsonObject()).Contains("GObjects"), "attached memory without a UE world says GObjects is not resolved");
    }

    [Test]
    public void ClassListsPropertiesByNameOrAddress()
    {
        var world = new World();
        foreach (var key in new[] { "Actor", Hex.Of(world.Actor) })
        {
            var result = Wire.Run(Tools, "ue_class", Args(("class", key)));
            Check.Equal("Actor", (string?)result["name"]);
            Check.Equal(0x70, (int)result["size"]!);
            var props = result["properties"]!.AsArray();
            Check.Equal("Health,Target,Spot,Alive", string.Join(',', props.Select(prop => (string?)prop!["name"])));
            Check.Equal("0x30", (string?)props[0]!["offset"]);
            Check.Equal("FloatProperty", (string?)props[0]!["type"]);
            Check.Equal("Vec2", (string?)props[2]!["struct"]);
            Check.Equal("0x2", (string?)props[3]!["boolMask"]);
        }
        Check.That(Wire.Fails(Tools, "ue_class", Args(("class", "Nope"))).Contains("not found"), "unknown name");
        Check.That(Wire.Fails(Tools, "ue_class", Args(("class", "0x10"))).Contains("not found"), "bad address");
    }

    [Test]
    public void ClassShowsTheSuperChain()
    {
        var ue = new TestUe();
        var animal = ue.Class("Animal", 0, 0x40, new TestProp("Legs", "IntProperty", 0x30));
        var dog = ue.Class("Dog", animal, 0x48, new TestProp("Bark", "IntProperty", 0x40));
        var result = Wire.Run(Tools, "ue_class", Args(("class", "Dog")));
        Check.Equal("Animal", (string?)result["super"]![0]);
        Check.Equal("Legs,Bark", string.Join(',', result["properties"]!.AsArray().Select(prop => (string?)prop!["name"])));
        _ = dog;
    }

    [Test]
    public void ClassByNameFindsBlueprintClasses()
    {
        var ue = new TestUe();
        var widgetBlueprintClass = ue.Class("WidgetBlueprintGeneratedClass", ue.MetaClass);
        ue.BlueprintClass("UIPageHost_C", widgetBlueprintClass, ue.Class("UserWidget"));
        var result = Wire.Run(Tools, "ue_class", Args(("class", "UIPageHost_C")));
        Check.Equal("UserWidget", (string?)result["super"]![0]);
    }

    [Test]
    public void ObjectFormatsFieldsAndExpandsToTheDepth()
    {
        var world = new World();
        var shallow = Wire.Run(Tools, "ue_object", Args(("address", Hex.Of(world.Hero))));
        var fields = shallow["fields"]!.AsArray();
        Check.Equal("12.5", (string?)fields[0]!["value"]);
        Check.Equal("Actor Villain", (string?)fields[1]!["value"]);
        Check.Equal(Hex.Of(world.Villain), (string?)fields[1]!["address"]);
        Check.Equal("true", (string?)fields[3]!["value"]);
        Check.That(fields[1]!["object"] is null && fields[2]!["fields"] is null, "depth 1 does not expand");

        var deep = Wire.Run(Tools, "ue_object", Args(("address", Hex.Of(world.Hero)), ("depth", 2)));
        var target = deep["fields"]![1]!["object"]!;
        Check.That(((string?)target["name"])!.Contains("Villain"), "pointer expanded");
        Check.That(target["fields"]![1]!["object"] is null, "the pointer back to the root is not followed again");
        Check.Equal("3", (string?)deep["fields"]![2]!["fields"]![0]!["value"]);
        Check.Equal("4", (string?)deep["fields"]![2]!["fields"]![1]!["value"]);
    }

    [Test]
    public void NonObjectAddressIsAToolError()
    {
        var world = new World();
        Check.That(Wire.Fails(Tools, "ue_object", Args(("address", "0x10"))).Contains("not a UObject"), "garbage");
        Check.That(Wire.Fails(Tools, "ue_object", Args(("address", Hex.Of(world.Hero)), ("depth", 9))).Contains("between"), "depth range");
        Check.That(Wire.Fails(Tools, "ue_object", Args(("address", "xyz"))).Contains("hex"), "not hex");
    }

    [Test]
    public void NegativeStructSizeIsSkippedNotThrown()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vec2", 8, new TestProp("X", "FloatProperty", 0));
        var holder = ue.Class("Holder", 0, 0x50, new TestProp("Bad", "StructProperty", 0x40, -4, 0, vector));
        var instance = ue.Object("Instance", holder);
        var result = Wire.Run(Tools, "ue_object", Args(("address", Hex.Of(instance)), ("depth", 2)));
        Check.That(result["fields"]![0]!["fields"] is null, "no expansion for a negative size");
    }

    [Test]
    public void HugeStructSizeIsSkippedNotThrown()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vec2", 8, new TestProp("X", "FloatProperty", 0));
        var holder = ue.Class("Holder", 0, 0x50, new TestProp("Huge", "StructProperty", 0x40, int.MaxValue, 0, vector));
        var instance = ue.Object("Instance", holder);
        var result = Wire.Run(Tools, "ue_object", Args(("address", Hex.Of(instance)), ("depth", 2)));
        Check.That(result["fields"]![0]!["fields"] is null, "no expansion for an overflowing size");
    }

    [Test]
    public void NameResolvesIndices()
    {
        var ue = new TestUe();
        var index = ue.Name("Skeleton");
        Check.Equal("Skeleton", (string?)Wire.Run(Tools, "ue_name", Args(("index", index)))["name"]);
        Check.That(Wire.Fails(Tools, "ue_name", Args(("index", 1_000_000))).Contains("no name"), "unknown index");
        Check.That(Wire.Fails(Tools, "ue_name", Args(("index", -1))).Contains("between"), "negative");
    }
}
