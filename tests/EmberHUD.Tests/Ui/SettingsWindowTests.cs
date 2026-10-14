using System.Text.Json;
using EmberHUD.Host.Plugins;
using EmberHUD.Host.Ui;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public sealed class SettingsWindowTests : IDisposable
{
    const string BrokenSource = "namespace Broken; public sealed class Main : EmberHUD.Plugin { int X = \"no\"; }";

    readonly PluginFixture _fixture = new();
    readonly LogCapture _log = new();
    readonly string _themes = Directory.CreateTempSubdirectory("emberhud-settings-themes-").FullName;
    readonly Queue<Action> _beforeFrame = new();

    public SettingsWindowTests()
    {
        const string flat = """{ "Style": { "WindowRounding": 0, "ChildRounding": 0, "PopupRounding": 0, "FrameRounding": 0, "GrabRounding": 0, "ScrollbarRounding": 0, "TabRounding": 0 } }""";
        File.WriteAllText(Path.Combine(_themes, "Plain.json"), flat);
        File.WriteAllText(Path.Combine(_themes, "Other.json"), flat);
        TestFrame.Run(() => { });
        PristineStyle.Reset();
    }

    static ImGuiWindowPtr Win => ImGuiP.FindWindowByName("EmberHUD###settings");

    static float PaneLeft => Win.Pos.X + ImGui.GetStyle().WindowPadding.X + SettingsWindow.ListWidth + ImGui.GetStyle().ItemSpacing.X / 2;

    static float PaneRight => Win.Pos.X + Win.Size.X;

    static float ContentTop => Win.Pos.Y + ImGui.GetFrameHeight();

    public void Dispose()
    {
        Controls.HotkeyConflict = (_, _) => null;
        _log.Dispose();
        _fixture.Dispose();
        Directory.Delete(_themes, true);
    }

    readonly record struct Quad(Vector2 Min, Vector2 Max, uint Col, bool Glyph)
    {
        public Vector2 Centre => (Min + Max) / 2;

        public float Width => Max.X - Min.X;

        public float Height => Max.Y - Min.Y;

        public bool Contains(Quad other) => other.Centre.X >= Min.X && other.Centre.X <= Max.X && other.Centre.Y >= Min.Y && other.Centre.Y <= Max.Y;

        public static List<Quad> Capture()
        {
            var quads = new List<Quad>();
            var data = ImGui.GetDrawData();
            for (var i = 0; i < data.CmdListsCount; i++)
            {
                var list = data.CmdLists[i];
                var index = list.IdxBuffer;
                for (var at = 0; at < index.Size;)
                {
                    if (at + 6 <= index.Size && IsQuad(index, at))
                    {
                        var corners = Enumerable.Range(0, 4).Select(corner => list.VtxBuffer[index[at] + corner]).ToArray();
                        quads.Add(new Quad(
                            new Vector2(corners.Min(corner => corner.Pos.X), corners.Min(corner => corner.Pos.Y)),
                            new Vector2(corners.Max(corner => corner.Pos.X), corners.Max(corner => corner.Pos.Y)),
                            corners[0].Col,
                            corners[0].Uv != corners[2].Uv));
                        at += 6;
                    }
                    else at += 3;
                }
            }
            return quads;
        }

        static bool IsQuad(ImVector<ushort> index, int at) =>
            index[at + 1] == index[at] + 1 && index[at + 2] == index[at] + 2 && index[at + 3] == index[at] && index[at + 4] == index[at] + 2 && index[at + 5] == index[at] + 3;
    }

    static uint Normal(ImGuiCol col) => ImGui.GetColorU32(col);

    static uint Dimmed(ImGuiCol col) => ImGui.GetColorU32(col, ImGui.GetStyle().DisabledAlpha);

    static uint ErrorColour => EColor.U32(EColor.Bad);

    static List<Quad> Buttons(List<Quad> scene) =>
        [.. scene.Where(quad => !quad.Glyph && quad.Min.X > PaneLeft && (quad.Col == Normal(ImGuiCol.Button) || quad.Col == Dimmed(ImGuiCol.Button) || quad.Col == Normal(ImGuiCol.ButtonActive) || quad.Col == Dimmed(ImGuiCol.ButtonActive)) && quad.Width > quad.Height * 1.2f).OrderBy(quad => quad.Min.Y).ThenBy(quad => quad.Min.X)];

    static List<Quad> Frames(List<Quad> scene) =>
        [.. scene.Where(quad => !quad.Glyph && quad.Min.X > PaneLeft && quad.Col == Normal(ImGuiCol.FrameBg)).OrderBy(quad => quad.Min.Y).ThenBy(quad => quad.Min.X)];

    static bool IsDim(Quad quad) => (quad.Col & 0xFFFFFF) == (Normal(ImGuiCol.ModalWindowDimBg) & 0xFFFFFF) && quad.Width >= ImGui.GetIO().DisplaySize.X;

    static Quad FirstItemBelow(List<Quad> scene, Quad combo) =>
        scene.Where(quad => quad.Glyph && quad.Col == Normal(ImGuiCol.Text) && quad.Min.Y > combo.Max.Y && quad.Centre.X > combo.Min.X && quad.Centre.X < combo.Max.X).OrderBy(quad => quad.Centre.Y).First();

    static Quad Checkbox(List<Quad> scene) => Frames(scene).First(quad => Math.Abs(quad.Width - quad.Height) < 1);

    static int GlyphsIn(List<Quad> scene, Quad box) => scene.Count(quad => quad.Glyph && box.Contains(quad));

    static List<List<Quad>> Rows(IEnumerable<Quad> glyphs)
    {
        var rows = new List<List<Quad>>();
        foreach (var glyph in glyphs.Where(quad => quad.Glyph && quad.Min.Y > ContentTop).OrderBy(quad => quad.Centre.Y))
        {
            if (rows.Count > 0 && glyph.Centre.Y - rows[^1][^1].Centre.Y <= 8) rows[^1].Add(glyph);
            else rows.Add([glyph]);
        }
        return rows;
    }

    static List<List<Quad>> ListRows(List<Quad> scene) => Rows(scene.Where(quad => quad.Max.X < PaneLeft));

    static List<List<Quad>> PageRows(List<Quad> scene) => Rows(scene.Where(quad => quad.Min.X > PaneLeft));

    ThemeStore Themes(string name)
    {
        var themes = new ThemeStore(_themes, Path.Combine(AppContext.BaseDirectory, "Fonts"), name, _beforeFrame.Enqueue);
        themes.Apply();
        return themes;
    }

    SettingsWindow Window(PluginHost host, ThemeStore? themes = null, LogBuffer? logs = null)
    {
        var window = new SettingsWindow(_fixture.Sdk, host, themes ?? Themes("Plain"), logs ?? new LogBuffer());
        window.Toggle();
        return window;
    }

    void Frame(SettingsWindow window)
    {
        while (_beforeFrame.TryDequeue(out var action)) action();
        TestFrame.Run(window.Draw);
    }

    List<Quad> Settled(SettingsWindow window)
    {
        Frame(window);
        Frame(window);
        Frame(window);
        return Quad.Capture();
    }

    void Click(SettingsWindow window, Vector2 at)
    {
        var io = ImGui.GetIO();
        io.AddMousePosEvent(at.X, at.Y);
        Frame(window);
        io.AddMouseButtonEvent(0, true);
        Frame(window);
        io.AddMouseButtonEvent(0, false);
        Frame(window);
        io.AddMousePosEvent(-10000, -10000);
        Frame(window);
        Frame(window);
    }

    void OpenTab(SettingsWindow window, int tab)
    {
        window.Page = SettingsPage.General;
        var labels = PageRows(Settled(window))[0].OrderBy(glyph => glyph.Min.X).ToList();
        Click(window, labels[new[] { 0, "General".Length, "General".Length + "Theme".Length }[tab]].Centre);
    }

    PluginHost Discovered(params string[] good)
    {
        foreach (var name in good) _fixture.Write(name, "Main.cs", PluginFixture.Source(name, "", "public ToggleNode Show = new(true);"));
        var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        PluginFixture.Settle(host);
        return host;
    }

    static ToggleNode Show(PluginSlot slot)
    {
        var settings = slot.Instance!.SettingsObject!;
        return (ToggleNode)settings.GetType().GetField("Show")!.GetValue(settings)!;
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
    public void EveryPageDrawsIncludingAnErroredPlugin()
    {
        _fixture.Write("Broken", "Main.cs", BrokenSource);
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", "", "public ToggleNode Show = new(true);"));
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
            window.Page = SettingsPage.Plugin;
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
        ((List<PluginSlot>)host.Slots).Reverse();
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
        Assert.Equal(SettingsPage.Plugin, window.Page);
        Assert.Same(Slot(host, "Aaaa"), window.Selected);

        Click(window, ListRows(Settled(window))[0][0].Centre);
        Assert.Equal(SettingsPage.General, window.Page);

        Click(window, ListRows(Settled(window))[2][0].Centre);
        Assert.Equal(SettingsPage.Plugin, window.Page);
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
        window.Page = SettingsPage.Plugin;

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
        window.Page = SettingsPage.Plugin;
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
        window.Page = SettingsPage.Plugin;

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
        window.Page = SettingsPage.Plugin;

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
        window.Page = SettingsPage.Plugin;
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
        var settings = string.Join(" ", Enumerable.Range(0, 60).Select(index => $"public ToggleNode T{index} = new(true);"));
        _fixture.Write("First", "Main.cs", PluginFixture.Source("First", "", settings));
        _fixture.Write("Second", "Main.cs", PluginFixture.Source("Second", "", settings));
        using var host = Discovered();
        var window = Window(host);
        window.Page = SettingsPage.Plugin;
        window.Selected = Slot(host, "First");
        float FirstBodyBox() => Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - quad.Height) < 1).Skip(1).First().Min.Y;

        var top = FirstBodyBox();
        var io = ImGui.GetIO();
        io.AddMousePosEvent(PaneLeft + 100, Win.Pos.Y + Win.Size.Y - 100);
        Frame(window);
        var body = ImGui.GetCurrentContext().HoveredWindow;
        var grab = new Vector2(body.Pos.X + body.Size.X - 6, body.Pos.Y + 20);
        io.AddMousePosEvent(grab.X, grab.Y);
        Frame(window);
        io.AddMouseButtonEvent(0, true);
        Frame(window);
        io.AddMousePosEvent(grab.X, grab.Y + 150);
        Frame(window);
        Frame(window);
        io.AddMouseButtonEvent(0, false);
        Frame(window);
        io.AddMousePosEvent(-10000, -10000);
        Frame(window);
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
        window.Page = SettingsPage.Plugin;
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
        window.Page = SettingsPage.Plugin;
        window.Selected = good;
        Show(good).Value = false;
                var before = Settled(window);
        Assert.DoesNotContain(before, IsDim);

        Click(window, Buttons(before)[2].Centre);
        var open = Quad.Capture();
        Assert.Contains(open, IsDim);
        Assert.False(Show(good).Value);
        var modal = Buttons(open).Except(Buttons(before)).OrderBy(button => button.Min.X).ToList();
        Assert.Equal(2, modal.Count);

        Click(window, modal[1].Centre);
        Assert.DoesNotContain(Settled(window), IsDim);
        Assert.False(Show(good).Value);

        Click(window, Buttons(Settled(window))[2].Centre);
        modal = Buttons(Quad.Capture()).Except(Buttons(before)).OrderBy(button => button.Min.X).ToList();
        Click(window, modal[0].Centre);

        Assert.True(Show(good).Value);
        Assert.DoesNotContain(Settled(window), IsDim);
    }

    [Fact]
    public void TheEnabledCheckboxIsRightAlignedAndTogglesThePlugin()
    {
        using var host = Discovered("Good");
        var good = Slot(host, "Good");
        var window = Window(host);
        window.Page = SettingsPage.Plugin;
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

        Assert.False(_fixture.Sdk.AutoReload);
        Click(window, Checkbox(Settled(window)).Centre);
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

    [Fact]
    public void TheThemePageFollowsTheCurrentThemeIntoSdkSettings()
    {
        using var host = Discovered();
        var window = Window(host, Themes("Other"));
        OpenTab(window, 1);

        Assert.Equal("Other", _fixture.Sdk.Theme);
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
            Click(window, Frames(Quad.Capture()).Single(quad => Math.Abs(quad.Width - 260) < 1).Centre);
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

        var sliders = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - 140) < 1).ToList();
        Click(window, new Vector2(sliders[0].Max.X - 1, sliders[0].Centre.Y));
        Settled(window);
        Assert.Equal(40f, themes.Current.Fonts.Regular.Size);
        Assert.Equal(untouched.Mono.Size, themes.Current.Fonts.Mono.Size);

        sliders = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - 140) < 1).ToList();
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

        var mono = Frames(Settled(window)).Where(quad => Math.Abs(quad.Width - (260 - ImGui.GetFrameHeight())) < 1.5f).ToList()[4];
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
