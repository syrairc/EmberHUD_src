using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public static class RichText
{
    public static Vector2 Pill(ImDrawListPtr dl, ImFontPtr font, float size, Vector2 pos, string text, Color textColor, Color bg, float padX = 4f, float padY = 2f, float rounding = 2f)
    {
        var ts = ImGui.CalcTextSizeA(font, size, float.MaxValue, 0f, text);
        var min = new Vector2(pos.X - padX, pos.Y - padY);
        var max = new Vector2(pos.X + ts.X + padX, pos.Y + ts.Y + padY);
        if (bg.A > 0) dl.AddRectFilled(min, max, EColor.U32(bg), rounding);
        ImGui.AddText(dl, font, size, pos, EColor.U32(textColor), text);
        return max - min;
    }

    public static void Highlight(ImDrawListPtr dl, Vector2 min, Vector2 max, Color color, float thickness = 3f, float rounding = 0f, float expand = 2f)
    {
        var e = new Vector2(expand);
        dl.AddRect(min - e, max + e, EColor.U32(color), rounding, ImDrawFlags.None, thickness);
    }
}
