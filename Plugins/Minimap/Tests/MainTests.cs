using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class MainTests
{
    static readonly Vector2 Screen = new(1920, 1080);
    static readonly Walls Tall = Walls.Empty with { Min = new(-10, -20), Max = new(10, 20) };

    readonly List<string> _uploads = [];
    readonly List<string> _released = [];
    readonly Terrain _cell = Scene.Cells(3, 3, false, (1, 1));
    long _now;

    Main RegionPlugin()
    {
        TestGame.UseTextures((key, _, _, _) => _uploads.Add(key), null, _released.Add);
        var main = Scene.Plugin();
        main.Clock = () => _now;
        return main;
    }

    void TickTwiceAt(Main main, long at, GameState state)
    {
        _now = at;
        TestGame.Set(state);
        TickTwice(main);
    }

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
        Check.Equal<float?>(0.06f, view?.Scale);
    }

    [Test]
    public void RotationAndVerticalScaleGoIntoTheFollowingView()
    {
        var settings = new Settings();
        settings.View.Rotation.Value = 90;
        settings.View.VerticalScale.Value = 0.5f;

        var view = Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen);

        Check.Equal<float?>(90f, view?.RotationDegrees);
        Check.Equal<float?>(0.5f, view?.VerticalScale);
        Check.Equal<Vector2?>(new Vector2(960, 540), view?.ToScreen(new Vector3(4, 7, 0)));
        var right = view?.ToScreen(new Vector3(5, 7, 0)) - view?.ToScreen(new Vector3(4, 7, 0));
        Check.That(right is { } d && MathF.Abs(d.X) < 0.001f && d.Y > 0, "world +X turns to screen down after a 90 degree clockwise map rotation");
    }

    [Test]
    public void RotationAndVerticalScaleGoIntoTheFittedView()
    {
        var settings = new Settings();
        settings.View.FollowPlayer.Value = false;
        settings.View.Rotation.Value = 90;

        var view = Main.ViewFor(settings, null, Tall, new Vector2(1000, 500));

        Check.Equal<float?>(90f, view?.RotationDegrees);
        Check.That(view?.Scale > 0 && view?.Scale != 10.5f, "fits the rotated bounds");
    }

    [Test]
    public void TheGameMinimapRotationIsUsedForTheView()
    {
        var view = Main.ViewFor(new Settings(), new Vector3(4, 7, 0), Walls.Empty, Screen, -90);

        Check.Equal<float?>(-90f, view?.RotationDegrees);
    }

    [Test]
    public void TheRotationSettingIsAnExtraOffsetOnTopOfTheGameRotation()
    {
        var settings = new Settings();
        settings.View.Rotation.Value = 15;

        Check.Equal<float?>(-75f, Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen, -90)?.RotationDegrees);
    }

    [Test]
    public void AnUnknownGameRotationCountsAsZero()
    {
        var settings = new Settings();
        settings.View.Rotation.Value = 20;

        Check.Equal<float?>(20f, Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen, null)?.RotationDegrees);
    }

    [Test]
    public void TheFittedViewAddsTheGameRotationToo()
    {
        var settings = new Settings();
        settings.View.FollowPlayer.Value = false;
        settings.View.Rotation.Value = 30;

        Check.Equal<float?>(120f, Main.ViewFor(settings, null, Tall, new Vector2(1000, 500), 90)?.RotationDegrees);
    }

    [Test]
    public void TickPublishesAViewRotatedLikeTheGameState()
    {
        TestGame.Set(Scene.State(player: new Player(new Vector3(15, 15, 0), Vector2.UnitX), terrain: Scene.Cells(3, 3, false, (1, 1)), rotation: -90));
        var main = Scene.Plugin();
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.Equal<float?>(-90f, Map.View?.RotationDegrees);
    }

    [Test]
    public void ZoomRangeGoesFromAThousandthToFour()
    {
        var zoom = new Settings().View.Zoom;

        Check.Equal(0.002f, zoom.Min);
        Check.Equal(4f, zoom.Max);
        Check.Equal(0.06f, zoom.Value);
    }

    [Test]
    public void RotationAndVerticalScaleRangesAndDefaults()
    {
        var view = new Settings().View;

        Check.Equal(-180f, view.Rotation.Min);
        Check.Equal(180f, view.Rotation.Max);
        Check.Equal(0f, view.Rotation.Value);
        Check.Equal(0.2f, view.VerticalScale.Min);
        Check.Equal(1.5f, view.VerticalScale.Max);
        Check.Equal(1f, view.VerticalScale.Value);
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
    public void FollowingWithoutAPlayerFitsInstead()
    {
        var settings = new Settings();

        Check.Equal<float?>(10.5f, Main.ViewFor(settings, null, Tall, new Vector2(1000, 500))?.Scale);
    }

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
        var main = Scene.Plugin();
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.That(Map.View is not null);
    }

    [Test]
    public void AHiddenMapPublishesNoView()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.View.Show.Value = false;
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.That(Map.View is null);
    }

    [Test]
    public void TheToggleHotkeyFlipsVisibility()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
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
        var main = Scene.Plugin();
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
    public void ZoomOutClampsAtTheNewMinimum()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.View.Zoom.Value = 0.0025f;
        try
        {
            TestInput.Hold();
            TestInput.Hold(Key.PageDown);
            TestFrame.Run(main.Tick);
            Check.Equal(0.002f, main.Settings.View.Zoom.Value);
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
        var main = Scene.Plugin();
        main.Settings.View.FollowPlayer.Value = false;
        Map.View = null;

        TickTwice(main);

        Check.That(Map.View is not null);
    }

    [Test]
    public void WallsFromTheLastAreaAreDroppedWhenTheAreaChanges()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.View.FollowPlayer.Value = false;
        TickTwice(main);

        TestGame.Set(Scene.State(area: 2));
        Map.View = null;
        TestFrame.Run(main.Tick);

        Check.That(Map.View is null);
    }

    [Test]
    public void TheRegionRebuildsAtMostOnceASecondAndOnlyWhenTheTrailGrew()
    {
        try
        {
            var main = RegionPlugin();
            ExploredTrail one = new([new Vector2(15, 15)], 4);
            ExploredTrail two = new([new Vector2(15, 15), new Vector2(16, 15)], 4);

            TickTwiceAt(main, 0, Scene.State(terrain: _cell, explored: one));
            Check.Equal(1, _uploads.Count, "the first build is at once");
            TickTwiceAt(main, 500, Scene.State(terrain: _cell, explored: two));
            Check.Equal(1, _uploads.Count, "grew, but not a second yet");
            TickTwiceAt(main, 1000, Scene.State(terrain: _cell, explored: two));
            Check.Equal(2, _uploads.Count, "a second later");
            TickTwiceAt(main, 2500, Scene.State(terrain: _cell, explored: two));
            Check.Equal(2, _uploads.Count, "nothing grew, no rebuild");
            Check.Equal("region-1", _uploads[^1]);
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void APlayerInANewCellOrANewFillColourRebuildsTheRegion()
    {
        try
        {
            var main = RegionPlugin();
            ExploredTrail trail = new([new Vector2(15, 15)], 4);
            GameState At(float x) => Scene.State(player: new Player(new Vector3(x, 15, 0), Vector2.UnitX), terrain: _cell, explored: trail);

            TickTwiceAt(main, 0, At(15));
            TickTwiceAt(main, 1000, At(18));
            Check.Equal(1, _uploads.Count, "same cell");
            TickTwiceAt(main, 2000, At(25));
            Check.Equal(2, _uploads.Count, "next cell");
            main.Settings.View.ExploredFill.Value = Color.Red;
            TickTwiceAt(main, 3000, At(25));
            Check.Equal(3, _uploads.Count, "new colour");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void ANewAreaUploadsItsOwnTextureAndReleasesTheOldOne()
    {
        try
        {
            var main = RegionPlugin();
            TickTwiceAt(main, 0, Scene.State(terrain: _cell, area: 1));
            TickTwiceAt(main, 10, Scene.State(terrain: Scene.Cells(2, 2, false, (0, 0)), area: 2));

            Check.That(_uploads.SequenceEqual(new[] { "region-1", "region-2" }), string.Join(", ", _uploads));
            Check.That(_released.SequenceEqual(new[] { "region-1" }), string.Join(", ", _released));
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void ABuildThatFinishesAfterTheAreaChangedIsDropped()
    {
        try
        {
            var main = RegionPlugin();
            TestGame.Set(Scene.State(terrain: _cell, area: 1));
            TestFrame.Run(main.Tick);
            TestGame.Set(Scene.State(terrain: Scene.Cells(2, 2, false, (0, 0)), area: 2));
            TickTwice(main);

            Check.That(_uploads.SequenceEqual(new[] { "region-2" }), string.Join(", ", _uploads));
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void TheWallsStyleBuildsNoRegion()
    {
        try
        {
            var main = RegionPlugin();
            main.Settings.View.DrawStyle.Value = "Walls";
            TickTwiceAt(main, 0, Scene.State(terrain: _cell));

            Check.Equal(0, _uploads.Count);
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void AHiddenMapBuildsNoRegionUntilItIsShownAgain()
    {
        try
        {
            var main = RegionPlugin();
            main.Settings.View.Show.Value = false;
            TickTwiceAt(main, 0, Scene.State(terrain: _cell));
            Check.Equal(0, _uploads.Count, "hidden");

            main.Settings.View.Show.Value = true;
            TickTwiceAt(main, 10, Scene.State(terrain: _cell));
            Check.Equal(1, _uploads.Count, "shown");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void ABuildAfterAnAreaChangeStartsFromNoPreviousRegion()
    {
        byte[]? last = null;
        try
        {
            TestGame.UseTextures((_, rgba, _, _) => last = rgba, null);
            var main = Scene.Plugin();
            main.Clock = () => _now;
            var strip = Scene.Cells(4, 1, false, (0, 0), (3, 0));

            TickTwiceAt(main, 0, Scene.State(terrain: strip, area: 1, explored: new([new Vector2(5, 5)], 4)));
            TickTwiceAt(main, 10, Scene.State(terrain: strip, area: 2, explored: new([new Vector2(35, 5)], 4)));

            var view = main.Settings.View;
            Check.Equal(view.RegionFill.Value.A, last![3], "the old area's explored cell is not carried over");
            Check.Equal(view.ExploredFill.Value.A, last[6 * 4 + 3], "the new area's trail is stamped");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }
}
