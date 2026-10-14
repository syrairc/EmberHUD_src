using System.Text.Json;
using EmberHUD.Host.Ui;
using EmberHUD.Ui;
using EmberHUD.Testing;
using Hexa.NET.ImGui;
using static EmberHUD.Tests.ImGuiScene;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public sealed class SettingsThemeTabTests : SettingsWindowFixture
{
    [Fact]
    public void ThemeNamesAreReadOnceWhileTheComboStaysOpen()
    {
        File.WriteAllText(Path.Combine(_themes, "Qwertyuiopa.json"), "{}");
        using var host = Discovered();
        var window = Window(host, Themes("Plain"));
        OpenTab(window, 1);
        Click(window, Frames(Settled(window))[0].Centre);
        Assert.Equal(1, PageRows(Quad.Capture()).Count(row => row.Count == "Qwertyuiopa".Length));

        File.Delete(Path.Combine(_themes, "Qwertyuiopa.json"));
        Frame(window);
        Frame(window);

        Assert.Equal(1, PageRows(Quad.Capture()).Count(row => row.Count == "Qwertyuiopa".Length));
    }

    [Fact]
    public void ThemeSaveIsThePrimaryButton()
    {
        using var host = Discovered();
        var themes = Themes("Plain");
        var window = Window(host, themes);
        OpenTab(window, 1);
        var buttons = Buttons(Settled(window));
        Assert.Equal(Normal(ImGuiCol.ButtonActive), buttons[0].Col);
        Assert.Equal(Normal(ImGuiCol.Button), buttons[1].Col);
    }

    [Fact]
    public void TheGradientsSectionShowsAPickerPerStop()
    {
        File.WriteAllText(Path.Combine(_themes, "Graded.json"), """{ "Gradients": { "Button": { "Top": "#FF0000FF", "Bottom": "#00FF00FF", "Text": "#0000FFFF" } } }""");
        using var host = Discovered();
        var themes = Themes("Graded");
        var window = Window(host, themes);
        OpenTab(window, 1);
        var scene = Settled(window);
        foreach (var colour in new[] { System.Drawing.Color.Red, System.Drawing.Color.Lime, System.Drawing.Color.Blue })
            Assert.Contains(scene, quad => !quad.Glyph && quad.Col == EColor.U32(colour));
    }

    [Fact]
    public void ThePickerSwitchesTheThemeAndSdkSettingsFollow()
    {
        using var host = Discovered();
        var themes = Themes("Plain");
        var window = Window(host, themes);
        OpenTab(window, 1);

        var combo = Frames(Settled(window))[0];
        Click(window, combo.Centre);
        Click(window, FirstItemBelow(Quad.Capture(), combo).Centre);
        Settled(window);

        Assert.Equal("Other", themes.Name);
        Assert.Equal("Other", _fixture.Sdk.Theme);
    }

    [Fact]
    public void SaveWritesTheLiveStyleAndRevertRestoresTheSavedOne()
    {
        using var host = Discovered();
        var window = Window(host, Themes("Plain"));
        OpenTab(window, 1);
        var original = ImGui.GetStyle().GrabMinSize;
        var file = Path.Combine(_themes, "Plain.json");

        try
        {
            ImGui.GetStyle().GrabMinSize = original + 5;
            Click(window, Buttons(Settled(window))[1].Centre);
            Assert.Equal(original, ImGui.GetStyle().GrabMinSize);
            Assert.False(JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("Style").TryGetProperty("GrabMinSize", out _));

            ImGui.GetStyle().GrabMinSize = original + 5;
            Click(window, Buttons(Settled(window))[0].Centre);
            Assert.Equal(original + 5, JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetProperty("Style").GetProperty("GrabMinSize").GetSingle());
        }
        finally
        {
            ImGui.GetStyle().GrabMinSize = original;
        }
    }

    [Fact]
    public void SaveAsStaysDisabledForEmptyWhitespaceOrInvalidNamesAndSavesAValidOne()
    {
        using var host = Discovered();
        var themes = Themes("Plain");
        var window = Window(host, themes);
        OpenTab(window, 1);

        Quad SaveAs() => Buttons(Settled(window))[2];
        void Type(string text)
        {
            Click(window, Frames(Quad.Capture()).Single(quad => Math.Abs(quad.Width - SettingsWindow.ControlWidth) < 1).Centre);
            var io = ImGui.GetIO();
            io.AddKeyEvent(ImGuiKey.ModCtrl, true);
            io.AddKeyEvent(ImGuiKey.A, true);
            Frame(window);
            io.AddKeyEvent(ImGuiKey.A, false);
            io.AddKeyEvent(ImGuiKey.ModCtrl, false);
            Frame(window);
            io.AddInputCharactersUTF8(text);
            Frame(window);
            Frame(window);
        }

        Assert.Equal(Dimmed(ImGuiCol.Button), SaveAs().Col);

        Type("   ");
        Assert.Equal(Dimmed(ImGuiCol.Button), SaveAs().Col);

        Type("a:b");
        Assert.Equal(Dimmed(ImGuiCol.Button), SaveAs().Col);

        Type(" Mine ");
        var button = SaveAs();
        Assert.Equal(Normal(ImGuiCol.Button), button.Col);
        Click(window, button.Centre);

        Assert.True(File.Exists(Path.Combine(_themes, "Mine.json")));
        Assert.Equal("Mine", themes.Name);
        Assert.Equal("Mine", _fixture.Sdk.Theme);
        Assert.Equal(Dimmed(ImGuiCol.Button), SaveAs().Col);
    }

    [Fact]
    public void FontSlidersApplyOnlyTheTouchedFontThroughUseFonts()
    {
        using var host = Discovered();
        var themes = Themes("Plain");
        var window = Window(host, themes);
        OpenTab(window, 1);
        var untouched = themes.Current.Fonts;

        Settled(window);
        Assert.Same(untouched, themes.Current.Fonts);

        var sliders = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - SettingsWindow.FontSliderWidth) < 1).ToList();
        Click(window, new Vector2(sliders[0].Max.X - 1, sliders[0].Centre.Y));
        Settled(window);
        Assert.Equal(40f, themes.Current.Fonts.Regular.Size);
        Assert.Equal(untouched.Mono.Size, themes.Current.Fonts.Mono.Size);

        sliders = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - SettingsWindow.FontSliderWidth) < 1).ToList();
        Click(window, new Vector2(sliders[3].Max.X - 1, sliders[3].Centre.Y));
        Settled(window);
        Assert.Equal(40f, themes.Current.Fonts.Mono.Size);
        Assert.Equal(untouched.Bold, themes.Current.Fonts.Bold);
        Assert.Equal(untouched.Label, themes.Current.Fonts.Label);
        Assert.Equal(untouched.Regular.File, themes.Current.Fonts.Regular.File);
    }

    [Fact]
    public void PickingAFontFileAppliesItToThatRowOnly()
    {
        using var host = Discovered();
        var themes = Themes("Plain");
        var window = Window(host, themes);
        OpenTab(window, 1);
        var untouched = themes.Current.Fonts;

        var mono = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - (SettingsWindow.ControlWidth - ImGui.GetFrameHeight())) < 1.5f).ToList()[4];
        Click(window, mono.Centre);
        var filter = Frames(Quad.Capture()).First(quad => quad.Min.Y > mono.Max.Y);
        Click(window, FirstItemBelow(Quad.Capture(), filter).Centre);
        Settled(window);

        Assert.Equal(themes.FontFiles()[0], themes.Current.Fonts.Mono.File);
        Assert.NotEqual(untouched.Mono.File, themes.Current.Fonts.Mono.File);
        Assert.Equal(untouched.Regular, themes.Current.Fonts.Regular);
        Assert.Equal(untouched.Bold, themes.Current.Fonts.Bold);
        Assert.Equal(untouched.Label, themes.Current.Fonts.Label);
    }
}
