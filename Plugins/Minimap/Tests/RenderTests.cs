using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;
using EmberHUD.Testing;

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
        terrain: terrain ? Scene.OneCell() : null);

    static readonly Sprite Fill = new(new ImTextureRef { TexID = new ImTextureID(9ul) }, Vector2.Zero, Vector2.One);

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
    public void TheRegionFillIsOneQuad() => Check.Equal(Drawn(OneCellScene()) + 4, DrawnWithFill(OneCellScene()));

    [Test]
    public void TheFillQuadIsDrawnFirstOverTheWholeGrid()
    {
        TestGame.UseTextures((_, _, _, _) => { }, _ => Fill);
        try
        {
            TestGame.Set(OneCellScene());
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
        var outlines = DrawnWithFill(OneCellScene(), resolve: _ => null);

        Check.That(outlines > 0, "the outlines draw");
        Check.Equal(Drawn(OneCellScene()), outlines);
    }

    [Test]
    public void TheWallsStyleDrawsTheOldWallLinesAndNoRegion()
    {
        void WallsStyle(Main main) { main.Settings.Map.DrawStyle = MapStyle.Walls; main.Settings.General.OffsetY = 0; }
        var perLine = 0;
        TestFrame.Run(() =>
        {
            var before = Draw.Background.VtxBuffer.Size;
            Draw.Line(new Vector2(10, 10), new Vector2(100, 10), Color.White, new Settings().Map.Walls.Thickness);
            perLine = Draw.Background.VtxBuffer.Size - before;
        });

        Check.Equal(4 * perLine, DrawnWithFill(OneCellScene(), WallsStyle) - DrawnWithFill(Scene.State(), WallsStyle));
    }

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

    static Vector2 FarthestFromPlayer(Vector2 forward, Action<Main>? configure = null, float? gameRotation = null)
    {
        TestGame.Set(Scene.State(player: Facing(forward), rotation: gameRotation));
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
        Action<Main> fixedSize = main => main.Settings.Icons.ScaleWithZoom = false;
        var full = Drawn(Busy(Facing(Vector2.UnitX)), configure: fixedSize);

        Check.That(full > Drawn(Busy(Facing(Vector2.UnitX), entities: false), configure: fixedSize), "markers draw");
        Check.That(full > Drawn(Busy(Facing(Vector2.UnitX), terrain: false), configure: fixedSize), "walls draw");
        Check.That(full > Drawn(Busy(null), configure: fixedSize), "the arrow draws");
    }

    [Test]
    public void AMapHiddenAfterItWasPublishedDrawsNothing() =>
        Check.Equal(0, Drawn(Busy(Facing(Vector2.UnitX)), beforeRender: main => main.Settings.General.Show = false));

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
        var scene = Scene.State(player: Facing(Vector2.UnitX), entities: [Scene.Entity(EntityKind.Monster, new Vector3(18, 12, 0), Rarity.Rare)]);

        var withMarker = Drawn(scene);
        var without = Drawn(scene, configure: main => main.Settings.Icons.RareMonsters.Show = false);

        Check.That(withMarker > without, "the rare marker adds vertices");
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
        var tip = FarthestFromPlayer(new Vector2(0, -1), main => main.Settings.General.Rotation = 90);

        Check.That(tip.X > 0 && MathF.Abs(tip.Y) < 1, "forward -Y turns right with a 90 degree map rotation");
    }

    [Test]
    public void TheArrowTurnsWithTheGameRotationToo()
    {
        var tip = FarthestFromPlayer(new Vector2(0, -1), gameRotation: 90);

        Check.That(tip.X > 0 && MathF.Abs(tip.Y) < 1, "forward -Y turns right with a 90 degree game rotation");
    }

    [Test]
    public void TheArrowFollowsTheVerticalScaleToo()
    {
        var plain = FarthestFromPlayer(new Vector2(1, 1));
        var squashed = FarthestFromPlayer(new Vector2(1, 1), main => main.Settings.General.VerticalScale = 0.3f);

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

    static GameState OneCellScene(params MapIcon[] icons) => Scene.State(terrain: Scene.OneCell(), icons: icons);

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
        var walls = Drawn(OneCellScene());

        Check.Equal(walls + 4, DrawnWith(name => name == "UI_MiniMap_Portal" ? Ready : null, OneCellScene(Portal)));
        Check.That(DrawnWith(_ => null, OneCellScene(Portal)) > walls + 4, "the fallback marker has more vertices than one quad");
    }

    [Test]
    public void TurningGameIconsOffDrawsTheMarkerEvenWhenTheTextureIsReady()
    {
        var off = DrawnWith(_ => Ready, OneCellScene(Portal), main => main.Settings.Icons.UseGameIcons = false);

        Check.Equal(DrawnWith(_ => null, OneCellScene(Portal)), off);
    }

    [Test]
    public void AnIconWithoutATextureNameDrawsAMarkerNotNothing()
    {
        var walls = Drawn(OneCellScene());

        Check.That(DrawnWith(_ => Ready, OneCellScene(Portal with { Texture = "" })) > walls + 4, "empty texture name falls back");
        Check.That(DrawnWith(_ => null, OneCellScene(Portal with { Type = 987654 })) > walls, "an unknown type still draws a dot");
    }

    [Test]
    public void ANonFinitePositionIsSkippedAndABadSizeDrawsLikeOne()
    {
        var walls = Drawn(OneCellScene());
        var ready = (Func<string, Sprite?>)(_ => Ready);

        Check.Equal(walls, DrawnWith(ready, OneCellScene(Portal with { Position = new Vector2(float.NaN, 0) })));
        Check.Equal(walls, DrawnWith(ready, OneCellScene(Portal with { Position = new Vector2(float.PositiveInfinity, 0) })));
        foreach (var size in new[] { 0f, -2f, float.NaN, float.PositiveInfinity }) Check.Equal(walls + 4, DrawnWith(ready, OneCellScene(Portal with { Size = size })), $"size {size}");
    }

    [Test]
    public void AnEntityTheGameAlreadyIconisedIsNotDrawnTwice()
    {
        var monster = Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare);
        var terrain = Scene.OneCell();
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
        TestGame.UseGameIcons(name => name == "UI_MiniMap_Player1" ? Ready : null);
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
                    if (list.VtxBuffer[i].Uv.Y != Ready.Uv0.Y) continue;
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
        var terrain = Scene.OneCell();
        var withMarker = Drawn(Scene.State(entities: [monster], terrain: terrain));

        Check.That(withMarker > Drawn(Scene.State(terrain: terrain)), "the monster draws");
        Check.Equal(withMarker, Drawn(Scene.State(entities: [monster], terrain: terrain, icons: [Portal with { EntityId = monster.Id, Position = new Vector2(float.NaN, 0) }])));
    }

    [Test]
    public void AnEntityWithAnSvgIconAsksForItWithItsStyleAndDrawsOneQuad()
    {
        var asked = new List<(string Icon, int Pixels, Color Colour)>();
        var terrain = Scene.OneCell();
        var rare = Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare);
        var walls = Drawn(Scene.State(terrain: terrain));
        TestGame.UseSvgIcons((icon, pixels, colour, _) =>
        {
            asked.Add((icon, pixels, colour));
            return Ready;
        });
        try
        {
            var drawn = Drawn(Scene.State(entities: [rare], terrain: terrain), configure: main =>
            {
                main.Settings.Icons.ScaleWithZoom = false;
                main.Settings.Icons.RareMonsters.Scale = 1.5f;
                main.Settings.Icons.RareMonsters.Colour = Color.DeepSkyBlue;
            });

            Check.Equal(walls + 4, drawn);
            Check.Equal("svg:elite", asked[^1].Icon);
            Check.Equal(12, asked[^1].Pixels);
            Check.Equal(Color.DeepSkyBlue.ToArgb(), asked[^1].Colour.ToArgb());
        }
        finally
        {
            TestGame.UseSvgIcons(null);
        }
    }

    [Test]
    public void AnSvgMarkerIsADotUntilItsIconIsReadyAndPlainMarkersNeverAsk()
    {
        var terrain = Scene.OneCell();
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

    static uint Packed(Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);

    static readonly Color FixedColour = Color.FromArgb(0xFF, 0x12, 0x34, 0x56);
    static readonly Color FoundColour = Color.FromArgb(0xFF, 0x65, 0x43, 0x21);

    static IconStyle FoundT(bool show = true, float scale = 1) => new() { Key = "T", Name = "T", Icon = "game:T", Colour = FoundColour, Scale = scale, Show = show };

    static GameState LinkedScene(string texture = "T") =>
        Scene.State(terrain: Scene.OneCell(), entities: [Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare)], icons: [Portal with { Texture = texture, EntityId = 1 }]);

    static void FixedRare(Main main, bool overrideGameIcon = false)
    {
        var rare = main.Settings.Icons.RareMonsters;
        rare.Icon = "marker:Square";
        rare.Colour = FixedColour;
        rare.OverrideGameIcon = overrideGameIcon;
    }

    static HashSet<uint> Colours(GameState state, Action<Main> configure)
    {
        var found = new HashSet<uint>();
        TestGame.UseGameIcons(name => name == "T" ? Ready : null);
        try
        {
            TestGame.Set(state);
            Map.View = null;
            var main = Scene.Plugin();
            configure(main);
            TestFrame.Run(() =>
            {
                main.Tick();
                main.Tick();
                var list = Draw.Background;
                var first = list.VtxBuffer.Size;
                main.Render();
                for (var i = first; i < list.VtxBuffer.Size; i++) found.Add(list.VtxBuffer[i].Col);
            });
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
        return found;
    }

    [Test]
    public void AnEntityWithAGameIconDrawsOnlyTheGameIcon()
    {
        var colours = Colours(LinkedScene(), main =>
        {
            FixedRare(main);
            main.Settings.Icons.Found.Add(FoundT());
        });

        Check.That(!colours.Contains(Packed(FixedColour)), "the fixed row is not drawn");
        Check.That(colours.Contains(Packed(FoundColour)), "the game icon is drawn");
    }

    [Test]
    public void OverrideGameIconDrawsTheFixedRowAndSkipsTheGameIcon()
    {
        var colours = Colours(LinkedScene(), main =>
        {
            FixedRare(main, overrideGameIcon: true);
            main.Settings.Icons.Found.Add(FoundT());
        });

        Check.That(colours.Contains(Packed(FixedColour)), "the fixed row is drawn");
        Check.That(!colours.Contains(Packed(FoundColour)), "the game icon is not drawn");
    }

    [Test]
    public void AHiddenRowDrawsNothing()
    {
        var empty = Drawn(OneCellScene());

        Check.That(DrawnWith(_ => Ready, OneCellScene(Portal with { Texture = "T" })) > empty, "a shown row draws");
        Check.Equal(empty, DrawnWith(_ => Ready, OneCellScene(Portal with { Texture = "T" }), main => main.Settings.Icons.Found.Add(FoundT(show: false))));
    }

    [Test]
    public void AnIconWithNoTextureStillDrawsTheFallbackShape()
    {
        var gold = Packed(Color.FromArgb(0xF9, 0xDD, 0x75));
        var settings = default(Settings);

        Check.That(Colours(OneCellScene(Portal with { Texture = "" }), main => settings = main.Settings).Contains(gold), "gold fallback drawn");
        Check.Equal(0, settings!.Icons.Found.Count);
    }

    [Test]
    public void GameIconSizeMultipliesBaseGameSizeGlobalScaleAndRowScale()
    {
        TestGame.UseGameIcons(name => name == "T" ? Ready : null);
        try
        {
            TestGame.Set(OneCellScene(Portal with { Texture = "T", Size = 1 }));
            Map.View = null;
            var main = Scene.Plugin();
            main.Settings.Icons.GameIconScale = 2;
            main.Settings.Icons.ScaleWithZoom = false;
            main.Settings.Icons.Found.Add(FoundT(scale: 1.5f));
            TestFrame.Run(() =>
            {
                main.Tick();
                main.Tick();
                var list = Draw.Background;
                main.Render();
                var quad = Enumerable.Range(list.VtxBuffer.Size - 4, 4).Select(i => list.VtxBuffer[i].Pos).ToArray();
                Check.Equal(72f, quad.Max(p => p.X) - quad.Min(p => p.X));
                Check.Equal(72f, quad.Max(p => p.Y) - quad.Min(p => p.Y));
            });
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Test]
    public void GameIconsGrowWithTheMapZoom()
    {
        TestGame.UseGameIcons(name => name == "T" ? Ready : null);
        try
        {
            var player = new Player(new Vector3(12, 12, 0), Vector2.UnitX);
            TestGame.Set(Scene.State(player: player, terrain: Scene.OneCell(), icons: [Portal with { Texture = "T", Size = 1, Position = new Vector2(12, 12) }]));
            Map.View = null;
            var main = Scene.Plugin();
            main.Settings.General.Zoom = GeneralTab.DefaultZoom * 2;
            main.Settings.Icons.ZoomScaleFactor = 1;
            main.Settings.Icons.Found.Add(FoundT(scale: 1));
            TestFrame.Run(() =>
            {
                main.Tick();
                main.Tick();
                var list = Draw.Background;
                main.Render();
                var quads = Enumerable.Range(0, list.VtxBuffer.Size / 4).Select(q => Enumerable.Range(q * 4, 4).Select(i => list.VtxBuffer[i].Pos).ToArray());
                Check.That(quads.Any(quad => MathF.Abs(quad.Max(p => p.X) - quad.Min(p => p.X) - 48f) < 0.01f), "a 48px icon quad at twice the default zoom");
            });
        }
        finally
        {
            TestGame.UseGameIcons(null);
        }
    }

    [Test]
    public void TheIconIndexFollowsTheMapIconsArrayFromFrameToFrame()
    {
        var monster = Scene.Entity(EntityKind.Monster, new Vector3(15, 15, 0), Rarity.Rare);
        var terrain = Scene.OneCell();
        var withIcon = Scene.State(entities: [monster], terrain: terrain, icons: [Portal with { EntityId = monster.Id }]);
        var withoutIcon = Scene.State(entities: [monster], terrain: terrain);
        var iconOnly = Drawn(Scene.State(terrain: terrain, icons: [Portal with { EntityId = monster.Id }]));
        var terrainOnly = Drawn(Scene.State(terrain: terrain));
        var main = Scene.Plugin();
        int first = 0, second = 0, third = 0;

        int RenderedVertices()
        {
            var before = Draw.Background.VtxBuffer.Size;
            main.Render();
            return Draw.Background.VtxBuffer.Size - before;
        }

        Map.View = null;
        TestGame.Set(withIcon);
        TestFrame.Run(() =>
        {
            main.Tick();
            main.Tick();
            first = RenderedVertices();
        });
        TestGame.Set(withoutIcon);
        TestFrame.Run(() =>
        {
            main.Tick();
            second = RenderedVertices();
        });
        TestGame.Set(withIcon);
        TestFrame.Run(() =>
        {
            main.Tick();
            third = RenderedVertices();
        });

        Check.Equal(iconOnly, first, "the game icon hides the entity marker");
        Check.That(second > terrainOnly, "the icon array is gone, so the entity draws its own marker");
        Check.Equal(iconOnly, third, "and the next icon array hides it again");
    }

    [Test]
    public void FoundRowsAreRecordedWhileTicking()
    {
        TestGame.Set(OneCellScene(Portal with { Texture = "T" }));
        var main = Scene.Plugin();
        TestFrame.Run(() => main.Tick());

        Check.That(main.Settings.Icons.FoundFor("T") is not null, "T discovered");
    }

    [Test]
    public void WithoutATrailTheRegionOutlineIsDrawnOnce()
    {
        var oneLoop = TestFrame.Vertices(() => Draw.Polyline([new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 20), new Vector2(10, 20)], Color.White, 2.5f, true));

        Check.That(oneLoop > 0, "the probe loop draws");
        Check.Equal(oneLoop, Drawn(OneCellScene()));
    }

    [Test]
    public void WithoutATrailTheOutlineUsesTheExploredColourNotTheRegionOne()
    {
        var region = new Settings().Map.Region;

        var colours = Colours(OneCellScene(), _ => { });

        Check.That(colours.Contains(Packed(region.ExploredOutline)), "explored colour");
        Check.That(!colours.Contains(Packed(region.Outline)), "region colour");
    }

    [Test]
    public void WithATrailBothOutlinesStillDraw()
    {
        var withTrail = Scene.State(terrain: Scene.OneCell(), explored: new ExploredTrail([new Vector2(15, 15)], 50));

        Check.Equal(2 * Drawn(OneCellScene()), Drawn(withTrail));
    }

    static readonly MapView Identity = new(Vector2.Zero, 1);

    static int Indices(Action draw)
    {
        var indices = 0;
        TestFrame.Run(() =>
        {
            var list = ImGui.GetBackgroundDrawList();
            var before = list.IdxBuffer.Size;
            draw();
            indices = list.IdxBuffer.Size - before;
        });
        return indices;
    }

    [Test]
    public void OnlyTheVisibleRunOfALongOutlineIsSubmitted()
    {
        Vector2[] points = [new(-9000, 100), new(-8000, 100), new(-7000, 100), new(10, 100), new(20, 100)];
        var main = new Main();

        var drawn = TestFrame.Vertices(() => main.DrawOutline([new Polyline(points, false)], Identity, Screen, Color.White, 2));
        var visibleRun = TestFrame.Vertices(() => Draw.Polyline(points.AsSpan(2), Color.White, 2));

        Check.That(drawn > 0, "something draws");
        Check.Equal(visibleRun, drawn);
    }

    [Test]
    public void AnOutlineEntirelyOffScreenDrawsNothing()
    {
        Vector2[] points = [new(-9000, 100), new(-8000, 100), new(-7000, 100)];
        var main = new Main();

        Check.Equal(0, TestFrame.Vertices(() => main.DrawOutline([new Polyline(points, false), new Polyline(points, true)], Identity, Screen, Color.White, 2)));
    }

    [Test]
    public void AClosedOutlineThatIsFullyVisibleStaysOneClosedLoop()
    {
        Vector2[] points = [new(100, 100), new(200, 100), new(200, 200), new(100, 200)];
        var main = new Main();

        var drawn = Indices(() => main.DrawOutline([new Polyline(points, true)], Identity, Screen, Color.White, 2));
        var closed = Indices(() => Draw.Polyline(points, Color.White, 2, true));
        var open = Indices(() => Draw.Polyline(points, Color.White, 2));

        Check.That(closed != open, "the probe can tell closed from open");
        Check.Equal(closed, drawn);
    }

    [Test]
    public void AClosedOutlineWithAnOffScreenSideDrawsTheRestAsOneOpenRun()
    {
        Vector2[] points = [new(-9000, 100), new(-8000, 100), new(100, 100), new(100, 200), new(-9000, 200)];
        var main = new Main();

        var drawn = Indices(() => main.DrawOutline([new Polyline(points, true)], Identity, Screen, Color.White, 2));
        var run = Indices(() => Draw.Polyline(points.AsSpan(1), Color.White, 2));

        Check.Equal(run, drawn);
    }

    [Test]
    public void ANonFinitePointBreaksTheRunInsteadOfDrawingGarbage()
    {
        Vector2[] points = [new(float.NaN, 0), new(10, 10), new(20, 10)];
        var main = new Main();

        var drawn = TestFrame.Vertices(() => main.DrawOutline([new Polyline(points, false)], Identity, Screen, Color.White, 2));
        var run = TestFrame.Vertices(() => Draw.Polyline(points.AsSpan(1), Color.White, 2));

        Check.Equal(run, drawn);
    }

    [Test]
    public void AnEmptyOrSinglePointOutlineDrawsNothing()
    {
        var main = new Main();

        Check.Equal(0, TestFrame.Vertices(() => main.DrawOutline([new Polyline([], true), new Polyline([], false), new Polyline([new Vector2(10, 10)], true)], Identity, Screen, Color.White, 2)));
    }

    [Test]
    public void AClosedOutlineVisibleAcrossItsStartPointDrawsOneJoinedRun()
    {
        Vector2[] points = [new(100, 100), new(-9000, 100), new(-9000, 200), new(100, 200)];
        Vector2[] joined = [points[2], points[3], points[0], points[1]];
        var main = new Main();

        var drawn = TestFrame.Vertices(() => main.DrawOutline([new Polyline(points, true)], Identity, Screen, Color.White, 2));
        var oneRun = TestFrame.Vertices(() => Draw.Polyline(joined, Color.White, 2));
        var twoRuns = TestFrame.Vertices(() => { Draw.Polyline(joined.AsSpan(0, 3), Color.White, 2); Draw.Polyline(joined.AsSpan(2), Color.White, 2); });

        var drawnIndices = Indices(() => main.DrawOutline([new Polyline(points, true)], Identity, Screen, Color.White, 2));
        var openIndices = Indices(() => Draw.Polyline(joined, Color.White, 2));
        var closedIndices = Indices(() => Draw.Polyline(joined, Color.White, 2, true));

        Check.That(oneRun != twoRuns, "the probe can tell one run from two");
        Check.That(openIndices != closedIndices, "the probe can tell open from closed");
        Check.Equal(oneRun, drawn);
        Check.Equal(openIndices, drawnIndices);
    }

    [Test]
    public void ASegmentCrossingTheWholeViewportWithBothEndsOutsideStillDraws()
    {
        var main = new Main();

        Check.That(TestFrame.Vertices(() => main.DrawOutline([new Polyline([new Vector2(-100, 100), new Vector2(3000, 100)], false)], Identity, Screen, Color.White, 2)) > 0, "crossing segment draws");
    }
}
