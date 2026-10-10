using System.Drawing;
using TorchlightSDK.Testing;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class ToastsTests
{
    const double Forever = 1e9;
    const float Gap = 6f;

    [Theory]
    [InlineData(2.0, 1f)]
    [InlineData(0.5, 1f)]
    [InlineData(0.25, 0.5f)]
    [InlineData(0.0, 0f)]
    [InlineData(-1.0, 0f)]
    public void FadeAlphaRampsOverTheLastFadeSeconds(double remaining, float expected) => Assert.Equal(expected, ToastHost.FadeAlpha(remaining, 0.5));

    [Theory]
    [InlineData(ToastGrow.Up, 100f, true)]
    [InlineData(ToastGrow.Down, 900f, false)]
    [InlineData(ToastGrow.Default, 900f, true)]
    [InlineData(ToastGrow.Default, 100f, false)]
    [InlineData(ToastGrow.Default, 540f, false)]
    public void DefaultGrowsAwayFromTheNearestScreenEdge(ToastGrow grow, float topY, bool up) => Assert.Equal(up, ToastHost.GrowsUp(grow, topY, 1080f));

    [Theory]
    [InlineData(200f, 40f, 145f)]
    [InlineData(200f, 0f, 185f)]
    [InlineData(10f, 40f, 1f)]
    public void MessageBarWrapLeavesRoomForItsButtons(float total, float reserved, float expected) => Assert.Equal(expected, MessageBar.WrapWidth(total, 3f, 6f, reserved));

    [Theory]
    [InlineData(ToastLevel.Info, 150, 190, 240)]
    [InlineData(ToastLevel.Success, 120, 210, 120)]
    [InlineData(ToastLevel.Warning, 230, 180, 90)]
    [InlineData(ToastLevel.Error, 230, 110, 100)]
    public void AccentColourPerLevel(ToastLevel level, int r, int g, int b) => Assert.Equal(Color.FromArgb(255, r, g, b), ToastHost.Accent(level));

    [Fact]
    public void ShowDropsBlankTextAndKeepsTheNewestMax()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Max = 2 };
        host.Show("  ", style);
        host.Show(null, style);
        host.Show("one", style);
        host.Show("two", style);
        host.Show("three", style);
        Assert.Equal(2, host.Count);
        host.Clear();
        Assert.Equal(0, host.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void ShowIgnoresNullEmptyAndWhitespaceText(string? text)
    {
        var host = new ToastHost();
        host.Show(text, new ToastStyle());
        Assert.Equal(0, host.Count);
    }

    [Fact]
    public void ToastTextStartsAfterTheAccentAndPadding()
    {
        var style = new ToastStyle
        {
            Duration = Forever,
            Padding = 9f,
            AccentWidth = 7f,
            TextColor = Color.FromArgb(255, 31, 32, 33),
            Background = Color.FromArgb(255, 11, 12, 13),
        };
        var host = new ToastHost();
        host.Show("text", style);
        var verts = DrawToasts(host, style, 400f, 100f);
        var box = Extent(verts.Where(v => v.Col == EColor.U32(style.Background)));
        var text = Extent(verts.Where(v => v.Col == EColor.U32(style.TextColor)));
        var left = box.Min.X + style.AccentWidth + style.Padding;
        Assert.InRange(text.Min.X, left, left + style.TextSize / 2);
        Assert.InRange(text.Min.Y, box.Min.Y + style.Padding, box.Min.Y + style.Padding + style.TextSize);
    }

    [Fact]
    public void OnlyTheNewestMaxToastsAreDrawn()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Max = 2, Duration = Forever };
        host.Show("oldest", style, ToastLevel.Error);
        host.Show("middle", style, ToastLevel.Success);
        host.Show("newest", style, ToastLevel.Warning);
        var verts = DrawToasts(host, style, 400f, 100f);
        Assert.Empty(Bar(verts, ToastLevel.Error));
        Assert.NotEmpty(Bar(verts, ToastLevel.Success));
        Assert.NotEmpty(Bar(verts, ToastLevel.Warning));
    }

    [Fact]
    public void AnExpiredToastIsGoneAfterTheNextDraw()
    {
        var host = new ToastHost();
        var style = new ToastStyle();
        host.Show("gone", style, seconds: 0);
        host.Show("stays", style, ToastLevel.Warning, Forever);
        TestFrame.Run(() => host.Draw(960f, 80f, style));
        Assert.Equal(1, host.Count);
    }

    [Fact]
    public void CountExcludesToastsThatAlreadyExpired()
    {
        var host = new ToastHost();
        var style = new ToastStyle();
        host.Show("gone", style, seconds: 0);
        host.Show("stays", style, seconds: Forever);
        Assert.Equal(1, host.Count);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    [InlineData(double.NegativeInfinity)]
    public void ShowRejectsDurationsThatAreNotAZeroOrPositiveNumber(double seconds)
    {
        var host = new ToastHost();
        host.Show("bad", new ToastStyle(), seconds: seconds);
        host.Show("bad style", new ToastStyle { Duration = seconds });
        Assert.Equal(0, host.Count);
        Assert.Empty(DrawToasts(host, new ToastStyle(), 400f, 100f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ShowWithNoRoomKeepsNothingAndDoesNotThrow(int max)
    {
        var host = new ToastHost();
        var style = new ToastStyle { Max = max, Duration = Forever };
        host.Show("one", style);
        host.Show("two", style);
        Assert.Equal(0, host.Count);
    }

    [Fact]
    public void AnEmptyHostDrawsNothing()
    {
        Assert.Empty(DrawToasts(new ToastHost(), new ToastStyle(), 400f, 100f));
    }

    [Fact]
    public void ShowFallsBackToTheStyleDuration()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Duration = 0 };
        host.Show("styled", style);
        host.Show("explicit", style, seconds: Forever);
        TestFrame.Run(() => host.Draw(400f, 100f, style));
        Assert.Equal(1, host.Count);
    }

    [Fact]
    public void TwoHostsKeepSeparateToasts()
    {
        var style = new ToastStyle { Duration = Forever };
        var a = new ToastHost();
        var b = new ToastHost();
        a.Show("a", style);
        a.Show("a2", style);
        b.Show("b", style);
        a.Clear();
        Assert.Equal(0, a.Count);
        Assert.Equal(1, b.Count);
    }

    [Fact]
    public void ToastsFadeOutOverTheLastFadeSeconds()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Duration = 25000, FadeSeconds = 100000 };
        host.Show("fading", style);
        var accent = AccentRgb(DrawToasts(host, style, 400f, 100f), ToastLevel.Info);
        Assert.NotEmpty(accent);
        Assert.All(accent, v => Assert.InRange(v.Col >> 24, 63u, 65u));
    }

    [Fact]
    public void ToastsBeforeTheirFadeWindowAreFullyOpaque()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Duration = Forever, FadeSeconds = 0.5 };
        host.Show("solid", style);
        var accent = AccentRgb(DrawToasts(host, style, 400f, 100f), ToastLevel.Info);
        Assert.NotEmpty(accent);
        Assert.All(accent, v => Assert.Equal(255u, v.Col >> 24));
    }

    [Fact]
    public void ToastsStackDownFromTheAnchorOldestFirst()
    {
        var style = new ToastStyle { Grow = ToastGrow.Down, Duration = Forever };
        var host = new ToastHost();
        host.Show("first", style, ToastLevel.Success);
        host.Show("second", style, ToastLevel.Error);
        var verts = DrawToasts(host, style, 400f, 100f);
        var boxH = SingleRowBoxHeight(style);
        var first = Extent(Bar(verts, ToastLevel.Success));
        var second = Extent(Bar(verts, ToastLevel.Error));
        Assert.Equal(100f, first.Min.Y);
        Assert.Equal(100f + boxH, first.Max.Y);
        Assert.Equal(first.Max.Y + Gap, second.Min.Y);
        Assert.Equal(second.Min.Y + boxH, second.Max.Y);
    }

    [Fact]
    public void ToastsStackUpFromTheAnchorNewestNearest()
    {
        var style = new ToastStyle { Grow = ToastGrow.Up, Duration = Forever };
        var host = new ToastHost();
        host.Show("first", style, ToastLevel.Success);
        host.Show("second", style, ToastLevel.Error);
        var verts = DrawToasts(host, style, 400f, 500f);
        var boxH = SingleRowBoxHeight(style);
        var newest = Extent(Bar(verts, ToastLevel.Error));
        var oldest = Extent(Bar(verts, ToastLevel.Success));
        Assert.Equal(500f, newest.Max.Y);
        Assert.Equal(500f - boxH, newest.Min.Y);
        Assert.Equal(newest.Min.Y - Gap, oldest.Max.Y);
        Assert.Equal(oldest.Max.Y - boxH, oldest.Min.Y);
    }

    [Fact]
    public void DefaultGrowGoesUpFromTheLowerHalfOfTheScreen()
    {
        var style = new ToastStyle { Grow = ToastGrow.Default, Duration = Forever };
        var host = new ToastHost();
        host.Show("low", style, ToastLevel.Success);
        var lowY = UiFrame.Io().DisplaySize.Y * 0.75f;
        var low = Extent(Bar(DrawToasts(host, style, 400f, lowY), ToastLevel.Success));
        Assert.Equal(lowY, low.Max.Y);
        var highY = UiFrame.Io().DisplaySize.Y * 0.25f;
        var high = Extent(Bar(DrawToasts(host, style, 400f, highY), ToastLevel.Success));
        Assert.Equal(highY, high.Min.Y);
    }

    [Fact]
    public void ToastsAreCentredOnTheAnchorAndTheAccentIsAccentWidthWide()
    {
        var style = new ToastStyle { Duration = Forever, AccentWidth = 7f };
        var host = new ToastHost();
        host.Show("centred", style, ToastLevel.Info);
        var verts = DrawToasts(host, style, 400f, 100f);
        var bar = Extent(Bar(verts, ToastLevel.Info));
        Assert.Equal(7f, bar.Max.X - bar.Min.X);
        var bg = Extent(verts.Where(v => v.Col == BackgroundU32(style)));
        Assert.Equal(400f, (bg.Min.X + bg.Max.X) / 2f, 0.01f);
        Assert.Equal(bar.Min.X, bg.Min.X);
    }

    [Fact]
    public void LongTextWrapsToMaxWidthAndGrowsTheBox()
    {
        var style = new ToastStyle { Duration = Forever, MaxWidth = 120f };
        var host = new ToastHost();
        host.Show("a toast long enough that it has to wrap onto a second row", style, ToastLevel.Info);
        var bg = Extent(DrawToasts(host, style, 400f, 100f).Where(v => v.Col == BackgroundU32(style)));
        Assert.True(bg.Max.X - bg.Min.X <= style.MaxWidth + 0.01f);
        Assert.True(bg.Max.Y - bg.Min.Y > SingleRowBoxHeight(style));
        var rows = (bg.Max.Y - bg.Min.Y - style.Padding * 2) / LineHeight(style);
        Assert.True(rows >= 2f);
        Assert.Equal(MathF.Round(rows), rows, 0.001f);
    }

    [Fact]
    public void ZeroMaxWidthNeverWraps()
    {
        var style = new ToastStyle { Duration = Forever, MaxWidth = 0f };
        var host = new ToastHost();
        host.Show("a toast long enough that it would wrap if it had a maximum width at all", style, ToastLevel.Info);
        var bg = Extent(DrawToasts(host, style, 1000f, 100f).Where(v => v.Col == BackgroundU32(style)));
        Assert.Equal(SingleRowBoxHeight(style), bg.Max.Y - bg.Min.Y);
    }

    [Fact]
    public void BackgroundBorderAndTextUseTheirOwnStyleColours()
    {
        var style = new ToastStyle
        {
            Duration = Forever,
            Background = Color.FromArgb(200, 11, 12, 13),
            Border = Color.FromArgb(210, 21, 22, 23),
            TextColor = Color.FromArgb(220, 31, 32, 33),
            BorderThickness = 2f,
        };
        var host = new ToastHost();
        host.Show("coloured", style);
        var verts = DrawToasts(host, style, 400f, 100f);
        Assert.Contains(verts, v => v.Col == EColor.U32(style.Background));
        Assert.Contains(verts, v => v.Col == EColor.U32(style.Border));
        Assert.Contains(verts, v => v.Col == EColor.U32(style.TextColor));
    }

    [Fact]
    public void ZeroBorderThicknessAndTransparentBackgroundDrawNothingForThem()
    {
        var style = new ToastStyle
        {
            Duration = Forever,
            Background = Color.FromArgb(0, 11, 12, 13),
            Border = Color.FromArgb(210, 21, 22, 23),
            BorderThickness = 0f,
        };
        var host = new ToastHost();
        host.Show("bare", style);
        var verts = DrawToasts(host, style, 400f, 100f);
        Assert.DoesNotContain(verts, v => (v.Col & 0xFFFFFF) == RgbOf(style.Border));
        Assert.DoesNotContain(verts, v => (v.Col & 0xFFFFFF) == RgbOf(style.Background));
        Assert.NotEmpty(Bar(verts, ToastLevel.Info));
    }

    [Fact]
    public void TextSizeSetsTheRowHeight()
    {
        var small = new ToastStyle { Duration = Forever, TextSize = 12f };
        var large = new ToastStyle { Duration = Forever, TextSize = 30f };
        var host = new ToastHost();
        host.Show("size", small);
        var smallBar = Extent(Bar(DrawToasts(host, small, 400f, 100f), ToastLevel.Info));
        Assert.Equal(SingleRowBoxHeight(small), smallBar.Max.Y - smallBar.Min.Y);
        var largeBar = Extent(Bar(DrawToasts(host, large, 400f, 100f), ToastLevel.Info));
        Assert.Equal(SingleRowBoxHeight(large), largeBar.Max.Y - largeBar.Min.Y);
    }

    [Fact]
    public void ToastsAndBarsDrawOneBalancedFrame()
    {
        var host = new ToastHost();
        var style = new ToastStyle { Grow = ToastGrow.Up, MaxWidth = 120f };
        host.Show("a toast long enough that it has to wrap onto a second row", style, ToastLevel.Error);
        var open = true;
        var clicked = false;
        UiFrame.Run("test", () =>
        {
            host.Draw(960f, 900f, style);
            clicked |= MessageBar.Draw("info", ToastLevel.Info, "100% fine");
            clicked |= MessageBar.Draw("warn", ToastLevel.Warning, "Something to fix", ref open, "Fix it");
        });
        Assert.False(clicked);
        Assert.True(open);
    }

    [Fact]
    public void MessageBarFillsTheContentWidthOrTheGivenWidth()
    {
        Vector2 fill = default, given = default;
        var windowPadX = 0f;
        UiFrame.Run("bar width", () =>
        {
            MessageBar.Draw("fill", ToastLevel.Info, "text");
            fill = ImGui.GetItemRectSize();
            MessageBar.Draw("given", ToastLevel.Info, "text", width: 123f);
            given = ImGui.GetItemRectSize();
            windowPadX = ImGui.GetStyle().WindowPadding.X;
        });
        Assert.Equal(UiFrame.WindowSize.X - windowPadX * 2, fill.X, 0.01f);
        Assert.Equal(123f, given.X);
        Assert.Equal(fill.Y, given.Y);
    }

    [Fact]
    public void AMessageBarWithButtonsIsAtLeastOneButtonRowTall()
    {
        float plain = 0, withAction = 0, frameH = 0, padY = 0, textH = 0;
        UiFrame.Run("bar height", () =>
        {
            MessageBar.Draw("plain", ToastLevel.Info, "text");
            plain = ImGui.GetItemRectSize().Y;
            MessageBar.Draw("act", ToastLevel.Info, "text", "Go");
            withAction = ImGui.GetItemRectSize().Y;
            frameH = ImGui.GetFrameHeight();
            padY = ImGui.GetStyle().FramePadding.Y;
            textH = ImGui.CalcTextSize("text").Y;
        });
        Assert.Equal(textH + padY * 2, plain, 0.01f);
        Assert.Equal(frameH + padY * 2, withAction, 0.01f);
    }

    [Fact]
    public void LongMessageBarTextWrapsAndGrowsTheBar()
    {
        float shortH = 0, longH = 0, expected = 0;
        const string longText = "a message long enough that it has to wrap onto several rows of a narrow bar";
        UiFrame.Run("bar wrap", () =>
        {
            MessageBar.Draw("short", ToastLevel.Info, "short", width: 150f);
            shortH = ImGui.GetItemRectSize().Y;
            MessageBar.Draw("long", ToastLevel.Info, longText, width: 150f);
            longH = ImGui.GetItemRectSize().Y;
            var style = ImGui.GetStyle();
            var wrap = MessageBar.WrapWidth(150f, 3f, style.FramePadding.X + 2f, 0f);
            expected = ImGui.CalcTextSize(longText, false, wrap).Y + style.FramePadding.Y * 2;
        });
        Assert.True(longH > shortH);
        Assert.Equal(expected, longH, 0.01f);
    }

    [Fact]
    public void MessageBarColoursComeFromTheLevel()
    {
        foreach (var level in Enum.GetValues<ToastLevel>())
        {
            List<ImDrawVert> verts = [];
            UiFrame.Run("bar colours", () =>
            {
                var list = ImGui.GetWindowDrawList();
                var from = list.VtxBuffer.Size;
                MessageBar.Draw("c", level, "text");
                for (var i = from; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
            });
            var accent = ToastHost.Accent(level);
            Assert.Contains(verts, v => v.Col == EColor.U32(accent));
            Assert.Contains(verts, v => v.Col == EColor.U32(EColor.Fade(accent, 0.14f)));
            Assert.Contains(verts, v => v.Col == EColor.U32(EColor.Fade(accent, 0.5f)));
        }
    }

    [Fact]
    public void ADismissedMessageBarDrawsNothingAndTakesNoSpace()
    {
        var open = false;
        var clicked = true;
        float before = 0, after = 0;
        UiFrame.Run("bar closed", () =>
        {
            before = ImGui.GetCursorScreenPos().Y;
            clicked = MessageBar.Draw("closed", ToastLevel.Info, "text", ref open, "Go");
            after = ImGui.GetCursorScreenPos().Y;
        });
        Assert.False(clicked);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ClickingTheActionReturnsTrueAndTheCloseButtonClearsOpen()
    {
        var open = true;
        var action = false;
        Vector2 xCentre = default, actionCentre = default;
        void Body()
        {
            var org = ImGui.GetCursorScreenPos();
            action |= MessageBar.Draw("bar", ToastLevel.Info, "text", ref open, "Go");
            var style = ImGui.GetStyle();
            var size = ImGui.GetItemRectSize();
            var pad = style.FramePadding.X + 2f;
            var xW = ImGui.GetFrameHeight();
            var actW = ImGui.CalcTextSize("Go").X + style.FramePadding.X * 2;
            var y = org.Y + size.Y / 2;
            xCentre = new Vector2(org.X + size.X - pad - xW / 2, y);
            actionCentre = new Vector2(org.X + size.X - pad - xW - style.ItemSpacing.X - actW / 2, y);
        }
        UiFrame.Run("bar click", Body);
        try
        {
            UiFrame.Click("bar click", actionCentre, Body);
            Assert.True(action);
            Assert.True(open);
            UiFrame.Click("bar click", xCentre, Body);
            Assert.False(open);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void MessageBarButtonsSitInTheirOwnRowsWithSpacingBetweenThem()
    {
        var open = true;
        var action = false;
        var org = Vector2.Zero;
        var size = Vector2.Zero;
        void Body()
        {
            org = ImGui.GetCursorScreenPos();
            action |= MessageBar.Draw("bar", ToastLevel.Info, "text", ref open, "Go");
            size = ImGui.GetItemRectSize();
        }
        UiFrame.Run("bar rows", Body);
        var style = ImGui.GetStyle();
        var pad = style.FramePadding.X + 2f;
        var xW = ImGui.GetFrameHeight();
        var actW = ImGui.CalcTextSize("Go").X + style.FramePadding.X * 2;
        var xLeft = org.X + size.X - pad - xW;
        var actionRight = xLeft - style.ItemSpacing.X;
        var actionLeft = actionRight - actW;
        var middle = org.Y + size.Y / 2;
        try
        {
            UiFrame.Click("bar rows", new Vector2(actionLeft + 1f, middle), Body);
            Assert.True(action);
            action = false;
            UiFrame.Click("bar rows", new Vector2(actionRight - 1f, middle), Body);
            Assert.True(action);
            action = false;
            UiFrame.Click("bar rows", new Vector2(actionRight + style.ItemSpacing.X / 2f, middle), Body);
            Assert.False(action);
            Assert.True(open);
            UiFrame.Click("bar rows", new Vector2(actionLeft + actW / 2f, org.Y + style.FramePadding.Y / 2f), Body);
            Assert.False(action);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void MessageBarTextIsWrappedInsideTheBarAndStartsAfterTheAccent()
    {
        var org = Vector2.Zero;
        var size = Vector2.Zero;
        List<ImDrawVert> verts = [];
        uint textCol = 0;
        float padX = 0;
        UiFrame.Run("bar text", () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            org = ImGui.GetCursorScreenPos();
            MessageBar.Draw("t", ToastLevel.Info, "a message long enough that it has to wrap onto several rows of a narrow bar", width: 150f);
            size = ImGui.GetItemRectSize();
            for (var i = from; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
            textCol = ImGui.GetColorU32(ImGuiCol.Text);
            padX = ImGui.GetStyle().FramePadding.X + 2f;
        });
        var text = Extent(verts.Where(v => v.Col == textCol));
        Assert.InRange(text.Min.X, org.X + 3f + padX, org.X + 3f + padX + ImGui.GetFontSize());
        Assert.True(text.Max.X <= org.X + size.X);
        Assert.True(text.Max.Y <= org.Y + size.Y);
    }

    [Fact]
    public void TheSameBarIdUnderTwoScopesKeepsItsButtonsApart()
    {
        bool firstClicked = false, secondClicked = false;
        var firstOrg = Vector2.Zero;
        var secondOrg = Vector2.Zero;
        float total = 0;
        void Body()
        {
            ImGui.PushID("one");
            firstOrg = ImGui.GetCursorScreenPos();
            firstClicked |= MessageBar.Draw("bar", ToastLevel.Info, "text", "Go");
            total = ImGui.GetItemRectSize().X;
            ImGui.PopID();
            ImGui.PushID("two");
            secondOrg = ImGui.GetCursorScreenPos();
            secondClicked |= MessageBar.Draw("bar", ToastLevel.Info, "text", "Go");
            ImGui.PopID();
        }
        UiFrame.Run("bar scopes", Body);
        var style = ImGui.GetStyle();
        var actW = ImGui.CalcTextSize("Go").X + style.FramePadding.X * 2;
        var centreX = firstOrg.X + total - (style.FramePadding.X + 2f) - actW / 2;
        var h = ImGui.GetFrameHeight() + style.FramePadding.Y * 2;
        try
        {
            UiFrame.Click("bar scopes", new Vector2(centreX, secondOrg.Y + h / 2), Body);
            Assert.True(secondClicked);
            Assert.False(firstClicked);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void TwoBarsWithDifferentIdsDismissIndependently()
    {
        bool firstOpen = true, secondOpen = true;
        Vector2 firstOrg = default, secondOrg = default;
        float total = 0;
        void Body()
        {
            firstOrg = ImGui.GetCursorScreenPos();
            MessageBar.Draw("a", ToastLevel.Info, "text", ref firstOpen);
            total = ImGui.GetItemRectSize().X;
            secondOrg = ImGui.GetCursorScreenPos();
            MessageBar.Draw("b", ToastLevel.Info, "text", ref secondOpen);
        }
        UiFrame.Run("bar dismiss", Body);
        var style = ImGui.GetStyle();
        var xW = ImGui.GetFrameHeight();
        var centreX = firstOrg.X + total - (style.FramePadding.X + 2f) - xW / 2;
        var h = xW + style.FramePadding.Y * 2;
        try
        {
            UiFrame.Click("bar dismiss", new Vector2(centreX, secondOrg.Y + h / 2), Body);
            Assert.True(firstOpen);
            Assert.False(secondOpen);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    static float LineHeight(ToastStyle style) => MathF.Ceiling(style.TextSize) + 4f;

    static float SingleRowBoxHeight(ToastStyle style) => LineHeight(style) + style.Padding * 2;

    static uint RgbOf(Color c) => EColor.U32(c) & 0xFFFFFF;

    static uint BackgroundU32(ToastStyle style) => EColor.U32(EColor.Fade(style.Background, style.Background.A / 255f));

    static List<ImDrawVert> DrawToasts(ToastHost host, ToastStyle style, float anchorX, float topY)
    {
        List<ImDrawVert> verts = [];
        TestFrame.Run(() =>
        {
            var list = ImGui.GetForegroundDrawList();
            var from = list.VtxBuffer.Size;
            host.Draw(anchorX, topY, style);
            for (var i = from; i < list.VtxBuffer.Size; i++) verts.Add(list.VtxBuffer[i]);
        });
        return verts;
    }

    static List<ImDrawVert> Bar(List<ImDrawVert> verts, ToastLevel level) => verts.Where(v => v.Col == EColor.U32(ToastHost.Accent(level))).ToList();

    static List<ImDrawVert> AccentRgb(List<ImDrawVert> verts, ToastLevel level) => verts.Where(v => (v.Col & 0xFFFFFF) == RgbOf(ToastHost.Accent(level))).ToList();

    static (Vector2 Min, Vector2 Max) Extent(IEnumerable<ImDrawVert> verts)
    {
        var points = verts.Select(v => v.Pos).ToList();
        Assert.NotEmpty(points);
        return (new Vector2(points.Min(p => p.X), points.Min(p => p.Y)), new Vector2(points.Max(p => p.X), points.Max(p => p.Y)));
    }
}
