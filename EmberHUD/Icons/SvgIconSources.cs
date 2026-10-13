using System.Reflection;
using System.Text.RegularExpressions;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Host.Icons;

internal static class SvgIconSources
{
    const string ResourcePrefix = "icon:";

    static readonly Regex External = new("""@import|(?:xlink:)?href\s*=\s*["'](?!\s*#)|url\(\s*["']?(?!\s*["']?#)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<(string Name, string Svg)> BuiltIns(Assembly host) =>
        host.GetManifestResourceNames().Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Select(resource => (resource[ResourcePrefix.Length..], Read(host, resource)));

    public static Dictionary<string, string> Load(IEnumerable<(string Name, string Svg)> builtIns, string overrideDirectory, string pluginsDirectory)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, svg) in builtIns) Add(sources, Draw.SvgIconPrefix + name, svg, $"built-in {name}");
        AddFolder(sources, overrideDirectory, "");
        foreach (var plugin in PluginCache.Plugins(pluginsDirectory)) AddFolder(sources, Path.Combine(plugin, "Icons"), Path.GetFileName(plugin) + "/");
        return sources;
    }

    public static string? ExternalReference(string svg) => External.Match(svg) is { Success: true } match ? match.Value : null;

    static string Read(Assembly host, string resource)
    {
        using var reader = new StreamReader(host.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    static void AddFolder(Dictionary<string, string> sources, string directory, string prefix)
    {
        if (!Directory.Exists(directory)) return;
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.svg");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"svg icon folder {directory} skipped - {ex.Message}");
            return;
        }
        foreach (var file in files)
        {
            try
            {
                if (new FileInfo(file).Length > 256 * 1024) Log.Warn($"svg icon {file} skipped - over 256 KB");
                else Add(sources, Draw.SvgIconPrefix + prefix + Path.GetFileNameWithoutExtension(file), File.ReadAllText(file), file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"svg icon {file} skipped - {ex.Message}");
            }
        }
    }

    static void Add(Dictionary<string, string> sources, string key, string svg, string origin)
    {
        if (ExternalReference(svg) is { } reference) Log.Warn($"svg icon {origin} skipped - external reference {reference}");
        else sources[key] = svg;
    }
}
