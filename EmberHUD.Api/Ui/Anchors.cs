using System.Numerics;

namespace EmberHUD.Ui;

public enum Anchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

public static class Anchors
{
    public static Vector2 Place(Vector2 min, Vector2 max, Vector2 size, Anchor anchor, float pad = 2f)
    {
        var x = anchor switch
        {
            Anchor.TopLeft or Anchor.Left or Anchor.BottomLeft => min.X + pad,
            Anchor.TopRight or Anchor.Right or Anchor.BottomRight => max.X - size.X - pad,
            _ => (min.X + max.X - size.X) / 2f,
        };
        var y = anchor switch
        {
            Anchor.TopLeft or Anchor.Top or Anchor.TopRight => min.Y + pad,
            Anchor.BottomLeft or Anchor.Bottom or Anchor.BottomRight => max.Y - size.Y - pad,
            _ => (min.Y + max.Y - size.Y) / 2f,
        };
        return new Vector2(x, y);
    }
}
