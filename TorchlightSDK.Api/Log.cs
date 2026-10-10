namespace TorchlightSDK;

public enum LogLevel { Info, Warn, Error }

public static class Log
{
    internal static readonly AsyncLocal<string?> CurrentPlugin = new();

    internal static Action<LogLevel, string?, string>? Sink { get; set; }

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Warn(string message) => Write(LogLevel.Warn, message);

    public static void Error(string message) => Write(LogLevel.Error, message);

    public static void Error(Exception exception, string? context = null) => Write(LogLevel.Error, context is null ? exception.ToString() : $"{context} - {exception}");

    static void Write(LogLevel level, string message) => Sink?.Invoke(level, CurrentPlugin.Value, message);
}
