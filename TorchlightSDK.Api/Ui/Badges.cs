using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public static class Badges
{
    const float DotR = 3f, DotGap = 5f;

    public static string CountText(int n, int max = 99) => n > max ? max + "+" : n.ToString();

    public static void Draw(string? text, Color? color = null, bool outline = false, bool dot = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var style = ImGui.GetStyle();
        var ts = ImGui.CalcTextSize(text);
        var padX = style.FramePadding.X;
        var size = new Vector2(ts.X + padX * 2 + (dot ? DotR * 2 + DotGap : 0f), ts.Y + style.FramePadding.Y);
        var org = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);

        var dl = ImGui.GetWindowDrawList();
        var fill = color.HasValue ? EColor.U32(color.Value) : ImGui.GetColorU32(ImGuiCol.Header);
        var ink = color.HasValue ? EColor.U32(EColor.Contrast(color.Value)) : ImGui.GetColorU32(ImGuiCol.Text);
        var r = size.Y * 0.5f;
        if (dot)
        {
            dl.AddRectFilled(org, org + size, ImGui.GetColorU32(ImGuiCol.FrameBg), r);
            dl.AddRect(org, org + size, ImGui.GetColorU32(ImGuiCol.Border), r);
            dl.AddCircleFilled(new Vector2(org.X + padX + DotR, org.Y + size.Y * 0.5f), DotR, fill);
            dl.AddText(org + new Vector2(padX + DotR * 2 + DotGap, (size.Y - ts.Y) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), text);
            return;
        }
        if (outline) dl.AddRect(org, org + size, fill, r);
        else dl.AddRectFilled(org, org + size, fill, r);
        dl.AddText(org + new Vector2(padX, (size.Y - ts.Y) * 0.5f), outline ? fill : ink, text);
    }

    public static float Width(string? text, bool dot = false) =>
        string.IsNullOrEmpty(text) ? 0f : ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2 + (dot ? DotR * 2 + DotGap : 0f);

    public static void Counter(int count, Color? color = null, int max = 99, bool showZero = false)
    {
        if (count > 0 || showZero) Draw(CountText(count, max), color);
    }
}
