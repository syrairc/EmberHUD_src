using EmberHUD.Host.Ui;

namespace EmberHUD.Host.Logging;

internal sealed class PluginLogLevels
{
    readonly Lock _gate = new();
    volatile Dictionary<string, LogLevel> _levels = [];

    public IReadOnlyDictionary<string, LogLevel> Snapshot => _levels;

    public LogLevel? Get(string? plugin) => plugin is not null && _levels.TryGetValue(plugin, out var level) ? level : null;

    public void Set(string plugin, LogLevel level)
    {
        lock (_gate) _levels = new Dictionary<string, LogLevel>(_levels) { [plugin] = level };
    }

    public void Remove(string plugin)
    {
        lock (_gate)
        {
            var copy = new Dictionary<string, LogLevel>(_levels);
            if (copy.Remove(plugin)) _levels = copy;
        }
    }
}

internal static class LogLevels
{
    public static Func<LogLevel, string?, bool> Filter(SdkSettings sdk, PluginLogLevels plugins) =>
        (level, source) => level >= Valid(plugins.Get(source) ?? sdk.LogMinimum);

    public static LogLevel Valid(LogLevel level) => Enum.IsDefined(level) ? level : LogLevel.Info;
}
