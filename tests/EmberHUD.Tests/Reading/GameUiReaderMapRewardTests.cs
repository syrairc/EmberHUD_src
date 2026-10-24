using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderMapRewardTests
{
    internal sealed class Screen
    {
        public required FakeWorld World;
        public required FakeUi Ui;
        public required nint Page;
        public required nint PageRoot;
        public required nint Drops;
        public required nint Stage;
        public long Now;
    }

    static readonly Vector2 ClipMin = new(1000, 300);
    static readonly Vector2 ClipMax = new(1900, 500);
    const string MemoryPath = "/Game/Art/UI/Textures/Common/Icon/FateCard/128/UI_Memory_6002_Icon_128.UI_Memory_6002_Icon";
    const string TicketPath = "/Game/Art/UI/Textures/Common/Icon/Ticket/128/UI_ShatteredStar_Lightning_Icon_128.UI_ShatteredStar_Lightning_Icon";

    internal static Screen Open()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var size = new Vector2(3440, 1440);
        var page = ui.Widget(ui.UserClass(Offsets.MapRewardPageClassName), Offsets.MapRewardPageClassName + "_1", Vector2.Zero, size);
        world.Memory.Map(page + 0x400, 0x400);
        var pageRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, size);
        var scrolls = Offsets.MapRewardScrolls.Split(' ');
        var drops = ui.Widget(ui.PanelClass("ScrollBox"), scrolls[0], ClipMin, ClipMax - ClipMin);
        var stage = ui.Widget(ui.PanelClass("ScrollBox"), scrolls[1], ClipMin + new Vector2(0, 400), ClipMax - ClipMin);
        ui.SetTree(page, pageRoot);
        ui.SetChildren(pageRoot, drops, stage);
        ui.SetChildren(drops);
        ui.SetChildren(stage);
        ui.AddRoot(page);
        return new Screen { World = world, Ui = ui, Page = page, PageRoot = pageRoot, Drops = drops, Stage = stage };
    }

    internal static nint Item(Screen screen, string iconPath, string? count, float x, float y, uint pathNumber = 0, byte visibility = 4)
    {
        var world = screen.World;
        var ui = screen.Ui;
        var item = ui.Widget(ui.UserClass("GoodSmallBaseItem_C"), "GoodSmallBaseItem_C_1", new Vector2(x, y), new Vector2(94, 94), visibility);
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, new Vector2(94, 94));
        var icon = ui.Widget(world.AddClass("UIImage"), "UIImage_Icon", new Vector2(x, y), new Vector2(94, 94));
        world.Memory.Write(item + Offsets.MapRewardItemIcon, icon);
        world.Memory.Write(icon + Offsets.MapRewardIconPath, world.AddName(iconPath));
        world.Memory.Write(icon + Offsets.MapRewardIconPath + Offsets.FNameNumber, pathNumber);
        ui.SetTree(item, root);
        if (count is null) ui.SetChildren(root, icon);
        else
        {
            var number = ui.Widget(world.AddClass("UITextBlock"), Offsets.MapRewardCountChild, Vector2.Zero, new Vector2(30, 20));
            ui.Text(number, Offsets.TextBlockText, count);
            ui.SetChildren(root, icon, number);
        }
        return item;
    }

    static nint Ptr(Screen screen, nint at) => screen.World.Memory.TryReadPtr(at, out var value) ? value : 0;

    static ItemInfoReader Table(FakeWorld world, params FakeItemRow[] rows)
    {
        world.SetItemBaseTable(7, rows);
        return new ItemInfoReader(world.Memory, FakeWorld.Module);
    }

    static MapRewardView? Tick(GameUiReader reader, Screen screen)
    {
        reader.Read();
        var view = reader.ReadMapReward();
        screen.Now += Offsets.BagRefreshMilliseconds;
        return view;
    }

    static GameUiReader Reader(Screen screen, ItemInfoReader? items = null) => screen.World.UiReader(() => screen.Now, items);

    [Fact]
    public void MemoryIconResolvesFromTheNameWithoutTheItemTable()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath.Replace("6002", "6216"), "1", 1010, 310), Item(screen, MemoryPath, "4", 1110, 310));
        var view = Tick(Reader(screen), screen)!;
        Assert.Equal([(6216, 1), (6002, 4)], view.Items.Select(i => (i.BaseId, i.Count)));
        Assert.Equal((new Vector2(1010, 310), new Vector2(1104, 404)), (view.Items[0].Min, view.Items[0].Max));
    }

    [Fact]
    public void NamedIconResolvesThroughByIcon()
    {
        var screen = Open();
        var items = Table(screen.World, new FakeItemRow(500123, Icon: "UI_ShatteredStar_Lightning_Icon_128"));
        screen.Ui.SetChildren(screen.Stage, Item(screen, TicketPath, "2", 1010, 710));
        var view = Tick(Reader(screen, items), screen)!;
        Assert.Equal([(500123, 2)], view.Items.Select(i => (i.BaseId, i.Count)));
    }

    [Fact]
    public void SharedOrUnknownIconIsDropped()
    {
        var screen = Open();
        var items = Table(screen.World, new FakeItemRow(4522, Icon: "Icon_Equip_Amulet_Norm_002_03_128"), new FakeItemRow(4528, Icon: "Icon_Equip_Amulet_Norm_002_03_128"));
        screen.Ui.SetChildren(screen.Drops,
            Item(screen, "/Game/Art/UI/Icon/Icon_Equip_Amulet_Norm_002_03_128.Icon_Equip_Amulet_Norm_002_03", "1", 1010, 310),
            Item(screen, "/Game/Art/UI/Icon/Nobody_Icon_128.Nobody_Icon", "1", 1110, 310),
            Item(screen, MemoryPath, "1", 1210, 310));
        var view = Tick(Reader(screen, items), screen)!;
        Assert.Equal([6002], view.Items.Select(i => i.BaseId));
    }

    [Fact]
    public void NamedIconWithoutAnItemTableIsDropped()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Stage, Item(screen, TicketPath, "2", 1010, 710));
        Assert.Null(Tick(Reader(screen), screen));
    }

    [Fact]
    public void ClosedPageIsNull()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath, "1", 1010, 310));
        var reader = Reader(screen);
        Assert.NotNull(Tick(reader, screen));
        screen.World.Memory.Write(screen.Page + Offsets.UWidgetVisibility, (byte)1);
        Assert.NotNull(Tick(reader, screen));
        Assert.Null(Tick(reader, screen));
    }

    [Fact]
    public void NoPageIsNull()
    {
        var world = new FakeWorld();
        var reader = world.UiReader();
        reader.Read();
        Assert.Null(reader.ReadMapReward());
    }

    [Fact]
    public void PageWithoutItemsIsNull()
    {
        var screen = Open();
        Assert.Null(Tick(Reader(screen), screen));
    }

    [Fact]
    public void PageRebuiltBetweenReadsDoesNotKeepOldItems()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath.Replace("6002", "6216"), "1", 1010, 310), Item(screen, MemoryPath, "1", 1110, 310));
        var reader = Reader(screen);
        Assert.Equal([6216, 6002], Tick(reader, screen)!.Items.Select(i => i.BaseId));
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath.Replace("6002", "6300"), "5", 1010, 310));
        var view = Tick(reader, screen)!;
        Assert.Equal([(6300, 5)], view.Items.Select(i => (i.BaseId, i.Count)));
    }

    [Fact]
    public void NullResourceObjectStillResolvesFromSoftPath()
    {
        var screen = Open();
        var item = Item(screen, MemoryPath, "1", 1010, 310);
        var icon = Ptr(screen, item + Offsets.MapRewardItemIcon);
        Assert.Equal(0, Ptr(screen, icon + 0x118 + 0x50));
        screen.Ui.SetChildren(screen.Drops, item);
        Assert.Equal([6002], Tick(Reader(screen), screen)!.Items.Select(i => i.BaseId));
    }

    [Fact]
    public void FNameNumberOnTheObjectPartDoesNotChangeTheFileName()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath, "1", 1010, 310, pathNumber: 129));
        Assert.Equal([6002], Tick(Reader(screen), screen)!.Items.Select(i => i.BaseId));
    }

    [Fact]
    public void FNameWithoutObjectPartTakesItsNumberSuffix()
    {
        var screen = Open();
        var items = Table(screen.World, new FakeItemRow(400028, Icon: "UI_BeaconIcon_Light_Icon_128"));
        screen.Ui.SetChildren(screen.Drops, Item(screen, "/Game/Art/UI/Icon/UI_BeaconIcon_Light_Icon", "1", 1010, 310, pathNumber: 129));
        Assert.Equal([400028], Tick(Reader(screen, items), screen)!.Items.Select(i => i.BaseId));
    }

    [Fact]
    public void ItemOutsideClipIsDropped()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath, "1", 1010, 310), Item(screen, MemoryPath, "1", 1960, 310), Item(screen, MemoryPath, "1", 1010, 520));
        Assert.Equal([new Vector2(1010, 310)], Tick(Reader(screen), screen)!.Items.Select(i => i.Min));
    }

    [Fact]
    public void CountComesFromTheCountSource()
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath, "37", 1010, 310));
        Assert.Equal(37, Tick(Reader(screen), screen)!.Items.Single().Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("x")]
    public void UnreadableOrNonPositiveCountDropsTheItem(string? count)
    {
        var screen = Open();
        screen.Ui.SetChildren(screen.Drops, Item(screen, MemoryPath, count, 1010, 310), Item(screen, MemoryPath, "2", 1110, 310));
        Assert.Equal([new Vector2(1110, 310)], Tick(Reader(screen), screen)!.Items.Select(i => i.Min));
    }

    [Fact]
    public void UnlockRequirementIconsAreNotRead()
    {
        var screen = Open();
        var require = Item(screen, MemoryPath, "1", 100, 100);
        screen.World.Memory.Write(screen.Page + 0x460, require);
        Assert.Null(Tick(Reader(screen), screen));
    }

    [Fact]
    public void ItemWithGarbagePathIsSkipped()
    {
        var screen = Open();
        var bad = Item(screen, MemoryPath, "1", 1010, 310);
        screen.World.Memory.Write(Ptr(screen, bad + Offsets.MapRewardItemIcon) + Offsets.MapRewardIconPath, 0x7FFFFFu);
        screen.Ui.SetChildren(screen.Drops, bad, Item(screen, MemoryPath, "1", 1110, 310));
        Assert.Equal([new Vector2(1110, 310)], Tick(Reader(screen), screen)!.Items.Select(i => i.Min));
    }
}
