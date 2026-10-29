using EmberHUD.Host;
using EmberHUD.Host.Reading;
using EmberHUD.Tests.Reading;

namespace EmberHUD.Tests;

public class BuffCatalogTests
{
    private static readonly BuffTableRvas Rvas = new(0x2000, 0x2008, 0x2010, 0x2018);
    private const int Unloaded = 9999;

    private static (FakeWorld World, BuffCatalog Catalog) Make()
    {
        var world = new FakeWorld();
        return (world, new BuffCatalog(world.Memory, FakeWorld.Module, Rvas));
    }

    [Fact]
    public void ReadsEveryFieldOffTheRow()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, ("char_buff|name|15010_0", "Bull Rage"));
        var row = world.AddBuffRow(15010, new FakeWorld.BuffRowSpec("char_buff|name|15010_0", Type: 3, Propensity: true, MaxStacks: 5, Tick: 0.5, MaxDuration: 12), "UI_Buff");

        var info = catalog.Read(15010, row)!.Value;

        Assert.Equal(new BuffInfo(15010, "UI_Buff", "Bull Rage", BuffKind.Control, true, 5, 0.5, 12), info);
    }

    [Fact]
    public void NameFallsBackToTheModeZeroKeyWhenTheRowKeyIsEmpty()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 400 + Offsets.BuffNameSuffix, "Affliction"));

        Assert.Equal("Affliction", catalog.Read(400, world.AddBuffRow(400))!.Value.Name);
    }

    [Fact]
    public void ARowKeyWithAnotherModeIsUsedAsIs()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, ("char_buff|name|50_3", "Mode three"));

        Assert.Equal("Mode three", catalog.Read(50, world.AddBuffRow(50, new("char_buff|name|50_3")))!.Value.Name);
    }

    [Fact]
    public void ARowWithAnotherIdIsRejected()
    {
        var (world, catalog) = Make();

        Assert.Null(catalog.Read(1, world.AddBuffRow(2)));
    }

    [Fact]
    public void AGarbageKindIsNone()
    {
        var (world, catalog) = Make();

        Assert.Equal(BuffKind.None, catalog.Read(1, world.AddBuffRow(1, new(Type: 77)))!.Value.Kind);
    }

    [Fact]
    public void ANonFiniteTickRejectsTheRow()
    {
        var (world, catalog) = Make();

        Assert.Null(catalog.Read(1, world.AddBuffRow(1, new(Tick: double.NaN))));
    }

    [Theory]
    [InlineData(110, CrowdControl.Freeze)]
    [InlineData(300, CrowdControl.Root)]
    [InlineData(310, CrowdControl.Silence)]
    [InlineData(230, CrowdControl.Paralysis)]
    [InlineData(170, CrowdControl.Paralysis)]
    [InlineData(50010, CrowdControl.Fear)]
    [InlineData(100, CrowdControl.Stagger)]
    [InlineData(10000091, CrowdControl.TimeStop)]
    [InlineData(400, CrowdControl.None)]
    public void FixedBuffIdsGiveTheirFlag(int id, CrowdControl expected)
    {
        var (world, catalog) = Make();

        Assert.Equal(expected, catalog.Read(id, world.AddBuffRow(id))!.Value.Control);
    }

    [Fact]
    public void ExplicitStateIdsGiveTheirFlagWithoutAStateTable()
    {
        var (world, catalog) = Make();
        var row = world.AddBuffRow(7, new(States: [4000010, 4000114, 4000550, 4000380, 4000430, 4000440]));

        Assert.Equal(CrowdControl.Stun | CrowdControl.Freeze | CrowdControl.Root | CrowdControl.Silence | CrowdControl.Fear | CrowdControl.TimeStop, catalog.Read(7, row)!.Value.Control);
    }

    [Fact]
    public void StateCategoriesGiveTheMovementAndCastFlags()
    {
        var (world, catalog) = Make();
        world.SetCharStateTable(Rvas, 5, (4000700, [1, 20]), (4000800, [26, 38, 99]));
        var row = world.AddBuffRow(8, new(States: [4000700, 4000800]));

        Assert.Equal(CrowdControl.CantMove | CrowdControl.Frozen | CrowdControl.CantCast | CrowdControl.Immune, catalog.Read(8, row)!.Value.Control);
    }

    [Fact]
    public void TheControlIsComputedOnceWhenEverythingResolved()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 8 + Offsets.BuffNameSuffix, "Held"));
        world.SetCharStateTable(Rvas, 5, (4000700, [1]));
        var row = world.AddBuffRow(8, new(States: [4000700]));
        Assert.Equal(CrowdControl.CantMove, catalog.Read(8, row)!.Value.Control);

        world.SetCharStateTable(Rvas, 5, (4000700, [26]));

        Assert.Equal(CrowdControl.CantMove, catalog.Read(8, row)!.Value.Control);
    }

    [Fact]
    public void ANullStateRowIsRetriedNotCachedAsFinal()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 8 + Offsets.BuffNameSuffix, "Held"));
        world.SetCharStateTable(Rvas, 5, (4000700, null));
        var row = world.AddBuffRow(8, new(States: [4000700, 4000010]));
        Assert.Equal(CrowdControl.Stun, catalog.Read(8, row)!.Value.Control);

        world.SetCharStateTable(Rvas, 5, (4000700, [2]));

        Assert.Equal(CrowdControl.Stun | CrowdControl.CantMove, catalog.Read(8, row)!.Value.Control);
    }

    [Fact]
    public void AMissingNameIsRetriedUntilTheLocTableLoads()
    {
        var (world, catalog) = Make();
        var row = world.AddBuffRow(8);
        Assert.Null(catalog.Read(8, row)!.Value.Name);

        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 8 + Offsets.BuffNameSuffix, "Late"));

        Assert.Equal("Late", catalog.Read(8, row)!.Value.Name);
    }

    [Fact]
    public void AGarbageStateCountIsRejectedAndTheFixedFlagsStay()
    {
        var (world, catalog) = Make();
        var row = world.AddBuffRow(110, new(States: [4000010], StateCount: 1_000_000));

        var info = catalog.Read(110, row)!.Value;

        Assert.Equal(CrowdControl.Freeze, info.Control);
    }

    [Fact]
    public void AGarbageCategoryCountIsRejected()
    {
        var (world, catalog) = Make();
        world.SetCharStateTable(Rvas, 5, (4000700, [1]));
        world.Memory.Write(world.StateRows[4000700] + Offsets.CharStateRowCategory + Offsets.TArrayNum, -4);
        var row = world.AddBuffRow(8, new(States: [4000700]));

        Assert.Equal(CrowdControl.None, catalog.Read(8, row)!.Value.Control);
    }

    [Fact]
    public void AnUnreadableStateArrayDoesNotThrow()
    {
        var (world, catalog) = Make();
        var row = world.AddBuffRow(8, new(States: [1]));
        world.Memory.Write(row + Offsets.BuffCfgRowAddedState, FakeWorld.Unmapped);

        Assert.Equal(CrowdControl.None, catalog.Read(8, row)!.Value.Control);
    }

    [Fact]
    public void GetWalksTheTwoLevelTableForModeZero()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 51110 + Offsets.BuffNameSuffix, "Monster Root"));
        world.SetBuffCfgTable(Rvas, 7, (51110, 0, world.AddBuffRow(51110, new(Type: 3), "UI_Root")), (51110, 1, world.AddBuffRow(51110, new(Type: 4))), (15010, 0, world.AddBuffRow(15010)));

        var info = catalog.Get(51110)!.Value;

        Assert.Equal((BuffKind.Control, "UI_Root", "Monster Root"), (info.Kind, info.Icon, info.Name));
    }

    [Fact]
    public void GetSkipsAnIdThatOnlyHasOtherModes()
    {
        var (world, catalog) = Make();
        world.SetBuffCfgTable(Rvas, 7, (5, 2, world.AddBuffRow(5)));

        Assert.Null(catalog.Get(5));
    }

    [Fact]
    public void GetRetriesAnUnloadedRow()
    {
        var (world, catalog) = Make();
        world.SetBuffCfgTable(Rvas, 7, (Unloaded, 0, 0));
        Assert.Null(catalog.Get(Unloaded));

        world.SetBuffCfgTable(Rvas, 7, (Unloaded, 0, world.AddBuffRow(Unloaded)));

        Assert.Equal(Unloaded, catalog.Get(Unloaded)!.Value.Id);
    }

    [Fact]
    public void GetRejectsAnUnknownIdAndARowWithAnotherId()
    {
        var (world, catalog) = Make();
        world.SetBuffCfgTable(Rvas, 7, (3, 0, world.AddBuffRow(4)));

        Assert.Null(catalog.Get(3));
        Assert.Null(catalog.Get(77));
        Assert.Null(catalog.Get(0));
    }

    [Fact]
    public void GetWithoutTheTablesIsNull()
    {
        var world = new FakeWorld();
        var catalog = new BuffCatalog(world.Memory, FakeWorld.Module, new BuffTableRvas(0, 0, 0, 0));

        Assert.Null(catalog.Get(3));
    }

    [Fact]
    public void AGarbageBucketCountIsRejected()
    {
        var (world, catalog) = Make();
        world.SetBuffCfgTable(Rvas, 7, (3, 0, world.AddBuffRow(3)));
        world.Memory.Write(FakeWorld.Module + Rvas.BuffCount, uint.MaxValue);

        Assert.Null(catalog.Get(3));
    }

    [Fact]
    public void GetServesWhatReadAlreadyLearned()
    {
        var (world, catalog) = Make();
        world.SetLanguageTable(2, 7, (Offsets.BuffNameKey + 8 + Offsets.BuffNameSuffix, "Held"));
        catalog.Read(8, world.AddBuffRow(8, new(Type: 2)));

        Assert.Equal(BuffKind.Ailment, catalog.Get(8)!.Value.Kind);
    }
}
