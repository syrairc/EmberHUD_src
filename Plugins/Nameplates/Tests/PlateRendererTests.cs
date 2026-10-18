using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using EmberHUD.Ui;

namespace NameplatesPlugin;

public sealed class PlateRendererTests
{
    static readonly Color Fill = Color.FromArgb(255, 10, 200, 30);
    static readonly Color Back = Color.FromArgb(255, 1, 2, 3);
    static readonly Color Border = Color.FromArgb(255, 4, 5, 6);
    static readonly Color NameColour = Color.FromArgb(255, 250, 20, 240);
    static readonly Vector2 At = new(500, 300);
    static readonly Vector2 Viewport = new(1920, 1080);
    static readonly Camera Forward = new(Vector3.Zero, Vector3.Zero, 90);

    static PlateStyle Style(bool name = false)
    {
        var style = new PlateStyle();
        style.Bar.Width = 100;
        style.Bar.Height = 12;
        style.Bar.BorderWidth = 2;
        style.Bar.Fill = Fill;
        style.Bar.Back = Back;
        style.Bar.Border = Border;
        style.Above = new TextLine { Enabled = name, Template = "{name}", Style = new TextStyle { Color = NameColour } };
        style.OnBar = new TextLine();
        style.Below = new TextLine();
        return style;
    }

    static List<(Vector2 Pos, Vector2 Uv)> Drawn(Color colour, Action draw)
    {
        var found = new List<(Vector2, Vector2)>();
        TestFrame.Run(() =>
        {
            var list = Draw.Background;
            var first = list.VtxBuffer.Size;
            draw();
            for (var i = first; i < list.VtxBuffer.Size; i++)
            {
                var vertex = list.VtxBuffer[i];
                if (vertex.Col == EColor.U32(colour)) found.Add((vertex.Pos, vertex.Uv));
            }
        });
        return found;
    }

    static (float Min, float Max) Across(List<(Vector2 Pos, Vector2 Uv)> vertices)
    {
        Check.That(vertices.Count > 0, "nothing drawn in that colour");
        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var (pos, _) in vertices)
        {
            min = Math.Min(min, pos.X);
            max = Math.Max(max, pos.X);
        }
        return (min, max);
    }

    static bool DrawPlate(PlateData data, PlateStyle style, Vector2 anchor) => PlateRenderer.DrawPlate(data, style, Anchor.Projected(style, anchor), Viewport);

    static PlateData Data(double life, double maxLife, string name = "Ghoul") => new(name, life, maxLife, 0, "Normal", "Monster");

    static Entity Ghoul(Vector3 at) => new(1, 1, at, EntityKind.Monster, Rarity.Normal) { Name = "Ghoul", Life = 50, MaxLife = 100 };

    static int Rendered(GameState state, Action<Settings>? configure = null)
    {
        TestGame.Set(state);
        try
        {
            var main = new Main();
            configure?.Invoke(main.Settings);
            return TestFrame.Vertices(main.Render);
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    static CastBarSettings CastBar(float offsetY = 4)
    {
        var cast = new CastBarSettings { Enabled = true, OffsetY = offsetY, Text = new TextLine() };
        cast.Bar.Width = 100;
        cast.Bar.Height = 8;
        cast.Bar.BorderWidth = 0;
        cast.Bar.Fill = Fill;
        cast.Bar.Back = Back;
        return cast;
    }

    static List<(Vector2 Pos, Vector2 Uv)> DrawnCast(CastBarSettings cast, Color colour, float progress) =>
        Drawn(colour, () => PlateRenderer.DrawCast(cast, Data(50, 100), progress, Anchor.Projected(Style(), At)));

    static (float Min, float Max) Down(List<(Vector2 Pos, Vector2 Uv)> vertices) => (vertices.Min(v => v.Pos.Y), vertices.Max(v => v.Pos.Y));

    [Test]
    public void TheCastBarFillsItsProgressUnderThePlate()
    {
        var cast = CastBar();
        Check.Equal((450f, 475f), Across(DrawnCast(cast, Fill, 0.25f)));
        Check.Equal((450f, 550f), Across(DrawnCast(cast, Back, 0.25f)));
        Check.Equal((310f, 318f), Down(DrawnCast(cast, Back, 0.25f)));
    }

    [Test]
    public void TheCastBarOffsetMovesItDown() => Check.Equal((326f, 334f), Down(DrawnCast(CastBar(20), Back, 0.5f)));

    [Test]
    public void ACastBarAtZeroProgressDrawsOnlyItsBack()
    {
        var cast = CastBar();
        Check.Equal(0, DrawnCast(cast, Fill, 0).Count);
        Check.That(DrawnCast(cast, Back, 0).Count > 0, "no back drawn");
    }

    [Test]
    public void TheCastBarTextIsCentredOnTheBar()
    {
        var cast = CastBar();
        cast.Text = new TextLine { Enabled = true, Template = "{cast}", Style = new TextStyle { Color = NameColour } };
        var data = new PlateData("Ghoul", 50, 100, 0, "Normal", "Monster", "Slash", 1);
        var text = Drawn(NameColour, () => PlateRenderer.DrawCast(cast, data, 0.5f, Anchor.Projected(Style(), At)));
        var (min, max) = Across(text);
        var (top, bottom) = Down(text);
        Check.That(min > 450 && max < 550 && Math.Abs((top + bottom) / 2 - 314) < 3, $"{min}..{max} {top}..{bottom}");
    }

    static readonly ScreenRect TopBar = new(new Vector2(400, 100), new Vector2(1000, 130));

    static List<(Vector2 Pos, Vector2 Uv)> BossText(TextRow row, HAlign align = HAlign.Center) =>
        Drawn(NameColour, () => PlateRenderer.DrawBossBarHp(new TextLine { Enabled = true, Template = "{hp} / {hpmax}", Align = align, Style = new TextStyle { Color = NameColour } }, row, Data(45_000_000, 126_000_000), TopBar));

    [Test]
    public void BossBarHpIsCentredOnTheBarByDefault()
    {
        var text = BossText(TextRow.OnBar);
        var (min, max) = Across(text);
        var (top, bottom) = Down(text);
        Check.That(min > 400 && max < 1000 && Math.Abs((min + max) / 2 - 700) < 3, $"{min}..{max}");
        Check.That(Math.Abs((top + bottom) / 2 - 115) < 4, $"{top}..{bottom}");
    }

    [Test]
    public void BossBarHpCanSitAboveOrBelowTheBar()
    {
        Check.That(Down(BossText(TextRow.Above)).Max <= 100, "above the bar");
        Check.That(Down(BossText(TextRow.Below)).Min >= 130, "below the bar");
    }

    [Test]
    public void BossBarHpFollowsTheAlignment()
    {
        Check.That(Across(BossText(TextRow.OnBar, HAlign.Left)).Min >= 400 && Across(BossText(TextRow.OnBar, HAlign.Left)).Min < 410, "left edge");
        Check.That(Across(BossText(TextRow.OnBar, HAlign.Right)).Max <= 1000 && Across(BossText(TextRow.OnBar, HAlign.Right)).Max > 990, "right edge");
    }

    static GameState BossFight() => GameState.Empty with
    {
        BossBar = new BossBar(TopBar, 0.36f),
        Entities = [new Entity(7, 1, Vector3.Zero, EntityKind.Monster, Rarity.Boss) { Name = "Ghost", Life = 45_000_000, MaxLife = 126_000_000 }],
    };

    [Test]
    public void BossBarHpIsOffByDefaultAndDrawsWhenEnabled()
    {
        Check.Equal(0, Rendered(BossFight()));
        Check.That(Rendered(BossFight(), s => s.BossBarHp.Enabled = true) > 0, "nothing drawn");
    }

    [Test]
    public void BossBarHpNeedsABossBarAndAMatchingBoss()
    {
        Check.Equal(0, Rendered(GameState.Empty with { Entities = BossFight().Entities }, s => s.BossBarHp.Enabled = true));
        Check.Equal(0, Rendered(BossFight() with { BossBar = new BossBar(TopBar, 0.9f) }, s => s.BossBarHp.Enabled = true));
    }

    [Test]
    public void BossBarHpHidesBehindFullScreenPanels()
    {
        var covered = BossFight() with { Ui = new GameUi([new GamePanel("Map", PanelKind.WorldMap, Vector2.Zero, Viewport, true)]) };
        Check.Equal(0, Rendered(covered, s => s.BossBarHp.Enabled = true));
    }

    static GameState Casting(bool casting) => GameState.Empty with
    {
        Camera = Forward,
        Entities = [Ghoul(new Vector3(1000, 0, 0)) with { Cast = casting ? new Cast(1, 1, 0.5f, 1, Vector2.Zero, Vector2.UnitX) { Name = "Slash" } : null }],
    };

    [Test]
    public void TheCastBarIsOffByDefaultAndDrawsWhenEnabled()
    {
        var plain = Rendered(Casting(false));
        Check.Equal(plain, Rendered(Casting(true)));
        Check.That(Rendered(Casting(true), s => s.Buckets[(int)Bucket.Normal].CastBar!.Enabled = true) > plain, "cast bar drew nothing");
        Check.Equal(plain, Rendered(Casting(false), s => s.Buckets[(int)Bucket.Normal].CastBar!.Enabled = true));
    }

    [Test]
    public void ACastBarOnAnotherBucketDoesNotDrawOnThisMonster()
    {
        var plain = Rendered(Casting(false));
        Check.Equal(plain, Rendered(Casting(true), s => s.Buckets[(int)Bucket.Boss].CastBar!.Enabled = true));
    }

    [Test]
    public void AnEnabledCastBarAsksTheHostForCasts()
    {
        var asked = typeof(Game).GetProperty("CastsAsked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        asked.SetValue(null, 0L);
        Rendered(Casting(false));
        Check.Equal(0L, (long)asked.GetValue(null)!);
        Rendered(Casting(false), s => s.Buckets[(int)Bucket.Normal].CastBar!.Enabled = true);
        Check.That((long)asked.GetValue(null)! != 0, "WantCasts was not called");
    }

    [Test]
    public void ABarDrawsFillOnlyForTheLifeFraction()
    {
        var style = Style();
        var back = Drawn(Back, () => DrawPlate(Data(50, 100), style, At));
        var fill = Drawn(Fill, () => DrawPlate(Data(50, 100), style, At));
        Check.Equal((450f, 550f), Across(back));
        Check.Equal((450f, 500f), Across(fill));
        Check.Equal(4, fill.Count);
    }

    [Test]
    public void NoBarWhenMaxLifeIsZeroButTheNameStillDraws()
    {
        var style = Style(name: true);
        Check.Equal(0, Drawn(Back, () => DrawPlate(Data(0, 0), style, At)).Count);
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(0, 0), style, At)).Count);
        var name = Drawn(NameColour, () => DrawPlate(Data(0, 0), style, At));
        var (min, max) = Across(name);
        Check.That(min >= 400 && max <= 600 && min < 500 && max > 500, $"name not centred on the plate {min}..{max}");
    }

    [Test]
    public void OnBarTextIsSkippedWithoutABar()
    {
        var style = Style();
        style.OnBar = new TextLine { Enabled = true, Template = "{hp%}%", Style = new TextStyle { Color = NameColour } };
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(0, 0), style, At)).Count);
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(double.NaN, 100), style, At)).Count);
        Check.That(Drawn(NameColour, () => DrawPlate(Data(50, 100), style, At)).Count > 0);
    }

    static (float Min, float Max) NameExtent(HAlign align)
    {
        var style = Style(name: true);
        style.Above.Align = align;
        return Across(Drawn(NameColour, () => DrawPlate(Data(50, 100), style, At)));
    }

    [Test]
    public void LeftAlignedTextStartsAtTheBarLeftEdge()
    {
        var (min, max) = NameExtent(HAlign.Left);
        Check.That(min >= 449 && min < 460 && max < 500, $"{min}..{max}");
    }

    [Test]
    public void CentreAlignedTextIsCentredOnTheBar()
    {
        var (min, max) = NameExtent(HAlign.Center);
        Check.That(min > 450 && max < 550 && Math.Abs((min + max) / 2 - 500) < 5, $"{min}..{max}");
    }

    [Test]
    public void RightAlignedTextEndsAtTheBarRightEdge()
    {
        var (min, max) = NameExtent(HAlign.Right);
        Check.That(max <= 551 && max > 540 && min > 500, $"{min}..{max}");
    }

    [Test]
    public void ANaNAnchorDrawsNothing()
    {
        var style = Style(name: true);
        Check.That(!DrawPlate(Data(50, 100), style, new Vector2(float.NaN, 300)));
        Check.That(!DrawPlate(Data(50, 100), style, new Vector2(500, float.NaN)));
    }

    [Test]
    public void LifeAboveMaxClampsToAFullBar() =>
        Check.Equal((450f, 550f), Across(Drawn(Fill, () => DrawPlate(Data(500, 100), Style(), At))));

    [Test]
    public void NegativeLifeDrawsAnEmptyBar()
    {
        var style = Style();
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(-20, 100), style, At)).Count);
        Check.Equal((450f, 550f), Across(Drawn(Back, () => DrawPlate(Data(-20, 100), style, At))));
    }

    [Test]
    public void NaNLifeDrawsNoBar()
    {
        var style = Style();
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(double.NaN, 100), style, At)).Count);
        Check.Equal(0, Drawn(Back, () => DrawPlate(Data(double.NaN, 100), style, At)).Count);
    }

    [Test]
    public void OffsetYMovesThePlateUp()
    {
        var style = Style();
        style.OffsetY = 40;
        var fill = Drawn(Fill, () => DrawPlate(Data(100, 100), style, At));
        Check.Equal(254f, fill[0].Pos.Y);
    }

    [Test]
    public void TheTexturedFillIsClippedByUvAndRect()
    {
        var sprite = new Sprite(default, new Vector2(0.25f, 0), new Vector2(0.75f, 1));
        TestGame.UseTextures((_, _, _, _) => { }, _ => sprite);
        try
        {
            var fill = Drawn(Fill, () => DrawPlate(Data(50, 100), Style(), At));
            Check.Equal((450f, 500f), Across(fill));
            var maxU = 0f;
            foreach (var (_, uv) in fill) maxU = Math.Max(maxU, uv.X);
            Check.Equal(0.5f, maxU);
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void OffScreenDrawsNothing()
    {
        var style = Style(name: true);
        Check.Equal(0, TestFrame.Vertices(() => Check.That(!DrawPlate(Data(50, 100), style, new Vector2(-500, 300)), "reported drawn")));
        Check.Equal(0, TestFrame.Vertices(() => DrawPlate(Data(50, 100), style, new Vector2(500, 2000))));
        Check.That(TestFrame.Vertices(() => DrawPlate(Data(50, 100), style, At)) > 0, "control plate drew nothing");
    }

    [Test]
    public void EmptyTemplatesDrawNothing()
    {
        var style = new PlateStyle();
        style.Above = new TextLine { Enabled = true, Template = "" };
        style.OnBar = new TextLine { Enabled = true, Template = "" };
        style.Below = new TextLine { Enabled = true, Template = "" };
        Check.Equal(0, TestFrame.Vertices(() => DrawPlate(Data(0, 0), style, At)));
    }

    [Test]
    public void AnEmptyNameDrawsNoNameText()
    {
        var style = Style(name: true);
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(50, 100, name: ""), style, At)).Count);
    }

    [Test]
    public void BehindTheCameraDrawsNothing()
    {
        var infront = GameState.Empty with { Camera = Forward, Entities = [Ghoul(new Vector3(1000, 0, 0))] };
        var behind = GameState.Empty with { Camera = Forward, Entities = [Ghoul(new Vector3(-1000, 0, 0))] };
        Check.That(Rendered(infront) > 0, "control entity drew nothing");
        Check.Equal(0, Rendered(behind));
    }

    [Test]
    public void FullScreenPanelHidesEverything()
    {
        var panel = new GamePanel("map", PanelKind.WorldMap, Vector2.Zero, Viewport, true);
        var state = GameState.Empty with { Camera = Forward, Entities = [Ghoul(new Vector3(1000, 0, 0))], Ui = new GameUi([panel]) };
        Check.Equal(0, Rendered(state));
        Check.That(Rendered(state, settings => settings.HideBehindPanels = false) > 0, "panels hid plates with HideBehindPanels off");
    }

    static readonly ScreenRect Track = new(new Vector2(600, 300), new Vector2(680, 306));

    [Test]
    public void DrawBarOffDrawsNoBarButKeepsTheTexts()
    {
        var style = Style(name: true);
        style.DrawBar = false;
        style.OnBar = new TextLine { Enabled = true, Template = "{hp%}%", Style = new TextStyle { Color = NameColour } };
        var texts = Drawn(NameColour, () => PlateRenderer.DrawPlate(Data(50, 100), style, Track, Viewport));
        var (textMin, textMax) = Across(texts);
        Check.That(textMin >= 600 && textMax <= 680, $"texts off the track {textMin}..{textMax}");
        Check.Equal(0, Drawn(Back, () => DrawPlate(Data(50, 100), style, At)).Count);
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(50, 100), style, At)).Count);
        Check.Equal(0, Drawn(Border, () => DrawPlate(Data(50, 100), style, At)).Count);
        style.DrawBar = true;
        Check.That(Drawn(Back, () => DrawPlate(Data(50, 100), style, At)).Count > 0, "bar missing with DrawBar on");
    }

    [Test]
    public void TheBackQuadMatchesTheGameRect()
    {
        var style = Style();
        style.Bar.BorderWidth = 0;
        var settings = new Settings { AnchorToGameBar = true };
        var entity = new Entity(1, 1, Vector3.Zero, EntityKind.Monster, Rarity.Normal) { HasGameBar = true, GameBar = Track };
        var rect = Anchor.Rect(settings, entity, style, At)!.Value;
        var back = Drawn(Back, () => PlateRenderer.DrawPlate(Data(50, 100), style, rect, Viewport));
        Check.Equal((600f, 680f), Across(back));
        Check.Equal(300f, back[0].Pos.Y);
        style.Bar.BorderWidth = 2;
        rect = Anchor.Rect(settings, entity, style, At)!.Value;
        back = Drawn(Back, () => PlateRenderer.DrawPlate(Data(50, 100), style, rect, Viewport));
        Check.Equal((598f, 682f), Across(back));
    }

    [Test]
    public void ThePlayerGetsASelfPlate()
    {
        var state = GameState.Empty with { Camera = Forward, Player = new Player(new Vector3(1000, 0, 0), Vector2.UnitX) { Life = 50, MaxLife = 100 } };
        Check.That(Rendered(state) > 0, "no self plate");
        Check.Equal(0, Rendered(state, settings => settings.Buckets[(int)Bucket.Self].Show = false));
    }
}
