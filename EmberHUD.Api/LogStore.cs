namespace EmberHUD;

internal interface ILogStoreProvider
{
    int Version { get; }

    LogEntry[] Today(int max);

    DateOnly[] Days();

    LogEntry[] Read(DateOnly day, int max);
}

public static class LogStore
{
    internal static ILogStoreProvider? Provider { get; set; }

    public static int Version => Provider?.Version ?? 0;

    public static LogEntry[] Today(int max) => Provider?.Today(max) ?? [];

    public static DateOnly[] Days() => Provider?.Days() ?? [];

    public static LogEntry[] Read(DateOnly day, int max) => Provider?.Read(day, max) ?? [];
}
