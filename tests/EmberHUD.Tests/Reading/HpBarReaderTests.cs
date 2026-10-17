using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public class HpBarReaderTests
{
    private sealed class Rig
    {
        public FakeWorld World { get; } = new();
        public FakeUi Ui { get; }
        public Vector2? Origin { get; set; } = Vector2.Zero;
        public HpBarReader Reader { get; }

        public Rig()
        {
            Ui = new FakeUi(World);
            Reader = new HpBarReader(World.Memory, FakeWorld.Module, new Names(World.Memory, FakeWorld.Module), () => Origin);
        }

        public FakeEntity Monster(uint id) => World.AddEntity(id, 100, true, 0, 0, 0) with { Value = World.Alloc(0x800) };

        public (nint Component, nint Widget, nint Track) Normal(FakeEntity monster, byte widgetVisibility = 4, byte trackVisibility = 3, byte canvasVisibility = 3) =>
            Ui.AddHpBar(monster, Offsets.HpBarNormalClassName, Offsets.HpBarNormalCanvas, Offsets.HpBarNormalTrack, new Vector2(1745.5f, 499.1f), new Vector2(255.36f, 23.04f), 0.2464f, 0.383f, widgetVisibility, trackVisibility, canvasVisibility);

        public (nint Component, nint Widget, nint Track) Elite(FakeEntity monster) =>
            Ui.AddHpBar(monster, Offsets.HpBarEliteClassName, Offsets.HpBarEliteCanvas, Offsets.HpBarEliteTrack, new Vector2(1535.3f, 429.3f), new Vector2(192, 35), 0.4469f, 0.4469f);

        public (nint Component, nint Widget, nint Track) Boss(FakeEntity monster) =>
            Ui.AddHpBar(monster, Offsets.HpBarBossClassName, Offsets.HpBarBossCanvas, Offsets.HpBarBossTrack, new Vector2(1525.6f, 309.6f), new Vector2(160, 16), 1, 1);

        public Entity[] Apply(EntityKind kind, params FakeEntity[] monsters)
        {
            World.PublishEntities(monsters);
            return Reader.Apply([.. monsters.Select(m => new Entity((int)m.Id, 100, Vector3.Zero, kind, Rarity.Normal))]);
        }

        public Entity Apply(FakeEntity monster) => Apply(EntityKind.Monster, monster).Single();
    }

    private static ScreenRect Rect(float x, float y, float w, float h, float mx, float my) => new(new Vector2(x, y), new Vector2(x + w * mx, y + h * my));

    private static readonly ScreenRect NormalRect = Rect(1745.5f, 499.1f, 255.36f, 23.04f, 0.2464f, 0.383f);
    private static readonly ScreenRect EliteRect = Rect(1535.3f, 429.3f, 192, 35, 0.4469f, 0.4469f);

    [Fact]
    public void ShownNormalBarIsTheImageBgRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Equal(NormalRect, entity.GameBar);
    }

    [Fact]
    public void ShownEliteBarIsTheBarBgRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Elite(monster);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Equal(EliteRect, entity.GameBar);
    }

    [Fact]
    public void ShownBossBarIsTheImageBgRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Boss(monster);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Equal(Rect(1525.6f, 309.6f, 160, 16, 1, 1), entity.GameBar);
    }

    [Fact]
    public void OriginIsSubtracted()
    {
        var rig = new Rig { Origin = new Vector2(100, 50) };
        var monster = rig.Monster(1);
        rig.Normal(monster);
        Assert.Equal(1645.5f, rig.Apply(monster).GameBar!.Value.Min.X, 0.01f);
    }

    [Fact]
    public void NoOriginHasTheBarButNoRect()
    {
        var rig = new Rig { Origin = null };
        var monster = rig.Monster(1);
        rig.Normal(monster);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void CollapsedWidgetHasBarButNoRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster, widgetVisibility: 1);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void CollapsedTrackHasBarButNoRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster, trackVisibility: 1);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void CollapsedCanvasWithAStaleVisibleTrackHasBarButNoRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster, canvasVisibility: 1);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void EliteUsesItsOwnCanvasOffset()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        var (_, widget, _) = rig.Elite(monster);
        rig.World.Memory.Write(widget + Offsets.HpBarNormalCanvas, 0L);
        Assert.Equal(EliteRect, rig.Apply(monster).GameBar);
        var canvas = rig.World.Memory.TryReadPtr(widget + Offsets.HpBarEliteCanvas, out var c) ? c : 0;
        rig.World.Memory.Write(canvas + Offsets.UWidgetVisibility, (byte)1);
        Assert.Null(rig.Apply(monster).GameBar);
    }

    [Fact]
    public void ReusedPooledWidgetGivesNoRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        var (_, widget, _) = rig.Normal(monster);
        rig.World.SetObjectSerial(widget, 6);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void NullWeakWidgetHasBarButNoRect()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        var (component, _, _) = rig.Normal(monster);
        rig.World.Memory.Write(component + Offsets.HpBarWidgetWeak, 0L);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void UnknownWidgetClassHasNoBar()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Ui.AddHpBar(monster, "HpBarMonsterWoodStake_C", Offsets.HpBarNormalCanvas, Offsets.HpBarNormalTrack, new Vector2(10, 10), new Vector2(100, 10), 1, 1);
        Assert.False(rig.Apply(monster).HasGameBar);
    }

    [Fact]
    public void MonsterMissingFromTheMapHasNoBar()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster);
        rig.World.PublishEntities(rig.Monster(2));
        var entity = rig.Reader.Apply([new Entity(1, 100, Vector3.Zero, EntityKind.Monster, Rarity.Normal)]).Single();
        Assert.False(entity.HasGameBar);
    }

    [Fact]
    public void MonsterWithoutTheUiComponentHasNoBar()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.World.AddComponents(monster.Value, "ECfgComponent");
        Assert.False(rig.Apply(monster).HasGameBar);
    }

    [Theory]
    [InlineData(float.NaN, 10, 1)]
    [InlineData(100, 0, 1)]
    [InlineData(100, 10, 1000)]
    public void BrokenGeometryGivesNoRect(float width, float height, float scale)
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Ui.AddHpBar(monster, Offsets.HpBarNormalClassName, Offsets.HpBarNormalCanvas, Offsets.HpBarNormalTrack, new Vector2(10, 10), new Vector2(width, height), scale, scale);
        var entity = rig.Apply(monster);
        Assert.True(entity.HasGameBar);
        Assert.Null(entity.GameBar);
    }

    [Fact]
    public void NonMonsterKindsComeBackUntouched()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster);
        rig.World.PublishEntities(monster);
        var input = new[] { new Entity(1, 100, Vector3.Zero, EntityKind.Npc, Rarity.Normal) };
        Assert.Same(input, rig.Reader.Apply(input));
        Assert.False(input[0].HasGameBar);
    }

    [Fact]
    public void EachMonsterGetsItsOwnRect()
    {
        var rig = new Rig();
        var a = rig.Monster(1);
        var b = rig.Monster(2);
        rig.Normal(a);
        rig.Elite(b);
        var entities = rig.Apply(EntityKind.Monster, a, b);
        Assert.Equal(NormalRect, entities[0].GameBar);
        Assert.Equal(EliteRect, entities[1].GameBar);
    }

    [Fact]
    public void ComponentIsCachedUntilReset()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster);
        rig.Apply(monster);
        var components = monster.Value + Offsets.EntityComponents;
        rig.World.Memory.Reads.Clear();
        rig.Apply(monster);
        Assert.DoesNotContain(rig.World.Memory.Reads, r => r.Address == components);
        rig.Reader.Reset();
        rig.World.Memory.Reads.Clear();
        rig.Apply(monster);
        Assert.Contains(rig.World.Memory.Reads, r => r.Address == components);
    }

    [Fact]
    public void CachedComponentOfAnotherEntityIsDropped()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        var (component, _, _) = rig.Normal(monster);
        rig.Apply(monster);
        rig.World.Memory.Write(component + Offsets.ComponentEntity, FakeWorld.Unmapped);
        var components = monster.Value + Offsets.EntityComponents;
        rig.World.Memory.Reads.Clear();
        rig.Apply(monster);
        Assert.Contains(rig.World.Memory.Reads, r => r.Address == components);
    }

    [Fact]
    public void UnchangedEntitiesReturnTheSameArray()
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        rig.Normal(monster);
        rig.World.PublishEntities(monster);
        var first = rig.Reader.Apply([new Entity(1, 100, Vector3.Zero, EntityKind.Monster, Rarity.Normal)]);
        Assert.Same(first, rig.Reader.Apply(first));
    }

    [Theory]
    [InlineData("monsterMap")]
    [InlineData("components")]
    [InlineData("weak")]
    [InlineData("widgetClass")]
    [InlineData("track")]
    [InlineData("slate")]
    [InlineData("fightMgr")]
    public void UnreadableMemoryDoesNotThrow(string broken)
    {
        var rig = new Rig();
        var monster = rig.Monster(1);
        var (component, widget, track) = rig.Normal(monster);
        rig.World.PublishEntities(monster);
        var memory = rig.World.Memory;
        switch (broken)
        {
            case "monsterMap": memory.Write(rig.World.FightMgr + Offsets.FightMapMonster, FakeWorld.Unmapped); break;
            case "components": memory.Write(monster.Value + Offsets.EntityComponents, FakeWorld.Unmapped); break;
            case "weak": memory.Write(component + Offsets.HpBarWidgetWeak, uint.MaxValue); break;
            case "widgetClass": memory.Write(widget + Offsets.UObjectClass, FakeWorld.Unmapped); break;
            case "track": memory.Write(widget + Offsets.HpBarNormalTrack, FakeWorld.Unmapped); break;
            case "slate": memory.Write(track + Offsets.UWidgetSlate, FakeWorld.Unmapped); break;
            case "fightMgr": memory.Write(FakeWorld.Module + Offsets.FightMgrGlobalRva, FakeWorld.Unmapped); break;
        }

        var entity = rig.Reader.Apply([new Entity(1, 100, Vector3.Zero, EntityKind.Monster, Rarity.Normal)]).Single();
        Assert.Null(entity.GameBar);
        Assert.Equal(broken is "slate" or "track", entity.HasGameBar);
    }
}
