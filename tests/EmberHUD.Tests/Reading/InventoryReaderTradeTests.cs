using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class InventoryReaderTradeTests
{
    static LuaTable Manager(InventoryReaderTests.Fixture f)
    {
        var listings = f.Lua.NewTable(4, 2);
        var manager = f.Lua.NewTable(0, 4);
        f.Lua.SetTable(manager, Offsets.LuaAuctionListings, listings);
        f.Lua.SetTable(f.Lua.Globals, Offsets.LuaAuctionManager, manager);
        return listings;
    }

    static void Listing(InventoryReaderTests.Fixture f, LuaTable listings, int index, long goodId, long price, string status, int baseId, int count, int stackMax = 9999, int priceType = 100300, int type = 15, bool withItem = true)
    {
        var record = f.Lua.NewTable(0, 4);
        f.Lua.SetInt(record, Offsets.LuaAuctionGoodId, goodId);
        f.Lua.SetInt(record, Offsets.LuaAuctionPriceType, priceType);
        f.Lua.SetInt(record, Offsets.LuaAuctionPrice, price);
        f.Lua.SetString(record, Offsets.LuaAuctionStatus, status);
        if (withItem) f.Lua.SetTable(record, Offsets.LuaAuctionItem, f.Item($"{baseId}_{goodId}", baseId, count, stackMax, type: type));
        var entry = f.Lua.NewTable(0, 2);
        f.Lua.SetTable(entry, Offsets.LuaItemRecord, record);
        f.Lua.SetIntKeyTable(listings, index, entry);
    }

    [Fact]
    public void ReadsEveryListingWithItsItem()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 981152232460525568L, 263, "Available", 200028, 692);
        Listing(f, listings, 2, 981152232460525569L, 32, "Sold", 200028, 40);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.Equal(2, goods.Length);
        Assert.Equal(981152232460525568L, goods[0].GoodId);
        Assert.Equal(100300, goods[0].PriceType);
        Assert.Equal(263, goods[0].Price);
        Assert.Equal("Available", goods[0].Status);
        Assert.Equal(200028, goods[0].Item.BaseId);
        Assert.Equal(692, goods[0].Item.Count);
        Assert.Equal("Sold", goods[1].Status);
        Assert.Equal(40, goods[1].Item.Count);
    }

    [Fact]
    public void ListingOrderFollowsTheGameList()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 3, 3, 30, "Available", 7, 1);
        Listing(f, listings, 1, 1, 10, "Available", 7, 1);
        Listing(f, listings, 2, 2, 20, "Available", 7, 1);

        Assert.Equal([1L, 2L, 3L], f.Reader().ReadTradeGoods()!.Select(g => g.GoodId));
    }

    [Fact]
    public void GearTypesLandInTheEquipmentBag()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 110001, 1, stackMax: 1, type: 1);
        Listing(f, listings, 2, 2, 10, "Available", 200028, 5);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.Equal(InventoryBag.Equipment, goods[0].Item.Bag);
        Assert.Equal(InventoryBag.Commodities, goods[1].Item.Bag);
    }

    [Fact]
    public void AnUnreadableListingIsSkippedNotThrown()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 7, 1, withItem: false);
        Listing(f, listings, 2, 2, 20, "Available", 7, 1);

        Assert.Equal([2L], f.Reader().ReadTradeGoods()!.Select(g => g.GoodId));
    }

    [Fact]
    public void NoAuctionManagerIsNull()
    {
        Assert.Null(new InventoryReaderTests.Fixture().Reader().ReadTradeGoods());
    }

    [Fact]
    public void ReadsAgainOnlyAfterTheRefreshInterval()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 7, 1);
        var reader = f.Reader();
        var first = reader.ReadTradeGoods();
        Listing(f, listings, 2, 2, 20, "Available", 7, 1);
        f.Now = Stopwatch.Frequency * Offsets.TradeGoodsRefreshMilliseconds / 1000 - 1;
        Assert.Same(first, reader.ReadTradeGoods());
        f.Now += 1;
        Assert.Equal(2, reader.ReadTradeGoods()!.Length);
    }
}
