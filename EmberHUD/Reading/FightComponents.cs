using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class FightComponents(IReadBackend memory, nint moduleBase)
{
    public bool TryReadWorld(out nint world)
    {
        world = 0;
        return memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr) &&
               memory.TryReadPtr(fightMgr + Offsets.FightWorldHop1, out var holder) &&
               memory.TryReadPtr(holder + Offsets.FightWorldHop2, out var inner) &&
               memory.TryReadPtr(inner + Offsets.FightWorldHop3, out world);
    }

    public bool TryFindFighter(nint world, int id, out nint fighter) =>
        memory.TryFindHashValue(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, unchecked((uint)id), out fighter);

    public bool IsHostile(nint world, int id) =>
        TryFindFighter(world, id, out var fighter) &&
        TryReadComponent(fighter, Offsets.SideComponentId, out var side) && side != 0 &&
        memory.TryRead(side + Offsets.SideCamp, out byte camp) && camp == Offsets.HostileCamp;

    public bool TryReadComponent(nint fighter, int componentId, out nint component)
    {
        if (TryFromTable(fighter + Offsets.FightEntityDirectTable, componentId, out var direct) && direct != 0)
        {
            component = direct;
            return true;
        }

        component = 0;
        return TryFromTable(fighter + Offsets.FightEntityEntryTable, componentId, out var entry) && entry != 0 &&
               memory.TryReadPtr(entry + Offsets.FightEntryComponent, out component);
    }

    public Dictionary<int, nint> ReadAll(nint fighter)
    {
        var found = new Dictionary<int, nint>();
        if (TryReadTable(fighter + Offsets.FightEntityDirectTable, out var directIndex, out var directSlots))
            for (var id = 0; id < directIndex.Length; id++)
                if (directIndex[id] < directSlots.Length && directSlots[directIndex[id]] != 0) found[id] = directSlots[directIndex[id]];

        if (TryReadTable(fighter + Offsets.FightEntityEntryTable, out var entryIndex, out var entrySlots))
            for (var id = 0; id < entryIndex.Length; id++)
                if (!found.ContainsKey(id) && entryIndex[id] < entrySlots.Length && entrySlots[entryIndex[id]] != 0 &&
                    memory.TryReadPtr(entrySlots[entryIndex[id]] + Offsets.FightEntryComponent, out var component)) found[id] = component;
        return found;
    }

    private bool TryReadTable(nint table, out byte[] index, out nint[] slots)
    {
        index = [];
        slots = [];
        if (!memory.TryRead(table + Offsets.FightTableSlotCap, out int slotCap) || slotCap < 0 || slotCap > Offsets.FightComponentCount + 8 ||
            !memory.TryReadPtr(table + Offsets.FightTableIndex, out var indexAt) || !memory.TryReadPtr(table + Offsets.FightTableSlots, out var slotsAt)) return false;

        var indexBytes = new byte[Offsets.FightComponentCount];
        var slotValues = new nint[slotCap + 1];
        if (!memory.TryRead(indexAt, indexBytes) || !memory.TryRead(slotsAt, slotValues.AsSpan())) return false;
        index = indexBytes;
        slots = slotValues;
        return true;
    }

    private bool TryFromTable(nint table, int componentId, out nint slot)
    {
        slot = 0;
        return memory.TryRead(table + Offsets.FightTableSlotCap, out int slotCap) &&
               memory.TryReadPtr(table + Offsets.FightTableIndex, out var index) &&
               memory.TryRead(index + componentId, out byte at) && at <= slotCap &&
               memory.TryReadPtr(table + Offsets.FightTableSlots, out var slots) &&
               memory.TryRead(slots + at * nint.Size, out slot);
    }
}
