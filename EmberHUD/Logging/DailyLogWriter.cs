using System.Globalization;
using System.Threading.Channels;

namespace EmberHUD.Host.Logging;

internal sealed class DailyLogWriter : IDisposable
{
    private const string Prefix = "ember-";
    private const string Suffix = ".log";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly string _directory;
    private readonly Channel<(DateTime Time, string Text)> _queue = Channel.CreateUnbounded<(DateTime, string)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Thread _thread;
    private StreamWriter? _file;
    private DateOnly _fileDay;
    private bool _failing;

    public DailyLogWriter(string directory, Func<DateTime> now, int keepDays = 14)
    {
        _directory = directory;
        try
        {
            Directory.CreateDirectory(directory);
            DeleteOldFiles(DateOnly.FromDateTime(now()).AddDays(-keepDays));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"log directory unavailable: {ex.Message}");
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "DailyLogWriter" };
        _thread.Start();
    }

    public static string FileName(DateOnly day) => Prefix + day.ToString(DateFormat, CultureInfo.InvariantCulture) + Suffix;

    public void Write(DateTime time, string text) => _queue.Writer.TryWrite((time, text));

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _thread.Join(TimeSpan.FromSeconds(5));
    }

    private void DeleteOldFiles(DateOnly cutoff)
    {
        foreach (var path in Directory.EnumerateFiles(_directory))
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !name.EndsWith(Suffix, StringComparison.Ordinal))
                continue;
            var date = name.Substring(Prefix.Length, name.Length - Prefix.Length - Suffix.Length);
            if (!DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || day >= cutoff)
                continue;
            try
            {
                File.Delete(Path.Combine(_directory, name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void Run()
    {
        var reader = _queue.Reader;
        while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            while (reader.TryRead(out var entry))
                WriteEntry(entry.Time, entry.Text);
        CloseFile();
    }

    private void WriteEntry(DateTime time, string text)
    {
        try
        {
            var day = DateOnly.FromDateTime(time);
            if (_file != null && day != _fileDay)
                CloseFile();
            if (_file == null)
            {
                Directory.CreateDirectory(_directory);
                var stream = new FileStream(Path.Combine(_directory, FileName(day)), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _file = new StreamWriter(stream) { AutoFlush = true };
                _fileDay = day;
            }

            _file.WriteLine(text);
            _failing = false;
        }
        catch (Exception ex)
        {
            CloseFile();
            if (!_failing)
                Console.Error.WriteLine($"log write failed: {ex.Message}");
            _failing = true;
        }
    }

    private void CloseFile()
    {
        try
        {
            _file?.Dispose();
        }
        catch (Exception)
        {
        }

        _file = null;
    }
}
