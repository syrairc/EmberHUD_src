using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public sealed class DeathReaderTests
{
    sealed class Rig
    {
        public readonly FakeWorld World = new();
        public readonly FakeUi Ui;
        public readonly FakeLua Lua;
        public readonly LuaTable Notify;
        public readonly nint Page;
        public readonly nint Switcher;

        public Rig(double? rate = 10, bool canUse = true, int index = 0, bool withSwitcher = true)
        {
            Ui = new FakeUi(World);
            Lua = new FakeLua(World.Memory, FakeWorld.Module);
            var manager = Lua.NewTable(0, 2);
            Notify = Lua.NewTable(0, 3);
            if (rate is { } r) Lua.SetFloat(Notify, Offsets.LuaDieReduceRate, r);
            Lua.SetBool(Notify, Offsets.LuaDieCanUseCoin, canUse);
            Lua.SetTable(manager, "DieNotify", Notify);
            Lua.SetTable(Lua.Globals, "FightMgr", manager);
            Page = Ui.Widget(Ui.UserClass(Offsets.DeathPanelClassName), Offsets.DeathPanelClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
            var root = Ui.Widget(Ui.PanelClass("CanvasPanel"), "FullView", Vector2.Zero, new Vector2(3440, 1440));
            Switcher = Ui.Widget(Ui.PanelClass("WidgetSwitcher"), Offsets.DeathTokenWidget, Vector2.Zero, new Vector2(40, 40));
            World.Memory.Write(Switcher + Offsets.SwitcherActiveIndex, index);
            Ui.SetTree(Page, root);
            Ui.SetChildren(root, withSwitcher ? [Switcher] : []);
            Ui.AddRoot(Ui.Host(Page));
        }

        public DeathState? Read(bool panelOpen = true)
        {
            var ui = World.UiReader();
            var state = panelOpen ? ui.Read() : GameUi.Empty;
            return new DeathReader(World.Memory, FakeWorld.Module, ui).Read(state);
        }
    }

    [Fact]
    public void ReadsRateAndUncheckedToken()
    {
        var death = new Rig().Read()!;
        Assert.Equal(10, death.XpLossPercent);
        Assert.True(death.CanUseToken);
        Assert.False(death.TokenChecked);
    }

    [Fact]
    public void CheckedSwitcherIsTokenChecked() => Assert.True(new Rig(index: 1).Read()!.TokenChecked);

    [Fact]
    public void NoPanelIsNull() => Assert.Null(new Rig().Read(panelOpen: false));

    [Fact]
    public void MissingRateIsNull() => Assert.Null(new Rig(rate: null).Read());

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void RateOutOfRangeIsNull(double rate) => Assert.Null(new Rig(rate: rate).Read());

    [Fact]
    public void CannotUseTokenIsNotChecked()
    {
        var death = new Rig(canUse: false, index: 1).Read()!;
        Assert.False(death.CanUseToken);
        Assert.False(death.TokenChecked);
    }

    [Fact]
    public void MissingSwitcherIsUnknown() => Assert.Null(new Rig(withSwitcher: false).Read()!.TokenChecked);

    [Fact]
    public void GarbageIndexIsUnknown() => Assert.Null(new Rig(index: 7).Read()!.TokenChecked);
}
