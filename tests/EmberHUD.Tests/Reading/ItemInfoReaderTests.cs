using EmberHUD.Host;

namespace EmberHUD.Tests;

public class ItemInfoReaderTests
{
    private static ItemInfoReader ReaderFor(FakeWorld world) => new(world.Memory, FakeWorld.Module);

    [Fact]
    public void ReadsTheBaseRow()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(200029, Rarity: 3, Type1: 5, Type2: 6, Type3: 7, Type4: 8, Tier: 2, RequireLevel: 40), new FakeItemRow(5080, Rarity: 2));
        var reader = ReaderFor(world);

        Assert.Equal(new ItemInfo(200029, 3, 5, 6, 7, 8, 2, 40, false), reader.ByBase(200029));
        Assert.Equal(2, reader.ByBase(5080)?.Rarity);
        Assert.Null(reader.ByBase(1));
    }

    [Fact]
    public void NullRowAndWrongIdRowAreSkipped()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(10, NoRow: true), new FakeItemRow(11, StoredId: 999));
        var reader = ReaderFor(world);

        Assert.Null(reader.ByBase(10));
        Assert.Null(reader.ByBase(11));
    }

    [Fact]
    public void UnloadedRowIsNotCachedAndLoadsLater()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(10, NoRow: true));
        var reader = ReaderFor(world);
        Assert.Null(reader.ByBase(10));

        world.SetItemBaseTable(7, new FakeItemRow(10, Rarity: 4));
        Assert.Equal(4, reader.ByBase(10)?.Rarity);
    }

    [Fact]
    public void FoundRowsAreCached()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(10, Rarity: 4));
        var reader = ReaderFor(world);
        Assert.Equal(4, reader.ByBase(10)?.Rarity);

        world.SetItemBaseTable(7);
        Assert.Equal(4, reader.ByBase(10)?.Rarity);
    }

    [Fact]
    public void UniqueGoesThroughItemGoldToTheBaseRow()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(3610, Rarity: 1, Type1: 1, Tier: 6, RequireLevel: 60));
        world.SetItemGoldTable(5, (112258, 3610), (112259, 77));
        var reader = ReaderFor(world);

        Assert.Equal(new ItemInfo(3610, 1, 1, 0, 0, 0, 6, 60, true), reader.ByUnique(112258));
        Assert.Null(reader.ByUnique(112259));
        Assert.Null(reader.ByUnique(5));
        Assert.True(reader.IsUnique(112259));
        Assert.False(reader.IsUnique(5));
    }

    [Fact]
    public void BrokenOrOversizedTablesGiveNothing()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(10));
        world.Memory.Write(FakeWorld.Module + Offsets.ItemBaseBucketCountRva, (uint)Offsets.MaxBuckets + 1);
        Assert.Null(ReaderFor(world).ByBase(10));

        world.SetItemBaseTable(7, new FakeItemRow(10));
        world.Memory.Write(FakeWorld.Module + Offsets.ItemBaseBucketsRva, 0L);
        Assert.Null(ReaderFor(world).ByBase(10));
    }
}
