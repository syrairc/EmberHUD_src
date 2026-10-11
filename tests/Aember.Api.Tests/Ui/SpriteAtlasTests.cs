using System.Numerics;
using Aember.Ui;

namespace Aember.Api.Tests;

public class SpriteAtlasTests
{
    enum Shape { A, B, C }

    static readonly GridAtlas Atlas = new(1024, 768, 128, 8);

    [Fact]
    public void CellsCountRowMajor() => Assert.Equal((128, 128), Atlas.Cell0(9));

    [Fact]
    public void CellsAreNotSymmetricInXAndY() => Assert.Equal((256, 128), Atlas.Cell0(10));

    [Fact]
    public void TheFirstCellStartsAtTheOrigin()
    {
        var (uv0, uv1) = Atlas.UVPair(0);
        Assert.Equal(Vector2.Zero, uv0);
        Assert.Equal(128f / 1024f, uv1.X, 1e-6f);
        Assert.Equal(128f / 768f, uv1.Y, 1e-6f);
    }

    [Fact]
    public void FlippedVSwapsV()
    {
        var flip = Atlas.UVPairFlippedV(0);
        Assert.Equal(0f, flip.Uv0.X);
        Assert.Equal(128f / 768f, flip.Uv0.Y, 1e-6f);
        Assert.Equal(128f / 1024f, flip.Uv1.X, 1e-6f);
        Assert.Equal(0f, flip.Uv1.Y);
    }

    [Fact]
    public void ParseReadsAnEnumName() => Assert.Equal(1, GridAtlas.Parse("b", Shape.A));

    [Fact]
    public void ParseFallsBackOnAnUnknownName() => Assert.Equal(2, GridAtlas.Parse("nope", Shape.C));
}
