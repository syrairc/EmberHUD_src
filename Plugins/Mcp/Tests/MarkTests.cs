using System.Numerics;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class MarkTests
{
    static readonly Camera Above = new(new Vector3(0, 0, 1000), new Vector3(-90, 0, 0), 90);
    static readonly Vector2 Viewport = new(1920, 1080);

    static (Marks Marks, Tool[] Tools, long[] Clock) Setup(GameState state)
    {
        TestGame.Set(state);
        TestGame.UseScene(null);
        long[] clock = [1000];
        var marks = new Marks { Viewport = Viewport };
        return (marks, MarkTools.Create(marks, () => clock[0]), clock);
    }

    static GameState World(params Entity[] entities) => GameState.Empty with { Camera = Above, Entities = entities };

    static JsonObject Args(params (string Name, JsonNode? Value)[] pairs)
    {
        var args = new JsonObject();
        foreach (var (name, value) in pairs) args[name] = value;
        return args;
    }

    [Test]
    public void EntityMarkFollowsTheEntity()
    {
        var (marks, tools, _) = Setup(World(Mob(5, 1, 100)));
        var result = Wire.Run(tools, "draw", Args(("entity", 5)));
        Check.That(result["screen"] is not null, "on screen right away");
        var before = marks.Locate(marks.All[0], Game.State);
        TestGame.Set(World(Mob(5, 1, 300)));
        var after = marks.Locate(marks.All[0], Game.State);
        Check.That(before is not null && after is not null && before != after, "moves with the entity");
    }

    [Test]
    public void EntityMarkHidesWhileTheEntityIsGone()
    {
        var (marks, tools, _) = Setup(World(Mob(5, 1, 100)));
        Wire.Run(tools, "draw", Args(("entity", 5)));
        TestGame.Set(World());
        Check.That(marks.Locate(marks.All[0], Game.State) is null, "hidden");
        Check.Equal(1, marks.Count);
    }

    [Test]
    public void ProjectileIdsResolve()
    {
        var (marks, tools, _) = Setup(GameState.Empty with { Camera = Above, Projectiles = [new Projectile(9, 1, Rarity.Normal, new Vector2(50, 0), Vector2.Zero, Vector2.UnitX, 10, 100)] });
        Wire.Run(tools, "draw", Args(("entity", 9)));
        Check.That(marks.Locate(marks.All[0], Game.State) is not null, "projectile found");
    }

    [Test]
    public void SceneEntitiesResolveThroughTheThrottledWalk()
    {
        var (marks, tools, clock) = Setup(World());
        var walks = 0;
        TestGame.UseScene(() =>
        {
            walks++;
            return [new SceneEntity(7, 1, new Vector3(20, 0, 0), true, null, "Thing")];
        });
        Wire.Run(tools, "draw", Args(("entity", 7)));
        Check.That(marks.Locate(marks.All[0], Game.State) is not null, "found in the scene");
        var before = walks;
        marks.Tick(clock[0] + 10);
        Check.Equal(before, walks);
        marks.Tick(clock[0] + 200);
        Check.Equal(before + 1, walks);
        TestGame.UseScene(null);
    }

    [Test]
    public void UnknownEntityFails()
    {
        var (_, tools, _) = Setup(World());
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 42))).Contains("no entity 42"), "names the id");
    }

    [Test]
    public void ScreenMarkStaysOnItsPixels()
    {
        var (marks, tools, _) = Setup(GameState.Empty);
        Wire.Run(tools, "draw", Args(("x", 300), ("y", 200), ("screen", true), ("shape", "circle")));
        Check.Equal(new Vector2(300, 200), marks.Locate(marks.All[0], Game.State)!.Value);
    }

    [Test]
    public void WorldMarkUsesThePlayerHeightWithoutZ()
    {
        var (marks, tools, _) = Setup(World() with { Player = new Player(new Vector3(0, 0, 55), Vector2.UnitX) });
        Wire.Run(tools, "draw", Args(("x", 10), ("y", 20)));
        Check.Equal(new Vector3(10, 20, 55), marks.All[0].World!.Value);
    }

    [Test]
    public void BadArgumentsFail()
    {
        var (_, tools, _) = Setup(World(Mob(5, 1, 100)));
        Check.That(Wire.Fails(tools, "draw").Contains("entity, or x and y"), "needs an anchor");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("x", 1))).Contains("not both"), "one anchor");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("shape", "star"))).Contains("box, circle or text"), "shapes listed");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("shape", "text"))).Contains("label"), "text needs a label");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("color", "notacolor"))).Contains("color"), "bad color");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("seconds", 0))).Contains("seconds"), "bad seconds");
    }

    [Test]
    public void ColorsTakeNamesAndHex()
    {
        var (marks, tools, _) = Setup(World(Mob(5, 1, 100)));
        Wire.Run(tools, "draw", Args(("entity", 5), ("color", "#10FF20")));
        Wire.Run(tools, "draw", Args(("entity", 5), ("color", "cyan")));
        Check.Equal(System.Drawing.Color.FromArgb(0x10, 0xFF, 0x20).ToArgb(), marks.All[0].Color.ToArgb());
        Check.Equal(System.Drawing.Color.Cyan.ToArgb(), marks.All[1].Color.ToArgb());
    }

    [Test]
    public void SecondsExpireTheMark()
    {
        var (marks, tools, clock) = Setup(World(Mob(5, 1, 100)));
        Wire.Run(tools, "draw", Args(("entity", 5), ("seconds", 2)));
        marks.Tick(clock[0] + 1999);
        Check.Equal(1, marks.Count);
        marks.Tick(clock[0] + 2000);
        Check.Equal(0, marks.Count);
    }

    [Test]
    public void ClearTakesAnIdOrEverything()
    {
        var (marks, tools, _) = Setup(World(Mob(5, 1, 100)));
        var first = (int)Wire.Run(tools, "draw", Args(("entity", 5)))["id"]!;
        Wire.Run(tools, "draw", Args(("entity", 5)));
        Wire.Run(tools, "draw", Args(("entity", 5)));
        Check.Equal(1, (int)Wire.Run(tools, "draw_clear", Args(("id", first)))["cleared"]!);
        Check.That(Wire.Fails(tools, "draw_clear", Args(("id", first))).Contains("no mark"), "gone already");
        Check.Equal(2, (int)Wire.Run(tools, "draw_clear")["cleared"]!);
        Check.Equal(0, marks.Count);
    }

    [Test]
    public void AtMostAHundredMarks()
    {
        var (_, tools, _) = Setup(World(Mob(5, 1, 100)));
        for (var i = 0; i < Marks.Max; i++) Wire.Run(tools, "draw", Args(("entity", 5)));
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5))).Contains("at most"), "capped");
    }

    [Test]
    public void AreaChangeClearsMarks()
    {
        var main = new Main();
        TestGame.Set(GameState.Empty with { AreaId = 1 });
        main.Tick();
        main.Marking.Add(new Mark(0, "box", null, null, new Vector2(1, 1), 40, System.Drawing.Color.Red, null, null), 0);
        main.Tick();
        Check.Equal(1, main.Marking.Count);
        TestGame.Set(GameState.Empty with { AreaId = 2 });
        main.Tick();
        Check.Equal(0, main.Marking.Count);
    }

    [Test]
    public void RenderDrawsEveryShape()
    {
        var (marks, tools, _) = Setup(World(Mob(5, 1, 100)));
        Wire.Run(tools, "draw", Args(("entity", 5), ("label", "this one?")));
        Wire.Run(tools, "draw", Args(("x", 50), ("y", 50), ("screen", true), ("shape", "circle")));
        Wire.Run(tools, "draw", Args(("x", 80), ("y", 80), ("screen", true), ("shape", "text"), ("label", "hi")));
        var vertices = 0;
        TestFrame.Run(() =>
        {
            marks.Render(Viewport);
            vertices = Hexa.NET.ImGui.ImGui.GetForegroundDrawList().VtxBuffer.Size;
        });
        Check.That(vertices > 0, "draws");
    }
}
