using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class ChipsTests
{
    const string Window = "chips";
    static readonly List<string> Items = ["Fire", "Cold"];

    sealed class Rig(string id, params string[] tags)
    {
        public readonly List<string> Tags = [.. tags];
        public bool Changed;
        public Vector2 Origin;
        public Func<string, bool>? Validate;
        public bool CaseInsensitiveDedup = true;

        public void Body()
        {
            Origin = ImGui.GetCursorScreenPos();
            Changed |= Chips.Draw(id, Tags, "add...", CaseInsensitiveDedup, Validate);
        }

        public void Idle() => UiFrame.Run(Window, Body);
    }

    static float Metric(Func<float> read)
    {
        var value = 0f;
        UiFrame.Run(Window, () => value = read());
        return value;
    }

    static float FrameHeight() => Metric(() => ImGui.GetFrameHeight());

    static float ItemSpacingX() => Metric(() => ImGui.GetStyle().ItemSpacing.X);

    static float ButtonWidth(string label) => Metric(() => ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2);

    static float PlusX() => 160f + ItemSpacingX() + ButtonWidth("+") / 2;

    static float ChipEnd(string label) => ButtonWidth(label) + 2f + ButtonWidth("x");

    static void Focus(Rig rig)
    {
        rig.Idle();
        var rowBelowChips = rig.Tags.Count > 0 ? FrameHeight() + Metric(() => ImGui.GetStyle().ItemSpacing.Y) * 2 : 0f;
        UiFrame.Click(Window, rig.Origin + new Vector2(20f, rowBelowChips + FrameHeight() / 2), rig.Body);
    }

    static void Type(Rig rig, string text)
    {
        Focus(rig);
        UiFrame.Io().AddInputCharactersUTF8(text);
        rig.Idle();
    }

    static void PressEnter(Rig rig)
    {
        var io = UiFrame.Io();
        io.AddKeyEvent(ImGuiKey.Enter, true);
        rig.Idle();
        io.AddKeyEvent(ImGuiKey.Enter, false);
        rig.Idle();
    }

    [Fact]
    public void ANewTagIsAccepted() => Assert.True(Chips.ShouldAdd("Lightning", Items, true, null));

    [Fact]
    public void DedupIsCaseInsensitiveByDefault() => Assert.False(Chips.ShouldAdd("fire", Items, true, null));

    [Fact]
    public void DedupOffAllowsACaseDifferentDuplicate() => Assert.True(Chips.ShouldAdd("fire", Items, false, null));

    [Fact]
    public void AnExactDuplicateIsStillRejectedWithDedupOff() => Assert.False(Chips.ShouldAdd("Fire", Items, false, null));

    [Fact]
    public void BlankTextIsNeverAdded() => Assert.False(Chips.ShouldAdd("  ", Items, true, null));

    [Fact]
    public void NullTextDoesNotThrow() => Assert.False(Chips.ShouldAdd(null, Items, true, null));

    [Fact]
    public void AFailedValidatorRejects() => Assert.False(Chips.ShouldAdd("Bad", Items, true, s => s.Length > 5));

    [Fact]
    public void APassingValidatorAllows() => Assert.True(Chips.ShouldAdd("Chaos", Items, true, s => s.Length > 3));

    [Fact]
    public void ADrawWithNoInputChangesNothing()
    {
        var rig = new Rig("idle", "Fire", "Cold", "A much longer tag that has to wrap", "x");
        rig.Idle();
        rig.Idle();
        Assert.False(rig.Changed);
        Assert.Equal(["Fire", "Cold", "A much longer tag that has to wrap", "x"], rig.Tags);
    }

    [Fact]
    public void ClickingAChipsXRemovesThatChipAndReportsTheChange()
    {
        var rig = new Rig("remove", "Fire", "Cold");
        rig.Idle();
        var at = rig.Origin + new Vector2(ButtonWidth("Fire") + 2f + 1f, FrameHeight() / 2);

        UiFrame.Click(Window, at, rig.Body);

        Assert.True(rig.Changed);
        Assert.Equal(["Cold"], rig.Tags);
    }

    [Fact]
    public void ClickingTheSecondChipsXLeavesTheFirst()
    {
        var rig = new Rig("removeSecond", "Fire", "Cold");
        rig.Idle();
        var at = rig.Origin + new Vector2(ChipEnd("Fire") + ItemSpacingX() + ButtonWidth("Cold") + 2f + ButtonWidth("x") / 2, FrameHeight() / 2);

        UiFrame.Click(Window, at, rig.Body);

        Assert.True(rig.Changed);
        Assert.Equal(["Fire"], rig.Tags);
    }

    [Fact]
    public void TypingThenEnterAddsTheTagAndClearsTheBox()
    {
        var rig = new Rig("enter");
        try
        {
            Type(rig, "Chaos");
            PressEnter(rig);
            Assert.True(rig.Changed);
            Assert.Equal(["Chaos"], rig.Tags);

            rig.Changed = false;
            rig.Tags.Clear();
            Focus(rig);
            PressEnter(rig);
            Assert.False(rig.Changed);
            Assert.Empty(rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void LeavingTheBoxWithoutEnterAddsNothing()
    {
        var rig = new Rig("leave");
        try
        {
            Type(rig, "Chaos");
            UiFrame.ClickAway(Window, rig.Body);
            Assert.False(rig.Changed);
            Assert.Empty(rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void EscapeOutOfTheBoxAddsNothing()
    {
        var rig = new Rig("escape");
        var io = UiFrame.Io();
        try
        {
            Type(rig, "Chaos");
            io.AddKeyEvent(ImGuiKey.Escape, true);
            rig.Idle();
            io.AddKeyEvent(ImGuiKey.Escape, false);
            rig.Idle();
            Assert.False(rig.Changed);
            Assert.Empty(rig.Tags);
        }
        finally
        {
            io.AddKeyEvent(ImGuiKey.Escape, false);
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void KeypadEnterAddsTheTagToo()
    {
        var rig = new Rig("keypad");
        try
        {
            Type(rig, "Chaos");
            var io = UiFrame.Io();
            io.AddKeyEvent(ImGuiKey.KeypadEnter, true);
            rig.Idle();
            io.AddKeyEvent(ImGuiKey.KeypadEnter, false);
            rig.Idle();
            Assert.True(rig.Changed);
            Assert.Equal(["Chaos"], rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void DuplicateLabelsInOneListRemoveTheClickedChipOnly()
    {
        var rig = new Rig("dupLabels", "A", "B", "A") { CaseInsensitiveDedup = false };
        rig.Idle();
        var gap = ItemSpacingX();
        var at = rig.Origin + new Vector2(ChipEnd("A") + gap + ChipEnd("B") + gap + ButtonWidth("A") + 2f + 1f, FrameHeight() / 2);

        UiFrame.Click(Window, at, rig.Body);

        Assert.True(rig.Changed);
        Assert.Equal(["A", "B"], rig.Tags);
    }

    [Fact]
    public void TwoListsSharingAChipLabelAndTheBoxIdStayIndependent()
    {
        List<string> first = ["Fire"];
        List<string> second = ["Fire"];
        var secondOrigin = Vector2.Zero;
        void Body()
        {
            Chips.Draw("one", first);
            secondOrigin = ImGui.GetCursorScreenPos();
            Chips.Draw("two", second);
        }
        var io = UiFrame.Io();
        try
        {
            UiFrame.Run(Window, Body);
            var frame = FrameHeight();
            var spacing = Metric(() => ImGui.GetStyle().ItemSpacing.Y);
            var inputY = frame + spacing * 2 + frame / 2;
            UiFrame.Click(Window, secondOrigin + new Vector2(20f, inputY), Body);
            io.AddInputCharactersUTF8("Chaos");
            UiFrame.Run(Window, Body);
            io.AddKeyEvent(ImGuiKey.Enter, true);
            UiFrame.Run(Window, Body);
            io.AddKeyEvent(ImGuiKey.Enter, false);
            UiFrame.Run(Window, Body);
            Assert.Equal(["Fire"], first);
            Assert.Equal(["Fire", "Chaos"], second);

            UiFrame.Click(Window, secondOrigin + new Vector2(ButtonWidth("Fire") + 2f + 1f, frame / 2), Body);
            Assert.Equal(["Fire"], first);
            Assert.Equal(["Chaos"], second);
        }
        finally
        {
            io.AddKeyEvent(ImGuiKey.Enter, false);
            UiFrame.ClickAway(Window, Body);
        }
    }

    [Fact]
    public void EnterWhileTheBoxIsNotFocusedAddsNothing()
    {
        var rig = new Rig("unfocused");
        try
        {
            Type(rig, "Chaos");
            UiFrame.ClickAway(Window, rig.Body);
            PressEnter(rig);
            Assert.False(rig.Changed);
            Assert.Empty(rig.Tags);
            Focus(rig);
            PressEnter(rig);
            Assert.Equal(["Chaos"], rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void ChipLabelsAreDrawnOnTheFrameBackgroundColour()
    {
        int FrameBgVerts(params string[] tags)
        {
            var count = 0;
            var list = tags.ToList();
            UiFrame.Run(Window, () =>
            {
                var draw = ImGui.GetWindowDrawList();
                var frameBg = ImGui.GetColorU32(ImGuiCol.FrameBg);
                var start = draw.VtxBuffer.Size;
                Chips.Draw("colours" + tags.Length, list);
                for (var i = start; i < draw.VtxBuffer.Size; i++) if (draw.VtxBuffer[i].Col == frameBg) count++;
            });
            return count;
        }

        Assert.True(FrameBgVerts("Fire") > FrameBgVerts());
    }

    [Fact]
    public void TypingThenThePlusButtonAddsTheTag()
    {
        var rig = new Rig("plus");
        try
        {
            Type(rig, "Chaos");
            UiFrame.Click(Window, rig.Origin + new Vector2(PlusX(), FrameHeight() / 2), rig.Body);
            Assert.True(rig.Changed);
            Assert.Equal(["Chaos"], rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void ThePlusButtonWithAnEmptyBoxAddsNothing()
    {
        var rig = new Rig("emptyPlus");
        rig.Idle();
        UiFrame.Click(Window, rig.Origin + new Vector2(PlusX(), FrameHeight() / 2), rig.Body);
        Assert.False(rig.Changed);
        Assert.Empty(rig.Tags);
    }

    [Fact]
    public void ARejectedEntryStaysInTheBoxSoALaterAcceptanceAddsIt()
    {
        var accept = false;
        var rig = new Rig("rejected", "Fire") { Validate = _ => accept };
        try
        {
            Type(rig, "Chaos");
            PressEnter(rig);
            Assert.False(rig.Changed);
            Assert.Equal(["Fire"], rig.Tags);

            accept = true;
            Focus(rig);
            PressEnter(rig);
            Assert.True(rig.Changed);
            Assert.Equal(["Fire", "Chaos"], rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void ADuplicateIsRejectedUnlessDedupIsOff()
    {
        var rig = new Rig("dup", "Fire");
        try
        {
            Type(rig, "fire");
            PressEnter(rig);
            Assert.False(rig.Changed);
            Assert.Equal(["Fire"], rig.Tags);

            rig.CaseInsensitiveDedup = false;
            Focus(rig);
            PressEnter(rig);
            Assert.True(rig.Changed);
            Assert.Equal(["Fire", "fire"], rig.Tags);
        }
        finally
        {
            UiFrame.ClickAway(Window, rig.Body);
        }
    }

    [Fact]
    public void TheSameIdUnderTwoParentScopesKeepsSeparateBoxes()
    {
        List<string> first = [];
        List<string> second = [];
        var origin = Vector2.Zero;
        var secondOrigin = Vector2.Zero;
        void Body()
        {
            origin = ImGui.GetCursorScreenPos();
            ImGui.PushID("scopeA");
            Chips.Draw("tags", first);
            ImGui.PopID();
            secondOrigin = ImGui.GetCursorScreenPos();
            ImGui.PushID("scopeB");
            Chips.Draw("tags", second);
            ImGui.PopID();
        }
        var io = UiFrame.Io();
        try
        {
            UiFrame.Run(Window, Body);
            var middle = FrameHeight() / 2;
            UiFrame.Click(Window, origin + new Vector2(20f, middle), Body);
            io.AddInputCharactersUTF8("A");
            UiFrame.Run(Window, Body);

            UiFrame.Click(Window, secondOrigin + new Vector2(PlusX(), middle), Body);
            Assert.Empty(first);
            Assert.Empty(second);

            UiFrame.Click(Window, origin + new Vector2(PlusX(), middle), Body);
            Assert.Equal(["A"], first);
            Assert.Empty(second);
        }
        finally
        {
            UiFrame.ClickAway(Window, Body);
        }
    }

    [Fact]
    public void ChipsWrapOntoANewRowOnlyWhenTheNextOneDoesNotFit()
    {
        var fit = 0f;
        var padding = 0f;
        UiFrame.Run(Window, () =>
        {
            var style = ImGui.GetStyle();
            float Chip(string label) => ImGui.CalcTextSize(label).X + style.FramePadding.X * 2 + 2f + ImGui.CalcTextSize("x").X + style.FramePadding.X * 2;
            fit = Chip("Fire") + style.ItemSpacing.X + Chip("Cold");
            ImGui.BeginChild("probe", new Vector2(400, 300), ImGuiChildFlags.None);
            padding = 400 - ImGui.GetContentRegionAvail().X;
            ImGui.EndChild();
        });

        float Height(string name, float widthDelta)
        {
            List<string> tags = ["Fire", "Cold"];
            var delta = 0f;
            UiFrame.Run(Window, () =>
            {
                ImGui.BeginChild(name, new Vector2(fit + padding + widthDelta, 300), ImGuiChildFlags.None);
                ImGui.TextUnformatted("above");
                var before = ImGui.GetCursorScreenPos().Y;
                Chips.Draw(name, tags);
                delta = ImGui.GetCursorScreenPos().Y - before;
                ImGui.EndChild();
            });
            return delta;
        }

        var oneRow = Height("fits", 1f);
        var twoRows = Height("wraps", -1f);
        var rowStep = Metric(() => ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y);
        var frame = FrameHeight();
        var spacing = Metric(() => ImGui.GetStyle().ItemSpacing.Y);
        Assert.Equal(rowStep, twoRows - oneRow, 1e-3f);
        Assert.Equal(frame * 2 + spacing * 3, oneRow, 1e-3f);
    }
}
