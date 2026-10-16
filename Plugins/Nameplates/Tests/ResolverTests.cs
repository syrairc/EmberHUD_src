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
