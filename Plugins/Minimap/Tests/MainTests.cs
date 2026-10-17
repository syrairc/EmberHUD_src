using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Minimap;

public sealed class MainTests
{
    static readonly Vector2 Screen = new(1920, 1080);
    static readonly Walls Tall = Walls.Empty with { Min = new(-10, -20), Max = new(10, 20) };

    readonly List<string> _uploads = [];
    readonly List<string> _released = [];
    readonly Terrain _cell = Scene.OneCell();
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

    static GameState InArea() => Scene.State(player: new Player(new Vector3(15, 15, 0), Vector2.UnitX), terrain: Scene.OneCell());

    static void TickTwice(Main main) => TestFrame.Run(() =>
    {
        main.Tick();
        main.Tick();
    });

    [Test]
    public void FollowingPutsThePlayerAtTheMiddlePlusTheOffset()
    {
        var settings = new Settings();
        settings.General.OffsetX = 10;
        settings.General.OffsetY = -5;

        var view = Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen);

        Check.Equal<Vector2?>(new Vector2(970, 535), view?.ToScreen(new Vector3(4, 7, 0)));
        Check.Equal<float?>(0.037f, view?.Scale);
    }

    [Test]
    public void RotationAndVerticalScaleGoIntoTheFollowingView()
    {
        var settings = new Settings();
        settings.General.Rotation = 90;
        settings.General.VerticalScale = 0.5f;
        settings.General.OffsetY = 0;

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
        settings.General.FollowPlayer = false;
        settings.General.OffsetY = 0;
        settings.General.Rotation = 90;

        var view = Main.ViewFor(settings, null, Tall, new Vector2(1000, 500));

        Check.Equal<float?>(90f, view?.RotationDegrees);
        Check.That(view?.Scale > 0 && view?.Scale != 10.5f, "fits the rotated bounds");
    }

    static Settings GameMode()
    {
        var settings = new Settings();
        settings.General.UseGameMinimap = true;
        return settings;
    }

    static MapView? GameView(Settings settings, GameMinimap? game, Vector2? viewport = null, float? rotation = -30) =>
        Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, viewport ?? Screen, rotation, game);

    [Test]
    public void GameMinimapViewUsesTheGameZoomAndRotation()
    {
        var settings = GameMode();
        settings.General.Rotation = 45;
        settings.General.Zoom = 3;
        settings.General.VerticalScale = 0.5f;
        settings.General.FollowPlayer = false;
        settings.General.OffsetX = 100;
        settings.General.OffsetY = 100;
        settings.General.GameOffsetX = 10;
        settings.General.GameOffsetY = -5;

        var view = GameView(settings, new GameMinimap(true, 3840));

        Check.Equal<float?>(0.5f, view?.Scale);
        Check.Equal<float?>(-30f, view?.RotationDegrees);
        Check.Equal<float?>(1f, view?.VerticalScale);
        Check.Equal<Vector2?>(new Vector2(970, 535), view?.ToScreen(new Vector3(4, 7, 0)));
    }

    [Test]
    public void GameMinimapViewIsNullWhileTheGameMapIsClosedOrMissing()
    {
        Check.That(GameView(GameMode(), new GameMinimap(false, 3840)) is null);
        Check.That(GameView(GameMode(), null) is null);
    }

    [Test]
    public void GameMinimapViewIsNullForBadWidths()
    {
        Check.That(GameView(GameMode(), new GameMinimap(true, 3840), Vector2.Zero) is null);
        Check.That(GameView(GameMode(), new GameMinimap(true, 0)) is null);
        Check.That(GameView(GameMode(), new GameMinimap(true, -5)) is null);
        Check.That(GameView(GameMode(), new GameMinimap(true, float.NaN)) is null);
        Check.That(GameView(GameMode(), new GameMinimap(true, float.PositiveInfinity)) is null);
    }

    [Test]
    public void GameMinimapStateIsIgnoredWhileTheToggleIsOff()
    {
        var view = GameView(new Settings(), new GameMinimap(false, 3840), rotation: null);

        Check.Equal<float?>(0.037f, view?.Scale);
    }

    [Test]
    public void TickShowsTheViewOnlyWhileTheGameMapIsOpen()
    {
        var main = Scene.Plugin();
        main.Settings.General.UseGameMinimap = true;
        var inArea = InArea();
        Map.View = null;

        TestGame.Set(inArea with { GameMinimap = new GameMinimap(false, 3840) });
        TestFrame.Run(main.Tick);
        Check.That(Map.View is null);

        TestGame.Set(inArea with { GameMinimap = new GameMinimap(true, 3840) });
        TestFrame.Run(main.Tick);
        Check.That(Map.View is not null);
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
        settings.General.Rotation = 15;

        Check.Equal<float?>(-75f, Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen, -90)?.RotationDegrees);
    }

    [Test]
    public void AnUnknownGameRotationCountsAsZero()
    {
        var settings = new Settings();
        settings.General.Rotation = 20;

        Check.Equal<float?>(20f, Main.ViewFor(settings, new Vector3(4, 7, 0), Walls.Empty, Screen, null)?.RotationDegrees);
    }

    [Test]
    public void TheFittedViewAddsTheGameRotationToo()
    {
        var settings = new Settings();
        settings.General.FollowPlayer = false;
        settings.General.OffsetY = 0;
        settings.General.Rotation = 30;

        Check.Equal<float?>(120f, Main.ViewFor(settings, null, Tall, new Vector2(1000, 500), 90)?.RotationDegrees);
    }

    [Test]
    public void TickPublishesAViewRotatedLikeTheGameState()
    {
        TestGame.Set(Scene.State(player: new Player(new Vector3(15, 15, 0), Vector2.UnitX), terrain: Scene.OneCell(), rotation: -90));
        var main = Scene.Plugin();
        Map.View = null;

        TestFrame.Run(main.Tick);

        Check.Equal<float?>(-90f, Map.View?.RotationDegrees);
    }

    [Test]
    public void TheDefaultZoomSitsInsideTheZoomRange()
    {
        Check.That(GeneralTab.MinZoom < GeneralTab.DefaultZoom && GeneralTab.DefaultZoom < GeneralTab.MaxZoom, "default inside the range");
        Check.Equal(GeneralTab.DefaultZoom, new Settings().General.Zoom);
    }

    [Test]
    public void NotFollowingFitsTheWalls()
    {
        var settings = new Settings();
        settings.General.FollowPlayer = false;
        settings.General.OffsetY = 0;

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
    public void AFullScreenPanelWithdrawsThePublishedViewAndClosingItRestoresIt()
    {
        var map = new GameUi([new GamePanel("Map", PanelKind.WorldMap, Vector2.Zero, new Vector2(3440, 1440), true)]);
        var bag = new GameUi([new GamePanel("Bag", PanelKind.Inventory, new Vector2(2400, 200), new Vector2(3300, 1200), false)]);
        var main = Scene.Plugin();
        Map.View = null;

        TestGame.Set(InArea() with { Ui = bag });
        TestFrame.Run(main.Tick);
        Check.That(Map.View is not null, "side panel");

        TestGame.Set(InArea() with { Ui = map });
        TestFrame.Run(main.Tick);
        Check.That(Map.View is null, "full screen panel");

        TestGame.Set(InArea());
        TestFrame.Run(main.Tick);
        Check.That(Map.View is not null, "closed");
    }

    [Test]
    public void AHiddenMapPublishesNoView()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.General.Show = false;
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
            TestInput.Dispatch(main);
            Check.That(main.Settings.General.Show, "no key, no toggle");
            TestInput.Hold(Key.F10);
            TestInput.Dispatch(main);
            Check.That(!main.Settings.General.Show, "press hides");
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Test]
    public void RegistersFourHotkeysWithTheOldDefaults()
    {
        var hotkeys = Scene.Plugin().Hotkeys;
        Check.Equal("Toggle map,Zoom in,Zoom out,Re-check rotation", string.Join(",", hotkeys.Select(binding => binding.Id)));
        Check.Equal("F10,PageUp,PageDown,F9", string.Join(",", hotkeys.Select(binding => binding.Default.Key)));
    }

    [Test]
    public void ZoomHotkeysScaleAndClamp()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.General.Zoom = GeneralTab.MaxZoom;
        try
        {
            TestInput.Hold();
            TestInput.Hold(Key.PageUp);
            TestInput.Dispatch(main);
            Check.Equal(GeneralTab.MaxZoom, main.Settings.General.Zoom);
            TestInput.Hold();
            TestInput.Hold(Key.PageDown);
            TestInput.Dispatch(main);
            Check.Equal(GeneralTab.MaxZoom / 1.25f, main.Settings.General.Zoom);
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
        main.Settings.General.Zoom = 0.0025f;
        try
        {
            TestInput.Hold();
            TestInput.Hold(Key.PageDown);
            TestInput.Dispatch(main);
            Check.Equal(0.002f, main.Settings.General.Zoom);
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
        main.Settings.General.FollowPlayer = false;
        Map.View = null;

        TickTwice(main);

        Check.That(Map.View is not null);
    }

    [Test]
    public void WallsFromTheLastAreaAreDroppedWhenTheAreaChanges()
    {
        TestGame.Set(InArea());
        var main = Scene.Plugin();
        main.Settings.General.FollowPlayer = false;
        TickTwice(main);

        TestGame.Set(Scene.State(area: 2));
        Map.View = null;
        TestFrame.Run(main.Tick);

        Check.That(Map.View is null);
    }

    [Test]
    public void GameModeBuildsTheRegionWithATransparentExploredFillAndRebuildsWhenToggled()
    {
        List<byte[]> built = [];
        try
        {
            TestGame.UseTextures((_, rgba, _, _) => built.Add(rgba.ToArray()), null);
            var main = Scene.Plugin();
            main.Clock = () => _now;
            const int centreAlpha = (1 * 3 + 1) * 4 + 3;

            TickTwiceAt(main, 0, Scene.State(terrain: _cell));
            main.Settings.General.UseGameMinimap = true;
            TickTwiceAt(main, 1000, Scene.State(terrain: _cell));
            main.Settings.General.UseGameMinimap = false;
            TickTwiceAt(main, 2000, Scene.State(terrain: _cell));
            TickTwiceAt(main, 5000, Scene.State(terrain: _cell));

            Check.Equal(3, built.Count, "one build per toggle state, none when nothing changed");
            Check.That(built[0][centreAlpha] > 0, "explored fill shows with the toggle off");
            Check.Equal((byte)0, built[1][centreAlpha], "transparent in game mode");
            Check.That(built[2][centreAlpha] > 0, "and back");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
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
            TickTwiceAt(main, 125, Scene.State(terrain: _cell, explored: two));
            Check.Equal(1, _uploads.Count, "grew, but not a second yet");
            TickTwiceAt(main, 999, Scene.State(terrain: _cell, explored: two));
            Check.Equal(1, _uploads.Count, "still inside the second");
            TickTwiceAt(main, 1000, Scene.State(terrain: _cell, explored: two));
            Check.Equal(2, _uploads.Count, "a second later");
            TickTwiceAt(main, 5000, Scene.State(terrain: _cell, explored: two));
            Check.Equal(2, _uploads.Count, "nothing grew, no rebuild");
            Check.That(_uploads.All(key => key == "region-1"), "every rebuild reuses the one texture key so the host can refresh it in place");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void TheRegionPixelsAlternateBetweenTwoBuffers()
    {
        List<byte[]> built = [];
        try
        {
            TestGame.UseTextures((_, rgba, _, _) => built.Add(rgba), null);
            var main = Scene.Plugin();
            main.Clock = () => _now;

            for (var i = 0; i < 4; i++)
                TickTwiceAt(main, i * 1000, Scene.State(terrain: _cell, explored: new([.. Enumerable.Repeat(new Vector2(15, 15), i + 1)], 4)));

            Check.Equal(4, built.Count);
            Check.That(!ReferenceEquals(built[0], built[1]), "two different buffers");
            Check.That(ReferenceEquals(built[0], built[2]) && ReferenceEquals(built[1], built[3]), "and they take turns");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void ANewAreaDoesNotCarryTheOldAreasBufferForward()
    {
        List<byte[]> built = [];
        try
        {
            TestGame.UseTextures((_, rgba, _, _) => built.Add(rgba), null);
            var main = Scene.Plugin();
            main.Clock = () => _now;

            TickTwiceAt(main, 0, Scene.State(terrain: _cell, area: 1));
            TickTwiceAt(main, 1000, Scene.State(terrain: _cell, area: 1, explored: new([new Vector2(15, 15)], 4)));
            TickTwiceAt(main, 2000, Scene.State(terrain: _cell, area: 2));
            TickTwiceAt(main, 3000, Scene.State(terrain: _cell, area: 2, explored: new([new Vector2(15, 15)], 4)));

            Check.Equal(4, built.Count);
            Check.That(!ReferenceEquals(built[1], built[3]), "the last area-1 buffer is not recycled into area 2");
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
            main.Settings.Map.Region.ExploredFill = Color.Red;
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
            TickTwiceAt(main, 10, Scene.State(terrain: Scene.Cells(2, 2, (0, 0)), area: 2));

            Check.That(_uploads.SequenceEqual(new[] { "region-1", "region-2" }), string.Join(", ", _uploads));
            Check.That(_released.SequenceEqual(new[] { "region-1" }), string.Join(", ", _released));
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    static Terrain Broken() => new(2, 2, 10f, Vector2.Zero, null!);

    [Test]
    public void AFaultingBuildDoesNotThrowOutOfTickAndIsNotRestarted()
    {
        try
        {
            var main = RegionPlugin();

            TickTwiceAt(main, 0, Scene.State(terrain: Broken(), area: 1));

            Check.Equal(0, _uploads.Count, "nothing to upload for a broken grid");
            Check.That(!main.WallJob.Running && !main.RegionJob.Running, "the same broken terrain is not built again");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void ABrokenTerrainDoesNotBlockTheNextArea()
    {
        try
        {
            var main = RegionPlugin();
            TickTwiceAt(main, 0, Scene.State(terrain: Broken(), area: 1));

            TickTwiceAt(main, 10, Scene.State(terrain: _cell, area: 2));

            Check.That(_uploads.SequenceEqual(new[] { "region-2" }), string.Join(", ", _uploads));
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
            TestGame.Set(Scene.State(terrain: Scene.Cells(2, 2, (0, 0)), area: 2));
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
            main.Settings.Map.DrawStyle = MapStyle.Walls;
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
            main.Settings.General.Show = false;
            TickTwiceAt(main, 0, Scene.State(terrain: _cell));
            Check.Equal(0, _uploads.Count, "hidden");

            main.Settings.General.Show = true;
            TickTwiceAt(main, 10, Scene.State(terrain: _cell));
            Check.Equal(1, _uploads.Count, "shown");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void HidesOnlyBehindFullScreenPanelsWhenAsked()
    {
        var settings = new Settings();
        var bag = new GameUi([new GamePanel("Bag", PanelKind.Inventory, new Vector2(2400, 200), new Vector2(3300, 1200), false)]);
        var map = new GameUi([new GamePanel("Map", PanelKind.WorldMap, Vector2.Zero, new Vector2(3440, 1440), true)]);

        Check.That(!Main.Hidden(settings, GameUi.Empty), "nothing open");
        Check.That(!Main.Hidden(settings, bag), "side panel");
        Check.That(Main.Hidden(settings, map), "full screen panel");
        settings.General.HideBehindPanels = false;
        Check.That(!Main.Hidden(settings, map), "toggle off");
        settings.General.Show = false;
        Check.That(Main.Hidden(settings, GameUi.Empty), "map off");
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
            var strip = Scene.Cells(4, 1, (0, 0), (3, 0));

            TickTwiceAt(main, 0, Scene.State(terrain: strip, area: 1, explored: new([new Vector2(5, 5)], 4)));
            TickTwiceAt(main, 10, Scene.State(terrain: strip, area: 2, explored: new([new Vector2(35, 5)], 4)));

            var view = main.Settings.Map.Region;
            Check.Equal(view.Fill.A, last![3], "the old area's explored cell is not carried over");
            Check.Equal(view.ExploredFill.A, last[3 * 4 + 3], "the new area's trail is stamped");
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void FallbackShapesFollowTheIconTypeGroup()
    {
        foreach (var type in new[] { 2, 1040, 1050, 1060 }) Check.Equal(MarkerShape.Star, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 10, 11, 12, 14, 32, 33, 1000, 1022 }) Check.Equal(MarkerShape.Diamond, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 7, 8, 9, 1010, 2001, 2002 }) Check.Equal(MarkerShape.Hexagon, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 3, 1006, 1007, 1009, 1011 }) Check.Equal(MarkerShape.Square, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 15, 20, 24, 29 }) Check.Equal(MarkerShape.Triangle, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 1003, 1008, 1023, 1024 }) Check.Equal(MarkerShape.Sparkle, Main.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 0, 1, 25, 1061, 99999, -5 }) Check.Equal(MarkerShape.Dot, Main.FallbackShape(type), $"type {type}");
    }
}
