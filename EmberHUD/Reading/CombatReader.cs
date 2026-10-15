using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class CombatReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly record struct HitKey(nint Hit, long Bits, int Attacker, int Event);

    private static readonly int HeaderSize = Offsets.HurtListCount + sizeof(int) - Offsets.HurtInlineFlag;

    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly List<(int Key, nint Value)> _pairs = [];
    private readonly List<(int Victim, nint Container)> _containers = [];
    private readonly Dictionary<int, HashSet<HitKey>> _seen = [];
    private readonly byte[] _header = new byte[HeaderSize];
    private readonly byte[] _hit = new byte[Offsets.HitSize];
    private nint _fightMgr;
    private int _localPlayerId;
    private long _nextRefresh;
    private double _effective;
    private double _raw;
    private long _hits;
    private volatile CombatStats? _snapshot;

    public CombatStats? Snapshot => _snapshot;

    public void Poll()
    {
        var now = _clock();
        if (!memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr)) fightMgr = 0;
        if (fightMgr != _fightMgr)
        {
            Reset(fightMgr);
            _nextRefresh = 0;
        }

        if (fightMgr == 0)
        {
            _snapshot = null;
            return;
        }

        if (now >= _nextRefresh)
        {
            Refresh();
            _nextRefresh = now + Stopwatch.Frequency * Offsets.CombatRefreshMilliseconds / 1000;
        }

        if (_localPlayerId == 0)
        {
            _snapshot = null;
            return;
        }

        foreach (var (victim, container) in _containers)
            Scan(victim, container);
        _snapshot = new CombatStats(now, _effective, _raw, _hits);
    }

    private void Refresh()
    {
        _containers.Clear();
        _localPlayerId = 0;
        if (!_world.TryReadLocalPlayerId(out _localPlayerId)) return;
        _pairs.Clear();
        memory.ReadTMapPointers(_fightMgr + Offsets.FightMapMonster, _pairs);
        foreach (var (key, value) in _pairs)
            if (value != 0 && memory.TryReadPtr(value + Offsets.UObjectHurtContainer, out var container) && container != 0)
                _containers.Add((key, container));
        var live = _containers.Select(c => c.Victim).ToHashSet();
        foreach (var stale in _seen.Keys.Where(k => !live.Contains(k)).ToArray())
            _seen.Remove(stale);
    }

    private void Reset(nint fightMgr)
    {
        _fightMgr = fightMgr;
        _effective = _raw = 0;
        _hits = 0;
        _seen.Clear();
        _containers.Clear();
        _localPlayerId = 0;
        _snapshot = null;
    }

    private void Scan(int victim, nint container)
    {
        if (!memory.TryRead(container + Offsets.HurtInlineFlag, _header) || _header[0] != 0) return;
        var list = BitConverter.ToInt64(_header, Offsets.HurtListPointer - Offsets.HurtInlineFlag);
        var count = BitConverter.ToInt32(_header, Offsets.HurtListCount - Offsets.HurtInlineFlag);
        if (count <= 0)
        {
            _seen.Remove(victim);
            return;
        }

        if (list == 0 || count > Offsets.MaxHurtList || !memory.TryReadArray((nint)list, count, out long[] hits)) return;
        if (!_seen.TryGetValue(victim, out var known)) _seen[victim] = known = [];
        foreach (var pointer in hits)
        {
            if (pointer == 0 || !memory.TryRead((nint)pointer, _hit)) continue;
            var attacker = BitConverter.ToInt32(_hit, Offsets.HitAttacker);
            var bits = BitConverter.ToInt64(_hit, Offsets.HitEffective);
            var eventId = BitConverter.ToInt32(_hit, Offsets.HitEvent);
            if (!known.Add(new HitKey((nint)pointer, bits, attacker, eventId)) || attacker != _localPlayerId) continue;
            var effective = BitConverter.Int64BitsToDouble(bits);
            if (!double.IsFinite(effective) || effective < 0) continue;
            var raw = BitConverter.ToDouble(_hit, Offsets.HitRaw);
            _effective += effective;
            _raw += double.IsFinite(raw) && raw >= effective ? raw : effective;
            _hits++;
        }
    }
}
