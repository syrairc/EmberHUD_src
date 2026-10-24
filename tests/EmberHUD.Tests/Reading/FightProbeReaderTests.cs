using System.Numerics;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class FightProbeReaderTests
{
    private static readonly (double X, double Y) Spot = (500, 200);

    private static (FakeWorld World, FightProbeReader Reader) Rig()
    {
        var world = new FakeWorld();
        return (world, new FightProbeReader(world.Memory, FakeWorld.Module));
    }

    [Fact]
    public void ReadsShapeScaleCampAndComponents()
    {
        var (world, reader) = Rig();
        world.AddArea(3652, Spot, 300, 2, scale: 2.5);
        var probe = Assert.Single(reader.Near(Vector2.Zero, 1000));
        Assert.Equal(3652, probe.Id);
        Assert.Equal(new Vector2(500, 200), probe.Position);
        Assert.Equal(1u, probe.ShapeKind);
        Assert.Equal(300u, probe.ShapeRadius);
        Assert.Equal(2.5, probe.Scale);
        Assert.Equal(2, probe.Camp);
        Assert.Contains(Offsets.BuffAreaComponentId, probe.Components);
        Assert.Contains(Offsets.ShapeComponentId, probe.Components);
    }

    [Fact]
    public void ReadsTheBuffsOnAFighter()
    {
        var (world, reader) = Rig();
        world.AddBuffed(178, Spot, world.AddBuff(82151, 3.5, 4, stacks: 2), world.AddBuff(10, 1, 1));
        var buffs = Assert.Single(reader.Near(Vector2.Zero, 1000)).Buffs.OrderBy(buff => buff.Id).ToArray();
        Assert.Equal([10, 82151], buffs.Select(buff => buff.Id));
        Assert.Equal([1, 2], buffs.Select(buff => buff.Stacks));
        Assert.Equal(3.5, buffs[1].Remaining);
    }

    [Fact]
    public void AFighterWithoutBuffsHasNone()
    {
        var (world, reader) = Rig();
        world.AddArea(1, Spot, 300, 1);
        Assert.Empty(Assert.Single(reader.Near(Vector2.Zero, 1000)).Buffs);
    }

    [Fact]
    public void ReadsTheOwnerOfARock()
    {
        var (world, reader) = Rig();
        world.AddRock(9031, 7504, Spot, 175);
        var rock = Assert.Single(reader.Near(Vector2.Zero, 1000), probe => probe.Id == 9031);
        Assert.Equal(7504, rock.OwnerId);
        Assert.Equal(-1, rock.Camp);
    }

    [Fact]
    public void DropsFightersOutsideTheRadius()
    {
        var (world, reader) = Rig();
        world.AddArea(1, Spot, 300, 1);
        Assert.Empty(reader.Near(new Vector2(5000, 5000), 1000));
    }

    [Fact]
    public void GarbagePositionsAreSkipped()
    {
        var (world, reader) = Rig();
        world.AddArea(1, (double.NaN, 0), 300, 1);
        Assert.Empty(reader.Near(Vector2.Zero, 1000));
    }
}
