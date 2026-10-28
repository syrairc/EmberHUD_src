using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class FightEntityReaderTests
{
    private static readonly (double X, double Y) Spot = (500, 200);

    private static (FakeWorld World, FightEntityReader Reader) Rig()
    {
        var world = new FakeWorld();
        return (world, new FightEntityReader(world.Memory, FakeWorld.Module));
    }

    private static FightEntity Only(FightEntityReader reader) => Assert.Single(reader.Near(Vector2.Zero, 1000, 10));

    private static int[] BlockComponents =>
    [
        Offsets.MoveComponentId, Offsets.MoveCmdComponentId, Offsets.BulletComponentId, Offsets.TrackComponentId, Offsets.SkillAttachComponentId, Offsets.LastedEffectComponentId,
        Offsets.ViewAssistComponentId, Offsets.NpcComponentId, Offsets.RoleComponentId, Offsets.ShapeComponentId, Offsets.ScaleComponentId, Offsets.SideComponentId,
        Offsets.AttachmentComponentId, Offsets.SuicideComponentId,
    ];

    private static Dictionary<int, nint> FullFighter(FakeWorld world, uint id = 10)
    {
        var components = world.AddFighter(id, Spot, BlockComponents);
        var memory = world.Memory;

        var move = components[Offsets.MoveComponentId];
        memory.Write(move + Offsets.MoveSpeed, 1200.0);
        memory.Write(move + Offsets.MoveTickDistance, 20.0);
        memory.WriteArray(move + Offsets.MoveDirection, new[] { 0.0, 1.0 });
        memory.WriteArray(move + Offsets.MoveLastStep, new[] { 3.0, 4.0 });
        memory.Write(move + Offsets.MoveBlocked, (byte)1);
        memory.Write(move + Offsets.MoveActive, (byte)1);
        memory.Write(move + Offsets.MoveCommandId, 9u);
        memory.Write(move + Offsets.MoveTravelled, 55.5);

        var moveCmd = components[Offsets.MoveCmdComponentId];
        var cmd = world.Alloc(0x100);
        memory.Write(moveCmd + Offsets.MoveCmdCommand, cmd);
        memory.Write(moveCmd + Offsets.MoveCmdSpeedProfile, (byte)2);
        memory.Write(cmd + Offsets.CmdType, (byte)5);
        memory.Write(cmd + Offsets.CmdId, 9u);
        memory.Write(cmd + Offsets.CmdElapsed, 1.5);

        var bullet = components[Offsets.BulletComponentId];
        memory.Write(bullet + Offsets.BulletMoveType, (byte)4);
        memory.Write(bullet + Offsets.BulletRange, 900.0);
        memory.WriteArray(bullet + Offsets.BulletOrigin, new[] { 1.0, 2.0, 3.0 });
        memory.WriteArray(bullet + Offsets.BulletLast, new[] { 4.0, 5.0, 6.0 });
        memory.WriteArray(bullet + Offsets.BulletEnd, new[] { 7.0, 8.0, 9.0 });
        memory.Write(bullet + Offsets.BulletTerrainStop, (byte)1);
        memory.Write(bullet + Offsets.BulletArmed, (byte)1);
        memory.Write(bullet + Offsets.BulletFinished, (byte)0);
        memory.Write(bullet + Offsets.BulletSource + Offsets.RefBound, (byte)1);
        memory.Write(bullet + Offsets.BulletSource + Offsets.RefId, 77u);

        var track = components[Offsets.TrackComponentId];
        memory.Write(track + Offsets.TrackTarget + Offsets.RefBound, (byte)1);
        memory.Write(track + Offsets.TrackTarget + Offsets.RefId, 88u);
        memory.Write(track + Offsets.TrackNeedsTarget, (byte)1);
        memory.Write(track + Offsets.TrackSurvives, (byte)0);
        memory.Write(track + Offsets.TrackRange, 640.0);

        var link = components[Offsets.SkillAttachComponentId];
        memory.Write(link + Offsets.SkillAttachSkill, 4242u);
        memory.Write(link + Offsets.SkillAttachHits, 2u);
        memory.Write(link + Offsets.SkillAttachMaxHits, 5u);
        memory.Write(link + Offsets.SkillAttachUnlimited, (byte)1);

        var timed = components[Offsets.LastedEffectComponentId];
        memory.Write(timed + Offsets.LastedElapsed, 1.25);
        memory.Write(timed + Offsets.LastedDuration, 6.0);

        var beam = components[Offsets.ViewAssistComponentId];
        memory.WriteArray(beam + Offsets.ViewAssistStart, new[] { 10.0, 20.0, 30.0 });
        memory.WriteArray(beam + Offsets.ViewAssistEnd, new[] { 40.0, 50.0, 60.0 });

        var npc = components[Offsets.NpcComponentId];
        memory.Write(npc + Offsets.NpcId, 1001u);
        memory.Write(npc + Offsets.NpcRarity, 2u);
        memory.Write(npc + Offsets.NpcSource, 3u);
        memory.Write(npc + Offsets.NpcCategory, 4u);

        var role = components[Offsets.RoleComponentId];
        memory.Write(role + Offsets.FightRoleConfig, 555u);
        memory.Write(role + Offsets.FightRoleRarity, 1u);
        memory.Write(role + Offsets.FightRoleLevel, 80);

        var shape = components[Offsets.ShapeComponentId];
        memory.Write(shape + Offsets.ShapeKind, 1u);
        memory.Write(shape + Offsets.ShapeRadius, 300u);
        memory.Write(shape + Offsets.ShapeExtentA, 11u);
        memory.Write(shape + Offsets.ShapeExtentB, 12u);

        memory.Write(components[Offsets.ScaleComponentId] + Offsets.ScaleValue, 2.5);
        memory.Write(components[Offsets.SideComponentId] + Offsets.SideCamp, (byte)2);
        memory.Write(components[Offsets.AttachmentComponentId] + Offsets.AttachmentOwnerId, 7504u);
        memory.Write(components[Offsets.SuicideComponentId] + Offsets.SuicideLife, 12.5);
        return components;
    }

    [Fact]
    public void AnUnlimitedLinkKeepsAHugeMaxHits()
    {
        var (world, reader) = Rig();
        var components = world.AddFighter(11, Spot, Offsets.SkillAttachComponentId);
        var link = components[Offsets.SkillAttachComponentId];
        world.Memory.Write(link + Offsets.SkillAttachSkill, 4242u);
        world.Memory.Write(link + Offsets.SkillAttachMaxHits, uint.MaxValue);
        world.Memory.Write(link + Offsets.SkillAttachUnlimited, (byte)1);
        Assert.Equal(new FightLink(4242, 0, int.MaxValue, true), Only(reader).Link);
    }

    [Fact]
    public void ReadsShapeScaleCampAndComponents()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Spot, 300, 2, scale: 2.5);
        var entity = Only(reader);
        Assert.Equal(3652, entity.Id);
        Assert.Equal(new Vector3(500, 200, 0), entity.Position);
        Assert.Equal(new FightShape(1, 300, 0, 0), entity.Shape);
        Assert.Equal(2.5, entity.Scale);
        Assert.Equal(2, entity.Camp);
        Assert.Contains(Offsets.BuffAreaComponentId, entity.Components);
        Assert.Contains(Offsets.ShapeComponentId, entity.Components);
        Assert.Equal(entity.Components.Order(), entity.Components);
        Assert.Equal(FightArchetype.BuffArea, entity.Archetype);
    }

    [Fact]
    public void ReadsTheBuffsOnAFighter()
    {
        var (world, reader) = Rig();
        world.AddBuffed(178, Spot, world.AddBuff(82151, 3.5, 4, stacks: 2), world.AddBuff(10, 1, 1));
        var buffs = Only(reader).Buffs.OrderBy(buff => buff.Id).ToArray();
        Assert.Equal([10, 82151], buffs.Select(buff => buff.Id));
        Assert.Equal([1, 2], buffs.Select(buff => buff.Stacks));
        Assert.Equal(3.5, buffs[1].Remaining);
    }

    [Fact]
    public void AFighterWithoutBuffsHasNone()
    {
        var (world, reader) = Rig();
        world.AddArea(1, Spot, 300, 1);
        Assert.Empty(Only(reader).Buffs);
    }

    [Fact]
    public void ReadsTheOwnerOfARock()
    {
        var (world, reader) = Rig();
        world.AddRock(9031, 7504, Spot, 175);
        var rock = Assert.Single(reader.Near(Vector2.Zero, 1000, 10), entity => entity.Id == 9031);
        Assert.Equal(7504, rock.OwnerId);
        Assert.Null(rock.Camp);
    }

    [Fact]
    public void DropsFightersOutsideTheRadius()
    {
        var (world, reader) = Rig();
        world.AddArea(1, Spot, 300, 1);
        Assert.Empty(reader.Near(new Vector2(5000, 5000), 1000, 10));
    }

    [Fact]
    public void NearestFirstAndCappedAtMax()
    {
        var (world, reader) = Rig();
        world.AddFighter(1, (900, 0));
        world.AddFighter(2, (100, 0));
        world.AddFighter(3, (500, 0));
        world.AddFighter(4, (5000, 0));
        Assert.Equal([2, 3, 1], reader.Near(Vector2.Zero, 1000, 10).Select(entity => entity.Id));
        Assert.Equal([2, 3], reader.Near(Vector2.Zero, 1000, 2).Select(entity => entity.Id));
    }

    [Fact]
    public void GarbageTransformDropsTheEntity()
    {
        var (world, reader) = Rig();
        var components = world.AddFighter(1, Spot);
        world.AddFighter(2, Spot);
        world.Memory.WriteArray(components[Offsets.TransformComponentId] + Offsets.TransformPosition, new[] { double.NaN, 0.0, 0.0 });
        Assert.Equal([2], reader.Near(Vector2.Zero, 1000, 10).Select(entity => entity.Id));

        world.Memory.WriteArray(components[Offsets.TransformComponentId] + Offsets.TransformPosition, new[] { 1.0, 2.0, 3.0 });
        world.Memory.WriteArray(components[Offsets.TransformComponentId] + Offsets.TransformDirection, new[] { double.PositiveInfinity, 0.0 });
        Assert.Equal([2], reader.Near(Vector2.Zero, 1000, 10).Select(entity => entity.Id));

        world.Memory.WriteArray(components[Offsets.TransformComponentId] + Offsets.TransformDirection, new[] { 0.0, 1e9 });
        Assert.Equal([2], reader.Near(Vector2.Zero, 1000, 10).Select(entity => entity.Id));
    }

    [Fact]
    public void AFighterWithoutTransformIsDropped()
    {
        var (world, reader) = Rig();
        world.AddFightEntity(1, (Offsets.ShapeComponentId, world.Alloc(0x100)));
        Assert.Empty(reader.Near(Vector2.Zero, 1000, 10));
    }

    [Fact]
    public void ReadsPositionAndDirection()
    {
        var (world, reader) = Rig();
        var components = world.AddFighter(1, Spot);
        var transform = components[Offsets.TransformComponentId];
        world.Memory.WriteArray(transform + Offsets.TransformPosition, new[] { 500.0, 200.0, 33.0 });
        world.Memory.WriteArray(transform + Offsets.TransformDirection, new[] { 0.0, -1.0 });
        var entity = Only(reader);
        Assert.Equal(new Vector3(500, 200, 33), entity.Position);
        Assert.Equal(new Vector2(0, -1), entity.Direction);
    }

    [Fact]
    public void EveryBlockReadsWhenGood()
    {
        var (world, reader) = Rig();
        FullFighter(world);
        var entity = Only(reader);

        Assert.Equal(new FightMotion(1200, 20, new Vector2(0, 1), new Vector2(3, 4), true, true, 9, 55.5), entity.Motion);
        Assert.Equal(new FightCommand(MoveCommandType.Circle, 9, 1.5, 2), entity.Command);
        Assert.Equal(new FightBullet(4, 900, new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9), true, true, false, 77), entity.Bullet);
        Assert.Equal(BulletMoveType.Link, entity.Bullet!.Value.Kind);
        Assert.Equal(new FightTrack(true, 88, true, false, 640), entity.Track);
        Assert.Equal(new FightLink(4242, 2, 5, true), entity.Link);
        Assert.Equal(new FightTimed(1.25, 6), entity.Timed);
        Assert.Equal(new FightBeam(new Vector3(10, 20, 30), new Vector3(40, 50, 60)), entity.Beam);
        Assert.Equal(new FightNpc(1001, 2, 3, 4), entity.Npc);
        Assert.Equal(new FightRole(555, 1, 80), entity.Role);
        Assert.Equal(new FightShape(1, 300, 11, 12), entity.Shape);
        Assert.Equal(2.5, entity.Scale);
        Assert.Equal(2, entity.Camp);
        Assert.Equal(7504, entity.OwnerId);
        Assert.Equal(12.5, entity.Lifetime);
    }

    private static nint Pointer(FakeMemory memory, nint at) => memory.TryReadPtr(at, out var pointer) ? pointer : 0;

    private static FightEntity? Survivor(FakeWorld world, FightEntityReader reader, uint id = 10) =>
        reader.Near(Vector2.Zero, 1000, 10).SingleOrDefault(entity => entity.Id == id);

    private static IEnumerable<object[]> Poisons()
    {
        yield return Case("motion speed NaN", Offsets.MoveComponentId, (m, c) => m.Write(c + Offsets.MoveSpeed, double.NaN), e => e.Motion);
        yield return Case("motion travelled huge", Offsets.MoveComponentId, (m, c) => m.Write(c + Offsets.MoveTravelled, 1e9), e => e.Motion);
        yield return Case("motion velocity infinite", Offsets.MoveComponentId, (m, c) => m.WriteArray(c + Offsets.MoveDirection, new[] { 0.0, double.PositiveInfinity }), e => e.Motion);
        yield return Case("motion command id huge", Offsets.MoveComponentId, (m, c) => m.Write(c + Offsets.MoveCommandId, uint.MaxValue), e => e.Motion);
        yield return Case("command pointer null", Offsets.MoveCmdComponentId, (m, c) => m.Write(c + Offsets.MoveCmdCommand, 0L), e => e.Command);
        yield return Case("command pointer unmapped", Offsets.MoveCmdComponentId, (m, c) => m.Write(c + Offsets.MoveCmdCommand, 0x1234L), e => e.Command);
        yield return Case("command type 13", Offsets.MoveCmdComponentId, (m, c) => m.Write(Pointer(m, c + Offsets.MoveCmdCommand) + Offsets.CmdType, (byte)13), e => e.Command);
        yield return Case("command elapsed NaN", Offsets.MoveCmdComponentId, (m, c) => m.Write(Pointer(m, c + Offsets.MoveCmdCommand) + Offsets.CmdElapsed, double.NaN), e => e.Command);
        yield return Case("bullet range huge", Offsets.BulletComponentId, (m, c) => m.Write(c + Offsets.BulletRange, 1e9), e => e.Bullet);
        yield return Case("bullet end NaN", Offsets.BulletComponentId, (m, c) => m.WriteArray(c + Offsets.BulletEnd, new[] { 0.0, double.NaN, 0.0 }), e => e.Bullet);
        yield return Case("bullet bound source id zero", Offsets.BulletComponentId, (m, c) => m.Write(c + Offsets.BulletSource + Offsets.RefId, 0u), e => e.Bullet);
        yield return Case("track range infinite", Offsets.TrackComponentId, (m, c) => m.Write(c + Offsets.TrackRange, double.PositiveInfinity), e => e.Track);
        yield return Case("track bound target id huge", Offsets.TrackComponentId, (m, c) => m.Write(c + Offsets.TrackTarget + Offsets.RefId, uint.MaxValue), e => e.Track);
        yield return Case("link skill id huge", Offsets.SkillAttachComponentId, (m, c) => m.Write(c + Offsets.SkillAttachSkill, uint.MaxValue), e => e.Link);
        yield return Case("timed elapsed NaN", Offsets.LastedEffectComponentId, (m, c) => m.Write(c + Offsets.LastedElapsed, double.NaN), e => e.Timed);
        yield return Case("beam start huge", Offsets.ViewAssistComponentId, (m, c) => m.WriteArray(c + Offsets.ViewAssistStart, new[] { 1e12, 0.0, 0.0 }), e => e.Beam);
        yield return Case("npc id zero", Offsets.NpcComponentId, (m, c) => m.Write(c + Offsets.NpcId, 0u), e => e.Npc);
        yield return Case("npc category huge", Offsets.NpcComponentId, (m, c) => m.Write(c + Offsets.NpcCategory, uint.MaxValue), e => e.Npc);
        yield return Case("role config huge", Offsets.RoleComponentId, (m, c) => m.Write(c + Offsets.FightRoleConfig, 200_000_000u), e => e.Role);
        yield return Case("role level negative", Offsets.RoleComponentId, (m, c) => m.Write(c + Offsets.FightRoleLevel, -5), e => e.Role);
        yield return Case("scale NaN", Offsets.ScaleComponentId, (m, c) => m.Write(c + Offsets.ScaleValue, double.NaN), e => e.Scale);
        yield return Case("lifetime NaN", Offsets.SuicideComponentId, (m, c) => m.Write(c + Offsets.SuicideLife, double.NaN), e => e.Lifetime);
        yield return Case("lifetime negative", Offsets.SuicideComponentId, (m, c) => m.Write(c + Offsets.SuicideLife, -1.0), e => e.Lifetime);
        yield return Case("lifetime above max", Offsets.SuicideComponentId, (m, c) => m.Write(c + Offsets.SuicideLife, Offsets.MaxLifetimeSeconds + 1.0), e => e.Lifetime);

        static object[] Case(string name, int component, Action<FakeMemory, nint> poison, Func<FightEntity, object?> block) => [name, component, poison, block];
    }

    [Fact]
    public void GarbageInABlockNullsOnlyThatBlock()
    {
        foreach (var args in Poisons())
        {
            var (name, component, poison, block) = ((string)args[0], (int)args[1], (Action<FakeMemory, nint>)args[2], (Func<FightEntity, object?>)args[3]);
            var (world, reader) = Rig();
            var components = FullFighter(world);
            Assert.True(block(Survivor(world, reader)!) is not null, $"{name}: good first");

            poison(world.Memory, components[component]);
            var entity = Survivor(world, reader);
            Assert.True(entity is not null, $"{name}: entity survives");
            Assert.True(block(entity!) is null, $"{name}: block null");
            if (component != Offsets.ShapeComponentId) Assert.NotNull(entity!.Shape);
        }
    }

    [Fact]
    public void UnreadableBlocksAreNullAndTheEntitySurvives()
    {
        var (world, reader) = Rig();
        var transform = world.Alloc(0x100);
        world.Memory.WriteArray(transform + Offsets.TransformPosition, new[] { 500.0, 200.0, 0.0 });
        world.AddFightEntity(10, [(Offsets.TransformComponentId, transform), .. BlockComponents.Select(id => (id, world.Alloc(0x10)))]);

        var entity = Survivor(world, reader)!;
        Assert.Null(entity.Motion);
        Assert.Null(entity.Command);
        Assert.Null(entity.Bullet);
        Assert.Null(entity.Track);
        Assert.Null(entity.Link);
        Assert.Null(entity.Timed);
        Assert.Null(entity.Beam);
        Assert.Null(entity.Npc);
        Assert.Null(entity.Role);
        Assert.Null(entity.Shape);
        Assert.Null(entity.Scale);
        Assert.Null(entity.Camp);
        Assert.Equal(0, entity.OwnerId);
        Assert.Null(entity.Lifetime);
        Assert.Empty(entity.Buffs);
        Assert.Equal(BlockComponents.Length + 1, entity.Components.Length);
    }

    [Fact]
    public void AbsentBlocksAreNull()
    {
        var (world, reader) = Rig();
        world.AddFighter(1, Spot);
        var entity = Only(reader);
        Assert.Null(entity.Motion);
        Assert.Null(entity.Bullet);
        Assert.Null(entity.Shape);
        Assert.Null(entity.Camp);
        Assert.Null(entity.Lifetime);
        Assert.Equal(0, entity.OwnerId);
    }

    [Fact]
    public void LifetimeAcceptsZeroAndTheMax()
    {
        var (world, reader) = Rig();
        var components = FullFighter(world);
        world.Memory.Write(components[Offsets.SuicideComponentId] + Offsets.SuicideLife, 0.0);
        Assert.Equal(0.0, Survivor(world, reader)!.Lifetime);
        world.Memory.Write(components[Offsets.SuicideComponentId] + Offsets.SuicideLife, (double)Offsets.MaxLifetimeSeconds);
        Assert.Equal(Offsets.MaxLifetimeSeconds, Survivor(world, reader)!.Lifetime);
    }

    [Fact]
    public void UnboundEntityRefsGiveZeroIds()
    {
        var (world, reader) = Rig();
        var components = FullFighter(world);
        world.Memory.Write(components[Offsets.BulletComponentId] + Offsets.BulletSource + Offsets.RefBound, (byte)0);
        world.Memory.Write(components[Offsets.BulletComponentId] + Offsets.BulletSource + Offsets.RefId, uint.MaxValue);
        world.Memory.Write(components[Offsets.TrackComponentId] + Offsets.TrackTarget + Offsets.RefBound, (byte)0);
        var entity = Survivor(world, reader)!;
        Assert.Equal(0, entity.Bullet!.Value.SourceId);
        Assert.Equal(new FightTrack(false, 0, true, false, 640), entity.Track);
    }

    [Fact]
    public void IdsAboveIntMaxDoNotThrow()
    {
        var (world, reader) = Rig();
        var components = FullFighter(world);
        world.Memory.Write(components[Offsets.AttachmentComponentId] + Offsets.AttachmentOwnerId, 0xFFFF_FFF0u);
        Assert.Equal(0, Survivor(world, reader)!.OwnerId);
    }

    public static IEnumerable<object[]> Archetypes()
    {
        yield return Row(FightArchetype.Player, Offsets.PlayerComponentId);
        yield return Row(FightArchetype.Player, Offsets.PlayerComponentId, Offsets.NpcComponentId);
        yield return Row(FightArchetype.Minion, Offsets.ServantComponentId);
        yield return Row(FightArchetype.Minion, Offsets.ServantComponentId, Offsets.NpcComponentId);
        yield return Row(FightArchetype.Monster, Offsets.NpcComponentId);
        yield return Row(FightArchetype.Monster, Offsets.NpcComponentId, Offsets.BulletComponentId);
        yield return Row(FightArchetype.Bullet, Offsets.BulletComponentId);
        yield return Row(FightArchetype.Bullet, Offsets.BulletComponentId, Offsets.LaserComponentId);
        yield return Row(FightArchetype.Laser, Offsets.LaserComponentId);
        yield return Row(FightArchetype.Laser, Offsets.LaserComponentId, Offsets.LastedEffectComponentId);
        yield return Row(FightArchetype.LastedEffect, Offsets.LastedEffectComponentId);
        yield return Row(FightArchetype.LastedEffect, Offsets.LastedEffectComponentId, Offsets.DirectHitComponentId);
        yield return Row(FightArchetype.DirectHit, Offsets.DirectHitComponentId);
        yield return Row(FightArchetype.DirectHit, Offsets.DirectHitComponentId, Offsets.ThunderCloudComponentId);
        yield return Row(FightArchetype.ThunderCloud, Offsets.ThunderCloudComponentId);
        yield return Row(FightArchetype.ThunderCloud, Offsets.ThunderCloudComponentId, Offsets.GroundEffectComponentId, Offsets.AiComponentId);
        yield return Row(FightArchetype.GroundEffect, Offsets.GroundEffectComponentId, Offsets.AiComponentId);
        yield return Row(FightArchetype.GroundEffect, Offsets.GroundEffectComponentId, Offsets.AiComponentId, Offsets.BuffAreaComponentId);
        yield return Row(FightArchetype.BuffArea, Offsets.GroundEffectComponentId, Offsets.BuffAreaComponentId);
        yield return Row(FightArchetype.BuffArea, Offsets.BuffAreaComponentId);
        yield return Row(FightArchetype.BuffArea, Offsets.BuffAreaComponentId, Offsets.TriggerAreaComponentId);
        yield return Row(FightArchetype.TriggerArea, Offsets.TriggerAreaComponentId);
        yield return Row(FightArchetype.TriggerArea, Offsets.TriggerAreaComponentId, Offsets.MapTrapComponentId);
        yield return Row(FightArchetype.MapTrap, Offsets.MapTrapComponentId);
        yield return Row(FightArchetype.MapTrap, Offsets.MapTrapComponentId, Offsets.ShapeComponentId);
        yield return Row(FightArchetype.Anchor, Offsets.ShapeComponentId, Offsets.TransformComponentId);
        yield return Row(FightArchetype.Anchor, Offsets.ScaleComponentId, Offsets.TransformComponentId);
        yield return Row(FightArchetype.Anchor, Offsets.ScaleComponentId, Offsets.ShapeComponentId, Offsets.TransformComponentId);
        yield return Row(FightArchetype.Unknown, Offsets.TransformComponentId);
        yield return Row(FightArchetype.Unknown, Offsets.ShapeComponentId, Offsets.AttachmentComponentId);
        yield return Row(FightArchetype.Unknown);
        yield return Row(FightArchetype.Unknown, Offsets.GroundEffectComponentId, Offsets.ShapeComponentId);

        static object[] Row(FightArchetype expected, params int[] components) => [expected, components];
    }

    [Theory]
    [MemberData(nameof(Archetypes))]
    public void ClassifiesByComponents(FightArchetype expected, int[] components) =>
        Assert.Equal(expected, FightClassifier.Classify(components.ToHashSet()));

    [Fact]
    public void TheReaderClassifiesWhatItReads()
    {
        var (world, reader) = Rig();
        world.AddFighter(1, (10, 0), Offsets.GroundEffectComponentId);
        world.AddFighter(2, (20, 0), Offsets.GroundEffectComponentId, Offsets.AiComponentId);
        world.AddFighter(3, (30, 0), Offsets.PlayerComponentId, Offsets.NpcComponentId);
        var byId = reader.Near(Vector2.Zero, 1000, 10).ToDictionary(entity => entity.Id, entity => entity.Archetype);
        Assert.NotEqual(FightArchetype.GroundEffect, byId[1]);
        Assert.Equal(FightArchetype.GroundEffect, byId[2]);
        Assert.Equal(FightArchetype.Player, byId[3]);
    }

    [Fact]
    public void ReadAllAgreesWithTryReadComponent()
    {
        var world = new FakeWorld();
        var viaEntry = world.AddEntity(1, 0, true, 0, 0, 0);
        var viaDirect = world.AddEntity(2, 0, true, 0, 0, 0);
        world.AddCast(viaEntry, 5, 1, 1, 2);
        world.AddCast(viaDirect, 5, 1, 1, 2, direct: true);
        world.AddFighter(3, Spot, Offsets.ShapeComponentId, Offsets.ScaleComponentId, Offsets.BulletComponentId);
        var components = new FightComponents(world.Memory, FakeWorld.Module);

        foreach (var id in new uint[] { 1, 2, 3 })
        {
            var fighter = world.FighterEntity(id);
            var all = components.ReadAll(fighter);
            Assert.NotEmpty(all);
            for (var component = 0; component < Offsets.FightComponentCount; component++)
            {
                var found = components.TryReadComponent(fighter, component, out var address) && address != 0;
                Assert.Equal(found, all.ContainsKey(component));
                if (found) Assert.Equal(address, all[component]);
            }
        }
    }

    [Fact]
    public void ReadAllBulkReadsInAHandfulOfReads()
    {
        var world = new FakeWorld();
        world.AddFighter(3, Spot, Offsets.ShapeComponentId, Offsets.ScaleComponentId, Offsets.BulletComponentId, Offsets.SideComponentId);
        var components = new FightComponents(world.Memory, FakeWorld.Module);
        world.Memory.Reads.Clear();
        Assert.Equal(5, components.ReadAll(world.FighterEntity(3)).Count);
        Assert.InRange(world.Memory.Reads.Count, 1, 10);
    }

    [Fact]
    public void ReadAllIgnoresAGarbageSlotCap()
    {
        var world = new FakeWorld();
        world.AddFighter(3, Spot, Offsets.ShapeComponentId);
        var fighter = world.FighterEntity(3);
        world.Memory.Write(fighter + Offsets.FightEntityDirectTable + Offsets.FightTableSlotCap, 1_000_000);
        Assert.Empty(new FightComponents(world.Memory, FakeWorld.Module).ReadAll(fighter));
    }
}
