using System.Globalization;

namespace OffsetGen;

internal static class LayoutParser
{
    public static IReadOnlyList<(string Name, int Value)> Parse(string text)
    {
        var entries = new List<(string Name, int Value)>();
        foreach (var (name, raw) in Lines(text))
        {
            if (raw.StartsWith('"')) continue;
            var parsed = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) && hex <= int.MaxValue ? (int?)hex : null
                : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : null;
            entries.Add((name, parsed ?? throw new FormatException($"bad value '{raw}' for {name}")));
        }

        return entries;
    }

    public static IReadOnlyList<(string Name, string Value)> ParseStrings(string text)
    {
        var entries = new List<(string Name, string Value)>();
        foreach (var (name, raw) in Lines(text))
        {
            if (!raw.StartsWith('"')) continue;
            if (raw.Length < 3 || !raw.EndsWith('"')) throw new FormatException($"bad string '{raw}' for {name}");
            entries.Add((name, raw[1..^1]));
        }

        return entries;
    }

    private static IEnumerable<(string Name, string Raw)> Lines(string text)
    {
        var seen = new HashSet<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) throw new FormatException($"expected 'Name = value': {line}");
            var name = parts[0];
            if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_') || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new FormatException($"bad name '{name}'");
            if (!seen.Add(name)) throw new FormatException($"duplicate name '{name}'");
            yield return (name, parts[1]);
        }
    }
}
