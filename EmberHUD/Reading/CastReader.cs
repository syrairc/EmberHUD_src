using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class CastReader(IReadBackend memory, nint moduleBase)
{
    public Entity[] Apply(Entity[] entities)
    {
        Entity[]? result = null;
        nint world = 0;
        for (var i = 0; i < entities.Length; i++)
        {
            if (entities[i].Kind != EntityKind.Monster) continue;
            if (world == 0 && !TryReadWorld(out world)) return entities;
            if (!TryRead(world, entities[i].Id, out var cast)) continue;
            result ??= [.. entities];
            result[i] = entities[i] with { Cast = cast };
        }

        return result ?? entities;
    }

    private bool TryReadWorld(out nint world)
    {
        world = 0;
        return memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr) &&
               memory.TryReadPtr(fightMgr + Offsets.FightWorldHop1, out var holder) &&
               memory.TryReadPtr(holder + Offsets.FightWorldHop2, out var inner) &&
               memory.TryReadPtr(inner + Offsets.FightWorldHop3, out world);
    }

    private bool TryRead(nint world, int id, out Cast cast)
    {
        cast = default;
        if (!memory.TryFindHashValue(world + Offsets.FightWorldEntityBucketCount, world + Offsets.FightWorldEntityBuckets, unchecked((uint)id), out var fighter) ||
            !TryReadComponent(fighter, out var component))
            return false;

        var found = default(Cast);
        var any = false;
        memory.TryWalkBuckets(component + Offsets.SkillActiveBucketCount, component + Offsets.SkillActiveBuckets, Offsets.MaxBuckets, Offsets.NodeSize, Offsets.NodeNext, Offsets.MaxEntities,
            (_, node) =>
            {
                var skill = (nint)BitConverter.ToInt64(node[Offsets.NodeEntity..]);
                if (skill == 0 || !TryReadSkill(skill, id, out found)) return true;
                any = true;
                return false;
            });
        cast = found;
        return any;
    }

    private bool TryReadComponent(nint fighter, out nint component)
    {
        if (TryFromTable(fighter + Offsets.FightEntityDirectTable, out var direct) && direct != 0)
        {
            component = direct;
            return true;
        }

        component = 0;
        return TryFromTable(fighter + Offsets.FightEntityEntryTable, out var entry) && entry != 0 &&
               memory.TryReadPtr(entry + Offsets.FightEntryComponent, out component);
    }

    private bool TryFromTable(nint table, out nint slot)
    {
        slot = 0;
        return memory.TryRead(table + Offsets.FightTableSlotCap, out int slotCap) &&
               memory.TryReadPtr(table + Offsets.FightTableIndex, out var index) &&
               memory.TryRead(index + Offsets.SkillComponentId, out byte at) && at < slotCap &&
               memory.TryReadPtr(table + Offsets.FightTableSlots, out var slots) &&
               memory.TryRead(slots + at * nint.Size, out slot);
    }

    private bool TryReadSkill(nint skill, int caster, out Cast cast)
    {
        cast = default;
        Span<double> pair = stackalloc double[2];
        if (!memory.TryRead(skill + Offsets.SkillCaster, out int owner) || owner != caster ||
            !memory.TryRead(skill + Offsets.SkillState, out byte state) || state > Offsets.MaxCastState ||
            !memory.TryRead(skill + Offsets.SkillStoneCfg, out int stone) ||
            !memory.TryReadPtr(skill + Offsets.SkillTimer, out var timer) || !memory.TryReadPtr(skill + Offsets.SkillTiming, out var timing) ||
            !memory.TryRead(timer + Offsets.TimerPrecastRemaining, out double remaining) || !memory.TryRead(timer + Offsets.TimerSpeed, out double speed) ||
            !memory.TryRead(timing + Offsets.TimingPrecastTotal, out double configured) ||
            !double.IsFinite(remaining) || remaining < 0 || remaining > Offsets.MaxCastSeconds ||
            !double.IsFinite(speed) || speed <= 0 || speed > Offsets.MaxCastSpeed ||
            !double.IsFinite(configured) || configured <= 0 || configured > Offsets.MaxCastSeconds)
            return false;

        var total = configured / speed;
        var left = state == 0 ? total : Math.Min(remaining, total);
        if (!memory.TryRead(skill + Offsets.SkillTarget, pair) || !IsFinite(pair, out var target) ||
            !memory.TryRead(skill + Offsets.SkillDirection, pair) || !IsFinite(pair, out var direction))
            return false;

        cast = new Cast(stone, state, (float)(1 - left / total), (float)left, target, direction);
        return true;
    }

    private static bool IsFinite(ReadOnlySpan<double> pair, out Vector2 value)
    {
        value = new Vector2((float)pair[0], (float)pair[1]);
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
