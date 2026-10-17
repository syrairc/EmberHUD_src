using System;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NavigationPlugin;

public sealed class RenderTests
{
    static readonly Vector2 Screen = new(1920, 1080);
    static readonly Camera Looking = new(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);
    static readonly Terrain Open = new(100, 100, 10f, Vector2.Zero, new System.Collections.BitArray(10000, true));

    static GameState Scene(Camera? camera, int targets, GameUi? ui = null) => new()
    {
        AreaId = 1,
        Terrain = Open,
        Camera = camera,
        Ui = ui ?? GameUi.Empty,
        Player = new Player(Vector3.Zero, Vector2.UnitX),
        QuestTargets = targets == 0 ? [] : [.. Enumerable.Range(1, targets).Select(i => new QuestTarget(i, new Vector2(600 * i, 100 * i)))],
    };

    static int Drawn(GameState state, Action<Settings>? configure = null, MapView? view = null, Func<Terrain, Vector2, Vector2, Vector2[]?>? finder = null)
    {
        TestGame.Set(state);
        Map.View = view;
        var main = new Main { Seconds = () => 1 };
        main.Paths.Runner = job => job();
        main.Paths.Finder = finder ?? ((_, from, to) => [from, to]);
        main.Settings.Legend.Show = false;
        main.Settings.Compass.Show = false;
        configure?.Invoke(main.Settings);
        return TestFrame.Vertices(() =>
        {
            main.Tick();
            main.Render();
        });
    }

    static MapView Minimap() => MapView.Follow(Map.Project(Vector3.Zero), Screen, 0.5f);

    static void OnlyGround(Settings settings) => settings.General.Minimap = false;

    static void OnlyMinimap(Settings settings) => settings.General.Ground = false;

    [Test]
    public void GroundOffDrawsNothing() => Check.Equal(0, Drawn(Scene(Looking, 1), s => s.General.Ground = false));

    [Test]
    public void MinimapOffDrawsNothing() => Check.Equal(0, Drawn(Scene(null, 1), s => s.General.Minimap = false, Minimap()));

    [Test]
    public void NullMapViewSkipsMinimap() => Check.Equal(0, Drawn(Scene(null, 1), view: null));

    [Test]
    public void NoCameraSkipsGround() => Check.Equal(0, Drawn(Scene(null, 1), OnlyGround));

    static GameUi FullScreenPanel => new([new GamePanel("Map", PanelKind.WorldMap, default, new Vector2(100, 100), true)]);

    [Test]
    public void GroundHiddenBehindFullScreenPanel()
    {
        Check.Equal(0, Drawn(Scene(Looking, 1, FullScreenPanel), OnlyGround));
        Check.That(Drawn(Scene(Looking, 1), OnlyGround) > 0, "draws without the panel");
    }

    [Test]
    public void MinimapStillDrawsBehindFullScreenPanel() =>
        Check.That(Drawn(Scene(null, 1, FullScreenPanel), OnlyMinimap, Minimap()) > 0, "minimap");

    [Test]
    public void HidingStopsTheWorkAndShowingRecomputes()
    {
        TestGame.Set(Scene(Looking, 1));
        Map.View = null;
        var calls = 0;
        var main = new Main { Seconds = () => 1 };
        main.Paths.Runner = job => job();
        main.Paths.Finder = (_, from, to) =>
        {
            calls++;
            return [from, to];
        };
        main.Settings.General.Show = false;
        main.Tick();
        Check.Equal(0, calls);
        main.Settings.General.Show = true;
        main.Tick();
        Check.Equal(1, calls);
    }

    [Test]
    public void DrawsMoreVerticesWithTwoPathsThanOne()
    {
        Check.That(Drawn(Scene(Looking, 2), OnlyGround) > Drawn(Scene(Looking, 1), OnlyGround), "ground");
        Check.That(Drawn(Scene(null, 2), OnlyMinimap, Minimap()) > Drawn(Scene(null, 1), OnlyMinimap, Minimap()), "minimap");
    }

    static void OnlyLegend(Settings settings)
    {
        settings.General.Ground = false;
        settings.General.Minimap = false;
        settings.Legend.Show = true;
    }

    [Test]
    public void LegendDrawsWhenOnWithPaths() => Check.That(Drawn(Scene(null, 1), OnlyLegend) > 0, "legend");

    [Test]
    public void LegendDrawsMoreWithMorePaths() => Check.That(Drawn(Scene(null, 2), OnlyLegend) > Drawn(Scene(null, 1), OnlyLegend), "rows");

    [Test]
    public void LegendNeedsPaths() => Check.Equal(0, Drawn(Scene(null, 0), OnlyLegend));

    [Test]
    public void LegendHiddenBehindFullScreenPanelUnlessSettingOff()
    {
        var full = new GameUi([new GamePanel("Map", PanelKind.WorldMap, default, new Vector2(100, 100), true)]);

        Check.Equal(0, Drawn(Scene(null, 1, full), OnlyLegend));
        Check.That(Drawn(Scene(null, 1, full), s =>
        {
            OnlyLegend(s);
            s.Legend.HideBehindPanels = false;
        }) > 0, "draws when setting off");
    }

    [Test]
    public void ShowOffDrawsNothing() => Check.Equal(0, Drawn(Scene(Looking, 1), s => s.General.Show = false, Minimap()));

    static int GroundShapeVertices(GroundShape shape, Action<Settings>? more = null) => Drawn(Scene(Looking, 1), s =>
    {
        OnlyGround(s);
        s.GroundShape = shape;
        s.GroundStyle.BaseLine = 0;
        s.GroundStyle.Trail = 0;
        more?.Invoke(s);
    });

    [Test]
    public void StreakAndChevronDrawFilledGeometry()
    {
        var streak = GroundShapeVertices(GroundShape.Streak);
        var chevron = GroundShapeVertices(GroundShape.Chevron);

        Check.That(chevron > 0, "chevron");
        Check.That(streak > chevron, "streak draws two layers per particle");
    }

    [Test]
    public void DotShapeMatchesTheOldDotCount()
    {
        var state = Scene(Looking, 1);
        var style = new Settings().GroundStyle;
        var points = new[] { Vector2.Zero, new Vector2(600, 100) };
        var particles = Particles.Positions(points, PathFinder.Length(points), style.Spacing, style.Speed, 1, new Vector2[256]);
        TestGame.Set(state);
        var perDot = TestFrame.Vertices(() => Ground.Dot(new Vector2(300, 50), style.Size, System.Drawing.Color.Red));

        Check.That(particles > 0 && perDot > 0, "baseline exists");
        Check.Equal(particles * perDot, GroundShapeVertices(GroundShape.Dot));
    }

    [Test]
    public void CoincidentPathDrawsNothing()
    {
        foreach (var shape in new[] { GroundShape.Streak, GroundShape.Chevron, GroundShape.Dot })
            Check.Equal(0, Drawn(Scene(Looking, 1), s =>
            {
                OnlyGround(s);
                s.GroundShape = shape;
                s.GroundStyle.BaseLine = 0;
            }, finder: (_, from, _) => [from, from]));
    }

    static void OnlyCompass(Settings settings)
    {
        settings.General.Ground = false;
        settings.General.Minimap = false;
        settings.Compass.Show = true;
    }

    static Func<Terrain, Vector2, Vector2, Vector2[]?> Unreachable => (_, _, _) => null;

    [Test]
    public void CompassOffDrawsNothing() => Check.Equal(0, Drawn(Scene(Looking, 1), s =>
    {
        OnlyCompass(s);
        s.Compass.Show = false;
    }));

    [Test]
    public void CompassDrawsOneArrowPerObjective()
    {
        var one = Drawn(Scene(Looking, 1), OnlyCompass);

        Check.That(one > 0, "arrow");
        Check.That(Drawn(Scene(Looking, 2), OnlyCompass) > one, "two arrows");
    }

    [Test]
    public void UnreachableObjectiveStillGetsAnArrow() =>
        Check.That(Drawn(Scene(Looking, 1), OnlyCompass, finder: Unreachable) > 0, "dimmed arrow");

    [Test]
    public void CompassHiddenBehindFullScreenPanel()
    {
        Check.Equal(0, Drawn(Scene(Looking, 1, FullScreenPanel), OnlyCompass));
        Check.That(Drawn(Scene(Looking, 1), OnlyCompass) > 0, "draws without the panel");
    }

    [Test]
    public void CompassNeedsCameraAndPlayer()
    {
        Check.Equal(0, Drawn(Scene(null, 1), OnlyCompass));
        Check.Equal(0, Drawn(Scene(Looking, 1) with { Player = null }, OnlyCompass));
    }

    [Test]
    public void CompassHidesOnceArrived()
    {
        var arrived = Scene(Looking, 1) with { QuestTargets = [new QuestTarget(1, new Vector2(100, 0))] };

        Check.Equal(0, Drawn(arrived, OnlyCompass));
    }

    [Test]
    public void GroundShapeDoesNotChangeTheMinimap()
    {
        int Count(GroundShape shape) => Drawn(Scene(null, 1), s =>
        {
            OnlyMinimap(s);
            s.GroundShape = shape;
        }, Minimap());

        Check.That(Count(GroundShape.Dot) > 0, "minimap draws");
        Check.Equal(Count(GroundShape.Dot), Count(GroundShape.Streak));
        Check.Equal(Count(GroundShape.Dot), Count(GroundShape.Chevron));
    }
}
