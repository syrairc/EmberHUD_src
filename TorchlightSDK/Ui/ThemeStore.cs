using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Host.Ui;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFont(string File, float Size);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeGradient(
    Color Top,
    Color Bottom,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Color? Highlight = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Color? Text = null);

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFonts
{
    public ThemeFont Regular { get; init; } = new("NotoSans-Regular.ttf", 15);

    public ThemeFont Bold { get; init; } = new("NotoSans-SemiBold.ttf", 16);

    public ThemeFont Label { get; init; } = new("Rajdhani-SemiBold.ttf", 16);

    public ThemeFont Mono { get; init; } = new("JetBrainsMono-Regular.ttf", 15);
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record ThemeFile
{
    public Dictionary<string, Color> Colors { get; init; } = [];

    public Dictionary<string, JsonElement> Style { get; init; } = [];

    public ThemeFonts Fonts { get; init; } = new();

    public Dictionary<string, ThemeGradient> Gradients { get; init; } = [];
}

internal sealed class ThemeStore
{
    static readonly FieldInfo[] StyleFields = [.. Enum.GetNames<ImGuiStyleVar>().Select(name => typeof(ImGuiStyle).GetField(name)).OfType<FieldInfo>()];

    readonly string _themes;
    readonly string _fonts;
    readonly Action<Action> _beforeFrame;
    readonly Dictionary<string, ImFontPtr> _loaded = new(StringComparer.OrdinalIgnoreCase);
    readonly List<GradientRun> _runs = [];
    GradientShade[] _shades = [];
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

    public void UseGradients(Dictionary<string, ThemeGradient> gradients) => _beforeFrame(() =>
    {
        Current = Current with { Gradients = gradients };
        ApplyGradients(warn: false);
    });

    public void Shade() => ThemeGradients.Apply(ImGui.GetDrawData(), _shades, ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]) & 0xFFFFFF, _runs);

    public void Save() => SaveAs(Name);

    public unsafe void SaveAs(string name)
    {
        object look = *ImGui.GetStyle().Handle;
        var saved = Current with
        {
            Gradients = Current.Gradients.Where(entry => !(TryColour(entry.Key, out var col) && !entry.Value.Top.IsEmpty && !entry.Value.Bottom.IsEmpty && !Tagged(col))).ToDictionary(),
            Style = StyleFields.ToDictionary(field => field.Name, field => JsonSerializer.SerializeToElement(field.GetValue(look) is Vector2 pair ? new[] { pair.X, pair.Y } : field.GetValue(look))),
            Colors = Enum.GetValues<ImGuiCol>().Where(col => col != ImGuiCol.Count && !Tagged(col)).ToDictionary(col => col.ToString(), col => EColor.FromVector4(ImGui.GetStyle().Colors[(int)col])),
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
            return JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(path), SettingsJson.Options) is { } file ? file with { Gradients = (file.Gradients ?? []).Where(entry => entry.Value is not null).ToDictionary() } : null;
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
            if (TryColour(key, out var col)) ImGui.GetStyle().Colors[(int)col] = EColor.ToVector4(colour);
            else Log.Warn($"theme {Name} - unknown colour {key}");
        }
        ApplyGradients(warn: true);
    }

    static bool TryColour(string key, out ImGuiCol col) => Enum.TryParse(key, out col) && Enum.IsDefined(col) && col != ImGuiCol.Count;

    static bool Tagged(ImGuiCol col) => ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)col])) >= 0;

    void ApplyGradients(bool warn)
    {
        foreach (var col in Enum.GetValues<ImGuiCol>().Where(col => col != ImGuiCol.Count && Tagged(col)))
            ImGui.GetStyle().Colors[(int)col] = Current.Colors.TryGetValue(col.ToString(), out var listed) ? EColor.ToVector4(listed) : _defaults.Colors[(int)col];
        List<GradientShade> shades = [];
        foreach (var (key, gradient) in Current.Gradients)
        {
            if (!TryColour(key, out var col))
            {
                if (warn) Log.Warn($"theme {Name} - unknown gradient {key}");
                continue;
            }
            if (gradient.Top.IsEmpty || gradient.Bottom.IsEmpty)
            {
                if (warn) Log.Warn($"theme {Name} - gradient {key} needs Top and Bottom");
                continue;
            }
            ImGui.GetStyle().Colors[(int)col] = ThemeGradients.Sentinel(shades.Count, gradient.Top.A / 255f);
            shades.Add(new GradientShade(Rgb(gradient.Top), Rgb(gradient.Bottom), Rgb(gradient.Highlight ?? default), Rgb(gradient.Text ?? default), gradient.Highlight is not null, gradient.Text is not null));
        }
        _shades = [.. shades];
    }

    static uint Rgb(Color colour) => EColor.U32(colour) & 0xFFFFFF;

    void LoadFonts(ThemeFonts fonts)
    {
        var io = ImGui.GetIO();
        ThemeFont[] slots = [fonts.Regular, fonts.Bold, fonts.Label, fonts.Mono];
        ImFontPtr[] picked =
        [
            .. slots.Select(font =>
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
        Fonts.Bold = picked[1];
        Fonts.BoldSize = fonts.Bold.Size;
        Fonts.Label = picked[2];
        Fonts.LabelSize = fonts.Label.Size;
        Fonts.Mono = picked[3];
        Fonts.MonoSize = fonts.Mono.Size;
        foreach (var stale in _loaded.Keys.Where(file => !slots.Any(slot => slot.File.Equals(file, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            ImGui.RemoveFont(io.Fonts, _loaded[stale]);
            _loaded.Remove(stale);
        }
        ImGui.GetStyle().FontSizeBase = fonts.Regular.Size;
    }
}
