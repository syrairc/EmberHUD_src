using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Threats;

public sealed class GroundHazardsTests
{
    static readonly GroundHazard Pool = new(1, HazardKind.Area, new Vector2(1000, 0), 500);

    [Test]
    public void InsideACircle()
    {
        Check.That(GroundHazards.Inside(Pool, new Vector2(1499, 0)), "499 away");
        Check.That(!GroundHazards.Inside(Pool, new Vector2(1501, 0)), "501 away");
    }

    static readonly GroundHazard Ring = new(2, HazardKind.Damage, new Vector2(1000, 0), 500) { InnerRadius = 450 };

    [Test]
    public void InsideARingOnlyInTheBand()
    {
        Check.That(!GroundHazards.Inside(Ring, new Vector2(1000, 0)), "centre");
        Check.That(!GroundHazards.Inside(Ring, new Vector2(1449, 0)), "just inside the hole");
        Check.That(GroundHazards.Inside(Ring, new Vector2(1451, 0)), "in the band");
        Check.That(GroundHazards.Inside(Ring, new Vector2(1499, 0)), "outer edge");
        Check.That(!GroundHazards.Inside(Ring, new Vector2(1501, 0)), "outside");
    }

    [Test]
    public void ABadInnerRadiusIsNeverInside()
    {
        Check.That(!GroundHazards.Inside(Ring with { InnerRadius = float.NaN }, new Vector2(1451, 0)), "nan");
        Check.That(!GroundHazards.Inside(Ring with { InnerRadius = -1 }, new Vector2(1451, 0)), "negative");
    }
}
