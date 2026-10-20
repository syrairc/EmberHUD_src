using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static partial class Controls
{
    // Accounting-style cell: game icon pinned left, amount right-aligned. No icon (or no texture) = amount only
    public static void AmountCell(string text, string? iconTexture = null)
    {
        var left = ImGui.GetCursorPosX();
        var right = left + ImGui.GetContentRegionAvail().X;
        var minX = left;
        if (iconTexture is not null && EmberHUD.Draw.TryGameIcon(iconTexture, out var icon))
        {
            var height = ImGui.GetTextLineHeight();
            IconImage(icon, height, System.Drawing.Color.White);
            ImGui.SameLine(0, 0);
            minX += height + ImGui.GetStyle().ItemSpacing.X;
        }
        ImGui.SetCursorPosX(MathF.Max(minX, right - ImGui.CalcTextSize(text).X));
        ImGui.TextUnformatted(text);
    }
}
