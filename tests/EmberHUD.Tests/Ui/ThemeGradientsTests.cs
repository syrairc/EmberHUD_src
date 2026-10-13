using EmberHUD.Host.Ui;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Tests;

public sealed class ThemeGradientsTests
{
    const uint Opaque = 0xFF000000;
    const uint TextRgb = 0xD9D9D9;

    static readonly GradientShade Gold = new(Rgb(0xFF, 0xD4, 0x6A), Rgb(0xB9, 0x82, 0x0F), Rgb(0xFF, 0xE7, 0xA3), Rgb(0x2A, 0x2C, 0x30), HasHighlight: true, HasText: true);
    static readonly GradientShade Slate = new(Rgb(0x25, 0x2B, 0x33), Rgb(0x2C, 0x40, 0x72), 0, 0, HasHighlight: false, HasText: false);
    static readonly GradientShade[] Shades = [Gold, Slate];
    static readonly uint FrameRgb = Rgb(0x28, 0x40, 0x55);
    static readonly uint[] Frames = [FrameRgb];

    public ThemeGradientsTests() => TestFrame.Run(() => { });

    static uint Rgb(int r, int g, int b) => (uint)(r | g << 8 | b << 16);

    static ImDrawVert V(float x, float y, uint colour) => new() { Pos = new Vector2(x, y), Col = colour };

    static uint Tag(int index, uint alpha = Opaque) => alpha | ThemeGradients.SentinelRgb(index);

    static void Shade(ImDrawVert[] vertices) => ThemeGradients.Apply(vertices, Shades, TextRgb, []);

    [Fact]
    public void TextInsideAFrameDrawnOnGoldStaysLightAndTextOnTheGoldAroundItStillGoesDark()
    {
        ImDrawVert[] vertices =
        [
            V(0, 0, Tag(0)), V(100, 0, Tag(0)), V(100, 40, Tag(0)), V(0, 40, Tag(0)),
            V(10, 5, Opaque | FrameRgb), V(60, 5, Opaque | FrameRgb), V(60, 30, Opaque | FrameRgb), V(10, 30, Opaque | FrameRgb),
            V(20, 10, Opaque | TextRgb), V(80, 35, Opaque | TextRgb),
        ];
        var runs = new List<GradientRun>();
        ThemeGradients.Apply(vertices, Shades, TextRgb, runs, Frames);

        Assert.Equal(Opaque | TextRgb, vertices[8].Col);
        Assert.Equal(Opaque | Gold.Text, vertices[9].Col);
        for (var i = 4; i < 8; i++) Assert.Equal(Opaque | FrameRgb, vertices[i].Col);
        Assert.Contains(runs, run => run.Shade == -1 && run.Start == 4 && run.End == 8);
    }

    [Fact]
    public void AFrameDrawnBeforeTheGoldDoesNotBlock()
    {
        ImDrawVert[] vertices =
        [
            V(10, 5, Opaque | FrameRgb), V(60, 5, Opaque | FrameRgb), V(60, 30, Opaque | FrameRgb), V(10, 30, Opaque | FrameRgb),
            V(0, 0, Tag(0)), V(100, 0, Tag(0)), V(100, 40, Tag(0)), V(0, 40, Tag(0)),
            V(20, 10, Opaque | TextRgb),
        ];
        ThemeGradients.Apply(vertices, Shades, TextRgb, [], Frames);

        Assert.Equal(Opaque | Gold.Text, vertices[8].Col);
    }

    [Fact]
    public void ADisabledFrameWithLowerAlphaStillBlocks()
    {
        const uint half = 0x80000000;
        ImDrawVert[] vertices =
        [
            V(0, 0, Tag(0)), V(100, 0, Tag(0)), V(100, 40, Tag(0)), V(0, 40, Tag(0)),
            V(10, 5, half | FrameRgb), V(60, 5, half | FrameRgb), V(60, 30, half | FrameRgb), V(10, 30, half | FrameRgb),
            V(20, 10, half | TextRgb),
        ];
        ThemeGradients.Apply(vertices, Shades, TextRgb, [], Frames);

        Assert.Equal(half | TextRgb, vertices[8].Col);
        Assert.Equal(half | FrameRgb, vertices[4].Col);
    }

    [Fact]
    public void TheSentinelVectorPacksToTheSentinelRgb()
    {
        Assert.Equal(ThemeGradients.SentinelRgb(7), ImGui.ColorConvertFloat4ToU32(ThemeGradients.Sentinel(7, 1)) & 0xFFFFFF);
        Assert.Equal(7, ThemeGradients.IndexOf(Tag(7)));
        Assert.Equal(-1, ThemeGradients.IndexOf(Opaque | TextRgb));
    }

    [Fact]
    public void ARunIsShadedTopToBottomAndEveryVertexKeepsItsAlpha()
    {
        ImDrawVert[] vertices = [V(0, 10, Tag(1)), V(40, 10, Tag(1, 0)), V(0, 20, Tag(1, 0x80000000)), V(40, 30, Tag(1)), V(0, 30, Tag(1))];
        Shade(vertices);

        Assert.Equal(Opaque | Slate.Top, vertices[0].Col);
        Assert.Equal(Slate.Top, vertices[1].Col);
        Assert.Equal(0x80000000u, vertices[2].Col & 0xFF000000);
        Assert.InRange(vertices[2].Col & 0xFF, 0x28u, 0x29u);
        Assert.Equal(Opaque | Slate.Bottom, vertices[3].Col);
        Assert.Equal(Opaque | Slate.Bottom, vertices[4].Col);
    }

    [Fact]
    public void TheHighlightOnlyTakesTheTopEdge()
    {
        ImDrawVert[] vertices = [V(4, 10, Tag(0)), V(36, 10.8f, Tag(0)), V(0, 14, Tag(0)), V(40, 30, Tag(0))];
        Shade(vertices);

        Assert.Equal(Opaque | Gold.Highlight, vertices[0].Col);
        Assert.Equal(Opaque | Gold.Highlight, vertices[1].Col);
        Assert.NotEqual(Opaque | Gold.Highlight, vertices[2].Col);
        Assert.NotEqual(Opaque | Gold.Top, vertices[2].Col);
        Assert.Equal(Opaque | Gold.Bottom, vertices[3].Col);
    }

    [Fact]
    public void TextInsideAGoldRunTurnsDarkAndAllOtherTextStays()
    {
        ImDrawVert[] vertices =
        [
            V(0, 0, Tag(0)), V(40, 0, Tag(0)), V(40, 20, Tag(0)), V(0, 20, Tag(0)),
            V(100, 0, Tag(1)), V(200, 0, Tag(1)), V(200, 100, Tag(1)), V(100, 100, Tag(1)),
            V(10, 10, Opaque | TextRgb), V(150, 50, Opaque | TextRgb), V(300, 300, 0x80000000 | TextRgb),
        ];
        Shade(vertices);

        Assert.Equal(Opaque | Gold.Text, vertices[8].Col);
        Assert.Equal(Opaque | TextRgb, vertices[9].Col);
        Assert.Equal(0x80000000 | TextRgb, vertices[10].Col);
    }

    [Fact]
    public void TextTakesTheLastTaggedFillUnderItSoADarkButtonOnGoldKeepsItsLightText()
    {
        ImDrawVert[] vertices =
        [
            V(0, 0, Tag(0)), V(100, 0, Tag(0)), V(100, 40, Tag(0)), V(0, 40, Tag(0)),
            V(10, 5, Tag(1)), V(50, 5, Tag(1)), V(50, 30, Tag(1)), V(10, 30, Tag(1)),
            V(20, 10, Opaque | TextRgb), V(80, 35, Opaque | TextRgb),
        ];
        Shade(vertices);

        Assert.Equal(Opaque | TextRgb, vertices[8].Col);
        Assert.Equal(Opaque | Gold.Text, vertices[9].Col);
    }

    [Fact]
    public void VerticesThatAreNotATagOrPointPastTheShadesAreUntouched()
    {
        ImDrawVert[] vertices = [V(0, 0, 0xFF0201FF), V(0, 5, 0xFF336699), V(0, 9, Tag(5))];
        Shade(vertices);

        Assert.Equal(0xFF0201FFu, vertices[0].Col);
        Assert.Equal(0xFF336699u, vertices[1].Col);
        Assert.Equal(Tag(5), vertices[2].Col);
    }

    [Fact]
    public void TwoRunsSplitByAnotherColourEachGetTheirOwnGradient()
    {
        ImDrawVert[] vertices = [V(0, 0, Tag(0)), V(40, 10, Tag(0)), V(0, 10, 0xFF336699), V(4, 100, Tag(0)), V(40, 110, Tag(0))];
        var runs = new List<GradientRun>();
        ThemeGradients.Apply(vertices, Shades, TextRgb, runs, Frames);

        Assert.Equal(2, runs.Count);
        Assert.Equal(Opaque | Gold.Highlight, vertices[3].Col);
        Assert.Equal(Opaque | Gold.Bottom, vertices[4].Col);
    }

    [Fact]
    public void ARunOfOneVertexBecomesItsTopColour()
    {
        ImDrawVert[] vertices = [V(5, 5, Tag(1))];
        Shade(vertices);

        Assert.Equal(Opaque | Slate.Top, vertices[0].Col);
    }

    [Fact]
    public void ShadingAFrameAllocatesNothingOnceWarm()
    {
        ImDrawVert[] vertices = [V(0, 0, Tag(0)), V(40, 20, Tag(0)), V(10, 10, Opaque | TextRgb), V(0, 30, Tag(1)), V(40, 60, Tag(1))];
        var pristine = (ImDrawVert[])vertices.Clone();
        var runs = new List<GradientRun>();
        ThemeGradients.Apply(vertices, Shades, TextRgb, runs, Frames);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            Array.Copy(pristine, vertices, vertices.Length);
            ThemeGradients.Apply(vertices, Shades, TextRgb, runs, Frames);
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void TheDrawDataPassReachesEveryDrawList()
    {
        var style = ImGui.GetStyle();
        var saved = style.Colors[(int)ImGuiCol.Button];
        try
        {
            style.Colors[(int)ImGuiCol.Button] = ThemeGradients.Sentinel(0, 1);
            for (var frame = 0; frame < 2; frame++)
                TestFrame.Run(() =>
                {
                    ImGui.Begin("gradients");
                    ImGui.Button("gold");
                    ImGui.End();
                });
            ThemeGradients.Apply(ImGui.GetDrawData(), Shades, TextRgb, [], Frames);

            var data = ImGui.GetDrawData();
            var colours = new List<uint>();
            for (var n = 0; n < data.CmdListsCount; n++)
            {
                var list = data.CmdLists[n];
                for (var i = 0; i < list.VtxBuffer.Size; i++) colours.Add(list.VtxBuffer[i].Col & 0xFFFFFF);
            }
            Assert.DoesNotContain(ThemeGradients.SentinelRgb(0), colours);
            Assert.Contains(Gold.Bottom, colours);
        }
        finally
        {
            style.Colors[(int)ImGuiCol.Button] = saved;
        }
    }
}
