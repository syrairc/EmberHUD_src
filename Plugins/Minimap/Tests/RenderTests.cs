using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using Aember;
using Aember.Testing;

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

    static readonly Sprite Fill = new(new ImTextureRef { TexID = new ImTextureID(9ul) }, Vector2.Zero, Vector2.One);

    static GameState OneCell() => Scene.State(terrain: Scene.Cells(3, 3, false, (1, 1)));

    static int DrawnWithFill(GameState state, Action<Main>? configure = null, Func<string, Sprite?>? resolve = null)
    {
        TestGame.UseTextures((_, _, _, _) => { }, resolve ?? (_ => Fill));
        try
        {
            return Drawn(state, configure: configure);
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void TheRegionFillIsOneQuad() => Check.Equal(Drawn(OneCell()) + 4, DrawnWithFill(OneCell()));

    [Test]
    public void TheFillQuadIsDrawnFirstOverTheWholeGrid()
    {
        TestGame.UseTextures((_, _, _, _) => { }, _ => Fill);
        try
        {
            TestGame.Set(OneCell());
            Map.View = null;
            var main = Scene.Plugin();
            TestFrame.Run(() =>
            {
                main.Tick();
                main.Tick();
                var map = Map.View!.Value;
                var list = Draw.Background;
                var first = list.VtxBuffer.Size;
                main.Render();
                Vector2[] corners = [new(0, 0), new(30, 0), new(30, 30), new(0, 30)];
                Vector2[] uvs = [Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY];
                for (var i = 0; i < 4; i++)
                {
                    Check.Equal(map.ToScreen(corners[i]), list.VtxBuffer[first + i].Pos, $"corner {i}");
                    Check.Equal(uvs[i], list.VtxBuffer[first + i].Uv, $"uv {i}");
                }
            });
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void AFillTextureThatNeverArrivedSkipsTheFillButKeepsTheOutlines()
    {
        var outlines = DrawnWithFill(OneCell(), resolve: _ => null);

        Check.That(outlines > 0, "the outlines draw");
        Check.Equal(Drawn(OneCell()), outlines);
    }

    [Test]
    public void TheWallsStyleDrawsTheOldWallLinesAndNoRegion()
    {
        void WallsStyle(Main main) { main.Settings.View.DrawStyle.Value = "Walls"; main.Settings.View.OffsetY.Value = 0; }
        var perLine = 0;
        TestFrame.Run(() =>
        {
            var before = Draw.Background.VtxBuffer.Size;
            Draw.Line(new Vector2(10, 10), new Vector2(100, 10), Color.White, new Settings().View.WallThickness.Value);
            perLine = Draw.Background.VtxBuffer.Size - before;
        });

        Check.Equal(4 * perLine, DrawnWithFill(OneCell(), WallsStyle) - DrawnWithFill(Scene.State(), WallsStyle));
    }

    [Test]
    public void ADrawStyleItDoesNotKnowDrawsTheRegion() =>
        Check.Equal(DrawnWithFill(OneCell()), DrawnWithFill(OneCell(), main => main.Settings.View.DrawStyle.Value = "region"));

    static Player Facing(Vector2 forward) => new(new Vector3(15, 15, 0), forward);

    static int Drawn(GameState state, Action<Main>? beforeRender = null, Action<Main>? configure = null)
    {
        TestGame.Set(state);
        Map.View = null;
        var main = Scene.Plugin();
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
        var main = Scene.Plugin();
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
        var main = Scene.Plugin();
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

    static readonly Sprite Ready = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One);

    static readonly MapIcon Portal = new(10, "UI_MiniMap_Portal", new Vector2(15, 15), 1, 0, 0);

    static GameState IconScene(params MapIcon[] icons) => Scene.State(terrain: Scene.Cells(3, 3, false, (1, 1)), icons: icons);

    static int DrawnWith(Func<string, Sprite?>? icons, GameState state, Action<Main>? configure = null)
    {
        TestGame.UseGameIcons(icons);
        try
        {
            return Drawn(state, configure: configure);
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Test]
    public void AReadyGameIconIsOneImageQuadAndAMarkerUntilThen()
    {
        var walls = Drawn(IconScene());

        Check.Equal(walls + 4, DrawnWith(name => name == "UI_MiniMap_Portal" ? Ready : null, IconScene(Portal)));
        Check.That(DrawnWith(_ => null, IconScene(Portal)) > walls + 4, "the fallback marker has more vertices than one quad");
    }

    [Test]
    public void TurningGameIconsOffDrawsTheMarkerEvenWhenTheTextureIsReady()
    {
        var off = DrawnWith(_ => Ready, IconScene(Portal), main => main.Settings.View.UseGameIcons.Value = false);

        Check.Equal(DrawnWith(_ => null, IconScene(Portal)), off);
    }

    [Test]
    public void AnIconWithoutATextureNameDrawsAMarkerNotNothing()
    {
        var walls = Drawn(IconScene());

        Check.That(DrawnWith(_ => Ready, IconScene(Portal with { Texture = "" })) > walls + 4, "empty texture name falls back");
        Check.That(DrawnWith(_ => null, IconScene(Portal with { Type = 987654 })) > walls, "an unknown type still draws a dot");
    }

    [Test]
    public void ANonFinitePositionIsSkippedAndABadSizeDrawsLikeOne()
    {
        var walls = Drawn(IconScene());
        var ready = (Func<string, Sprite?>)(_ => Ready);

        Check.Equal(walls, DrawnWith(ready, IconScene(Portal with { Position = new Vector2(float.NaN, 0) })));
        Check.Equal(walls, DrawnWith(ready, IconScene(Portal with { Position = new Vector2(float.PositiveInfinity, 0) })));
        foreach (var size in new[] { 0f, -2f, float.NaN, float.PositiveInfinity }) Check.Equal(walls + 4, DrawnWith(ready, IconScene(Portal with { Size = size })), $"size {size}");
    }

    [Test]
    public void AnEntityTheGameAlreadyIconisedIsNotDrawnTwice()
    {
        var monster = Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare);
        var terrain = Scene.Cells(3, 3, false, (1, 1));
        var iconOnly = Drawn(Scene.State(terrain: terrain, icons: [Portal with { EntityId = monster.Id }]));

        Check.Equal(iconOnly, Drawn(Scene.State(entities: [monster], terrain: terrain, icons: [Portal with { EntityId = monster.Id }])));
        Check.That(Drawn(Scene.State(entities: [monster], terrain: terrain, icons: [Portal with { EntityId = 0 }])) > Drawn(Scene.State(terrain: terrain, icons: [Portal with { EntityId = 0 }])), "an icon with EntityId 0 never hides an entity");
    }

    [Test]
    public void ThePlayerIsTheGameArrowRotatedByTheForwardWhenItIsReady()
    {
        var state = Scene.State(player: Facing(Vector2.UnitX));

        Check.Equal(4, DrawnWith(name => name == "UI_MiniMap_Player1" ? Ready : null, state));
        Check.That(DrawnWith(_ => null, state) != 4, "the triangle is the fallback");
        Check.That(DrawnWith(name => name == "UI_MiniMap_Player1" ? Ready : null, Scene.State(player: Facing(Vector2.Zero))) != 0, "a player with no heading still draws a marker");
    }

    static Vector2 TopEdgeOffsetFromPlayer(Vector2 forward)
    {
        var sprite = new Sprite(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One);
        TestGame.UseGameIcons(name => name == "UI_MiniMap_Player1" ? sprite : null);
        try
        {
            TestGame.Set(Scene.State(player: Facing(forward)));
            Map.View = null;
            var main = Scene.Plugin();
            var top = Vector2.Zero;
            TestFrame.Run(() =>
            {
                main.Tick();
                main.Tick();
                var player = Map.View!.Value.ToScreen(new Vector3(15, 15, 0));
                var list = Draw.Background;
                var first = list.VtxBuffer.Size;
                main.Render();
                var count = 0;
                for (var i = first; i < list.VtxBuffer.Size; i++)
                {
                    if (list.VtxBuffer[i].Uv.Y != sprite.Uv0.Y) continue;
                    top += list.VtxBuffer[i].Pos - player;
                    count++;
                }

                Check.Equal(2, count, "two vertices on the texture's top edge");
            });
            return top;
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Test]
    public void TheGameArrowSpriteHasItsTopEdgeOnTheSideThePlayerFaces()
    {
        var right = TopEdgeOffsetFromPlayer(Vector2.UnitX);
        var up = TopEdgeOffsetFromPlayer(new Vector2(0, -1));

        Check.That(right.X > 0 && MathF.Abs(right.Y) < 1, "facing +X puts the texture top on screen right");
        Check.That(up.Y < 0 && MathF.Abs(up.X) < 1, "facing -Y puts the texture top on screen up");
    }

    [Test]
    public void AnEntityWhoseGameIconIsOffscreenKeepsItsMarker()
    {
        var monster = Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare);
        var terrain = Scene.Cells(3, 3, false, (1, 1));
        var withMarker = Drawn(Scene.State(entities: [monster], terrain: terrain));

        Check.That(withMarker > Drawn(Scene.State(terrain: terrain)), "the monster draws");
        Check.Equal(withMarker, Drawn(Scene.State(entities: [monster], terrain: terrain, icons: [Portal with { EntityId = monster.Id, Position = new Vector2(float.NaN, 0) }])));
    }

    [Test]
    public void AnEntityWithAnSvgIconAsksForItWithItsStyleAndDrawsOneQuad()
    {
        var asked = new List<(string Icon, int Pixels, Color Colour, Color Accent)>();
        var terrain = Scene.Cells(3, 3, false, (1, 1));
        var portal = Scene.Entity(EntityKind.Portal, new Vector3(15, 15, 0));
        var walls = Drawn(Scene.State(terrain: terrain));
        TestGame.UseSvgIcons((icon, pixels, colour, accent) =>
        {
            asked.Add((icon, pixels, colour, accent));
            return Ready;
        });
        try
        {
            var drawn = Drawn(Scene.State(entities: [portal], terrain: terrain), configure: main =>
            {
                main.Settings.Markers.Portal.Radius.Value = 6;
                main.Settings.Markers.Portal.Colour.Value = Color.DeepSkyBlue;
                main.Settings.Markers.Portal.Accent.Value = Color.White;
            });

            Check.Equal(walls + 4, drawn);
            Check.Equal("svg:portal", asked[^1].Icon);
            Check.Equal(12, asked[^1].Pixels);
            Check.Equal(Color.DeepSkyBlue.ToArgb(), asked[^1].Colour.ToArgb());
            Check.Equal(Color.White.ToArgb(), asked[^1].Accent.ToArgb());
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }

    [Test]
    public void AnSvgMarkerIsADotUntilItsIconIsReadyAndPlainMarkersNeverAsk()
    {
        var terrain = Scene.Cells(3, 3, false, (1, 1));
        var walls = Drawn(Scene.State(terrain: terrain));
        var asked = 0;
        TestGame.UseSvgIcons((_, _, _, _) =>
        {
            asked++;
            return null;
        });
        try
        {
            Check.That(Drawn(Scene.State(entities: [Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare)], terrain: terrain)) > walls + 4, "the dot has more vertices than a quad");
            var askedForElite = asked;
            Drawn(Scene.State(entities: [Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Normal), Scene.Entity(EntityKind.Npc, new Vector3(15, 15, 0))], terrain: terrain));
            Check.Equal(askedForElite, asked);
            Check.That(askedForElite > 0, "the rare monster asked for svg:elite");
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }
}
