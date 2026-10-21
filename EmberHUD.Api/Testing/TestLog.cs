namespace EmberHUD.Testing;

public static class TestLog
{
    static int version;

    public static void Use(params LogEntry[] entries) => LogStore.Provider = new Fake(entries, ++version);

    public static void Reset() => LogStore.Provider = null;

    sealed class Fake(LogEntry[] entries, int version) : ILogStoreProvider
    {
        public int Version => version;

        public LogEntry[] Today(int max) => entries.TakeLast(max).ToArray();

        public DateOnly[] Days() => [];

        public LogEntry[] Read(DateOnly day, int max) => [];
    }
}
