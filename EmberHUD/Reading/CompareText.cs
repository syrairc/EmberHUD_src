using System.Globalization;

namespace EmberHUD.Host.Reading;

internal static class CompareText
{
    internal static float? Parse(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.EndsWith('%')) return null;
        if (!float.TryParse(trimmed[..^1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return null;
        if (!float.IsFinite(value) || Math.Abs(value) > 10000) return null;
        return value;
    }
}
