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

    private bool TryFromTable(nint table, int componentId, out nint slot)
    {
        slot = 0;
        return memory.TryRead(table + Offsets.FightTableSlotCap, out int slotCap) &&
               memory.TryReadPtr(table + Offsets.FightTableIndex, out var index) &&
               memory.TryRead(index + componentId, out byte at) && at < slotCap &&
               memory.TryReadPtr(table + Offsets.FightTableSlots, out var slots) &&
               memory.TryRead(slots + at * nint.Size, out slot);
    }
}
