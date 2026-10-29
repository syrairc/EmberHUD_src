using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal readonly record struct BuffTableRvas(int Buffs, int BuffCount, int States, int StateCount)
{
    public static BuffTableRvas FromOffsets() => new(Offsets.BuffCfgBuckets, Offsets.BuffCfgBucketCount, Offsets.CharStateCfgBuckets, Offsets.CharStateCfgBucketCount);
}

internal sealed class BuffCatalog(IReadBackend memory, nint moduleBase, BuffTableRvas? tables = null)
{
    private readonly BuffTableRvas _tables = tables ?? BuffTableRvas.FromOffsets();
    private readonly LocText _loc = new(memory, moduleBase);
    private readonly Dictionary<int, BuffInfo> _final = [];
    private readonly Dictionary<int, CrowdControl> _stateControl = [];
    private readonly Lock _gate = new();

    public BuffInfo? Get(int id)
    {
        lock (_gate)
            return _final.TryGetValue(id, out var cached) ? cached : TryFindRow(id, out var row) ? ReadRow(id, row) : null;
    }

    public BuffInfo? Read(int id, nint row)
    {
        lock (_gate)
            return _final.TryGetValue(id, out var cached) ? cached : ReadRow(id, row);
    }

    private bool TryFindRow(int id, out nint row)
    {
        row = 0;
        return id > 0 && _tables.Buffs != 0 && _tables.BuffCount != 0 &&
               memory.TryFindHashNode(moduleBase + _tables.BuffCount, moduleBase + _tables.Buffs, Offsets.MaxBuckets, (uint)id, Offsets.BuffCfgOuterNext,
                   node => memory.TryRead(node + Offsets.NodeKey, out uint key) && key == id, out var outer) &&
               memory.TryFindHashNode(outer + Offsets.BuffCfgInnerCount, outer + Offsets.BuffCfgInnerBuckets, Offsets.MaxBuckets, 0, Offsets.NodeNext,
                   node => memory.TryRead(node + Offsets.NodeKey, out uint mode) && mode == 0, out var inner) &&
               memory.TryReadPtr(inner + Offsets.NodeEntity, out row);
    }

    private BuffInfo? ReadRow(int id, nint row)
    {
        if (id <= 0 || !memory.TryRead(row + Offsets.BuffCfgId, out int rowId) || rowId != id ||
            !memory.TryRead(row + Offsets.BuffCfgRowType, out int type) ||
            !memory.TryRead(row + Offsets.BuffCfgRowPropensity, out byte propensity) ||
            !memory.TryRead(row + Offsets.BuffCfgRowMaxStacks, out int maxStacks) ||
            !memory.TryRead(row + Offsets.BuffCfgRowTick, out double tick) || !double.IsFinite(tick) ||
            !memory.TryRead(row + Offsets.BuffCfgRowMaxDuration, out double maxDuration) || !double.IsFinite(maxDuration))
            return null;

        var iconRead = memory.TryReadIconName(row + Offsets.BuffCfgRowIcon, out var icon);
        var name = ReadName(id, row);
        var control = ReadControl(id, row, out var resolved);
        var info = new BuffInfo(id, icon, name, type is >= 0 and <= (int)BuffKind.Charge ? (BuffKind)type : BuffKind.None, propensity != 0, maxStacks, tick, maxDuration, control);
        if (iconRead && name is not null && resolved) _final[id] = info;
        return info;
    }

    private string? ReadName(int id, nint row)
    {
        var rowKey = memory.TryReadConfigString(row + Offsets.BuffCfgRowName, out var key) ? _loc.Get(key) : null;
        return rowKey ?? _loc.Get(Offsets.BuffNameKey + id + Offsets.BuffNameSuffix);
    }

    private CrowdControl ReadControl(int id, nint row, out bool resolved)
    {
        var control = CrowdControlRules.ForBuff(id);
        resolved = TryReadInts(row + Offsets.BuffCfgRowAddedState, Offsets.MaxAddedStates, out var states);
        foreach (var state in states)
        {
            control |= CrowdControlRules.ForState(state);
            if (_tables.States == 0 || _tables.StateCount == 0) continue;
            if (TryReadStateControl(state, out var fromCategories)) control |= fromCategories;
            else resolved = false;
        }
        return control;
    }

    private bool TryReadStateControl(int state, out CrowdControl control)
    {
        if (_stateControl.TryGetValue(state, out control)) return true;
        control = CrowdControl.None;
        if (!memory.TryFindHashValue(moduleBase + _tables.StateCount, moduleBase + _tables.States, (uint)state, out var row) ||
            !TryReadInts(row + Offsets.CharStateRowCategory, Offsets.MaxStateCategories, out var categories))
            return false;

        foreach (var category in categories) control |= CrowdControlRules.ForCategory(category);
        _stateControl[state] = control;
        return true;
    }

    private bool TryReadInts(nint at, int max, out int[] values)
    {
        values = [];
        if (!memory.TryRead(at, out nint data) || !memory.TryRead(at + Offsets.TArrayNum, out int count) || count < 0 || count > max) return false;
        return count == 0 || (data != 0 && memory.TryReadArray(data, count, out values));
    }
}
