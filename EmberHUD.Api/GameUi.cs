using System.Numerics;

namespace EmberHUD;

public enum PanelKind : byte { Other, Inventory, Stash, WorldMap, Skills, Talents, Vendor, Settings }

public readonly record struct GamePanel(string Name, PanelKind Kind, Vector2 Min, Vector2 Max, bool FullScreen)
{
    public nint Address { get; init; }

    public bool Contains(Vector2 point) => point.X >= Min.X && point.X < Max.X && point.Y >= Min.Y && point.Y < Max.Y;
}

public sealed record GameUi(GamePanel[] Panels)
{
    internal static volatile Func<nint, string, (Vector2 Min, Vector2 Max)?>? Finder;

    public static readonly GameUi Empty = new([]);

    public bool AnyFullScreen => Panels.Any(p => p.FullScreen);

    public GamePanel? Get(PanelKind kind)
    {
        for (var i = Panels.Length - 1; i >= 0; i--)
            if (Panels[i].Kind == kind) return Panels[i];
        return null;
    }

    public bool Covers(Vector2 point) => Panels.Any(p => p.Contains(point));

    public static (Vector2 Min, Vector2 Max)? Find(GamePanel panel, string childName) =>
        panel.Address != 0 && Finder is { } finder ? finder(panel.Address, childName) : null;
}
