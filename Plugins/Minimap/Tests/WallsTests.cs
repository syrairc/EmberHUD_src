using System.Linq;
using System.Numerics;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class WallsTests
{
    [Test]
    public void ALoneCellProjectsItsFourEdgesIntoMapSpaceWithTheirBounds()
    {
        var walls = Walls.Build(1, Scene.Cells(3, 3, false, (1, 1)));

        Check.Equal(8, walls.Lines.Length);
        Check.That(walls.Lines.Contains(new Vector2(10, 10)));
        Check.That(walls.Lines.Contains(new Vector2(20, 20)));
        Check.Equal(new Vector2(10, 10), walls.Min);
        Check.Equal(new Vector2(20, 20), walls.Max);
    }

    [Test]
    public void NoTerrainBuildsNothing()
    {
        var walls = Walls.Build(3, null);

        Check.Equal(3, walls.AreaId);
        Check.Equal(0, walls.Lines.Length);
        Check.Equal(Vector2.Zero, walls.Min);
        Check.Equal(Vector2.Zero, walls.Max);
    }

    [Test]
    public void ATerrainWithNoUsableCellSizeBuildsNothing()
    {
        var bits = new System.Collections.BitArray(1);
        bits[0] = true;

        var walls = Walls.Build(1, new Terrain(1, 1, float.NaN, Vector2.Zero, false, bits));

        Check.Equal(0, walls.Lines.Length);
    }

    [Test]
    public void MatchesNeedsTheSameAreaAndTheSameTerrainInstance()
    {
        var terrain = Scene.Cells(2, 2, false, (0, 0));
        var walls = Walls.Build(5, terrain);

        Check.That(walls.Matches(5, terrain));
        Check.That(!walls.Matches(6, terrain));
        Check.That(!walls.Matches(5, Scene.Cells(2, 2, false, (0, 0))));
        Check.That(!Walls.Empty.Matches(0, null));
    }
}
