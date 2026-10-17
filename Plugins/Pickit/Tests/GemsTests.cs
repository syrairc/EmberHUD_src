using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class GemsTests
{
    static GroundItem Item(int[] prefixes, int[] suffixes, float height = 30) => new(1, new Vector2(100, 200), new Vector2(300, 200 + height), Vector3.Zero, true, prefixes, suffixes);

    [Test]
    public void PrefixesLeftSuffixesRight()
    {
        var gems = Gems.Layout(Item([5], [5]), new Settings());
        Check.Equal(2, gems.Length);
        var prefix = gems.Single(g => g.Prefix);
        var suffix = gems.Single(g => !g.Prefix);
        Check.That(prefix.Centre.X < 200 && suffix.Centre.X > 200);
    }

    [Test]
    public void BestTierSitsAtTheCorner()
    {
        var settings = new Settings();
        var gems = Gems.Layout(Item([3, 1, 2], []), settings);
        Check.Equal(1, gems[0].Tier);
        Check.Equal(3, gems[^1].Tier);
        Check.That(gems[0].Centre.X < gems[^1].Centre.X);
        settings.BestIsLowest = false;
        gems = Gems.Layout(Item([3, 1, 2], []), settings);
        Check.Equal(3, gems[0].Tier);
        var suffixes = Gems.Layout(Item([], [3, 1, 2]), settings);
        Check.Equal(3, suffixes[0].Tier);
        Check.That(suffixes[0].Centre.X > suffixes[^1].Centre.X);
    }

    [Test]
    public void GemCentreIsOnTheTopEdgeByDefault()
    {
        var gem = Gems.Layout(Item([1], []), new Settings())[0];
        Check.Equal(200f, gem.Centre.Y);
        Check.Equal(0.33f * 30 / 2, gem.Radius);
        var settings = new Settings { AnchorY = 1 };
        Check.Equal(230f, Gems.Layout(Item([1], []), settings)[0].Centre.Y);
    }

    [Test]
    public void OnlyPrefixesDrawsOnlyLeft()
    {
        var gems = Gems.Layout(Item([1, 2], []), new Settings());
        Check.Equal(2, gems.Length);
        Check.That(gems.All(g => g.Prefix && g.Centre.X < 200));
    }

    [Test]
    public void NoAffixesNoGems() => Check.Equal(0, Gems.Layout(Item([], []), new Settings()).Length);

    [Test]
    public void CapsAtMaxAffixes()
    {
        var many = Enumerable.Range(0, 20).ToArray();
        var gems = Gems.Layout(Item(many, many), new Settings());
        Check.Equal(GroundItem.MaxAffixes * 2, gems.Length);
        Check.Equal(GroundItem.MaxAffixes, gems.Count(g => g.Prefix));
    }

    [Test]
    public void TierOutOfRangeClampsToLastStyle()
    {
        var settings = new Settings();
        settings.Tiers[9].Scale = 2;
        var gem = Gems.Layout(Item([99], []), settings)[0];
        Check.Equal(99, gem.Tier);
        Check.Equal(0.33f * 30 / 2 * 2, gem.Radius);
    }

    [Test]
    public void ZeroSizeLabelGivesNoGems()
    {
        var settings = new Settings();
        Check.Equal(0, Gems.Layout(new GroundItem(1, Vector2.Zero, Vector2.Zero, Vector3.Zero, true, [1], [1]), settings).Length);
        Check.Equal(0, Gems.Layout(new GroundItem(1, new Vector2(10, 10), new Vector2(0, 0), Vector3.Zero, true, [1], [1]), settings).Length);
    }

    [Test]
    public void SpacingAndInsetMoveTheGems()
    {
        var settings = new Settings();
        var plain = Gems.Layout(Item([1, 2], []), settings);
        settings.GemSpacing = 1;
        settings.InsetX = 10;
        var moved = Gems.Layout(Item([1, 2], []), settings);
        Check.Equal(plain[0].Centre.X + 10, moved[0].Centre.X);
        Check.That(moved[1].Centre.X - moved[0].Centre.X > plain[1].Centre.X - plain[0].Centre.X);
    }

    [Test]
    public void ScaleChangesRadius()
    {
        var settings = new Settings();
        var before = Gems.Layout(Item([1], []), settings)[0].Radius;
        settings.Tiers[1].Scale = 2;
        Check.Equal(before * 2, Gems.Layout(Item([1], []), settings)[0].Radius);
    }
}
