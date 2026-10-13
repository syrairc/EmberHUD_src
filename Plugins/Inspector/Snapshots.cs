using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD;

namespace Inspector;

internal sealed class Snapshots(Func<long> clock)
{
    sealed class Entry
    {
        public byte[]? Bytes;
        public long? ReadAt;
        public long UsedAt;
    }

    const int MaxLength = 0x10000;

    readonly Dictionary<(nint, int), Entry> _entries = [];

    public bool Frozen;
    public int RefreshRate = 10;

    public byte[]? Get(nint address, int length)
    {
        if (length <= 0 || length > MaxLength) return null;
        var now = clock();
        if (!_entries.TryGetValue((address, length), out var entry)) _entries[(address, length)] = entry = new Entry();
        entry.UsedAt = now;
        if (entry.ReadAt is null || !Frozen && now - entry.ReadAt >= 1000 / Math.Max(1, RefreshRate))
        {
            var bytes = new byte[length];
            entry.Bytes = Memory.Read(address, bytes) == length ? bytes : null;
            entry.ReadAt = now;
        }
        return entry.Bytes;
    }

    public void Prune()
    {
        if (Frozen) return;
        var now = clock();
        foreach (var key in _entries.Where(pair => now - pair.Value.UsedAt > 1000).Select(pair => pair.Key).ToArray()) _entries.Remove(key);
    }

    public void Clear() => _entries.Clear();
}
