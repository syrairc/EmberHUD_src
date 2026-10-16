using System;
using System.Collections.Generic;
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
        var texts = Drawn(NameColour, () => DrawPlate(Data(50, 100), style, At));
        Check.That(texts.Count > 0, "texts missing");
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
