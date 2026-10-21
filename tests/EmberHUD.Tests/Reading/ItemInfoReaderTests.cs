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
    public void ReadsTheIconString()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(200029, Icon: "UI_Ash_21StonelowZHENGUI_Icon_128"));

        Assert.Equal("UI_Ash_21StonelowZHENGUI_Icon_128", ReaderFor(world).ByBase(200029)?.Icon);
    }

    [Fact]
    public void EmptyMinusOneOversizedOrUnreadableIconGivesNullIconButKeepsTheRow()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(1), new FakeItemRow(2, Icon: ""), new FakeItemRow(3, Icon: "-1"), new FakeItemRow(4, Icon: new string('A', Offsets.MaxIconNameLength + 1)), new FakeItemRow(5, Icon: "X", IconUnreadable: true), new FakeItemRow(6, Icon: "bad\u0001name"));
        var reader = ReaderFor(world);

        foreach (var id in new[] { 1, 2, 3, 4, 5, 6 })
        {
            var info = reader.ByBase(id);
            Assert.Equal(id, info?.BaseId);
            Assert.Null(info?.Icon);
        }
    }

    [Fact]
    public void UniqueIconWinsAndFallsBackToTheBaseIcon()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(56, Icon: "Icon_Equip_Weapon_Claw_Norm_001_128"), new FakeItemRow(57, Icon: "Base57"));
        world.SetItemGoldTable(5, (12, 56), (13, 57));
        world.SetItemGoldIcon(12, "Icon_Equip_Weapon_Wand_Epic_001_128");
        var reader = ReaderFor(world);

        var epic = reader.ByUnique(12);
        Assert.Equal("Icon_Equip_Weapon_Wand_Epic_001_128", epic?.Icon);
        Assert.True(epic?.IsUnique);
        Assert.Equal("Base57", reader.ByUnique(13)?.Icon);
        Assert.Equal("Icon_Equip_Weapon_Claw_Norm_001_128", reader.ByBase(56)?.Icon);
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
        world.SetItemBaseTable(7, new FakeItemRow(10, Rarity: 4, Icon: "I"));
        var reader = ReaderFor(world);
        Assert.Equal(4, reader.ByBase(10)?.Rarity);

        world.SetItemBaseTable(7);
        Assert.Equal(4, reader.ByBase(10)?.Rarity);
    }

    [Fact]
    public void ARowWithoutAnIconIsNotCachedAndTheIconArrivesLater()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(10, Icon: "x", IconUnreadable: true));
        world.SetItemGoldTable(5, (12, 10));
        var reader = ReaderFor(world);
        Assert.Null(reader.ByBase(10)?.Icon);
        Assert.Null(reader.ByUnique(12)?.Icon);

        world.SetItemBaseTable(7, new FakeItemRow(10, Icon: "Late"));
        Assert.Equal("Late", reader.ByBase(10)?.Icon);
        Assert.Equal("Late", reader.ByUnique(12)?.Icon);
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

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    public void EmptyOrMinusOneIconRowIsCached(string icon)
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(261010, Rarity: 2, Icon: icon));
        var reader = ReaderFor(world);
        Assert.Null(reader.ByBase(261010)?.Icon);

        world.SetItemBaseTable(7, new FakeItemRow(261010, Rarity: 9, Icon: "Changed"));

        Assert.Equal(2, reader.ByBase(261010)?.Rarity);
        Assert.Null(reader.ByBase(261010)?.Icon);
    }

    [Fact]
    public void UniqueWithEmptyIconsIsCachedAndAGarbageOneIsNot()
    {
        var world = new FakeWorld();
        world.SetItemBaseTable(7, new FakeItemRow(56, Rarity: 2, Icon: "-1"), new FakeItemRow(57, Icon: "badname"));
        world.SetItemGoldTable(5, (12, 56), (13, 57));
        world.SetItemGoldIcon(12, "-1");
        world.SetItemGoldIcon(13, "");
        var reader = ReaderFor(world);
        Assert.Null(reader.ByUnique(12)?.Icon);
        Assert.Null(reader.ByUnique(13)?.Icon);

        world.SetItemBaseTable(7, new FakeItemRow(56, Rarity: 9, Icon: "Changed"), new FakeItemRow(57, Icon: "Fixed"));

        Assert.Equal(2, reader.ByUnique(12)?.Rarity);
        Assert.Equal("Fixed", reader.ByUnique(13)?.Icon);
    }
}
