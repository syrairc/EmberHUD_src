using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderItemMenuTests
{
    static readonly Vector2 Size = new(100, 40);

    sealed class Scene
    {
        public FakeWorld World = new();
        public FakeUi Ui = null!;
        public long Now;
        public int Next = 1;
    }

    sealed record Popup(nint Tip, nint Handle);

    static Scene Open()
    {
        var scene = new Scene();
        scene.Ui = new FakeUi(scene.World);
        return scene;
    }

    static GameUiReader Reader(Scene scene) => scene.World.UiReader(() => scene.Now);

    static ItemMenu? Read(GameUiReader reader, Scene scene)
    {
        var menu = reader.ReadItemMenu();
        scene.Now += Offsets.ItemMenuRefreshMilliseconds;
        return menu;
    }

    static nint Button(Scene scene, string cls, string name, Vector2? at, nint owner, int offset, byte visibility = 4)
    {
        var button = scene.Ui.Widget(scene.Ui.UserClass(cls), name, at ?? Vector2.Zero, at is null ? Vector2.Zero : Size, visibility);
        scene.World.Memory.Write(owner + offset, button);
        return button;
    }

    static Popup AddPopup(Scene scene, Vector2? takeOut, Vector2? lockAt, Vector2? more, Vector2? priceCheck, string handleClass = "TipsNewHandleItem_C")
    {
        var ui = scene.Ui;
        var n = scene.Next++;
        var tip = ui.Widget(ui.UserClass(Offsets.ItemMenuTipClass), Offsets.ItemMenuTipClass + "_" + n, Vector2.Zero, new Vector2(300, 400));
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "CanvasPanel_0", Vector2.Zero, new Vector2(300, 400));
        var hand = ui.Widget(ui.PanelClass("SizeBox"), "HandWidget", Vector2.Zero, new Vector2(300, 80));
        var handle = ui.Widget(ui.UserClass(handleClass), handleClass + "_" + n, Vector2.Zero, new Vector2(300, 80));
        ui.SetTree(tip, root);
        ui.SetChildren(hand, handle);
        scene.World.Memory.Write(tip + Offsets.ItemMenuHandWidget, hand);
        Button(scene, "UIButton", "CommonButtonItem_Equip", takeOut, handle, Offsets.ItemMenuTakeOut);
        Button(scene, "UIButton", "UIButton_Lock", lockAt, handle, Offsets.ItemMenuLock);
        Button(scene, "UIButton", "UIButton_More", more, handle, Offsets.ItemMenuMore);
        var price = ui.Widget(ui.UserClass("TipTradeQueryButton_C"), Offsets.ItemMenuPriceCheck, priceCheck ?? Vector2.Zero, priceCheck is null ? Vector2.Zero : Size);
        ui.SetChildren(root, hand, price);
        return new Popup(tip, handle);
    }

    static nint AddMoreButton(Scene scene, Vector2? at) =>
        scene.Ui.Widget(scene.Ui.UserClass(Offsets.ItemMenuMoreButtonClass), Offsets.ItemMenuMoreButtonClass + "_" + scene.Next++, at ?? Vector2.Zero, at is null ? Vector2.Zero : Size);

    static ScreenRect At(Vector2 min) => new(min, min + Size);

    [Fact]
    public void ThreeVisibleButtonsGiveThreeRects()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), new(2096, 829), new(2167, 829), null);
        var menu = Read(Reader(scene), scene)!;
        Assert.Equal(At(new(2238, 829)), menu.TakeOut);
        Assert.Equal(At(new(2096, 829)), menu.Lock);
        Assert.Equal(At(new(2167, 829)), menu.More);
        Assert.Null(menu.PriceCheck);
        Assert.Null(menu.DropSource);
        Assert.Null(menu.Details);
        Assert.Null(menu.Share);
    }

    [Fact]
    public void RectsSubtractTheWindowOrigin()
    {
        var scene = new Scene();
        scene.Ui = new FakeUi(scene.World, new Vector2(100, 50));
        AddPopup(scene, new(2238, 829), null, null, null);
        Assert.Equal(At(new(2138, 779)), Read(Reader(scene), scene)!.TakeOut);
    }

    [Fact]
    public void AHiddenButtonReadsNullForThatFieldOnly()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, new(2167, 829), null);
        var menu = Read(Reader(scene), scene)!;
        Assert.Equal(At(new(2238, 829)), menu.TakeOut);
        Assert.Null(menu.Lock);
        Assert.Equal(At(new(2167, 829)), menu.More);
    }

    [Fact]
    public void AnOffScreenButtonReadsNull()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), new(-500, 829), new(2167, 4000), null);
        var menu = Read(Reader(scene), scene)!;
        Assert.NotNull(menu.TakeOut);
        Assert.Null(menu.Lock);
        Assert.Null(menu.More);
    }

    [Fact]
    public void PriceCheckIsReadFromTheTooltipItself()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, new(2280, 382));
        Assert.Equal(At(new(2280, 382)), Read(Reader(scene), scene)!.PriceCheck);
    }

    [Fact]
    public void ARectIsReReadFromTheSlateEveryTime()
    {
        var scene = Open();
        var popup = AddPopup(scene, new(2238, 829), null, null, null);
        var reader = Reader(scene);
        Assert.Equal(At(new(2238, 829)), Read(reader, scene)!.TakeOut);
        scene.World.Memory.TryReadPtr(popup.Handle + Offsets.ItemMenuTakeOut, out var button);
        scene.Ui.WeakPtr(button + Offsets.UWidgetSlate, scene.Ui.Slate(new Vector2(2000, 700), Size));
        Assert.Equal(At(new(2000, 700)), Read(reader, scene)!.TakeOut);
    }

    [Fact]
    public void AStaleHandleItemNotUnderTheLiveTooltipIsIgnored()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, null);
        var stale = scene.Ui.Widget(scene.Ui.UserClass(Offsets.ItemMenuHandleClass), "TipsNewHandleItem_C_99", Vector2.Zero, Size);
        Button(scene, "UIButton", "UIButton_Lock", new(10, 10), stale, Offsets.ItemMenuLock);
        var menu = Read(Reader(scene), scene)!;
        Assert.Equal(At(new(2238, 829)), menu.TakeOut);
        Assert.Null(menu.Lock);
    }

    [Fact]
    public void ASlotContentThatIsNotTheHandleClassGivesNoButtons()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), new(2096, 829), new(2167, 829), null, handleClass: "SomethingElse_C");
        Assert.Null(Read(Reader(scene), scene));
    }

    [Fact]
    public void NoPopupIsNull()
    {
        var scene = Open();
        Assert.Null(Read(Reader(scene), scene));
        AddPopup(scene, null, null, null, null);
        Assert.Null(Read(Reader(scene), scene));
    }

    [Fact]
    public void ADefaultObjectTooltipIsNotAPopup()
    {
        var scene = Open();
        var popup = AddPopup(scene, new(2238, 829), null, null, null);
        scene.World.Memory.Write(popup.Tip + Offsets.UObjectName, scene.World.AddName(Offsets.DefaultObjectPrefix + "TipWeaponItem_C"));
        Assert.Null(Read(Reader(scene), scene));
    }

    [Fact]
    public void TheMoreRowIsFoundByClassAndLabelledByPosition()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), new(2096, 829), new(2167, 829), null);
        AddMoreButton(scene, new(2091, 768));
        AddMoreButton(scene, null);
        AddMoreButton(scene, new(2225, 721));
        AddMoreButton(scene, new(2091, 721));
        var menu = Read(Reader(scene), scene)!;
        Assert.Equal(At(new(2091, 721)), menu.DropSource);
        Assert.Equal(At(new(2225, 721)), menu.Details);
        Assert.Equal(At(new(2091, 768)), menu.Share);
    }

    [Fact]
    public void MoreRowButtonsOffScreenAreDropped()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, null);
        AddMoreButton(scene, new(-900, 721));
        AddMoreButton(scene, new(2225, 721));
        var menu = Read(Reader(scene), scene)!;
        Assert.Equal(At(new(2225, 721)), menu.DropSource);
        Assert.Null(menu.Details);
        Assert.Null(menu.Share);
    }

    [Fact]
    public void MoreThanThreeVisibleMoreButtonsAreAmbiguousSoNone()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, null);
        foreach (var x in (int[])[2091, 2225, 2359, 2493]) AddMoreButton(scene, new(x, 721));
        var menu = Read(Reader(scene), scene)!;
        Assert.NotNull(menu.TakeOut);
        Assert.Null(menu.DropSource);
        Assert.Null(menu.Details);
        Assert.Null(menu.Share);
    }

    [Fact]
    public void AMoreRowWithoutAPopupIsNull()
    {
        var scene = Open();
        AddMoreButton(scene, new(2091, 721));
        Assert.Null(Read(Reader(scene), scene));
    }

    [Fact]
    public void SeveralPopupsWithNoSingleOnScreenWinnerAreNull()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, null);
        AddPopup(scene, new(1000, 300), null, null, null);
        Assert.Null(Read(Reader(scene), scene));
    }

    [Fact]
    public void AnOffScreenPopupLosesToTheOnScreenOne()
    {
        var scene = Open();
        AddPopup(scene, new(-800, 829), null, null, null);
        AddPopup(scene, new(2238, 829), null, null, null);
        Assert.Equal(At(new(2238, 829)), Read(Reader(scene), scene)!.TakeOut);
    }

    [Fact]
    public void ThePopupIsFoundAgainAfterItAppearsLater()
    {
        var scene = Open();
        var reader = Reader(scene);
        Assert.Null(Read(reader, scene));
        AddPopup(scene, new(2238, 829), null, null, null);
        scene.Now += Offsets.ItemMenuScanMilliseconds;
        Assert.NotNull(Read(reader, scene));
    }

    [Fact]
    public void LiveReaderIsIdleUntilThePluginAsks()
    {
        var scene = Open();
        AddPopup(scene, new(2238, 829), null, null, null);
        scene.World.PublishEntities();
        Game.ItemMenuAsked = 0;
        var live = new LiveReader(scene.World.Memory, FakeWorld.Module);
        Assert.True(live.TryRead(out var state, out _));
        Assert.Null(state.ItemMenu);
        Game.WantItemMenu();
        try
        {
            Assert.True(live.TryRead(out state, out _));
            Assert.Equal(At(new(2238, 829)), state.ItemMenu!.TakeOut);
        }
        finally { Game.ItemMenuAsked = 0; }
    }
}
