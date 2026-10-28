using EmberHUD.Host.Ui;

namespace EmberHUD.Host.Reading;

internal static class Performance
{
    private static volatile PerformanceSettings _current = PerformanceSettings.Defaults;

    public static int TickMilliseconds => _current.TickMilliseconds;
    public static int FighterMilliseconds => _current.FighterMilliseconds;
    public static int ProjectileMilliseconds => _current.ProjectileMilliseconds;
    public static int GroundHazardMilliseconds => _current.GroundHazardMilliseconds;
    public static int CastMilliseconds => _current.CastMilliseconds;
    public static int CombatPollMilliseconds => _current.CombatPollMilliseconds;
    public static int CharacterMilliseconds => _current.CharacterMilliseconds;
    public static int InventoryMilliseconds => _current.InventoryMilliseconds;
    public static int PanelMilliseconds => _current.PanelMilliseconds;
    public static int GroundItemMilliseconds => _current.GroundItemMilliseconds;
    public static int MinigameMilliseconds => _current.MinigameMilliseconds;
    public static int XpMilliseconds => _current.XpMilliseconds;

    public static void Apply(PerformanceSettings settings) => _current = settings.Clamped();

    public static void Reset() => _current = PerformanceSettings.Defaults;
}
