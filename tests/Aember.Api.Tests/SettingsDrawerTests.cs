using System.Drawing;
using Hexa.NET.ImGui;
using Aember.Ui;

namespace Aember.Api.Tests;

public sealed class SettingsDrawerTests
{
    public sealed class MapTab
    {
        public ToggleNode ShowWalls = new(true);
        [Menu("Zoom level", "How far in, 100% is the game's own")] public RangeNode<float> Zoom = new(8, 1, 40);
    }

    public sealed class Everything : PluginSettings
    {
        public MapTab Map = new();
        public ToggleNode Show = new(true);
        public RangeNode<int> Count = new(3, 0, 10);
        public ColorNode Colour = new(Color.White);
        public HotkeyNode Toggle = new(new Hotkey(Key.F10));
        public TextNode Name = new("map");
        public ChoiceNode Mode = new("Fit", "Fit", "Follow");
        public RangeNode<double> Unsupported = new(1, 0, 2);
        public int Plain = 4;
    }

    public sealed class WallsTab
    {
        public ToggleNode Show = new(true);
    }

    public sealed class Tabbed2
    {
        public ToggleNode Show = new(true);
    }

    public sealed class Tabbed : PluginSettings
    {
        public WallsTab Walls = new();
    }

    public sealed class Single<T>(T node)
    {
        public T Node = node;
    }

    public sealed class NullText
    {
        public TextNode Name = new("x") { Value = null! };
    }

    public sealed class Tipped
    {
        [Menu("Percent", "50% done %s %d")] public ToggleNode Show = new(true);
    }

    public sealed class Pair
    {
        public ToggleNode First = new(true);
        public ToggleNode Second = new(true);
    }

    static float Height(object holder)
    {
        var height = 0f;
        UiFrame.Run("height", () =>
        {
            var before = ImGui.GetCursorPosY();
            SettingsDrawer.DrawNodes(holder);
            height = ImGui.GetCursorPosY() - before;
        });
        return height;
    }

    [Theory]
    [InlineData("ShowWalls", "Show Walls")]
    [InlineData("X", "X")]
    [InlineData("ShowHP", "Show HP")]
    [InlineData("Zoom2X", "Zoom2 X")]
    public void LabelSplitsTheFieldNameOnCapitals(string field, string label) => Assert.Equal(label, SettingsDrawer.Label(field));

    [Fact]
    public void EveryNodeKindDrawsAndNothingChangesOnItsOwn() => UiFrame.Run("every node", () =>
    {
        var settings = new Everything();
        Assert.False(SettingsDrawer.DrawNodes(settings));
        Assert.False(SettingsDrawer.DrawNodes(settings.Map));
        settings.Draw();
    });

    [Fact]
    public void EachNodeKindTakesSpaceAndTheRestTakesNone()
    {
        Assert.True(Height(new Single<ToggleNode>(new(true))) > 0);
        Assert.True(Height(new Single<RangeNode<int>>(new(1, 0, 2))) > 0);
        Assert.True(Height(new Single<RangeNode<float>>(new(1, 0, 2))) > 0);
        Assert.True(Height(new Single<ColorNode>(new(Color.White))) > 0);
        Assert.True(Height(new Single<HotkeyNode>(new(new Hotkey(Key.F10)))) > 0);
        Assert.True(Height(new Single<TextNode>(new("a"))) > 0);
        Assert.True(Height(new Single<ChoiceNode>(new("a", "a", "b"))) > 0);
        Assert.Equal(0, Height(new Single<RangeNode<double>>(new(1, 0, 2))));
        Assert.Equal(0, Height(new Single<int>(3)));
        Assert.Equal(0, Height(new Single<MapTab>(new())));
        Assert.True(Height(new Single<TextStyle>(TextStyle.WorldLabel())) > 0);
        Assert.Equal(0, Height(new Single<TextStyle>(null!)));
        Assert.Equal(0, Height(new Single<ToggleNode>(null!)));
    }

    [Fact]
    public void ANullTextValueStillDraws()
    {
        var holder = new NullText();
        Assert.True(Height(holder) > 0);
        Assert.Equal("", holder.Name.Value);
    }

    static uint Scoped(string holderType, string id)
    {
        ImGui.PushID(holderType);
        var scoped = ImGui.GetID(id);
        ImGui.PopID();
        return scoped;
    }

    [Fact]
    public void WidgetIdsComeFromTheMenuLabelTheFieldNameAndTheHolderType() => UiFrame.Run("ids", () =>
    {
        SettingsDrawer.DrawNodes(new MapTab());
        Assert.Equal(Scoped(nameof(MapTab), "Zoom level##Zoom"), ImGui.GetItemID());
        SettingsDrawer.DrawNodes(new Single<ToggleNode>(new(true)));
        Assert.Equal(Scoped(typeof(Single<ToggleNode>).Name, "Node##Node"), ImGui.GetItemID());
    });

    [Fact]
    public void TwoHoldersWithTheSameFieldNameDoNotShareIds() => UiFrame.Run("clash", () =>
    {
        SettingsDrawer.DrawNodes(new WallsTab());
        var first = ImGui.GetItemID();
        SettingsDrawer.DrawNodes(new Tabbed2());
        Assert.NotEqual(first, ImGui.GetItemID());
    });

    static bool ClickChanges(object holder, string window)
    {
        var changed = false;
        var box = Vector2.Zero;
        void Body()
        {
            changed |= SettingsDrawer.DrawNodes(holder);
            box = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run(window, Body);
        UiFrame.Click(window, box, Body);
        return changed;
    }

    [Fact]
    public void ASliderClickReportsTheChange()
    {
        var ints = new Single<RangeNode<int>>(new(0, 0, 10));
        Assert.True(ClickChanges(ints, "int slider"));
        Assert.NotEqual(0, ints.Node.Value);
        var floats = new Single<RangeNode<float>>(new(0, 0, 10));
        Assert.True(ClickChanges(floats, "float slider"));
        Assert.NotEqual(0, floats.Node.Value);
    }

    [Fact]
    public void TypingIntoATextNodeReportsTheChange()
    {
        var holder = new Single<TextNode>(new("a"));
        var changed = false;
        var box = Vector2.Zero;
        void Body()
        {
            changed |= SettingsDrawer.DrawNodes(holder);
            box = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run("text", Body);
        UiFrame.Click("text", box, Body);
        ImGui.GetIO().AddInputCharacter('z');
        UiFrame.Run("text", Body);
        UiFrame.Run("text", Body);
        Assert.True(changed);
        Assert.Equal("az", holder.Node.Value);
    }

    [Fact]
    public void PickingAChoiceReportsTheChange()
    {
        var holder = new Single<ChoiceNode>(new("Fit", "Fit", "Follow"));
        var changed = false;
        var box = Vector2.Zero;
        void Body()
        {
            changed |= SettingsDrawer.DrawNodes(holder);
            box = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run("choice", Body);
        UiFrame.Click("choice", box, Body);
        var popup = ImGuiP.FindWindowByName("##Combo_00");
        Assert.False(popup.IsNull);
        var second = popup.Pos + new Vector2(popup.Size.X / 2, ImGui.GetStyle().WindowPadding.Y + ImGui.GetFrameHeight() * 1.5f);
        UiFrame.Click("choice", second, Body);
        Assert.True(changed);
        Assert.Equal("Follow", holder.Node.Value);
    }

    [Fact]
    public void PickingAColourReportsTheChange()
    {
        var holder = new Single<ColorNode>(new(Color.White));
        var changed = false;
        var swatch = Vector2.Zero;
        var picker = Vector2.Zero;
        void Body()
        {
            changed |= SettingsDrawer.DrawNodes(holder);
            swatch = ImGui.GetItemRectMin() + new Vector2(8, 8);
            ImGui.PushID("Single`1");
            ImGui.PushID("Node##Node");
            var pickerId = ImGui.GetID("picker");
            ImGui.PopID();
            ImGui.PopID();
            var window = ImGuiP.FindWindowByName($"##Popup_{pickerId:x8}");
            if (!window.IsNull) picker = window.Pos;
        }
        UiFrame.Run("colour", Body);
        UiFrame.Click("colour", swatch, Body);
        Assert.NotEqual(Vector2.Zero, picker);
        UiFrame.Click("colour", picker + new Vector2(30, 30), Body);
        Assert.True(changed);
        Assert.NotEqual(Color.White, holder.Node.Value);
    }

    [Fact]
    public void FieldsDrawInDeclarationOrder() => UiFrame.Run("order", () =>
    {
        SettingsDrawer.DrawNodes(new Pair());
        Assert.Equal(Scoped(nameof(Pair), "Second##Second"), ImGui.GetItemID());
    });

    [Fact]
    public void TheMenuTooltipShowsRawWhenHovered()
    {
        const string raw = "50% done %s %d";
        var holder = new Tipped();
        var actual = 0f;
        var expected = 0f;
        void Body() => UiFrame.Run("tip", () =>
        {
            ImGui.GetIO().AddMousePosEvent(20, 36);
            SettingsDrawer.DrawNodes(holder);
        });
        Body();
        Body();
        Body();
        UiFrame.Run("tip", () =>
        {
            SettingsDrawer.DrawNodes(holder);
            var tooltip = ImGuiP.FindWindowByName("##Tooltip_00");
            Assert.False(tooltip.IsNull);
            actual = tooltip.Size.X;
            expected = ImGui.CalcTextSize(raw).X + ImGui.GetStyle().WindowPadding.X * 2;
        });
        Assert.Equal(expected, actual, 1f);
    }

    [Fact]
    public void ANestedHolderDrawsOnItsOwnAndReportsTheChange()
    {
        var settings = new Tabbed();
        var changed = false;
        var box = Vector2.Zero;
        void Body()
        {
            changed |= SettingsDrawer.DrawNodes(settings.Walls);
            box = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
        }
        UiFrame.Run("nested holder", Body);
        UiFrame.Click("nested holder", box, Body);
        Assert.True(changed);
        Assert.False(settings.Walls.Show.Value);
    }
}

public sealed class SettingsDrawerHotkeyTests : IDisposable
{
    readonly HashSet<int> _keys = [];

    public SettingsDrawerHotkeyTests()
    {
        Input.KeyDown = _keys.Contains;
        Input.CancelCapture();
        Input.NewFrame(false);
        Input.NewFrame(false);
    }

    public void Dispose()
    {
        _keys.Clear();
        Input.CancelCapture();
        Input.NewFrame(false);
    }

    [Fact]
    public void BindingAKeyReportsTheChange()
    {
        var holder = new SettingsDrawerTests.Single<HotkeyNode>(new(new Hotkey(Key.F8)));
        var changed = false;
        var button = Vector2.Zero;
        void Body()
        {
            button = ImGui.GetCursorScreenPos() + new Vector2(8, 8);
            changed |= SettingsDrawer.DrawNodes(holder);
        }
        UiFrame.Run("hotkey node", Body);
        UiFrame.Click("hotkey node", button, Body);
        Assert.True(Input.Capturing);
        _keys.Add(0x74);
        Input.NewFrame(false);
        UiFrame.Run("hotkey node", Body);
        Assert.True(changed);
        Assert.Equal(new Hotkey(Key.F5), holder.Node.Value);
    }
}
