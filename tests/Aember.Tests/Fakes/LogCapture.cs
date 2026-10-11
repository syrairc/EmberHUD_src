namespace Aember.Tests;

internal sealed class LogCapture : IDisposable
{
    readonly List<(LogLevel Level, string? Plugin, string Message)> _lines = [];

    public LogCapture() => Log.Sink = (level, plugin, message) =>
    {
        lock (_lines) _lines.Add((level, plugin, message));
    };

    public string Text
    {
        get
        {
            lock (_lines) return string.Join("\n", _lines.Select(line => $"{line.Level} [{line.Plugin}] {line.Message}"));
        }
    }

    public List<string> Messages(string? plugin = null)
    {
        lock (_lines) return [.. _lines.Where(line => plugin is null || line.Plugin == plugin).Select(line => line.Message)];
    }

    public void Dispose() => Log.Sink = null;
}
