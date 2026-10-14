using Hexa.NET.ImGui;

namespace EmberHUD.Ui;

public static class Text
{
    public static bool Matches(string? filter, string? text) => string.IsNullOrWhiteSpace(filter) || (text ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);

    public static string EscPct(string? text) => text?.Replace("%", "%%") ?? "";

    public static void Tip(string? text)
    {
        if (!string.IsNullOrEmpty(text) && ImGui.IsItemHovered()) ImGui.SetTooltip(EscPct(text));
    }

    public static string[] Wrap(string? text, float maxWidth, float scale) => Wrap(text, maxWidth, s => ImGui.CalcTextSize(s).X * scale);

    public static string[] Wrap(string? text, float maxWidth, TextStyle style) => Wrap(text, maxWidth, s => Draw.MeasureText(s, style).X);

    internal static string[] Wrap(string? text, float maxWidth, Func<string, float> width)
    {
        text ??= "";
        if (width(text) <= maxWidth) return [text];

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var space = width(" ");
        List<string> lines = [];
        var start = 0;
        var lineWidth = 0f;
        for (var i = 0; i < words.Length; i++)
        {
            var wordWidth = width(words[i]);
            if (i > start && lineWidth + space + wordWidth > maxWidth)
            {
                lines.Add(string.Join(' ', words, start, i - start));
                start = i;
                lineWidth = wordWidth;
            }
            else lineWidth += (i > start ? space : 0f) + wordWidth;
        }
        lines.Add(string.Join(' ', words, start, words.Length - start));
        return [.. lines];
    }
}
