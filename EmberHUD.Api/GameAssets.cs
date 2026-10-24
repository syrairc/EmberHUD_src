namespace EmberHUD;

public enum AssetKind { Minimap, Items, Skills, Buffs }

public static class GameAssets
{
    const long MissMilliseconds = 3000;

    internal static Func<AssetKind, bool>? Probe { get; set; }

    internal static Func<AssetKind, string, Sprite?>? Resolver { get; set; }

    internal static Func<long> Clock { get; set; } = () => Environment.TickCount64;

    static readonly NameCache BaseNames = new(id => Items.TryGet(id, out var info) ? info.Icon : null);
    static readonly NameCache UniqueNames = new(id => Items.FromUnique(id, out var info) ? info.Icon : null);
    static readonly NameCache SkillNames = new(id => Skills.TryGet(id, out var info) ? info.Icon : null);
    static readonly NameCache BuffNames = new(id => Buffs.TryGet(id, out var info) ? info.Icon : null);

    public static bool IsExtracted(AssetKind kind) => Probe?.Invoke(kind) == true;

    internal static bool TryItem(int baseId, out Sprite sprite) => TrySprite(AssetKind.Items, BaseNames, baseId, out sprite);

    internal static bool TryUnique(int uniqueId, out Sprite sprite) => TrySprite(AssetKind.Items, UniqueNames, uniqueId, out sprite);

    internal static bool TrySkill(int cfgId, out Sprite sprite) => TrySprite(AssetKind.Skills, SkillNames, cfgId, out sprite);

    internal static bool TryBuff(int buffId, out Sprite sprite) => TrySprite(AssetKind.Buffs, BuffNames, buffId, out sprite);

    internal static bool TryNamed(AssetKind kind, string name, out Sprite sprite)
    {
        sprite = default;
        if (Resolver is not { } resolve || !IsExtracted(kind) || resolve(kind, name) is not { } found) return false;
        sprite = found;
        return true;
    }

    internal static void ResetNames()
    {
        BaseNames.Clear();
        UniqueNames.Clear();
        SkillNames.Clear();
        BuffNames.Clear();
    }

    static bool TrySprite(AssetKind kind, NameCache names, int id, out Sprite sprite)
    {
        sprite = default;
        if (id <= 0 || Resolver is not { } resolve || !IsExtracted(kind) || names.Get(id) is not { } name || resolve(kind, name) is not { } found) return false;
        sprite = found;
        return true;
    }

    sealed class NameCache(Func<int, string?> lookup)
    {
        readonly Dictionary<int, string> _hits = [];
        readonly Dictionary<int, long> _missUntil = [];

        readonly Lock _gate = new();

        // ponytail: lookup runs under the lock; host getters take their own gates and never call back here
        public string? Get(int id)
        {
            lock (_gate)
            {
                if (_hits.TryGetValue(id, out var cached)) return cached;
                var now = Clock();
                if (_missUntil.TryGetValue(id, out var until) && now < until) return null;
                var name = lookup(id);
                if (name is null)
                {
                    _missUntil[id] = now + MissMilliseconds;
                    return null;
                }
                _missUntil.Remove(id);
                return _hits[id] = name;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _hits.Clear();
                _missUntil.Clear();
            }
        }
    }
}
