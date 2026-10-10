using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Host.Ui;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFont(string File, float Size);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFonts
{
    public ThemeFont Regular { get; init; } = new("segoeui.ttf", 15);

    public ThemeFont Mono { get; init; } = new("JetBrainsMono-Regular.ttf", 15);
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFile
{
    public Dictionary<string, Color> Colors { get; init; } = [];

    public Dictionary<string, JsonElement> Style { get; init; } = [];

    public ThemeFonts Fonts { get; init; } = new();
}

internal sealed class ThemeStore
{
    static readonly FieldInfo[] StyleFields = [.. Enum.GetNames<ImGuiStyleVar>().Select(name => typeof(ImGuiStyle).GetField(name)).OfType<FieldInfo>()];

    readonly string _themes;
    readonly string _fonts;
    readonly Action<Action> _beforeFrame;
    readonly Dictionary<string, ImFontPtr> _loaded = new(StringComparer.OrdinalIgnoreCase);
    ImGuiStyle _defaults;

    public ThemeStore(string themesDirectory, string fontsDirectory, string name, Action<Action> beforeFrame)
    {
        _themes = themesDirectory;
        _fonts = fontsDirectory;
        _beforeFrame = beforeFrame;
        var theme = Read(name);
        if (theme is null) Log.Warn($"theme {name} not found, using Torchlight");
        Name = theme is null ? "Torchlight" : name;
        Current = theme ?? Read("Torchlight") ?? new ThemeFile();
    }

    public string Name { get; private set; }

    public ThemeFile Current { get; private set; }

    public IReadOnlyList<string> Names() =>
        Directory.Exists(_themes) ? [.. Directory.EnumerateFiles(_themes, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)] : [];

    public IReadOnlyList<string> FontFiles() =>
    [
        .. new[] { _fonts, Environment.GetFolderPath(Environment.SpecialFolder.Fonts) }
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory))
            .Where(file => file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    public unsafe void Apply()
    {
        _defaults = *ImGui.GetStyle().Handle;
        _loaded.Clear();
        ApplyLook();
        LoadFonts(Current.Fonts);
    }

    public void Use(string name) => _beforeFrame(() =>
    {
        if (Read(name) is not { } theme)
        {
            Log.Warn($"theme {name} not found");
            return;
        }
        Name = name;
        Current = theme;
        ApplyLook();
        LoadFonts(theme.Fonts);
    });

    public void UseFonts(ThemeFonts fonts) => _beforeFrame(() =>
    {
        Current = Current with { Fonts = fonts };
        LoadFonts(fonts);
    });

    public void Save() => SaveAs(Name);

    public unsafe void SaveAs(string name)
    {
        object look = *ImGui.GetStyle().Handle;
        var saved = Current with
        {
            Style = StyleFields.ToDictionary(field => field.Name, field => JsonSerializer.SerializeToElement(field.GetValue(look) is Vector2 pair ? new[] { pair.X, pair.Y } : field.GetValue(look))),
            Colors = Enum.GetValues<ImGuiCol>().Where(col => col != ImGuiCol.Count).ToDictionary(col => col.ToString(), col => EColor.FromVector4(ImGui.GetStyle().Colors[(int)col])),
        };
        var path = Path.Combine(_themes, name + ".json");
        try
        {
            Directory.CreateDirectory(_themes);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(saved, SettingsJson.Options));
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"could not save theme {name} - {ex.Message}");
            return;
        }
        Name = name;
        Current = saved;
    }

    ThemeFile? Read(string theme)
    {
        var path = Path.Combine(_themes, theme + ".json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(path), SettingsJson.Options);
        }
        catch (JsonException ex)
        {
            Log.Warn($"theme {theme} is not valid JSON - {ex.Message}");
            return null;
        }
        catch (IOException ex)
        {
            Log.Warn($"theme {theme} could not be read - {ex.Message}");
            return null;
        }
    }

    unsafe void ApplyLook()
    {
        var style = ImGui.GetStyle().Handle;
        var size = style->FontSizeBase;
        object look = _defaults;
        foreach (var (key, value) in Current.Style)
        {
            if (StyleFields.FirstOrDefault(field => field.Name == key) is not { } field)
            {
                Log.Warn($"theme {Name} - unknown style {key}");
                continue;
            }
            try
            {
                field.SetValue(look, field.FieldType == typeof(Vector2) ? (object)new Vector2(value[0].GetSingle(), value[1].GetSingle()) : value.GetSingle());
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or IndexOutOfRangeException)
            {
                Log.Warn($"theme {Name} - style {key} - {ex.Message}");
            }
        }
        *style = (ImGuiStyle)look;
        style->FontSizeBase = size;
        foreach (var (key, colour) in Current.Colors)
        {
            if (Enum.TryParse<ImGuiCol>(key, out var col) && Enum.IsDefined(col) && col != ImGuiCol.Count) ImGui.GetStyle().Colors[(int)col] = EColor.ToVector4(colour);
            else Log.Warn($"theme {Name} - unknown colour {key}");
        }
    }

    void LoadFonts(ThemeFonts fonts)
    {
        var io = ImGui.GetIO();
        ImFontPtr[] picked =
        [
            .. new[] { fonts.Regular, fonts.Mono }.Select(font =>
            {
                if (_loaded.TryGetValue(font.File, out var loaded)) return loaded;
                var path = new[] { _fonts, Environment.GetFolderPath(Environment.SpecialFolder.Fonts) }.Select(directory => Path.Combine(directory, font.File)).FirstOrDefault(File.Exists);
                var added = path is null ? default : ImGui.AddFontFromFileTTF(io.Fonts, path, font.Size);
                if (added.IsNull)
                {
                    Log.Warn($"theme {Name} - font {font.File} not found, using the default font");
                    added = ImGui.AddFontDefault(io.Fonts);
                }
                return _loaded[font.File] = added;
            }),
        ];
        io.FontDefault = picked[0];
        Fonts.Regular = picked[0];
        Fonts.RegularSize = fonts.Regular.Size;
        Fonts.Mono = picked[1];
        Fonts.MonoSize = fonts.Mono.Size;
        foreach (var stale in _loaded.Keys.Where(file => !file.Equals(fonts.Regular.File, StringComparison.OrdinalIgnoreCase) && !file.Equals(fonts.Mono.File, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            ImGui.RemoveFont(io.Fonts, _loaded[stale]);
            _loaded.Remove(stale);
        }
        ImGui.GetStyle().FontSizeBase = fonts.Regular.Size;
    }
}
