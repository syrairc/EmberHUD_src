using System.Numerics;

namespace EmberHUD.Ui;

public readonly record struct GridAtlas(int Width, int Height, int Cell, int Columns)
{
    public (int X, int Y) Cell0(int index) => ((index % Columns) * Cell, (index / Columns) * Cell);

    public (Vector2 Uv0, Vector2 Uv1) UVPair(int index)
    {
        var (x, y) = Cell0(index);
        return (new Vector2((float)x / Width, (float)y / Height), new Vector2((float)(x + Cell) / Width, (float)(y + Cell) / Height));
    }

    public (Vector2 Uv0, Vector2 Uv1) UVPairFlippedV(int index)
    {
        var (a, b) = UVPair(index);
        return (new Vector2(a.X, b.Y), new Vector2(b.X, a.Y));
    }

    public static int Parse<T>(string key, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(key, true, out var v) ? Convert.ToInt32(v) : Convert.ToInt32(fallback);
}
