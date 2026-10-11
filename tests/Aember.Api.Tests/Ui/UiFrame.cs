using Aember.Testing;
using Hexa.NET.ImGui;

namespace Aember.Api.Tests;

static class UiFrame
{
    public static readonly Vector2 WindowSize = new(800, 600);

    public static ImGuiIOPtr Io()
    {
        if (ImGui.GetCurrentContext().IsNull) Run("ensure context", () => { });
        return ImGui.GetIO();
    }

    public static void Unhover()
    {
        var io = Io();
        io.AddMouseButtonEvent(0, false);
        io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        Run("unhover", () => { });
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
        body();
        ImGui.End();
    });

    public static void Click(string window, Vector2 at, Action body, int button = 0)
    {
        var io = Io();
        for (var i = 0; i < io.MouseDoubleClickTime * 60 + 1; i++) Run(window, body);
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

    public static ModalMetrics ReadModalMetrics()
    {
        var metrics = default(ModalMetrics);
        Run("modal metrics", () =>
        {
            var style = ImGui.GetStyle();
            metrics = new ModalMetrics(
                ImGui.CalcTextSize("Confirm").X + style.FramePadding.X * 2,
                ImGui.CalcTextSize("Cancel").X + style.FramePadding.X * 2,
                ImGui.GetFrameHeight(),
                ImGui.GetFontSize(),
                style.ItemSpacing,
                style.WindowPadding);
        });
        return metrics;
    }

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
