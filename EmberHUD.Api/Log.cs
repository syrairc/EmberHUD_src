namespace EmberHUD;

public enum LogLevel { Trace, Debug, Info, Warn, Error }

public static class Log
{
    internal static readonly AsyncLocal<string?> CurrentPlugin = new();

    internal static Action<LogLevel, string?, string>? Sink { get; set; }

    internal static Func<LogLevel, string?, bool>? Filter { get; set; }

    public static bool IsEnabled(LogLevel level) => Passes(level, CurrentPlugin.Value);

    public static void Trace(string message) => Write(LogLevel.Trace, message);

    public static void Debug(string message) => Write(LogLevel.Debug, message);

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Warn(string message) => Write(LogLevel.Warn, message);

    public static void Error(string message) => Write(LogLevel.Error, message);

    public static void Error(Exception exception, string? context = null) => Write(LogLevel.Error, Format(exception, context));

    internal static SourceLog For(string source) => new(source);

    internal static string Format(Exception exception, string? context) => context is null ? exception.ToString() : $"{context} - {exception}";

    internal static bool Passes(LogLevel level, string? source) => Filter?.Invoke(level, source) ?? true;

    static void Write(LogLevel level, string message)
    {
        var plugin = CurrentPlugin.Value;
        if (Passes(level, plugin)) Sink?.Invoke(level, plugin, message);
    }
}

internal readonly struct SourceLog(string source)
{
    public void Trace(string message) => Write(LogLevel.Trace, message);

    public void Debug(string message) => Write(LogLevel.Debug, message);

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warn(string message) => Write(LogLevel.Warn, message);

    public void Error(string message) => Write(LogLevel.Error, message);

    public void Error(Exception exception, string? context = null) => Write(LogLevel.Error, Log.Format(exception, context));

    void Write(LogLevel level, string message)
    {
        var tag = Log.CurrentPlugin.Value ?? source;
        if (Log.Passes(level, tag)) Log.Sink?.Invoke(level, tag, message);
    }
}
