using Aember.Testing;

namespace Aember.Api.Tests;

public sealed class HotkeyBindingTests
{
    sealed class Probe : Plugin
    {
        public int Fired;
        public int Zoomed;

        public override void Initialize()
        {
            RegisterHotkey("Toggle", "Shows it", () => Fired++, new Hotkey(Key.F8));
            RegisterHotkey("Zoom in", "Closer", () => Zoomed++, new Hotkey(Key.PageUp), "View");
            RegisterHotkey("Unbound", "No default", () => Fired += 100);
        }

        public void RegisterAgain() => RegisterHotkey("Toggle", "again", () => { }, null);

        public void RegisterSameNameInOtherCategory() => RegisterHotkey("Toggle", "other", () => { }, null, "View");
    }

    static Probe Started()
    {
        var plugin = new Probe();
        plugin.Initialize();
        return plugin;
    }

    [Fact]
    public void RegistrationStoresTheFieldsInOrderWithTheDefaultAsTheValue()
    {
        var plugin = Started();

        Assert.Equal(["Toggle", "View/Zoom in", "Unbound"], plugin.Hotkeys.Select(binding => binding.Id));
        var zoom = plugin.Hotkeys[1];
        Assert.Equal(("Zoom in", "Closer", "View"), (zoom.Name, zoom.Description, zoom.Category));
        Assert.Equal(new Hotkey(Key.PageUp), zoom.Default);
        Assert.Equal(zoom.Default, zoom.Value);
        Assert.Null(plugin.Hotkeys[0].Category);
    }

    [Fact]
    public void NoDefaultMeansUnbound()
    {
        var unbound = Started().Hotkeys[2];

        Assert.Equal(default, unbound.Default);
        Assert.Equal(Key.None, unbound.Value.Key);
    }

    [Fact]
    public void TheSameCategoryAndNameTwiceThrowsButTheSameNameInAnotherCategoryIsFine()
    {
        var plugin = Started();

        Assert.Throws<InvalidOperationException>(plugin.RegisterAgain);
        Assert.Equal(3, plugin.Hotkeys.Count);
        plugin.RegisterSameNameInOtherCategory();
        Assert.Equal(4, plugin.Hotkeys.Count);
    }

    [Fact]
    public void DispatchFiresOnlyThePressedBindingsCallbackOnce()
    {
        var plugin = Started();
        try
        {
            TestInput.Hold();
            TestInput.Dispatch(plugin);
            Assert.Equal(0, plugin.Fired);

            TestInput.Hold(Key.F8);
            TestInput.Dispatch(plugin);
            Assert.Equal((1, 0), (plugin.Fired, plugin.Zoomed));

            TestInput.Hold(Key.F8);
            TestInput.Dispatch(plugin);
            Assert.Equal(1, plugin.Fired);
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Fact]
    public void DispatchUsesTheReboundValueAndNeverFiresAnUnboundOne()
    {
        var plugin = Started();
        plugin.Hotkeys[0].Value = new Hotkey(Key.F9);
        try
        {
            TestInput.Hold(Key.F8);
            TestInput.Dispatch(plugin);
            Assert.Equal(0, plugin.Fired);

            TestInput.Hold();
            TestInput.Hold(Key.F9);
            TestInput.Dispatch(plugin);
            Assert.Equal(1, plugin.Fired);
        }
        finally
        {
            TestInput.Hold();
        }
    }
}
