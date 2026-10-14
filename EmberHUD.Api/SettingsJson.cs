using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EmberHUD;

internal static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new ColorJson(), new HotkeyJson(), new JsonStringEnumConverter() },
    };

    public static PluginSettings Load(Type type, string? defaultsJson, string? userJson, Action<string> report)
    {
        var settings = (PluginSettings)Activator.CreateInstance(type)!;
        var merged = Merge(Parse(defaultsJson, "defaults.json"), Parse(userJson, "the settings file"));
        if (merged is JsonObject root) Populate(settings, root, "", report);
        return settings;

        JsonNode? Parse(string? json, string source)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var node = JsonNode.Parse(json);
                if (node is null or JsonObject) return node;
                report($"{source} is not a JSON object, ignored");
                return null;
            }
            catch (JsonException ex)
            {
                report($"{source} is not valid JSON, ignored - {ex.Message}");
                return null;
            }
        }
    }

    public static string Serialize(PluginSettings settings) => ToJson(settings).ToJsonString(Options);

    internal static JsonNode? Merge(JsonNode? under, JsonNode? over)
    {
        if (under is JsonObject below && over is JsonObject above)
        {
            var merged = (JsonObject)below.DeepClone();
            foreach (var (key, value) in above) merged[key] = Merge(merged[key], value);
            return merged;
        }
        return (over ?? under)?.DeepClone();
    }

    internal static IEnumerable<FieldInfo> Fields(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(field => !field.IsDefined(typeof(JsonIgnoreAttribute))).OrderBy(field => field.MetadataToken);

    static bool IsHolder(Type type) =>
        type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type) && Fields(type).Any();

    static void Populate(object holder, JsonObject json, string prefix, Action<string> report)
    {
        foreach (var field in Fields(holder.GetType()))
        {
            if (!json.TryGetPropertyValue(field.Name, out var value) || value is null) continue;
            try
            {
                if (IsHolder(field.FieldType) && field.GetValue(holder) is { } current && value is JsonObject nested) Populate(current, nested, $"{prefix}{field.Name}.", report);
                else field.SetValue(holder, value.Deserialize(field.FieldType, Options));
            }
            catch (Exception ex)
            {
                report($"{prefix}{field.Name} - {ex.Message}");
            }
        }
    }

    static JsonObject ToJson(object holder)
    {
        var json = new JsonObject();
        foreach (var field in Fields(holder.GetType()))
        {
            var value = field.GetValue(holder);
            json[field.Name] = value is null ? null : IsHolder(field.FieldType) ? ToJson(value) : JsonSerializer.SerializeToNode(value, field.FieldType, Options);
        }
        return json;
    }

    sealed class ColorJson : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (text is not { Length: 9 } || text[0] != '#' || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgba))
                throw new JsonException($"'{text}' is not a #RRGGBBAA colour");
            return Color.FromArgb((int)((rgba & 0xFF) << 24 | rgba >> 8));
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}");
    }

    sealed class HotkeyJson : JsonConverter<Hotkey>
    {
        public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            return Hotkey.TryParse(text ?? "", out var hotkey) ? hotkey : throw new JsonException($"'{text}' is not a hotkey");
        }

        public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }
}
