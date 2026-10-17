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
}
