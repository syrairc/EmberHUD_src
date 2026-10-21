using EmberHUD.Host.Ui;

namespace EmberHUD.Host.Logging;

internal static class LogLevels
{
    public static readonly string[] HostSources = ["Host", "Loader", "Reader", "Overlay", "Updater", "Icons", "Theme"];

    public static Func<LogLevel, string?, bool> Filter(SdkSettings sdk) => (level, source) =>
    {
        var overrides = sdk.LogSources;
        var floor = overrides is not null && overrides.TryGetValue(source ?? "Host", out var found) ? found : sdk.LogMinimum;
        return level >= Valid(floor);
    };

    public static LogLevel Valid(LogLevel level) => Enum.IsDefined(level) ? level : LogLevel.Info;

    public static void SetOverride(SdkSettings sdk, string source, LogLevel? level)
    {
        var copy = new Dictionary<string, LogLevel>(sdk.LogSources);
        if (level is { } value) copy[source] = value;
        else copy.Remove(source);
        sdk.LogSources = copy;
    }

    public static int OverrideIndex(Dictionary<string, LogLevel> sources, string source) => sources.TryGetValue(source, out var level) ? (int)Valid(level) + 1 : 0;

    public static LogLevel? FromOverrideIndex(int index) => index <= 0 ? null : (LogLevel)(index - 1);
}
