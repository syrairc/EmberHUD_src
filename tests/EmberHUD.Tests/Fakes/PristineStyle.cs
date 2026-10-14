using Hexa.NET.ImGui;

namespace EmberHUD.Tests;

internal static class PristineStyle
{
    static readonly Lazy<ImGuiStyle> Captured = new(Capture);

    public static unsafe void Reset() => *ImGui.GetStyle().Handle = Captured.Value;

    static unsafe ImGuiStyle Capture()
    {
        var main = ImGui.GetCurrentContext();
        var scratch = ImGui.CreateContext();
        ImGui.SetCurrentContext(scratch);
        var style = *ImGui.GetStyle().Handle;
        ImGui.SetCurrentContext(main);
        ImGui.DestroyContext(scratch);
        return style;
    }
}
