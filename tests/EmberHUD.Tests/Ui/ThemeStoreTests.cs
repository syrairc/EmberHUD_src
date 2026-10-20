using System.Drawing;
using EmberHUD.Ui;
using EmberHUD.Host.Ui;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Tests;

public sealed class ThemeStoreTests : IDisposable
{
    static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "Themes");
    static readonly string FontsDirectory = Path.Combine(AppContext.BaseDirectory, "Fonts");
    readonly TempDir _temp = new();
    readonly LogCapture _log = new();

    string _themes => _temp.Path;

    public ThemeStoreTests()
    {
        TestFrame.Run(() => { });
        ImGuiScene.ResetStyle();
    }

    public void Dispose()
    {
        _log.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public void UseAndSaveAsReportTheNewNameAndAnUnknownThemeDoesNot()
    {
        File.WriteAllText(Path.Combine(_themes, "A.json"), """{ "Style": { "FrameRounding": 7 } }""");
        File.WriteAllText(Path.Combine(_themes, "B.json"), """{ "Style": { "FrameRounding": 9 } }""");
        var themes = Store(_themes, "A");
        themes.Apply();
        List<string> names = [];
        themes.Changed += names.Add;

        themes.Use("Nope");
        themes.Use("B");
        themes.SaveAs("Mine");

        Assert.Equal(["B", "Mine"], names);
    }

    static ThemeStore Store(string directory, string name) => new(directory, FontsDirectory, name, action => action());

    static (Vector2 InputMin, Vector2 InputMax, Vector2 PlainMin, Vector2 PlainMax) InputOnGold()
    {
        var boxes = default((Vector2, Vector2, Vector2, Vector2));
        for (var frame = 0; frame < 2; frame++)
            TestFrame.Run(() =>
            {
                ImGui.SetNextWindowSize(new Vector2(400, 200));
                ImGui.Begin("input on gold");
                var at = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(at, at + new Vector2(300, 70), ImGui.GetColorU32(ImGuiCol.Button));
                ImGui.SetCursorScreenPos(at + new Vector2(10, 10));
                var value = "value";
                ImGui.SetNextItemWidth(200);
                ImGui.InputText("##in", ref value, 32);
                var inputMin = ImGui.GetItemRectMin();
                var inputMax = ImGui.GetItemRectMax();
                ImGui.SetCursorScreenPos(at + new Vector2(10, 45));
                ImGui.TextUnformatted("plain");
                boxes = (inputMin, inputMax, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                ImGui.End();
            });
        return boxes;
    }

    static bool Inside(Vector2 pos, Vector2 min, Vector2 max) => pos.X >= min.X && pos.X <= max.X && pos.Y >= min.Y && pos.Y <= max.Y;

    static void AssertInputTextLightAndPlainTextDark(ThemeStore themes)
    {
        var (inputMin, inputMax, plainMin, plainMax) = InputOnGold();
        themes.Shade();

        var text = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]) & 0xFFFFFF;
        var vertices = ImGuiScene.Vertices().Select(v => (v.Pos, Rgb: v.Col & 0xFFFFFF)).ToList();
        Assert.Contains(vertices, v => v.Rgb == text && Inside(v.Pos, inputMin, inputMax));
        Assert.DoesNotContain(vertices, v => v.Rgb == 0x302C2A && Inside(v.Pos, inputMin, inputMax));
        Assert.Contains(vertices, v => v.Rgb == 0x302C2A && Inside(v.Pos, plainMin, plainMax));
    }

    [Fact]
    public void TextInAnInputOnGoldStaysLightWhileTextOnTheGoldGoesDark()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), Graded);
        var themes = Store(_themes, "Graded");
        themes.Apply();
        AssertInputTextLightAndPlainTextDark(themes);
    }

    [Fact]
    public void AThemeThatTagsFrameBgItselfStillKeepsInputTextLight()
    {
        File.WriteAllText(Path.Combine(_themes, "Framed.json"), """{ "Gradients": { "Button": { "Top": "#FFD46AFF", "Bottom": "#B9820FFF", "Text": "#2A2C30FF" }, "FrameBg": { "Top": "#284055FF", "Bottom": "#1C2C3BFF" } } }""");
        var themes = Store(_themes, "Framed");
        themes.Apply();
        AssertInputTextLightAndPlainTextDark(themes);
    }

    [Fact]
    public void TheTorchlightThemeAppliesItsStyleColoursAndFonts()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        Assert.Equal(6f, ImGui.GetStyle().WindowRounding);
        Assert.Equal(new Vector2(12, 12), ImGui.GetStyle().WindowPadding);
        Assert.Equal(new Vector4(0xD9 / 255f, 0xD9 / 255f, 0xD9 / 255f, 1), ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        Assert.Equal(4f, ImGui.GetStyle().FrameRounding);
        foreach (var col in new[] { ImGuiCol.WindowBg, ImGuiCol.Button, ImGuiCol.ButtonActive, ImGuiCol.TabSelected })
            Assert.True(ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)col])) >= 0, $"{col} is a gradient");
        Assert.Equal(new ThemeFont("NotoSans-Regular.ttf", 15), themes.Current.Fonts.Regular);
        Assert.False(Fonts.Of(FontFace.Regular).Font.IsNull);
        Assert.False(Fonts.Of(FontFace.Mono).Font.IsNull);
        Assert.Equal(15f, Fonts.Of(FontFace.Mono).Size);
        Assert.False(Fonts.Of(FontFace.Bold).Font.IsNull);
        Assert.False(Fonts.Of(FontFace.Label).Font.IsNull);
        Assert.Equal(16f, Fonts.Of(FontFace.Bold).Size);
        Assert.Equal(16f, Fonts.Of(FontFace.Label).Size);
        Assert.NotEqual(Fonts.Of(FontFace.Regular).Font, Fonts.Of(FontFace.Mono).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Regular).Font, Fonts.Of(FontFace.Label).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Bold).Font, Fonts.Of(FontFace.Label).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Bold).Font, Fonts.Of(FontFace.Regular).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Bold).Font, Fonts.Of(FontFace.Mono).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Label).Font, Fonts.Of(FontFace.Mono).Font);
        Assert.Equal(15f, ImGui.GetStyle().FontSizeBase);
        Assert.Empty(_log.Text);
        TestFrame.Run(() => ImGui.TextUnformatted("themed"));
    }

    [Fact]
    public void AnOldTwoSlotThemeGetsTheDefaultBoldAndLabelFaces()
    {
        File.WriteAllText(Path.Combine(_themes, "Old.json"), """{ "Fonts": { "Regular": { "File": "JetBrainsMono-Regular.ttf", "Size": 14 }, "Mono": { "File": "JetBrainsMono-Regular.ttf", "Size": 14 } } }""");
        var themes = Store(_themes, "Old");
        themes.Apply();
        Assert.Equal(new ThemeFont("NotoSans-SemiBold.ttf", 16), themes.Current.Fonts.Bold);
        Assert.Equal(new ThemeFont("Rajdhani-SemiBold.ttf", 16), themes.Current.Fonts.Label);
        Assert.False(Fonts.Of(FontFace.Bold).Font.IsNull);
        Assert.NotEqual(Fonts.Of(FontFace.Regular).Font, Fonts.Of(FontFace.Label).Font);
        Assert.NotEqual(Fonts.Of(FontFace.Bold).Font, Fonts.Of(FontFace.Label).Font);
        Assert.Equal(14f, Fonts.Of(FontFace.Regular).Size);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void ABoldOrLabelChangeOnlyTouchesThatSlot()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        var regular = Fonts.Of(FontFace.Regular).Font;
        var label = Fonts.Of(FontFace.Label).Font;
        var mono = Fonts.Of(FontFace.Mono).Font;
        var bold = Fonts.Of(FontFace.Bold).Font;
        themes.UseFonts(themes.Current.Fonts with { Label = themes.Current.Fonts.Label with { Size = 30 }, Bold = new ThemeFont("NotoSans-Bold.ttf", 18) });
        Assert.Equal(regular, Fonts.Of(FontFace.Regular).Font);
        Assert.Equal(label, Fonts.Of(FontFace.Label).Font);
        Assert.Equal(mono, Fonts.Of(FontFace.Mono).Font);
        Assert.NotEqual(bold, Fonts.Of(FontFace.Bold).Font);
        Assert.Equal(30f, Fonts.Of(FontFace.Label).Size);
        Assert.Equal(18f, Fonts.Of(FontFace.Bold).Size);
        Assert.Empty(_log.Text);
        TestFrame.Run(() =>
        {
            using (Fonts.Use(FontFace.Bold)) ImGui.TextUnformatted("bold");
            using (Fonts.Use(FontFace.Label)) ImGui.TextUnformatted("label");
        });
    }

    [Fact]
    public void TorchlightIsTheOnlyShippedThemeAndLoadsWithoutWarnings()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        Assert.Equal(["Torchlight"], themes.Names());
        themes.Use("Torchlight");
        Assert.Equal("Torchlight", themes.Name);
        Assert.Empty(_log.Text);
        TestFrame.Run(() => ImGui.TextUnformatted("torchlight"));
    }

    [Theory]
    [InlineData("Azure")]
    [InlineData("Nope")]
    public void ANameThatIsNotShippedFallsBackToTorchlight(string saved)
    {
        var themes = Store(Shipped, saved);
        Assert.Equal("Torchlight", themes.Name);
        Assert.Contains($"theme {saved} not found", _log.Text);
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
        var mono = Fonts.Of(FontFace.Mono).Font;
        themes.UseFonts(themes.Current.Fonts with { Mono = themes.Current.Fonts.Mono with { Size = 20 } });
        Assert.Equal(mono, Fonts.Of(FontFace.Mono).Font);
        Assert.Equal(20f, Fonts.Of(FontFace.Mono).Size);
        Assert.Equal(20f, themes.Current.Fonts.Mono.Size);
    }

    [Fact]
    public void AMissingFontFallsBackToTheDefaultFont()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        themes.UseFonts(themes.Current.Fonts with { Regular = new ThemeFont("nope.ttf", 15) });
        Assert.Contains("font nope.ttf not found", _log.Text);
        Assert.False(Fonts.Of(FontFace.Regular).Font.IsNull);
        TestFrame.Run(() => ImGui.TextUnformatted("fallback"));
    }

    [Fact]
    public void AFontNobodyUsesAnyMoreIsRemovedFromTheAtlas()
    {
        var themes = Store(Shipped, "Torchlight");
        themes.Apply();
        var removed = Fonts.Of(FontFace.Mono).Font;
        var before = ImGui.GetIO().Fonts.Fonts.Size;
        themes.UseFonts(themes.Current.Fonts with { Mono = themes.Current.Fonts.Regular });
        Assert.Equal(Fonts.Of(FontFace.Regular).Font, Fonts.Of(FontFace.Mono).Font);
        Assert.NotEqual(removed, Fonts.Of(FontFace.Mono).Font);
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
        Assert.Equal(15f, Fonts.Of(FontFace.Mono).Size);
        queued.ForEach(action => action());
        Assert.Equal("B", themes.Name);
        Assert.Equal(9f, ImGui.GetStyle().FrameRounding);
        Assert.Equal(22f, Fonts.Of(FontFace.Mono).Size);
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

    const string Graded = """{ "Gradients": { "Button": { "Top": "#FFD46AFF", "Bottom": "#B9820FFF", "Highlight": "#FFE7A3FF", "Text": "#2A2C30FF" } } }""";

    [Fact]
    public void AGradientTagsItsColourAndShadeTurnsTheTagIntoTheGradient()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), Graded);
        var themes = Store(_themes, "Graded");
        themes.Apply();
        Assert.Equal(0, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Button])));

        for (var frame = 0; frame < 2; frame++)
            TestFrame.Run(() =>
            {
                ImGui.Begin("graded");
                ImGui.Button("gold");
                ImGui.End();
            });
        themes.Shade();

        var colours = ImGuiScene.Vertices().Select(v => v.Col).ToList();
        Assert.DoesNotContain(colours, colour => ThemeGradients.IndexOf(colour) >= 0);
        Assert.Contains(colours, colour => (colour & 0xFFFFFF) == 0x0F82B9);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void UnknownGradientsAndMissingStopsWarnAndTheRestApplies()
    {
        File.WriteAllText(Path.Combine(_themes, "Odd.json"), """{ "Gradients": { "TabActive": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" }, "Header": { "Top": "#FFFFFFFF" }, "Button": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" } } }""");
        var themes = Store(_themes, "Odd");
        themes.Apply();
        Assert.Contains("unknown gradient TabActive", _log.Text);
        Assert.Contains("gradient Header needs Top and Bottom", _log.Text);
        Assert.Equal(-1, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Header])));
        Assert.Equal(0, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Button])));
    }

    [Fact]
    public void SaveNeverWritesASentinelAndKeepsTheGradients()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), Graded);
        var themes = Store(_themes, "Graded");
        themes.Apply();
        var gradient = themes.Current.Gradients["Button"];
        themes.SaveAs("Mine");

        var again = Store(_themes, "Mine");
        Assert.False(again.Current.Colors.ContainsKey("Button"));
        Assert.True(again.Current.Colors.ContainsKey("Text"));
        Assert.Equal(gradient, again.Current.Gradients["Button"]);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void ReTaggingPutsBackTheColourOfAGradientThatWentAway()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), """{ "Colors": { "Header": "#102030FF" }, "Gradients": { "Button": { "Top": "#FFD46AFF", "Bottom": "#B9820FFF" }, "Header": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" } } }""");
        List<Action> queued = [];
        var themes = new ThemeStore(_themes, FontsDirectory, "Graded", queued.Add);
        themes.Apply();
        themes.UseGradients(new Dictionary<string, ThemeGradient> { ["Header"] = themes.Current.Gradients["Header"] });
        queued[0]();

        Assert.Equal(-1, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Button])));
        Assert.Equal(0, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Header])));

        themes.UseGradients([]);
        queued[1]();
        Assert.Equal(new Vector4(0x10 / 255f, 0x20 / 255f, 0x30 / 255f, 1), ImGui.GetStyle().Colors[(int)ImGuiCol.Header]);
    }

    [Fact]
    public void UseGradientsQueuesUntilTheFrameRunsAndKeepsTheRestOfTheLook()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), """{ "Style": { "FrameRounding": 7 }, "Gradients": { "Button": { "Top": "#FFD46AFF", "Bottom": "#B9820FFF" } } }""");
        List<Action> queued = [];
        var themes = new ThemeStore(_themes, FontsDirectory, "Graded", queued.Add);
        themes.Apply();
        ImGui.GetStyle().WindowRounding = 3;
        var edited = new Dictionary<string, ThemeGradient>(themes.Current.Gradients) { ["Button"] = new ThemeGradient(Color.Red, Color.Blue) };
        themes.UseGradients(edited);
        Assert.Single(queued);
        Assert.Equal(Color.FromArgb(0xFF, 0xFF, 0xD4, 0x6A), themes.Current.Gradients["Button"].Top);

        queued[0]();
        Assert.Equal(Color.Red, themes.Current.Gradients["Button"].Top);
        Assert.Equal(0, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Button])));
        Assert.Equal(3f, ImGui.GetStyle().WindowRounding);
        Assert.Equal(7f, ImGui.GetStyle().FrameRounding);
    }

    [Fact]
    public void SaveDropsAGradientTheStyleEditorFlattened()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), """{ "Gradients": { "Button": { "Top": "#FFD46AFF", "Bottom": "#B9820FFF" }, "Header": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" }, "TabActive": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" }, "Border": { "Top": "#FFFFFFFF" } } }""");
        var themes = Store(_themes, "Graded");
        themes.Apply();
        var flat = new Vector4(0.1f, 0.2f, 0.3f, 1);
        ImGui.GetStyle().Colors[(int)ImGuiCol.Button] = flat;
        themes.SaveAs("Mine");

        var again = Store(_themes, "Mine");
        Assert.Equal(EColor.FromVector4(flat), again.Current.Colors["Button"]);
        Assert.False(again.Current.Gradients.ContainsKey("Button"));
        Assert.True(again.Current.Gradients.ContainsKey("Header"));
        Assert.True(again.Current.Gradients.ContainsKey("TabActive"));
        Assert.True(again.Current.Gradients.ContainsKey("Border"));
        Assert.False(themes.Current.Gradients.ContainsKey("Button"));
    }

    [Fact]
    public void UseGradientsDoesNotRepeatTheApplyWarnings()
    {
        File.WriteAllText(Path.Combine(_themes, "Odd.json"), """{ "Gradients": { "TabActive": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" }, "Header": { "Top": "#FFFFFFFF" }, "Button": { "Top": "#FFFFFFFF", "Bottom": "#000000FF" } } }""");
        var themes = Store(_themes, "Odd");
        themes.Apply();
        var warned = _log.Text;
        Assert.Contains("unknown gradient TabActive", warned);

        themes.UseGradients(themes.Current.Gradients);
        themes.UseGradients(themes.Current.Gradients);

        Assert.Equal(warned, _log.Text);
    }

    [Theory]
    [InlineData("""{ "Gradients": { "Button": null } }""")]
    [InlineData("""{ "Gradients": null }""")]
    public void NullGradientsAreSkippedAndNeverThrow(string json)
    {
        File.WriteAllText(Path.Combine(_themes, "Nully.json"), json);
        var themes = Store(_themes, "Nully");
        themes.Apply();
        Assert.NotNull(themes.Current.Gradients);
        Assert.DoesNotContain(themes.Current.Gradients.Values, gradient => gradient is null);
        themes.SaveAs("Mine");

        Assert.Equal(-1, ThemeGradients.IndexOf(ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Button])));
        Assert.Empty(Store(_themes, "Mine").Current.Gradients);
    }
}
