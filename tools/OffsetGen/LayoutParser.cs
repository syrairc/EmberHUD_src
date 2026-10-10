using System.Globalization;

namespace OffsetGen;

internal static class LayoutParser
{
    public static IReadOnlyList<(string Name, int Value)> Parse(string text)
    {
        var entries = new List<(string Name, int Value)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) throw new FormatException($"expected 'Name = value': {line}");
            var name = parts[0];
            if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_') || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new FormatException($"bad name '{name}'");
            if (entries.Any(e => e.Name == name)) throw new FormatException($"duplicate name '{name}'");

            var number = parts[1];
            var parsed = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(number[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? (int?)hex : null
                : int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : null;
            entries.Add((name, parsed ?? throw new FormatException($"bad value '{number}' for {name}")));
        }

        return entries;
    }
}
