using System.Drawing;
using TorchlightSDK.Host.Ui;
using TorchlightSDK.Testing;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Tests;

public sealed class ThemeStoreTests : IDisposable
{
    static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "Themes");
    static readonly string FontsDirectory = Path.Combine(AppContext.BaseDirectory, "Fonts");
    readonly string _themes = Directory.CreateTempSubdirectory("torchlightsdk-themes-").FullName;
    readonly LogCapture _log = new();

    public ThemeStoreTests() => TestFrame.Run(() => { });

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_themes, true);
    }

    static ThemeStore Store(string directory, string name) => new(directory, FontsDirectory, name, action => action());

    [Fact]
    public void TheTorchlightThemeAppliesItsStyleColoursAndFonts()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        Assert.Equal(6f, ImGui.GetStyle().WindowRounding);
        Assert.Equal(new Vector2(12, 12), ImGui.GetStyle().WindowPadding);
        Assert.Equal(new Vector4(0xE6 / 255f, 0xE3 / 255f, 0xD6 / 255f, 1), ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        Assert.False(Fonts.Regular.IsNull);
        Assert.False(Fonts.Mono.IsNull);
        Assert.Equal(15f, Fonts.MonoSize);
        Assert.Equal(15f, ImGui.GetStyle().FontSizeBase);
        Assert.Empty(_log.Text);
        TestFrame.Run(() => ImGui.TextUnformatted("themed"));
    }

    [Fact]
    public void EveryShippedThemeLoadsWithoutWarnings()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        Assert.Equal(["Amethyst", "Azure", "Chartreuse", "Crimson", "Denim", "Emerald", "Gold", "Graphite", "HighContrastMono", "Indigo", "MonoSteel", "Rose", "Torchlight", "TrueGrayscale", "Violet"], themes.Names());
        foreach (var name in themes.Names()) themes.Use(name);
        Assert.Equal("Violet", themes.Name);
        Assert.Empty(_log.Text);
        TestFrame.Run(() => ImGui.TextUnformatted("violet"));
    }

    [Fact]
    public void AMissingThemeFallsBackToTorchlight()
    {
        var themes = Store(Shipped, "Nope");
        Assert.Equal("Torchlight", themes.Name);
        Assert.Contains("theme Nope not found", _log.Text);
    }

    [Fact]
    public void SaveAsWritesTheLiveLookAndSwitchesToIt()
    {
        File.Copy(Path.Combine(Shipped, "Torchlight.json"), Path.Combine(_themes, "Torchlight.json"));
        var themes = Store(_themes, "Torchlight");
        themes.Apply();
        ImGui.GetStyle().WindowRounding = 3;
        ImGui.GetStyle().Colors[(int)ImGuiCol.Button] = new Vector4(1, 0, 0, 1);
        themes.SaveAs("Mine");
        Assert.Equal("Mine", themes.Name);
        Assert.Equal(["Mine", "Torchlight"], themes.Names());
        Assert.False(File.Exists(Path.Combine(_themes, "Mine.json.tmp")));

        var again = Store(_themes, "Mine");
        Assert.Equal(3f, again.Current.Style["WindowRounding"].GetSingle());
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), again.Current.Colors["Button"]);
        again.Apply();
        Assert.Equal(3f, ImGui.GetStyle().WindowRounding);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void UnknownNamesAndBadValuesWarnAndTheRestApplies()
    {
        File.WriteAllText(Path.Combine(_themes, "Odd.json"), """{ "Colors": { "TabActive": "#FF0000FF", "Text": "#FFFFFFFF" }, "Style": { "ChildWindowRounding": 2, "WindowPadding": 5, "FrameRounding": 7 } }""");
        var themes = Store(_themes, "Odd");
        themes.Apply();
        Assert.Contains("unknown colour TabActive", _log.Text);
        Assert.Contains("unknown style ChildWindowRounding", _log.Text);
        Assert.Contains("style WindowPadding - ", _log.Text);
        Assert.Equal(7f, ImGui.GetStyle().FrameRounding);
        Assert.Equal(Vector4.One, ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
    }

    [Fact]
    public void ASizeOnlyFontChangeKeepsTheLoadedFont()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        var mono = Fonts.Mono;
        themes.UseFonts(themes.Current.Fonts with { Mono = themes.Current.Fonts.Mono with { Size = 20 } });
        Assert.Equal(mono, Fonts.Mono);
        Assert.Equal(20f, Fonts.MonoSize);
        Assert.Equal(20f, themes.Current.Fonts.Mono.Size);
    }

    [Fact]
    public void AMissingFontFallsBackToTheDefaultFont()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        themes.UseFonts(themes.Current.Fonts with { Regular = new ThemeFont("nope.ttf", 15) });
        Assert.Contains("font nope.ttf not found", _log.Text);
        Assert.False(Fonts.Regular.IsNull);
        TestFrame.Run(() => ImGui.TextUnformatted("fallback"));
    }

    [Fact]
    public void AFontNobodyUsesAnyMoreIsRemovedFromTheAtlas()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        var removed = Fonts.Mono;
        var before = ImGui.GetIO().Fonts.Fonts.Size;
        themes.UseFonts(themes.Current.Fonts with { Mono = themes.Current.Fonts.Regular });
        Assert.Equal(Fonts.Regular, Fonts.Mono);
        Assert.NotEqual(removed, Fonts.Mono);
        Assert.Equal(before - 1, ImGui.GetIO().Fonts.Fonts.Size);
        TestFrame.Run(() => ImGui.TextUnformatted("swapped"));
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void SaveAsCreatesTheThemesDirectory()
    {
        var directory = Path.Combine(_themes, "new", "themes");
        var themes = Store(directory, "Nope");
        themes.Apply();
        themes.SaveAs("Mine");
        Assert.True(File.Exists(Path.Combine(directory, "Mine.json")));
    }

    [Fact]
    public void SaveAsOntoALockedFileWarnsAndKeepsTheOldTheme()
    {
        File.Copy(Path.Combine(Shipped, "Torchlight.json"), Path.Combine(_themes, "Torchlight.json"));
        var themes = Store(_themes, "Torchlight");
        themes.Apply();
        var before = themes.Current;
        ImGui.GetStyle().WindowRounding = 7;
        var locked = Path.Combine(_themes, "Mine.json");
        File.WriteAllText(locked, "{}");
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            themes.SaveAs("Mine");
        Assert.Equal("Torchlight", themes.Name);
        Assert.Same(before, themes.Current);
        Assert.Contains("could not save theme Mine", _log.Text);
        themes.SaveAs("Mine");
        Assert.Equal("Mine", themes.Name);
        Assert.Equal(7f, Store(_themes, "Mine").Current.Style["WindowRounding"].GetSingle());
    }

    [Fact]
    public void SaveAsIntoAnUnwritableDirectoryWarnsAndKeepsTheOldTheme()
    {
        var blocked = Path.Combine(_themes, "blocked");
        File.WriteAllText(blocked, "");
        var themes = Store(Path.Combine(blocked, "themes"), "Nope");
        themes.Apply();
        var before = themes.Current;
        themes.SaveAs("Mine");
        Assert.Equal("Torchlight", themes.Name);
        Assert.Same(before, themes.Current);
        Assert.Contains("could not save theme Mine", _log.Text);
    }

    [Fact]
    public void UseAndUseFontsOnlyQueueUntilTheFrameRuns()
    {
        File.WriteAllText(Path.Combine(_themes, "A.json"), """{ "Style": { "FrameRounding": 7 } }""");
        File.WriteAllText(Path.Combine(_themes, "B.json"), """{ "Style": { "FrameRounding": 9 } }""");
        List<Action> queued = [];
        var themes = new ThemeStore(_themes, FontsDirectory, "A", queued.Add);
        themes.Apply();
        themes.Use("B");
        themes.UseFonts(themes.Current.Fonts with { Mono = themes.Current.Fonts.Mono with { Size = 22 } });
        Assert.Equal(2, queued.Count);
        Assert.Equal("A", themes.Name);
        Assert.Equal(7f, ImGui.GetStyle().FrameRounding);
        Assert.Equal(15f, Fonts.MonoSize);
        queued.ForEach(action => action());
        Assert.Equal("B", themes.Name);
        Assert.Equal(9f, ImGui.GetStyle().FrameRounding);
        Assert.Equal(22f, Fonts.MonoSize);
    }

    [Fact]
    public void UsingAnUnknownThemeWarnsAndKeepsTheCurrentOne()
    {
        File.WriteAllText(Path.Combine(_themes, "A.json"), """{ "Style": { "FrameRounding": 7 } }""");
        var themes = Store(_themes, "A");
        themes.Apply();
        themes.Use("Nope");
        Assert.Contains("theme Nope not found", _log.Text);
        Assert.Equal("A", themes.Name);
        Assert.Equal(7f, ImGui.GetStyle().FrameRounding);
    }

    [Fact]
    public void InvalidJsonWarnsAndFallsBackToTorchlight()
    {
        File.Copy(Path.Combine(Shipped, "Torchlight.json"), Path.Combine(_themes, "Torchlight.json"));
        File.WriteAllText(Path.Combine(_themes, "Broken.json"), "{ nope");
        var themes = Store(_themes, "Broken");
        Assert.Contains("theme Broken is not valid JSON", _log.Text);
        Assert.Equal("Torchlight", themes.Name);
        Assert.NotEmpty(themes.Current.Colors);
    }

    [Fact]
    public void ARePlacedThemeResetsUnlistedValuesToTheDefaults()
    {
        File.WriteAllText(Path.Combine(_themes, "A.json"), """{ "Style": { "FrameRounding": 7 } }""");
        File.WriteAllText(Path.Combine(_themes, "B.json"), """{ "Style": { "WindowRounding": 5 } }""");
        var defaultFrameRounding = ImGui.GetStyle().FrameRounding;
        var themes = new ThemeStore(_themes, FontsDirectory, "A", action => action());
        themes.Apply();
        Assert.Equal(7f, ImGui.GetStyle().FrameRounding);
        themes.Use("B");
        Assert.Equal(5f, ImGui.GetStyle().WindowRounding);
        Assert.Equal(defaultFrameRounding, ImGui.GetStyle().FrameRounding);
    }

    [Fact]
    public void AnUnreadableThemeFileWarnsAndFallsBackToTorchlight()
    {
        File.Copy(Path.Combine(Shipped, "Torchlight.json"), Path.Combine(_themes, "Torchlight.json"));
        var locked = Path.Combine(_themes, "Locked.json");
        File.WriteAllText(locked, "{}");
        using var hold = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var themes = Store(_themes, "Locked");
        Assert.Contains("theme Locked could not be read", _log.Text);
        Assert.Equal("Torchlight", themes.Name);
    }
}
