using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EmberHUD.Host.Reading;

internal static partial class VoraxText
{
    [GeneratedRegex(@"\{%s:(\d+)\}")]
    private static partial Regex ParamToken();

    public static string FillParams(string text, IReadOnlyList<int> parameters) =>
        ParamToken().Replace(text, m =>
        {
            return int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= parameters.Count
                ? parameters[n - 1].ToString(CultureInfo.InvariantCulture)
                : m.Value;
        }).Replace("%%", "%");

    public static string StripRichText(string text)
    {
        var result = new StringBuilder(text.Length);
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '<') inTag = true;
            else if (c == '>' && inTag) inTag = false;
            else if (!inTag) result.Append(c);
        }
        return result.ToString();
    }
}
