using System.Linq;
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
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("shape", "star"))).Contains("polygon"), "shapes listed");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("shape", "text"))).Contains("label"), "text needs a label");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("color", "notacolor"))).Contains("color"), "bad color");
        Check.That(Wire.Fails(tools, "draw", Args(("entity", 5), ("seconds", 0))).Contains("seconds"), "bad seconds");
    }

    static JsonArray Points(params (float X, float Y)[] points) => new(points.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());

    [Test]
    public void RectRunsFromCornerToCorner()
    {
        var (marks, tools, _) = Setup(GameState.Empty);
        Wire.Run(tools, "draw", Args(("shape", "rect"), ("x", 100), ("y", 50), ("x2", 160), ("y2", 140), ("screen", true)));
        Check.That(marks.All[0].Points!.SequenceEqual([new Vector2(100, 50), new Vector2(160, 140)]), "corners kept");
        Check.Equal(new Vector2(100, 50), marks.Locate(marks.All[0], Game.State)!.Value);
    }

    [Test]
    public void RectWithWidthAndHeightSitsOnItsPivot()
    {
        var (marks, tools, _) = Setup(GameState.Empty);
        Wire.Run(tools, "draw", Args(("shape", "rect"), ("x", 100), ("y", 50), ("width", 60), ("height", 90), ("screen", true)));
        Wire.Run(tools, "draw", Args(("shape", "rect"), ("x", 100), ("y", 50), ("width", 60), ("height", 90), ("pivot", "topleft"), ("screen", true)));
        Check.Equal((new Vector2(70, 5), new Vector2(130, 95)), Marks.Box(new Vector2(100, 50), marks.All[0].Extent!.Value, marks.All[0].TopLeft));
        Check.Equal((new Vector2(100, 50), new Vector2(160, 140)), Marks.Box(new Vector2(100, 50), marks.All[1].Extent!.Value, marks.All[1].TopLeft));
    }

    [Test]
    public void PolygonAndLineTakePoints()
    {
        var (marks, tools, _) = Setup(GameState.Empty);
        Wire.Run(tools, "draw", Args(("shape", "polygon"), ("points", Points((0, 0), (10, 0), (10, 10))), ("screen", true), ("fill", true)));
        Wire.Run(tools, "draw", Args(("shape", "line"), ("x", 1), ("y", 2), ("x2", 30), ("y2", 40), ("screen", true), ("thickness", 5)));
        Check.Equal(3, marks.All[0].Points!.Length);
        Check.That(marks.All[0].Fill, "fill kept");
        Check.That(marks.All[1].Points!.SequenceEqual([new Vector2(1, 2), new Vector2(30, 40)]), "line ends kept");
        Check.Equal(5f, marks.All[1].Thickness);
    }

    [Test]
    public void WorldPolygonProjectsItsPoints()
    {
        var (marks, tools, _) = Setup(World() with { Player = new Player(new Vector3(0, 0, 0), Vector2.UnitX) });
        Wire.Run(tools, "draw", Args(("shape", "polygon"), ("points", Points((0, 0), (100, 0), (100, 100)))));
        var mark = marks.All[0];
        Check.Equal(Game.State.Camera!.ToScreen(new Vector3(0, 0, 0), Viewport), marks.Locate(mark, Game.State));
        Check.Equal(Game.State.Camera!.ToScreen(new Vector3(100, 100, 0), Viewport), marks.Project(mark, mark.Points![2], Game.State));
    }

    [Test]
    public void NewShapesRejectBadArguments()
    {
        var (_, tools, _) = Setup(World(Mob(5, 1, 100)));
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "rect"), ("x", 1), ("y", 1), ("screen", true))).Contains("width and height"), "rect needs a size");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "ellipse"), ("x", 1), ("y", 1), ("width", 5), ("screen", true))).Contains("width and height"), "ellipse needs both");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "line"), ("x", 1), ("y", 1), ("screen", true))).Contains("x2 and y2"), "line needs an end");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "polygon"), ("points", Points((0, 0), (1, 1))), ("screen", true))).Contains("3 points"), "polygon needs 3");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "polygon"), ("points", new JsonArray(1, 2, 3)), ("screen", true))).Contains("points"), "points must be pairs");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "line"), ("entity", 5), ("x2", 1), ("y2", 1))).Contains("entity"), "no entity lines");
        Check.That(Wire.Fails(tools, "draw", Args(("shape", "box"), ("x", 1), ("y", 1), ("thickness", 0))).Contains("thickness"), "thickness range");
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

    [Test]
    public void RenderDrawsTheRectLinePolygonEllipseAndCrossShapes()
    {
        var (marks, tools, _) = Setup(GameState.Empty);
        (string Shape, (string, JsonNode?)[] Extra)[] shapes =
        [
            ("rect", [("x", 10), ("y", 10), ("x2", 90), ("y2", 70), ("fill", true)]),
            ("ellipse", [("x", 200), ("y", 100), ("width", 80), ("height", 30)]),
            ("cross", [("x", 300), ("y", 100)]),
            ("line", [("x", 10), ("y", 200), ("x2", 120), ("y2", 260), ("label", "edge")]),
            ("polygon", [("points", Points((400, 100), (460, 100), (430, 160))), ("fill", true)]),
        ];
        foreach (var (shape, extra) in shapes)
        {
            var before = 0;
            marks.Clear(null);
            Wire.Run(tools, "draw", Args([("shape", shape), ("screen", true), .. extra]));
            var vertices = 0;
            TestFrame.Run(() =>
            {
                marks.Render(Viewport);
                vertices = Hexa.NET.ImGui.ImGui.GetForegroundDrawList().VtxBuffer.Size;
            });
            Check.That(vertices > before, shape + " draws");
        }
    }
}
