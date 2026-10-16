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

    static int Drawn(GameState state, Action<Settings>? configure = null, MapView? view = null)
    {
        TestGame.Set(state);
        Map.View = view;
        var main = new Main { Seconds = () => 1 };
        main.Paths.Runner = job => job();
        main.Paths.Finder = (_, from, to) => [from, to];
        main.Settings.Legend.Show = false;
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

    [Test]
    public void HiddenBehindFullScreenPanel()
    {
        var full = new GameUi([new GamePanel("Map", PanelKind.WorldMap, default, new Vector2(100, 100), true)]);

        Check.Equal(0, Drawn(Scene(Looking, 1, full), view: Minimap()));
        Check.That(Drawn(Scene(Looking, 1), view: Minimap()) > 0, "draws without the panel");
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

    static void OnlyLabels(Settings settings)
    {
        settings.General.Minimap = false;
        settings.Labels.Show = true;
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
    public void LabelsDrawOnlyWhenShown()
    {
        Check.That(Drawn(Scene(Looking, 1), s => OnlyLabels(s)) > Drawn(Scene(Looking, 1), s =>
        {
            OnlyLabels(s);
            s.Labels.Show = false;
        }), "labels add vertices");
    }

    [Test]
    public void ShowOffDrawsNothing() => Check.Equal(0, Drawn(Scene(Looking, 1), s => s.General.Show = false, Minimap()));
}
