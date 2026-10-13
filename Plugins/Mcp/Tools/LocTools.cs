using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Aember;

namespace Mcp;

internal static class LocTools
{
    const int MaxText = 300;

    public static Tool[] Create()
    {
        var keys = Schema.Of(["keys"], ("keys", "array", "loc keys, 1 to 100"));
        keys["properties"]!["keys"]!["items"] = new JsonObject { ["type"] = "string" };
        return
        [
            new("loc", "Game text for loc keys like level|name|4403, in the game's current language. Unknown keys, or no live game, give null.", keys, Get),
            new("loc_find", "Search the game's current language table for texts containing text (case-insensitive). Returns { matches: [{ key, text }], truncated }, texts over 300 chars cut with ... ; truncated is true when the scan hit its node cap before the end of the table, so a miss may not be final. Empty without a live game.",
                Schema.Of(["text"], ("text", "string", "text to look for"), ("limit", "integer", "matches to return, 1 to 500, default 50")), Find),
        ];
    }

    static JsonObject Get(Args args) => new(args.Strings("keys", 1, 100).Distinct().Select(key => KeyValuePair.Create(key, (JsonNode?)Loc.Get(key))));

    static JsonObject Find(Args args)
    {
        var text = args.Str("text");
        var found = Loc.Find(text, args.Int("limit", 50, 1, 500), out var truncated);
        return new JsonObject
        {
            ["matches"] = new JsonArray([.. found.Select(match => (JsonNode)new JsonObject { ["key"] = match.Key, ["text"] = match.Text.Length > MaxText ? match.Text[..MaxText] + "..." : match.Text })]),
            ["truncated"] = truncated,
        };
    }
}
