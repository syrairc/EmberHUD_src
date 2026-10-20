namespace EmberHUD;

public readonly record struct ItemInfo(int BaseId, int Rarity, int Type1, int Type2, int Type3, int Type4, int Tier, int RequireLevel, bool IsUnique);

public static class Items
{
    internal static Func<int, ItemInfo?>? BaseGetter { get; set; }
    internal static Func<int, ItemInfo?>? UniqueGetter { get; set; }

    public static bool TryGet(int baseId, out ItemInfo info) => Take(BaseGetter, baseId, out info);

    public static bool FromUnique(int uniqueId, out ItemInfo info) => Take(UniqueGetter, uniqueId, out info);

    public static bool IsUnique(int uniqueId) => UniqueGetter?.Invoke(uniqueId) is not null;

    static bool Take(Func<int, ItemInfo?>? getter, int id, out ItemInfo info)
    {
        var found = getter?.Invoke(id);
        info = found ?? default;
        return found is not null;
    }
}
