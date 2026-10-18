using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderTests
{
    static readonly string[] FirstPanel = Offsets.GamePanels.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(row => row.Split(',', StringSplitOptions.TrimEntries)).Single(row => row[0] == "PCBag_C");
    static string PanelClass => FirstPanel[0];
    static PanelKind PanelKindOf => Enum.Parse<PanelKind>(FirstPanel[1]);
    static string RectChild => FirstPanel[2];

    static (FakeWorld World, FakeUi Ui, nint Page, nint Root, nint Child) OpenNamedPanel(Vector2? origin = null)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world, origin);
        var page = ui.Widget(ui.UserClass(PanelClass), PanelClass + "_1", origin ?? Vector2.Zero, new Vector2(3440, 1440));
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "Root", origin ?? Vector2.Zero, new Vector2(3440, 1440));
        var child = ui.Widget(ui.UserClass("Image"), RectChild, (origin ?? Vector2.Zero) + new Vector2(2400, 200), new Vector2(900, 1000));
        ui.SetTree(page, root);
        ui.SetChildren(root, child);
        ui.AddRoot(ui.Host(page));
        return (world, ui, page, root, child);
    }

    static (FakeWorld World, FakeUi Ui, nint Root) OpenRelicPop(string pageClass = "S16BoardRelicPop_C")
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var page = ui.Widget(ui.UserClass(pageClass), pageClass + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var root = ui.Widget(ui.PanelClass("CanvasPanel"), "Root", Vector2.Zero, new Vector2(3440, 1440));
        ui.SetTree(page, root);
        ui.AddRoot(ui.Host(page));
        return (world, ui, root);
    }

    [Fact]
    public void PanelWithConfiguredPartsReportsTheirRects()
    {
        var (world, ui, root) = OpenRelicPop();
        var first = ui.Widget(ui.UserClass("Image"), "S16BoardRelicOptCell", new Vector2(100, 200), new Vector2(300, 400), nameNumber: 2);
        var confirm = ui.Widget(ui.UserClass("Image"), "Btn_S16Board_RelicPop_Confirm", new Vector2(500, 600), new Vector2(50, 60));
        ui.SetChildren(root, first, confirm);

        var parts = Assert.Single(world.UiReader().Read().Panels).Parts;

        Assert.Equal(["S16BoardRelicOptCell_1", "Btn_S16Board_RelicPop_Confirm"], parts.Select(p => p.Name));
        Assert.Equal(new Vector2(100, 200), parts[0].Min);
        Assert.Equal(new Vector2(400, 600), parts[0].Max);
        Assert.Equal(new Vector2(500, 600), parts[1].Min);
    }

    [Fact]
    public void HiddenPartIsSkipped()
    {
        var (world, ui, root) = OpenRelicPop();
        var shown = ui.Widget(ui.UserClass("Image"), "S16BoardRelicOptCell", new Vector2(100, 200), new Vector2(300, 400), nameNumber: 2);
        var hidden = ui.Widget(ui.UserClass("Image"), "S16BoardRelicOptCell", new Vector2(500, 200), new Vector2(300, 400), visibility: 1, nameNumber: 3);
        ui.SetChildren(root, shown, hidden);

        var parts = Assert.Single(world.UiReader().Read().Panels).Parts;

        Assert.Equal(["S16BoardRelicOptCell_1"], parts.Select(p => p.Name));
    }

    [Fact]
    public void ChildrenTokenListsVisibleChildrenInOrderWithoutGaps()
    {
        var (world, ui, root) = OpenRelicPop("S16BoardMain_C");
        var box = ui.Widget(ui.PanelClass("HorizontalBox"), "List_S16Board_Bottom_Actions", Vector2.Zero, new Vector2(900, 300));
        var a = ui.Widget(ui.UserClass("Image"), "S16BoardActionCell", new Vector2(10, 20), new Vector2(100, 200));
        var hidden = ui.Widget(ui.UserClass("Image"), "S16BoardActionCell", new Vector2(120, 20), new Vector2(100, 200), visibility: 1, nameNumber: 2);
        var c = ui.Widget(ui.UserClass("Image"), "S16BoardActionCell_C_2147463473", new Vector2(230, 20), new Vector2(100, 200));
        ui.SetChildren(box, a, hidden, c);
        ui.SetChildren(root, box);

        var parts = Assert.Single(world.UiReader().Read().Panels).Parts;

        Assert.Equal(["List_S16Board_Bottom_Actions[0]", "List_S16Board_Bottom_Actions[1]"], parts.Select(p => p.Name));
        Assert.Equal(new Vector2(10, 20), parts[0].Min);
        Assert.Equal(new Vector2(230, 20), parts[1].Min);
    }

    [Fact]
    public void ChildrenTokenOfACollapsedBoxGivesNoParts()
    {
        var (world, ui, root) = OpenRelicPop("S16BoardMain_C");
        var box = ui.Widget(ui.PanelClass("HorizontalBox"), "List_S16Board_Bottom_Actions", Vector2.Zero, new Vector2(900, 300), visibility: 1);
        var a = ui.Widget(ui.UserClass("Image"), "S16BoardActionCell", new Vector2(10, 20), new Vector2(100, 200));
        ui.SetChildren(box, a);
        ui.SetChildren(root, box);

        Assert.Empty(Assert.Single(world.UiReader().Read().Panels).Parts);
    }

    [Fact]
    public void ChildrenTokenWithoutTheBoxGivesNoParts()
    {
        var (world, ui, root) = OpenRelicPop("S16BoardMain_C");
        ui.SetChildren(root);

        Assert.Empty(Assert.Single(world.UiReader().Read().Panels).Parts);
    }

    [Fact]
    public void MissingPartsAreSkippedAndThePanelStillReads()
    {
        var (world, ui, root) = OpenRelicPop();
        ui.SetChildren(root);

        var panel = Assert.Single(world.UiReader().Read().Panels);

        Assert.Equal("S16BoardRelicPop_C", panel.Name);
        Assert.Empty(panel.Parts);
    }

    [Fact]
    public void PanelWithoutAnEntryHasNoParts()
    {
        var (world, ui, root) = OpenRelicPop("Other_C");
        ui.SetChildren(root, ui.Widget(ui.UserClass("Image"), "S16BoardRelicOptCell", Vector2.Zero, new Vector2(10, 10), nameNumber: 2));

        Assert.Empty(Assert.Single(world.UiReader().Read().Panels).Parts);
    }

    [Fact]
    public void MissingPartIsSearchedOnceUntilTheRetryIntervalPasses()
    {
        var (world, ui, root) = OpenRelicPop();
        ui.SetChildren(root);
        long now = 0;
        var reader = world.UiReader(() => now);
        reader.Read();

        var before = world.Memory.Reads.Count;
        reader.Read();
        var cached = world.Memory.Reads.Count - before;
        var cell = ui.Widget(ui.UserClass("Image"), "S16BoardRelicOptCell", new Vector2(100, 200), new Vector2(300, 400), nameNumber: 2);
        ui.SetChildren(root, cell);
        Assert.Empty(Assert.Single(reader.Read().Panels).Parts);

        now = Offsets.RectChildRetryMilliseconds;
        Assert.Single(Assert.Single(reader.Read().Panels).Parts);
        before = world.Memory.Reads.Count;
        reader.Read();
        Assert.True(cached > 0 && world.Memory.Reads.Count - before > 0);
    }

    [Fact]
    public void TryReadFailsWithoutAnOverlayAndSucceedsWithRoots()
    {
        Assert.False(new FakeWorld().UiReader().TryRead(out var none));
        Assert.Same(GameUi.Empty, none);

        var world = new FakeWorld();
        var ui = new FakeUi(world);
        Assert.False(world.UiReader().TryRead(out _));
        ui.AddPad([1, 2]);
        Assert.True(world.UiReader().TryRead(out var quiet));
        Assert.Empty(quiet.Panels);
    }

    [Fact]
    public void TryReadFailsWhenNoRootResolves()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddRoot(0);
        Assert.False(world.UiReader().TryRead(out _));
    }

    [Fact]
    public void AnUnresolvedRootStopsCountingAsIncompleteAfterOneRefreshCycle()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddPad([1, 2]);
        ui.AddRoot(0);
        long now = 0;
        var reader = world.UiReader(() => now);
        Assert.False(reader.TryRead(out _));
        now = Offsets.UiRootRefreshMilliseconds - 1;
        Assert.False(reader.TryRead(out _));
        now = Offsets.UiRootRefreshMilliseconds;
        Assert.True(reader.TryRead(out _));
    }

    [Fact]
    public void AKnownChatRootWithAnUnreadablePageVisibilityNeverReadsClean()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddPad([1, 2]);
        var chat = ui.AddChat();
        world.Memory.MakeUnreadable(chat + Offsets.UWidgetVisibility, 1);
        long now = 0;
        var reader = world.UiReader(() => now);
        foreach (var at in new long[] { 0, Offsets.UiRootRefreshMilliseconds - 1, Offsets.UiRootRefreshMilliseconds, 2000 })
        {
            now = at;
            Assert.False(reader.TryRead(out _, out var chatSeen));
            Assert.False(chatSeen);
        }
    }

    [Fact]
    public void AKnownChatRootWithAnUnreadableWidgetVisibilityNeverReadsClean()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddPad([1, 2]);
        ui.AddChat();
        world.Memory.MakeUnreadable(ui.ChatHost + Offsets.UWidgetVisibility, 1);
        long now = 0;
        var reader = world.UiReader(() => now);
        foreach (var at in new long[] { 0, Offsets.UiRootRefreshMilliseconds, 2000 })
        {
            now = at;
            Assert.False(reader.TryRead(out _));
        }
    }

    [Fact]
    public void AnOverlayOutageForgetsEarlierRootFailures()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddPad([1, 2]);
        ui.AddRoot(0);
        long now = 0;
        var reader = world.UiReader(() => now);
        Assert.False(reader.TryRead(out _));
        world.Memory.TryRead(ui.Engine + Offsets.EngineGameViewport, out nint viewport);
        world.Memory.Write(ui.Engine + Offsets.EngineGameViewport, 0L);
        now = Offsets.UiRootRefreshMilliseconds - 100;
        Assert.False(reader.TryRead(out _));
        world.Memory.Write(ui.Engine + Offsets.EngineGameViewport, viewport);
        now = Offsets.UiRootRefreshMilliseconds + 100;
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void ChatPresenceComesFromTheRootWalkEvenWhenTheRectReadFails()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var chat = ui.AddChat();
        world.Memory.Write(chat + Offsets.UWidgetSlate, 0L);
        var reader = world.UiReader();
        Assert.False(reader.TryRead(out var panels, out var chatSeen));
        Assert.True(chatSeen);
        Assert.Empty(panels.Panels);
    }

    [Fact]
    public void TheChatPageIsListedAsAnOtherPanelThatIsNotFullScreen()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddChat();
        Assert.True(world.UiReader().TryRead(out var open));
        var panel = Assert.Single(open.Panels);
        Assert.Equal((Offsets.ChatPageClassName, PanelKind.Other, false), (panel.Name, panel.Kind, panel.FullScreen));
        Assert.False(open.AnyFullScreen);
    }

    [Fact]
    public void HostedNamedPanelReportsKindRectAndFlag()
    {
        var (world, _, page, _, _) = OpenNamedPanel();

        var panel = Assert.Single(world.UiReader().Read().Panels);

        Assert.Equal(PanelClass, panel.Name);
        Assert.Equal(PanelKindOf, panel.Kind);
        Assert.False(panel.FullScreen);
        Assert.Equal(page, panel.Address);
        Assert.Equal(new Vector2(2400, 200), panel.Min);
        Assert.Equal(new Vector2(3300, 1200), panel.Max);
    }

    [Fact]
    public void RectsAreRelativeToTheViewportOverlay()
    {
        var (world, _, _, _, _) = OpenNamedPanel(new Vector2(100, 50));

        var panel = Assert.Single(world.UiReader().Read().Panels);

        Assert.Equal(new Vector2(2400, 200), panel.Min);
    }

    [Fact]
    public void UnknownHostedPageIsOther()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var page = ui.Widget(ui.UserClass("SomethingNew_C"), "SomethingNew_C_1", Vector2.Zero, new Vector2(3440, 1440));
        ui.AddRoot(ui.Host(page));

        var panel = Assert.Single(world.UiReader().Read().Panels);

        Assert.Equal(new GamePanel("SomethingNew_C", PanelKind.Other, Vector2.Zero, new Vector2(3440, 1440), false) { Address = page }, panel);
    }

    [Fact]
    public void PanelsAreFullScreenWhileTheGameHidesItsOwnMinimap()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddRoot(ui.Host(ui.Widget(ui.UserClass("SomethingNew_C"), "SomethingNew_C_1", Vector2.Zero, new Vector2(3440, 1440))));
        var gameMinimap = ui.Widget(ui.UserClass(Offsets.GameMinimapPage), Offsets.GameMinimapPage + "_1", Vector2.Zero, new Vector2(3440, 1440));
        ui.AddRoot(ui.Host(gameMinimap));
        var reader = world.UiReader();

        Assert.False(Assert.Single(reader.Read().Panels).FullScreen);

        world.Memory.Write(gameMinimap + Offsets.UWidgetVisibility, (byte)1);
        Assert.True(Assert.Single(reader.Read().Panels).FullScreen);
    }

    [Fact]
    public void HudRootsHiddenPagesAndEmptyHostsAreSkipped()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddRoot(ui.Widget(ui.UserClass("FightChatView_C"), "FightChatView_C_1", Vector2.Zero, new Vector2(400, 300)));
        ui.AddRoot(ui.Host(ui.Widget(ui.UserClass("Hidden_C"), "Hidden_C_1", Vector2.Zero, new Vector2(10, 10), visibility: 1)));
        ui.AddRoot(ui.Host(0));
        foreach (var hud in Offsets.HudPages.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(1))
            ui.AddRoot(ui.Host(ui.Widget(ui.UserClass(hud), hud + "_1", Vector2.Zero, new Vector2(3440, 1440))));

        Assert.Empty(world.UiReader().Read().Panels);
    }

    [Fact]
    public void ThePageInTwoOverlaySlotsIsListedOnce()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var host = ui.Host(ui.Widget(ui.UserClass("Twice_C"), "Twice_C_1", Vector2.Zero, new Vector2(3440, 1440)));
        ui.AddRoot(host);
        ui.AddRoot(host);

        Assert.Single(world.UiReader().Read().Panels);
    }

    [Fact]
    public void PanelsKeepViewportOrderBackToFront()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        foreach (var name in new[] { "Back_C", "Front_C" })
            ui.AddRoot(ui.Host(ui.Widget(ui.UserClass(name), name + "_1", Vector2.Zero, new Vector2(3440, 1440))));

        Assert.Equal(["Back_C", "Front_C"], world.UiReader().Read().Panels.Select(p => p.Name));
    }

    [Fact]
    public void DeadSlateWidgetDropsThePanel()
    {
        var (world, ui, page, _, child) = OpenNamedPanel();
        var reader = world.UiReader();
        Assert.Single(reader.Read().Panels);

        ui.WeakPtr(page + Offsets.UWidgetSlate, ui.Slate(Vector2.Zero, new Vector2(3440, 1440)), shared: 0);
        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(Vector2.Zero, new Vector2(900, 1000)), shared: 0);
        Assert.Empty(reader.Read().Panels);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(1e9f)]
    [InlineData(-5f)]
    public void BadGeometryIsRejected(float width)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddRoot(ui.Host(ui.Widget(ui.UserClass("Odd_C"), "Odd_C_1", Vector2.Zero, new Vector2(width, 100))));

        Assert.Empty(world.UiReader().Read().Panels);
    }

    [Fact]
    public void BrokenChainGivesEmptyUi()
    {
        var (world, ui, _, _, _) = OpenNamedPanel();
        var reader = world.UiReader();

        world.Memory.Write(ui.Engine + Offsets.EngineGameViewport, 0L);
        Assert.Same(GameUi.Empty, reader.Read());

        world.Memory.Write(FakeWorld.Module + Offsets.GEngineGlobalRva, FakeWorld.Unmapped);
        Assert.Same(GameUi.Empty, reader.Read());
    }

    [Fact]
    public void MoreOverlayRootsThanTheViewportCapIsATornArray()
    {
        var (world, ui, _, _, _) = OpenNamedPanel();
        var slotCount = ui.Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum;
        var reader = world.UiReader();
        Assert.Single(reader.Read().Panels);

        world.Memory.Write(slotCount, Offsets.MaxViewportRoots + 1);
        Assert.Same(GameUi.Empty, reader.Read());

        world.Memory.Write(slotCount, 1);
        Assert.Single(reader.Read().Panels);
    }

    [Fact]
    public void MorePanelContentsThanTheNodeCapAreATornArray()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var cell = ui.Widget(ui.UserClass("Image"), "Cell", new Vector2(2450, 300), new Vector2(80, 80));
        ui.SetChildren(root, child, cell);
        var reader = world.UiReader();
        Assert.NotEqual((nint)0, reader.FindChild(page, "Cell"));

        world.Memory.Write(root + Offsets.PanelSlots + Offsets.TArrayNum, Offsets.MaxWidgetNodes + 1);

        Assert.Equal((nint)0, reader.FindChild(page, "Cell"));
    }

    [Fact]
    public void FindChildWalksNestedPanelsByName()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var grid = ui.Widget(ui.PanelClass("GridPanel"), "Grid", new Vector2(2450, 300), new Vector2(800, 600));
        var cell = ui.Widget(ui.UserClass("Image"), "Cell", new Vector2(2450, 300), new Vector2(80, 80), nameNumber: 1);
        ui.SetChildren(grid, cell);
        ui.SetChildren(root, child, grid);

        Assert.Equal(cell, world.UiReader().FindChild(page, "Cell_0"));
        Assert.Equal((nint)0, world.UiReader().FindChild(page, "Nope"));
    }

    [Fact]
    public void FindChildStopsOnCycles()
    {
        var (world, ui, page, _, _) = OpenNamedPanel();
        var loop = ui.Widget(ui.PanelClass("Loop"), "Loop", Vector2.Zero, new Vector2(10, 10));
        ui.SetChildren(loop, loop);
        ui.SetTree(page, loop);

        Assert.Equal((nint)0, world.UiReader().FindChild(page, "Nope"));
    }

    [Fact]
    public void MissingRectChildIsSearchedOnceUntilTheRetryIntervalPasses()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        ui.SetChildren(root);
        long now = 0;
        var reader = world.UiReader(() => now);

        var before = world.Memory.Reads.Count;
        reader.Read();
        var firstRead = world.Memory.Reads.Count - before;
        before = world.Memory.Reads.Count;
        reader.Read();
        var secondRead = world.Memory.Reads.Count - before;
        now = Offsets.RectChildRetryMilliseconds - 1;
        reader.Read();
        before = world.Memory.Reads.Count;
        reader.Read();
        var thirdRead = world.Memory.Reads.Count - before;
        now = Offsets.RectChildRetryMilliseconds;
        before = world.Memory.Reads.Count;
        reader.Read();
        var afterInterval = world.Memory.Reads.Count - before;

        Assert.True(firstRead > secondRead);
        Assert.Equal(secondRead, thirdRead);
        Assert.True(afterInterval > secondRead);
    }

    [Fact]
    public void RectChildAddedLaterIsFoundAfterTheRetryInterval()
    {
        var (world, ui, _, root, child) = OpenNamedPanel();
        ui.SetChildren(root);
        long now = 0;
        var reader = world.UiReader(() => now);
        Assert.Equal(Vector2.Zero, Assert.Single(reader.Read().Panels).Min);

        ui.SetChildren(root, child);
        now = Offsets.RectChildRetryMilliseconds - 1;
        Assert.Equal(Vector2.Zero, Assert.Single(reader.Read().Panels).Min);

        now = Offsets.RectChildRetryMilliseconds;
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);
    }

    [Fact]
    public void DeadCachedRectChildFallsBackToThePageThenIsResolvedAgain()
    {
        var (world, ui, _, _, child) = OpenNamedPanel();
        long now = 0;
        var reader = world.UiReader(() => now);
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(Vector2.Zero, new Vector2(900, 1000)), shared: 0);
        Assert.Equal(Vector2.Zero, Assert.Single(reader.Read().Panels).Min);

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(new Vector2(2400, 200), new Vector2(900, 1000)));
        now = Offsets.RectChildRetryMilliseconds;
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);
    }

    static (FakeWorld World, FakeUi Ui, nint Page, nint Root) PageWithCell(string name, uint number)
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var cell = ui.Widget(ui.UserClass("Image"), name, new Vector2(2450, 300), new Vector2(80, 80), nameNumber: number);
        ui.SetChildren(root, child, cell);
        return (world, ui, page, root);
    }

    [Fact]
    public void FNameNumberOnePlusMapsToTheSuffixedName()
    {
        var (world, _, page, _) = PageWithCell("Cell", 1);
        var reader = world.UiReader();

        Assert.NotEqual((nint)0, reader.FindChild(page, "Cell_0"));
        Assert.Equal((nint)0, reader.FindChild(page, "Cell"));
    }

    [Fact]
    public void FNameNumberZeroMatchesThePlainName()
    {
        var (world, _, page, _) = PageWithCell("Cell", 0);
        var reader = world.UiReader();

        Assert.NotEqual((nint)0, reader.FindChild(page, "Cell"));
        Assert.Equal((nint)0, reader.FindChild(page, "Cell_0"));
    }

    [Fact]
    public void ALeadingZeroSuffixStaysPartOfThePlainName()
    {
        var (world, _, page, _) = PageWithCell("Foo_01", 0);

        Assert.NotEqual((nint)0, world.UiReader().FindChild(page, "Foo_01"));
    }

    [Fact]
    public void RectChildLookupRejectsADifferentFNameNumber()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        var numbered = ui.Widget(ui.UserClass("Image"), RectChild, new Vector2(2000, 100), new Vector2(500, 500), nameNumber: 1);
        ui.SetChildren(root, numbered);

        Assert.Equal(Vector2.Zero, Assert.Single(world.UiReader().Read().Panels).Min);
    }

    [Fact]
    public void ReadGivesTheSharedEmptyUiWhenNothingIsOpen()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);

        Assert.Same(GameUi.Empty, world.UiReader().Read());
    }

    [Fact]
    public void RectChildCacheIsDroppedWhenThePageGoesAway()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        var reader = world.UiReader();
        var slotCount = ui.Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum;
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);

        world.Memory.Write(slotCount, 0);
        Assert.Empty(reader.Read().Panels);

        var moved = ui.Widget(ui.UserClass("Image"), RectChild, new Vector2(2500, 250), new Vector2(900, 1000));
        ui.SetChildren(root, moved);
        world.Memory.Write(slotCount, 1);
        Assert.Equal(new Vector2(2500, 250), Assert.Single(reader.Read().Panels).Min);
    }

    [Fact]
    public void ASteadyUiCostsAFractionOfTheFirstRead()
    {
        var (world, _, _, _, _) = OpenNamedPanel();
        var reader = world.UiReader(() => 0);
        world.Memory.Reads.Clear();
        reader.Read();
        var first = world.Memory.Reads.Count;
        world.Memory.Reads.Clear();

        reader.Read();

        Assert.True(world.Memory.Reads.Count * 2 <= first, $"first {first} steady {world.Memory.Reads.Count}");
    }

    [Fact]
    public void AnUnchangedUiIsTheSameInstanceAndAMovedPanelIsANewOne()
    {
        var (world, ui, _, _, child) = OpenNamedPanel();
        var reader = world.UiReader(() => 0);
        var first = reader.Read();
        Assert.Same(first, reader.Read());

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(new Vector2(2500, 250), new Vector2(900, 1000)));
        var moved = reader.Read();

        Assert.NotSame(first, moved);
        Assert.Equal(new Vector2(2500, 250), Assert.Single(moved.Panels).Min);
    }

    [Fact]
    public void AHiddenPanelFollowsItsVisibilityWithoutWaitingForARefresh()
    {
        var (world, _, page, _, _) = OpenNamedPanel();
        var reader = world.UiReader(() => 0);
        Assert.Single(reader.Read().Panels);

        world.Memory.Write(page + Offsets.UWidgetVisibility, (byte)1);
        Assert.Empty(reader.Read().Panels);

        world.Memory.Write(page + Offsets.UWidgetVisibility, (byte)4);
        Assert.Single(reader.Read().Panels);
    }

    [Fact]
    public void APageSwappedUnderTheSameRootIsPickedUpAtTheNextRefresh()
    {
        long now = 0;
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var first = ui.Widget(ui.UserClass("First_C"), "First_C_1", Vector2.Zero, new Vector2(3440, 1440));
        var second = ui.Widget(ui.UserClass("Second_C"), "Second_C_1", Vector2.Zero, new Vector2(3440, 1440));
        var host = ui.Host(first);
        ui.AddRoot(host);
        var reader = world.UiReader(() => now);
        Assert.Equal("First_C", Assert.Single(reader.Read().Panels).Name);

        Assert.True(world.Memory.TryReadPtr(host + Offsets.PageHostContent, out var content));
        ui.SetChildren(content, second);

        now = Offsets.UiRootRefreshMilliseconds - 1;
        Assert.Equal("First_C", Assert.Single(reader.Read().Panels).Name);
        now = Offsets.UiRootRefreshMilliseconds;
        Assert.Equal("Second_C", Assert.Single(reader.Read().Panels).Name);
    }

    static readonly string[] HudLines =
    [
        "<RichText Id=\"100004\">+60</>% <RichText Id=\"100007\" HyperLinkType=\"1\" EventId=\"509\">Drop Quantity</>",
        "<RichText>-6% Max Elemental Resistance for you</>",
    ];

    static (FakeWorld World, FakeUi Ui, nint Box, nint DetailRoot) HudWithMapPanel(FakeWorld? into = null, bool hosted = false)
    {
        var world = into ?? new FakeWorld();
        var ui = new FakeUi(world);
        var (box, detailRoot) = ui.MapHud(HudLines, hosted, "<RichText>There are Coffins awaiting escort in this stage</>");
        return (world, ui, box, detailRoot);
    }

    [Fact]
    public void HudTextFindsTheHudInsideAPageHost()
    {
        var (world, _, _, _) = HudWithMapPanel(hosted: true);
        var reader = world.UiReader();
        Assert.Equal("Map Base XP gain: 100%", reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        Assert.Equal(["+60% Drop Quantity", "-6% Max Elemental Resistance for you"], reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SkillBarComesFromThePadRootAndThePadIsNotAPanel(bool hosted)
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddPad([2300, 2040, 2210, 1230, 1960, 0], hosted);
        var reader = world.UiReader();

        Assert.Equal([2300, 2040, 2210, 1230, 1960, 0], reader.ReadSkillBar()!);
        Assert.Empty(reader.Read().Panels);
    }

    [Fact]
    public void SkillBarIsNullWithoutAPadAndFoundBesideTheHud()
    {
        var (world, ui, _, _) = HudWithMapPanel();
        Assert.Null(world.UiReader().ReadSkillBar());

        ui.AddPad([1, 2]);
        Assert.Equal([1, 2], world.UiReader().ReadSkillBar()!);
    }

    [Fact]
    public void SkillBarIsNullWhenTheCountIsTorn()
    {
        var world = new FakeWorld();
        var pad = new FakeUi(world).AddPad([1, 2, 3]);
        world.Memory.Write(pad + Offsets.SkillCfgIdsOffset + Offsets.TArrayNum, Offsets.MaxSkills + 1);

        Assert.Null(world.UiReader().ReadSkillBar());
    }

    [Fact]
    public void HudTextReadsANamedWidgetInsideANestedUserWidget()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Equal("Map Base XP gain: 100%", world.UiReader().HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
    }

    [Fact]
    public void HudTextsListsVisibleItemsWithMarkupStripped()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Equal(["+60% Drop Quantity", "-6% Max Elemental Resistance for you"], world.UiReader().HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void NoHudGivesNothing()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);
        var reader = world.UiReader();
        Assert.Null(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        Assert.Empty(reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void AMissingChildGivesNothing()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Null(world.UiReader().HudText("Nope", Offsets.TextBlockText));
        Assert.Empty(world.UiReader().HudTexts("Nope", Offsets.TextBlockText));
    }

    [Fact]
    public void ABadStringIsSkipped()
    {
        var (world, ui, box, _) = HudWithMapPanel();
        var huge = ui.Widget(ui.UserClass("UIRichTextBlock"), "UIRichTextBlock_9", Vector2.Zero, new Vector2(400, 20));
        ui.Text(huge, Offsets.TextBlockText, "x");
        world.Memory.TryReadPtr(huge + Offsets.TextBlockText, out var data);
        world.Memory.Write(data + Offsets.TextDataString + Offsets.FStringNum, Offsets.MaxFStringChars + 1);
        var empty = ui.Widget(ui.UserClass("UIRichTextBlock"), "UIRichTextBlock_10", Vector2.Zero, new Vector2(400, 20));
        ui.SetChildren(box, huge, empty);
        Assert.Empty(world.UiReader().HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void FoundChildrenAreCachedNotWalkedAgain()
    {
        var (world, _, _, _) = HudWithMapPanel();
        var reader = world.UiReader();
        Assert.NotNull(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        var first = world.Memory.Reads.Count;
        Assert.NotNull(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        var second = world.Memory.Reads.Count - first;
        Assert.True(second < first / 2, $"cached lookup took {second} reads, the walk took {first}");
    }

    static nint ReplaceAffixBox(FakeUi ui, nint detailRoot, string line)
    {
        var box = ui.Widget(ui.PanelClass("VerticalBox"), Offsets.MapAffixWidget, Vector2.Zero, new Vector2(400, 200));
        var item = ui.Widget(ui.UserClass("UIRichTextBlock"), "UIRichTextBlock_1", Vector2.Zero, new Vector2(400, 20));
        ui.Text(item, Offsets.TextBlockText, line);
        ui.SetChildren(box, item);
        ui.SetChildren(detailRoot, box);
        return box;
    }

    [Fact]
    public void ForgetHudWalksAgainForAReplacedWidget()
    {
        var (world, ui, _, detailRoot) = HudWithMapPanel();
        var reader = world.UiReader();
        Assert.Equal(2, reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText).Length);
        ReplaceAffixBox(ui, detailRoot, "+20% Drop Quantity");
        Assert.Equal(2, reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText).Length);
        reader.ForgetHud();
        Assert.Equal(["+20% Drop Quantity"], reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void LosingTheOverlayForgetsTheHud()
    {
        var (world, _, _, _) = HudWithMapPanel();
        var reader = world.UiReader();
        Assert.Equal(2, reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText).Length);
        world.Memory.Write(FakeWorld.Module + Offsets.GEngineGlobalRva, 0L);
        Assert.Empty(reader.Read().Panels);
        var (_, ui, _, detailRoot) = HudWithMapPanel(world);
        ReplaceAffixBox(ui, detailRoot, "+20% Drop Quantity");
        Assert.Equal(["+20% Drop Quantity"], reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }
}
