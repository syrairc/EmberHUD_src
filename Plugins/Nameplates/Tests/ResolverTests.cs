using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NameplatesPlugin;

public sealed class ResolverTests
{
    static Entity Make(EntityKind kind = EntityKind.Monster, Rarity rarity = Rarity.Normal, bool friendly = false, bool hidden = false, string name = "Thing", int cfgId = 1, double life = 50, double maxLife = 100) =>
        new(1, cfgId, Vector3.Zero, kind, rarity) { Friendly = friendly, Hidden = hidden, Name = name, Life = life, MaxLife = maxLife };

    static PlateStyle Styled(float offsetY) => new() { OffsetY = offsetY };

    [Test]
    public void TheBossOnTheBarIsTheBossWhoseHealthMatchesIt()
    {
        var near = Make(rarity: Rarity.Boss, name: "Near", life: 36, maxLife: 100);
        var far = Make(rarity: Rarity.Boss, name: "Far", life: 90, maxLife: 100);
        Check.Equal("Near", Resolver.BossOnBar([far, near], 0.364f)!.Value.Name);
        Check.Equal("Far", Resolver.BossOnBar([far, near], 0.9f)!.Value.Name);
    }

    [Test]
    public void NoBossMatchesWhenNothingIsCloseOrEligible()
    {
        Check.That(Resolver.BossOnBar([Make(rarity: Rarity.Boss, life: 90)], 0.2f) is null, "too far from the bar");
        Check.That(Resolver.BossOnBar([Make(rarity: Rarity.Rare, life: 20)], 0.2f) is null, "not a boss");
        Check.That(Resolver.BossOnBar([Make(rarity: Rarity.Boss, life: 20, friendly: true)], 0.2f) is null, "friendly");
        Check.That(Resolver.BossOnBar([Make(rarity: Rarity.Boss, life: 20, hidden: true)], 0.2f) is null, "hidden");
        Check.That(Resolver.BossOnBar([Make(rarity: Rarity.Boss, life: 0, maxLife: 0)], 0f) is null, "no health pool");
        Check.That(Resolver.BossOnBar([], 0.5f) is null, "no entities");
    }

    [Test]
    public void MonsterRaritiesMapToBuckets()
    {
        Check.Equal(Bucket.Normal, Resolver.BucketOf(Make(rarity: Rarity.Normal)));
        Check.Equal(Bucket.Magic, Resolver.BucketOf(Make(rarity: Rarity.Magic)));
        Check.Equal(Bucket.Rare, Resolver.BucketOf(Make(rarity: Rarity.Rare)));
        Check.Equal(Bucket.Boss, Resolver.BucketOf(Make(rarity: Rarity.Boss)));
    }

    [Test]
    public void FriendlyMonsterAllyAndPlayerAreAlly()
    {
        Check.Equal(Bucket.Ally, Resolver.BucketOf(Make(friendly: true)));
        Check.Equal(Bucket.Ally, Resolver.BucketOf(Make(EntityKind.Ally)));
        Check.Equal(Bucket.Ally, Resolver.BucketOf(Make(EntityKind.Player)));
    }

    [Test]
    public void NpcAndSelfNotConfusedWithMonsters()
    {
        Check.Equal(Bucket.Npc, Resolver.BucketOf(Make(EntityKind.Npc, Rarity.Boss)));
        Check.That(Resolver.BucketOf(Make()) != Bucket.Self);
    }

    [Test]
    public void PortalsAndMapObjectsGetNoPlate()
    {
        Check.That(Resolver.BucketOf(Make(EntityKind.Portal)) is null);
        Check.That(Resolver.BucketOf(Make(EntityKind.MapObject)) is null);
        Check.That(Resolver.BucketOf(Make(EntityKind.Destructible)) is null);
    }

    [Test]
    public void HiddenGetsNoPlate() => Check.That(Resolver.Resolve(new Settings(), Make(hidden: true), 0) is null);

    [Test]
    public void BucketOffGivesNull()
    {
        var settings = new Settings();
        settings.Buckets[(int)Bucket.Normal].Show = false;
        Check.That(Resolver.Resolve(settings, Make(), 0) is null);
    }

    [Test]
    public void OverDistanceGivesNull()
    {
        var settings = new Settings { MaxDistance = 100 };
        Check.That(Resolver.Resolve(settings, Make(), 101) is null);
        Check.That(Resolver.Resolve(settings, Make(), 100) is not null);
    }

    [Test]
    public void NaNDistanceGivesNull() => Check.That(Resolver.Resolve(new Settings(), Make(), float.NaN) is null);

    [Test]
    public void NaNLifeCountsAsNotDamaged()
    {
        var settings = new Settings();
        settings.Buckets[(int)Bucket.Normal].DamagedOnly = true;
        Check.That(Resolver.Resolve(settings, Make(life: double.NaN, maxLife: 100), 0) is null);
        Check.That(Resolver.Resolve(settings, Make(life: 50, maxLife: double.NaN), 0) is null);
    }

    [Test]
    public void DistanceIsShownInUnits()
    {
        var settings = new Settings { DistanceUnit = 50 };
        Check.Equal(2f, settings.Displayed(100));
        settings.DistanceUnit = 0;
        Check.Equal(1f, settings.Displayed(100));
        settings.DistanceUnit = float.PositiveInfinity;
        Check.Equal(1f, settings.Displayed(100));
        settings.DistanceUnit = float.NaN;
        Check.Equal(1f, settings.Displayed(100));
    }

    [Test]
    public void DamagedOnlyHidesFullAndUnknownHealth()
    {
        var settings = new Settings();
        settings.Buckets[(int)Bucket.Normal].DamagedOnly = true;
        Check.That(Resolver.Resolve(settings, Make(life: 100, maxLife: 100), 0) is null);
        Check.That(Resolver.Resolve(settings, Make(life: 0, maxLife: 0), 0) is null);
        Check.That(Resolver.Resolve(settings, Make(life: 99, maxLife: 100), 0) is not null);
    }

    [Test]
    public void NameOverrideWins()
    {
        var settings = new Settings();
        var style = Styled(7);
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.Name, Name = "THING", Style = style });
        Check.That(ReferenceEquals(style, Resolver.Resolve(settings, Make(name: "thing"), 0)));
    }

    [Test]
    public void CfgIdOverrideWins()
    {
        var settings = new Settings();
        var style = Styled(7);
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.CfgId, CfgId = 42, Style = style });
        Check.That(ReferenceEquals(style, Resolver.Resolve(settings, Make(cfgId: 42), 0)));
        Check.That(!ReferenceEquals(style, Resolver.Resolve(settings, Make(cfgId: 43), 0)));
    }

    [Test]
    public void EmptyNameOverrideNeverMatches()
    {
        var settings = new Settings();
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.Name, Name = "", Style = Styled(7) });
        Check.That(ReferenceEquals(settings.Buckets[(int)Bucket.Normal].Style, Resolver.Resolve(settings, Make(name: ""), 0)));
    }

    [Test]
    public void ZeroCfgIdOverrideNeverMatches()
    {
        var settings = new Settings();
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.CfgId, CfgId = 0, Style = Styled(7) });
        Check.That(ReferenceEquals(settings.Buckets[(int)Bucket.Normal].Style, Resolver.Resolve(settings, Make(cfgId: 0), 0)));
    }

    [Test]
    public void DisabledOverrideIsIgnored()
    {
        var settings = new Settings();
        settings.Overrides.Add(new StyleOverride { Enabled = false, Kind = MatchKind.Name, Name = "Thing", Style = Styled(7) });
        Check.That(ReferenceEquals(settings.Buckets[(int)Bucket.Normal].Style, Resolver.Resolve(settings, Make(), 0)));
    }

    [Test]
    public void FirstMatchingOverrideWins()
    {
        var settings = new Settings();
        var first = Styled(1);
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.Name, Name = "Thing", Style = first });
        settings.Overrides.Add(new StyleOverride { Kind = MatchKind.Name, Name = "Thing", Style = Styled(2) });
        Check.That(ReferenceEquals(first, Resolver.Resolve(settings, Make(), 0)));
    }
}
