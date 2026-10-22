using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class CharacterReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null, CfgIconCache? skillIcons = null, CfgIconCache? buffIcons = null)
{
    private readonly CfgIconCache _skillIcons = skillIcons ?? new();
    private readonly CfgIconCache _buffIcons = buffIcons ?? new();
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LogicWorldReader _world = new(memory, moduleBase);
    private readonly LocText _loc = new(memory, moduleBase);
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private long _nextSkillRefresh;
    private Buff[]? _buffs;
    private Skill[]? _skills;

    public Buff[]? ReadBuffs()
    {
        var now = _clock();
        if (now < _nextRefresh) return _buffs;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.CharacterRefreshMilliseconds / 1000;
        return _buffs = BuildBuffs();
    }

    public Skill[]? ReadSkills(Func<int[]?> readSkillBar)
    {
        var now = _clock();
        if (now < _nextSkillRefresh) return _skills;
        _nextSkillRefresh = now + Stopwatch.Frequency * Offsets.CharacterRefreshMilliseconds / 1000;
        return _skills = BuildSkills(readSkillBar());
    }

    private Skill[]? BuildSkills(int[]? bar)
    {
        if (bar is null || !_world.TryReadLocalPlayerId(out var playerId) || !TryFightWorld(out var world) ||
            !TryEntity(world, playerId, out var player) || !TryComponent(player, Offsets.ComponentSkill, out var component))
            return null;

        var skills = new List<Skill>();
        var slots = _lua.TryOpen(out var globals) && _lua.TryGetTable(globals, Offsets.LuaSkillManager, out var manager) && _lua.TryGetTable(manager, Offsets.LuaSkillSlots, out var tabs)
            ? tabs
            : (LuaTable?)null;
        var intact = true;
        var walked = memory.TryWalkBuckets(component + Offsets.CooldownMapCount, component + Offsets.CooldownMap, Offsets.MaxBuckets, Offsets.CooldownNodeSize, Offsets.CooldownNodeNext, Offsets.MaxSkills, (_, node) =>
        {
            var remaining = BitConverter.ToDouble(node[Offsets.CooldownRemaining..]);
            var total = BitConverter.ToDouble(node[Offsets.CooldownTotal..]);
            if (!double.IsFinite(remaining) || !double.IsFinite(total) || !TryCfg(world, (int)BitConverter.ToUInt32(node[Offsets.CooldownSkill..]), out var cfgId, out var icon))
                return intact = false;

            _skillIcons.Add(cfgId, icon);
            var maxCharges = BitConverter.ToInt32(node[Offsets.CooldownMaxCharges..]);
            var hasCharges = maxCharges > 1;
            var slot = cfgId == 0 ? -1 : Array.IndexOf(bar, cfgId);
            skills.Add(new Skill(cfgId, slot, remaining, total, hasCharges ? BitConverter.ToInt32(node[Offsets.CooldownCharges..]) : 0, hasCharges ? maxCharges : 0, icon) { Name = slot >= 0 && slots is { } tabList ? BarSkillName(tabList, slot) : null });
            return true;
        });

        return walked && intact ? [.. skills] : null;
    }

    // bar slot n is skill tab n + 1, its first slot holds the skill item
    private string? BarSkillName(LuaTable tabs, int slot) =>
        _lua.TryGetTableAt(tabs, slot + 1, out var tab) && _lua.TryGetTableAt(tab, 1, out var first) &&
        _lua.TryGetTable(first, Offsets.LuaSkillData, out var data) && _lua.TryGetTable(data, Offsets.LuaItemRecord, out var record) &&
        _lua.TryGetInt(record, Offsets.LuaItemBaseId, out var baseId)
            ? _loc.Get(Offsets.ItemNameKey + baseId)
            : null;

    private bool TryCfg(nint world, int skillEntityId, out int cfgId, out string? icon)
    {
        cfgId = 0;
        icon = null;
        if (!TryEntity(world, skillEntityId, out var entity) || !TryComponent(entity, Offsets.ComponentSkillCfg, out var component) ||
            !memory.TryReadPtr(component + Offsets.SkillCfgPointer, out var cfg) || !memory.TryRead(cfg, out cfgId))
            return false;
        icon = _skillIcons.Get(cfgId) ?? memory.ReadIconName(cfg + Offsets.SkillCfgRowIcon);
        return true;
    }

    private Buff[]? BuildBuffs()
    {
        if (!TryFightEntity(out var entity) || !TryComponent(entity, Offsets.ComponentBuff, out var component))
            return null;

        var buffs = new List<Buff>();
        var walked = memory.TryWalkBuckets(component + Offsets.BuffMapCount, component + Offsets.BuffMap, Offsets.MaxBuckets, Offsets.NodeNext + sizeof(long), Offsets.NodeNext, Offsets.MaxBuffs, (_, node) =>
        {
            var instance = (nint)BitConverter.ToInt64(node[Offsets.FightNodeValue..]);
            if (instance != 0 && TryReadBuff(instance, out var buff)) buffs.Add(buff);
            return true;
        });

        return walked ? [.. buffs] : null;
    }

    private bool TryReadBuff(nint instance, out Buff buff)
    {
        buff = default;
        if (!memory.TryRead(instance + Offsets.BuffId, out int id) || id <= 0 ||
            !memory.TryRead(instance + Offsets.BuffTotal, out double total) || !double.IsFinite(total) ||
            !memory.TryRead(instance + Offsets.BuffRemaining, out double remaining) || !double.IsFinite(remaining) ||
            !memory.TryReadPtr(instance + Offsets.BuffCfgRow, out var row) ||
            !memory.TryRead(row + Offsets.BuffCfgId, out int rowId) || rowId != id)
            return false;

        var icon = _buffIcons.Get(id) ?? memory.ReadIconName(row + Offsets.BuffCfgRowIcon);
        _buffIcons.Add(id, icon);
        buff = new Buff(id, _loc.Get(Offsets.BuffNameKey + id + Offsets.BuffNameSuffix), remaining, total, icon, ReadStacks(instance));
        return true;
    }

    private int ReadStacks(nint instance) =>
        memory.TryRead(instance + Offsets.BuffStackable, out byte stackable) && stackable != 0 &&
        memory.TryRead(instance + Offsets.BuffStacks, out int stacks) && stacks > 0
            ? stacks
            : 1;

    private bool TryFightEntity(out nint entity)
    {
        entity = 0;
        return _world.TryReadLocalPlayerId(out var id) && TryFightWorld(out var world) && TryEntity(world, id, out entity);
    }

    private bool TryFightWorld(out nint world)
    {
        world = 0;
        return memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr) &&
               memory.TryReadPtr(fightMgr + Offsets.FightWorldFirst, out var first) &&
               memory.TryReadPtr(first + Offsets.FightWorldSecond, out var second) &&
               memory.TryReadPtr(second + Offsets.FightWorldThird, out world);
    }

    private bool TryEntity(nint world, int id, out nint entity)
    {
        entity = 0;
        return memory.TryFindHashNode(world + Offsets.FightWorldMapCount, world + Offsets.FightWorldMap, Offsets.MaxBuckets, (ulong)id, Offsets.NodeNext,
                   node => IsFightNode(node, id), out var found) &&
               memory.TryReadPtr(found + Offsets.FightNodeValue, out entity);
    }

    private bool IsFightNode(nint node, int id) => memory.TryRead(node + Offsets.FightNodeKey, out long key) && key == id;

    private bool TryComponent(nint entity, int componentId, out nint component)
    {
        component = 0;
        return memory.TryReadPtr(entity + Offsets.FightEntityComponentIndex, out var index) &&
               memory.TryReadPtr(entity + Offsets.FightEntityComponentSlots, out var slots) &&
               memory.TryRead(index + componentId, out byte slot) && slot != 0 &&
               memory.TryReadPtr(slots + slot * nint.Size, out component);
    }
}
