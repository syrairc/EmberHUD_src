using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

static class UiFrame
{
    public static readonly Vector2 WindowSize = new(800, 600);

    public static ImGuiIOPtr Io()
    {
        if (ImGui.GetCurrentContext().IsNull) Run("ensure context", () => { });
        var io = ImGui.GetIO();
        io.MouseDoubleClickTime = 0f;
        return io;
    }

    static int _ids;

    public static string NextId(string prefix) => prefix + Interlocked.Increment(ref _ids);

    public static T Measure<T>(Func<T> read)
    {
        T value = default!;
        Run("measure", () => value = read());
        return value;
    }

    public sealed record StyleMetrics(float FrameHeight, float FontSize, Vector2 Spacing, Vector2 FramePadding, Vector2 WindowPadding, float Alpha, float ChildRounding, Vector4 ChildBg, Vector4 WindowBg, Vector4 FrameBg);

    static StyleMetrics? _style;

    public static StyleMetrics Style => _style ??= Measure(() =>
    {
        var style = ImGui.GetStyle();
        return new StyleMetrics(ImGui.GetFrameHeight(), ImGui.GetFontSize(), style.ItemSpacing, style.FramePadding, style.WindowPadding, style.Alpha, style.ChildRounding, style.Colors[(int)ImGuiCol.ChildBg], style.Colors[(int)ImGuiCol.WindowBg], style.Colors[(int)ImGuiCol.FrameBg]);
    });

    static readonly Dictionary<string, float> _textWidths = [];

    public static float TextWidth(string text)
    {
        if (!_textWidths.TryGetValue(text, out var width)) _textWidths[text] = width = Measure(() => ImGui.CalcTextSize(text).X);
        return width;
    }

    public static float ButtonWidth(string label) => TextWidth(label) + Style.FramePadding.X * 2;

    public static void Settle(string window, Action body)
    {
        var io = Io();
        io.AddMouseButtonEvent(0, false);
        io.AddMouseButtonEvent(1, false);
        io.AddKeyEvent(ImGuiKey.DownArrow, false);
        io.AddKeyEvent(ImGuiKey.UpArrow, false);
        io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        Run(window, body);
        Run(window, body);
    }

    public static void Unhover() => Settle("unhover", () => { });

    public static bool PopupOpen(string window, Action body)
    {
        var open = false;
        Run(window, () =>
        {
            body();
            open = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId);
        });
        return open;
    }

    public static void TwoScopes(Action a, Action b)
    {
        ImGui.PushID("A");
        a();
        ImGui.PopID();
        ImGui.PushID("B");
        b();
        ImGui.PopID();
    }

    public static void CloseModal(string window, Action body, Func<Vector2> cancel)
    {
        for (var i = 0; i < 2 && PopupOpen(window, body); i++) Click(window, cancel(), body);
    }

    public static void Hover(string window, Vector2 at, Action body)
    {
        Io().AddMousePosEvent(at.X, at.Y);
        Run(window, body);
        Run(window, body);
    }

    public static void ClickAway(string window, Action body) => Click(window, WindowSize - new Vector2(100, 100), body);

    public static void Run(string window, Action body) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(WindowSize);
        ImGui.Begin(window, ImGuiWindowFlags.NoSavedSettings);
        var host = ImGuiP.GetCurrentWindow();
        var ids = host.IDStack.Size;
        body();
        if (host.IDStack.Size != ids) throw new InvalidOperationException($"the body left the ID stack at {host.IDStack.Size} instead of {ids}");
        if (!ImGuiP.GetCurrentTable().IsNull) throw new InvalidOperationException("the body left a table open");
        ImGui.End();
    });

    public static void Click(string window, Vector2 at, Action body, int button = 0)
    {
        var io = Io();
        Run(window, body);
        io.AddMousePosEvent(at.X, at.Y);
        Run(window, body);
        io.AddMouseButtonEvent(button, true);
        Run(window, body);
        io.AddMouseButtonEvent(button, false);
        Run(window, body);
        io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        Run(window, body);
    }

    public static void Press(string window, Vector2 at, Action body, int button = 0)
    {
        var io = Io();
        io.AddMousePosEvent(at.X, at.Y);
        Run(window, body);
        Run(window, body);
        io.AddMouseButtonEvent(button, true);
        Run(window, body);
    }

    public static void MoveTo(string window, Vector2 at, Action body)
    {
        Io().AddMousePosEvent(at.X, at.Y);
        Run(window, body);
    }

    public static void Release(string window, Action body, int button = 0)
    {
        var io = Io();
        io.AddMouseButtonEvent(button, false);
        Run(window, body);
        io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        Run(window, body);
    }

    public static void Drag(string window, Vector2 from, Vector2 to, Action body)
    {
        Press(window, from, body);
        const int steps = 4;
        for (var i = 1; i <= steps; i++) MoveTo(window, Vector2.Lerp(from, to, i / (float)steps), body);
        Release(window, body);
    }

    public readonly record struct ModalMetrics(float ConfirmWidth, float CancelWidth, float FrameHeight, float FontSize, Vector2 Spacing, Vector2 WindowPadding);

    public static ModalMetrics ReadModalMetrics() => new(ButtonWidth("Confirm"), ButtonWidth("Cancel"), Style.FrameHeight, Style.FontSize, Style.Spacing, Style.WindowPadding);

    public static Vector2 ConfirmModalButton(Vector2 modalPos, bool confirm, ModalMetrics m)
    {
        var x = m.WindowPadding.X + (confirm ? m.ConfirmWidth / 2 : m.ConfirmWidth + m.Spacing.X + m.CancelWidth / 2);
        var y = m.FrameHeight + m.WindowPadding.Y + m.FontSize + m.Spacing.Y + m.FrameHeight / 2;
        return modalPos + new Vector2(x, y);
    }

    public static void EraseText(string window, Action body, Vector2 box)
    {
        Click(window, box, body);
        var io = Io();
        io.AddKeyEvent(ImGuiKey.ModCtrl, true);
        io.AddKeyEvent(ImGuiKey.A, true);
        Run(window, body);
        io.AddKeyEvent(ImGuiKey.A, false);
        io.AddKeyEvent(ImGuiKey.ModCtrl, false);
        Run(window, body);
        io.AddKeyEvent(ImGuiKey.Backspace, true);
        Run(window, body);
        io.AddKeyEvent(ImGuiKey.Backspace, false);
        Run(window, body);
        ClickAway(window, body);
    }

    public static List<ImDrawVert> Verts(ImDrawListPtr list)
    {
        List<ImDrawVert> verts = [];
        for (var i = 0; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
        return verts;
    }

    public static List<Vector2> GlyphUvs(string window, string text)
    {
        List<Vector2> uvs = [];
        Run(window, () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            ImGui.TextUnformatted(text);
            for (var i = from; i < list.VtxBuffer.Size; i++) uvs.Add(list.VtxBuffer[i].Uv);
        });
        return uvs;
    }

    public static List<Vector2>? ActiveTooltipUvs()
    {
        var windows = ImGui.GetCurrentContext().Windows;
        for (var i = 0; i < windows.Size; i++)
        {
            var window = windows[i];
            if (window.Active && (window.Flags & ImGuiWindowFlags.Tooltip) != 0) return Verts(window.DrawList).Select(v => v.Uv).ToList();
        }
        return null;
    }
}
