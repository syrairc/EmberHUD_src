namespace EmberHUD;

public enum BuffKind { None = 0, Basic = 1, Ailment = 2, Control = 3, Curse = 4, Charge = 5 }

[Flags]
public enum CrowdControl
{
    None = 0,
    Stun = 1 << 0,
    Freeze = 1 << 1,
    Root = 1 << 2,
    Silence = 1 << 3,
    Fear = 1 << 4,
    Paralysis = 1 << 5,
    Stagger = 1 << 6,
    TimeStop = 1 << 7,
    CantMove = 1 << 8,
    CantCast = 1 << 9,
    Frozen = 1 << 10,
    Immune = 1 << 11,
}

public readonly record struct BuffInfo(int Id, string? Icon, string? Name = null, BuffKind Kind = BuffKind.None, bool Propensity = false, int MaxStacks = 0, double TickInterval = 0, double MaxDuration = 0, CrowdControl Control = CrowdControl.None);

public static class Buffs
{
    public const int AcupunctureRoot = 10000091;

    internal static Func<int, BuffInfo?>? Getter { get; set; }

    public static bool TryGet(int id, out BuffInfo info)
    {
        var found = Getter?.Invoke(id);
        info = found ?? default;
        return found is not null;
    }

    public static BuffInfo? Get(int id) => TryGet(id, out var info) ? info : null;

    internal static CrowdControl Union(IEnumerable<int> ids)
    {
        var all = CrowdControl.None;
        foreach (var id in ids)
            if (TryGet(id, out var info)) all |= info.Control;
        return all;
    }
}
