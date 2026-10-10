using System.Numerics;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public class SideNavTests
{
    const string Win = "sidenav";
    const float RowPad = 8f;
    const float BadgePad = 2f;

    static readonly Func<bool> Body = () => false;
    static readonly (string Label, Func<bool>? Body)[] Tabs = [("Group", null), ("A", Body), ("B", Body)];

    static readonly Vector4 HeaderColour = new(0.9f, 0.1f, 0.1f, 1f);
    static readonly Vector4 HoveredColour = new(0.1f, 0.9f, 0.1f, 1f);
    static readonly Vector4 ButtonColour = new(0.1f, 0.1f, 0.9f, 1f);
    static readonly Vector4 TextColour = new(0.9f, 0.9f, 0.1f, 1f);
    static readonly Vector4 DisabledColour = new(0.1f, 0.9f, 0.9f, 1f);
    static readonly Vector4 BorderColour = new(0.9f, 0.1f, 0.9f, 1f);

    static readonly Sprite ActiveSprite = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.125f, 0.25f), new Vector2(0.375f, 0.5f));
    static readonly Sprite IdleSprite = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.625f, 0.75f), new Vector2(0.875f, 1f));
    static readonly Sprite PlainSprite = new(new ImTextureRef { TexID = new ImTextureID(1ul) }, new Vector2(0.0625f, 0.5625f), new Vector2(0.1875f, 0.6875f));

    static int _next;

    static string NextId(string prefix) => prefix + ++_next;

    static uint U32(Vector4 colour) => ImGui.ColorConvertFloat4ToU32(colour);

    static void Scoped(Action test)
    {
        var io = UiFrame.Io();
        Measure();
        var style = ImGui.GetStyle();
        ImGuiCol[] touched = [ImGuiCol.Header, ImGuiCol.HeaderHovered, ImGuiCol.Button, ImGuiCol.Text, ImGuiCol.TextDisabled, ImGuiCol.Border];
        var saved = touched.Select(c => style.Colors[(int)c]).ToArray();
        try
        {
            test();
        }
        finally
        {
            for (var i = 0; i < touched.Length; i++) style.Colors[(int)touched[i]] = saved[i];
            io.AddMouseButtonEvent(0, false);
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
            UiFrame.Run(Win, () => { });
            UiFrame.Run(Win, () => { });
        }
    }

    static void Paint()
    {
        var style = ImGui.GetStyle();
        style.Colors[(int)ImGuiCol.Header] = HeaderColour;
        style.Colors[(int)ImGuiCol.HeaderHovered] = HoveredColour;
        style.Colors[(int)ImGuiCol.Button] = ButtonColour;
        style.Colors[(int)ImGuiCol.Text] = TextColour;
        style.Colors[(int)ImGuiCol.TextDisabled] = DisabledColour;
        style.Colors[(int)ImGuiCol.Border] = BorderColour;
    }

    sealed record Metrics(float FrameHeight, Vector2 FramePadding, Vector2 ItemSpacing, float LineHeight);

    static Metrics? _metrics;

    static Metrics Measure()
    {
        if (_metrics is not null) return _metrics;
        UiFrame.Run(Win, () =>
        {
            var style = ImGui.GetStyle();
            _metrics = new Metrics(ImGui.GetFrameHeight(), style.FramePadding, style.ItemSpacing, ImGui.GetTextLineHeight());
        });
        return _metrics!;
    }

    static Vector2 TextSize(string text)
    {
        var size = Vector2.Zero;
        UiFrame.Run(Win, () => size = ImGui.CalcTextSize(text));
        return size;
    }

    static string Open(string? scope, string? wrapper)
    {
        if (scope is not null) ImGui.PushID(scope);
        var parent = Win;
        if (wrapper is not null)
        {
            parent += $"/{wrapper}_{ImGui.GetID(wrapper):X8}";
            ImGui.BeginChild(wrapper, new Vector2(0, 200));
        }
        return parent;
    }

    static void Close(string? scope, string? wrapper)
    {
        if (wrapper is not null) ImGui.EndChild();
        if (scope is not null) ImGui.PopID();
    }

    static ImGuiWindowPtr Find(string name)
    {
        var window = ImGuiP.FindWindowByName(name);
        return window.IsNull || !window.Active ? default : window;
    }

    static (Vector2 Min, Vector2 Max) RowRect(ImGuiWindowPtr rail, int row, float iconSize)
    {
        var content = rail.ContentRegionRect;
        var height = Math.Max(Measure().FrameHeight, iconSize + RowPad);
        var top = content.Min.Y + row * height;
        return (new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + height));
    }

    static Vector2 RowCentre(ImGuiWindowPtr rail, int row, float iconSize)
    {
        var (min, max) = RowRect(rail, row, iconSize);
        return (min + max) / 2;
    }

    static Vector2 FootCentre(ImGuiWindowPtr rail)
    {
        var content = rail.ContentRegionRect;
        return new Vector2((content.Min.X + content.Max.X) / 2, content.Max.Y - Measure().FrameHeight / 2);
    }

    static (Vector2 Min, Vector2 Max) Bounds(IEnumerable<ImDrawVert> verts)
    {
        var all = verts.ToList();
        return (new Vector2(all.Min(v => v.Pos.X), all.Min(v => v.Pos.Y)), new Vector2(all.Max(v => v.Pos.X), all.Max(v => v.Pos.Y)));
    }

    static List<ImDrawVert> WithColour(ImGuiWindowPtr window, Vector4 colour) => UiFrame.Verts(window.DrawList).Where(v => v.Col == U32(colour)).ToList();

    static List<ImDrawVert> WithUv(ImGuiWindowPtr window, Vector2 uv) => UiFrame.Verts(window.DrawList).Where(v => v.Uv == uv).ToList();

    static List<Vector2> TextUvs(ImGuiWindowPtr window) => WithColour(window, TextColour).Select(v => v.Uv).ToList();

    static List<Vector4> DrawnClips(ImGuiWindowPtr window)
    {
        var commands = window.DrawList.CmdBuffer;
        List<Vector4> clips = [];
        for (var i = 0; i < commands.Size; i++)
        {
            if (commands[i].ElemCount > 0) clips.Add(commands[i].ClipRect);
        }
        return clips;
    }

    static void AssertText(ImGuiWindowPtr rail, string expected)
    {
        var actual = TextUvs(rail);
        Assert.Equal(UiFrame.GlyphUvs(Win, expected), actual);
    }

    static (Vector2 Min, Vector2 Max) DrawnRectBounds(Vector2 min, Vector2 max)
    {
        (Vector2 Min, Vector2 Max) bounds = default;
        UiFrame.Run(Win, () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            list.AddRect(min, max, 0xFFFFFFFFu);
            bounds = Bounds(UiFrame.Verts(list).Skip(from));
        });
        return bounds;
    }

    static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 0.01f);
        Assert.Equal(expected.Y, actual.Y, 0.01f);
    }

    sealed class RailRig
    {
        public string Id = NextId("rail");
        public NavItem[]? Items;
        public NavState? State = new();
        public float CollapsedWidth = 40f;
        public float ExpandedWidth = 152f;
        public float IconSize = 20f;
        public string? Scope;
        public string? Wrapper;
        public bool Changed;
        public bool Returned;
        public List<string> Bodies = [];
        public uint ProbeBefore;
        public uint ProbeAfter;
        string _railName = "";
        string _bodyName = "";

        public void Draw()
        {
            var parent = Open(Scope, Wrapper);
            ProbeBefore = ImGui.GetID("probe");
            Returned = SideNav.Rail(Id, Items, State, CollapsedWidth, ExpandedWidth, IconSize);
            Changed |= Returned;
            ProbeAfter = ImGui.GetID("probe");
            ImGui.PushID(Id);
            _railName = $"{parent}/##rail_{ImGui.GetID("##rail"):X8}";
            _bodyName = $"{parent}/##body_{ImGui.GetID("##body"):X8}";
            ImGui.PopID();
            Close(Scope, Wrapper);
        }

        public void Frame()
        {
            Bodies.Clear();
            UiFrame.Run(Win, Draw);
        }

        public ImGuiWindowPtr Rail => Find(_railName);

        public ImGuiWindowPtr BodyPane => Find(_bodyName);

        public NavItem Item(string key, Action<NavItem>? configure = null, bool bodyResult = false)
        {
            var item = new NavItem { Key = key, Label = key, Body = () => { Bodies.Add(key); return bodyResult; } };
            configure?.Invoke(item);
            return item;
        }

        public void ClickRow(int row)
        {
            Frame();
            UiFrame.Click(Win, RowCentre(Rail, row, IconSize), Draw);
        }
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(99, 1)]
    public void ARememberedIndexOnAHeaderOrOutOfRangeFallsToTheFirstRealTab(int want, int expected) =>
        Assert.Equal(expected, SideNav.PickTab(Tabs, want));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    public void ARememberedRealTabIsKept(int want, int expected) =>
        Assert.Equal(expected, SideNav.PickTab([("A", Body), ("B", Body)], want));

    [Fact]
    public void AllHeadersDrawsNothing() => Assert.Equal(-1, SideNav.PickTab([("Only a header", null)], 0));

    [Fact]
    public void NullTabsNeverThrow() => Assert.Equal(-1, SideNav.PickTab(null, 0));

    [Fact]
    public void EmptyTabsPickNothing() => Assert.Equal(-1, SideNav.PickTab([], 0));

    [Fact]
    public void NavDrawsOneBalancedFrame()
    {
        var sprite = new Sprite(new ImTextureRef { TexID = new ImTextureID(1ul) }, Vector2.Zero, Vector2.One);
        NavItem[] items =
        [
            new() { Key = "general", Label = "General", Icon = sprite, Body = () => false },
            new() { Key = "icons", Label = "Map Icons 100%", Badge = () => "3", Body = () => false },
            new() { Key = "hidden", Label = "Hidden", Visible = () => false },
        ];
        var state = new NavState { ShowLabels = true };
        var changed = false;

        UiFrame.Run("test", () =>
        {
            ImGui.BeginChild("rail", new Vector2(0, 200));
            changed |= SideNav.Rail("rail", items, state);
            ImGui.EndChild();
            state.ShowLabels = false;
            ImGui.BeginChild("collapsed", new Vector2(0, 200));
            changed |= SideNav.Rail("collapsed", items, state);
            ImGui.EndChild();
            ImGui.BeginChild("side", new Vector2(0, 200));
            changed |= SideNav.SideTabs("side", 120f, Tabs);
            ImGui.EndChild();
        });

        Assert.False(changed);
        Assert.Equal("general", state.Active);
    }

    [Fact]
    public void ClickingARowMakesItActiveAndDrawsOnlyItsBody() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b"), rig.Item("c")];
        rig.ClickRow(1);
        Assert.Equal("b", rig.State!.Active);
        rig.Frame();
        Assert.Equal(["b"], rig.Bodies);
        Assert.False(rig.Changed);
    });

    [Fact]
    public void EachRowHasItsOwnItemId() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b")];
        rig.Frame();
        var rail = rig.Rail;
        UiFrame.Press(Win, RowCentre(rail, 0, rig.IconSize), rig.Draw);
        var first = ImGui.GetCurrentContext().ActiveId;
        UiFrame.Release(Win, rig.Draw);
        UiFrame.Press(Win, RowCentre(rail, 1, rig.IconSize), rig.Draw);
        var second = ImGui.GetCurrentContext().ActiveId;
        UiFrame.Release(Win, rig.Draw);
        Assert.NotEqual(0u, first);
        Assert.NotEqual(0u, second);
        Assert.NotEqual(first, second);
    });

    [Fact]
    public void TheRailReturnsTheActiveBodysChangedFlag() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a", bodyResult: true), rig.Item("b", bodyResult: false)];
        rig.Frame();
        Assert.True(rig.Returned);
        rig.State!.Active = "b";
        rig.Frame();
        Assert.False(rig.Returned);
        Assert.Equal(["b"], rig.Bodies);
    });

    [Fact]
    public void AHiddenItemTakesNoRowAndCannotBeClicked() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("gone", i => i.Visible = () => false), rig.Item("c")];
        rig.ClickRow(1);
        Assert.Equal("c", rig.State!.Active);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("gone")]
    public void AMissingUnknownOrHiddenActiveKeyFallsToTheFirstVisibleItem(string? active) => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("gone", i => i.Visible = () => false), rig.Item("a"), rig.Item("b")];
        rig.State!.Active = active;
        rig.Frame();
        Assert.Equal("a", rig.State.Active);
        Assert.Equal(["a"], rig.Bodies);
    });

    [Fact]
    public void AnActiveItemThatIsStillVisibleIsKept() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b"), rig.Item("c")];
        rig.State!.Active = "c";
        rig.Frame();
        Assert.Equal("c", rig.State.Active);
        Assert.Equal(["c"], rig.Bodies);
    });

    [Fact]
    public void WhenNothingIsVisibleNothingIsDrawn() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Visible = () => false), rig.Item("b", i => i.Visible = () => false)];
        rig.State!.Active = "a";
        rig.Frame();
        Assert.False(rig.Returned);
        Assert.Empty(rig.Bodies);
        Assert.Equal("a", rig.State.Active);
        Assert.True(rig.Rail.IsNull);
        Assert.True(rig.BodyPane.IsNull);
    });

    [Fact]
    public void NullOrEmptyItemsOrNullStateDrawNothing() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = null;
        rig.Frame();
        Assert.True(rig.Rail.IsNull);
        rig.Items = [];
        rig.Frame();
        Assert.True(rig.Rail.IsNull);
        rig.Items = [rig.Item("a")];
        rig.State = null;
        rig.Frame();
        Assert.True(rig.Rail.IsNull);
        Assert.Empty(rig.Bodies);
        Assert.False(rig.Changed);
    });

    [Theory]
    [InlineData(false, 40f, 152f)]
    [InlineData(true, 40f, 152f)]
    [InlineData(false, 64f, 200f)]
    [InlineData(true, 64f, 200f)]
    public void TheRailIsTheCollapsedOrExpandedWidthAndTheBodyStartsAfterIt(bool labels, float collapsed, float expanded) => Scoped(() =>
    {
        var rig = new RailRig { CollapsedWidth = collapsed, ExpandedWidth = expanded };
        rig.Items = [rig.Item("a")];
        rig.State!.ShowLabels = labels;
        rig.Frame();
        var rail = rig.Rail;
        var width = labels ? expanded : collapsed;
        Assert.Equal(width, rail.Size.X, 0.01f);
        Assert.Equal(rail.Pos.X + width + Measure().ItemSpacing.X, rig.BodyPane.Pos.X, 0.01f);
        Assert.Equal(rail.Pos.Y, rig.BodyPane.Pos.Y, 0.01f);
    });

    [Fact]
    public void TheChevronAtTheFootTogglesLabelsBothWaysWithoutTouchingTheSelection() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b")];
        rig.State!.Active = "b";
        rig.Frame();
        UiFrame.Click(Win, FootCentre(rig.Rail), rig.Draw);
        Assert.True(rig.State.ShowLabels);
        Assert.Equal("b", rig.State.Active);
        UiFrame.Click(Win, FootCentre(rig.Rail), rig.Draw);
        Assert.False(rig.State.ShowLabels);
        Assert.Equal("b", rig.State.Active);
    });

    [Theory]
    [InlineData(false, ">>")]
    [InlineData(true, "<<")]
    public void TheChevronShowsWhichWayItWillToggle(bool labels, string glyph) => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Icon = PlainSprite)];
        rig.State!.ShowLabels = labels;
        rig.Frame();
        var rail = rig.Rail;
        var footTop = rail.ContentRegionRect.Max.Y - Measure().FrameHeight;
        var actual = WithColour(rail, DisabledColour).Where(v => v.Pos.Y >= footTop - 1f).Select(v => v.Uv).ToList();
        Assert.Equal(UiFrame.GlyphUvs(Win, glyph), actual);
    });

    [Fact]
    public void TheChevronHasATopRuleAndFillsWithHeaderHoveredWhenHovered() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Icon = PlainSprite)];
        rig.Frame();
        var rail = rig.Rail;
        var content = rail.ContentRegionRect;
        var footTop = content.Max.Y - Measure().FrameHeight;
        var rule = WithColour(rail, BorderColour).Where(v => Math.Abs(v.Pos.Y - footTop) <= 3f).ToList();
        Assert.NotEmpty(rule);
        Assert.Equal(content.Min.X, rule.Min(v => v.Pos.X), 1f);
        Assert.Equal(content.Max.X, rule.Max(v => v.Pos.X), 1f);
        Assert.Empty(WithColour(rail, HoveredColour));

        UiFrame.Hover(Win, FootCentre(rail), rig.Draw);
        var fill = Bounds(WithColour(rig.Rail, HoveredColour));
        AssertNear(new Vector2(content.Min.X, footTop), fill.Min);
        AssertNear(new Vector2(content.Max.X, content.Max.Y), fill.Max);
    });

    [Fact]
    public void ClickingInTheSlackBetweenTheItemsAndTheChevronChangesNothing() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b")];
        rig.Frame();
        var rail = rig.Rail;
        var middle = new Vector2((rail.ContentRegionRect.Min.X + rail.ContentRegionRect.Max.X) / 2, (RowRect(rail, 2, rig.IconSize).Max.Y + FootCentre(rail).Y) / 2);
        UiFrame.Click(Win, middle, rig.Draw);
        Assert.False(rig.State!.ShowLabels);
        Assert.Equal("a", rig.State.Active);
    });

    [Theory]
    [InlineData(4f)]
    [InlineData(40f)]
    public void ARowIsAtLeastTheFrameHeightAndOtherwiseTheIconSizePlusPadding(float iconSize) => Scoped(() =>
    {
        Paint();
        var rig = new RailRig { IconSize = iconSize };
        rig.Items = [rig.Item("a"), rig.Item("b")];
        rig.Frame();
        var fill = Bounds(WithColour(rig.Rail, HeaderColour));
        Assert.Equal(Math.Max(Measure().FrameHeight, iconSize + RowPad), fill.Max.Y - fill.Min.Y, 0.01f);
    });

    [Fact]
    public void TheSelectedRowIsFilledWithHeaderAndAHoveredRowWithHeaderHovered() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b"), rig.Item("c")];
        rig.State!.Active = "a";
        rig.Frame();
        var rail = rig.Rail;
        var (min0, max0) = RowRect(rail, 0, rig.IconSize);
        var selected = WithColour(rail, HeaderColour);
        Assert.NotEmpty(selected);
        AssertNear(min0, Bounds(selected).Min);
        AssertNear(max0, Bounds(selected).Max);
        Assert.Empty(WithColour(rail, HoveredColour));

        var (min2, max2) = RowRect(rail, 2, rig.IconSize);
        UiFrame.Hover(Win, RowCentre(rail, 2, rig.IconSize), rig.Draw);
        rail = rig.Rail;
        var hovered = WithColour(rail, HoveredColour);
        AssertNear(min2, Bounds(hovered).Min);
        AssertNear(max2, Bounds(hovered).Max);
        AssertNear(min0, Bounds(WithColour(rail, HeaderColour)).Min);
    });

    [Fact]
    public void HoverWinsOverSelectedOnTheSameRow() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b")];
        rig.State!.Active = "a";
        rig.Frame();
        UiFrame.Hover(Win, RowCentre(rig.Rail, 0, rig.IconSize), rig.Draw);
        Assert.Empty(WithColour(rig.Rail, HeaderColour));
        Assert.NotEmpty(WithColour(rig.Rail, HoveredColour));
    });

    [Fact]
    public void ARowDrawsTheActiveSpriteWhenSelectedAndTheIdleOneOtherwiseCentredInTheCollapsedRail() => Scoped(() =>
    {
        var rig = new RailRig { IconSize = 24f };
        rig.Items = [rig.Item("a", i => (i.Icon, i.IconActive) = (IdleSprite, ActiveSprite)), rig.Item("b", i => i.Icon = PlainSprite)];
        rig.State!.Active = "a";
        rig.Frame();
        var rail = rig.Rail;
        Assert.Single(WithUv(rail, ActiveSprite.Uv0));
        Assert.Single(WithUv(rail, ActiveSprite.Uv1));
        Assert.Empty(WithUv(rail, IdleSprite.Uv0));
        Assert.Single(WithUv(rail, PlainSprite.Uv0));

        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var height = rowMax.Y - rowMin.Y;
        var iconMin = new Vector2(rowMin.X + (rowMax.X - rowMin.X - rig.IconSize) / 2, rowMin.Y + (height - rig.IconSize) / 2);
        AssertNear(iconMin, WithUv(rail, ActiveSprite.Uv0).Single().Pos);
        AssertNear(iconMin + new Vector2(rig.IconSize), WithUv(rail, ActiveSprite.Uv1).Single().Pos);
        Assert.Equal(0xFFFFFFFFu, WithUv(rail, ActiveSprite.Uv0).Single().Col);

        rig.State.Active = "b";
        rig.Frame();
        rail = rig.Rail;
        Assert.Single(WithUv(rail, IdleSprite.Uv0));
        Assert.Empty(WithUv(rail, ActiveSprite.Uv0));
    });

    [Fact]
    public void ASelectedItemWithoutAnActiveSpriteKeepsItsIcon() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Icon = IdleSprite)];
        rig.Frame();
        Assert.Single(WithUv(rig.Rail, IdleSprite.Uv0));
    });

    [Fact]
    public void WithLabelsOnTheIconSitsAtTheFramePaddingInsteadOfCentred() => Scoped(() =>
    {
        var rig = new RailRig { IconSize = 20f };
        rig.Items = [rig.Item("a", i => i.Icon = PlainSprite)];
        rig.State!.ShowLabels = true;
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var height = rowMax.Y - rowMin.Y;
        AssertNear(new Vector2(rowMin.X + Measure().FramePadding.X, rowMin.Y + (height - rig.IconSize) / 2), WithUv(rail, PlainSprite.Uv0).Single().Pos);
    });

    [Fact]
    public void LabelsAreDrawnOnlyWhenLabelsAreOn() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("General", i => i.Icon = PlainSprite)];
        rig.Frame();
        Assert.Empty(WithColour(rig.Rail, TextColour));
        rig.State!.ShowLabels = true;
        rig.Frame();
        AssertText(rig.Rail, "General");
    });

    [Fact]
    public void ALabelStartsOnePaddingRightOfTheIconAndIsCentredVertically() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("General", i => i.Icon = PlainSprite)];
        rig.State!.ShowLabels = true;
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var metrics = Measure();
        var origin = new Vector2(rowMin.X + metrics.FramePadding.X + rig.IconSize + metrics.FramePadding.X, rowMin.Y + (rowMax.Y - rowMin.Y - metrics.LineHeight) / 2);
        var drawn = Bounds(WithColour(rail, TextColour)).Min;
        var reference = default(Vector2);
        UiFrame.Run(Win, () =>
        {
            var list = ImGui.GetWindowDrawList();
            var from = list.VtxBuffer.Size;
            ImGui.SetCursorScreenPos(origin);
            ImGui.TextUnformatted("General");
            reference = Bounds(UiFrame.Verts(list).Skip(from)).Min;
        });
        AssertNear(reference, drawn);
    });

    [Fact]
    public void ALabelIsClippedToItsRow() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("General", i => i.Icon = PlainSprite)];
        rig.State!.ShowLabels = true;
        rig.Frame();
        var rail = rig.Rail;
        var (min, max) = RowRect(rail, 0, rig.IconSize);
        Assert.Contains(DrawnClips(rail), clip => Math.Abs(clip.X - min.X) < 0.01f && Math.Abs(clip.Y - min.Y) < 0.01f && Math.Abs(clip.Z - max.X) < 0.01f && Math.Abs(clip.W - max.Y) < 0.01f);
    });

    [Fact]
    public void InitialsAreClippedToTheirBox() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("General")];
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var height = rowMax.Y - rowMin.Y;
        var boxMin = new Vector2(rowMin.X + (rowMax.X - rowMin.X - rig.IconSize) / 2, rowMin.Y + (height - rig.IconSize) / 2);
        var boxMax = boxMin + new Vector2(rig.IconSize);
        Assert.Contains(DrawnClips(rail), clip => Math.Abs(clip.X - boxMin.X) < 0.01f && Math.Abs(clip.Y - boxMin.Y) < 0.01f && Math.Abs(clip.Z - boxMax.X) < 0.01f && Math.Abs(clip.W - boxMax.Y) < 0.01f);
    });

    [Theory]
    [InlineData("Map Icons Extra", "MI")]
    [InlineData("single", "S")]
    [InlineData("1st map", "SM")]
    [InlineData("a  b", "AB")]
    public void AnItemWithoutASpriteDrawsUpToTwoInitialsInAnOutlinedBox(string label, string initials) => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Label = label)];
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var outline = Bounds(WithColour(rail, DisabledColour).Where(v => v.Pos.Y <= rowMax.Y));
        var actual = TextUvs(rail);
        var height = rowMax.Y - rowMin.Y;
        var boxMin = new Vector2(rowMin.X + (rowMax.X - rowMin.X - rig.IconSize) / 2, rowMin.Y + (height - rig.IconSize) / 2);
        var expectedBox = DrawnRectBounds(boxMin, boxMin + new Vector2(rig.IconSize));
        AssertNear(expectedBox.Min, outline.Min);
        AssertNear(expectedBox.Max, outline.Max);
        Assert.Equal(UiFrame.GlyphUvs(Win, initials), actual);
    });

    [Theory]
    [InlineData("")]
    [InlineData("123 456")]
    public void AnItemWithNoLettersDrawsTheBoxWithoutText(string label) => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Label = label)];
        rig.Frame();
        var rail = rig.Rail;
        Assert.Empty(WithColour(rail, TextColour));
        Assert.Contains(WithColour(rail, DisabledColour), v => v.Pos.Y <= RowRect(rail, 0, rig.IconSize).Max.Y);
    });

    [Fact]
    public void AnItemWithASpriteDrawsNoInitialsBox() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Icon = PlainSprite)];
        rig.Frame();
        var rail = rig.Rail;
        var disabled = WithColour(rail, DisabledColour);
        Assert.NotEmpty(disabled);
        var footTop = rail.ContentRegionRect.Max.Y - Measure().FrameHeight;
        Assert.All(disabled, v => Assert.True(v.Pos.Y >= footTop - 1f));
    });

    [Fact]
    public void ACollapsedHoverShowsTheLabelWithPercentSignsIntactAsATooltip() => Scoped(() =>
    {
        var label = "100% done";
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Label = label)];
        rig.Frame();
        var at = RowCentre(rig.Rail, 0, rig.IconSize);
        UiFrame.Io().AddMousePosEvent(at.X, at.Y);
        rig.Frame();
        rig.Frame();
        rig.Frame();
        var tooltip = UiFrame.ActiveTooltipUvs();
        Assert.NotNull(tooltip);
        var expected = UiFrame.GlyphUvs(Win, label);
        Assert.True(tooltip.Count >= expected.Count && tooltip.Skip(tooltip.Count - expected.Count).SequenceEqual(expected));
    });

    [Fact]
    public void NoTooltipWhenLabelsAreOnOrNothingIsHovered() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a")];
        rig.State!.ShowLabels = true;
        rig.Frame();
        var at = RowCentre(rig.Rail, 0, rig.IconSize);
        UiFrame.Io().AddMousePosEvent(at.X, at.Y);
        rig.Frame();
        rig.Frame();
        rig.Frame();
        Assert.Null(UiFrame.ActiveTooltipUvs());

        rig.State.ShowLabels = false;
        UiFrame.Io().AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        rig.Frame();
        rig.Frame();
        rig.Frame();
        Assert.Null(UiFrame.ActiveTooltipUvs());
    });

    [Fact]
    public void AnEmptyLabelHasNoTooltip() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Label = "")];
        rig.Frame();
        var at = RowCentre(rig.Rail, 0, rig.IconSize);
        UiFrame.Io().AddMousePosEvent(at.X, at.Y);
        rig.Frame();
        rig.Frame();
        rig.Frame();
        Assert.Null(UiFrame.ActiveTooltipUvs());
    });

    [Fact]
    public void ACollapsedBadgeSitsOnTheIconsTopCornerAndDrawsItsText() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => (i.Icon, i.Badge) = (PlainSprite, () => "3"))];
        var textSize = TextSize("3");
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var box = Bounds(WithColour(rail, ButtonColour));
        var width = textSize.X + BadgePad * 2;
        var iconMaxX = rowMin.X + (rowMax.X - rowMin.X - rig.IconSize) / 2 + rig.IconSize;
        Assert.Equal(width, box.Max.X - box.Min.X, 0.01f);
        Assert.Equal(textSize.Y, box.Max.Y - box.Min.Y, 0.01f);
        Assert.Equal(rowMin.Y, box.Min.Y, 0.01f);
        Assert.Equal(Math.Min(iconMaxX - width / 2, rowMax.X - width), box.Min.X, 0.01f);
        AssertText(rail, "3");
    });

    [Fact]
    public void ABadgeBesideALabelIsRightAlignedAndVerticallyCentred() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => (i.Icon, i.Badge) = (PlainSprite, () => "3"))];
        rig.State!.ShowLabels = true;
        var textSize = TextSize("3");
        rig.Frame();
        var rail = rig.Rail;
        var (rowMin, rowMax) = RowRect(rail, 0, rig.IconSize);
        var box = Bounds(WithColour(rail, ButtonColour));
        var width = textSize.X + BadgePad * 2;
        Assert.Equal(rowMax.X - BadgePad * 2, box.Max.X, 0.01f);
        Assert.Equal(width, box.Max.X - box.Min.X, 0.01f);
        Assert.Equal(rowMin.Y + (rowMax.Y - rowMin.Y - textSize.Y) / 2, box.Min.Y, 0.01f);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ANullOrEmptyBadgeDrawsNothing(string? badge) => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => (i.Icon, i.Badge) = (PlainSprite, () => badge))];
        rig.Frame();
        Assert.Empty(WithColour(rig.Rail, ButtonColour));
    });

    [Fact]
    public void ABadgeIsReReadEveryFrame() => Scoped(() =>
    {
        Paint();
        var rig = new RailRig();
        var text = "1";
        rig.Items = [rig.Item("a", i => (i.Icon, i.Badge) = (PlainSprite, () => text))];
        rig.Frame();
        AssertText(rig.Rail, "1");
        text = "7";
        rig.Frame();
        AssertText(rig.Rail, "7");
    });

    [Theory]
    [InlineData("a", "a")]
    [InlineData("b", "b")]
    public void ANullItemEntryTakesNoRowAndIsSkipped(string active, string expected) => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), null!, rig.Item("b")];
        rig.State!.Active = active;
        rig.Frame();
        Assert.Equal(expected, rig.State.Active);
        Assert.Equal([expected], rig.Bodies);
        rig.ClickRow(1);
        Assert.Equal("b", rig.State.Active);
    });

    [Fact]
    public void ANullItemLabelDrawsInAnExpandedRail() => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a", i => i.Label = null!)];
        rig.State!.ShowLabels = true;
        rig.Frame();
        Assert.False(rig.Rail.IsNull);
        Assert.Equal(rig.ProbeBefore, rig.ProbeAfter);
    });

    public enum RailVariant { Collapsed, Expanded, FallbackFromHidden, NothingVisible, NullState, NullItems, EmptyItems }

    [Theory]
    [InlineData(RailVariant.Collapsed)]
    [InlineData(RailVariant.Expanded)]
    [InlineData(RailVariant.FallbackFromHidden)]
    [InlineData(RailVariant.NothingVisible)]
    [InlineData(RailVariant.NullState)]
    [InlineData(RailVariant.NullItems)]
    [InlineData(RailVariant.EmptyItems)]
    public void TheRailLeavesTheIdStackBalancedInEveryVariant(RailVariant variant) => Scoped(() =>
    {
        var rig = new RailRig();
        rig.Items = [rig.Item("a"), rig.Item("b")];
        switch (variant)
        {
            case RailVariant.Expanded:
                rig.State!.ShowLabels = true;
                break;
            case RailVariant.FallbackFromHidden:
                rig.Items = [rig.Item("gone", i => i.Visible = () => false), rig.Item("b")];
                rig.State!.Active = "gone";
                break;
            case RailVariant.NothingVisible:
                rig.Items = [rig.Item("a", i => i.Visible = () => false)];
                break;
            case RailVariant.NullState:
                rig.State = null;
                break;
            case RailVariant.NullItems:
                rig.Items = null;
                break;
            case RailVariant.EmptyItems:
                rig.Items = [];
                break;
        }
        rig.Frame();
        Assert.Equal(rig.ProbeBefore, rig.ProbeAfter);
    });

    [Fact]
    public void ABodyThatReadsIdsSeesTheRailsOwnScope() => Scoped(() =>
    {
        var inside = new List<uint>();
        var outside = 0u;
        var first = new RailRig();
        var second = new RailRig();
        first.Items = [new NavItem { Key = "x", Label = "x", Body = () => { inside.Add(ImGui.GetID("probe")); return false; } }];
        second.Items = [new NavItem { Key = "x", Label = "x", Body = () => { inside.Add(ImGui.GetID("probe")); return false; } }];
        UiFrame.Run(Win, () =>
        {
            outside = ImGui.GetID("probe");
            first.Draw();
            second.Draw();
        });
        Assert.Equal(2, inside.Count);
        Assert.NotEqual(inside[0], inside[1]);
        Assert.DoesNotContain(outside, inside);
    });

    [Fact]
    public void TwoRailsWithDifferentIdsKeepIndependentSelections() => Scoped(() =>
    {
        var first = new RailRig { Wrapper = "first" };
        var second = new RailRig { Wrapper = "second" };
        first.Items = [first.Item("a"), first.Item("b")];
        second.Items = [second.Item("a"), second.Item("b")];
        void Both()
        {
            first.Draw();
            second.Draw();
        }
        UiFrame.Run(Win, Both);
        Assert.NotEqual(first.Rail.Pos.Y, second.Rail.Pos.Y);
        UiFrame.Click(Win, RowCentre(second.Rail, 1, second.IconSize), Both);
        Assert.Equal("b", second.State!.Active);
        Assert.Equal("a", first.State!.Active);
    });

    [Fact]
    public void TheSameRailIdUnderTwoScopesKeepsIndependentSelections() => Scoped(() =>
    {
        var first = new RailRig { Id = "shared", Scope = "one", Wrapper = "wrap" };
        var second = new RailRig { Id = "shared", Scope = "two", Wrapper = "wrap" };
        first.Items = [first.Item("a"), first.Item("b")];
        second.Items = [second.Item("a"), second.Item("b")];
        void Both()
        {
            first.Draw();
            second.Draw();
        }
        UiFrame.Run(Win, Both);
        UiFrame.Click(Win, RowCentre(second.Rail, 1, second.IconSize), Both);
        Assert.Equal("b", second.State!.Active);
        Assert.Equal("a", first.State!.Active);
    });

    static (string Label, Func<bool>? Body)[] Pages(List<string> log, bool lastResult = false) =>
    [
        ("A", () => { log.Add("A"); return false; }),
        ("B", () => { log.Add("B"); return false; }),
        ("C", () => { log.Add("C"); return lastResult; }),
    ];

    static float[] SideRowOffsets((string Label, Func<bool>? Body)[] tabs)
    {
        List<float> offsets = [];
        UiFrame.Run(Win, () =>
        {
            ImGui.BeginChild("##ref", new Vector2(100, 0), ImGuiChildFlags.Borders);
            var start = ImGui.GetCursorScreenPos().Y;
            var line = ImGui.GetTextLineHeight();
            foreach (var tab in tabs)
            {
                offsets.Add(ImGui.GetCursorScreenPos().Y - start + line / 2);
                if (tab.Body is null) ImGui.SeparatorText(tab.Label);
                else ImGui.Selectable(tab.Label, false);
            }
            ImGui.EndChild();
        });
        return [.. offsets];
    }

    sealed class SideRig(string id, float leftWidth, (string Label, Func<bool>? Body)[] tabs)
    {
        public string Id = id;
        public string? Scope;
        public string? Wrapper;
        public (string Label, Func<bool>? Body)[] Tabs = tabs;
        public bool Returned;
        public uint ProbeBefore;
        public uint ProbeAfter;
        string _navName = "";
        string _bodyName = "";

        public void Draw()
        {
            var parent = Open(Scope, Wrapper);
            ProbeBefore = ImGui.GetID("probe");
            Returned = SideNav.SideTabs(Id, leftWidth, Tabs);
            ProbeAfter = ImGui.GetID("probe");
            ImGui.PushID(Id);
            _navName = $"{parent}/##nav_{ImGui.GetID("##nav"):X8}";
            _bodyName = $"{parent}/##body_{ImGui.GetID("##body"):X8}";
            ImGui.PopID();
            Close(Scope, Wrapper);
        }

        public void Frame() => UiFrame.Run(Win, Draw);

        public ImGuiWindowPtr Nav => Find(_navName);

        public ImGuiWindowPtr BodyPane => Find(_bodyName);

        public Vector2 Row(int index)
        {
            var content = Nav.ContentRegionRect;
            return new Vector2(content.Min.X + 6f, content.Min.Y + SideRowOffsets(Tabs)[index]);
        }
    }

    [Fact]
    public void ClickingATabSelectsItDrawsOnlyItsBodyAndKeepsItAcrossFrames() => Scoped(() =>
    {
        var log = new List<string>();
        var rig = new SideRig(NextId("side"), 120f, Pages(log));
        rig.Frame();
        Assert.Equal(["A"], log);
        UiFrame.Click(Win, rig.Row(1), rig.Draw);
        log.Clear();
        rig.Frame();
        rig.Frame();
        Assert.Equal(["B", "B"], log);
    });

    [Fact]
    public void TheSideTabsReturnTheSelectedBodysChangedFlag() => Scoped(() =>
    {
        var log = new List<string>();
        var rig = new SideRig(NextId("side"), 120f, Pages(log, lastResult: true));
        rig.Frame();
        Assert.False(rig.Returned);
        UiFrame.Click(Win, rig.Row(2), rig.Draw);
        rig.Frame();
        Assert.True(rig.Returned);
    });

    [Fact]
    public void ClickingAHeaderDoesNotSelectIt() => Scoped(() =>
    {
        var log = new List<string>();
        (string Label, Func<bool>? Body)[] tabs = [("Group", null), ("A", () => { log.Add("A"); return false; }), ("B", () => { log.Add("B"); return false; })];
        var rig = new SideRig(NextId("side"), 120f, tabs);
        rig.Frame();
        UiFrame.Click(Win, rig.Row(2), rig.Draw);
        UiFrame.Click(Win, rig.Row(0), rig.Draw);
        log.Clear();
        rig.Frame();
        Assert.Equal(["B"], log);
    });

    [Fact]
    public void TabsWithTheSameLabelAreSeparateRows() => Scoped(() =>
    {
        var log = new List<string>();
        (string Label, Func<bool>? Body)[] tabs = [("Same", () => { log.Add("first"); return false; }), ("Same", () => { log.Add("second"); return false; })];
        var rig = new SideRig(NextId("side"), 120f, tabs);
        rig.Frame();
        UiFrame.Click(Win, rig.Row(1), rig.Draw);
        log.Clear();
        rig.Frame();
        Assert.Equal(["second"], log);
    });

    [Fact]
    public void TheSelectedTabIsFilledWithHeader() => Scoped(() =>
    {
        Paint();
        var rig = new SideRig(NextId("side"), 120f, Pages([]));
        rig.Frame();
        UiFrame.Click(Win, rig.Row(1), rig.Draw);
        rig.Frame();
        var nav = rig.Nav;
        var filled = Bounds(UiFrame.Verts(nav.DrawList).Where(v => v.Col == U32(HeaderColour)));
        var offsets = SideRowOffsets(rig.Tabs);
        var top = nav.ContentRegionRect.Min.Y;
        Assert.InRange(top + offsets[1], filled.Min.Y, filled.Max.Y);
        Assert.True(top + offsets[0] < filled.Min.Y);
        Assert.True(top + offsets[2] > filled.Max.Y);
    });

    [Fact]
    public void TheNavColumnIsTheRequestedWidthAndTheBodyStartsAfterIt() => Scoped(() =>
    {
        var rig = new SideRig(NextId("side"), 137f, Pages([]));
        rig.Frame();
        Assert.Equal(137f, rig.Nav.Size.X, 0.01f);
        Assert.Equal(rig.Nav.Pos.X + 137f + Measure().ItemSpacing.X, rig.BodyPane.Pos.X, 0.01f);
    });

    [Fact]
    public void ASelectionThatLandsOnAHeaderAfterTheTabsChangeFallsBackToTheFirstTab() => Scoped(() =>
    {
        var log = new List<string>();
        var rig = new SideRig(NextId("side"), 120f, Pages(log));
        rig.Frame();
        UiFrame.Click(Win, rig.Row(2), rig.Draw);
        log.Clear();
        rig.Frame();
        Assert.Equal(["C"], log);
        log.Clear();
        rig.Tabs = [("A", () => { log.Add("A"); return false; }), ("B", () => { log.Add("B"); return false; }), ("Group", null)];
        rig.Frame();
        Assert.Equal(["A"], log);
    });

    [Fact]
    public void TwoSideTabsWithDifferentIdsKeepIndependentSelections() => Scoped(() =>
    {
        var logFirst = new List<string>();
        var logSecond = new List<string>();
        var first = new SideRig(NextId("side"), 100f, Pages(logFirst)) { Wrapper = "first" };
        var second = new SideRig(NextId("side"), 100f, Pages(logSecond)) { Wrapper = "second" };
        void Both()
        {
            first.Draw();
            second.Draw();
        }
        UiFrame.Run(Win, Both);
        UiFrame.Click(Win, second.Row(1), Both);
        logFirst.Clear();
        logSecond.Clear();
        UiFrame.Run(Win, Both);
        Assert.Equal(["A"], logFirst);
        Assert.Equal(["B"], logSecond);
    });

    [Fact]
    public void TheSameSideTabsIdUnderTwoScopesKeepsIndependentSelections() => Scoped(() =>
    {
        var logOne = new List<string>();
        var logTwo = new List<string>();
        var one = new SideRig("shared", 100f, Pages(logOne)) { Scope = "one", Wrapper = "wrap" };
        var two = new SideRig("shared", 100f, Pages(logTwo)) { Scope = "two", Wrapper = "wrap" };
        void Both()
        {
            one.Draw();
            two.Draw();
        }
        UiFrame.Run(Win, Both);
        UiFrame.Click(Win, two.Row(1), Both);
        logOne.Clear();
        logTwo.Clear();
        UiFrame.Run(Win, Both);
        Assert.Equal(["A"], logOne);
        Assert.Equal(["B"], logTwo);
    });

    public enum SideVariant { Tabs, WithHeader, AllHeaders, Empty, BodyThrows }

    [Theory]
    [InlineData(SideVariant.Tabs)]
    [InlineData(SideVariant.WithHeader)]
    [InlineData(SideVariant.AllHeaders)]
    [InlineData(SideVariant.Empty)]
    [InlineData(SideVariant.BodyThrows)]
    public void TheSideTabsLeaveTheIdStackBalancedInEveryVariant(SideVariant variant) => Scoped(() =>
    {
        (string Label, Func<bool>? Body)[] tabs = variant switch
        {
            SideVariant.WithHeader => Tabs,
            SideVariant.AllHeaders => [("Only", null)],
            SideVariant.Empty => [],
            SideVariant.BodyThrows => [("A", () => throw new InvalidOperationException("body"))],
            _ => Pages([]),
        };
        var rig = new SideRig(NextId("side"), 100f, tabs);
        var thrown = false;
        UiFrame.Run(Win, () =>
        {
            rig.ProbeBefore = ImGui.GetID("probe");
            try
            {
                SideNav.SideTabs(rig.Id, 100f, rig.Tabs);
            }
            catch (InvalidOperationException)
            {
                thrown = true;
            }
            rig.ProbeAfter = ImGui.GetID("probe");
        });
        Assert.Equal(rig.ProbeBefore, rig.ProbeAfter);
        Assert.Equal(variant == SideVariant.BodyThrows, thrown);
    });

    [Fact]
    public void AllHeadersAndNoTabsDrawNothingAndReturnFalse() => Scoped(() =>
    {
        var headers = new SideRig(NextId("side"), 100f, [("Only", null)]);
        headers.Frame();
        Assert.False(headers.Returned);
        Assert.True(headers.Nav.IsNull);
        var empty = new SideRig(NextId("side"), 100f, []);
        empty.Frame();
        Assert.False(empty.Returned);
        Assert.True(empty.Nav.IsNull);
    });
}
