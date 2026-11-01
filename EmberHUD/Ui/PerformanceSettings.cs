using System.Reflection;

namespace EmberHUD.Host.Ui;

internal sealed class PerformanceSettings
{
    public const int MinTick = 8, MaxTick = 250, MinCombatPoll = 1, MaxCombatPoll = 100, MinRefresh = 16, MaxRefresh = 2000, MaxThrottle = 2000;

    public int TickMilliseconds = Offsets.ReaderTickMilliseconds;
    public int FighterMilliseconds = Offsets.FighterRefreshMilliseconds;
    public int ProjectileMilliseconds;
    public int GroundHazardMilliseconds;
    public int CastMilliseconds;
    public int CombatPollMilliseconds = Offsets.CombatPollMilliseconds;
    public int CharacterMilliseconds = Offsets.CharacterRefreshMilliseconds;
    public int InventoryMilliseconds = Offsets.InventoryRefreshMilliseconds;
    public int PanelMilliseconds = Offsets.BagRefreshMilliseconds;
    public int GroundItemMilliseconds = Offsets.GroundItemsRefreshMilliseconds;
    public int MinigameMilliseconds = Offsets.TartarusRefreshMilliseconds;
    public int XpMilliseconds = Offsets.XpRefreshMilliseconds;

    public static PerformanceSettings Defaults => new();

    public void Normalize()
    {
        TickMilliseconds = Math.Clamp(TickMilliseconds, MinTick, MaxTick);
        FighterMilliseconds = Math.Clamp(FighterMilliseconds, MinRefresh, MaxRefresh);
        ProjectileMilliseconds = Math.Clamp(ProjectileMilliseconds, 0, MaxThrottle);
        GroundHazardMilliseconds = Math.Clamp(GroundHazardMilliseconds, 0, MaxThrottle);
        CastMilliseconds = Math.Clamp(CastMilliseconds, 0, MaxThrottle);
        CombatPollMilliseconds = Math.Clamp(CombatPollMilliseconds, MinCombatPoll, MaxCombatPoll);
        CharacterMilliseconds = Math.Clamp(CharacterMilliseconds, MinRefresh, MaxRefresh);
        InventoryMilliseconds = Math.Clamp(InventoryMilliseconds, MinRefresh, MaxRefresh);
        PanelMilliseconds = Math.Clamp(PanelMilliseconds, MinRefresh, MaxRefresh);
        GroundItemMilliseconds = Math.Clamp(GroundItemMilliseconds, MinRefresh, MaxRefresh);
        MinigameMilliseconds = Math.Clamp(MinigameMilliseconds, MinRefresh, MaxRefresh);
        XpMilliseconds = Math.Clamp(XpMilliseconds, MinRefresh, MaxRefresh);
    }

    public PerformanceSettings Clamped()
    {
        var copy = (PerformanceSettings)MemberwiseClone();
        copy.Normalize();
        return copy;
    }

    public void Reset()
    {
        var defaults = Defaults;
        foreach (var field in typeof(PerformanceSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            field.SetValue(this, field.GetValue(defaults));
    }
}
