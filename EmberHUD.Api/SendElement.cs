namespace EmberHUD;

public sealed record ElementOptions
{
    public MouseMode? Mode { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;
    public HumanizerConfig? Config { get; init; }
    public CancellationToken Cancel { get; init; }
    public int Margin { get; init; } = 4;
}

public static partial class Send
{
    /// <summary>Moves to a centre-weighted point inside the rect. True only if the move went out, false for a null or degenerate rect.</summary>
    public static Task<bool> MoveToElement(ScreenRect? rect, ElementOptions? options = null)
    {
        options ??= new ElementOptions();
        return PickPoint(rect, options.Margin, Rng) is var (x, y)
            ? MoveTo(x, y, options.Mode, options.Cancel, options.Config)
            : Task.FromResult(false);
    }

    /// <summary>Moves to a centre-weighted point inside the rect and clicks. True only if the button went down, false for a null or degenerate rect.</summary>
    public static Task<bool> ClickElement(ScreenRect? rect, ElementOptions? options = null)
    {
        options ??= new ElementOptions();
        return PickPoint(rect, options.Margin, Rng) is var (x, y)
            ? ClickAt(x, y, options.Button, options.Mode, options.Cancel, options.Config)
            : Task.FromResult(false);
    }

    internal static (int X, int Y)? PickPoint(ScreenRect? rect, int margin, Random rng)
    {
        if (rect is not { } r) return null;
        var (minX, minY, maxX, maxY) = (r.Min.X, r.Min.Y, r.Max.X, r.Max.Y);
        if (!float.IsFinite(minX + minY + maxX + maxY) || maxX <= minX || maxY <= minY) return null;
        var inset = Math.Max(margin, 0);
        return ((int)Math.Round(PickAxis(minX, maxX, inset, rng)), (int)Math.Round(PickAxis(minY, maxY, inset, rng)));
    }

    static double PickAxis(double min, double max, int inset, Random rng)
    {
        var (low, high) = (min + inset, max - inset);
        return high <= low ? (min + max) / 2 : low + (rng.NextDouble() + rng.NextDouble()) / 2 * (high - low);
    }
}
