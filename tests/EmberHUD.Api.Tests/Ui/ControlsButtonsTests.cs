using System.Drawing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class ControlsButtonsTests : IDisposable
{
    enum Speed { Slow, Medium, Fast }

    static readonly Controls.GroupButton[] Plain = [new("One"), new("Two"), new("Three")];
    static readonly Color Light = Color.FromArgb(255, 240, 230, 200);
    static readonly Color Dark = Color.FromArgb(255, 70, 110, 70);
    static readonly uint Black = 0xFF000000;
    static readonly ImTextureRef Texture = new() { TexID = new ImTextureID(1ul) };
    static readonly GridAtlas Atlas = new(64, 64, 32, 2);

    static Controls.IconSheet[] Sheets(ImTextureRef? texture) => [Controls.IconSheet.Grid("Demo", texture, Atlas, 4)];

    static uint Packed(Color c) => EColor.U32(c);

    static List<ImDrawVert> Verts(Action body, Vector2? at = null, bool held = false)
    {
        var io = UiFrame.Io();
        List<ImDrawVert> verts = [];
        if (at is { } p)
        {
            io.AddMousePosEvent(p.X, p.Y);
            UiFrame.Run("test", body);
            if (held)
            {
                io.AddMouseButtonEvent(0, true);
                UiFrame.Run("test", body);
            }
            UiFrame.Run("test", body);
        }
        else UiFrame.Unhover();
        UiFrame.Run("test", () => verts.AddRange(UiFrame.Drawn(body)));
        return verts;
    }

    static (Vector2 Min, Vector2 Max) LastItem(Action body)
    {
        (Vector2, Vector2) rect = default;
        UiFrame.Run("test", () =>
        {
            body();
            rect = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        });
        return rect;
    }

    static Vector2 CentreOf(Action body)
    {
        var (min, max) = LastItem(body);
        return (min + max) / 2;
    }

    static Vector2[] RowCentres(Action body, int count)
    {
        var row = Vector2.Zero;
        UiFrame.Run("test", () =>
        {
            row = ImGui.GetCursorScreenPos() + new Vector2(0, ImGui.GetFrameHeight() / 2);
            body();
        });
        var centres = UiFrame.ItemRuns("test", body, row, row + new Vector2(800f, 0)).Select(r => r.Centre).ToArray();
        Assert.Equal(count, centres.Length);
        return centres;
    }

    static bool PopupOpen(string id)
    {
        ImGui.PushID(id);
        var open = ImGui.IsPopupOpen("##menu");
        ImGui.PopID();
        return open;
    }

    static List<UiFrame.ItemRun> MenuItems(Action body)
    {
        var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup);
        var x = popup.Pos.X + popup.Size.X / 2;
        return UiFrame.ItemRuns("test", body, new Vector2(x, popup.Pos.Y), new Vector2(x, popup.Pos.Y + popup.Size.Y));
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void AGroupNeedsTwoAndCapsAtFive()
    {
        Assert.Equal(0, Controls.GroupCount(0));
        Assert.Equal(0, Controls.GroupCount(1));
        Assert.Equal(2, Controls.GroupCount(2));
        Assert.Equal(5, Controls.GroupCount(5));
        Assert.Equal(5, Controls.GroupCount(9));
    }

    [Fact]
    public void ASplitLeavesThePrimaryTheRest()
    {
        Assert.Equal(80f, Controls.SplitPrimaryWidth(100f, 20f));
        Assert.Equal(79f, Controls.SplitPrimaryWidth(100f, 20f, 1f));
        Assert.Equal(1f, Controls.SplitPrimaryWidth(10f, 20f));
    }

    [Fact]
    public void ACompoundButtonIsTheTallerOfTextAndIconPlusPadding()
    {
        Assert.Equal(26f, Controls.CompoundHeight(10f, 8f, 2f, 0f, 3f));
        Assert.Equal(16f, Controls.CompoundHeight(10f, 0f, 2f, 0f, 3f));
        Assert.Equal(46f, Controls.CompoundHeight(10f, 8f, 2f, 40f, 3f));
    }

    [Fact]
    public void FitItemsReservesNoOverflowSlotWhenEverythingFits()
    {
        float[] widths = [30f, 30f, 30f];
        Assert.Equal(3, Controls.FitItems(widths, 100f, 5f, 20f));
        Assert.Equal(2, Controls.FitItems(widths, 95f, 5f, 20f));
        Assert.Equal(0, Controls.FitItems(widths, 40f, 5f, 20f));
        Assert.Equal(0, Controls.FitItems([], 100f, 5f, 20f));
        Assert.Equal(0, Controls.FitItems(null, 100f, 5f, 20f));
    }

    [Fact]
    public void ButtonsDrawOneBalancedFrameWithoutClicks()
    {
        var texture = new ImTextureRef { TexID = new ImTextureID(1ul) };
        Controls.IconSheet[] sheets = [Controls.IconSheet.Grid("Demo", texture, new GridAtlas(64, 64, 32, 2), 4)];
        Controls.GroupButton[] buttons = [new("Low", Color.FromArgb(255, 70, 110, 70)), new("Mid"), new("High", Sheet: "Demo", Cell: 1, Side: Controls.IconSide.Right)];
        var selected = 1;
        var speed = Speed.Medium;
        int hit = 0, menu = 0, overflow = 0;
        var changed = false;

        UiFrame.Run("test", () =>
        {
            hit = Controls.ButtonGroup("actions", buttons, sheets: sheets);
            changed |= Controls.ButtonGroup("pick", ref selected, buttons, sheets, desaturate: true);
            changed |= Controls.ButtonGroup("speed", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast));
            menu = Controls.MenuButton("Save", ["Save as...", null, "Revert"]);
            changed |= Controls.SplitButton("Run", ["Run all", "Run one"], out _);
            changed |= Controls.CompoundButton(new Controls.GroupButton("Import", Sheet: "Demo"), "pull from a 100% file", sheets);
            overflow = Controls.OverflowRow("tools", ["Reload", "Import", "Export", "Validate", "Reset", "Diagnostics"], 200f);
        });

        Assert.Equal(-1, hit);
        Assert.Equal(-1, menu);
        Assert.Equal(-1, overflow);
        Assert.False(changed);
        Assert.Equal(Speed.Medium, speed);
    }

    [Fact]
    public void AGroupReturnsTheIndexOfTheButtonClicked()
    {
        var hit = -1;

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("g", Plain), 3)[1], () =>
        {
            var clicked = Controls.ButtonGroup("g", Plain);
            if (clicked >= 0) hit = clicked;
        });

        Assert.Equal(1, hit);
    }

    [Fact]
    public void ANullGroupReturnsMinusOne() => UiFrame.Run("test", () => Assert.Equal(-1, Controls.ButtonGroup("g", null)));

    [Fact]
    public void ASingleButtonIsNotAGroupAndDrawsNothing()
    {
        UiFrame.Run("test", () =>
        {
            var before = ImGui.GetCursorPos();
            var hit = Controls.ButtonGroup("g", [new("only")]);
            Assert.Equal(-1, hit);
            Assert.Equal(before, ImGui.GetCursorPos());
        });
    }

    [Fact]
    public void ASixthButtonIsNotDrawn()
    {
        string[] labels = ["a", "b", "c", "d", "e", "f"];
        var buttons = labels.Select(l => new Controls.GroupButton(l)).ToArray();
        float right = 0, expected = 0;

        UiFrame.Run("test", () =>
        {
            var start = ImGui.GetCursorScreenPos().X;
            Controls.ButtonGroup("g", buttons);
            right = ImGui.GetItemRectMax().X;
            expected = start + labels.Take(5).Sum(l => ImGui.CalcTextSize(l).X + ImGui.GetStyle().FramePadding.X * 2) + 4;
        });

        Assert.Equal(expected, right, 0.01f);
    }

    [Fact]
    public void ClickingAnotherButtonMovesThePick()
    {
        var selected = 0;
        var changed = false;

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("g", ref selected, Plain), 3)[2], () => changed |= Controls.ButtonGroup("g", ref selected, Plain));

        Assert.True(changed);
        Assert.Equal(2, selected);
    }

    [Fact]
    public void ClickingThePickedButtonIsNoChange()
    {
        var selected = 1;
        var changed = false;

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("g", ref selected, Plain), 3)[1], () => changed |= Controls.ButtonGroup("g", ref selected, Plain));

        Assert.False(changed);
        Assert.Equal(1, selected);
    }

    [Fact]
    public void ThePickFillsWithTheHeaderColourAndTheRestFade()
    {
        uint header = 0, plain = 0, faded = 0;
        var verts = Verts(() =>
        {
            var style = ImGui.GetStyle();
            header = Packed(EColor.FromVector4(style.Colors[(int)ImGuiCol.Header]));
            plain = Packed(EColor.FromVector4(style.Colors[(int)ImGuiCol.Button]));
            faded = Packed(EColor.Fade(EColor.FromVector4(style.Colors[(int)ImGuiCol.Button]), 0.3f));
            Controls.ButtonGroup("g", Plain, selected: 1);
        });

        Assert.NotEqual(header, plain);
        Assert.Contains(verts, v => v.Col == header);
        Assert.Contains(verts, v => v.Col == faded);
        Assert.DoesNotContain(verts, v => v.Col == plain);
    }

    [Fact]
    public void AColouredLoserFadesToThirtyPercentAlpha()
    {
        Controls.GroupButton[] buttons = [new("A", Dark), new("B", Dark)];

        var verts = Verts(() => Controls.ButtonGroup("g", buttons, selected: 0));

        Assert.Contains(verts, v => v.Col == Packed(Dark));
        Assert.Contains(verts, v => v.Col == Packed(EColor.Fade(Dark, 0.3f)));
        Assert.DoesNotContain(verts, v => v.Col == Packed(EColor.Desaturate(Dark)));
    }

    [Fact]
    public void DesaturateNeverDerivesAColourFromTheButtonStyleColour()
    {
        UiFrame.Io();
        var colours = ImGui.GetStyle().Colors;
        var button = colours[(int)ImGuiCol.Button];
        try
        {
            colours[(int)ImGuiCol.Button] = new Vector4(1 / 255f, 2 / 255f, 9 / 255f, 1);

            var verts = Verts(() => Controls.ButtonGroup("g", Plain, selected: 0, desaturate: true));

            Assert.Contains(verts, v => (v.Col & 0xFFFF) == 0x0201);
        }
        finally
        {
            colours[(int)ImGuiCol.Button] = button;
        }
    }

    [Fact]
    public void DesaturateGreysTheLosersInsteadOfFadingThem()
    {
        Controls.GroupButton[] buttons = [new("A", Dark), new("B", Dark)];

        var verts = Verts(() => Controls.ButtonGroup("g", buttons, selected: 0, desaturate: true));

        Assert.Contains(verts, v => v.Col == Packed(Dark));
        Assert.Contains(verts, v => v.Col == Packed(EColor.Desaturate(Dark)));
        Assert.DoesNotContain(verts, v => v.Col == Packed(EColor.Fade(Dark, 0.3f)));
    }

    [Fact]
    public void AColouredButtonFillsWithItsColourAndPicksContrastText()
    {
        Controls.GroupButton[] buttons = [new("A", Light), new("B")];

        var verts = Verts(() => Controls.ButtonGroup("g", buttons));

        Assert.Contains(verts, v => v.Col == Packed(Light));
        Assert.Contains(verts, v => v.Col == Black);
    }

    [Fact]
    public void HoveringAColouredButtonBrightensItsFill()
    {
        Controls.GroupButton[] buttons = [new("A", Dark), new("B")];

        var verts = Verts(() => Controls.ButtonGroup("g", buttons), RowCentres(() => Controls.ButtonGroup("g", buttons), 2)[0]);

        Assert.Contains(verts, v => v.Col == Packed(EColor.Scale(Dark, 1.2f)));
    }

    [Fact]
    public void HoldingAColouredButtonDarkensItsFill()
    {
        Controls.GroupButton[] buttons = [new("A", Dark), new("B")];

        var verts = Verts(() => Controls.ButtonGroup("g", buttons), RowCentres(() => Controls.ButtonGroup("g", buttons), 2)[0], held: true);

        Assert.Contains(verts, v => v.Col == Packed(EColor.Scale(Dark, 0.8f)));
    }

    [Theory]
    [InlineData(Controls.IconSide.Left)]
    [InlineData(Controls.IconSide.Right)]
    public void TheIconSitsOnItsSideDrawsItsCellAndTakesTheTextColour(Controls.IconSide side)
    {
        Controls.GroupButton[] buttons = [new("A"), new("High", Light, "Demo", 1, side)];
        var (uv0, uv1) = Atlas.UVPair(1);
        Vector2 min = default, max = default;
        float pad = 0, size = 16f;

        var verts = Verts(() =>
        {
            Controls.ButtonGroup("g", buttons, sheets: Sheets(Texture), iconSize: size);
            (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            pad = ImGui.GetStyle().FramePadding.X;
        });

        var topLeft = verts.Single(v => v.Uv == uv0 && v.Col == Black);
        var bottomRight = verts.Single(v => v.Uv == uv1 && v.Col == Black);
        var expectedX = side == Controls.IconSide.Left ? min.X + pad : max.X - pad - size;
        Assert.Equal(expectedX, topLeft.Pos.X, 0.01f);
        Assert.Equal(size, bottomRight.Pos.X - topLeft.Pos.X, 0.01f);
        Assert.Equal(size, bottomRight.Pos.Y - topLeft.Pos.Y, 0.01f);
    }

    [Fact]
    public void TheIconCellPicksItsUvs()
    {
        Controls.GroupButton[] buttons = [new("A"), new("High", Light, "Demo", 3)];
        var (uv0, uv1) = Atlas.UVPair(3);

        var verts = Verts(() => Controls.ButtonGroup("g", buttons, sheets: Sheets(Texture)));

        Assert.Contains(verts, v => v.Uv == uv0 && v.Col == Black);
        Assert.Contains(verts, v => v.Uv == uv1 && v.Col == Black);
    }

    [Fact]
    public void AnIconWidensItsButtonByTheIconAndAGap()
    {
        Controls.GroupButton[] buttons = [new("A"), new("High", Sheet: "Demo")];
        var withIcon = 0f;
        var without = 0f;
        var expectedExtra = 0f;
        UiFrame.Run("test", () =>
        {
            Controls.ButtonGroup("with", buttons, sheets: Sheets(Texture), iconSize: 20f);
            withIcon = ImGui.GetItemRectMax().X - ImGui.GetItemRectMin().X;
            Controls.ButtonGroup("without", buttons, sheets: Sheets(null), iconSize: 20f);
            without = ImGui.GetItemRectMax().X - ImGui.GetItemRectMin().X;
            expectedExtra = 20f + ImGui.GetStyle().ItemInnerSpacing.X;
        });

        Assert.Equal(expectedExtra, withIcon - without, 0.01f);
    }

    [Fact]
    public void ASheetWithNoTextureDrawsNoIcon()
    {
        Controls.GroupButton[] buttons = [new("A"), new("High", Light, "Demo", 1)];
        var (uv0, _) = Atlas.UVPair(1);

        var verts = Verts(() => Controls.ButtonGroup("g", buttons, sheets: Sheets(null)));

        Assert.DoesNotContain(verts, v => v.Uv == uv0 && v.Col == Black);
    }

    [Fact]
    public void ATypedGroupWritesTheClickedValue()
    {
        var speed = Speed.Slow;
        var changed = false;

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("s", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast)), 3)[2], () =>
            changed |= Controls.ButtonGroup("s", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast)));

        Assert.True(changed);
        Assert.Equal(Speed.Fast, speed);
    }

    [Fact]
    public void ClickingTheCurrentTypedOptionChangesNothing()
    {
        var speed = Speed.Medium;
        var changed = false;

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("s", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast)), 3)[1], () =>
            changed |= Controls.ButtonGroup("s", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast)));

        Assert.False(changed);
        Assert.Equal(Speed.Medium, speed);
    }

    [Fact]
    public void ATypedGroupHighlightsTheCurrentValueWithTheHeaderColour()
    {
        var speed = Speed.Fast;
        uint header = 0;

        var verts = Verts(() =>
        {
            header = Packed(EColor.FromVector4(ImGui.GetStyle().Colors[(int)ImGuiCol.Header]));
            Controls.ButtonGroup("s", ref speed, ("Slow", Speed.Slow), ("Medium", Speed.Medium), ("Fast", Speed.Fast));
        });

        Assert.Contains(verts, v => v.Col == header);
    }

    [Fact]
    public void ATypedGroupWorksOverStrings()
    {
        var category = "all";

        UiFrame.Click("test", RowCentres(() => Controls.ButtonGroup("c", ref category, ("all", "all"), ("mods", "mods")), 2)[1], () => Controls.ButtonGroup("c", ref category, ("all", "all"), ("mods", "mods")));

        Assert.Equal("mods", category);
    }

    [Fact]
    public void AMenuButtonWithNoItemsDrawsNothing()
    {
        UiFrame.Run("test", () =>
        {
            var before = ImGui.GetCursorPos();
            Assert.Equal(-1, Controls.MenuButton("Save", null));
            Assert.Equal(-1, Controls.MenuButton("Save", []));
            Assert.Equal(before, ImGui.GetCursorPos());
        });
    }

    [Fact]
    public void AMenuButtonSizesToLabelChevronAndPadding()
    {
        var width = 0f;
        var expected = 0f;
        UiFrame.Run("test", () =>
        {
            Controls.MenuButton("Save", ["a"]);
            width = ImGui.GetItemRectMax().X - ImGui.GetItemRectMin().X;
            var style = ImGui.GetStyle();
            expected = ImGui.CalcTextSize("Save").X + ImGui.GetFrameHeight() * 0.4f + style.ItemInnerSpacing.X + style.FramePadding.X * 2;
        });

        Assert.Equal(expected, width, 0.01f);
    }

    [Fact]
    public void AMenuButtonHonoursAGivenWidth()
    {
        var (min, max) = LastItem(() => Controls.MenuButton("Save", ["a"], width: 150f));

        Assert.Equal(150f, max.X - min.X, 0.01f);
    }

    [Fact]
    public void ClickingAMenuButtonOpensItsMenu()
    {
        var open = false;
        void Body()
        {
            Controls.MenuButton("Save", ["a", "b"]);
            open = PopupOpen("Save");
        }
        var at = CentreOf(Body);
        Assert.False(open);

        UiFrame.Click("test", at, Body);
        Assert.True(open);
    }

    [Fact]
    public void AMenuPickReturnsTheArrayIndexPastASeparator()
    {
        string?[] items = ["Save as...", null, "Revert"];

        var hit = PickMenu(items, runs => runs[1].Centre);

        Assert.Equal(2, hit);
    }

    [Fact]
    public void AMenuPickReturnsTheFirstIndex()
    {
        string?[] items = ["Save as...", null, "Revert"];

        var hit = PickMenu(items, runs => runs[0].Centre);

        Assert.Equal(0, hit);
    }

    [Fact]
    public void ASeparatorRowCannotBePicked()
    {
        string?[] items = ["Save as...", null, "Revert"];

        var hit = PickMenu(items, runs => new Vector2(runs[0].Centre.X, (runs[0].Max.Y + runs[1].Min.Y) / 2));

        Assert.Equal(-1, hit);
    }

    static int PickMenu(string?[] items, Func<List<UiFrame.ItemRun>, Vector2> target)
    {
        var hit = -1;
        void Body()
        {
            var picked = Controls.MenuButton("Save", items);
            if (picked >= 0) hit = picked;
        }
        var (min, max) = LastItem(Body);
        UiFrame.Click("test", (min + max) / 2, Body);
        UiFrame.Click("test", target(MenuItems(Body)), Body);
        return hit;
    }

    static (UiFrame.ItemRun Primary, UiFrame.ItemRun Arrow) SplitRuns(Action body)
    {
        var y = 0f;
        UiFrame.Run("test", () =>
        {
            y = ImGui.GetCursorScreenPos().Y + ImGui.GetFrameHeight() / 2;
            body();
        });
        var runs = UiFrame.ItemRuns("test", body, new Vector2(0, y), new Vector2(400f, y));
        Assert.Equal(2, runs.Count);
        return (runs[0], runs[1]);
    }

    sealed class SplitRig(string?[]? menu)
    {
        public int Primary;
        public int Picked = -1;
        public bool Open;

        public void Body()
        {
            if (Controls.SplitButton("Run", menu, out var picked)) Primary++;
            if (picked >= 0) Picked = picked;
            Open = PopupOpen("Run");
        }
    }

    [Fact]
    public void ClickingThePrimaryHalfReportsTrueAndLeavesTheMenuShut()
    {
        var rig = new SplitRig(["Run all", "Run one"]);
        var (primary, _) = SplitRuns(rig.Body);

        UiFrame.Click("test", primary.Centre, rig.Body);

        Assert.Equal(1, rig.Primary);
        Assert.Equal(-1, rig.Picked);
        Assert.False(rig.Open);
    }

    [Fact]
    public void ClickingTheChevronHalfOpensTheMenuWithoutFiringThePrimary()
    {
        var rig = new SplitRig(["Run all", "Run one"]);
        var (_, arrow) = SplitRuns(rig.Body);

        UiFrame.Click("test", arrow.Centre, rig.Body);

        Assert.Equal(0, rig.Primary);
        Assert.True(rig.Open);
    }

    [Fact]
    public void APickedSplitMenuEntryComesBackThroughPicked()
    {
        var rig = new SplitRig(["Run all", "Run one"]);
        var (_, arrow) = SplitRuns(rig.Body);

        UiFrame.Click("test", arrow.Centre, rig.Body);
        UiFrame.Click("test", MenuItems(rig.Body)[1].Centre, rig.Body);

        Assert.Equal(1, rig.Picked);
        Assert.Equal(0, rig.Primary);
    }

    [Fact]
    public void ASplitWithNoMenuHasADeadChevron()
    {
        var rig = new SplitRig(null);
        var (_, arrow) = SplitRuns(rig.Body);

        UiFrame.Click("test", arrow.Centre, rig.Body);

        Assert.False(rig.Open);
        Assert.Equal(0, rig.Primary);
    }

    [Fact]
    public void AGivenSplitWidthCoversBothHalves()
    {
        var origin = 0f;
        var (min, max) = LastItem(() =>
        {
            origin = ImGui.GetCursorScreenPos().X;
            Controls.SplitButton("Run", ["a"], out _, width: 200f);
        });

        Assert.Equal(200f, max.X - origin, 0.01f);
        Assert.True(min.X > origin);
    }

    [Fact]
    public void TheSplitHalvesOverlapByTheFrameRounding()
    {
        const float rounding = 4f;
        var origin = 0f;
        var primary = 0f;
        var (min, _) = LastItem(() =>
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, rounding);
            origin = ImGui.GetCursorScreenPos().X;
            primary = ImGui.CalcTextSize("Run").X + ImGui.GetStyle().FramePadding.X * 2;
            Controls.SplitButton("Run", ["a"], out _);
            ImGui.PopStyleVar();
        });

        Assert.Equal(origin + primary - rounding, min.X, 0.01f);
    }

    static Vector2 CompoundSize(string top, string? secondary, Controls.IconSheet[]? sheets = null, float iconSize = 24f, float width = 0f)
    {
        var (min, max) = LastItem(() => Controls.CompoundButton(new Controls.GroupButton(top, Sheet: sheets is null ? null : "Demo"), secondary, sheets, iconSize, width));
        return max - min;
    }

    [Fact]
    public void ACompoundButtonReturnsTrueWhenClicked()
    {
        var clicked = 0;
        void Body()
        {
            if (Controls.CompoundButton(new Controls.GroupButton("Import"), "pull in")) clicked++;
        }

        UiFrame.Click("test", CentreOf(Body), Body);

        Assert.Equal(1, clicked);
    }

    [Fact]
    public void ACompoundButtonStacksASmallerSecondLineUnderTheLabel()
    {
        var size = Vector2.Zero;
        var expected = 0f;
        UiFrame.Run("test", () =>
        {
            var style = ImGui.GetStyle();
            expected = ImGui.CalcTextSize("Import").Y + style.ItemInnerSpacing.Y + ImGui.GetFontSize() * 0.85f + style.FramePadding.Y * 2;
        });
        size = CompoundSize("Import", "pull in");

        Assert.Equal(expected, size.Y, 0.01f);
    }

    [Fact]
    public void ACompoundButtonWithNoSecondLineIsOneLineHigh()
    {
        var expected = 0f;
        UiFrame.Run("test", () => expected = ImGui.CalcTextSize("Import").Y + ImGui.GetStyle().FramePadding.Y * 2);

        Assert.Equal(expected, CompoundSize("Import", null).Y, 0.01f);
    }

    [Fact]
    public void ATallIconSetsTheCompoundHeight()
    {
        var expected = 0f;
        UiFrame.Run("test", () => expected = 60f + ImGui.GetStyle().FramePadding.Y * 2);

        Assert.Equal(expected, CompoundSize("Import", "pull in", Sheets(Texture), iconSize: 60f).Y, 0.01f);
    }

    [Fact]
    public void ACompoundButtonIsAsWideAsItsLongerLineAndIcon()
    {
        var expected = 0f;
        UiFrame.Run("test", () =>
        {
            var style = ImGui.GetStyle();
            var line = Math.Max(ImGui.CalcTextSize("Go").X, ImGui.CalcTextSize("a rather long explanation").X * 0.85f);
            expected = line + 24f + style.ItemInnerSpacing.X * 2 + style.FramePadding.X * 2;
        });

        Assert.Equal(expected, CompoundSize("Go", "a rather long explanation", Sheets(Texture)).X, 0.01f);
    }

    [Fact]
    public void ACompoundButtonHonoursAGivenWidth() => Assert.Equal(180f, CompoundSize("Go", "x", width: 180f).X, 0.01f);

    [Fact]
    public void ACompoundButtonFillsWithItsColourAndDimsTheSecondLine()
    {
        var verts = Verts(() => Controls.CompoundButton(new Controls.GroupButton("Import", Light), "pull in"));

        Assert.Contains(verts, v => v.Col == Packed(Light));
        Assert.Contains(verts, v => v.Col == Black);
        Assert.Contains(verts, v => v.Col == Packed(EColor.Fade(Color.Black, 0.7f)));
    }

    [Fact]
    public void HoveringAColouredCompoundButtonBrightensIt()
    {
        void Body() => Controls.CompoundButton(new Controls.GroupButton("Import", Dark), "pull in");

        var verts = Verts(Body, CentreOf(Body));

        Assert.Contains(verts, v => v.Col == Packed(EColor.Scale(Dark, 1.2f)));
    }

    [Fact]
    public void HoldingAColouredCompoundButtonDarkensIt()
    {
        void Body() => Controls.CompoundButton(new Controls.GroupButton("Import", Dark), "pull in");

        var verts = Verts(Body, CentreOf(Body), held: true);

        Assert.Contains(verts, v => v.Col == Packed(EColor.Scale(Dark, 0.8f)));
    }

    [Fact]
    public void ACompoundIconDrawsItsCellInsideTheLeftPaddingAtTheTextColour()
    {
        var (uv0, uv1) = Atlas.UVPair(2);
        Vector2 min = default;
        var pad = 0f;

        var verts = Verts(() =>
        {
            Controls.CompoundButton(new Controls.GroupButton("Import", Light, "Demo", 2), "pull in", Sheets(Texture), 24f);
            min = ImGui.GetItemRectMin();
            pad = ImGui.GetStyle().FramePadding.X;
        });

        var topLeft = verts.Single(v => v.Uv == uv0 && v.Col == Black);
        var bottomRight = verts.Single(v => v.Uv == uv1 && v.Col == Black);
        Assert.Equal(min.X + pad, topLeft.Pos.X, 0.01f);
        Assert.Equal(24f, bottomRight.Pos.X - topLeft.Pos.X, 0.01f);
    }

    static readonly string[] Tools = ["Reload", "Import", "Export", "Validate", "Reset", "Diagnostics"];

    static (int Fit, Vector2 Origin, float[] Widths) Overflow(float width)
    {
        (int, Vector2, float[]) result = default;
        UiFrame.Run("test", () =>
        {
            var style = ImGui.GetStyle();
            var widths = Tools.Select(t => ImGui.CalcTextSize(t).X + style.FramePadding.X * 2).ToArray();
            var more = ImGui.CalcTextSize("...").X + style.FramePadding.X * 2 + ImGui.GetFrameHeight() * 0.4f + style.ItemInnerSpacing.X;
            result = (Controls.FitItems(widths, width, style.ItemSpacing.X, more), ImGui.GetCursorScreenPos(), widths);
        });
        return result;
    }

    static Vector2 ToolCentre(Vector2 origin, float[] widths, int index)
    {
        var gap = 0f;
        UiFrame.Run("test", () => gap = ImGui.GetStyle().ItemSpacing.X);
        var h = 0f;
        UiFrame.Run("test", () => h = ImGui.GetFrameHeight());
        var x = origin.X + widths.Take(index).Sum() + gap * index;
        return new(x + widths[index] / 2, origin.Y + h / 2);
    }

    [Fact]
    public void ANarrowRowStillHasRoomForSomeButtonsAndAMenu()
    {
        var (fit, _, _) = Overflow(200f);

        Assert.InRange(fit, 1, Tools.Length - 1);
    }

    [Fact]
    public void ARowButtonReturnsItsIndex()
    {
        var (_, origin, widths) = Overflow(200f);
        var hit = -1;

        UiFrame.Click("test", ToolCentre(origin, widths, 1), () =>
        {
            var clicked = Controls.OverflowRow("tools", Tools, 200f);
            if (clicked >= 0) hit = clicked;
        });

        Assert.Equal(1, hit);
    }

    [Fact]
    public void AnOverflowedItemKeepsItsOriginalIndex()
    {
        var (fit, _, _) = Overflow(200f);
        var hit = -1;
        void Body()
        {
            var picked = Controls.OverflowRow("tools", Tools, 200f);
            if (picked >= 0) hit = picked;
        }
        var (min, max) = LastItem(Body);

        UiFrame.Click("test", (min + max) / 2, Body);
        UiFrame.Click("test", MenuItems(Body)[1].Centre, Body);

        Assert.Equal(fit + 1, hit);
    }

    [Fact]
    public void AZeroWidthMeasuresTheSpaceAvailable()
    {
        var (_, origin, widths) = Overflow(10000f);
        var hit = -1;

        UiFrame.Click("test", ToolCentre(origin, widths, Tools.Length - 1), () =>
        {
            var clicked = Controls.OverflowRow("tools", Tools);
            if (clicked >= 0) hit = clicked;
        });

        Assert.Equal(Tools.Length - 1, hit);
    }

    [Fact]
    public void ANullOrEmptyRowDrawsNothing()
    {
        UiFrame.Run("test", () =>
        {
            var before = ImGui.GetCursorPos();
            Assert.Equal(-1, Controls.OverflowRow("tools", null));
            Assert.Equal(-1, Controls.OverflowRow("tools", []));
            Assert.Equal(before, ImGui.GetCursorPos());
        });
    }

    [Fact]
    public void ThePrimaryButtonDrawsWithTheActiveColourAtRest()
    {
        uint gold = 0, plain = 0;
        List<uint> colours = [];
        UiFrame.Run("primary", () =>
        {
            gold = ImGui.GetColorU32(ImGuiCol.ButtonActive);
            plain = ImGui.GetColorU32(ImGuiCol.Button);
            colours = UiFrame.Drawn(() => Controls.PrimaryButton("Save")).Select(v => v.Col).ToList();
        });
        Assert.NotEqual(gold, plain);
        Assert.Contains(gold, colours);
        Assert.DoesNotContain(plain, colours);
    }
}
