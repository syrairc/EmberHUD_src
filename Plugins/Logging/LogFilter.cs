using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD;

namespace Logging;

readonly record struct LogRow(string Time, string Level, string Source, string Content, LogLevel Severity, string Full);

sealed class Throttle(long intervalMs, Func<long> clock)
{
    long _last;
    bool _started;

    public bool Due()
    {
        var now = clock();
        if (_started && now - _last < intervalMs) return false;
        _started = true;
        _last = now;
        return true;
    }
}

static class LogFilter
{
    public static LogEntry[] Apply(IReadOnlyList<LogEntry> entries, LogLevel minimum, IReadOnlySet<string> sources, string search) =>
        entries.Where(entry => entry.Level >= minimum
            && (sources.Count == 0 || sources.Contains(entry.Source ?? ""))
            && (search.Length == 0 || Contains(entry.Source, search) || Contains(entry.Message, search))).ToArray();

    public static string Text(IReadOnlyList<LogEntry> entries) => string.Join('\n', entries.Select(LogFormat.Format));

    public static int SourceWidth(IReadOnlyList<LogEntry> entries, int cap = 16) => Math.Min(cap, entries.Select(entry => (entry.Source ?? "").Length).DefaultIfEmpty(0).Max());

    public static string Pad(string source, int width) => source.Length > width ? source[..Math.Max(0, width - 1)] + "~" : source.PadRight(width);

    public static LogRow[] Rows(IReadOnlyList<LogEntry> entries, int sourceWidth)
    {
        var rows = new List<LogRow>(entries.Count);
        foreach (var entry in entries)
        {
            var full = entry.Message ?? "";
            var lines = full.Replace("\r", "").Split('\n');
            rows.Add(new(entry.Time.ToString("HH:mm:ss.fff"), LogFormat.Level(entry.Level), Pad(entry.Source ?? "", sourceWidth), lines[0], entry.Level, full));
            for (var i = 1; i < lines.Length; i++) rows.Add(new("", "", "", lines[i], entry.Level, full));
        }
        return rows.ToArray();
    }

    static bool Contains(string? text, string search) => text is not null && text.Contains(search, StringComparison.OrdinalIgnoreCase);
}
