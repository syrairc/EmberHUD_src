using System.Linq;
using System.Text.Json.Nodes;
using Aember.Testing;

namespace Mcp;

public sealed class LocToolsTests
{
    static readonly Tool[] Tools = LocTools.Create();

    static void UseTable(params (string Key, string Text)[] table) => UseTable(false, table);

    static void UseTable(bool truncated, params (string Key, string Text)[] table) =>
        TestGame.UseLoc(key => table.FirstOrDefault(row => row.Key == key).Text, (needle, max) => ([.. table.Where(row => row.Text.Contains(needle, System.StringComparison.OrdinalIgnoreCase)).Take(max)], truncated));

    [Test]
    public void LocMapsEachKeyToItsTextOrNull()
    {
        UseTable(("level|name|4403", "Rainforest of Divine Legacy"));
        var result = Wire.Run(Tools, "loc", new JsonObject { ["keys"] = new JsonArray("level|name|4403", "level|name|1", "level|name|4403") });
        TestGame.UseLoc(null, null);
        Check.Equal("Rainforest of Divine Legacy", (string?)result["level|name|4403"]);
        Check.That(result.ContainsKey("level|name|1") && result["level|name|1"] is null, "unknown key is null");
        Check.Equal(2, result.Count);
    }

    [Test]
    public void LocChecksTheKeys()
    {
        Check.That(Wire.Fails(Tools, "loc", new JsonObject()).Contains("keys"), "missing");
        Check.That(Wire.Fails(Tools, "loc", new JsonObject { ["keys"] = new JsonArray() }).Contains("1 to 100"), "empty");
        Check.That(Wire.Fails(Tools, "loc", new JsonObject { ["keys"] = new JsonArray(1, 2) }).Contains("strings"), "not strings");
        Check.That(Wire.Fails(Tools, "loc", new JsonObject { ["keys"] = new JsonArray([.. Enumerable.Range(0, 101).Select(i => (JsonNode)$"k{i}")]) }).Contains("1 to 100"), "too many");
    }

    [Test]
    public void LocFindReturnsMatchesUpToLimitAndCutsLongText()
    {
        UseTable(("a", "forest one"), ("b", "forest " + new string('x', 400)), ("c", "forest three"));
        var all = Wire.Run(Tools, "loc_find", new JsonObject { ["text"] = "FOREST" })["matches"]!.AsArray();
        var two = Wire.Run(Tools, "loc_find", new JsonObject { ["text"] = "forest", ["limit"] = 2 })["matches"]!.AsArray();
        TestGame.UseLoc(null, null);
        Check.Equal(3, all.Count);
        Check.Equal("a", (string?)all[0]!["key"]);
        Check.Equal("forest one", (string?)all[0]!["text"]);
        var cut = (string)all[1]!["text"]!;
        Check.That(cut.Length == 303 && cut.EndsWith("..."), cut.Length.ToString());
        Check.Equal(2, two.Count);
        Check.That(Wire.Fails(Tools, "loc_find", new JsonObject()).Contains("text"), "text is required");
        Check.That(Wire.Fails(Tools, "loc_find", new JsonObject { ["text"] = "a", ["limit"] = 501 }).Contains("between"), "limit range");
    }

    [Test]
    public void LocFindSaysWhenTheScanStoppedEarly()
    {
        UseTable(("a", "forest one"));
        var whole = Wire.Run(Tools, "loc_find", new JsonObject { ["text"] = "forest" });
        UseTable(true, ("a", "forest one"));
        var cut = Wire.Run(Tools, "loc_find", new JsonObject { ["text"] = "forest" });
        TestGame.UseLoc(null, null);
        Check.Equal(false, (bool?)whole["truncated"]);
        Check.Equal(true, (bool?)cut["truncated"]);
        Check.Equal(1, cut["matches"]!.AsArray().Count);
    }

    [Test]
    public void WithoutAGameLocIsNullAndFindIsEmpty()
    {
        TestGame.UseLoc(null, null);
        Check.That(Wire.Run(Tools, "loc", new JsonObject { ["keys"] = new JsonArray("level|name|4403") })["level|name|4403"] is null, "null text");
        Check.Equal(0, Wire.Run(Tools, "loc_find", new JsonObject { ["text"] = "forest" })["matches"]!.AsArray().Count);
    }
}
