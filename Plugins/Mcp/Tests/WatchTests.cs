using System.Text.Json.Nodes;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class WatchTests
{
    static JsonObject StartArgs(nint at, int length) => new() { ["address"] = Hex.Of(at), ["length"] = length, ["label"] = "test" };

    [Test]
    public void ChangesBetweenReadsAreReportedAsRuns()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var id = (int)Wire.Run(tools, "watch_start", StartArgs(at, 16))["id"]!;
        watches.Sample();
        TestMemory.Write(at + 2, (byte)0xAA);
        TestMemory.Write(at + 3, (byte)0xBB);
        watches.Sample();
        TestMemory.Write(at + 8, (byte)0x11);
        watches.Sample();
        var read = Wire.Run(tools, "watch_read", new JsonObject { ["id"] = id });
        var changes = read["changes"]!.AsArray();
        Check.Equal(2, changes.Count);
        Check.Equal("0x2", (string?)changes[0]!["offset"]);
        Check.Equal("0000", (string?)changes[0]!["was"]);
        Check.Equal("AABB", (string?)changes[0]!["now"]);
        Check.Equal("0x8", (string?)changes[1]!["offset"]);
        Check.Equal(3, (int)read["samples"]!);
        Check.Equal(2, (int)read["changedSamples"]!);
        Check.Equal("0000AABB000000001100000000000000", (string?)read["current"]);
        Check.Equal(true, (bool)read["readable"]!);
    }

    [Test]
    public void ReadingAdvancesTheBaseline()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var id = (int)Wire.Run(tools, "watch_start", StartArgs(at, 8))["id"]!;
        TestMemory.Write(at, (byte)5);
        watches.Sample();
        Check.Equal(1, Wire.Run(tools, "watch_read", new JsonObject { ["id"] = id })["changes"]!.AsArray().Count);
        watches.Sample();
        var again = Wire.Run(tools, "watch_read", new JsonObject { ["id"] = id });
        Check.Equal(0, again["changes"]!.AsArray().Count);
        Check.Equal(0, (int)again["changedSamples"]!);
    }

    [Test]
    public void ABriefChangeBetweenReadsIsNotLost()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var id = (int)Wire.Run(tools, "watch_start", StartArgs(at, 8))["id"]!;
        TestMemory.Write(at, (byte)9);
        watches.Sample();
        TestMemory.Write(at, (byte)0);
        watches.Sample();
        var read = Wire.Run(tools, "watch_read", new JsonObject { ["id"] = id });
        Check.Equal(1, read["changes"]!.AsArray().Count);
        Check.Equal("00", (string?)read["changes"]![0]!["was"]);
    }

    [Test]
    public void LimitsAndBadInputAreToolErrors()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        for (var i = 0; i < Watches.MaxWatches; i++) Wire.Run(tools, "watch_start", StartArgs(at, 8));
        Check.That(Wire.Fails(tools, "watch_start", StartArgs(at, 8)).Contains("16"), "17th watch");
        watches.StopAll();
        Check.That(Wire.Fails(tools, "watch_start", StartArgs(at, Watches.MaxLength + 1)).Contains("between"), "too long");
        Check.That(Wire.Fails(tools, "watch_start", StartArgs(0x10, 8)).Contains("nothing readable"), "unreadable");
        Check.That(Wire.Fails(tools, "watch_read", new JsonObject { ["id"] = 99 }).Contains("no watch"), "unknown id");
        Check.That(Wire.Fails(tools, "watch_stop", new JsonObject()).Contains("id"), "stop needs id or all");
    }

    [Test]
    public void StopRemovesOneOrAll()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var first = (int)Wire.Run(tools, "watch_start", StartArgs(at, 8))["id"]!;
        Wire.Run(tools, "watch_start", StartArgs(at, 8));
        Wire.Run(tools, "watch_stop", new JsonObject { ["id"] = first });
        Check.Equal(1, watches.Count);
        Check.That(Wire.Fails(tools, "watch_read", new JsonObject { ["id"] = first }).Contains("no watch"), "stopped");
        Wire.Run(tools, "watch_stop", new JsonObject { ["all"] = true });
        Check.Equal(0, watches.Count);
    }

    [Test]
    public void MemoryThatVanishesMarksTheWatchUnreadable()
    {
        var at = Block();
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var id = (int)Wire.Run(tools, "watch_start", StartArgs(at, 8))["id"]!;
        var other = (int)Wire.Run(tools, "watch_start", StartArgs(at, 8))["id"]!;
        TestMemory.Write(at, (byte)7);
        watches.Sample();
        TestMemory.Clear();
        watches.Sample();
        Check.Equal(false, (bool)Wire.Run(tools, "watch_read", new JsonObject { ["id"] = other })["readable"]!);
        var back = TestMemory.Alloc(64);
        Check.Equal(at, back);
        TestMemory.Write(back, 0);
        watches.Sample();
        var after = Wire.Run(tools, "watch_read", new JsonObject { ["id"] = id });
        Check.Equal(true, (bool)after["readable"]!);
        Check.Equal(0, after["changes"]!.AsArray().Count);
    }

    [Test]
    public void AUePropertyWatchResolvesTheOffset()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x60, new TestProp("Health", "FloatProperty", 0x30), new TestProp("Mana", "FloatProperty", 0x38));
        var hero = ue.Object("Hero", actor);
        var watches = new Watches();
        var tools = WatchTools.Create(watches);
        var started = Wire.Run(tools, "watch_start", new JsonObject { ["object"] = Hex.Of(hero), ["property"] = "Mana" });
        Check.Equal(Hex.Of(hero + 0x38), (string?)started["address"]);
        Check.Equal(4, (int)started["length"]!);
        TestMemory.Write(hero + 0x38, 7f);
        watches.Sample();
        Check.Equal(1, Wire.Run(tools, "watch_read", new JsonObject { ["id"] = (int)started["id"]! })["changes"]!.AsArray().Count);
        Check.That(Wire.Fails(tools, "watch_start", new JsonObject { ["object"] = Hex.Of(hero), ["property"] = "Nope" }).Contains("Nope"), "unknown property");
        Check.That(Wire.Fails(tools, "watch_start", new JsonObject { ["object"] = "0x10", ["property"] = "Mana" }).Contains("not a UObject"), "not an object");
    }

    [Test]
    public void OversizedAndGarbagePropertiesAreToolErrors()
    {
        var ue = new TestUe();
        var actor = ue.Class("Actor", 0, 0x60, new TestProp("Big", "ArrayProperty", 0x30, 5000), new TestProp("Huge", "ArrayProperty", 0x30, int.MaxValue), new TestProp("Back", "IntProperty", -8));
        var hero = ue.Object("Hero", actor);
        var tools = WatchTools.Create(new Watches());
        foreach (var name in new[] { "Big", "Huge", "Back" })
            Check.That(Wire.Fails(tools, "watch_start", new JsonObject { ["object"] = Hex.Of(hero), ["property"] = name }).Contains(name), name);
    }
}
