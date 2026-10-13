namespace EmberHUD;

public static class Game
{
    static int _minimapRescans;

    public static GameState State { get; internal set; } = GameState.Empty;

    public static void RescanMinimap() => Interlocked.Increment(ref _minimapRescans);

    internal static bool TakeMinimapRescan() => Interlocked.Exchange(ref _minimapRescans, 0) > 0;
}
