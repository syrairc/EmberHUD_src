using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class InventoryReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory;
        public readonly FakeLua Lua;
        public readonly LuaTable Bags;
        public readonly nint Module;
        public long Now;
        public LuaTable LastRecord;

        public Fixture(bool withManager = true, FakeMemory? memory = null, nint? module = null)
        {
            Memory = memory ?? new FakeMemory();
            Module = module ?? InventoryReaderTests.Module;
            Lua = new FakeLua(Memory, Module);
            Bags = Lua.NewTable(0, 3);
            if (!withManager) return;
            var manager = Lua.NewTable(0, 2);
            Lua.SetTable(manager, Offsets.LuaItemBags, Bags);
            Lua.SetTable(Lua.Globals, Offsets.LuaItemManager, manager);
        }

        public InventoryReader Reader() => new(Memory, Module, () => Now);

        public LuaTable Bag(int bag, int arraySize)
        {
            var table = Lua.NewTable(arraySize, 3);
            Lua.SetIntKeyTable(Bags, Offsets.InventoryTabFirst + bag, table);
            return table;
        }

        public LuaTable Item(string id, int baseId, int count, int stackMax, bool withCount = true, int type = 7, int specialId = 0)
        {
            var record = LastRecord = Lua.NewTable(0, 4);
            Lua.SetString(record, Offsets.LuaItemId, id);
            Lua.SetInt(record, Offsets.LuaItemBaseId, baseId);
            Lua.SetInt(record, Offsets.LuaItemLevel, 10);
            Lua.SetInt(record, Offsets.LuaItemQuality, 2);
            Lua.SetInt(record, Offsets.LuaItemRareValue, 3);
            if (specialId != 0) Lua.SetInt(record, Offsets.LuaItemSpecialId, specialId);
            if (withCount) Lua.SetInt(record, Offsets.LuaItemCount, count);
            Lua.SetInt(record, Offsets.LuaItemStackMax, stackMax);
            Lua.SetInt(record, Offsets.LuaItemType, type);
            var item = Lua.NewTable(0, 4);
            Lua.SetTable(item, Offsets.LuaItemRecord, record);
            return item;
        }

        public LuaTable Rows(int[] ids, bool badFirst = false)
        {
            var table = Lua.NewTable(ids.Length + 1, 1);
            var key = 1;
            if (badFirst) Lua.SetIntKeyTable(table, key++, Lua.NewTable(0, 1));
            foreach (var id in ids)
            {
                var row = Lua.NewTable(0, 2);
                Lua.SetInt(row, Offsets.LuaGroundAffixId, id);
                Lua.SetIntKeyTable(table, key++, row);
            }

            return table;
        }

        public void ShapeA(LuaTable table, int[] prefixes, int[] suffixes, bool badFirst = false)
        {
            Lua.SetTable(table, Offsets.LuaGroundPrefix, Rows(prefixes, badFirst));
            Lua.SetTable(table, Offsets.LuaGroundSuffix, Rows(suffixes));
        }

        public void ShapeB(LuaTable table, int[] prefixes, int[] suffixes)
        {
            var list = Lua.NewTable(0, 2);
            Lua.SetTable(list, Offsets.LuaItemPrefixInfoList, Rows(prefixes));
            Lua.SetTable(list, Offsets.LuaItemSuffixInfoList, Rows(suffixes));
            Lua.SetTable(table, Offsets.LuaItemAffixList, list);
        }

        public InventoryItem OnlyItem(LuaTable entry)
        {
            Lua.SetIntKeyTable(Bag(0, 1), 0, entry);
            return Assert.Single(Reader().Read()!);
        }
    }

    [Fact]
    public void SpecialIdMarksLegendary()
    {
        var f = new Fixture();
        var bag = f.Bag(0, 2);
        f.Lua.SetIntKeyTable(bag, 0, f.Item("1_a", 1, 1, 1, specialId: 112333));
        f.Lua.SetIntKeyTable(bag, 1, f.Item("2_b", 2, 1, 1));

        var items = f.Reader().Read()!;

        Assert.True(items[0].Legendary);
        Assert.False(items[1].Legendary);
    }

    [Fact]
    public void ReadsEveryBagAndSlot()
    {
        var f = new Fixture();
        var equipment = f.Bag(0, 2);
        f.Lua.SetIntKeyTable(equipment, 0, f.Item("1_a", 1, 1, 1));
        f.Lua.SetIntKeyTable(equipment, 1, f.Item("2_b", 2, 1, 1));
        f.Lua.SetIntKeyTable(equipment, 2, f.Item("3_c", 3, 1, 1));
        var commodities = f.Bag(2, 2);
        f.Lua.SetIntKeyTable(commodities, 0, f.Item("9_x", 9, 177, 1000));
        f.Lua.SetIntKeyTable(commodities, 1, f.Item("8_y", 8, 6, 50));
        f.Lua.SetIntKeyTable(commodities, 2, f.Item("7_z", 7, 2, 50));

        var items = f.Reader().Read()!;

        Assert.Equal(6, items.Length);
        Assert.Equal(new InventoryItem(InventoryBag.Equipment, 0, "1_a", 1, 10, 2, 3, 1, 1, 7), items[0]);
        Assert.Equal([0, 1, 2], items.Where(i => i.Bag == InventoryBag.Equipment).Select(i => i.Slot));
        Assert.Equal(new InventoryItem(InventoryBag.Commodities, 0, "9_x", 9, 10, 2, 3, 177, 1000, 7), items[3]);
        Assert.Equal([177, 6, 2], items.Where(i => i.Bag == InventoryBag.Commodities).Select(i => i.Count));
        Assert.Equal(new InventoryItem(InventoryBag.Commodities, 2, "7_z", 7, 10, 2, 3, 2, 50, 7), items[5]);
    }

    [Fact]
    public void ReadsShapeAOnTheRawRecord()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeA(f.LastRecord, [10, 11], [20]);
        var item = f.OnlyItem(entry);
        Assert.Equal([10, 11], item.PrefixIds);
        Assert.Equal([20], item.SuffixIds);
    }

    [Fact]
    public void ReadsShapeAOnTheEntryTable()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeA(entry, [10], [20, 21]);
        var item = f.OnlyItem(entry);
        Assert.Equal([10], item.PrefixIds);
        Assert.Equal([20, 21], item.SuffixIds);
    }

    [Fact]
    public void ReadsShapeBFromAffixList()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeB(f.LastRecord, [30], [40, 41]);
        var item = f.OnlyItem(entry);
        Assert.Equal([30], item.PrefixIds);
        Assert.Equal([40, 41], item.SuffixIds);
    }

    [Fact]
    public void ReadsShapeBOnTheEntryTable()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeB(entry, [30], []);
        var item = f.OnlyItem(entry);
        Assert.Equal([30], item.PrefixIds);
        Assert.Equal([], item.SuffixIds);
    }

    [Fact]
    public void ShapeAWinsWhenBothExist()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeB(f.LastRecord, [30], [40]);
        f.ShapeA(f.LastRecord, [10], [20]);
        var item = f.OnlyItem(entry);
        Assert.Equal([10], item.PrefixIds);
        Assert.Equal([20], item.SuffixIds);
    }

    [Fact]
    public void NoAffixTablesGivesEmptyArrays()
    {
        var f = new Fixture();
        var item = f.OnlyItem(f.Item("1_a", 1, 1, 1));
        Assert.Empty(item.PrefixIds);
        Assert.Empty(item.SuffixIds);
    }

    [Fact]
    public void BadAffixRowIsSkipped()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        f.ShapeA(f.LastRecord, [10, 11], [20], badFirst: true);
        var item = f.OnlyItem(entry);
        Assert.Equal([10, 11], item.PrefixIds);
        Assert.Equal([20], item.SuffixIds);
    }

    [Fact]
    public void CapsPerSide()
    {
        var f = new Fixture();
        var entry = f.Item("1_a", 1, 1, 1);
        var many = Enumerable.Range(1, Offsets.MaxGroundAffixes + 5).ToArray();
        f.ShapeA(f.LastRecord, many, many);
        var item = f.OnlyItem(entry);
        Assert.Equal(many.Take(Offsets.MaxGroundAffixes), item.PrefixIds);
        Assert.Equal(many.Take(Offsets.MaxGroundAffixes), item.SuffixIds);
    }

    [Fact]
    public void SlotZeroInTheNodePartIsReturned()
    {
        var f = new Fixture();
        var other = f.Bag(3, 1);
        f.Lua.SetIntKeyTable(other, 0, f.Item("5_a", 5, 4, 9));

        var item = Assert.Single(f.Reader().Read()!);

        Assert.Equal((InventoryBag.Other, 0, 4), (item.Bag, item.Slot, item.Count));
    }

    [Fact]
    public void ArrayHolesAreSkipped()
    {
        var f = new Fixture();
        var skills = f.Bag(1, 5);
        f.Lua.SetIntKeyTable(skills, 2, f.Item("5_a", 5, 1, 1));
        f.Lua.SetIntKeyTable(skills, 5, f.Item("6_a", 6, 1, 1));

        Assert.Equal([2, 5], f.Reader().Read()!.Select(i => i.Slot));
    }

    [Fact]
    public void FillerTablesWithoutARecordAreSkipped()
    {
        var f = new Fixture();
        var equipment = f.Bag(0, 4);
        f.Lua.SetIntKeyTable(equipment, 1, f.Lua.NewTable(0, 0));
        f.Lua.SetIntKeyTable(equipment, 2, f.Item("5_a", 5, 1, 1));
        f.Lua.SetIntKeyTable(equipment, 3, f.Lua.NewTable(0, 0));

        Assert.Equal(2, Assert.Single(f.Reader().Read()!).Slot);
    }

    [Fact]
    public void AnItemMissingAFieldIsSkippedOthersPublish()
    {
        var f = new Fixture();
        var commodities = f.Bag(2, 3);
        f.Lua.SetIntKeyTable(commodities, 1, f.Item("5_a", 5, 1, 1, withCount: false));
        f.Lua.SetIntKeyTable(commodities, 2, f.Item("6_a", 6, 3, 9));
        f.Lua.SetIntKeyTable(commodities, 3, f.Item("7_a", 7, 0, 9));

        Assert.Equal("6_a", Assert.Single(f.Reader().Read()!).Id);
    }

    [Fact]
    public void ThrottlesToTheRefreshInterval()
    {
        var f = new Fixture();
        var commodities = f.Bag(2, 1);
        f.Lua.SetIntKeyTable(commodities, 1, f.Item("5_a", 5, 1, 1));
        var reader = f.Reader();
        var first = reader.Read();

        f.Lua.SetIntKeyTable(commodities, 0, f.Item("6_a", 6, 1, 1));
        f.Now = Stopwatch.Frequency * Offsets.InventoryRefreshMilliseconds / 1000 - 1;
        Assert.Same(first, reader.Read());

        f.Now += 1;
        Assert.Equal(2, reader.Read()!.Length);
    }

    [Fact]
    public void NoLuaStateIsNull()
    {
        var f = new Fixture();
        f.Lua.BreakState();
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void NoItemMgrIsNull() => Assert.Null(new Fixture(withManager: false).Reader().Read());

    [Fact]
    public void ZeroGlobalRvaIsNull() => Assert.Null(new InventoryReader(new FakeMemory(), Module, () => 0).Read());

    [Fact]
    public void EmptyBagsAreAnEmptyArrayNotNull()
    {
        var f = new Fixture();
        f.Bag(0, 4);
        Assert.Empty(f.Reader().Read()!);
    }

    [Fact]
    public void AFailedRefreshIsNotServedStale()
    {
        var f = new Fixture();
        var commodities = f.Bag(2, 1);
        f.Lua.SetIntKeyTable(commodities, 1, f.Item("5_a", 5, 1, 1));
        var reader = f.Reader();
        Assert.NotNull(reader.Read());

        f.Lua.BreakState();
        f.Now = Stopwatch.Frequency;
        Assert.Null(reader.Read());
    }

    [Fact]
    public void TheCapKeepsTheFirstItemsByBagThenSlot()
    {
        var f = new Fixture();
        var equipment = f.Bag(0, 600);
        for (var slot = 1; slot <= 600; slot++) f.Lua.SetIntKeyTable(equipment, slot, f.Item($"1_{slot}", 1, 1, 1));
        var commodities = f.Bag(2, 450);
        for (var slot = 1; slot <= 450; slot++) f.Lua.SetIntKeyTable(commodities, slot, f.Item($"2_{slot}", 2, 1, 1));

        var items = f.Reader().Read()!;

        Assert.Equal(Offsets.MaxInventoryItems, items.Length);
        Assert.Equal(600, items.Count(i => i.Bag == InventoryBag.Equipment));
        Assert.Equal(Offsets.MaxInventoryItems - 600, items.Count(i => i.Bag == InventoryBag.Commodities));
        Assert.Equal(Offsets.MaxInventoryItems - 600, items[^1].Slot);
    }

    [Fact]
    public void FillerTablesAreBoundedByTheAttemptCap()
    {
        var f = new Fixture();
        var equipment = f.Bag(0, 5000);
        var filler = f.Lua.NewTable(0, 0);
        for (var slot = 1; slot <= 5000; slot++) f.Lua.SetIntKeyTable(equipment, slot, filler);
        f.Memory.Reads.Clear();

        Assert.Empty(f.Reader().Read()!);
        Assert.True(f.Memory.Reads.Count < Offsets.MaxInventoryItems * 4, $"{f.Memory.Reads.Count} reads");
    }
}
