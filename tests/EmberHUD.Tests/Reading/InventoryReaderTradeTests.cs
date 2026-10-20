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

    static (LuaTable Simple, LuaTable Advanced) Stores(InventoryReaderTests.Fixture f)
    {
        var simple = f.Lua.NewTable(4, 2);
        var advanced = f.Lua.NewTable(4, 2);
        var manager = f.Lua.NewTable(0, 4);
        f.Lua.SetTable(manager, Offsets.LuaAuctionListings, simple);
        f.Lua.SetTable(manager, Offsets.LuaAuctionAdvancedListings, advanced);
        f.Lua.SetTable(f.Lua.Globals, Offsets.LuaAuctionManager, manager);
        return (simple, advanced);
    }

    static LuaTable Listing(InventoryReaderTests.Fixture f, LuaTable listings, int index, long goodId, long price, string status, int baseId, int count, int stackMax = 9999, int priceType = 100300, int type = 15, bool withItem = true, int specialId = 0)
    {
        var record = f.Lua.NewTable(0, 4);
        f.Lua.SetInt(record, Offsets.LuaAuctionGoodId, goodId);
        f.Lua.SetInt(record, Offsets.LuaAuctionPriceType, priceType);
        f.Lua.SetInt(record, Offsets.LuaAuctionPrice, price);
        f.Lua.SetString(record, Offsets.LuaAuctionStatus, status);
        if (withItem) f.Lua.SetTable(record, Offsets.LuaAuctionItem, f.Item($"{baseId}_{goodId}", baseId, count, stackMax, type: type, specialId: specialId));
        var entry = f.Lua.NewTable(0, 2);
        f.Lua.SetTable(entry, Offsets.LuaItemRecord, record);
        f.Lua.SetIntKeyTable(listings, index, entry);
        return entry;
    }

    static void Advance(InventoryReaderTests.Fixture f) => f.Now += Stopwatch.Frequency;

    static void Affix(InventoryReaderTests.Fixture f, LuaTable list, string name, params (int Id, int Tier, double[] Rolls, string Text)[] rows)
    {
        var table = f.Lua.NewTable(rows.Length, 1);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = f.Lua.NewTable(0, 3);
            f.Lua.SetInt(row, Offsets.LuaGroundAffixId, rows[i].Id);
            f.Lua.SetInt(row, Offsets.LuaAffixTier, rows[i].Tier);
            f.Lua.SetString(row, Offsets.LuaAffixComplex, rows[i].Text);
            if (rows[i].Rolls.Length > 0)
            {
                var args = f.Lua.NewTable(rows[i].Rolls.Length, 0);
                for (var r = 0; r < rows[i].Rolls.Length; r++) f.Lua.SetIntKeyFloat(args, r + 1, rows[i].Rolls[r]);
                f.Lua.SetTable(row, Offsets.LuaAffixRolls, args);
            }

            f.Lua.SetIntKeyTable(table, i + 1, row);
        }

        f.Lua.SetTable(list, name, table);
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

    [Fact]
    public void ReadsBothStoresAndMarksTheAdvancedOnes()
    {
        var f = new InventoryReaderTests.Fixture();
        var (simple, advanced) = Stores(f);
        Listing(f, simple, 1, 1, 10, "Available", 7, 1);
        Listing(f, advanced, 1, 2, 20, "Available", 8, 1);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.Equal([1L, 2L], goods.Select(g => g.GoodId));
        Assert.False(goods[0].Advanced);
        Assert.True(goods[1].Advanced);
    }

    [Fact]
    public void OneStoreMissingIsStillRead()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 7, 1);
        Assert.Single(f.Reader().ReadTradeGoods()!);
    }

    [Fact]
    public void DuplicateGoodIdsKeepTheFirst()
    {
        var f = new InventoryReaderTests.Fixture();
        var (simple, advanced) = Stores(f);
        Listing(f, simple, 1, 5, 10, "Available", 7, 1);
        Listing(f, simple, 2, 5, 99, "Available", 7, 1);
        Listing(f, advanced, 1, 5, 77, "Available", 7, 1);
        Listing(f, advanced, 2, 6, 66, "Available", 7, 1);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.Equal([5L, 6L], goods.Select(g => g.GoodId));
        Assert.Equal(10, goods[0].Price);
        Assert.False(goods[0].Advanced);
    }

    [Fact]
    public void LaterPassesOnlyRefreshPriceAndStatus()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        var entry = Listing(f, listings, 1, 1, 10, "Available", 7, 1);
        var itemRaw = f.LastRecord;
        var reader = f.Reader();
        var first = reader.ReadTradeGoods()![0];

        f.Lua.UpdateInt(itemRaw, Offsets.LuaItemLevel, 99);
        Advance(f);
        Assert.Same(first, reader.ReadTradeGoods()![0]);

        var record = ListingRecord(f, entry);
        f.Lua.UpdateInt(record, Offsets.LuaAuctionPrice, 42);
        f.Lua.UpdateString(record, Offsets.LuaAuctionStatus, "Sold");
        Advance(f);
        var second = reader.ReadTradeGoods()![0];

        Assert.Equal(42, second.Price);
        Assert.Equal("Sold", second.Status);
        Assert.Equal(10, second.Item.Level);
    }

    static LuaTable ListingRecord(InventoryReaderTests.Fixture f, LuaTable entry)
    {
        var lua = new LuaReader(f.Memory, f.Module);
        lua.TryOpen(out _);
        Assert.True(lua.TryGetTable(entry, Offsets.LuaItemRecord, out var record));
        return record;
    }

    [Fact]
    public void AnAddressThatLeavesTheListIsDroppedFromTheCache()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        var first = Listing(f, listings, 1, 1, 10, "Available", 7, 1);
        var itemRaw = f.LastRecord;
        var reader = f.Reader();
        reader.ReadTradeGoods();

        Listing(f, listings, 1, 2, 20, "Available", 8, 1);
        Advance(f);
        Assert.Equal([2L], reader.ReadTradeGoods()!.Select(g => g.GoodId));

        f.Lua.UpdateInt(itemRaw, Offsets.LuaItemLevel, 99);
        f.Lua.SetIntKeyTable(listings, 1, first);
        Advance(f);
        Assert.Equal(99, reader.ReadTradeGoods()![0].Item.Level);
    }

    [Fact]
    public void ReadsAllFourAffixKindsWithFloatRollsAndLongText()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 110001, 1, stackMax: 1, type: 1);
        var list = f.Lua.NewTable(0, 2);
        var longText = "<p>" + new string('x', Offsets.MaxLuaString + 40) + "</p>";
        Affix(f, list, Offsets.LuaItemBaseAffixList, (1101, 2, [], "implicit"));
        Affix(f, list, Offsets.LuaItemPrefixInfoList, (2203, 3, [11.0, 2.5], longText), (2301, 1, [4.0], "p2"));
        Affix(f, list, Offsets.LuaItemSuffixInfoList, (3302, 2, [7.0], "s1"));
        Affix(f, list, Offsets.LuaItemBaseAttrInfoList, (4401, 1, [120.0], "armor"));
        f.Lua.SetTable(f.LastRecord, Offsets.LuaItemAffixList, list);

        var affixes = f.Reader().ReadTradeGoods()![0].Affixes;

        Assert.Equal([TradeAffixKind.Implicit, TradeAffixKind.Prefix, TradeAffixKind.Prefix, TradeAffixKind.Suffix, TradeAffixKind.BaseAttribute], affixes.Select(a => a.Kind));
        Assert.Equal([1101, 2203, 2301, 3302, 4401], affixes.Select(a => a.Id));
        Assert.Equal(3, affixes[1].Tier);
        Assert.Equal([11f, 2.5f], affixes[1].Rolls);
        Assert.Empty(affixes[0].Rolls);
        Assert.Equal(Offsets.MaxLuaString, affixes[1].Text.Length);
        Assert.StartsWith("<p>xxx", affixes[1].Text);
        Assert.Equal("armor", affixes[4].Text);
    }

    [Fact]
    public void FlagsComeFromTheTrueEntriesOfTheStatusTable()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 110001, 1, stackMax: 1, type: 1);
        var status = f.Lua.NewTable(0, 2);
        f.Lua.SetBool(status, "FORGE_PRECISE", true);
        f.Lua.SetBool(status, "LOCKED", false);
        f.Lua.SetTable(f.LastRecord, Offsets.LuaItemFlags, status);
        f.Lua.SetInt(f.LastRecord, Offsets.LuaItemEnhanceCount, 4);
        f.Lua.SetBool(f.LastRecord, Offsets.LuaItemForged, true);
        f.Lua.SetBool(f.LastRecord, Offsets.LuaItemCorrode, false);

        var good = f.Reader().ReadTradeGoods()![0];

        Assert.Equal(["FORGE_PRECISE"], good.Flags);
        Assert.Equal(4, good.EnhanceCount);
        Assert.True(good.Forged);
        Assert.False(good.Corrode);
    }

    [Fact]
    public void SkillsGetSpecialInfoAndTheirItemLevel()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 500001, 1, stackMax: 1, type: Offsets.TradeSkillItemType);
        var special = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(special, Offsets.LuaSpecialSkillId, 1234);
        f.Lua.SetInt(special, Offsets.LuaSpecialSkillType, 2);
        f.Lua.SetInt(special, Offsets.LuaSpecialHoleNum, 3);
        f.Lua.SetTable(f.LastRecord, Offsets.LuaItemSpecialInfo, special);
        var server = f.Lua.NewTable(0, 1);
        f.Lua.SetInt(server, Offsets.LuaServerSkillItemLevel, 85);
        f.Lua.SetTable(f.LastRecord, Offsets.LuaItemServerData, server);

        var good = f.Reader().ReadTradeGoods()![0];

        Assert.Equal(1234, good.SkillId);
        Assert.Equal(2, good.SkillType);
        Assert.Equal(3, good.HoleNum);
        Assert.Equal(85, good.ItemLevel);
        Assert.Equal(10, good.Item.Level);
    }

    [Fact]
    public void TheLegendaryIdComesFromTheItemRecord()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 4518, 1, stackMax: 1, type: 1, specialId: 110103);
        Listing(f, listings, 2, 2, 10, "Available", 4518, 1, stackMax: 1, type: 1);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.Equal(110103, goods[0].SpecialId);
        Assert.Equal(0, goods[1].SpecialId);
    }

    [Fact]
    public void AnItemTheGameHasNotEvaluatedIsUnidentified()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 3611, 1, stackMax: 1, type: 1, specialId: 112256);
        f.Lua.SetBool(f.LastRecord, Offsets.LuaItemEvaluated, false);
        Listing(f, listings, 2, 2, 10, "Available", 3611, 1, stackMax: 1, type: 1, specialId: 112256);
        f.Lua.SetBool(f.LastRecord, Offsets.LuaItemEvaluated, true);
        Listing(f, listings, 3, 3, 10, "Available", 3611, 1, stackMax: 1, type: 1, specialId: 112256);

        var goods = f.Reader().ReadTradeGoods()!;

        Assert.True(goods[0].Unidentified);
        Assert.False(goods[1].Unidentified);
        Assert.False(goods[2].Unidentified);
    }

    [Fact]
    public void GearKeepsItsLevelAsItemLevel()
    {
        var f = new InventoryReaderTests.Fixture();
        var listings = Manager(f);
        Listing(f, listings, 1, 1, 10, "Available", 110001, 1, stackMax: 1, type: 1);
        var server = f.Lua.NewTable(0, 1);
        f.Lua.SetInt(server, Offsets.LuaServerSkillItemLevel, 85);
        f.Lua.SetTable(f.LastRecord, Offsets.LuaItemServerData, server);

        var good = f.Reader().ReadTradeGoods()![0];

        Assert.Equal(10, good.ItemLevel);
        Assert.Empty(good.Affixes);
        Assert.Empty(good.Flags);
    }
}
