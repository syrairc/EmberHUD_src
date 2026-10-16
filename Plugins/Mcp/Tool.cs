using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal sealed class ToolError(string message) : Exception(message);

internal sealed record Tool(string Name, string Description, JsonObject Schema, Func<Args, JsonObject> Run, bool ReadOnly = true, bool Idempotent = true);

internal sealed class Args(JsonObject? source)
{
    public string Str(string name) => OptStr(name) ?? throw new ToolError($"{name} is required (string)");

    public string? OptStr(string name) => source?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public string[] Strings(string name, int min, int max)
    {
        if (source?[name] is not JsonArray array || array.Any(item => item is not JsonValue value || !value.TryGetValue<string>(out _)))
            throw new ToolError($"{name} is required (array of strings)");
        return array.Count >= min && array.Count <= max ? [.. array.Select(item => (string)item!)] : throw new ToolError($"{name} must have {min} to {max} entries");
    }

    public int Int(string name, int fallback, int min, int max)
    {
        var node = source?[name];
        if (node is null) return fallback;
        if (node is not JsonValue value || !value.TryGetValue<int>(out var number)) throw new ToolError($"{name} must be an integer");
        return number >= min && number <= max ? number : throw new ToolError($"{name} must be between {min} and {max}");
    }

    public int RequiredInt(string name, int min, int max) => source?[name] is null ? throw new ToolError($"{name} is required (integer)") : Int(name, 0, min, max);

    public int? OptInt(string name) => source?[name] is null ? null : Int(name, 0, int.MinValue, int.MaxValue);

    public float? OptFloat(string name) => source?[name] is JsonValue { } value && value.GetValueKind() == System.Text.Json.JsonValueKind.Number ? float.Parse(value.ToJsonString(), CultureInfo.InvariantCulture) : null;

    public bool Bool(string name, bool fallback) => source?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : fallback;

    public nint Addr(string name) => Parse(Str(name), name);

    public nint? OptAddr(string name) => OptStr(name) is { } text ? Parse(text, name) : null;

    static nint Parse(string text, string name)
    {
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) ? (nint)value : throw new ToolError($"{name} must be a hex address like 0x1F2E3D4C");
    }
}

internal static class Hex
{
    public static string Of(nint address) => "0x" + ((ulong)address).ToString("X");
}

internal static class Num
{
    public static JsonNode? F(float value) => float.IsFinite(value) ? JsonValue.Create(Math.Round(value, 2)) : null;

    public static void Write(Utf8JsonWriter json, string name, float value)
    {
        if (float.IsFinite(value)) json.WriteNumber(name, Math.Round(value, 2));
        else json.WriteNull(name);
    }

    public static void Write(Utf8JsonWriter json, string name, double value) => json.WriteNumber(name, Math.Round(value, 1));
}

internal static class Schema
{
    public static JsonObject Of(string[] required, params (string Name, string Type, string Description)[] properties)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(properties.Select(property => KeyValuePair.Create(property.Name, (JsonNode?)new JsonObject { ["type"] = property.Type, ["description"] = property.Description }))),
            ["additionalProperties"] = false,
        };
        if (required.Length > 0) schema["required"] = new JsonArray(required.Select(name => (JsonNode)name).ToArray());
        return schema;
    }
}

internal static class Live
{
    public static void Require()
    {
        if (!Memory.Attached) throw new ToolError("memory is not attached - run EmberHUD live with the game open");
    }
}
