using EmberHUD.Host.Plugins;
using EmberHUD.Host.Ui;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;
using static EmberHUD.Tests.ImGuiScene;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public sealed class SettingsWindowTests : SettingsWindowFixture
{
    const string BrokenSource = "namespace Broken; public sealed class Main : EmberHUD.Plugin { int X = \"no\"; }";

    [Fact]
    public void AnOpenSettingsWindowKeepsTheHostAutosaving()
    {
        using var host = Discovered();
        var window = Window(host);
        var path = Path.Combine(_fixture.Root, "Config", "sdk.json");
        _fixture.Sdk.AutoReload = true;
        host.SavedAt = Environment.TickCount64 - 2001;
        host.TickAll();
        Assert.False(File.Exists(path));

        Frame(window);
        host.SavedAt = Environment.TickCount64 - 2001;
        host.TickAll();

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AChangeMadeJustBeforeClosingIsStillSaved()
    {
        using var host = Discovered();
        var window = Window(host);
        var path = Path.Combine(_fixture.Root, "Config", "sdk.json");
        Frame(window);
        host.SavedAt = Environment.TickCount64 - 2001;
        host.TickAll();

        _fixture.Sdk.AutoReload = true;
        Frame(window);
        window.Toggle();
        Frame(window);
        host.SavedAt = Environment.TickCount64 - 2001;
        host.TickAll();

        Assert.Contains("\"AutoReload\": true", File.ReadAllText(path));
    }

    [Fact]
    public void EveryPageDrawsIncludingAnErroredPlugin()
    {
        _fixture.Write("Broken", "Main.cs", BrokenSource);
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", "", "public bool Show = true;"));
        using var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        PluginFixture.Settle(host);
        var good = host.Slots.Single(slot => slot.Name == "Good");
        host.SetEnabled(good, true);
        var themes = new ThemeStore(Path.Combine(AppContext.BaseDirectory, "Themes"), Path.Combine(AppContext.BaseDirectory, "Fonts"), "Torchlight", action => action());
        var logs = new LogBuffer();
        logs.Add("100% done %s %d");
        var window = new SettingsWindow(_fixture.Sdk, host, themes, logs);
        window.Toggle();

        foreach (var tab in new[] { 0, 1, 2 })
        {
            OpenTab(window, tab);
            TestFrame.Run(window.Draw);
            TestFrame.Run(window.Draw);
        }
        foreach (var slot in host.Slots)
        {
            window.Selected = slot;
            TestFrame.Run(window.Draw);
            TestFrame.Run(window.Draw);
        }

        Assert.Null(good.ErrorStage);
        Assert.Equal("Torchlight", _fixture.Sdk.Theme);
    }

    [Fact]
    public void TheListShowsEnabledPluginsFirstThenDisabledEachByNameIgnoringCaseWithErrorAndCompilingMarks()
    {
        using var host = Discovered("Aaaa", "Bb", "bbbbb", "Cccccc", "Dd", "Eeee");
        host.SetEnabled(Slot(host, "Bb"), true);
        host.SetEnabled(Slot(host, "bbbbb"), true);
        host.SetEnabled(Slot(host, "Cccccc"), true);
        Slot(host, "Cccccc").ErrorStage = PluginStage.Render;
        Slot(host, "Eeee").ErrorStage = PluginStage.Render;
        Slot(host, "Dd").Compiling = true;
        var window = Window(host);

        try
        {
            var rows = ListRows(Settled(window)).Select(row => (row[0].Col, row.Count)).ToList();

            Assert.Equal(
                [
                    (Normal(ImGuiCol.Text), "EmberHUDSettings".Length),
                    (Normal(ImGuiCol.Text), "Bb".Length),
                    (Normal(ImGuiCol.Text), "bbbbb".Length),
                    (ErrorColour, "!Cccccc".Length),
                    (Normal(ImGuiCol.TextDisabled), "Aaaa".Length),
                    (Normal(ImGuiCol.TextDisabled), "Dd(compiling)".Length),
                    (ErrorColour, "!Eeee".Length),
                ],
                rows);
        }
        finally
        {
            Slot(host, "Dd").Compiling = false;
        }
    }

    [Fact]
    public void ClickingAListRowSelectsThatPage()
    {
        using var host = Discovered("Aaaa", "Bb");
        var window = Window(host);

        Click(window, ListRows(Settled(window))[1][0].Centre);
        Assert.Same(Slot(host, "Aaaa"), window.Selected);

        Click(window, ListRows(Settled(window))[0][0].Centre);
        Assert.Null(window.Selected);

        Click(window, ListRows(Settled(window))[2][0].Centre);
        Assert.Same(Slot(host, "Bb"), window.Selected);
    }

    [Fact]
    public void ReloadBecomesRetryForAnErroredPluginAndBothRestartTheCompile()
    {
        _fixture.Write("Broken", "Main.cs", BrokenSource);
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var broken = Slot(host, "Broken");
        var window = Window(host);

        window.Selected = good;
        var scene = Settled(window);
        var reload = Buttons(scene)[0];
        Assert.Equal("Reload".Length, GlyphsIn(scene, reload));
        Assert.Equal(Normal(ImGuiCol.Button), reload.Col);
        Click(window, reload.Centre);
        Assert.True(good.Compiling);
        PluginFixture.Settle(host);

        window.Selected = broken;
        scene = Settled(window);
        var retry = Buttons(scene)[0];
        Assert.Equal("Retry".Length, GlyphsIn(scene, retry));
        Assert.Equal(Normal(ImGuiCol.Button), retry.Col);
        Click(window, retry.Centre);
        Assert.True(broken.Compiling);
        PluginFixture.Settle(host);
    }

    [Fact]
    public void ReloadIsDisabledWhileCompiling()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Selected = good;

        Assert.Equal(Normal(ImGuiCol.Button), Buttons(Settled(window))[0].Col);
        good.Compiling = true;
        try
        {
            Assert.Equal(Dimmed(ImGuiCol.Button), Buttons(Settled(window))[0].Col);
        }
        finally
        {
            good.Compiling = false;
        }
    }

    [Fact]
    public void WriteDefaultsAndResetAreDisabledWithoutAnInstance()
    {
        _fixture.Write("Broken", "Main.cs", BrokenSource);
        using var host = Discovered("Good");
        var window = Window(host);

        window.Selected = Slot(host, "Good");
        var buttons = Buttons(Settled(window));
        Assert.Equal([Normal(ImGuiCol.Button), Normal(ImGuiCol.Button), Normal(ImGuiCol.Button)], buttons.Take(3).Select(button => button.Col));

        window.Selected = Slot(host, "Broken");
        buttons = Buttons(Settled(window));
        Assert.Equal(Normal(ImGuiCol.Button), buttons[0].Col);
        Assert.Equal([Dimmed(ImGuiCol.Button), Dimmed(ImGuiCol.Button)], buttons.Skip(1).Take(2).Select(button => button.Col));
    }

    [Fact]
    public void AnErroredPluginShowsItsErrorInTheErrorColourAndAHealthyOneDoesNot()
    {
        _fixture.Write("Broken", "Main.cs", BrokenSource);
        using var host = Discovered("Good");
        var window = Window(host);

        window.Selected = Slot(host, "Good");
        Assert.DoesNotContain(Settled(window), quad => quad.Glyph && quad.Min.X > PaneLeft && quad.Col == ErrorColour);

        window.Selected = Slot(host, "Broken");
        Assert.Contains(Settled(window), quad => quad.Glyph && quad.Min.X > PaneLeft && quad.Col == ErrorColour);
    }

    [Fact]
    public void TheErrorBoxDrawsStageMessageAndEveryDiagnosticWrappedInsideThePane()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Selected = good;
        var message = string.Join(" ", Enumerable.Repeat("failure", 40));
        string[] diagnostics = ["Main.cs(3,5): error CS1002: ; expected", string.Join(" ", Enumerable.Repeat("diagnostic", 25))];
        good.ErrorStage = PluginStage.Compile;
        good.Error = message;
        good.Diagnostics = diagnostics;

        try
        {
            var glyphs = Settled(window).Where(quad => quad.Glyph && quad.Min.X > PaneLeft && quad.Col == ErrorColour).ToList();

            Assert.Equal($"{PluginStage.Compile} - {message}{string.Concat(diagnostics)}".Count(letter => letter != ' '), glyphs.Count);
            Assert.All(glyphs, glyph => Assert.True(glyph.Max.X <= PaneRight, "error text runs past the pane"));
            Assert.True(Rows(glyphs).Count > 6, "error text did not wrap");
        }
        finally
        {
            good.ErrorStage = null;
            good.Error = null;
            good.Diagnostics = [];
        }
    }

    [Fact]
    public void EachPluginPageKeepsItsOwnBodyScroll()
    {
        var settings = "public bool[] T = new bool[60]; public override void Draw() { for (var i = 0; i < T.Length; i++) ImGui.Checkbox($\"T{i}\", ref T[i]); }";
        _fixture.Write("First", "Main.cs", PluginFixture.Source("First", "", settings));
        _fixture.Write("Second", "Main.cs", PluginFixture.Source("Second", "", settings));
        using var host = Discovered();
        var window = Window(host);
        window.Selected = Slot(host, "First");
        float FirstBodyBox() => Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - quad.Height) < 1).Skip(1).First().Min.Y;

        var top = FirstBodyBox();
        var io = ImGui.GetIO();
        io.AddMousePosEvent(PaneLeft + 100, Win.Pos.Y + Win.Size.Y - 100);
        Frame(window);
        var body = ImGui.GetCurrentContext().HoveredWindow;
        var grab = new Vector2(body.Pos.X + body.Size.X - 6, body.Pos.Y + 20);
        ImGuiScene.Drag(() => Frame(window), grab, grab + new Vector2(0, 150));
        Assert.NotEqual(top, FirstBodyBox());

        window.Selected = Slot(host, "Second");
        Assert.Equal(top, FirstBodyBox());
    }

    [Fact]
    public void WriteDefaultsButtonWritesDefaultsJson()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Selected = good;
        var path = Path.Combine(good.Directory, "defaults.json");
        Assert.False(File.Exists(path));

        Click(window, Buttons(Settled(window))[1].Centre);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ResetAsksForConfirmationAndOnlyConfirmResets()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Selected = good;
        SetShow(good, false);
                var before = Settled(window);
        Assert.DoesNotContain(before, IsDim);

        Click(window, Buttons(before)[2].Centre);
        var open = Quad.Capture();
        Assert.Contains(open, IsDim);
        Assert.False(Show(good));
        var modal = Buttons(open).Except(Buttons(before)).OrderBy(button => button.Min.X).ToList();
        Assert.Equal(2, modal.Count);

        Click(window, modal[1].Centre);
        Assert.DoesNotContain(Settled(window), IsDim);
        Assert.False(Show(good));

        Click(window, Buttons(Settled(window))[2].Centre);
        modal = Buttons(Quad.Capture()).Except(Buttons(before)).OrderBy(button => button.Min.X).ToList();
        Click(window, modal[0].Centre);

        Assert.True(Show(good));
        Assert.DoesNotContain(Settled(window), IsDim);
    }

    [Fact]
    public void TheEnabledCheckboxIsRightAlignedAndTogglesThePlugin()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Selected = good;

        var box = Checkbox(Settled(window));
        Assert.True(box.Min.X > Win.Pos.X + Win.Size.X / 2);
        Click(window, box.Centre);
        Assert.True(good.Enabled);
        Assert.True(_fixture.Sdk.Plugins["Good"].Enabled);

        Click(window, Checkbox(Settled(window)).Centre);
        Assert.False(good.Enabled);
    }

    [Fact]
    public void TheGeneralPageTogglesAutoReloadAndClearsTheCache()
    {
        using var host = Discovered();
        var cache = Path.Combine(_fixture.Root, "Cache");
        Directory.CreateDirectory(cache);
        var dll = Path.Combine(cache, "old.dll");
        File.WriteAllText(dll, "x");
        var window = Window(host);
        OpenTab(window, 0);

        Assert.False(_fixture.Sdk.InteractiveOutOfFocus);
        Click(window, Checkbox(Settled(window)).Centre);
        Assert.True(_fixture.Sdk.InteractiveOutOfFocus);

        Assert.False(_fixture.Sdk.AutoReload);
        Click(window, Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - quad.Height) < 1).ElementAt(1).Centre);
        Assert.True(_fixture.Sdk.AutoReload);

        Assert.True(File.Exists(dll));
        Click(window, Buttons(Settled(window))[^1].Centre);
        Assert.False(File.Exists(dll));
    }

    [Fact]
    public void TheGeneralTabShowsTheGameStatusAndTheLog()
    {
        using var host = Discovered();
        var logs = new LogBuffer();
        logs.Add(new string('W', 37));
        TestGame.Set(GameState.Empty with { Status = new string('Q', 31) });
        var window = Window(host, logs: logs);
        OpenTab(window, 0);

        try
        {
            var scene = Settled(window);
            Assert.Contains(PageRows(scene), row => row.Count == 31);
            Assert.Contains(PageRows(scene), row => row.Count == 37);
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    [Fact]
    public void TheHotkeysTabShowsBothHotkeyPickers()
    {
        using var host = Discovered();
        var window = Window(host);
        OpenTab(window, 2);

        var scene = Settled(window);
        var buttons = Buttons(scene);
        Assert.Equal(_fixture.Sdk.Hotkeys.Settings.ToString().Replace(" ", "").Length, GlyphsIn(scene, buttons[0]));
        Assert.Equal(_fixture.Sdk.Hotkeys.Quit.ToString().Replace(" ", "").Length, GlyphsIn(scene, buttons[1]));
        Assert.Equal(2, buttons.Count);
    }

    PluginHost WithHotkeys()
    {
        _fixture.Write("Zeta", "Main.cs", PluginFixture.Source("Zeta", """
            public override void Initialize() => RegisterHotkey("Magnify", "Closer", () => { }, new Hotkey(Key.F3));
            """));
        _fixture.Write("alpha", "Main.cs", PluginFixture.Source("alpha", """
            public override void Initialize()
            {
                RegisterHotkey("Teleport", "Jumps", () => { }, new Hotkey(Key.F2), "Movement");
                RegisterHotkey("Blink", "Short jump", () => { }, new Hotkey(Key.F1));
            }
            """));
        var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        PluginFixture.Settle(host);
        host.SetEnabled(Slot(host, "Zeta"), true);
        host.SetEnabled(Slot(host, "alpha"), true);
        return host;
    }

    [Fact]
    public void TheHotkeysTabListsEmberHUDThenEnabledPluginsByNameWithUncategorisedRowsBeforeCategories()
    {
        using var host = WithHotkeys();
        var window = Window(host);
        OpenTab(window, 2);

        var counts = PageRows(Settled(window)).Select(row => row.Count).SkipWhile(count => count != "EmberHUD".Length).ToList();

        Assert.Equal(
            ["EmberHUD".Length, "F11".Length + "Settingswindow".Length, "Ctrl+F12".Length + "Quit".Length,
             "alpha".Length, "F1".Length + "Blink".Length, "Movement".Length, "F2".Length + "Teleport".Length,
             "Zeta".Length, "F3".Length + "Magnify".Length],
            counts);
    }

    [Fact]
    public void ADisabledPluginIsNotListed()
    {
        using var host = WithHotkeys();
        host.SetEnabled(Slot(host, "Zeta"), false);
        var window = Window(host);
        OpenTab(window, 2);

        Assert.DoesNotContain(PageRows(Settled(window)), row => row.Count == "Zeta".Length);
    }

    [Fact]
    public void AKeyBoundTwiceDrawsInTheErrorColour()
    {
        using var host = WithHotkeys();
        Controls.HotkeyConflict = host.ConflictFor;
        Slot(host, "Zeta").Instance!.Hotkeys[0].Value = new Hotkey(Key.F1);
        var window = Window(host);
        OpenTab(window, 2);

        Assert.Contains(Settled(window), quad => quad.Glyph && quad.Min.X > PaneLeft && quad.Col == ErrorColour);
    }
}
