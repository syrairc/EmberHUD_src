using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class InventoryReaderStashTests
{
    static LuaTable Tab(InventoryReaderTests.Fixture f, int tab, int[] slots, int type = 7)
    {
        var table = f.Lua.NewTable(0, 6);
        foreach (var slot in slots) f.Lua.SetIntKeyTable(table, slot, f.Item($"5_{slot}", 5, 1, 9, type: type));
        f.Lua.SetIntKeyTable(f.Bags, Offsets.StashTabFirstKey + tab, table);
        return table;
    }

    [Fact]
    public void ReadsTheTabsTable()
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, [5]);
        Tab(f, 1, [0, 1, 2, 3]);
        Assert.Equal([0, 1, 2, 3], f.Reader().ReadStash(1)!.Select(i => i.Slot));
    }

    [Fact]
    public void GapSlotHasNoItem()
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, [.. Enumerable.Range(0, 9), .. Enumerable.Range(10, 7)]);
        var items = f.Reader().ReadStash(0)!;
        Assert.Equal(16, items.Length);
        Assert.DoesNotContain(items, i => i.Slot == 9);
    }

    [Theory]
    [InlineData(1, InventoryBag.Equipment)]
    [InlineData(34, InventoryBag.Equipment)]
    [InlineData(26, InventoryBag.Commodities)]
    [InlineData(7, InventoryBag.Commodities)]
    public void GearTypeIsEquipmentBag(int type, InventoryBag bag)
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, [0], type);
        var item = f.Reader().ReadStash(0)!.Single();
        Assert.Equal((bag, type), (item.Bag, item.Type));
    }

    [Fact]
    public void OtherTabIsNotRead()
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, []);
        Tab(f, 1, [0, 1]);
        Assert.Empty(f.Reader().ReadStash(0)!);
    }

    [Fact]
    public void FailedWalkIsNull()
    {
        Assert.Null(new InventoryReaderTests.Fixture(withManager: false).Reader().ReadStash(0));
        Assert.Null(new InventoryReaderTests.Fixture().Reader().ReadStash(0));
    }

    [Fact]
    public void KeyZeroInNodePartIsRead()
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, [0]);
        Assert.Equal(0, Assert.Single(f.Reader().ReadStash(0)!).Slot);
    }

    [Fact]
    public void TabChangeInsideTheRefreshWindowRebuilds()
    {
        var f = new InventoryReaderTests.Fixture();
        Tab(f, 0, [0]);
        Tab(f, 1, [3, 4]);
        var reader = f.Reader();
        Assert.Equal([0], reader.ReadStash(0)!.Select(i => i.Slot));
        f.Now = 1;
        Assert.Equal([3, 4], reader.ReadStash(1)!.Select(i => i.Slot));
    }

    [Fact]
    public void StashRefreshesEveryStashItemsInterval()
    {
        var f = new InventoryReaderTests.Fixture();
        var table = Tab(f, 0, [0]);
        var reader = f.Reader();
        var first = reader.ReadStash(0);
        f.Lua.SetIntKeyTable(table, 1, f.Item("6_a", 6, 1, 9));
        f.Now = System.Diagnostics.Stopwatch.Frequency * Offsets.StashItemsRefreshMilliseconds / 1000 - 1;
        Assert.Same(first, reader.ReadStash(0));
        f.Now += 1;
        Assert.Equal(2, reader.ReadStash(0)!.Length);
    }

    [Fact]
    public void StashReadHasItsOwnTimer()
    {
        var f = new InventoryReaderTests.Fixture();
        f.Bag(2, 1);
        Tab(f, 0, [0]);
        var reader = f.Reader();
        Assert.NotNull(reader.Read());
        Assert.NotNull(reader.ReadStash(0));
    }
}
