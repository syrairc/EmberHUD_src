using System.Collections;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Minimap;

public sealed class RegionTests
{
    static readonly Color Blue = Color.FromArgb(0x1F, 0x5F, 0xB0, 0xB8);
    static readonly Color White = Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);

    static Region Build(Terrain terrain, ExploredTrail? trail, Vector2? player = null, Region? previous = null) => Region.Build(1, terrain, trail, player, Blue, White, previous);

    static ExploredTrail Trail(float radius, params Vector2[] points) => new(points, radius);

    static Terrain Row(int cols) => Scene.Cells(cols, 1, false, [.. Enumerable.Range(0, cols).Select(col => (col, 0))]);

    static Color Texel(Region region, int x, int y)
    {
        var at = (y * region.Width + x) * 4;
        return Color.FromArgb(region.Pixels[at + 3], region.Pixels[at], region.Pixels[at + 1], region.Pixels[at + 2]);
    }

    static int Count(BitArray bits)
    {
        var count = 0;
        for (var i = 0; i < bits.Length; i++)
            if (bits[i]) count++;
        return count;
    }

    static bool Same(BitArray a, BitArray b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    [Test]
    public void ACellIsExploredWhenItsCentreIsWithinTheRadius()
    {
        var region = Build(Row(5), Trail(10, new Vector2(5, 5)));

        Check.That(region.Explored[0] && region.Explored[1], "centres 0 and 10 away");
        Check.That(!region.Explored[2] && !region.Explored[3], "centre 20 away");
    }

    [Test]
    public void TheOddRowShiftMovesTheCentreTheTestUses()
    {
        var region = Build(Scene.Cells(2, 2, true, (0, 0), (1, 0), (0, 1), (1, 1)), Trail(1, new Vector2(10, 15)));

        Check.That(region.Explored[2], "odd row cell 0 is centred on x 10");
        Check.That(!region.Explored[0] && !region.Explored[1] && !region.Explored[3], "nothing else within 1");
    }

    [Test]
    public void ThePlayerStampsLikeATrailPoint()
    {
        var region = Build(Row(5), Trail(4, new Vector2(5, 5)), new Vector2(45, 5));

        Check.That(region.Explored[0] && region.Explored[4], "first trail point and the player");
        Check.That(!region.Explored[2], "nothing between");
    }

    [Test]
    public void AnIncrementalStampEqualsAFullRebuild()
    {
        var terrain = Scene.Cells(10, 10, true);
        Vector2[] walk = [new(5, 5), new(30, 20), new(60, 45), new(80, 80), new(15, 90)];
        var first = Build(terrain, Trail(12, walk[..2]));
        var more = Build(terrain, Trail(12, walk), previous: first);
        var full = Build(terrain, Trail(12, walk));

        Check.That(Count(full.Explored) > 0, "the walk explores something");
        Check.That(Same(more.Explored, full.Explored), "incremental == full");
    }

    [Test]
    public void AnIncrementalStampKeepsWhatThePlayerAlreadyRevealed()
    {
        var terrain = Row(5);
        var first = Build(terrain, Trail(4, new Vector2(5, 5)), new Vector2(45, 5));
        var next = Build(terrain, Trail(4, new Vector2(5, 5), new Vector2(25, 5)), previous: first);

        Check.That(next.Explored[4] && next.Explored[2], "kept the old player stamp and added the new point");
    }

    [Test]
    public void AShorterTrailANewRadiusOrAnotherTerrainStartsOver()
    {
        var terrain = Row(5);
        var first = Build(terrain, Trail(4, new Vector2(5, 5), new Vector2(45, 5)));
        Check.That(!Build(terrain, Trail(4, new Vector2(5, 5)), previous: first).Explored[4], "shorter trail");

        var wide = Build(terrain, Trail(12, new Vector2(5, 5)));
        Check.That(wide.Explored[1], "radius 12 reaches the next cell");
        Check.That(!Build(terrain, Trail(4, new Vector2(5, 5), new Vector2(45, 5)), previous: wide).Explored[1], "new radius");

        var walked = Build(terrain, Trail(4, new Vector2(5, 5)), new Vector2(45, 5));
        Check.That(!Build(Row(5), Trail(4, new Vector2(5, 5), new Vector2(15, 5)), previous: walked).Explored[4], "another terrain");
    }

    [Test]
    public void NonFinitePointsAndRadiusAreSkipped()
    {
        var terrain = Row(5);
        var region = Build(terrain, Trail(4, new Vector2(float.NaN, 5), new Vector2(25, float.PositiveInfinity), new Vector2(45, 5)), new Vector2(float.NaN, 0));
        Check.That(region.Explored[4] && !region.Explored[2], "only the finite point");

        foreach (var radius in new[] { float.NaN, float.PositiveInfinity, 0f, -3f })
            Check.Equal(0, Count(Build(terrain, Trail(radius, new Vector2(5, 5)), new Vector2(5, 5)).Explored), $"radius {radius}");
    }

    [Test]
    public void PointsFarOffTheGridOrAHugeRadiusNeverThrow()
    {
        var terrain = Row(5);

        Check.Equal(0, Count(Build(terrain, Trail(4, new Vector2(1e30f, -1e30f), new Vector2(-1e9f, 5))).Explored));
        Check.Equal(5, Count(Build(terrain, Trail(1e9f, new Vector2(5, 5))).Explored));
    }

    [Test]
    public void AWalkableBitmapShorterThanTheGridNeverThrows()
    {
        var region = Build(new Terrain(4, 4, 10, Vector2.Zero, false, new BitArray(2, true)), Trail(4, new Vector2(5, 5)));

        Check.Equal(8 * 4 * 4, region.Pixels.Length);
        Check.Equal(White.ToArgb(), Texel(region, 0, 0).ToArgb());
        Check.Equal(0, (int)Texel(region, 0, 1).A);
    }

    [Test]
    public void WalkableCellsAreBlueExploredOnesWhiteAndTheRestClear()
    {
        var region = Build(Scene.Cells(3, 1, false, (0, 0), (1, 0)), Trail(4, new Vector2(15, 5)));

        Check.Equal(6, region.Width);
        Check.Equal(1, region.Height);
        Check.Equal(Blue.ToArgb(), Texel(region, 0, 0).ToArgb());
        Check.Equal(Blue.ToArgb(), Texel(region, 1, 0).ToArgb());
        Check.Equal(White.ToArgb(), Texel(region, 2, 0).ToArgb());
        Check.Equal(White.ToArgb(), Texel(region, 3, 0).ToArgb());
        Check.Equal(0, (int)Texel(region, 4, 0).A);
        Check.Equal(0, (int)Texel(region, 5, 0).A);
    }

    [Test]
    public void AnOddRowsTwoTexelsSitOneTexelOver()
    {
        var region = Build(Scene.Cells(2, 2, true, (0, 0), (0, 1)), Trail(1, new Vector2(10, 15)));

        Check.Equal(5, region.Width);
        Check.Equal(Blue.ToArgb(), Texel(region, 0, 0).ToArgb());
        Check.Equal(Blue.ToArgb(), Texel(region, 1, 0).ToArgb());
        Check.Equal(0, (int)Texel(region, 2, 0).A);
        Check.Equal(0, (int)Texel(region, 0, 1).A);
        Check.Equal(White.ToArgb(), Texel(region, 1, 1).ToArgb());
        Check.Equal(White.ToArgb(), Texel(region, 2, 1).ToArgb());
        Check.Equal(0, (int)Texel(region, 3, 1).A);
    }

    [Test]
    public void ClearTexelsKeepTheFillColourSoTheEdgeDoesNotDarken() =>
        Check.Equal(Color.FromArgb(0, Blue).ToArgb(), Texel(Build(Scene.Cells(2, 1, false, (0, 0)), Trail(4, new Vector2(5, 5))), 3, 0).ToArgb());

    [Test]
    public void TheQuadCoversTheGridIncludingTheShiftedHalfCell()
    {
        var region = Build(new Terrain(3, 2, 10, new Vector2(100, 200), true, new BitArray(6, true)), null);

        Check.That(region.Corners.SequenceEqual(new[] { new Vector2(100, 200), new Vector2(135, 200), new Vector2(135, 220), new Vector2(100, 220) }), string.Join(" ", region.Corners));
    }

    [Test]
    public void NoTrailExploresEveryWalkableCellAndOutlinesTheWholeRegion()
    {
        var region = Build(Scene.Cells(4, 3, false, (1, 1), (2, 1)), null);

        Check.Equal(White.ToArgb(), Texel(region, 2, 1).ToArgb());
        Check.Equal(White.ToArgb(), Texel(region, 5, 1).ToArgb());
        Check.Equal(6, region.ExploredOutline.Single().Points.Length);
    }

    [Test]
    public void TheExploredOutlineGoesAroundTheExploredCellsOnly()
    {
        var loop = Build(Scene.Cells(4, 3, false, (1, 1), (2, 1)), Trail(4, new Vector2(15, 15))).ExploredOutline.Single();

        Check.That(loop.Closed, "closed");
        Check.Equal(4, loop.Points.Length);
        Check.That(loop.Points.Contains(new Vector2(20, 20)) && !loop.Points.Contains(new Vector2(30, 20)), "around cell 1 only");
    }

    [Test]
    public void AnUnusableCellSizeBuildsNothingToDrawButStillMatches()
    {
        var region = Build(new Terrain(2, 2, float.NaN, Vector2.Zero, false, new BitArray(4, true)), Trail(4, Vector2.Zero));

        Check.Equal(0, region.Pixels.Length);
        Check.Equal(0, region.Corners.Length);
        Check.That(region.Matches(1, region.Source), "matches, so it is not rebuilt every tick");
    }

    [Test]
    public void ItIsStaleWhenTheTrailGrowsThePlayerChangesCellOrAFillColourChanges()
    {
        var trail = Trail(4, new Vector2(5, 5));
        var region = Build(Row(5), trail, new Vector2(5, 5));

        Check.Equal<(int, int)?>((0, 0), region.PlayerCell);
        Check.That(!region.Stale(trail, (0, 0), Blue, White), "nothing changed");
        Check.That(!region.Stale(Trail(4, new Vector2(5, 5)), (0, 0), Blue, White), "a new instance with the same points did not grow");
        Check.That(region.Stale(Trail(4, new Vector2(5, 5), new Vector2(9, 5)), (0, 0), Blue, White), "grew");
        Check.That(region.Stale(trail, (1, 0), Blue, White), "player moved a cell");
        Check.That(region.Stale(trail, (0, 0), Color.Red, White), "region fill");
        Check.That(region.Stale(trail, (0, 0), Blue, Color.Red), "explored fill");
        Check.That(region.Stale(null, (0, 0), Blue, White), "the trail went away");
    }

    [Test]
    public void WithoutATrailThePlayerMovingIsNoReasonToRebuild() =>
        Check.That(!Build(Row(5), null, new Vector2(5, 5)).Stale(null, (3, 0), Blue, White), "everything is already explored");

    [Test]
    public void AnUnexploredHoleInsideTheExploredCellsGetsItsOwnInnerLoop()
    {
        var terrain = Scene.Cells(3, 3, false, [.. Enumerable.Range(0, 9).Select(i => (i % 3, i / 3))]);
        var ring = Enumerable.Range(0, 9).Where(i => i != 4).Select(i => new Vector2(i % 3 * 10 + 5, i / 3 * 10 + 5)).ToArray();
        var region = Build(terrain, Trail(4, ring));

        Check.That(!region.Explored[4], "the centre cell is not explored");
        Check.Equal(2, region.ExploredOutline.Length);
        Check.That(region.ExploredOutline.All(line => line.Closed), "both closed");
        Check.That(region.ExploredOutline.Any(line => line.Points.Length == 4 && line.Points.Contains(new Vector2(10, 10))), "inner loop around the hole");
    }
}
