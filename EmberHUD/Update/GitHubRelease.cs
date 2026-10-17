using System.Text.Json;

namespace EmberHUD.Host.Update;

internal sealed record ReleaseInfo(Version Version, string ZipName, string ZipUrl, string ZipSha256, string ManifestUrl, string ManifestSha256);

internal static class GitHubRelease
{
    public static ReleaseInfo? Parse(string json, Action<string> report)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement, report);
        }
        catch (JsonException ex)
        {
            report($"release json unreadable: {ex.Message}");
            return null;
        }
    }

    private static ReleaseInfo? Read(JsonElement root, Action<string> report)
    {
        if (!TryString(root, "tag_name", out var tag))
        {
            report("release has no tag_name");
            return null;
        }
        if (!Version.TryParse(tag, out var parsed))
        {
            report($"release tag '{tag}' is not a version");
            return null;
        }
        var version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));

        var assets = root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array ? list : default;
        var zip = FindAsset(assets, name => name.StartsWith("EmberHUD-", StringComparison.Ordinal) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (zip is null)
        {
            report("release has no EmberHUD-*.zip asset");
            return null;
        }
        var manifest = FindAsset(assets, name => name == "manifest.json");
        if (manifest is null)
        {
            report("release has no manifest.json asset");
            return null;
        }

        if (!TryDownload(zip.Value, "zip", out var zipName, out var zipUrl, out var zipSha, report)) return null;
        if (!TryDownload(manifest.Value, "manifest", out _, out var manifestUrl, out var manifestSha, report)) return null;
        return new ReleaseInfo(version, zipName, zipUrl, zipSha, manifestUrl, manifestSha);
    }

    private static JsonElement? FindAsset(JsonElement assets, Func<string, bool> named)
    {
        if (assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (TryString(asset, "name", out var name) && named(name)) return asset;
        }
        return null;
    }

    private static bool TryDownload(JsonElement asset, string label, out string name, out string url, out string sha256, Action<string> report)
    {
        sha256 = url = name = "";
        if (!TryString(asset, "name", out name)) return false;
        if (!TryString(asset, "browser_download_url", out url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            report($"{label} asset has no https download url");
            return false;
        }
        if (!TryString(asset, "digest", out var digest) || !digest.StartsWith("sha256:", StringComparison.Ordinal) || !IsSha256Hex(digest["sha256:".Length..]))
        {
            report($"{label} asset has no sha256 digest");
            return false;
        }
        sha256 = digest["sha256:".Length..].ToLowerInvariant();
        return true;
    }

    private static bool IsSha256Hex(string hex) => hex.Length == 64 && hex.All(Uri.IsHexDigit);

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = "";
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var found) || found.ValueKind != JsonValueKind.String) return false;
        value = found.GetString()!;
        return true;
    }
}
