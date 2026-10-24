namespace EmberHUD.Host.Icons;

internal sealed class GameIconSets
{
    sealed class Slot(string folder, string keyPrefix)
    {
        public string Folder { get; } = folder;
        public string KeyPrefix { get; } = keyPrefix;
        public IconStore? Store { get; set; }
        public DateTime Stamp { get; set; }
    }

    static readonly (AssetKind Kind, string Folder, string KeyPrefix)[] Layout =
    [
        (AssetKind.Minimap, "", ""),
        (AssetKind.Items, "Items", "items/"),
        (AssetKind.Skills, "Skills", "skills/"),
        (AssetKind.Buffs, "Buffs", "buffs/"),
    ];

    readonly Slot[] _slots;
    readonly TextureUpload _upload;
    readonly Func<string, Sprite?> _resolve;
    readonly bool _inline;

    public event Action? Changed;

    public IReadOnlyList<string> MinimapNames => Names(AssetKind.Minimap);

    public IReadOnlyList<string> Names(AssetKind kind) => For(kind).Store?.Names ?? [];

    public GameIconSets(string directory, TextureUpload upload, Func<string, Sprite?> resolve, bool inline = false)
    {
        _slots = [.. Layout.Select(l => new Slot(Path.Combine(directory, l.Folder), l.KeyPrefix))];
        _upload = upload;
        _resolve = resolve;
        _inline = inline;
        Scan();
    }

    public bool Probe(AssetKind kind) => For(kind).Store is not null;

    public Sprite? Resolve(AssetKind kind, ReadOnlySpan<char> name)
    {
        var store = For(kind).Store;
        if (kind == AssetKind.Items && store?.Has(name) != true) store = For(AssetKind.Skills).Store;
        return store?.Resolve(name);
    }

    public void Pump()
    {
        foreach (var slot in _slots) slot.Store?.Pump();
    }

    public void Rescan()
    {
        if (Scan()) Changed?.Invoke();
    }

    Slot For(AssetKind kind) => _slots[(int)kind];

    bool Scan()
    {
        var changed = false;
        foreach (var slot in _slots)
        {
            var index = Path.Combine(slot.Folder, "index.json");
            if (!File.Exists(index)) continue;
            var stamp = File.GetLastWriteTimeUtc(index);
            if (stamp == slot.Stamp) continue;
            slot.Stamp = stamp;
            var prefix = slot.KeyPrefix;
            var store = IconStore.Open(slot.Folder, (string key, byte[] rgba, int width, int height, out string failure) => _upload(prefix + key, rgba, width, height, out failure), key => _resolve(prefix + key), _inline);
            if (store is null) continue;
            slot.Store = store;
            changed = true;
        }
        return changed;
    }
}
