using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class SpaceTests
{
    static InventoryItem Item(InventoryBag bag, int slot) => new(bag, slot, "1_a", 1, 1, 0, 0, 1, 1, 0);

    static GameState State(params InventoryItem[] items) => new() { Inventory = items, BagCapacities = [56, 60, 100, 120] };

    static Main Plugin(bool enabled = true, bool bar = false)
    {
        var plugin = new Main();
        plugin.Initialize();
        plugin.Settings.SpaceEnabled = enabled;
        plugin.Settings.SpaceBar = bar;
        return plugin;
    }

    [Test]
    public void RowsCountItemsPerBagAndKeepOnlyEnabledBags()
    {
        var settings = new Settings { SpaceBags = [true, false, true, false] };
        var items = new[] { Item(InventoryBag.Equipment, 0), Item(InventoryBag.Equipment, 1), Item(InventoryBag.Skills, 0), Item(InventoryBag.Commodities, 5) };
        var rows = Space.Rows(items, [56, 60, 100, 120], settings);
        Check.Equal(2, rows.Count);
        Check.Equal("Gear 2/56 (54 free)", rows[0].Text);
        Check.Equal("Commodities 1/100 (99 free)", rows[1].Text);
    }

    [Test]
    public void FreeNeverGoesNegative() => Check.Equal(0, new Space.Row("Gear", 70, 56).Free);

    [Test]
    public void NormalizeRepairsBagTogglesAndBarSizes()
    {
        var settings = new Settings { SpaceBags = [false], SpaceText = null!, BarSegmentWidth = 0, BarSegmentHeight = float.NaN, BarSpacing = -4, BarRowLength = -1 };
        settings.Normalize();
        Check.Equal(4, settings.SpaceBags.Length);
        Check.That(!settings.SpaceBags[0] && settings.SpaceBags.Skip(1).All(b => b), "padded");
        Check.Equal(1f, settings.BarSegmentWidth);
        Check.Equal(12f, settings.BarSegmentHeight);
        Check.Equal(0f, settings.BarSpacing);
        Check.Equal(0, settings.BarRowLength);
        Check.That(settings.SpaceText != null);
        settings.SpaceBags = null!;
        settings.Normalize();
        Check.Equal(4, settings.SpaceBags.Length);
    }

    [Test]
    public void OffByDefaultAndDrawsNothingWhenOff()
    {
        Check.That(!new Settings().SpaceEnabled);
        TestGame.Set(State(Item(InventoryBag.Equipment, 0)));
        Check.Equal(0, TestFrame.Vertices(Plugin(enabled: false).Render));
    }

    [Test]
    public void TextModeDrawsAndBarModeDrawsMore()
    {
        TestGame.Set(State(Item(InventoryBag.Equipment, 0)));
        var text = TestFrame.Vertices(Plugin().Render);
        var bars = TestFrame.Vertices(Plugin(bar: true).Render);
        Check.That(text > 0, "text");
        Check.That(bars >= text + 4 * (56 + 60 + 100 + 120), "bar segments");
    }

    [Test]
    public void NoCapacityOrInventoryDrawsNothing()
    {
        TestGame.Set(new GameState { Inventory = [] });
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
        TestGame.Set(new GameState { BagCapacities = [56, 60, 100, 120] });
        Check.Equal(0, TestFrame.Vertices(Plugin().Render));
    }

    [Test]
    public void AsksForTheInventoryOnlyWhileEnabled()
    {
        TestGame.Set(State());
        var on = Plugin();
        var off = Plugin(enabled: false);
        Check.That(TestGame.InventoryAskedDuring(() => TestFrame.Run(on.Render)), "on");
        Check.That(!TestGame.InventoryAskedDuring(() => TestFrame.Run(off.Render)), "off");
    }

    [Test]
    public void SpaceSettingsPageDrawsHeadless()
    {
        var settings = new Settings { SpaceEnabled = true, SpaceBar = true };
        TestFrame.Run(settings.Draw);
        settings.SpaceBar = false;
        TestFrame.Run(settings.Draw);
    }
}
