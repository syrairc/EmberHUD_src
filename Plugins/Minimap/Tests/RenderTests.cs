using System;
using System.Numerics;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class RenderTests
{
    static readonly Vector2 Screen = new(1920, 1080);

    static GameState Busy(Player? player = null, bool entities = true, bool terrain = true) => Scene.State(
        player: player,
        entities: entities
            ?
            [
                Scene.Entity(EntityKind.Monster, new Vector3(12, 12, 0), Rarity.Boss),
                Scene.Entity(EntityKind.Portal, new Vector3(18, 12, 0)),
            ]
            : [],
        terrain: terrain ? Scene.Cells(3, 3, false, (1, 1)) : null);

    static Player Facing(Vector2 forward) => new(new Vector3(15, 15, 0), forward);

    static int Drawn(GameState state, Action<Main>? beforeRender = null, Action<Main>? configure = null)
    {
        TestGame.Set(state);
        Map.View = null;
        var main = new Main { WallJob = new(inline: true) };
        configure?.Invoke(main);
        var drawn = 0;
        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            beforeRender?.Invoke(main);
            var before = Draw.Background.VtxBuffer.Size;
            main.Render();
            drawn = Draw.Background.VtxBuffer.Size - before;
        });
        return drawn;
    }

    static Vector2 FarthestFromPlayer(Vector2 forward, Action<Main>? configure = null)
    {
        TestGame.Set(Busy(Facing(forward), entities: false, terrain: false));
        Map.View = null;
        var main = new Main { WallJob = new(inline: true) };
        configure?.Invoke(main);
        var tip = Vector2.Zero;
        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            var player = Map.View!.Value.ToScreen(new Vector3(15, 15, 0));
            var list = Draw.Background;
            var first = list.VtxBuffer.Size;
            main.Render();
            var best = -1f;
            for (var i = first; i < list.VtxBuffer.Size; i++)
            {
                var offset = list.VtxBuffer[i].Pos - player;
                if (offset.LengthSquared() <= best) continue;
                best = offset.LengthSquared();
                tip = offset;
            }
        });
        return tip;
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
    public void WallsMarkersAndTheArrowAllDraw()
    {
        var full = Drawn(Busy(Facing(Vector2.UnitX)));

        Check.That(full > Drawn(Busy(Facing(Vector2.UnitX), entities: false)), "markers draw");
        Check.That(full > Drawn(Busy(Facing(Vector2.UnitX), terrain: false)), "walls draw");
        Check.That(full > Drawn(Busy(null)), "the arrow draws");
    }

    [Test]
    public void AMapHiddenAfterItWasPublishedDrawsNothing() =>
        Check.Equal(0, Drawn(Busy(Facing(Vector2.UnitX)), beforeRender: main => main.Settings.View.Show.Value = false));

    [Test]
    public void AMapWithoutAViewDrawsNothing() =>
        Check.Equal(0, Drawn(Busy(Facing(Vector2.UnitX)), beforeRender: _ => Map.View = null));

    [Test]
    public void NonFiniteEntitiesAreSkipped()
    {
        var player = Facing(Vector2.UnitX);
        var scene = Scene.State(player: player, entities:
        [
            Scene.Entity(EntityKind.Monster, new Vector3(float.NaN, 12, 0)),
            Scene.Entity(EntityKind.Monster, new Vector3(float.PositiveInfinity, 0, 0)),
        ]);

        var baseline = Drawn(Scene.State(player: player));

        Check.That(baseline > 0, "the baseline draws something");
        Check.Equal(baseline, Drawn(scene));
    }

    [Test]
    public void ANonFinitePlayerDrawsNothing() =>
        Check.Equal(0, Drawn(Scene.State(player: new Player(new Vector3(float.NaN, 0, 0), Vector2.UnitX), entities: [Scene.Entity(EntityKind.Monster, new Vector3(float.PositiveInfinity, 0, 0))])));

    [Test]
    public void AHiddenKindDrawsLessThanAShownOne()
    {
        var scene = Scene.State(player: Facing(Vector2.UnitX), entities: [Scene.Entity(EntityKind.Portal, new Vector3(18, 12, 0))]);

        var withMarker = Drawn(scene);
        var without = Drawn(scene, configure: main => main.Settings.Markers.Portal.Show.Value = false);

        Check.That(withMarker > without, "the portal marker adds vertices");
    }

    [Test]
    public void TheArrowPointsDownTheScreenForPositiveY()
    {
        var tip = FarthestFromPlayer(new Vector2(0, 1));

        Check.That(tip.Y > 0 && MathF.Abs(tip.X) < 1, "logic +Y is screen down");
    }

    [Test]
    public void TheArrowPointsRightForPositiveX()
    {
        var tip = FarthestFromPlayer(Vector2.UnitX);

        Check.That(tip.X > 0 && MathF.Abs(tip.Y) < 1, "logic +X is screen right");
    }

    [Test]
    public void TheArrowTurnsWithTheMapRotation()
    {
        var tip = FarthestFromPlayer(new Vector2(0, -1), main => main.Settings.View.Rotation.Value = 90);

        Check.That(tip.X > 0 && MathF.Abs(tip.Y) < 1, "forward -Y turns right with a 90 degree map rotation");
    }

    [Test]
    public void TheArrowTurnsWithTheGameRotationToo()
    {
        TestGame.Set(Scene.State(player: Facing(new Vector2(0, -1)), rotation: 90));
        Map.View = null;
        var main = new Main { WallJob = new(inline: true) };
        var tip = Vector2.Zero;
        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            var player = Map.View!.Value.ToScreen(new Vector3(15, 15, 0));
            var list = Draw.Background;
            var first = list.VtxBuffer.Size;
            main.Render();
            var best = -1f;
            for (var i = first; i < list.VtxBuffer.Size; i++)
            {
                var offset = list.VtxBuffer[i].Pos - player;
                if (offset.LengthSquared() <= best) continue;
                best = offset.LengthSquared();
                tip = offset;
            }
        });

        Check.That(tip.X > 0 && MathF.Abs(tip.Y) < 1, "forward -Y turns right with a 90 degree game rotation");
    }

    [Test]
    public void TheArrowFollowsTheVerticalScaleToo()
    {
        var plain = FarthestFromPlayer(new Vector2(1, 1));
        var squashed = FarthestFromPlayer(new Vector2(1, 1), main => main.Settings.View.VerticalScale.Value = 0.3f);

        Check.That(MathF.Abs(squashed.Y) / MathF.Abs(squashed.X) < MathF.Abs(plain.Y) / MathF.Abs(plain.X), "arrow flattens with the map");
    }

    [Test]
    public void ANonFiniteForwardGivesNoArrow()
    {
        Check.That(Main.ArrowPoints(new Vector2(100, 100), new Vector2(float.NaN, 1), 10) is null, "NaN");
        Check.That(Main.ArrowPoints(new Vector2(100, 100), new Vector2(float.PositiveInfinity, 0), 10) is null, "infinity");
    }
}
