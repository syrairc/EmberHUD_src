using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TorchlightSDK.Api.Tests;

public sealed class SettingsJsonTests
{
    public sealed class Tab
    {
        public ToggleNode Walls = new(true);
        public RangeNode<float> Size = new(4, 1, 8);
    }

    public sealed class Tabs
    {
        public Tab Inner = new();
    }

    public sealed class Sample : PluginSettings
    {
        public ToggleNode Show = new(true);
        public RangeNode<float> Zoom = new(8, 1, 40);
        public RangeNode<int> Count = new(3, 0, 10);
        public ColorNode Wall = new(Color.FromArgb(255, 0xD8, 0xD0, 0x90));
        public HotkeyNode Toggle = new(new Hotkey(Key.F10));
        public TextNode Name = new("map");
        public ChoiceNode Mode = new("Fit", "Fit", "Follow");
        public List<string> Hidden = ["a"];
        public Tab Map = new();
        public Tabs Deep = new();
        [JsonIgnore] public int Transient = 5;
    }

    public sealed class Unbound : PluginSettings
    {
        public HotkeyNode Toggle = new(default);
    }

    public sealed class L4 { public ToggleNode On = new(true); }
    public sealed class L3 { public L4 Next = new(); }
    public sealed class L2 { public L3 Next = new(); }
    public sealed class L1 { public L2 Next = new(); }
    public sealed class Four : PluginSettings { public L1 Top = new(); }
    public sealed class L5 { public L1 Next = new(); }
    public sealed class Five : PluginSettings { public L5 Top = new(); }

    public sealed class NullHolder : PluginSettings
    {
        public Tab? Map = null;
        public ToggleNode Show = new(true);
    }

    public sealed class Positioned : PluginSettings
    {
        public ToggleNode Show = new(true);
        public Vector2 Spot = new(40, 400);
    }

    readonly List<string> _reports = [];

    Sample Load(string? defaults, string? user) => (Sample)SettingsJson.Load(typeof(Sample), defaults, user, _reports.Add);

    [Fact]
    public void SerializeWritesNodesAsBareValues()
    {
        var json = JsonNode.Parse(SettingsJson.Serialize(new Sample()))!.AsObject();
        Assert.True(json["Show"]!.GetValue<bool>());
        Assert.Equal(8f, json["Zoom"]!.GetValue<float>());
        Assert.Equal(3, json["Count"]!.GetValue<int>());
        Assert.Equal("#D8D090FF", json["Wall"]!.GetValue<string>());
        Assert.Equal("F10", json["Toggle"]!.GetValue<string>());
        Assert.Equal("map", json["Name"]!.GetValue<string>());
        Assert.Equal("Fit", json["Mode"]!.GetValue<string>());
        Assert.Equal("a", json["Hidden"]![0]!.GetValue<string>());
        Assert.Equal(4f, json["Map"]!["Size"]!.GetValue<float>());
        Assert.True(json["Deep"]!["Inner"]!["Walls"]!.GetValue<bool>());
        Assert.False(json.ContainsKey("Transient"));
    }

    [Fact]
    public void ColoursAreRgbaHex()
    {
        var colour = JsonSerializer.Deserialize<Color>("\"#11223344\"", SettingsJson.Options);
        Assert.Equal(Color.FromArgb(0x44, 0x11, 0x22, 0x33).ToArgb(), colour.ToArgb());
        Assert.Equal("\"#11223344\"", JsonSerializer.Serialize(colour, SettingsJson.Options));
        foreach (var bad in new[] { "\"#123\"", "\"11223344\"", "\"#GG223344\"", "12" })
            Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Color>(bad, SettingsJson.Options));
    }

    [Fact]
    public void HotkeysAreTheirTextForm()
    {
        Assert.Equal(new Hotkey(Key.F12, KeyModifiers.Ctrl), JsonSerializer.Deserialize<Hotkey>("\"Ctrl+F12\"", SettingsJson.Options));
        Assert.Equal("\"Ctrl+Shift+A\"", JsonSerializer.Serialize(new Hotkey(Key.A, KeyModifiers.Ctrl | KeyModifiers.Shift), SettingsJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Hotkey>("\"Ctrl+\"", SettingsJson.Options));
    }

    [Fact]
    public void AVectorFieldLoadsFromAnXYObjectAndALaterLayerReplacesIt()
    {
        var fromDefaults = (Positioned)SettingsJson.Load(typeof(Positioned), """{ "Spot": { "X": 7, "Y": 9 } }""", null, _reports.Add);
        Assert.Equal(new Vector2(7, 9), fromDefaults.Spot);
        var fromUser = (Positioned)SettingsJson.Load(typeof(Positioned), """{ "Spot": { "X": 7, "Y": 9 } }""", """{ "Spot": { "X": 1, "Y": 2 } }""", _reports.Add);
        Assert.Equal(new Vector2(1, 2), fromUser.Spot);
        Assert.Empty(_reports);
    }

    [Fact]
    public void ABadVectorFieldKeepsItsDefaultAndReports()
    {
        var settings = (Positioned)SettingsJson.Load(typeof(Positioned), """{ "Spot": "left" }""", null, _reports.Add);
        Assert.Equal(new Vector2(40, 400), settings.Spot);
        Assert.StartsWith("Spot - ", Assert.Single(_reports));
    }

    [Fact]
    public void LoadLayersCodeDefaultsThenDefaultsJsonThenTheUserFile()
    {
        var settings = Load("""{ "Zoom": 12, "Count": 5, "Map": { "Size": 6, "Walls": false } }""", """{ "Zoom": 20, "Map": { "Size": 7 } }""");
        Assert.Equal(20, settings.Zoom.Value);
        Assert.Equal(5, settings.Count.Value);
        Assert.Equal(7, settings.Map.Size.Value);
        Assert.False(settings.Map.Walls.Value);
        Assert.True(settings.Show.Value);
        Assert.Empty(_reports);
    }

    [Fact]
    public void NestedHoldersKeepTheirBoundsAndNestDeeper()
    {
        var settings = Load(null, """{ "Map": { "Size": 100 }, "Deep": { "Inner": { "Size": 5, "Walls": false } } }""");
        Assert.Equal(8, settings.Map.Size.Value);
        Assert.Equal(8, settings.Map.Size.Max);
        Assert.Equal(5, settings.Deep.Inner.Size.Value);
        Assert.False(settings.Deep.Inner.Walls.Value);
    }

    [Fact]
    public void EachBadFieldKeepsItsDefaultAndReportsOnce()
    {
        var settings = Load(null, """{ "Zoom": "big", "Wall": "#nope", "Toggle": "Ctrl+", "Hidden": 5, "Map": { "Size": "x" }, "Deep": 3, "Count": 4 }""");
        Assert.Equal(8, settings.Zoom.Value);
        Assert.Equal(Color.FromArgb(255, 0xD8, 0xD0, 0x90).ToArgb(), settings.Wall.Value.ToArgb());
        Assert.Equal(new Hotkey(Key.F10), settings.Toggle.Value);
        Assert.Equal(["a"], settings.Hidden);
        Assert.Equal(4, settings.Map.Size.Value);
        Assert.Equal(4, settings.Count.Value);
        Assert.Equal(["Zoom", "Wall", "Toggle", "Hidden", "Map.Size", "Deep"], _reports.Select(report => report.Split(" - ")[0]));
    }

    [Fact]
    public void RangesClampAfterLoad()
    {
        var settings = Load(null, """{ "Zoom": 400, "Count": -3 }""");
        Assert.Equal(40, settings.Zoom.Value);
        Assert.Equal(0, settings.Count.Value);
    }

    [Fact]
    public void UnreadableJsonIsReportedAndTheOtherLayerStillApplies()
    {
        var settings = Load("""{ "Zoom": 12 }""", "{ nope");
        Assert.Equal(12, settings.Zoom.Value);
        Assert.Contains("not valid JSON", Assert.Single(_reports));
    }

    [Fact]
    public void UnknownKeysAndNullsAreIgnored()
    {
        var settings = Load(null, """{ "Nope": 1, "Zoom": null, "Transient": 9 }""");
        Assert.Equal(8, settings.Zoom.Value);
        Assert.Equal(5, settings.Transient);
        Assert.Empty(_reports);
    }

    [Fact]
    public void WhatSerializeWritesLoadsBackTheSame()
    {
        var changed = new Sample();
        changed.Show.Value = false;
        changed.Zoom.Value = 12.5f;
        changed.Wall.Value = Color.FromArgb(10, 20, 30, 40);
        changed.Toggle.Value = new Hotkey(Key.F5, KeyModifiers.Alt);
        changed.Mode.Value = "Follow";
        changed.Hidden.Add("b");
        changed.Map.Size.Value = 2;
        var loaded = Load(null, SettingsJson.Serialize(changed));
        Assert.Equal(SettingsJson.Serialize(changed), SettingsJson.Serialize(loaded));
        Assert.Empty(_reports);
    }

    [Fact]
    public void MergeIsDeepForObjectsAndReplacesEverythingElse()
    {
        var merged = SettingsJson.Merge(JsonNode.Parse("""{ "a": [1, 2], "o": { "x": 1, "y": 2 }, "v": 1 }"""), JsonNode.Parse("""{ "a": [3], "o": { "y": 5 }, "w": 2 }"""));
        Assert.Equal("""{"a":[3],"o":{"x":1,"y":5},"v":1,"w":2}""", merged!.ToJsonString());
    }

    [Fact]
    public void AnUnboundHotkeyRoundTripsWithoutAReport()
    {
        var text = SettingsJson.Serialize(new Unbound());
        var loaded = (Unbound)SettingsJson.Load(typeof(Unbound), null, text, _reports.Add);
        Assert.Equal(default, loaded.Toggle.Value);
        Assert.Empty(_reports);
    }

    [Fact]
    public void SerializeKeepsPlusSignsLiteral()
    {
        var sample = new Sample();
        sample.Toggle.Value = new Hotkey(Key.F12, KeyModifiers.Ctrl);
        Assert.Contains("\"Ctrl+F12\"", SettingsJson.Serialize(sample));
    }

    [Fact]
    public void ANonObjectLayerIsIgnoredAloneAndReportedOnce()
    {
        var settings = Load("""{ "Zoom": 12 }""", "[1]");
        Assert.Equal(12, settings.Zoom.Value);
        Assert.Contains("not a JSON object", Assert.Single(_reports));
        _reports.Clear();
        settings = Load("5", """{ "Zoom": 20 }""");
        Assert.Equal(20, settings.Zoom.Value);
        Assert.Contains("not a JSON object", Assert.Single(_reports));
    }

    [Fact]
    public void HoldersNestFourLevelsDownAndNoFurther()
    {
        var four = JsonNode.Parse(SettingsJson.Serialize(new Four()))!;
        Assert.True(four["Top"]!["Next"]!["Next"]!["Next"]!["On"]!.GetValue<bool>());
        var five = JsonNode.Parse(SettingsJson.Serialize(new Five()))!;
        Assert.IsType<JsonObject>(five["Top"]!["Next"]!["Next"]!["Next"]!["Next"]!["On"]);
    }

    [Fact]
    public void ANullHolderFieldReportsAndTheRestStillLoads()
    {
        var settings = (NullHolder)SettingsJson.Load(typeof(NullHolder), null, """{ "Map": { "Size": 2 }, "Show": false }""", _reports.Add);
        Assert.False(settings.Show.Value);
        Assert.Equal("Map - expected an object", Assert.Single(_reports));
    }
}
