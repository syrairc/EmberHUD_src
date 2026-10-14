using EmberHUD.Host.Plugins;
using EmberHUD.Host.Ui;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;
using static EmberHUD.Tests.ImGuiScene;
using static EmberHUD.Tests.PluginFixture;

namespace EmberHUD.Tests;

public abstract class SettingsWindowFixture : IDisposable
{
    internal readonly PluginFixture _fixture = new();
    internal readonly LogCapture _log = new();
    internal readonly TempDir _temp = new();
    internal readonly Queue<Action> _beforeFrame = new();

    internal string _themes => _temp.Path;

    protected SettingsWindowFixture()
    {
        const string flat = """{ "Style": { "WindowRounding": 0, "ChildRounding": 0, "PopupRounding": 0, "FrameRounding": 0, "GrabRounding": 0, "ScrollbarRounding": 0, "TabRounding": 0 } }""";
        File.WriteAllText(Path.Combine(_themes, "Plain.json"), flat);
        File.WriteAllText(Path.Combine(_themes, "Other.json"), flat);
        TestFrame.Run(() => { });
        ImGuiScene.ResetStyle();
    }

    internal static ImGuiWindowPtr Win => ImGuiP.FindWindowByName("EmberHUD###settings");

    internal static float PaneLeft => Win.Pos.X + ImGui.GetStyle().WindowPadding.X + SettingsWindow.ListWidth + ImGui.GetStyle().ItemSpacing.X / 2;

    internal static float PaneRight => Win.Pos.X + Win.Size.X;

    internal static float ContentTop => Win.Pos.Y + ImGui.GetFrameHeight();

    public void Dispose()
    {
        Controls.HotkeyConflict = (_, _) => null;
        _log.Dispose();
        _fixture.Dispose();
        _temp.Dispose();
    }

    internal static uint Normal(ImGuiCol col) => ImGui.GetColorU32(col);

    internal static uint Dimmed(ImGuiCol col) => ImGui.GetColorU32(col, ImGui.GetStyle().DisabledAlpha);

    internal static uint ErrorColour => EColor.U32(EColor.Bad);

    internal static List<Quad> Buttons(List<Quad> scene) =>
        [.. scene.Where(quad => !quad.Glyph && quad.Min.X > PaneLeft && (quad.Col == Normal(ImGuiCol.Button) || quad.Col == Dimmed(ImGuiCol.Button) || quad.Col == Normal(ImGuiCol.ButtonActive) || quad.Col == Dimmed(ImGuiCol.ButtonActive)) && quad.Width > quad.Height * 1.2f).OrderBy(quad => quad.Min.Y).ThenBy(quad => quad.Min.X)];

    internal static List<Quad> Frames(List<Quad> scene) =>
        [.. scene.Where(quad => !quad.Glyph && quad.Min.X > PaneLeft && quad.Col == Normal(ImGuiCol.FrameBg)).OrderBy(quad => quad.Min.Y).ThenBy(quad => quad.Min.X)];

    internal static bool IsDim(Quad quad) => (quad.Col & 0xFFFFFF) == (Normal(ImGuiCol.ModalWindowDimBg) & 0xFFFFFF) && quad.Width >= ImGui.GetIO().DisplaySize.X;

    internal static Quad FirstItemBelow(List<Quad> scene, Quad combo) =>
        scene.Where(quad => quad.Glyph && quad.Col == Normal(ImGuiCol.Text) && quad.Min.Y > combo.Max.Y && quad.Centre.X > combo.Min.X && quad.Centre.X < combo.Max.X).OrderBy(quad => quad.Centre.Y).First();

    internal static Quad Checkbox(List<Quad> scene) => Frames(scene).First(quad => Math.Abs(quad.Width - quad.Height) < 1);

    internal static int GlyphsIn(List<Quad> scene, Quad box) => scene.Count(quad => quad.Glyph && box.Contains(quad));

    internal static List<List<Quad>> Rows(IEnumerable<Quad> glyphs)
    {
        var rows = new List<List<Quad>>();
        foreach (var glyph in glyphs.Where(quad => quad.Glyph && quad.Min.Y > ContentTop).OrderBy(quad => quad.Centre.Y))
        {
            if (rows.Count > 0 && glyph.Centre.Y - rows[^1][^1].Centre.Y <= 8) rows[^1].Add(glyph);
            else rows.Add([glyph]);
        }
        return rows;
    }

    internal static List<List<Quad>> ListRows(List<Quad> scene) => Rows(scene.Where(quad => quad.Max.X < PaneLeft));

    internal static List<List<Quad>> PageRows(List<Quad> scene) => Rows(scene.Where(quad => quad.Min.X > PaneLeft));

    internal ThemeStore Themes(string name)
    {
        var themes = new ThemeStore(_themes, Path.Combine(AppContext.BaseDirectory, "Fonts"), name, _beforeFrame.Enqueue);
        themes.Changed += changed => _fixture.Sdk.Theme = changed;
        themes.Apply();
        return themes;
    }

    internal SettingsWindow Window(PluginHost host, ThemeStore? themes = null, LogBuffer? logs = null)
    {
        var window = new SettingsWindow(_fixture.Sdk, host, themes ?? Themes("Plain"), logs ?? new LogBuffer());
        window.Toggle();
        return window;
    }

    internal void Frame(SettingsWindow window)
    {
        while (_beforeFrame.TryDequeue(out var action)) action();
        TestFrame.Run(window.Draw);
    }

    internal List<Quad> Settled(SettingsWindow window)
    {
        Frame(window);
        Frame(window);
        Frame(window);
        return Quad.Capture();
    }

    internal void Click(SettingsWindow window, Vector2 at) => ImGuiScene.Click(() => Frame(window), at);

    internal void OpenTab(SettingsWindow window, int tab)
    {
        window.Selected = null;
        var labels = PageRows(Settled(window))[0].OrderBy(glyph => glyph.Min.X).ToList();
        Click(window, labels[new[] { 0, "General".Length, "General".Length + "Theme".Length }[tab]].Centre);
    }

    internal PluginHost Discovered(params string[] good)
    {
        foreach (var name in good) _fixture.Write(name, "Main.cs", PluginFixture.Source(name, "", "public bool Show = true;"));
        var host = _fixture.Host();
        host.Discover(_fixture.Plugins);
        PluginFixture.Settle(host);
        return host;
    }

    internal static bool Show(PluginSlot slot) => (bool)slot.Instance!.SettingsObject!.GetType().GetField("Show")!.GetValue(slot.Instance.SettingsObject)!;

    internal static void SetShow(PluginSlot slot, bool value) => slot.Instance!.SettingsObject!.GetType().GetField("Show")!.SetValue(slot.Instance.SettingsObject, value);
}
