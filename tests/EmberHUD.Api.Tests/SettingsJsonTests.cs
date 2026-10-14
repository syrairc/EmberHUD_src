using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EmberHUD.Ui;

namespace EmberHUD.Api.Tests;

public sealed class SettingsJsonTests
{
    public enum SampleMode { Fit, Follow }

    public sealed class Tab
    {
        public bool Walls = true;
        public float Size = 4;
    }

    public sealed class Tabs
    {
        public Tab Inner = new();
    }

    public sealed class Sample : PluginSettings
    {
        public override void Draw() { }

        public bool Show = true;
        public float Zoom = 8;
        public int Count = 3;
        public Color Wall = Color.FromArgb(255, 0xD8, 0xD0, 0x90);
        public Hotkey Toggle = new(Key.F10);
        public string Name = "map";
        public SampleMode Mode = SampleMode.Fit;
        public List<string> Hidden = ["a"];
        public Tab Map = new();
        public Tabs Deep = new();
        [JsonIgnore] public int Transient = 5;
    }

    public sealed class Unbound : PluginSettings
    {
        public override void Draw() { }

        public Hotkey Toggle;
    }

    public sealed class NullHolder : PluginSettings
    {
        public override void Draw() { }

        public Tab? Map = null;
        public bool Show = true;
    }

    public sealed class Positioned : PluginSettings
    {
        public override void Draw() { }

        public bool Show = true;
        public Vector2 Spot = new(40, 400);
    }

    public sealed class Styled : PluginSettings
    {
        public override void Draw() { }

        public TextStyle Label = TextStyle.WorldLabel();
        public Dictionary<string, int> Counts = new() { ["a"] = 1 };
    }

    public sealed class Row
    {
        public string Key = "";
        public Color Colour = Color.White;
        public float Scale = 1;
    }

    public sealed class WithRows : PluginSettings
    {
        public override void Draw() { }

        public bool On = true;
        public List<Row> Rows = [];
    }

    public enum PlainMode { Region, Walls }

    public sealed class PlainTab
    {
        public bool Show = true;
        public int Count = 3;
        public float Zoom = 8;
        public Color Wall = Color.FromArgb(255, 0xD8, 0xD0, 0x90);
        public Hotkey Toggle = new(Key.F10);
        public string Name = "map";
        public PlainMode Mode = PlainMode.Region;
        public TextStyle Label = TextStyle.Hud();
    }

    public sealed class Plain : PluginSettings
    {
        public PlainTab Tab = new();

        public override void Draw() { }
    }

    readonly List<string> _reports = [];

    Sample Load(string? defaults, string? user) => (Sample)SettingsJson.Load(typeof(Sample), defaults, user, _reports.Add);

    [Fact]
    public void UnknownTopLevelKeysAreIgnored()
    {
        var loaded = (WithRows)SettingsJson.Load(typeof(WithRows), null, """{ "View": { "Zoom": 0.5 }, "Markers": { "Portal": { "Radius": 6 } } }""", _reports.Add);

        Assert.True(loaded.On);
        Assert.Empty(loaded.Rows);
        Assert.Empty(_reports);
    }

    [Fact]
    public void AListOfPlainRowsRoundTrips()
    {
        var settings = new WithRows();
        settings.Rows.Add(new Row { Key = "UI_MiniMap_Portal", Colour = Color.FromArgb(0x80, 0x33, 0xCC, 0xFF), Scale = 1.5f });

        var loaded = (WithRows)SettingsJson.Load(typeof(WithRows), null, SettingsJson.Serialize(settings), _reports.Add);

        Assert.Equal("UI_MiniMap_Portal", Assert.Single(loaded.Rows).Key);
        Assert.Equal(Color.FromArgb(0x80, 0x33, 0xCC, 0xFF).ToArgb(), loaded.Rows[0].Colour.ToArgb());
        Assert.Equal(1.5f, loaded.Rows[0].Scale);
        Assert.Empty(_reports);
    }

    [Fact]
    public void SerializeWritesBareValues()
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
        Assert.Equal(20, settings.Zoom);
        Assert.Equal(5, settings.Count);
        Assert.Equal(7, settings.Map.Size);
        Assert.False(settings.Map.Walls);
        Assert.True(settings.Show);
        Assert.Empty(_reports);
    }

    [Fact]
    public void NestedHoldersLoadDeeper()
    {
        var settings = Load(null, """{ "Map": { "Size": 100 }, "Deep": { "Inner": { "Size": 5, "Walls": false } } }""");
        Assert.Equal(100, settings.Map.Size);
        Assert.Equal(5, settings.Deep.Inner.Size);
        Assert.False(settings.Deep.Inner.Walls);
    }

    [Fact]
    public void EachBadFieldKeepsItsDefaultAndReportsOnce()
    {
        var settings = Load(null, """{ "Zoom": "big", "Wall": "#nope", "Toggle": "Ctrl+", "Hidden": 5, "Map": { "Size": "x" }, "Deep": 3, "Count": 4 }""");
        Assert.Equal(8, settings.Zoom);
        Assert.Equal(Color.FromArgb(255, 0xD8, 0xD0, 0x90).ToArgb(), settings.Wall.ToArgb());
        Assert.Equal(new Hotkey(Key.F10), settings.Toggle);
        Assert.Equal(["a"], settings.Hidden);
        Assert.Equal(4, settings.Map.Size);
        Assert.Equal(4, settings.Count);
        Assert.Equal(["Zoom", "Wall", "Toggle", "Hidden", "Map.Size", "Deep"], _reports.Select(report => report.Split(" - ")[0]));
    }

    [Fact]
    public void UnreadableJsonIsReportedAndTheOtherLayerStillApplies()
    {
        var settings = Load("""{ "Zoom": 12 }""", "{ nope");
        Assert.Equal(12, settings.Zoom);
        Assert.Contains("not valid JSON", Assert.Single(_reports));
    }

    [Fact]
    public void UnknownKeysAndNullsAreIgnored()
    {
        var settings = Load(null, """{ "Nope": 1, "Zoom": null, "Transient": 9 }""");
        Assert.Equal(8, settings.Zoom);
        Assert.Equal(5, settings.Transient);
        Assert.Empty(_reports);
    }

    [Fact]
    public void WhatSerializeWritesLoadsBackTheSame()
    {
        var changed = new Sample();
        changed.Show = false;
        changed.Zoom = 12.5f;
        changed.Wall = Color.FromArgb(10, 20, 30, 40);
        changed.Toggle = new Hotkey(Key.F5, KeyModifiers.Alt);
        changed.Mode = SampleMode.Follow;
        changed.Hidden.Add("b");
        changed.Map.Size = 2;
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
        Assert.Equal(default, loaded.Toggle);
        Assert.Empty(_reports);
    }

    [Fact]
    public void SerializeKeepsPlusSignsLiteral()
    {
        var sample = new Sample();
        sample.Toggle = new Hotkey(Key.F12, KeyModifiers.Ctrl);
        Assert.Contains("\"Ctrl+F12\"", SettingsJson.Serialize(sample));
    }

    [Fact]
    public void ANonObjectLayerIsIgnoredAloneAndReportedOnce()
    {
        var settings = Load("""{ "Zoom": 12 }""", "[1]");
        Assert.Equal(12, settings.Zoom);
        Assert.Contains("not a JSON object", Assert.Single(_reports));
        _reports.Clear();
        settings = Load("5", """{ "Zoom": 20 }""");
        Assert.Equal(20, settings.Zoom);
        Assert.Contains("not a JSON object", Assert.Single(_reports));
    }

    [Fact]
    public void APartialObjectForAStyleFieldKeepsThePresetValuesItLeavesOut()
    {
        var settings = (Styled)SettingsJson.Load(typeof(Styled), """{ "Label": { "Scale": 2 } }""", """{ "Label": { "Color": "#7FDBFEFF" } }""", _reports.Add);
        Assert.Equal(2f, settings.Label.Scale);
        Assert.Equal(Color.FromArgb(0x7F, 0xDB, 0xFE).ToArgb(), settings.Label.Color.ToArgb());
        Assert.Equal(FontFace.Label, settings.Label.Face);
        Assert.True(settings.Label.BgEnabled);
        Assert.Equal(GameColors.LabelBackground.ToArgb(), settings.Label.BgColor.ToArgb());
        Assert.Equal(0f, settings.Label.BorderRounding);
        Assert.Empty(_reports);
    }

    [Fact]
    public void ABadFaceNameReportsAndTheStyleKeepsItsPreset()
    {
        var settings = (Styled)SettingsJson.Load(typeof(Styled), null, """{ "Label": { "Face": "Huge", "Scale": 3 } }""", _reports.Add);
        Assert.StartsWith("Label.Face - ", Assert.Single(_reports));
        Assert.Equal(FontFace.Label, settings.Label.Face);
        Assert.Equal(3f, settings.Label.Scale);
    }

    [Fact]
    public void ACollectionFieldIsStillReplacedWhole()
    {
        var settings = (Styled)SettingsJson.Load(typeof(Styled), null, """{ "Counts": { "b": 2 } }""", _reports.Add);
        Assert.Equal(new Dictionary<string, int> { ["b"] = 2 }, settings.Counts);
        Assert.Empty(_reports);
    }

    [Fact]
    public void ANullHolderFieldLoadsWhole()
    {
        var settings = (NullHolder)SettingsJson.Load(typeof(NullHolder), null, """{ "Map": { "Size": 2 }, "Show": false }""", _reports.Add);
        Assert.False(settings.Show);
        Assert.Equal(2f, settings.Map!.Size);
        Assert.Empty(_reports);
    }

    [Fact]
    public void NodeEraJsonLoadsIntoPlainFields()
    {
        var json = """{ "Tab": { "Show": false, "Count": 7, "Zoom": 2.5, "Wall": "#11223344", "Toggle": "Ctrl+F9", "Name": "town", "Mode": "Walls", "Label": { "Scale": 2 } } }""";
        var reports = new List<string>();

        var tab = ((Plain)SettingsJson.Load(typeof(Plain), null, json, reports.Add)).Tab;

        Assert.Empty(reports);
        Assert.False(tab.Show);
        Assert.Equal(7, tab.Count);
        Assert.Equal(2.5f, tab.Zoom);
        Assert.Equal(Color.FromArgb(0x44, 0x11, 0x22, 0x33), tab.Wall);
        Assert.Equal(new Hotkey(Key.F9, KeyModifiers.Ctrl), tab.Toggle);
        Assert.Equal("town", tab.Name);
        Assert.Equal(PlainMode.Walls, tab.Mode);
        Assert.Equal(2f, tab.Label.Scale);
        Assert.Equal(TextStyle.Hud().Face, tab.Label.Face);
    }

    [Fact]
    public void ABadFieldInsideAPlainHolderReportsOnlyThatField()
    {
        var json = """{ "Tab": { "Count": "lots", "Name": "town", "Label": { "Scale": "big", "Face": "Bold" } } }""";
        var reports = new List<string>();

        var tab = ((Plain)SettingsJson.Load(typeof(Plain), null, json, reports.Add)).Tab;

        Assert.Equal(2, reports.Count);
        Assert.Contains(reports, report => report.StartsWith("Tab.Count - "));
        Assert.Contains(reports, report => report.StartsWith("Tab.Label.Scale - "));
        Assert.Equal(3, tab.Count);
        Assert.Equal("town", tab.Name);
        Assert.Equal(FontFace.Bold, tab.Label.Face);
    }

    [Fact]
    public void AnUnknownEnumNameKeepsTheDefaultAndReports()
    {
        var reports = new List<string>();

        var tab = ((Plain)SettingsJson.Load(typeof(Plain), null, """{ "Tab": { "Mode": "Fancy" } }""", reports.Add)).Tab;

        Assert.Equal(PlainMode.Region, tab.Mode);
        Assert.Single(reports);
    }

    [Fact]
    public void PlainFieldsSerializeAsBareValuesAndRoundTrip()
    {
        var saved = new Plain();
        saved.Tab.Count = 9;
        saved.Tab.Mode = PlainMode.Walls;
        var json = SettingsJson.Serialize(saved);

        var tab = JsonNode.Parse(json)!["Tab"]!;
        Assert.Equal(9, (int)tab["Count"]!);
        Assert.Equal("Walls", (string)tab["Mode"]!);
        Assert.Equal("#D8D090FF", (string)tab["Wall"]!);
        var loaded = ((Plain)SettingsJson.Load(typeof(Plain), null, json, _ => Assert.Fail("no report expected"))).Tab;
        Assert.Equal(9, loaded.Count);
        Assert.Equal(PlainMode.Walls, loaded.Mode);
    }
}
