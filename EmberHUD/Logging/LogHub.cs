using System.Globalization;

namespace EmberHUD.Host.Logging;

internal sealed class LogHub : ILogStoreProvider
{
    private const string Pattern = "ember-*.log";
    private const int LinesPerEntry = 8;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _directory;
    private readonly Func<DateTime> _now;
    private readonly LogEntry[] _ring;
    private int _next;
    private int _count;
    private readonly Lock _gate = new();
    private int _version;

    public LogHub(string logsDirectory, Func<DateTime> now, int capacity = 20000)
    {
        _directory = logsDirectory;
        _now = now;
        _ring = new LogEntry[Math.Max(1, capacity)];
        foreach (var entry in ReadTail(DateOnly.FromDateTime(now()), _ring.Length))
            Push(entry);
    }

    public int Version => Volatile.Read(ref _version);

    public void Add(LogEntry entry)
    {
        lock (_gate) Push(entry);
        Interlocked.Increment(ref _version);
    }

    public LogEntry[] Today(int max)
    {
        var today = _now().Date;
        var found = new List<LogEntry>();
        lock (_gate)
            for (var i = 0; i < _count && found.Count < max; i++)
            {
                var entry = Newest(i);
                if (entry.Time.Date == today) found.Add(entry);
            }

        found.Reverse();
        return [.. found];
    }

    public LogEntry[] Recent(int count)
    {
        lock (_gate)
        {
            var take = Math.Clamp(count, 0, _count);
            var result = new LogEntry[take];
            for (var i = 0; i < take; i++) result[take - 1 - i] = Newest(i);
            return result;
        }
    }

    private void Push(LogEntry entry)
    {
        _ring[_next] = entry;
        _next = (_next + 1) % _ring.Length;
        if (_count < _ring.Length) _count++;
    }

    private LogEntry Newest(int back) => _ring[(_next - 1 - back + 2 * _ring.Length) % _ring.Length];

    public string[] RecentLines(int count) => [.. Recent(count).SelectMany(e => LogFormat.Format(e).Split('\n'))];

    public LogEntry[] Read(DateOnly day, int max) => ReadTail(day, max);

    public DateOnly[] Days()
    {
        try
        {
            return [.. Directory.EnumerateFiles(_directory, Pattern)
                .Select(path => Path.GetFileNameWithoutExtension(path)["ember-".Length..])
                .Select(text => DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? (DateOnly?)day : null)
                .OfType<DateOnly>()
                .OrderDescending()];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private LogEntry[] ReadTail(DateOnly day, int max)
    {
        try
        {
            using var stream = new FileStream(Path.Combine(_directory, DailyLogWriter.FileName(day)), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var window = new Queue<string>();
            var limit = Math.Max(1, max) * LinesPerEntry;
            var truncated = false;
            while (reader.ReadLine() is { } line)
            {
                if (window.Count == limit)
                {
                    window.Dequeue();
                    truncated = true;
                }

                window.Enqueue(line);
            }

            var lines = truncated ? window.SkipWhile(l => l.StartsWith("    ", StringComparison.Ordinal)) : window;
            return [.. LogFormat.Parse(lines, day).TakeLast(max)];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
