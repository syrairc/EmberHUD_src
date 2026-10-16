using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NameplatesPlugin;

public sealed class AnchorTests
{
    static readonly ScreenRect Track = new(new Vector2(600, 300), new Vector2(680, 306));
    static readonly Vector2 Point = new(500, 300);

    static Entity Make(EntityKind kind, bool hasBar, ScreenRect? bar) => new(1, 1, Vector3.Zero, kind, Rarity.Normal) { HasGameBar = hasBar, GameBar = bar };

    static Settings On() => new() { AnchorToGameBar = true };

    static PlateStyle Style(float borderWidth = 0)
    {
        var style = new PlateStyle();
        style.Bar.Width = 100;
        style.Bar.Height = 12;
        style.Bar.BorderWidth = borderWidth;
        return style;
    }

    static ScreenRect Projected => new(new Vector2(450, 294), new Vector2(550, 306));

    [Test]
    public void GameBarModeUsesTheGameRectAndIgnoresTheBucketSize() =>
        Check.Equal<ScreenRect?>(Track, Anchor.Rect(On(), Make(EntityKind.Monster, true, Track), Style(), Point));

    [Test]
    public void AMissingGameBarSkipsThePlate() =>
        Check.That(Anchor.Rect(On(), Make(EntityKind.Monster, true, null), Style(), Point) is null);

    [Test]
    public void NoGameBarFallsBackToTheProjectedRect() =>
        Check.Equal<ScreenRect?>(Projected, Anchor.Rect(On(), Make(EntityKind.Monster, false, null), Style(), Point));

    [Test]
    public void OnlyMonstersUseTheGameBar()
    {
        foreach (var kind in new[] { EntityKind.Ally, EntityKind.Npc })
            Check.Equal<ScreenRect?>(Projected, Anchor.Rect(On(), Make(kind, true, Track), Style(), Point));
    }

    [Test]
    public void ModeOffUsesTheProjectedRectEvenWithAGameBar() =>
        Check.Equal<ScreenRect?>(Projected, Anchor.Rect(new Settings(), Make(EntityKind.Monster, true, Track), Style(), Point));

    [Test]
    public void NoProjectedPointMeansNoRect() =>
        Check.That(Anchor.Rect(new Settings(), Make(EntityKind.Monster, false, null), Style(), null) is null);

    [Test]
    public void TheBorderWidthGrowsTheGameRectOutward() =>
        Check.Equal<ScreenRect?>(new ScreenRect(new Vector2(597, 297), new Vector2(683, 309)), Anchor.Rect(On(), Make(EntityKind.Monster, true, Track), Style(3), Point));
}
