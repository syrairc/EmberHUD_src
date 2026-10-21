namespace EmberHUD;

public readonly record struct BuffInfo(int Id, string? Icon);

public static class Buffs
{
    internal static Func<int, BuffInfo?>? Getter { get; set; }

    public static bool TryGet(int id, out BuffInfo info)
    {
        var found = Getter?.Invoke(id);
        info = found ?? default;
        return found is not null;
    }
}
