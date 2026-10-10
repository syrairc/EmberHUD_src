using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public static class Text
{
    public static bool Matches(string? filter, string? text) => string.IsNullOrWhiteSpace(filter) || (text ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);

    public static string EscPct(string? text) => text?.Replace("%", "%%") ?? "";

    public static void Tip(string? text)
    {
        if (!string.IsNullOrEmpty(text) && ImGui.IsItemHovered()) ImGui.SetTooltip(EscPct(text));
    }

    public static List<string> Wrap(string? text, float maxWidth, float scale)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in (text ?? "").Split(' '))
        {
            var trial = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && ImGui.CalcTextSize(trial).X * scale > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else current = trial;
        }
        if (current.Length > 0) lines.Add(current);
        if (lines.Count == 0) lines.Add(text ?? "");
        return lines;
    }
}
