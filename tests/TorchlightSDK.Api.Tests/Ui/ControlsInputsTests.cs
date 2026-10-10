using System.Drawing;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class ControlsInputsTests
{
    static int Verts() => ImGui.GetWindowDrawList().VtxBuffer.Size;

    static Vector2 Centre() => (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;

    static int RenderedWindows() => ImGui.GetIO().MetricsRenderWindows;

    static HashSet<uint> VertexColoursSince(int start)
    {
        var verts = ImGui.GetWindowDrawList().VtxBuffer;
        var colours = new HashSet<uint>();
        for (var i = start; i < verts.Size; i++) colours.Add(verts[i].Col);
        return colours;
    }

    static void Away(string window, Vector2 from, Action body) => UiFrame.Hover(window, from + new Vector2(300, 300), body);

    [Fact]
    public void TriStateCyclesForward()
    {
        Assert.Equal(1, Controls.CycleTri(0, false));
        Assert.Equal(2, Controls.CycleTri(1, false));
        Assert.Equal(0, Controls.CycleTri(2, false));
    }

    [Fact]
    public void TriStateCyclesBack()
    {
        Assert.Equal(2, Controls.CycleTri(0, true));
        Assert.Equal(1, Controls.CycleTri(2, true));
    }

    [Fact]
    public void InputsDrawOneBalancedFrameAndChangeNothingUntouched()
    {
        int whole = 3, tri = 1;
        var fraction = 0.5f;
        bool overriding = false, on = true;
        var colour = Color.FromArgb(255, 200, 100, 50);
        Controls.TriItem[] items = [new("A"), new("B", 1), new("C", 2)];
        var changed = false;

        UiFrame.Run("test", () =>
        {
            changed |= Controls.SliderInt("Int", ref whole, 0, 10);
            changed |= Controls.SliderInt("Int via accessors", () => whole, v => whole = v, 0, 10);
            changed |= Controls.SliderFloat("Float", ref fraction, 0, 1, fmt: "%.2f");
            changed |= Controls.SliderFloat("Float via accessors", () => fraction, v => fraction = v, 0, 1);
            changed |= Controls.Color("Colour", ref colour);
            changed |= Controls.Color("Colour via accessors", () => colour, c => colour = c, alpha: false);
            if (Controls.Category("Section")) ImGui.TextUnformatted("body");
            Controls.InfoMark("right 50% of the time");
            Controls.InfoLabel("Label", "info");
            changed |= Controls.Field("path", "Path", () => false, hint: "a 100% hint");
            changed |= Controls.Field("bad", "Bad", () => false, error: "100% wrong");
            changed |= Controls.OverrideRow("ovr", "Override", ref overriding, () => Controls.SliderFloat("##v", ref fraction, 0, 1));
            Controls.AlignMid(12);
            ImGui.TextUnformatted("mid");
            changed |= Controls.IconToggle("M", ref on, "Map icon at 100%");
            changed |= Controls.TriState("Tri", ref tri);
            changed |= Controls.TriStateGrid("grid", items, 3);
            using (new Controls.PanelStyleScope()) ImGui.Button("styled");
        });

        Assert.False(changed);
        Assert.Equal(1, items[1].Value);
    }

    [Fact]
    public void SliderIdsComeFromTheLabelUnlessOneIsGiven()
    {
        uint byLabel = 0, byLabelExpected = 0, byId = 0, byIdExpected = 0, floatById = 0, floatByIdExpected = 0;
        var v = 1;
        var f = 0.5f;
        UiFrame.Run("slider ids", () =>
        {
            Controls.SliderInt("Alpha", ref v, 0, 10);
            byLabel = ImGui.GetItemID();
            byLabelExpected = ImGui.GetID("Alpha##Alpha");
            Controls.SliderInt("Alpha", ref v, 0, 10, "second");
            byId = ImGui.GetItemID();
            byIdExpected = ImGui.GetID("Alpha##second");
            Controls.SliderFloat("Alpha", ref f, 0, 1, "third");
            floatById = ImGui.GetItemID();
            floatByIdExpected = ImGui.GetID("Alpha##third");
        });

        Assert.Equal(byLabelExpected, byLabel);
        Assert.Equal(byIdExpected, byId);
        Assert.Equal(floatByIdExpected, floatById);
        Assert.NotEqual(byLabel, byId);
    }

    [Fact]
    public void ClickingASliderWritesThroughRefAndAccessors()
    {
        var refInt = 0;
        var viaInt = 0;
        var refFloat = 0f;
        var viaFloat = 0f;
        bool refIntChanged = false, viaIntChanged = false, refFloatChanged = false, viaFloatChanged = false;
        var target = 0;
        var at = Vector2.Zero;
        void Body()
        {
            switch (target)
            {
                case 0: refIntChanged |= Controls.SliderInt("A", ref refInt, 0, 100); break;
                case 1: viaIntChanged |= Controls.SliderInt("B", () => viaInt, v => viaInt = v, 0, 100); break;
                case 2: refFloatChanged |= Controls.SliderFloat("C", ref refFloat, 0, 100); break;
                default: viaFloatChanged |= Controls.SliderFloat("D", () => viaFloat, v => viaFloat = v, 0, 100); break;
            }
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            at = new Vector2(min.X + (max.X - min.X) * 0.4f, (min.Y + max.Y) / 2);
        }

        for (target = 0; target < 4; target++)
        {
            UiFrame.Run("slider click " + target, Body);
            UiFrame.Click("slider click " + target, at, Body);
        }

        Assert.True(refIntChanged && viaIntChanged && refFloatChanged && viaFloatChanged);
        Assert.InRange(refInt, 25, 55);
        Assert.InRange(viaInt, 25, 55);
        Assert.InRange(refFloat, 25f, 55f);
        Assert.InRange(viaFloat, 25f, 55f);
    }

    [Fact]
    public void SliderFloatFormatReachesBothOverloads()
    {
        var f = 0.5f;
        int plain = 0, fine = 0, plainAccessor = 0, fineAccessor = 0;
        UiFrame.Run("slider fmt", () =>
        {
            var start = Verts();
            Controls.SliderFloat("x", ref f, 0, 1, fmt: "%.0f");
            plain = Verts() - start;
            start = Verts();
            Controls.SliderFloat("x", ref f, 0, 1, fmt: "%.4f");
            fine = Verts() - start;
            start = Verts();
            Controls.SliderFloat("x", () => f, _ => { }, 0, 1, fmt: "%.0f");
            plainAccessor = Verts() - start;
            start = Verts();
            Controls.SliderFloat("x", () => f, _ => { }, 0, 1, fmt: "%.4f");
            fineAccessor = Verts() - start;
        });

        Assert.True(fine > plain);
        Assert.True(fineAccessor > plainAccessor);
        Assert.Equal(plain, plainAccessor);
        Assert.Equal(fine, fineAccessor);
    }

    [Fact]
    public void ColorAlphaFlagReachesBothOverloadsAndNothingChangesUntouched()
    {
        var translucent = Color.FromArgb(128, 200, 100, 50);
        int withAlpha = 0, withoutAlpha = 0, accessorWithAlpha = 0, accessorWithoutAlpha = 0;
        var changed = false;
        UiFrame.Run("colour alpha", () =>
        {
            var start = Verts();
            changed |= Controls.Color("a", ref translucent);
            withAlpha = Verts() - start;
            start = Verts();
            changed |= Controls.Color("a", ref translucent, "b", alpha: false);
            withoutAlpha = Verts() - start;
            start = Verts();
            changed |= Controls.Color("a", () => translucent, _ => { });
            accessorWithAlpha = Verts() - start;
            start = Verts();
            changed |= Controls.Color("a", () => translucent, _ => { }, alpha: false);
            accessorWithoutAlpha = Verts() - start;
        });

        Assert.False(changed);
        Assert.Equal(Color.FromArgb(128, 200, 100, 50), translucent);
        Assert.True(withAlpha > withoutAlpha);
        Assert.Equal(withAlpha, accessorWithAlpha);
        Assert.Equal(withoutAlpha, accessorWithoutAlpha);
    }

    [Fact]
    public void ColorPickerPopupLivesUnderTheLabelPlusIdScope()
    {
        var colour = Color.White;
        var swatch = Vector2.Zero;
        bool underGiven = false, underLabel = false;
        void Body()
        {
            var start = ImGui.GetCursorScreenPos();
            Controls.Color("Tint", ref colour, "given");
            swatch = start + new Vector2(ImGui.GetFrameHeight() / 2, ImGui.GetFrameHeight() / 2);
            ImGui.PushID("Tint##given");
            underGiven = ImGui.IsPopupOpen("picker");
            ImGui.PopID();
            ImGui.PushID("Tint##Tint");
            underLabel = ImGui.IsPopupOpen("picker");
            ImGui.PopID();
        }

        bool opened, openedUnderLabel;
        try
        {
            UiFrame.Run("colour ids", Body);
            UiFrame.Click("colour ids", swatch, Body);
            opened = underGiven;
            openedUnderLabel = underLabel;
        }
        finally
        {
            UiFrame.ClickAway("colour ids", Body);
        }

        Assert.True(opened);
        Assert.False(openedUnderLabel);
        Assert.False(underGiven);
    }

    [Fact]
    public void ColorSwatchOpensAPickerAndClickingItWritesBothOverloads()
    {
        var original = Color.FromArgb(255, 200, 100, 50);
        var refColour = original;
        var viaColour = original;
        var viaChanged = false;
        var refChanged = false;
        var target = 0;
        var swatch = Vector2.Zero;
        var picker = Vector2.Zero;
        void Body()
        {
            var start = ImGui.GetCursorScreenPos();
            if (target == 0) refChanged |= Controls.Color("c", ref refColour);
            else viaChanged |= Controls.Color("c", () => viaColour, c => viaColour = c);
            swatch = start + new Vector2(ImGui.GetFrameHeight() / 2, ImGui.GetFrameHeight() / 2);
            picker = start + new Vector2(ImGui.GetFrameHeight() * 3, ImGui.GetFrameHeight() * 5);
        }

        for (target = 0; target < 2; target++)
        {
            var window = "colour picker " + target;
            try
            {
                UiFrame.Run(window, Body);
                UiFrame.Click(window, swatch, Body);
                UiFrame.Click(window, picker, Body);
            }
            finally
            {
                UiFrame.ClickAway(window, Body);
            }
        }

        Assert.True(refChanged);
        Assert.True(viaChanged);
        Assert.NotEqual(original, refColour);
        Assert.NotEqual(original, viaColour);
    }

    [Fact]
    public void CategoryStartsOpenAndCollapsesOnClick()
    {
        var open = false;
        var at = Vector2.Zero;
        void Body()
        {
            open = Controls.Category("Section", "cat");
            at = Centre();
        }

        UiFrame.Run("category", Body);
        var openedByDefault = open;
        UiFrame.Click("category", at, Body);

        Assert.True(openedByDefault);
        Assert.False(open);
    }

    [Fact]
    public void CategoryIdComesFromTheLabelUnlessOneIsGiven()
    {
        uint byLabel = 0, byLabelExpected = 0, byId = 0, byIdExpected = 0;
        UiFrame.Run("category ids", () =>
        {
            Controls.Category("Section");
            byLabel = ImGui.GetItemID();
            byLabelExpected = ImGui.GetID("Section##Section");
            Controls.Category("Section", "again");
            byId = ImGui.GetItemID();
            byIdExpected = ImGui.GetID("Section##again");
        });

        Assert.Equal(byLabelExpected, byLabel);
        Assert.Equal(byIdExpected, byId);
    }

    [Fact]
    public void InfoLabelGluesTheMarkOneInnerSpaceAfterTheLabel()
    {
        float labelRight = 0, markLeft = 0, markWidth = 0, spacing = 0;
        UiFrame.Run("info label", () =>
        {
            var startX = ImGui.GetCursorScreenPos().X;
            Controls.InfoLabel("Label", "info");
            markLeft = ImGui.GetItemRectMin().X;
            markWidth = ImGui.GetItemRectSize().X;
            labelRight = startX + ImGui.CalcTextSize("Label").X;
            spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        });

        Assert.Equal(labelRight + spacing, markLeft, 0.5f);
        Assert.Equal(ImGui.CalcTextSize("(?)").X, markWidth, 0.5f);
    }

    [Fact]
    public void InfoLabelTreatsANullLabelAsEmpty()
    {
        float markLeft = 0, startX = 0, spacing = 0;
        UiFrame.Run("info null label", () =>
        {
            startX = ImGui.GetCursorScreenPos().X;
            Controls.InfoLabel(null, null);
            markLeft = ImGui.GetItemRectMin().X;
            spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        });

        Assert.Equal(startX + spacing, markLeft, 0.5f);
    }

    [Fact]
    public void InfoMarkShowsATooltipOnlyWhenHoveredAndGivenText()
    {
        var at = Vector2.Zero;
        var info = "tip with 50% in it";
        void Body()
        {
            Controls.InfoMark(info);
            at = Centre();
        }

        try
        {
            UiFrame.Run("info mark", Body);
            Away("info mark", at, Body);
            var idle = RenderedWindows();

            UiFrame.Hover("info mark", at, Body);
            var hovered = RenderedWindows();

            info = "";
            UiFrame.Hover("info mark", at, Body);
            var hoveredEmpty = RenderedWindows();

            Assert.Equal(idle + 1, hovered);
            Assert.Equal(idle, hoveredEmpty);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void FieldPassesTheControlFlagThroughAndSkipsANullControl()
    {
        var results = new List<bool>();
        var nullDelta = -1;
        UiFrame.Run("field flag", () =>
        {
            results.Add(Controls.Field("t", "L", () => true));
            results.Add(Controls.Field("f", "L", () => false));
            var start = Verts();
            results.Add(Controls.Field("n", "L", null));
            nullDelta = Verts() - start;
        });

        Assert.Equal([true, false, false], results);
        Assert.Equal(0, nullDelta);
    }

    [Fact]
    public void FieldErrorRingsTheControlAndTheHintOnlyShowsWithoutAnError()
    {
        var longText = new string('a', 30);
        int hintedLong = 0, errored = 0, bare = 0, labelled = 0, unlabelled = 0;
        float groupHeight = 0, twoButtons = 0;
        bool Control()
        {
            ImGui.Button("btn");
            return false;
        }

        UiFrame.Run("field error", () =>
        {
            var start = Verts();
            Controls.Field("h", null, Control, hint: longText);
            hintedLong = Verts() - start;
            start = Verts();
            Controls.Field("e", null, Control, hint: "a", error: longText);
            errored = Verts() - start;
            start = Verts();
            Controls.Field("b", null, Control);
            bare = Verts() - start;
            start = Verts();
            Controls.Field("l", "Label", Control);
            labelled = Verts() - start;
            start = Verts();
            Controls.Field("u", "", Control);
            unlabelled = Verts() - start;
            Controls.Field("g", null, () =>
            {
                ImGui.Button("one");
                ImGui.Button("two");
                return false;
            });
            groupHeight = ImGui.GetItemRectSize().Y;
            twoButtons = ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y;
        });

        Assert.True(errored > hintedLong);
        Assert.True(hintedLong > bare);
        Assert.True(labelled > bare);
        Assert.Equal(bare, unlabelled);
        Assert.Equal(twoButtons, groupHeight, 0.5f);
    }

    [Fact]
    public void FieldMessagesPrintPercentSignsLiterally()
    {
        int hint = 0, hintBaseline = 0, error = 0, errorBaseline = 0;
        UiFrame.Run("field percent", () =>
        {
            var start = Verts();
            Controls.Field("h1", null, () => false, hint: "a%sb");
            hint = Verts() - start;
            start = Verts();
            Controls.Field("h2", null, () => false, hint: "a#sb");
            hintBaseline = Verts() - start;
            start = Verts();
            Controls.Field("e1", null, () => false, error: "a%sb");
            error = Verts() - start;
            start = Verts();
            Controls.Field("e2", null, () => false, error: "a#sb");
            errorBaseline = Verts() - start;
        });

        Assert.Equal(hintBaseline, hint);
        Assert.Equal(errorBaseline, error);
    }

    [Fact]
    public void OverrideRowDisablesTheControlUntilTickedAndFoldsTheFlags()
    {
        var overriding = false;
        var controlAlpha = 0f;
        var controlResult = false;
        var changed = false;
        var checkbox = Vector2.Zero;
        void Body()
        {
            var start = ImGui.GetCursorScreenPos();
            var half = ImGui.GetFrameHeight() / 2;
            checkbox = start + new Vector2(half, half);
            changed |= Controls.OverrideRow("ovr", "Thing", ref overriding, () =>
            {
                controlAlpha = ImGui.GetStyle().Alpha;
                return controlResult;
            });
        }

        UiFrame.Run("override", Body);
        var disabledAlpha = controlAlpha;
        UiFrame.Click("override", checkbox, Body);
        var afterClick = overriding;
        var clickChanged = changed;
        var enabledAlpha = controlAlpha;

        changed = false;
        controlResult = true;
        UiFrame.Run("override", Body);

        Assert.True(afterClick);
        Assert.True(clickChanged);
        Assert.True(disabledAlpha < enabledAlpha);
        Assert.True(changed);
    }

    [Fact]
    public void OverrideRowShowsATooltipOnHoverOfTheCheckbox()
    {
        var overriding = false;
        var checkbox = Vector2.Zero;
        void Body()
        {
            Controls.OverrideRow("ovr", "Thing", ref overriding, () => false);
            checkbox = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding + new Vector2(ImGui.GetFrameHeight() / 2, ImGui.GetFrameHeight() / 2);
            checkbox.Y += ImGui.GetFrameHeight() / 2;
        }

        try
        {
            UiFrame.Run("override tip", Body);
            Away("override tip", checkbox, Body);
            var idle = RenderedWindows();
            UiFrame.Hover("override tip", checkbox, Body);

            Assert.Equal(idle + 1, RenderedWindows());
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void AlignMidNudgesShortItemsDownAndNeverUp()
    {
        float shortDelta = 0, tallDelta = 0, frame = 0;
        UiFrame.Run("align", () =>
        {
            frame = ImGui.GetFrameHeight();
            var y = ImGui.GetCursorPosY();
            Controls.AlignMid(frame / 2);
            shortDelta = ImGui.GetCursorPosY() - y;
            ImGui.SetCursorPosY(y);
            Controls.AlignMid(frame * 3);
            tallDelta = ImGui.GetCursorPosY() - y;
        });

        Assert.Equal(frame / 4, shortDelta, 0.01f);
        Assert.Equal(0f, tallDelta);
    }

    [Fact]
    public void IconToggleLatchesOnClickAndRingsTheOnState()
    {
        var on = false;
        var changed = false;
        var at = Vector2.Zero;
        uint id = 0, expectedId = 0;
        float width = 0;
        int offVerts = 0, onVerts = 0;
        void Body()
        {
            var start = Verts();
            changed |= Controls.IconToggle("M", ref on, id: "mapicon", width: 40f);
            if (on) onVerts = Verts() - start;
            else offVerts = Verts() - start;
            at = Centre();
            id = ImGui.GetItemID();
            expectedId = ImGui.GetID("M##mapicon");
            width = ImGui.GetItemRectSize().X;
        }

        UiFrame.Run("icon toggle", Body);
        UiFrame.Click("icon toggle", at, Body);

        Assert.True(on);
        Assert.True(changed);
        Assert.Equal(expectedId, id);
        Assert.Equal(40f, width, 0.01f);
        Assert.True(onVerts > offVerts);
    }

    [Fact]
    public void IconToggleIdDefaultsToTheGlyphAndTooltipShowsOnHover()
    {
        var on = true;
        var at = Vector2.Zero;
        uint id = 0, expectedId = 0;
        void Body()
        {
            Controls.IconToggle("W", ref on, "World icon at 50%");
            at = Centre();
            id = ImGui.GetItemID();
            expectedId = ImGui.GetID("W##W");
        }

        try
        {
            UiFrame.Run("icon toggle tip", Body);
            Away("icon toggle tip", at, Body);
            var idle = RenderedWindows();
            UiFrame.Hover("icon toggle tip", at, Body);

            Assert.Equal(expectedId, id);
            Assert.Equal(idle + 1, RenderedWindows());
            Assert.True(on);
        }
        finally
        {
            UiFrame.Unhover();
        }
    }

    [Fact]
    public void TriStateClickCyclesShiftClickGoesBackRightClickResets()
    {
        var v = 0;
        var changed = false;
        var at = Vector2.Zero;
        void Body()
        {
            changed |= Controls.TriState(null, ref v, "tri");
            at = Centre();
        }

        var io = UiFrame.Io();
        try
        {
            UiFrame.Run("tri click", Body);
            UiFrame.Click("tri click", at, Body);
            var afterFirst = v;
            var firstChanged = changed;

            UiFrame.Click("tri click", at, Body);
            var afterSecond = v;

            io.AddKeyEvent(ImGuiKey.ModShift, true);
            UiFrame.Run("tri click", Body);
            UiFrame.Click("tri click", at, Body);
            var afterShift = v;
            io.AddKeyEvent(ImGuiKey.ModShift, false);
            UiFrame.Run("tri click", Body);

            UiFrame.Click("tri click", at, Body, 1);
            var afterRight = v;

            changed = false;
            UiFrame.Click("tri click", at, Body, 1);
            var rightOnAny = changed;

            Assert.True(firstChanged);
            Assert.Equal(1, afterFirst);
            Assert.Equal(2, afterSecond);
            Assert.Equal(1, afterShift);
            Assert.Equal(0, afterRight);
            Assert.False(rightOnAny);
        }
        finally
        {
            io.AddKeyEvent(ImGuiKey.ModShift, false);
            UiFrame.Unhover();
        }
    }

    [Theory]
    [InlineData(0, ImGuiCol.TextDisabled)]
    [InlineData(1, ImGuiCol.CheckMark)]
    public void TriStateGlyphIsDrawnInTheStateColour(int state, ImGuiCol expected)
    {
        var v = state;
        HashSet<uint> colours = [];
        uint want = 0;
        UiFrame.Unhover();
        UiFrame.Run("tri colour " + state, () =>
        {
            var start = Verts();
            Controls.TriState(null, ref v, "c");
            colours = VertexColoursSince(start);
            want = ImGui.GetColorU32(expected);
        });

        Assert.Contains(want, colours);
    }

    [Fact]
    public void TriStateExcludeGlyphIsDrawnInTheBadColourAndOthersAreNot()
    {
        var v = 2;
        HashSet<uint> exclude = [], any = [];
        uint bad = EColor.U32(EColor.Bad);
        UiFrame.Unhover();
        UiFrame.Run("tri bad", () =>
        {
            var start = Verts();
            Controls.TriState(null, ref v, "bad");
            exclude = VertexColoursSince(start);
            v = 0;
            start = Verts();
            Controls.TriState(null, ref v, "any");
            any = VertexColoursSince(start);
        });

        Assert.Contains(bad, exclude);
        Assert.DoesNotContain(bad, any);
    }

    [Fact]
    public void IconToggleDrawsOnAndOffStatesInTheirOwnColours()
    {
        bool on = true, off = false;
        HashSet<uint> onColours = [], offColours = [];
        uint activeFill = 0, frameFill = 0, text = 0, disabledText = 0;
        UiFrame.Unhover();
        UiFrame.Run("icon colours", () =>
        {
            var start = Verts();
            Controls.IconToggle("A", ref on);
            onColours = VertexColoursSince(start);
            start = Verts();
            Controls.IconToggle("B", ref off);
            offColours = VertexColoursSince(start);
            activeFill = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            frameFill = ImGui.GetColorU32(ImGuiCol.FrameBg);
            text = ImGui.GetColorU32(ImGuiCol.Text);
            disabledText = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        });

        Assert.Contains(activeFill, onColours);
        Assert.Contains(text, onColours);
        Assert.DoesNotContain(frameFill, onColours);
        Assert.Contains(frameFill, offColours);
        Assert.Contains(disabledText, offColours);
        Assert.DoesNotContain(activeFill, offColours);
    }

    [Fact]
    public void TriStateBoxIsTheGivenWidthAndGrowsToFitTheScaledGlyph()
    {
        var v = 0;
        Vector2 plain = default, big = default;
        float rowHeight = 0, padY = 0, baseSize = 0;
        UiFrame.Run("tri size", () =>
        {
            Controls.TriState(null, ref v, "plain", box: 30f, glyphScale: 1f);
            plain = ImGui.GetItemRectSize();
            Controls.TriState(null, ref v, "big", box: 30f, glyphScale: 3f);
            big = ImGui.GetItemRectSize();
            rowHeight = ImGui.GetFrameHeight();
            padY = ImGui.GetStyle().FramePadding.Y;
            baseSize = ImGui.GetStyle().FontSizeBase;
        });

        Assert.Equal(30f, plain.X, 0.01f);
        Assert.Equal(30f, big.X, 0.01f);
        Assert.Equal(Math.Max(rowHeight, baseSize + padY * 2), plain.Y, 0.01f);
        Assert.Equal(Math.Max(rowHeight, baseSize * 3 + padY * 2), big.Y, 0.01f);
        Assert.True(big.Y > plain.Y);
    }

    [Fact]
    public void TriStateLabelSitsRightOfTheBoxAndFollowsTheButtonHeight()
    {
        var v = 0;
        float start = 0, labelLeft = 0, spacing = 0;
        float smallMid = 0, bigMid = 0, smallHeight = 0, bigHeight = 0;
        UiFrame.Run("tri label", () =>
        {
            start = ImGui.GetCursorScreenPos().X;
            var top = ImGui.GetCursorScreenPos().Y;
            Controls.TriState("Name", ref v, "small", box: 30f, glyphScale: 1f);
            labelLeft = ImGui.GetItemRectMin().X;
            smallMid = (ImGui.GetItemRectMin().Y + ImGui.GetItemRectMax().Y) / 2 - top;
            smallHeight = Math.Max(ImGui.GetFrameHeight(), ImGui.GetStyle().FontSizeBase + ImGui.GetStyle().FramePadding.Y * 2);
            spacing = ImGui.GetStyle().ItemSpacing.X;
            top = ImGui.GetCursorScreenPos().Y;
            Controls.TriState("Name", ref v, "big", box: 30f, glyphScale: 3f);
            bigMid = (ImGui.GetItemRectMin().Y + ImGui.GetItemRectMax().Y) / 2 - top;
            bigHeight = Math.Max(ImGui.GetFrameHeight(), ImGui.GetStyle().FontSizeBase * 3 + ImGui.GetStyle().FramePadding.Y * 2);
        });

        Assert.Equal(start + 30f + spacing, labelLeft, 0.5f);
        Assert.Equal((bigHeight - smallHeight) / 2, bigMid - smallMid, 0.5f);
    }

    [Fact]
    public void TriStateGridWritesTheClickedCellBackIntoTheArray()
    {
        Controls.TriItem[] items = [new("A"), new("B"), new("C")];
        var changed = false;
        var origin = Vector2.Zero;
        float availWidth = 0, frame = 0;
        Vector2 cellPad = default;
        void Body()
        {
            origin = ImGui.GetCursorScreenPos();
            availWidth = ImGui.GetContentRegionAvail().X;
            cellPad = ImGui.GetStyle().CellPadding;
            frame = ImGui.GetFrameHeight();
            changed |= Controls.TriStateGrid("grid", items, 2);
        }

        UiFrame.Run("grid click", Body);
        var secondColumn = new Vector2(origin.X + availWidth / 2 + cellPad.X + 22f / 2, origin.Y + cellPad.Y + frame / 2);
        UiFrame.Click("grid click", secondColumn, Body);

        Assert.True(changed);
        Assert.Equal([0, 1, 0], items.Select(i => i.Value));
    }

    [Fact]
    public void TriStateGridClampsColumnsAndIgnoresEmptyInput()
    {
        Controls.TriItem[] items = [new("A"), new("B")];
        var changed = false;
        var origin = Vector2.Zero;
        Vector2 cellPad = default;
        var frame = 0f;
        var spacingY = 0f;
        var emptyResults = new List<bool>();
        var emptyMoved = 0f;
        void Body()
        {
            origin = ImGui.GetCursorScreenPos();
            cellPad = ImGui.GetStyle().CellPadding;
            frame = ImGui.GetFrameHeight();
            spacingY = ImGui.GetStyle().ItemSpacing.Y;
            changed |= Controls.TriStateGrid("grid", items, 0);
            var y = ImGui.GetCursorPosY();
            emptyResults.Add(Controls.TriStateGrid("none", null, 2));
            emptyResults.Add(Controls.TriStateGrid("empty", [], 2));
            emptyMoved = ImGui.GetCursorPosY() - y;
        }

        UiFrame.Run("grid clamp", Body);
        var secondRow = new Vector2(origin.X + cellPad.X + 22f / 2, origin.Y + cellPad.Y * 3 + frame * 1.5f + spacingY);
        UiFrame.Click("grid clamp", secondRow, Body);

        Assert.True(changed);
        Assert.Equal([0, 1], items.Select(i => i.Value));
        Assert.All(emptyResults, Assert.False);
        Assert.Equal(0f, emptyMoved);
    }

    [Fact]
    public void PanelStyleScopeSetsTheVarsAndRestoresThemOnDispose()
    {
        Vector2 padBefore = default, padInside = default, padAfter = default;
        float roundingBefore = 0, roundingInside = 0, roundingAfter = 0;
        float[] otherInside = [];
        float[] otherBefore = [];
        float[] otherAfter = [];
        UiFrame.Run("panel style", () =>
        {
            var style = ImGui.GetStyle();
            padBefore = style.FramePadding;
            roundingBefore = style.FrameRounding;
            otherBefore = [style.GrabRounding, style.ChildRounding, style.PopupRounding];
            using (new Controls.PanelStyleScope())
            {
                padInside = style.FramePadding;
                roundingInside = style.FrameRounding;
                otherInside = [style.GrabRounding, style.ChildRounding, style.PopupRounding];
            }
            padAfter = style.FramePadding;
            roundingAfter = style.FrameRounding;
            otherAfter = [style.GrabRounding, style.ChildRounding, style.PopupRounding];
        });

        Assert.Equal(new Vector2(6, 3), padInside);
        Assert.Equal(3f, roundingInside);
        Assert.Equal([3f, 3f, 3f], otherInside);
        Assert.Equal(otherBefore, otherAfter);
        Assert.Equal(padBefore, padAfter);
        Assert.Equal(roundingBefore, roundingAfter);
    }
}
