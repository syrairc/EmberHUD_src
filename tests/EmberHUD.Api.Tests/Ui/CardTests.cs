using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class CardTests
{
    static float Luma(Vector4 c) => c.X * 0.299f + c.Y * 0.587f + c.Z * 0.114f;

    [Fact]
    public void AFilledCardClearsACollidingFrameBg()
    {
        var frame = new Vector4(0.17f, 0.17f, 0.17f, 1f);
        var fill = Card.SurfaceFill(new Vector4(0.12f, 0.12f, 0.12f, 1f), frame);
        Assert.True(Luma(fill) - Luma(frame) >= 0.049f);
    }

    [Fact]
    public void ADistantFrameBgNeedsNoExtraPush()
    {
        var window = new Vector4(0.12f, 0.12f, 0.12f, 1f);
        var fill = Card.SurfaceFill(window, new Vector4(0.02f, 0.02f, 0.02f, 1f));
        Assert.True(Luma(fill) > Luma(window));
        Assert.Equal(fill, Card.SurfaceFill(window, new Vector4(0f, 0f, 0f, 1f)));
    }

    [Fact]
    public void ALightThemeFillsDownwardPastTheFrame() =>
        Assert.True(Luma(Card.SurfaceFill(new Vector4(0.95f, 0.95f, 0.95f, 1f), new Vector4(0.9f, 0.9f, 0.9f, 1f))) < 0.9f);

    [Fact]
    public void EveryCardRightAlignsItsOwnHeaderAction()
    {
        float a = 0, b = 0;
        for (var frame = 0; frame < 3; frame++)
            UiFrame.Run("test", () =>
            {
                using (Card.Surface("a", width: 300f))
                    Card.Header("A", action: () =>
                    {
                        ImGui.Button("x");
                        a = ImGui.GetItemRectMax().X;
                    });
                using (Card.Surface("b", width: 300f))
                    Card.Header("B", action: () =>
                    {
                        ImGui.Button("a much longer label");
                        b = ImGui.GetItemRectMax().X;
                    });
            });
        Assert.Equal(a, b, 1f);
    }

    [Fact]
    public void CardsDrawOneBalancedFrameAndChangeNothingUntouched()
    {
        List<string> items = ["First", "Second"];
        var changed = false;
        UiFrame.Run("test", () =>
        {
            foreach (var look in Enum.GetValues<CardLook>())
                using (Card.Surface(look.ToString(), look))
                {
                    Card.Header(look.ToString(), "a 100% plain description");
                    Card.Footer(() => ImGui.TextUnformatted("footer"));
                }
            if (Card.BeginCard("collapsible", "Collapsible", () => ImGui.SmallButton("x"), () => ImGui.MenuItem("Menu"), defaultOpen: true))
                ImGui.TextUnformatted("body");
            Card.EndCard();
            changed |= Card.List("list", items, s => s, (s, _) =>
            {
                ImGui.TextUnformatted(s);
                return false;
            }, AddSource<string>.Button(() => "New"), reorderable: true, confirmDelete: true, filterText: s => s);
        });
        Assert.False(changed);
        Assert.Equal(2, items.Count);
    }

    const string Window = "card";

    readonly float FrameHeight = UiFrame.Measure(() => ImGui.GetFrameHeight());

    readonly float FontSize = UiFrame.Measure(() => ImGui.GetFontSize());

    readonly Vector2 Spacing = UiFrame.Measure(() => ImGui.GetStyle().ItemSpacing);

    readonly Vector2 Padding = UiFrame.Measure(() => ImGui.GetStyle().FramePadding);

    readonly float BaseAlpha = UiFrame.Measure(() => ImGui.GetStyle().Alpha);

    readonly float ChildRounding = UiFrame.Measure(() => ImGui.GetStyle().ChildRounding);

    readonly Vector2 WindowPadding = UiFrame.Measure(() => ImGui.GetStyle().WindowPadding);

    readonly Vector4 ChildBg = UiFrame.Measure(() => ImGui.GetStyle().Colors[(int)ImGuiCol.ChildBg]);

    readonly Vector4 WindowBg = UiFrame.Measure(() => ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg]);

    readonly Vector4 FrameBg = UiFrame.Measure(() => ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);

    static void Frames(Action body)
    {
        for (var i = 0; i < 3; i++) UiFrame.Run(Window, body);
    }

    static bool EndsWith(List<Vector2> all, List<Vector2> tail) => all.Count >= tail.Count && all.Skip(all.Count - tail.Count).SequenceEqual(tail);

    sealed class SurfaceRig
    {
        public string Id = UiFrame.NextId("surface");
        public CardLook Look = CardLook.Filled;
        public float Width;
        public float Padding = 10f;
        public Action? Body;
        public Vector2 Origin;
        public Vector2 Size;
        public float AvailBefore;
        public uint ProbeBefore;
        public uint ProbeInside;
        public uint ProbeAfter;
        public float BorderSize;
        public Vector4 ChildBgInside;
        public Vector4 ChildBgAfter;
        public Vector2 PaddingInside;
        public Vector2 PaddingAfter;
        public float RoundingInside;
        public float RoundingAfter;

        public void Draw()
        {
            Origin = ImGui.GetCursorScreenPos();
            AvailBefore = ImGui.GetContentRegionAvail().X;
            ProbeBefore = ImGui.GetID("probe");
            using (Card.Surface(Id, Look, Width, Padding))
            {
                ProbeInside = ImGui.GetID("probe");
                BorderSize = ImGuiP.GetCurrentWindow().WindowBorderSize;
                var style = ImGui.GetStyle();
                ChildBgInside = style.Colors[(int)ImGuiCol.ChildBg];
                PaddingInside = style.WindowPadding;
                RoundingInside = style.ChildRounding;
                if (Body is null) ImGui.TextUnformatted("content");
                else Body();
            }
            Size = ImGui.GetItemRectSize();
            ProbeAfter = ImGui.GetID("probe");
            var after = ImGui.GetStyle();
            ChildBgAfter = after.Colors[(int)ImGuiCol.ChildBg];
            PaddingAfter = after.WindowPadding;
            RoundingAfter = after.ChildRounding;
        }
    }

    [Theory]
    [InlineData(CardLook.Outline, 0f, 10f)]
    [InlineData(CardLook.Filled, 0f, 10f)]
    [InlineData(CardLook.Subtle, 0f, 10f)]
    [InlineData(CardLook.Outline, 300f, 4f)]
    [InlineData(CardLook.Filled, 300f, 4f)]
    [InlineData(CardLook.Subtle, 300f, 4f)]
    public void SurfaceIsScopedUnderItsIdAndBalancesTheIdStackForEveryVariant(CardLook look, float width, float padding)
    {
        var a = new SurfaceRig { Look = look, Width = width, Padding = padding };
        var b = new SurfaceRig { Look = look, Width = width, Padding = padding };
        UiFrame.Run(Window, () =>
        {
            a.Draw();
            b.Draw();
        });
        Assert.Equal(a.ProbeBefore, a.ProbeAfter);
        Assert.Equal(b.ProbeBefore, b.ProbeAfter);
        Assert.NotEqual(a.ProbeBefore, a.ProbeInside);
        Assert.NotEqual(a.ProbeInside, b.ProbeInside);
    }

    [Fact]
    public void TheSameSurfaceIdUnderTwoScopesGivesDifferentInnerIds()
    {
        var a = new SurfaceRig();
        var b = new SurfaceRig { Id = a.Id };
        UiFrame.Run(Window, () => UiFrame.TwoScopes(a.Draw, b.Draw));
        Assert.NotEqual(a.ProbeInside, b.ProbeInside);
    }

    [Theory]
    [InlineData(CardLook.Outline)]
    [InlineData(CardLook.Filled)]
    [InlineData(CardLook.Subtle)]
    public void SurfacePushesItsLookInsideAndRestoresEverythingAfter(CardLook look)
    {
        var rig = new SurfaceRig { Look = look, Padding = 7f };
        Frames(rig.Draw);
        Assert.Equal(new Vector2(7f), rig.PaddingInside);
        Assert.Equal(6f, rig.RoundingInside);
        Assert.Equal(look == CardLook.Filled ? Card.SurfaceFill(WindowBg, FrameBg) : ChildBg, rig.ChildBgInside);
        Assert.Equal(WindowPadding, rig.PaddingAfter);
        Assert.Equal(ChildRounding, rig.RoundingAfter);
        Assert.Equal(ChildBg, rig.ChildBgAfter);
    }

    [Theory]
    [InlineData(CardLook.Outline, true)]
    [InlineData(CardLook.Filled, true)]
    [InlineData(CardLook.Subtle, false)]
    public void OnlySubtleDrawsNoBorder(CardLook look, bool border)
    {
        var rig = new SurfaceRig { Look = look };
        Frames(rig.Draw);
        Assert.Equal(border, rig.BorderSize > 0f);
    }

    [Theory]
    [InlineData(0f, 10f)]
    [InlineData(300f, 4f)]
    public void SurfaceHugsItsContentHeightAndTakesTheGivenOrAvailableWidth(float width, float padding)
    {
        var rig = new SurfaceRig { Look = CardLook.Outline, Width = width, Padding = padding };
        Frames(rig.Draw);
        Assert.Equal(width > 0f ? width : rig.AvailBefore, rig.Size.X);
        Assert.Equal(FontSize + padding * 2, rig.Size.Y);
    }

    [Fact]
    public void SurfaceBodyStartsAPaddingInsideTheBox()
    {
        var start = Vector2.Zero;
        var rig = new SurfaceRig { Look = CardLook.Outline, Padding = 9f, Body = () => start = ImGui.GetCursorScreenPos() };
        Frames(rig.Draw);
        Assert.Equal(rig.Origin + new Vector2(9f), start);
    }

    readonly record struct HeaderInfo(float ActionRight, float WorkRight, float Top, float Bottom, float CardHeight);

    static HeaderInfo DrawHeader(string id, string? title, string? description, Action? action, float width = 300f)
    {
        var info = default(HeaderInfo);
        var actionRight = 0f;
        var rig = new SurfaceRig
        {
            Id = id,
            Look = CardLook.Subtle,
            Width = width,
            Body = () =>
            {
                var top = ImGui.GetCursorScreenPos().Y;
                Card.Header(title, description, action is null ? null : () =>
                {
                    action();
                    actionRight = ImGui.GetItemRectMax().X;
                });
                info = info with { ActionRight = actionRight, WorkRight = ImGuiP.GetCurrentWindow().ContentRegionRect.Max.X, Top = top, Bottom = ImGui.GetCursorScreenPos().Y };
            }
        };
        for (var i = 0; i < 3; i++) UiFrame.Run(Window, rig.Draw);
        return info with { CardHeight = rig.Size.Y };
    }

    [Fact]
    public void HeaderActionSitsFlushWithTheCardsInnerRightEdge()
    {
        var info = DrawHeader(UiFrame.NextId("flush"), "Title", null, () => ImGui.Button("act"));
        Assert.Equal(info.WorkRight, info.ActionRight, 0.5f);
    }

    [Fact]
    public void HeaderActionsOfDifferentWidthsInDifferentCardsAreEachFlush()
    {
        var narrow = DrawHeader(UiFrame.NextId("narrow"), "A", null, () => ImGui.Button("x"));
        var wide = DrawHeader(UiFrame.NextId("wide"), "B", null, () => ImGui.Button("a much longer label"));
        Assert.Equal(narrow.WorkRight, narrow.ActionRight, 0.5f);
        Assert.Equal(wide.WorkRight, wide.ActionRight, 0.5f);
    }

    [Fact]
    public void TheSameCardIdUnderTwoScopesKeepsEachHeadersOwnActionWidth()
    {
        float a = 0, b = 0, workA = 0, workB = 0;
        var id = UiFrame.NextId("scoped");
        SurfaceRig Make(Action action, Action<float, float> record) => new()
        {
            Id = id,
            Width = 300f,
            Look = CardLook.Subtle,
            Body = () => Card.Header("T", null, () =>
            {
                action();
                record(ImGui.GetItemRectMax().X, ImGuiP.GetCurrentWindow().ContentRegionRect.Max.X);
            })
        };
        var first = Make(() => ImGui.Button("x"), (r, w) => (a, workA) = (r, w));
        var second = Make(() => ImGui.Button("a much longer label"), (r, w) => (b, workB) = (r, w));
        for (var i = 0; i < 3; i++) UiFrame.Run(Window, () => UiFrame.TwoScopes(first.Draw, second.Draw));
        Assert.Equal(workA, a, 0.5f);
        Assert.Equal(workB, b, 0.5f);
    }

    [Fact]
    public void HeaderRunsItsActionOncePerFrameAndNullAddsNoRow()
    {
        var calls = 0;
        UiFrame.Run(Window, () => Card.Header("T", null, () => calls++));
        Assert.Equal(1, calls);
        var before = Vector2.Zero;
        var after = Vector2.Zero;
        UiFrame.Run(Window, () =>
        {
            before = ImGui.GetCursorScreenPos();
            Card.Header("T", null, null);
            after = ImGui.GetCursorScreenPos();
        });
        Assert.Equal(before.Y + FontSize + Spacing.Y, after.Y);
    }

    [Fact]
    public void HeaderTitleIsDrawnAsPlainText()
    {
        const string title = "50%d Title %s";
        var width = 0f;
        var expected = 0f;
        UiFrame.Run(Window, () =>
        {
            expected = ImGui.CalcTextSize(title).X;
            Card.Header(title);
            width = ImGui.GetItemRectSize().X;
        });
        Assert.Equal(expected, width, 0.01f);
    }

    [Fact]
    public void HeaderWithoutDescriptionOrWithAnEmptyOneDrawsOnlyTheTitleLineAndADescriptionAddsOne()
    {
        var oneLine = DrawHeader(UiFrame.NextId("h"), "Title", null, null);
        var empty = DrawHeader(UiFrame.NextId("h"), "Title", "", null);
        var noTitle = DrawHeader(UiFrame.NextId("h"), null, null, null);
        var described = DrawHeader(UiFrame.NextId("h"), "Title", "desc", null);
        Assert.Equal(FontSize + Spacing.Y, oneLine.Bottom - oneLine.Top, 0.01f);
        Assert.Equal(oneLine.Bottom - oneLine.Top, empty.Bottom - empty.Top, 0.01f);
        Assert.Equal(oneLine.Bottom - oneLine.Top, noTitle.Bottom - noTitle.Top, 0.01f);
        Assert.Equal(2 * (FontSize + Spacing.Y), described.Bottom - described.Top, 0.01f);
    }

    [Fact]
    public void HeaderDrawsTheTitleInTheTextColourAndTheDescriptionDimmedAndRestoresTheColour()
    {
        uint textColor = 0, dimColor = 0;
        List<uint> titleColors = [];
        List<uint> describedColors = [];
        var restored = false;
        UiFrame.Run(Window, () =>
        {
            textColor = ImGui.GetColorU32(ImGuiCol.Text);
            dimColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            var list = ImGui.GetWindowDrawList();
            var mark = list.VtxBuffer.Size;
            Card.Header("Title");
            for (var i = mark; i < list.VtxBuffer.Size; i++) titleColors.Add(list.VtxBuffer[i].Col);
            mark = list.VtxBuffer.Size;
            Card.Header("Title", "desc");
            for (var i = mark; i < list.VtxBuffer.Size; i++) describedColors.Add(list.VtxBuffer[i].Col);
            restored = ImGui.GetColorU32(ImGuiCol.Text) == textColor;
        });
        Assert.NotEqual(textColor, dimColor);
        Assert.All(titleColors, c => Assert.Equal(textColor, c));
        Assert.Contains(textColor, describedColors);
        Assert.Contains(dimColor, describedColors);
        Assert.True(restored);
    }

    [Fact]
    public void HeaderDescriptionIsPlainTextSoAPercentSignDoesNotEatIt()
    {
        const string text = "100%d done %s";
        var width = 0f;
        var expected = 0f;
        UiFrame.Run(Window, () =>
        {
            expected = ImGui.CalcTextSize(text).X;
            Card.Header("T", text);
            width = ImGui.GetItemRectSize().X;
        });
        Assert.Equal(expected, width, 0.01f);
    }

    [Fact]
    public void HeaderDescriptionWrapsAtTheCardsInnerEdge()
    {
        var longText = string.Join(' ', Enumerable.Repeat("wrapping words", 40));
        var wrapped = DrawHeader(UiFrame.NextId("w"), "T", longText, null, 200f);
        var oneLine = DrawHeader(UiFrame.NextId("w"), "T", "short", null, 200f);
        Assert.True(wrapped.CardHeight > oneLine.CardHeight + 2 * FontSize);
    }

    static float Footer(Action? body)
    {
        var delta = 0f;
        UiFrame.Run(Window, () =>
        {
            var start = ImGui.GetCursorScreenPos().Y;
            Card.Footer(body);
            delta = ImGui.GetCursorScreenPos().Y - start;
        });
        return delta;
    }

    [Fact]
    public void FooterWithNoBodyDrawsNothing() => Assert.Equal(0f, Footer(null));

    [Fact]
    public void FooterPutsARuleBetweenTwoGapsAboveItsBody()
    {
        var bodyOffset = 0f;
        UiFrame.Run(Window, () =>
        {
            var start = ImGui.GetCursorScreenPos().Y;
            Card.Footer(() => bodyOffset = ImGui.GetCursorScreenPos().Y - start);
        });
        Assert.Equal(2f + Spacing.Y + 0f + Spacing.Y + 2f + Spacing.Y, bodyOffset, 0.01f);
    }

    [Fact]
    public void FooterRunsItsBodyOnce()
    {
        var calls = 0;
        UiFrame.Run(Window, () => Card.Footer(() => calls++));
        Assert.Equal(1, calls);
    }

    sealed class CardRig
    {
        public string Id = UiFrame.NextId("card");
        public string Title = "Title";
        public bool? SetOpen;
        public bool DefaultOpen;
        public Action? Trailing;
        public Action? Menu;
        public Action? Body;
        public bool Open;
        public Vector2 Origin;
        public Vector2 Size;
        public Vector2 BodyStart;
        public Vector2 CursorAfter;
        public float WorkRight;
        public float BorderSize;
        public Vector2 PaddingInside;
        public uint ProbeBefore;
        public uint ProbeInside;
        public uint ProbeAfter;

        public void Draw()
        {
            Origin = ImGui.GetCursorScreenPos();
            ProbeBefore = ImGui.GetID("probe");
            Open = Card.BeginCard(Id, Title, Trailing, Menu, SetOpen, DefaultOpen);
            WorkRight = ImGuiP.GetCurrentWindow().ContentRegionRect.Max.X;
            BorderSize = ImGuiP.GetCurrentWindow().WindowBorderSize;
            PaddingInside = ImGui.GetStyle().WindowPadding;
            BodyStart = ImGui.GetCursorScreenPos();
            ProbeInside = ImGui.GetID("probe");
            if (Open)
            {
                ImGui.TextUnformatted("body");
                Body?.Invoke();
            }
            Card.EndCard();
            Size = ImGui.GetItemRectSize();
            CursorAfter = ImGui.GetCursorScreenPos();
            ProbeAfter = ImGui.GetID("probe");
        }
    }

    Vector2 CardHeader(CardRig rig) => rig.Origin + new Vector2(40f, 1f + FrameHeight / 2);

    [Theory]
    [InlineData(false, null, false, false)]
    [InlineData(true, null, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, null, true, false)]
    [InlineData(false, null, false, true)]
    [InlineData(true, true, true, true)]
    public void BeginCardIsScopedUnderItsIdAndBalancesTheIdStackForEveryVariant(bool defaultOpen, bool? setOpen, bool trailing, bool menu)
    {
        CardRig Make() => new() { DefaultOpen = defaultOpen, SetOpen = setOpen, Trailing = trailing ? () => ImGui.SmallButton("x") : null, Menu = menu ? () => ImGui.MenuItem("m") : null };
        var a = Make();
        var b = Make();
        UiFrame.Run(Window, () =>
        {
            a.Draw();
            b.Draw();
        });
        Assert.Equal(setOpen ?? defaultOpen, a.Open);
        Assert.Equal(a.ProbeBefore, a.ProbeAfter);
        Assert.Equal(b.ProbeBefore, b.ProbeAfter);
        Assert.NotEqual(a.ProbeBefore, a.ProbeInside);
        Assert.NotEqual(a.ProbeInside, b.ProbeInside);
    }

    [Fact]
    public void TheSameCardIdUnderTwoScopesGivesDifferentCardInnerIds()
    {
        var a = new CardRig();
        var b = new CardRig { Id = a.Id };
        UiFrame.Run(Window, () => UiFrame.TwoScopes(a.Draw, b.Draw));
        Assert.NotEqual(a.ProbeInside, b.ProbeInside);
    }

    [Fact]
    public void BeginCardIsABorderedBoxWithNoWindowPaddingAndRestoresThePaddingAfter()
    {
        var rig = new CardRig();
        var after = Vector2.Zero;
        Frames(() =>
        {
            rig.Draw();
            after = ImGui.GetStyle().WindowPadding;
        });
        Assert.True(rig.BorderSize > 0f);
        Assert.Equal(Vector2.Zero, rig.PaddingInside);
        Assert.Equal(WindowPadding, after);
    }

    [Fact]
    public void ClickingTheHeaderTogglesTheBodyAndReturnsTheNewState()
    {
        var rig = new CardRig();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            Assert.False(rig.Open);
            UiFrame.Click(Window, CardHeader(rig), rig.Draw);
            Assert.True(rig.Open);
            UiFrame.Click(Window, CardHeader(rig), rig.Draw);
            Assert.False(rig.Open);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ACardKeepsItsOpenStateWhenItsTitleChanges()
    {
        var rig = new CardRig { Title = "First" };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Click(Window, CardHeader(rig), rig.Draw);
            Assert.True(rig.Open);
            rig.Title = "Renamed while open";
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Run(Window, rig.Draw);
            Assert.True(rig.Open);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void SetOpenDrivesTheStateEveryFrameAndNullLeavesItAlone()
    {
        var rig = new CardRig { DefaultOpen = true, SetOpen = false };
        UiFrame.Run(Window, rig.Draw);
        UiFrame.Run(Window, rig.Draw);
        Assert.False(rig.Open);
        rig.SetOpen = true;
        UiFrame.Run(Window, rig.Draw);
        Assert.True(rig.Open);
        rig.SetOpen = false;
        UiFrame.Run(Window, rig.Draw);
        Assert.False(rig.Open);
        rig.SetOpen = null;
        UiFrame.Run(Window, rig.Draw);
        Assert.False(rig.Open);
    }

    [Fact]
    public void AnOpenBodyIsIndentedAndAClosedOneIsNotAndBothEndBackAtTheSameColumn()
    {
        var open = new CardRig { SetOpen = true };
        var closed = new CardRig { SetOpen = false };
        Frames(() =>
        {
            open.Draw();
            closed.Draw();
        });
        Assert.Equal(Card.BodyIndent, open.BodyStart.X - closed.BodyStart.X);
        Assert.Equal(open.Origin.X, open.CursorAfter.X);
        Assert.Equal(closed.Origin.X, closed.CursorAfter.X);
    }

    [Fact]
    public void AnOpenCardIsTallerThanAClosedOneByTheBodyAndItsTwoGaps()
    {
        var open = new CardRig { SetOpen = true };
        var closed = new CardRig { SetOpen = false };
        Frames(() =>
        {
            open.Draw();
            closed.Draw();
        });
        Assert.Equal(Spacing.Y + 2f + Spacing.Y + FontSize + Spacing.Y + 4f, open.Size.Y - closed.Size.Y, 0.01f);
    }

    [Fact]
    public void NestedCardsEndBackAtTheirOwnColumnWhateverTheirState()
    {
        var inner = new CardRig { SetOpen = false };
        var outer = new CardRig { SetOpen = true, Body = inner.Draw };
        Frames(outer.Draw);
        Assert.Equal(outer.Origin.X, outer.CursorAfter.X);
        Assert.Equal(inner.Origin.X, inner.CursorAfter.X);
        inner.SetOpen = true;
        Frames(outer.Draw);
        Assert.Equal(outer.Origin.X, outer.CursorAfter.X);
        Assert.Equal(inner.Origin.X, inner.CursorAfter.X);
        Assert.Equal(outer.Origin.X + Card.BodyIndent, inner.Origin.X);
    }

    float TrailingRight;

    Action Trail(string label) => () =>
    {
        ImGui.SmallButton(label);
        TrailingRight = ImGui.GetItemRectMax().X;
    };

    [Fact]
    public void TrailingWidgetsSitSixPixelsInsideTheCardsRightEdge()
    {
        var rig = new CardRig { Trailing = Trail("chip") };
        for (var i = 0; i < 3; i++) UiFrame.Run(Window, rig.Draw);
        Assert.Equal(rig.WorkRight - 6f, TrailingRight, 0.5f);
    }

    [Fact]
    public void EachCardAlignsItsOwnTrailingWidgetsAndTheSameIdUnderTwoScopesDoesToo()
    {
        float narrowRight = 0, wideRight = 0;
        var id = UiFrame.NextId("trail");
        var narrow = new CardRig { Id = id, Trailing = () =>
        {
            ImGui.SmallButton("x");
            narrowRight = ImGui.GetItemRectMax().X;
        } };
        var wide = new CardRig { Id = id, Trailing = () =>
        {
            ImGui.SmallButton("a much longer trailing label");
            wideRight = ImGui.GetItemRectMax().X;
        } };
        var other = new CardRig { Trailing = Trail("another one") };
        for (var i = 0; i < 3; i++) UiFrame.Run(Window, () =>
        {
            UiFrame.TwoScopes(narrow.Draw, wide.Draw);
            other.Draw();
        });
        Assert.Equal(narrow.WorkRight - 6f, narrowRight, 0.5f);
        Assert.Equal(wide.WorkRight - 6f, wideRight, 0.5f);
        Assert.Equal(other.WorkRight - 6f, TrailingRight, 0.5f);
    }

    [Fact]
    public void ClickingATrailingWidgetFiresItAndDoesNotToggleTheCard()
    {
        var clicks = 0;
        var rig = new CardRig { Trailing = () => clicks += ImGui.SmallButton("go") ? 1 : 0 };
        var at = Vector2.Zero;
        try
        {
            for (var i = 0; i < 3; i++) UiFrame.Run(Window, rig.Draw);
            var width = UiFrame.Measure(() => ImGui.CalcTextSize("go").X + Padding.X * 2);
            at = new Vector2(rig.WorkRight - 6f - width / 2, rig.Origin.Y + 1f + FrameHeight / 2);
            UiFrame.Click(Window, at, rig.Draw);
            Assert.Equal(1, clicks);
            Assert.False(rig.Open);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void RightClickingTheHeaderOpensTheMenuAndWithoutOneNothingOpens()
    {
        var shown = 0;
        var withMenu = new CardRig { Menu = () =>
        {
            shown++;
            ImGui.MenuItem("Copy");
        } };
        var withoutMenu = new CardRig();
        try
        {
            UiFrame.Run(Window, withMenu.Draw);
            UiFrame.Click(Window, CardHeader(withMenu), withMenu.Draw, button: 1);
            Assert.True(shown > 0);
            Assert.True(UiFrame.PopupOpen(Window, withMenu.Draw));
            UiFrame.ClickAway(Window, withMenu.Draw);
            Assert.False(UiFrame.PopupOpen(Window, withMenu.Draw));
            UiFrame.Run(Window, withoutMenu.Draw);
            UiFrame.Click(Window, CardHeader(withoutMenu), withoutMenu.Draw, button: 1);
            Assert.False(UiFrame.PopupOpen(Window, withoutMenu.Draw));
        }
        finally { UiFrame.ClickAway(Window, withMenu.Draw); UiFrame.Settle(Window, withMenu.Draw); }
    }

    sealed class ListRig
    {
        public readonly List<string> Items = ["Zq", "Mk", "Ptr"];
        public string Id = UiFrame.NextId("cardlist");
        public AddSource<string>? Add;
        public bool Reorderable;
        public bool Confirm;
        public Func<string, string>? FilterText;
        public Func<string, string> Label = s => s;
        public bool BodyEdits;
        public int Returns;
        public Vector2 Origin;
        public float Right;
        public readonly List<(string Item, int Index)> Bodies = [];
        public readonly Dictionary<int, float> BodyAlpha = [];
        public readonly List<uint> BodyProbes = [];
        public readonly List<uint> ExpectedProbes = [];
        public uint ProbeBefore;
        public uint ProbeAfter;
        public Vector2 ModalPos;
        public List<Vector2>? Tooltip;

        public void Draw()
        {
            Origin = ImGui.GetCursorScreenPos();
            Right = Origin.X + ImGui.GetContentRegionAvail().X;
            Bodies.Clear();
            BodyAlpha.Clear();
            BodyProbes.Clear();
            ExpectedProbes.Clear();
            for (var i = 0; i < Items.Count; i++)
            {
                ImGui.PushID(Id);
                ImGui.PushID(i);
                ExpectedProbes.Add(ImGui.GetID("probe"));
                ImGui.PopID();
                ImGui.PopID();
            }
            ProbeBefore = ImGui.GetID("probe");
            if (Card.List(Id, Items, Label, Body, Add, Reorderable, Confirm, FilterText)) Returns++;
            ProbeAfter = ImGui.GetID("probe");
            var modal = ImGuiP.FindWindowByName("Delete?##eidel");
            if (!modal.IsNull) ModalPos = modal.Pos;
            Tooltip = UiFrame.ActiveTooltipUvs() ?? Tooltip;
        }

        bool Body(string item, int index)
        {
            Bodies.Add((item, index));
            BodyAlpha[index] = ImGui.GetStyle().Alpha;
            BodyProbes.Add(ImGui.GetID("probe"));
            ImGui.TextUnformatted("body");
            return BodyEdits;
        }
    }

    float Offset(ListRig rig) => ((rig.Add is not null ? 1 : 0) + (rig.FilterText is not null ? 1 : 0)) * (FrameHeight + Spacing.Y);

    Vector2 HeaderAt(ListRig rig, int row) => new(rig.Origin.X + 30f, rig.Origin.Y + Offset(rig) + row * (FrameHeight + Spacing.Y) + FrameHeight / 2);

    Vector2 XButton(ListRig rig, int row) => new(rig.Right - Padding.X - FontSize / 2, HeaderAt(rig, row).Y);

    Vector2 FilterBox(ListRig rig) => new(rig.Origin.X + 20f, rig.Origin.Y + (rig.Add is not null ? FrameHeight + Spacing.Y : 0f) + FrameHeight / 2);

    void OpenHeaders(ListRig rig)
    {
        UiFrame.Run(Window, rig.Draw);
        for (var i = rig.Items.Count - 1; i >= 0; i--) UiFrame.Click(Window, HeaderAt(rig, i), rig.Draw);
        UiFrame.Run(Window, rig.Draw);
    }

    [Fact]
    public void ListAddButtonAppendsTheFactoryItemOnceAndReportsIt()
    {
        var rig = new ListRig { Add = AddSource<string>.Button(() => "new") };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            var width = UiFrame.Measure(() => ImGui.CalcTextSize("+ Add").X + Padding.X * 2);
            UiFrame.Click(Window, rig.Origin + new Vector2(width / 2, FrameHeight / 2), rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr", "new"], rig.Items);
            Assert.Equal(1, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListHeadersStartClosedAndAClickOpensOnlyThatBodyWithItsItemAndIndex()
    {
        var rig = new ListRig();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            Assert.Empty(rig.Bodies);
            UiFrame.Click(Window, HeaderAt(rig, 1), rig.Draw);
            Assert.Equal([("Mk", 1)], rig.Bodies);
            Assert.Equal(0, rig.Returns);
            UiFrame.Click(Window, HeaderAt(rig, 1), rig.Draw);
            Assert.Empty(rig.Bodies);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListReportsAnEditMadeByAnOpenBodyAndNothingForAnUntouchedOne()
    {
        var quiet = new ListRig();
        var editing = new ListRig { BodyEdits = true };
        try
        {
            OpenHeaders(quiet);
            Assert.Equal(3, quiet.Bodies.Count);
            Assert.Equal(0, quiet.Returns);
            UiFrame.Run(Window, editing.Draw);
            Assert.Equal(0, editing.Returns);
            UiFrame.Click(Window, HeaderAt(editing, 0), editing.Draw);
            Assert.True(editing.Returns > 0);
        }
        finally { UiFrame.Settle(Window, quiet.Draw); UiFrame.Settle(Window, editing.Draw); }
    }

    [Fact]
    public void ListBodiesRunInsideTheIndentAndPopItAgain()
    {
        var rig = new ListRig();
        var bodyX = 0f;
        var afterX = 0f;
        try
        {
            OpenHeaders(rig);
            UiFrame.Run(Window, () =>
            {
                rig.Draw();
                afterX = ImGui.GetCursorScreenPos().X;
            });
            UiFrame.Run(Window, () =>
            {
                var start = ImGui.GetCursorScreenPos().X;
                Card.List(rig.Id, rig.Items, rig.Label, (_, _) =>
                {
                    bodyX = ImGui.GetCursorScreenPos().X - start;
                    return false;
                });
            });
            Assert.Equal(ImGui.GetStyle().IndentSpacing, bodyX);
            Assert.Equal(rig.Origin.X, afterX);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ListHeaderXRemovesExactlyThatRowImmediately(int row)
    {
        var rig = new ListRig();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            var expected = rig.Items.Where((_, i) => i != row).ToList();
            UiFrame.Click(Window, XButton(rig, row), rig.Draw);
            Assert.Equal(expected, rig.Items);
            Assert.Equal(1, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListEmptyDrawsNoRowsAndReportsNothing()
    {
        var rig = new ListRig();
        rig.Items.Clear();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            Assert.Equal(0, rig.Returns);
            Assert.Empty(rig.Bodies);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    readonly UiFrame.ModalMetrics Modal = UiFrame.ReadModalMetrics();

    Vector2 ModalButton(ListRig rig, bool confirm) => UiFrame.ConfirmModalButton(rig.ModalPos, confirm, Modal);

    [Fact]
    public void ListConfirmDeleteAsksFirstAndConfirmRemovesTheClickedRow()
    {
        var rig = new ListRig { Confirm = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Click(Window, XButton(rig, 1), rig.Draw);
            Assert.True(UiFrame.PopupOpen(Window, rig.Draw));
            Assert.Equal(3, rig.Items.Count);
            Assert.Equal(0, rig.Returns);
            UiFrame.Click(Window, ModalButton(rig, true), rig.Draw);
            Assert.Equal(["Zq", "Ptr"], rig.Items);
            Assert.Equal(1, rig.Returns);
            Assert.False(UiFrame.PopupOpen(Window, rig.Draw));
        }
        finally { UiFrame.CloseModal(Window, rig.Draw, () => ModalButton(rig, false)); UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListConfirmDeleteCancelKeepsTheRowAndReportsNothing()
    {
        var rig = new ListRig { Confirm = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Click(Window, XButton(rig, 0), rig.Draw);
            Assert.True(UiFrame.PopupOpen(Window, rig.Draw));
            UiFrame.Click(Window, ModalButton(rig, false), rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(0, rig.Returns);
            Assert.False(UiFrame.PopupOpen(Window, rig.Draw));
        }
        finally { UiFrame.CloseModal(Window, rig.Draw, () => ModalButton(rig, false)); UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListConfirmDeleteBelongsToTheListWhoseXWasClickedEvenWhenAnotherDrawsFirst()
    {
        var other = new ListRig { Confirm = true };
        var owner = new ListRig { Confirm = true };
        void Body()
        {
            other.Draw();
            owner.Draw();
        }
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Click(Window, XButton(owner, 1), Body);
            Assert.True(UiFrame.PopupOpen(Window, Body));
            UiFrame.Click(Window, ModalButton(owner, true), Body);
            Assert.Equal(["Zq", "Ptr"], owner.Items);
            Assert.Equal(["Zq", "Mk", "Ptr"], other.Items);
            Assert.Equal(1, owner.Returns);
            Assert.Equal(0, other.Returns);
        }
        finally { UiFrame.CloseModal(Window, owner.Draw, () => ModalButton(owner, false)); UiFrame.Settle(Window, Body); }
    }

    [Fact]
    public void ListWithoutConfirmDeleteNeverOpensAModal()
    {
        var rig = new ListRig();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Click(Window, XButton(rig, 0), rig.Draw);
            Assert.False(UiFrame.PopupOpen(Window, rig.Draw));
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListDraggingAHeaderOntoAnotherMovesTheItemAndDoesNotOpenIt()
    {
        var rig = new ListRig { Reorderable = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Drag(Window, HeaderAt(rig, 0), HeaderAt(rig, 2), rig.Draw);
            Assert.Equal(["Mk", "Ptr", "Zq"], rig.Items);
            Assert.Equal(1, rig.Returns);
            Assert.Empty(rig.Bodies);
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Drag(Window, HeaderAt(rig, 2), HeaderAt(rig, 0), rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(2, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListWithoutReorderableIgnoresADrag()
    {
        var rig = new ListRig();
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Drag(Window, HeaderAt(rig, 0), HeaderAt(rig, 2), rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(0, rig.Returns);
            Assert.Null(rig.Tooltip);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListDroppingAHeaderOnNothingChangesNothing()
    {
        var rig = new ListRig { Reorderable = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            var away = HeaderAt(rig, 2) + new Vector2(0, 3 * FrameHeight);
            UiFrame.Press(Window, HeaderAt(rig, 1), rig.Draw);
            UiFrame.MoveTo(Window, away, rig.Draw);
            UiFrame.MoveTo(Window, away, rig.Draw);
            UiFrame.Release(Window, rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(0, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListDroppingAHeaderAfterCrossingRowsButOffTheListChangesNothing()
    {
        var rig = new ListRig { Reorderable = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            var away = HeaderAt(rig, 2) + new Vector2(0, 3 * FrameHeight);
            UiFrame.Press(Window, HeaderAt(rig, 0), rig.Draw);
            for (var row = 1; row <= 2; row++)
            {
                UiFrame.MoveTo(Window, HeaderAt(rig, row), rig.Draw);
                UiFrame.MoveTo(Window, HeaderAt(rig, row), rig.Draw);
            }
            UiFrame.MoveTo(Window, away, rig.Draw);
            UiFrame.MoveTo(Window, away, rig.Draw);
            UiFrame.Release(Window, rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(0, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListReleasingInTheGapBetweenRowsIsANoOp()
    {
        var rig = new ListRig { Reorderable = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            var gap = new Vector2(HeaderAt(rig, 1).X, HeaderAt(rig, 1).Y + FrameHeight / 2 + Spacing.Y / 2);
            UiFrame.Press(Window, HeaderAt(rig, 0), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1), rig.Draw);
            UiFrame.MoveTo(Window, gap, rig.Draw);
            UiFrame.MoveTo(Window, gap, rig.Draw);
            UiFrame.Release(Window, rig.Draw);
            Assert.Equal(["Zq", "Mk", "Ptr"], rig.Items);
            Assert.Equal(0, rig.Returns);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ASubtleSurfaceHonoursItsPadding()
    {
        var start = Vector2.Zero;
        var rig = new SurfaceRig { Look = CardLook.Subtle, Padding = 9f, Body = () =>
        {
            start = ImGui.GetCursorScreenPos();
            ImGui.TextUnformatted("content");
        } };
        Frames(rig.Draw);
        Assert.Equal(rig.Origin + new Vector2(9f), start);
        Assert.Equal(FontSize + 18f, rig.Size.Y);
    }

    [Fact]
    public void ListADragInOneScopeNeverMovesTheSameIdListInAnotherAndOnlyTheOwnerApplies()
    {
        var a = new ListRig { Reorderable = true };
        var b = new ListRig { Id = a.Id, Reorderable = true };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);
            UiFrame.Drag(Window, HeaderAt(a, 0), HeaderAt(a, 2), Body);
            Assert.Equal(["Mk", "Ptr", "Zq"], a.Items);
            Assert.Equal(["Zq", "Mk", "Ptr"], b.Items);
            Assert.Equal(1, a.Returns);
            Assert.Equal(0, b.Returns);
        }
        finally { UiFrame.Settle(Window, Body); }
    }

    [Fact]
    public void ListADragStartedInOneScopeAndDroppedOnTheSameIdListInAnotherChangesNothing()
    {
        var a = new ListRig { Reorderable = true };
        var b = new ListRig { Id = a.Id, Reorderable = true };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);
            UiFrame.Press(Window, HeaderAt(a, 0), Body);
            UiFrame.MoveTo(Window, HeaderAt(b, 2), Body);
            UiFrame.MoveTo(Window, HeaderAt(b, 2), Body);
            UiFrame.Release(Window, Body);
            Assert.Equal(["Zq", "Mk", "Ptr"], a.Items);
            Assert.Equal(["Zq", "Mk", "Ptr"], b.Items);
            Assert.Equal(0, a.Returns + b.Returns);
        }
        finally { UiFrame.Settle(Window, Body); }
    }

    [Fact]
    public void ListDragTooltipShowsTheDraggedRowsLabelOnlyWhenReorderable()
    {
        var draggedLabel = UiFrame.GlyphUvs(Window, "Mk");
        var otherLabel = UiFrame.GlyphUvs(Window, "Zq");
        var rig = new ListRig { Reorderable = true };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Press(Window, HeaderAt(rig, 1), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1) + new Vector2(0, FrameHeight + Spacing.Y), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1) + new Vector2(0, FrameHeight + Spacing.Y), rig.Draw);
            Assert.NotNull(rig.Tooltip);
            Assert.True(EndsWith(rig.Tooltip, draggedLabel));
            Assert.False(EndsWith(rig.Tooltip, otherLabel));
            UiFrame.Release(Window, rig.Draw);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListDragTooltipUsesTheLabelFunctionNotTheItem()
    {
        var shown = UiFrame.GlyphUvs(Window, "Item Mk");
        var rig = new ListRig { Reorderable = true, Label = s => "Item " + s };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Press(Window, HeaderAt(rig, 1), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1) + new Vector2(0, FrameHeight + Spacing.Y), rig.Draw);
            UiFrame.MoveTo(Window, HeaderAt(rig, 1) + new Vector2(0, FrameHeight + Spacing.Y), rig.Draw);
            Assert.NotNull(rig.Tooltip);
            Assert.True(EndsWith(rig.Tooltip, shown));
            UiFrame.Release(Window, rig.Draw);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ADragSourceShowsItsLabelInTheDragTooltipAndWithoutOneShowsNoText()
    {
        var key = UiFrame.Measure(() => ImGui.GetID("dragsource"));
        var items = new List<string> { "a", "b" };
        List<Vector2>? tooltip = null;
        var origin = Vector2.Zero;
        void Body()
        {
            origin = ImGui.GetCursorScreenPos();
            ImGui.Button("first");
            ListEditor.DragSourceTarget(key, 0, "Zq");
            ImGui.Button("second");
            ListEditor.DragSourceTarget(key, 1, null);
            ListEditor.ApplyReorder(key, items);
            tooltip = UiFrame.ActiveTooltipUvs() ?? tooltip;
        }
        var shown = UiFrame.GlyphUvs(Window, "Zq");
        var buttonWidth = UiFrame.Measure(() => ImGui.CalcTextSize("first").X + Padding.X * 2);
        try
        {
            UiFrame.Run(Window, Body);
            var first = origin + new Vector2(buttonWidth / 2, FrameHeight / 2);
            var second = first + new Vector2(0, FrameHeight + Spacing.Y);
            UiFrame.Press(Window, first, Body);
            UiFrame.MoveTo(Window, first + new Vector2(0, FrameHeight * 3), Body);
            UiFrame.MoveTo(Window, first + new Vector2(0, FrameHeight * 3), Body);
            Assert.NotNull(tooltip);
            Assert.True(EndsWith(tooltip, shown));
            var labeledVertices = tooltip.Count;
            UiFrame.Release(Window, Body);

            tooltip = null;
            UiFrame.Press(Window, second, Body);
            UiFrame.MoveTo(Window, second + new Vector2(0, FrameHeight * 3), Body);
            UiFrame.MoveTo(Window, second + new Vector2(0, FrameHeight * 3), Body);
            Assert.NotNull(tooltip);
            Assert.Equal(labeledVertices - shown.Count, tooltip.Count);
            UiFrame.Release(Window, Body);
        }
        finally { UiFrame.Settle(Window, Body); }
    }

    [Fact]
    public void ListFilterBoxSitsAboveTheRowsAndWithoutFilterTextThereIsNone()
    {
        var plain = new ListRig();
        var filtered = new ListRig { FilterText = s => s };
        UiFrame.Run(Window, plain.Draw);
        UiFrame.Run(Window, filtered.Draw);
        try
        {
            UiFrame.Click(Window, HeaderAt(plain, 0), plain.Draw);
            Assert.Single(plain.Bodies);
            UiFrame.Click(Window, HeaderAt(filtered, 0), filtered.Draw);
            Assert.Equal([("Zq", 0)], filtered.Bodies);
        }
        finally { UiFrame.Settle(Window, plain.Draw); UiFrame.Settle(Window, filtered.Draw); }
    }

    void Filter(ListRig rig, string text, Action body)
    {
        UiFrame.Click(Window, FilterBox(rig), body);
        UiFrame.Io().AddInputCharactersUTF8(text);
        UiFrame.Run(Window, body);
        UiFrame.Run(Window, body);
    }

    [Fact]
    public void ListFilterDimsTheBodiesOfNonMatchingItems()
    {
        var rig = new ListRig { FilterText = s => s };
        try
        {
            OpenHeaders(rig);
            Assert.All(rig.BodyAlpha.Values, a => Assert.Equal(BaseAlpha, a));
            Filter(rig, "Mk", rig.Draw);
            Assert.True(rig.BodyAlpha[0] < BaseAlpha);
            Assert.Equal(BaseAlpha, rig.BodyAlpha[1]);
            Assert.True(rig.BodyAlpha[2] < BaseAlpha);
        }
        finally { UiFrame.EraseText(Window, rig.Draw, FilterBox(rig)); UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListFilterTextIsKeptPerListScope()
    {
        var a = new ListRig { FilterText = s => s };
        var b = new ListRig { Id = a.Id, FilterText = s => s };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);
            for (var i = 2; i >= 0; i--) UiFrame.Click(Window, HeaderAt(b, i), Body);
            for (var i = 2; i >= 0; i--) UiFrame.Click(Window, HeaderAt(a, i), Body);
            UiFrame.Run(Window, Body);
            Filter(a, "Mk", Body);
            Assert.Contains(a.BodyAlpha.Values, alpha => alpha < BaseAlpha);
            Assert.All(b.BodyAlpha.Values, alpha => Assert.Equal(BaseAlpha, alpha));
        }
        finally { UiFrame.EraseText(Window, Body, FilterBox(a)); UiFrame.Settle(Window, Body); }
    }

    [Fact]
    public void ListRowsAreScopedUnderTheListIdAndTheRowIndex()
    {
        var rig = new ListRig { Id = "scoped-list" };
        try
        {
            OpenHeaders(rig);
            Assert.Equal(rig.ExpectedProbes, rig.BodyProbes);
            Assert.Equal(3, rig.BodyProbes.Distinct().Count());
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }

    [Fact]
    public void ListTheSameIdUnderTwoScopesGivesDifferentRowIds()
    {
        var a = new ListRig();
        var b = new ListRig { Id = a.Id };
        void Body() => UiFrame.TwoScopes(a.Draw, b.Draw);
        try
        {
            UiFrame.Run(Window, Body);
            UiFrame.Run(Window, Body);
            for (var i = 2; i >= 0; i--) UiFrame.Click(Window, HeaderAt(b, i), Body);
            for (var i = 2; i >= 0; i--) UiFrame.Click(Window, HeaderAt(a, i), Body);
            UiFrame.Run(Window, Body);
            Assert.Equal(3, a.BodyProbes.Count);
            Assert.Empty(a.BodyProbes.Intersect(b.BodyProbes));
        }
        finally { UiFrame.Settle(Window, Body); }
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, true, true, true, true)]
    public void ListBalancesTheIdStackForEveryOptionVariant(bool buttonAdd, bool comboAdd, bool reorderable, bool confirm, bool filter)
    {
        var rig = new ListRig
        {
            Add = buttonAdd ? AddSource<string>.Button(() => "new") : comboAdd ? AddSource<string>.Combo([("k", "Key")], key => key) : null,
            Reorderable = reorderable,
            Confirm = confirm,
            FilterText = filter ? s => s : null
        };
        var other = new ListRig();
        UiFrame.Run(Window, () =>
        {
            rig.Draw();
            other.Draw();
        });
        Assert.Equal(rig.ProbeBefore, rig.ProbeAfter);
        Assert.Equal(other.ProbeBefore, other.ProbeAfter);
        Assert.Equal(0, rig.Returns);
    }

    [Fact]
    public void ListHeaderKeepsItsOpenStateWhenTheLabelChanges()
    {
        var suffix = "";
        var rig = new ListRig { Label = s => s + suffix };
        try
        {
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Click(Window, HeaderAt(rig, 0), rig.Draw);
            Assert.Equal([("Zq", 0)], rig.Bodies);
            suffix = " edited";
            UiFrame.Run(Window, rig.Draw);
            UiFrame.Run(Window, rig.Draw);
            Assert.Equal([("Zq", 0)], rig.Bodies);
        }
        finally { UiFrame.Settle(Window, rig.Draw); }
    }
}
