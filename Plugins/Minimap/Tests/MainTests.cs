using System.Numerics;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class MainTests
{
    static readonly Vector2 Screen = new(1920, 1080);
    static readonly Walls Tall = Walls.Empty with { Min = new(-10, -20), Max = new(10, 20) };

    static GameState InArea(int area = 1) => Scene.State(player: new Player(new Vector3(15, 15, 0), Vector2.UnitX), terrain: Scene.Cells(3, 3, false, (1, 1)), area: area);

    static void TickTwice(Main main) => TestFrame.Run(() =>
    {
        main.Tick();
        main.Tick();
    });

    [Test]
    public void FollowingPutsThePlayerAtTheMiddlePlusTheOffset()
    {
        var settings = new Settings();
        settings.View.OffsetX.Value = 10;
        settings.View.OffsetY.Value = -5;

        var view = Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen);

        Check.Equal<Vector2?>(new Vector2(970, 535), view?.ToScreen(new Vector3(4, 7, 0)));
        Check.Equal<float?>(0.5f, view?.Scale);
    }

    [Test]
    public void NotFollowingFitsTheWalls()
    {
        var settings = new Settings();
        settings.View.FollowPlayer.Value = false;

        var view = Main.ViewFor(settings, new Vector3(4, 7, 0), Tall, new Vector2(1000, 500));

        Check.Equal<float?>(10.5f, view?.Scale);
        Check.Equal<Vector2?>(new Vector2(500, 40), view?.ToScreen(new Vector2(0, -20)));
    }

    [Test]
    public void FollowingWithoutAPlayerFitsInstead() =>
        Check.Equal<float?>(10.5f, Main.ViewFor(new Settings(), null, Tall, new Vector2(1000, 500))?.Scale);

    [Test]
    public void NoPlayerAndNoWallsGiveNoView() =>
        Check.That(Main.ViewFor(new Settings(), null, Walls.Empty, Screen) is null);

    [Test]
    public void ANonFinitePlayerGivesNoView() =>
        Check.That(Main.ViewFor(new Settings(), new Vector3(float.NaN, 0, 0), Walls.Empty, Screen) is null);

    [Test]
    public void TickPublishesTheViewForOtherPlugins()
    {
        TestGame.Set(InArea());
        var main = new Main();
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.That(Map.View is not null);
    }

    [Test]
    public void AHiddenMapPublishesNoView()
    {
        TestGame.Set(InArea());
        var main = new Main();
        main.Settings.View.Show.Value = false;
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.That(Map.View is null);
    }

    [Test]
    public void TheToggleHotkeyFlipsVisibility()
    {
        TestGame.Set(InArea());
        var main = new Main();
        try
        {
            TestInput.Hold();
            TestFrame.Run(main.Tick);
            Check.That(main.Settings.View.Show.Value, "no key, no toggle");
            TestInput.Hold(Key.F10);
            TestFrame.Run(main.Tick);
            Check.That(!main.Settings.View.Show.Value, "press hides");
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Test]
    public void ZoomHotkeysScaleAndClamp()
    {
        TestGame.Set(InArea());
        var main = new Main();
        main.Settings.View.Zoom.Value = main.Settings.View.Zoom.Max;
        try
        {
            TestInput.Hold();
            TestInput.Hold(Key.PageUp);
            TestFrame.Run(main.Tick);
            Check.Equal(main.Settings.View.Zoom.Max, main.Settings.View.Zoom.Value);
            TestInput.Hold();
            TestInput.Hold(Key.PageDown);
            TestFrame.Run(main.Tick);
            Check.Equal(main.Settings.View.Zoom.Max / 1.25f, main.Settings.View.Zoom.Value);
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Test]
    public void NotFollowingTheViewFitsTheWallsTheJobBuilt()
    {
        TestGame.Set(InArea());
        var main = new Main { WallJob = new(inline: true) };
        main.Settings.View.FollowPlayer.Value = false;
        Map.View = null;

        TickTwice(main);

        Check.That(Map.View is not null);
    }

    [Test]
    public void WallsFromTheLastAreaAreDroppedWhenTheAreaChanges()
    {
        TestGame.Set(InArea());
        var main = new Main { WallJob = new(inline: true) };
        main.Settings.View.FollowPlayer.Value = false;
        TickTwice(main);

        TestGame.Set(Scene.State(area: 2));
        Map.View = null;
        TestFrame.Run(main.Tick);

        Check.That(Map.View is null);
    }
}
