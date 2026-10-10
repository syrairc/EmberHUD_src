using System;
using System.Numerics;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class RenderTests
{
    static readonly Vector2 Screen = new(1920, 1080);

    static GameState Busy() => Scene.State(
        player: new Player(new Vector3(15, 15, 0), Vector2.UnitX),
        entities:
        [
            Scene.Entity(EntityKind.Monster, new Vector3(12, 12, 0), Rarity.Boss),
            Scene.Entity(EntityKind.Portal, new Vector3(18, 12, 0)),
            Scene.Entity(EntityKind.Monster, new Vector3(float.NaN, 12, 0)),
        ],
        terrain: Scene.Cells(3, 3, false, (1, 1)));

    static int Drawn(Action draw)
    {
        var drawn = 0;
        TestFrame.Run(() =>
        {
            var before = Draw.Background.VtxBuffer.Size;
            draw();
            drawn = Draw.Background.VtxBuffer.Size - before;
        });
        return drawn;
    }

    [Test]
    public void ASegmentTouchingTheViewportIsVisible()
    {
        Check.That(Main.Visible(new Vector2(-50, 10), new Vector2(50, 10), Screen), "crosses the left edge");
        Check.That(Main.Visible(new Vector2(100, 100), new Vector2(200, 100), Screen), "inside");
    }

    [Test]
    public void ASegmentFullyOutsideTheViewportIsCulled()
    {
        Check.That(!Main.Visible(new Vector2(-50, 10), new Vector2(-10, 10), Screen), "left of the screen");
        Check.That(!Main.Visible(new Vector2(100, 1200), new Vector2(200, 1300), Screen), "below the screen");
    }

    [Test]
    public void TheArrowPointsAlongTheForward()
    {
        var points = Main.ArrowPoints(new Vector2(100, 100), new Vector2(1, 0), 10);

        Check.Equal<Vector2?>(new Vector2(110, 100), points?.Tip);
        Check.Equal<Vector2?>(new Vector2(94, 106), points?.Left);
        Check.Equal<Vector2?>(new Vector2(94, 94), points?.Right);
    }

    [Test]
    public void ForwardOfAnyLengthGivesTheSameArrow() =>
        Check.Equal(Main.ArrowPoints(new Vector2(100, 100), new Vector2(1, 0), 10), Main.ArrowPoints(new Vector2(100, 100), new Vector2(40, 0), 10));

    [Test]
    public void NoForwardGivesNoArrow() =>
        Check.That(Main.ArrowPoints(new Vector2(100, 100), Vector2.Zero, 10) is null);

    [Test]
    public void EverythingOnTheMapDraws()
    {
        TestGame.Set(Busy());
        var main = new Main { WallJob = new(inline: true) };
        var drawn = 0;

        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            var before = Draw.Background.VtxBuffer.Size;
            main.Render();
            drawn = Draw.Background.VtxBuffer.Size - before;
        });

        Check.That(drawn > 0, "walls, markers and the arrow draw");
    }

    [Test]
    public void AHiddenMapDrawsNothing()
    {
        TestGame.Set(Busy());
        var main = new Main { WallJob = new(inline: true) };
        main.Settings.View.Show.Value = false;
        Map.View = null;
        var drawn = -1;

        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            var before = Draw.Background.VtxBuffer.Size;
            main.Render();
            drawn = Draw.Background.VtxBuffer.Size - before;
        });

        Check.Equal(0, drawn);
    }

    [Test]
    public void ANonFiniteEntityIsSkippedWithoutThrowing()
    {
        TestGame.Set(Scene.State(
            player: new Player(new Vector3(float.NaN, 0, 0), Vector2.UnitX),
            entities: [Scene.Entity(EntityKind.Monster, new Vector3(float.PositiveInfinity, 0, 0))]));
        var main = new Main();

        var drawn = Drawn(() =>
        {
            main.Tick();
            main.Render();
        });

        Check.Equal(0, drawn);
    }

    [Test]
    public void AHiddenKindDrawsLessThanAShownOne()
    {
        TestGame.Set(Scene.State(player: new Player(new Vector3(15, 15, 0), Vector2.UnitX), entities: [Scene.Entity(EntityKind.Portal, new Vector3(18, 12, 0))]));
        var shown = new Main();
        var hidden = new Main();
        hidden.Settings.Markers.Portal.Show.Value = false;

        var withMarker = Drawn(() =>
        {
            shown.Tick();
            shown.Render();
        });
        var without = Drawn(() =>
        {
            hidden.Tick();
            hidden.Render();
        });

        Check.That(withMarker > without, "the portal marker adds vertices");
    }
}
