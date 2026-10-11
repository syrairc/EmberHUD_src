using System.Drawing;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class BadgesTests
{
    static readonly Color Light = Color.FromArgb(255, 230, 180, 90);
    static readonly Color Dark = Color.FromArgb(255, 40, 60, 120);
    static readonly Color Green = Color.FromArgb(255, 100, 200, 100);

    record Capture(List<ImDrawVert> Verts, int Indices, Vector2 Origin, Vector2 CursorDelta, Vector2 Rect);

    static Capture Draw(Action draw)
    {
        Capture? capture = null;
        UiFrame.Run("badges", () =>
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var cursor = ImGui.GetCursorPos();
            var start = list.VtxBuffer.Size;
            var indexStart = list.IdxBuffer.Size;
            draw();
            var verts = new List<ImDrawVert>();
            for (var i = start; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
            capture = new Capture(verts, list.IdxBuffer.Size - indexStart, origin, ImGui.GetCursorPos() - cursor, ImGui.GetItemRectSize());
        });
        return capture!;
    }

    static List<ImDrawVert> WithColour(Capture capture, uint colour) => capture.Verts.Where(v => v.Col == colour).ToList();

    static (Vector2 Min, Vector2 Max) Bounds(List<ImDrawVert> verts) =>
        (new Vector2(verts.Min(v => v.Pos.X), verts.Min(v => v.Pos.Y)), new Vector2(verts.Max(v => v.Pos.X), verts.Max(v => v.Pos.Y)));

    static Vector2 BadgeSize(string text, bool dot = false)
    {
        var size = Vector2.Zero;
        UiFrame.Run("badges", () => size = new Vector2(Badges.Width(text, dot), ImGui.CalcTextSize(text).Y + ImGui.GetStyle().FramePadding.Y));
        return size;
    }

    static Capture Reference(Action<ImDrawListPtr, Vector2> draw) => Draw(() => draw(ImGui.GetWindowDrawList(), ImGui.GetCursorScreenPos()));

    static float TextHeight(string text)
    {
        var height = 0f;
        UiFrame.Run("badges", () => height = ImGui.CalcTextSize(text).Y);
        return height;
    }

    static float PadX()
    {
        var pad = 0f;
        UiFrame.Run("badges", () => pad = ImGui.GetStyle().FramePadding.X);
        return pad;
    }

    [Theory]
    [InlineData(5, 99, "5")]
    [InlineData(99, 99, "99")]
    [InlineData(100, 99, "99+")]
    [InlineData(12, 9, "9+")]
    public void CountTextCapsInsteadOfWidening(int n, int max, string expected) => Assert.Equal(expected, Badges.CountText(n, max));

    [Fact]
    public void BadgesDrawOneBalancedFrame() => UiFrame.Run("badges", () =>
    {
        Badges.Draw("New");
        ImGui.SameLine();
        Badges.Draw("Outline", Light, outline: true);
        ImGui.SameLine();
        Badges.Draw("Dot", Green, dot: true);
        Badges.Draw(null);
        Badges.Counter(120);
        Badges.Counter(0);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WidthIsTheTextPlusPaddingPlusTheDotWhenThereIsOne(bool dot)
    {
        var width = 0f;
        var text = 0f;
        var pad = 0f;
        UiFrame.Run("badges", () =>
        {
            width = Badges.Width("Hello", dot);
            text = ImGui.CalcTextSize("Hello").X;
            pad = ImGui.GetStyle().FramePadding.X;
        });
        Assert.Equal(text + pad * 2 + (dot ? 11f : 0f), width, 1e-3f);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyTextHasNoWidthAndDrawsNothing(string? text)
    {
        var width = -1f;
        var capture = Draw(() =>
        {
            Badges.Draw(text);
            width = Badges.Width(text, dot: true);
        });
        Assert.Equal(0f, width);
        Assert.Empty(capture.Verts);
        Assert.Equal(Vector2.Zero, capture.CursorDelta);
    }

    [Fact]
    public void ADrawnBadgeTakesItsMeasuredSizeInTheLayout()
    {
        var capture = Draw(() => Badges.Draw("Hello"));
        var size = BadgeSize("Hello");
        Assert.Equal(size.X, capture.Rect.X, 1e-3f);
        Assert.Equal(size.Y, capture.Rect.Y, 1e-3f);
    }

    [Fact]
    public void AFilledBadgeFillsItsRectInTheColourAndReadsInTheContrastColour()
    {
        var light = Draw(() => Badges.Draw("Hi", Light));
        var dark = Draw(() => Badges.Draw("Hi", Dark));
        var size = BadgeSize("Hi");
        var pad = PadX();

        var reference = Reference((list, org) => list.AddRectFilled(org, org + size, EColor.U32(Light), size.Y * 0.5f));
        var (min, max) = Bounds(WithColour(light, EColor.U32(Light)));
        var (refMin, refMax) = Bounds(WithColour(reference, EColor.U32(Light)));
        Assert.Equal(refMin, min);
        Assert.Equal(refMax, max);
        var referenceBlackText = Reference((list, org) => list.AddText(org, 0xFF000000u, "Hi"));
        Assert.Equal(reference.Indices + referenceBlackText.Indices, light.Indices);
        var textHeight = TextHeight("Hi");
        var lightText = WithColour(light, 0xFF000000u);
        var referenceText = Reference((list, org) => list.AddText(org + new Vector2(pad, (size.Y - textHeight) * 0.5f), 0xFF000000u, "Hi"));
        Assert.Equal(Bounds(WithColour(referenceText, 0xFF000000u)), Bounds(lightText));
        Assert.NotEmpty(WithColour(dark, 0xFFFFFFFFu));
        Assert.Empty(WithColour(dark, 0xFF000000u));
    }

    [Fact]
    public void ANullColourUsesTheThemeHeaderColour()
    {
        uint header = 0;
        UiFrame.Run("badges", () => header = ImGui.GetColorU32(ImGuiCol.Header));
        var capture = Draw(() => Badges.Draw("Hi"));
        Assert.Contains(capture.Verts, v => v.Col == header);
    }

    [Fact]
    public void ANullColourNeverDerivesAnythingFromTheHeaderColourAndTextUsesTheTextColour()
    {
        UiFrame.Io();
        var colours = ImGui.GetStyle().Colors;
        var header = colours[(int)ImGuiCol.Header];
        var text = colours[(int)ImGuiCol.Text];
        try
        {
            colours[(int)ImGuiCol.Header] = new Vector4(1 / 255f, 2 / 255f, 9 / 255f, 1);
            colours[(int)ImGuiCol.Text] = new Vector4(0.2f, 0.4f, 0.6f, 1);
            uint textU32 = 0;
            var capture = Draw(() =>
            {
                textU32 = ImGui.GetColorU32(ImGuiCol.Text);
                Badges.Draw("Hi");
            });

            Assert.Contains(capture.Verts, v => (v.Col & 0xFFFF) == 0x0201);
            Assert.Contains(capture.Verts, v => v.Col == textU32);
        }
        finally
        {
            colours[(int)ImGuiCol.Header] = header;
            colours[(int)ImGuiCol.Text] = text;
        }
    }

    [Fact]
    public void AnOutlineBadgeStrokesTheRingAndReadsInTheFillColour()
    {
        var outline = Draw(() => Badges.Draw("Hi", Dark, outline: true));
        var filled = Draw(() => Badges.Draw("Hi", Dark));
        var size = BadgeSize("Hi");
        var stroke = Reference((list, org) => list.AddRect(org, org + size, EColor.U32(Dark), size.Y * 0.5f));
        var text = Reference((list, org) => list.AddText(org, EColor.U32(Dark), "Hi"));

        Assert.Equal(stroke.Indices + text.Indices, outline.Indices);
        Assert.NotEqual(filled.Indices, outline.Indices);
        Assert.All(outline.Verts, v => Assert.Equal(EColor.U32(Dark) & 0x00FFFFFFu, v.Col & 0x00FFFFFFu));
        Assert.Empty(WithColour(outline, 0xFFFFFFFFu));
    }

    [Fact]
    public void ADotBadgeDrawsTheThemePillThenTheColouredDotThenThemeText()
    {
        const uint frameBg = 0xFF654321u, border = 0xFF123456u, text = 0xFFABCDEFu;
        var capture = Draw(() =>
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, frameBg);
            ImGui.PushStyleColor(ImGuiCol.Border, border);
            ImGui.PushStyleColor(ImGuiCol.Text, text);
            try
            {
                Badges.Draw("Hi", Green, dot: true);
            }
            finally
            {
                ImGui.PopStyleColor(3);
            }
        });
        var size = BadgeSize("Hi", dot: true);
        var pad = PadX();

        var pill = Reference((list, org) =>
        {
            list.AddRectFilled(org, org + size, frameBg, size.Y * 0.5f);
            list.AddRect(org, org + size, border, size.Y * 0.5f);
            list.AddCircleFilled(new Vector2(org.X + pad + 3f, org.Y + size.Y * 0.5f), 3f, EColor.U32(Green));
            list.AddText(org, text, "Hi");
        });
        Assert.Equal(pill.Indices, capture.Indices);
        Assert.NotEmpty(WithColour(capture, frameBg));
        Assert.NotEmpty(WithColour(capture, border));
        var (dotMin, dotMax) = Bounds(WithColour(capture, EColor.U32(Green)));
        Assert.Equal(capture.Origin.X + pad + 3f, (dotMin.X + dotMax.X) / 2, 0.3f);
        Assert.Equal(capture.Origin.Y + size.Y * 0.5f, (dotMin.Y + dotMax.Y) / 2, 0.3f);
        Assert.InRange(dotMax.X - dotMin.X, 4f, 6.1f);
        var textHeight = TextHeight("Hi");
        var referenceText = Reference((list, org) => list.AddText(org + new Vector2(pad + 6f + 5f, (size.Y - textHeight) * 0.5f), text, "Hi"));
        Assert.Equal(Bounds(WithColour(referenceText, text)), Bounds(WithColour(capture, text)));
        Assert.Equal(size.X, capture.Rect.X, 1e-3f);
    }

    [Fact]
    public void CounterDrawsTheCappedCountAndSkipsZeroUnlessAsked()
    {
        var capped = Draw(() => Badges.Counter(120));
        var zero = Draw(() => Badges.Counter(0));
        var shownZero = Draw(() => Badges.Counter(0, showZero: true));
        var negative = Draw(() => Badges.Counter(-3));
        var custom = Draw(() => Badges.Counter(1000, max: 9));

        Assert.Equal(BadgeSize("99+").X, capped.Rect.X, 1e-3f);
        Assert.Empty(zero.Verts);
        Assert.Equal(BadgeSize("0").X, shownZero.Rect.X, 1e-3f);
        Assert.NotEmpty(shownZero.Verts);
        Assert.Empty(negative.Verts);
        Assert.Equal(BadgeSize("9+").X, custom.Rect.X, 1e-3f);
    }

    [Fact]
    public void CounterPassesItsColourThrough()
    {
        var capture = Draw(() => Badges.Counter(4, Green));
        Assert.NotEmpty(WithColour(capture, EColor.U32(Green)));
    }
}
