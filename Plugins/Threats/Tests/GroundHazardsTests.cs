using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

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

    static List<(Vector2 Pos, uint Col)> Drawn(Settings settings, params GroundHazard[] hazards)
    {
        TestGame.Set(new GameState { Camera = Overdraw.Looking });
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            Groups.Declare(settings);
            foreach (var hazard in hazards) GroundHazards.Draw(hazard, settings, new Vector2(5000, 5000));
        });
        var found = new List<(Vector2, uint)>();
        for (var i = 0; i < list.VtxBuffer.Size; i++) found.Add((list.VtxBuffer[i].Pos, list.VtxBuffer[i].Col));
        return found;
    }

    static float FillArea(Settings settings, GroundHazard hazard)
    {
        TestGame.Set(new GameState { Camera = Overdraw.Looking });
        ImDrawListPtr list = default;
        TestFrame.Run(() =>
        {
            list = Draw.Background;
            Groups.Declare(settings);
            GroundHazards.Draw(hazard, settings, new Vector2(5000, 5000));
        });
        return Overdraw.Area(list, EColor.U32(Color.FromArgb(100, 11, 22, 33)));
    }

    [Test]
    public void OverlappingHazardsDrawOneOutline()
    {
        var settings = new Settings { AreaColour = Color.FromArgb(200, 11, 22, 33), FillOpacity = 0.5f };
        var single = Drawn(settings, Pool);
        var both = Drawn(settings, Pool, Pool with { Centre = new Vector2(1100, 0) });
        Check.That(single.Count > 0, "single drawn");
        Check.That(both.Count < single.Count * 2, "overlap not merged");
        var fill = (uint)Color.FromArgb(100, 11, 22, 33).A;
        Check.That(both.All(v => v.Col >> 24 == fill || v.Col >> 24 == 200), "fill alpha stacked or wrong");
    }

    [Test]
    public void RingHazardStillHasHole()
    {
        var settings = new Settings { AreaColour = Color.FromArgb(200, 11, 22, 33), FillOpacity = 0.5f };
        var camera = Overdraw.Looking;
        var rim = camera.ToScreen(new Vector3(1000, 200, 0), new Vector2(1920, 1080))!.Value;
        var vertices = Drawn(settings, Ring with { InnerRadius = 200, Kind = HazardKind.Area });
        Check.That(vertices.Any(v => v.Col >> 24 == 200 && Vector2.Distance(v.Pos, rim) < 8), "no outline around the hole");
        var ratio = FillArea(settings, Ring with { InnerRadius = 200, Kind = HazardKind.Area }) / FillArea(settings, Ring with { InnerRadius = 0, Kind = HazardKind.Area });
        Check.That(ratio < 0.95f, $"fill covers the hole {ratio}");
    }
}
