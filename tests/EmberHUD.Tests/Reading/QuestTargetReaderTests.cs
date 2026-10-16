using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class QuestTargetReaderTests
{
    private static QuestTarget[] Read(FakeWorld world)
    {
        var minimap = new MinimapReader(world.Memory, FakeWorld.Module, new Names(world.Memory, FakeWorld.Module));
        minimap.ReadRotationDegrees(1);
        return new QuestTargetReader(world.Memory).Read(minimap);
    }

    [Fact]
    public void ReadsEveryEntry()
    {
        var world = new FakeWorld();
        world.SetQuestTargets(world.AddMinimap(0f), 7, (1, 3450f, -1710f), (2, -8040f, -4950f));

        Assert.Equal([new QuestTarget(1, new Vector2(3450, -1710)), new QuestTarget(2, new Vector2(-8040, -4950))], Read(world));
    }

    [Fact]
    public void IdIsTheArrowId()
    {
        var world = new FakeWorld();
        world.SetQuestTargets(world.AddMinimap(0f), 7, (42, 1, 2));

        Assert.Equal(42, Assert.Single(Read(world)).Id);
    }

    [Fact]
    public void SkipsNonFinitePositions()
    {
        var world = new FakeWorld();
        world.SetQuestTargets(world.AddMinimap(0f), 7, (1, float.NaN, 1), (2, 1, float.PositiveInfinity), (3, 2e7f, 1), (4, 5, 6));

        Assert.Equal([4], Read(world).Select(target => target.Id));
    }

    [Fact]
    public void ReturnsEmptyOnUnreadableList()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetQuestTargets(minimap, 7, (1, 1, 1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapQuestTargets, FakeWorld.Unmapped);

        Assert.Empty(Read(world));
    }

    [Fact]
    public void ReturnsEmptyOnAbsurdCount()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetQuestTargets(minimap, 7, (1, 1, 1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapQuestTargets + Offsets.TArrayNum, Offsets.MaxQuestTargets + 1);

        Assert.Empty(Read(world));
    }

    [Fact]
    public void NoWidgetGivesNoTargets() => Assert.Empty(Read(new FakeWorld()));
}
