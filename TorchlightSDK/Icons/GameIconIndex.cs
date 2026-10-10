using System.Text.Json;

namespace TorchlightSDK.Host.Icons;

internal static class GameIconIndex
{
    public static Dictionary<string, string> Parse(string json)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("icons", out var icons) || icons.ValueKind != JsonValueKind.Object) return files;

        foreach (var icon in icons.EnumerateObject())
        {
            if (icon.Value.ValueKind != JsonValueKind.Object || !icon.Value.TryGetProperty("file", out var file) || file.ValueKind != JsonValueKind.String) continue;
            if (file.GetString() is { Length: > 0 } name && Path.GetFileName(name) == name) files[icon.Name] = name;
        }
        return files;
    }
}
