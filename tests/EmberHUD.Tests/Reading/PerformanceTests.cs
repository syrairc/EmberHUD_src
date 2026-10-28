using EmberHUD.Host;
using EmberHUD.Host.Ui;

namespace EmberHUD.Tests;

public class PerformanceTests
{
    [Fact]
    public void DefaultsEqualTheOffsetsKeys()
    {
        var defaults = PerformanceSettings.Defaults;
        Assert.Equal(Offsets.ReaderTickMilliseconds, defaults.TickMilliseconds);
        Assert.Equal(Offsets.FighterRefreshMilliseconds, defaults.FighterMilliseconds);
        Assert.Equal(0, defaults.ProjectileMilliseconds);
        Assert.Equal(0, defaults.GroundHazardMilliseconds);
        Assert.Equal(0, defaults.CastMilliseconds);
        Assert.Equal(Offsets.CombatPollMilliseconds, defaults.CombatPollMilliseconds);
        Assert.Equal(Offsets.CharacterRefreshMilliseconds, defaults.CharacterMilliseconds);
        Assert.Equal(Offsets.InventoryRefreshMilliseconds, defaults.InventoryMilliseconds);
        Assert.Equal(Offsets.BagRefreshMilliseconds, defaults.PanelMilliseconds);
        Assert.Equal(Offsets.StashRefreshMilliseconds, defaults.PanelMilliseconds);
        Assert.Equal(Offsets.GroundItemsRefreshMilliseconds, defaults.GroundItemMilliseconds);
        Assert.Equal(Offsets.TartarusRefreshMilliseconds, defaults.MinigameMilliseconds);
        Assert.Equal(Offsets.TarotRefreshMilliseconds, defaults.MinigameMilliseconds);
        Assert.Equal(Offsets.VoraxRefreshMilliseconds, defaults.MinigameMilliseconds);
        Assert.Equal(Offsets.FrozenRefreshMilliseconds, defaults.MinigameMilliseconds);
        Assert.Equal(Offsets.XpRefreshMilliseconds, defaults.XpMilliseconds);
    }

    [Fact]
    public void NormalizeClampsEveryFieldIntoItsRange()
    {
        var low = new PerformanceSettings
        {
            TickMilliseconds = -5, FighterMilliseconds = 0, ProjectileMilliseconds = -1, GroundHazardMilliseconds = -1, CastMilliseconds = -1, CombatPollMilliseconds = 0,
            CharacterMilliseconds = 1, InventoryMilliseconds = 1, PanelMilliseconds = 1, GroundItemMilliseconds = 1, MinigameMilliseconds = 1, XpMilliseconds = 1,
        };
        low.Normalize();
        Assert.Equal((8, 16, 0, 0, 0, 1), (low.TickMilliseconds, low.FighterMilliseconds, low.ProjectileMilliseconds, low.GroundHazardMilliseconds, low.CastMilliseconds, low.CombatPollMilliseconds));
        Assert.Equal((16, 16, 16, 16, 16, 16), (low.CharacterMilliseconds, low.InventoryMilliseconds, low.PanelMilliseconds, low.GroundItemMilliseconds, low.MinigameMilliseconds, low.XpMilliseconds));

        var high = new PerformanceSettings
        {
            TickMilliseconds = 9999, FighterMilliseconds = 9999, ProjectileMilliseconds = 9999, GroundHazardMilliseconds = 9999, CastMilliseconds = 9999, CombatPollMilliseconds = 9999,
            CharacterMilliseconds = 9999, InventoryMilliseconds = 9999, PanelMilliseconds = 9999, GroundItemMilliseconds = 9999, MinigameMilliseconds = 9999, XpMilliseconds = 9999,
        };
        high.Normalize();
        Assert.Equal((250, 2000, 2000, 2000, 2000, 100), (high.TickMilliseconds, high.FighterMilliseconds, high.ProjectileMilliseconds, high.GroundHazardMilliseconds, high.CastMilliseconds, high.CombatPollMilliseconds));
        Assert.Equal((2000, 2000, 2000, 2000, 2000, 2000), (high.CharacterMilliseconds, high.InventoryMilliseconds, high.PanelMilliseconds, high.GroundItemMilliseconds, high.MinigameMilliseconds, high.XpMilliseconds));
    }

    [Fact]
    public void ClampedLeavesTheSourceAlone()
    {
        var source = new PerformanceSettings { TickMilliseconds = 1 };
        Assert.Equal(8, source.Clamped().TickMilliseconds);
        Assert.Equal(1, source.TickMilliseconds);
    }

    [Fact]
    public void ResetRestoresEveryDefault()
    {
        var settings = new PerformanceSettings { TickMilliseconds = 100, CastMilliseconds = 50, XpMilliseconds = 500, PanelMilliseconds = 200 };
        settings.Reset();
        Assert.Equivalent(PerformanceSettings.Defaults, settings);
    }

    [Fact]
    public void RoundTripsThroughSettingsJson()
    {
        var sdk = new SdkSettings();
        sdk.Performance.TickMilliseconds = 50;
        sdk.Performance.ProjectileMilliseconds = 120;
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, SettingsJson.Serialize(sdk), _ => { });
        Assert.Equal(50, loaded.Performance.TickMilliseconds);
        Assert.Equal(120, loaded.Performance.ProjectileMilliseconds);
        Assert.Equal(Offsets.XpRefreshMilliseconds, loaded.Performance.XpMilliseconds);
    }

    [Fact]
    public void OldSdkJsonWithoutPerformanceLoadsDefaults()
    {
        var loaded = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, "{}", _ => { });
        Assert.Equivalent(PerformanceSettings.Defaults, loaded.Performance);
    }

    [Fact]
    public void ApplyStoresAClampedCopyAndResetGoesBack()
    {
        try
        {
            var settings = new PerformanceSettings { TickMilliseconds = 1, FighterMilliseconds = 400, CombatPollMilliseconds = 10 };
            Performance.Apply(settings);
            Assert.Equal(8, Performance.TickMilliseconds);
            Assert.Equal(400, Performance.FighterMilliseconds);
            Assert.Equal(10, Performance.CombatPollMilliseconds);
            settings.FighterMilliseconds = 900;
            Assert.Equal(400, Performance.FighterMilliseconds);
        }
        finally { Performance.Reset(); }

        Assert.Equal(Offsets.ReaderTickMilliseconds, Performance.TickMilliseconds);
        Assert.Equal(Offsets.FighterRefreshMilliseconds, Performance.FighterMilliseconds);
        Assert.Equal(0, Performance.CastMilliseconds);
    }

    [Fact]
    public void ThrottleWithZeroReadsEveryCall()
    {
        var throttle = new ReadThrottle<int[]>();
        var reads = 0;
        for (var i = 0; i < 3; i++) throttle.Read(true, 0, 1000, 1, () => new int[++reads]);
        Assert.Equal(3, reads);
    }

    [Fact]
    public void ThrottleReadsAtMostOncePerInterval()
    {
        var throttle = new ReadThrottle<int[]>();
        var reads = 0;
        var first = throttle.Read(true, 100, 1000, 1, () => new int[++reads]);
        Assert.Same(first, throttle.Read(true, 100, 1099, 1, () => new int[++reads]));
        Assert.Equal(1, reads);
        Assert.NotSame(first, throttle.Read(true, 100, 1100, 1, () => new int[++reads]));
        Assert.Equal(2, reads);
    }

    [Fact]
    public void ThrottleRereadsOnAreaChangeAndForgetsWhenNotWanted()
    {
        var throttle = new ReadThrottle<int[]>();
        var reads = 0;
        throttle.Read(true, 1000, 1000, 1, () => new int[++reads]);
        throttle.Read(true, 1000, 1001, 2, () => new int[++reads]);
        Assert.Equal(2, reads);
        Assert.Null(throttle.Read(false, 1000, 1002, 2, () => new int[++reads]));
        throttle.Read(true, 1000, 1003, 2, () => new int[++reads]);
        Assert.Equal(3, reads);
    }

    [Fact]
    public void ReaderLoopAsksForTheTickEveryIteration()
    {
        var asked = 0;
        using var loop = new ReaderLoop(() => new Counting(), _ => { }, () => Interlocked.Increment(ref asked) < 3 ? 8 : 20);
        loop.Start();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref asked) > 5, 2000));
    }

    private sealed class Counting : IGameSource
    {
        public bool TryRead(out GameState state, out string? failure)
        {
            state = GameState.Empty with { Status = "ok" };
            failure = null;
            return true;
        }

        public void Dispose() { }
    }
}
