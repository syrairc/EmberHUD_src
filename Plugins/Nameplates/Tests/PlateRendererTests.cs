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
    static readonly Vector2 Anchor = new(500, 300);
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

    static void DrawPlate(PlateData data, PlateStyle style, Vector2 anchor) => PlateRenderer.DrawPlate(data, style, anchor, Viewport);

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
        var back = Drawn(Back, () => DrawPlate(Data(50, 100), style, Anchor));
        var fill = Drawn(Fill, () => DrawPlate(Data(50, 100), style, Anchor));
        Check.Equal((450f, 550f), Across(back));
        Check.Equal((450f, 500f), Across(fill));
        Check.Equal(4, fill.Count);
    }

    [Test]
    public void NoBarWhenMaxLifeIsZeroButTheNameStillDraws()
    {
        var style = Style(name: true);
        Check.Equal(0, Drawn(Back, () => DrawPlate(Data(0, 0), style, Anchor)).Count);
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(0, 0), style, Anchor)).Count);
        var name = Drawn(NameColour, () => DrawPlate(Data(0, 0), style, Anchor));
        var (min, max) = Across(name);
        Check.That(min >= 400 && max <= 600 && min < 500 && max > 500, $"name not centred on the plate {min}..{max}");
    }

    [Test]
    public void OnBarTextIsSkippedWithoutABar()
    {
        var style = Style();
        style.OnBar = new TextLine { Enabled = true, Template = "{hp%}%", Style = new TextStyle { Color = NameColour } };
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(0, 0), style, Anchor)).Count);
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(double.NaN, 100), style, Anchor)).Count);
        Check.That(Drawn(NameColour, () => DrawPlate(Data(50, 100), style, Anchor)).Count > 0);
    }

    [Test]
    public void ANaNAnchorDrawsNothing()
    {
        var style = Style(name: true);
        Check.That(!PlateRenderer.DrawPlate(Data(50, 100), style, new Vector2(float.NaN, 300), Viewport));
        Check.That(!PlateRenderer.DrawPlate(Data(50, 100), style, new Vector2(500, float.NaN), Viewport));
    }

    [Test]
    public void LifeAboveMaxClampsToAFullBar() =>
        Check.Equal((450f, 550f), Across(Drawn(Fill, () => DrawPlate(Data(500, 100), Style(), Anchor))));

    [Test]
    public void NegativeLifeDrawsAnEmptyBar()
    {
        var style = Style();
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(-20, 100), style, Anchor)).Count);
        Check.Equal((450f, 550f), Across(Drawn(Back, () => DrawPlate(Data(-20, 100), style, Anchor))));
    }

    [Test]
    public void NaNLifeDrawsNoBar()
    {
        var style = Style();
        Check.Equal(0, Drawn(Fill, () => DrawPlate(Data(double.NaN, 100), style, Anchor)).Count);
        Check.Equal(0, Drawn(Back, () => DrawPlate(Data(double.NaN, 100), style, Anchor)).Count);
    }

    [Test]
    public void OffsetYMovesThePlateUp()
    {
        var style = Style();
        style.OffsetY = 40;
        var fill = Drawn(Fill, () => DrawPlate(Data(100, 100), style, Anchor));
        Check.Equal(254f, fill[0].Pos.Y);
    }

    [Test]
    public void TheTexturedFillIsClippedByUvAndRect()
    {
        var sprite = new Sprite(default, new Vector2(0.25f, 0), new Vector2(0.75f, 1));
        TestGame.UseTextures((_, _, _, _) => { }, _ => sprite);
        try
        {
            var fill = Drawn(Fill, () => DrawPlate(Data(50, 100), Style(), Anchor));
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
        Check.Equal(0, TestFrame.Vertices(() => Check.That(!PlateRenderer.DrawPlate(Data(50, 100), style, new Vector2(-500, 300), Viewport), "reported drawn")));
        Check.Equal(0, TestFrame.Vertices(() => PlateRenderer.DrawPlate(Data(50, 100), style, new Vector2(500, 2000), Viewport)));
        Check.That(TestFrame.Vertices(() => PlateRenderer.DrawPlate(Data(50, 100), style, Anchor, Viewport)) > 0, "control plate drew nothing");
    }

    [Test]
    public void EmptyTemplatesDrawNothing()
    {
        var style = new PlateStyle();
        style.Above = new TextLine { Enabled = true, Template = "" };
        style.OnBar = new TextLine { Enabled = true, Template = "" };
        style.Below = new TextLine { Enabled = true, Template = "" };
        Check.Equal(0, TestFrame.Vertices(() => PlateRenderer.DrawPlate(Data(0, 0), style, Anchor, Viewport)));
    }

    [Test]
    public void AnEmptyNameDrawsNoNameText()
    {
        var style = Style(name: true);
        Check.Equal(0, Drawn(NameColour, () => DrawPlate(Data(50, 100, name: ""), style, Anchor)).Count);
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

    [Test]
    public void ThePlayerGetsASelfPlate()
    {
        var state = GameState.Empty with { Camera = Forward, Player = new Player(new Vector3(1000, 0, 0), Vector2.UnitX) { Life = 50, MaxLife = 100 } };
        Check.That(Rendered(state) > 0, "no self plate");
        Check.Equal(0, Rendered(state, settings => settings.Buckets[(int)Bucket.Self].Show = false));
    }
}
