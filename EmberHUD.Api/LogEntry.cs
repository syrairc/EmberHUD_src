using System.Text.RegularExpressions;

namespace EmberHUD;

public readonly record struct LogEntry(DateTime Time, LogLevel Level, string Source, string Message);

public static partial class LogFormat
{
    const string Indent = "    ";

    [GeneratedRegex(@"^([01]\d|2[0-3]):([0-5]\d):([0-5]\d)\.(\d{3}) (TRACE|DEBUG|INFO |WARN |ERROR) \[([^\]]*)\] ?(.*)$")]
    private static partial Regex HeaderLine();

    public static string Level(LogLevel level) => level.ToString().ToUpperInvariant().PadRight(5);

    public static string Format(LogEntry entry)
    {
        var lines = (entry.Message ?? "").Replace("\r", "").Split('\n');
        var header = $"{entry.Time:HH:mm:ss.fff} {Level(entry.Level)} [{entry.Source ?? ""}]";
        if (lines[0].Length > 0) header += " " + lines[0];
        return string.Join('\n', lines.Skip(1).Select(l => Indent + l).Prepend(header));
    }

    public static List<LogEntry> Parse(IEnumerable<string> lines, DateOnly day)
    {
        var entries = new List<LogEntry>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var header = HeaderLine().Match(line);
            if (header.Success)
            {
                var time = day.ToDateTime(new TimeOnly(int.Parse(header.Groups[1].Value), int.Parse(header.Groups[2].Value), int.Parse(header.Groups[3].Value), int.Parse(header.Groups[4].Value)));
                var level = Enum.Parse<LogLevel>(header.Groups[5].Value.Trim(), ignoreCase: true);
                entries.Add(new(time, level, header.Groups[6].Value, header.Groups[7].Value));
            }
            else if (line.StartsWith(Indent) && entries.Count > 0)
            {
                var last = entries[^1];
                entries[^1] = last with { Message = last.Message + "\n" + line[Indent.Length..] };
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                entries.Add(new(day.ToDateTime(TimeOnly.MinValue), LogLevel.Info, "", line));
            }
        }
        return entries;
    }
}
