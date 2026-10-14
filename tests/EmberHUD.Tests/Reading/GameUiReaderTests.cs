using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

public sealed class GameUiReaderTests
{
    static readonly string[] FirstPanel = Offsets.GamePanels.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(row => row.Split(',', StringSplitOptions.TrimEntries)).Single(row => row[0] == "PCBag_C");
    static string PanelClass => FirstPanel[0];
    static PanelKind PanelKindOf => Enum.Parse<PanelKind>(FirstPanel[1]);
    static string RectChild => FirstPanel[2];

    static GameUiReader Reader(FakeWorld world, Func<long>? clock = null)
    {
        var minimap = new MinimapReader(world.Memory, FakeWorld.Module);
        return new GameUiReader(world.Memory, FakeWorld.Module, minimap.TryReadName, clock);
    }

    static (FakeWorld World, FakeUi Ui, long Page, long Root, long Child) OpenNamedPanel(Vector2? origin = null)
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

    [Fact]
    public void ThePanelUnderTestHasARectChild()
    {
        Assert.Equal("PCBag_C", PanelClass);
        Assert.NotEmpty(RectChild);
    }

    [Fact]
    public void HostedNamedPanelReportsKindRectAndFlag()
    {
        var (world, _, page, _, _) = OpenNamedPanel();

        var panel = Assert.Single(Reader(world).Read().Panels);

        Assert.Equal(PanelClass, panel.Name);
        Assert.Equal(PanelKindOf, panel.Kind);
        Assert.False(panel.FullScreen);
        Assert.Equal((nint)page, panel.Address);
        Assert.Equal(new Vector2(2400, 200), panel.Min);
        Assert.Equal(new Vector2(3300, 1200), panel.Max);
    }

    [Fact]
    public void RectsAreRelativeToTheViewportOverlay()
    {
        var (world, _, _, _, _) = OpenNamedPanel(new Vector2(100, 50));

        var panel = Assert.Single(Reader(world).Read().Panels);

        Assert.Equal(new Vector2(2400, 200), panel.Min);
    }

    [Fact]
    public void UnknownHostedPageIsOther()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var page = ui.Widget(ui.UserClass("SomethingNew_C"), "SomethingNew_C_1", Vector2.Zero, new Vector2(3440, 1440));
        ui.AddRoot(ui.Host(page));

        var panel = Assert.Single(Reader(world).Read().Panels);

        Assert.Equal(new GamePanel("SomethingNew_C", PanelKind.Other, Vector2.Zero, new Vector2(3440, 1440), false) { Address = (nint)page }, panel);
    }

    [Fact]
    public void PanelsAreFullScreenWhileTheGameHidesItsOwnMinimap()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        ui.AddRoot(ui.Host(ui.Widget(ui.UserClass("SomethingNew_C"), "SomethingNew_C_1", Vector2.Zero, new Vector2(3440, 1440))));
        var gameMinimap = ui.Widget(ui.UserClass(Offsets.GameMinimapPage), Offsets.GameMinimapPage + "_1", Vector2.Zero, new Vector2(3440, 1440));
        ui.AddRoot(ui.Host(gameMinimap));
        var reader = Reader(world);

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

        Assert.Empty(Reader(world).Read().Panels);
    }

    [Fact]
    public void ThePageInTwoOverlaySlotsIsListedOnce()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        var host = ui.Host(ui.Widget(ui.UserClass("Twice_C"), "Twice_C_1", Vector2.Zero, new Vector2(3440, 1440)));
        ui.AddRoot(host);
        ui.AddRoot(host);

        Assert.Single(Reader(world).Read().Panels);
    }

    [Fact]
    public void PanelsKeepViewportOrderBackToFront()
    {
        var world = new FakeWorld();
        var ui = new FakeUi(world);
        foreach (var name in new[] { "Back_C", "Front_C" })
            ui.AddRoot(ui.Host(ui.Widget(ui.UserClass(name), name + "_1", Vector2.Zero, new Vector2(3440, 1440))));

        Assert.Equal(["Back_C", "Front_C"], Reader(world).Read().Panels.Select(p => p.Name));
    }

    [Fact]
    public void DeadSlateWidgetDropsThePanel()
    {
        var (world, ui, page, _, child) = OpenNamedPanel();
        var reader = Reader(world);
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

        Assert.Empty(Reader(world).Read().Panels);
    }

    [Fact]
    public void BrokenChainGivesEmptyUi()
    {
        var (world, ui, _, _, _) = OpenNamedPanel();
        var reader = Reader(world);

        world.Memory.Write(ui.Engine + Offsets.EngineGameViewport, 0L);
        Assert.Same(GameUi.Empty, reader.Read());

        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GEngineGlobalRva, 0x7777L);
        Assert.Same(GameUi.Empty, reader.Read());
    }

    [Fact]
    public void FindRectWalksNestedPanelsByName()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var grid = ui.Widget(ui.PanelClass("GridPanel"), "Grid", new Vector2(2450, 300), new Vector2(800, 600));
        var cell = ui.Widget(ui.UserClass("Image"), "Cell", new Vector2(2450, 300), new Vector2(80, 80), nameNumber: 1);
        ui.SetChildren(grid, cell);
        ui.SetChildren(root, child, grid);

        Assert.Equal((new Vector2(2450, 300), new Vector2(2530, 380)), Reader(world).FindRect((nint)page, "Cell_0"));
        Assert.Null(Reader(world).FindRect((nint)page, "Nope"));
    }

    [Fact]
    public void FindRectStopsOnCycles()
    {
        var (world, ui, page, _, _) = OpenNamedPanel();
        var loop = ui.Widget(ui.PanelClass("Loop"), "Loop", Vector2.Zero, new Vector2(10, 10));
        ui.SetChildren(loop, loop);
        ui.SetTree(page, loop);

        Assert.Null(Reader(world).FindRect((nint)page, "Nope"));
    }

    [Fact]
    public void MissingRectChildIsSearchedOnceUntilTheRetryIntervalPasses()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        ui.SetChildren(root);
        long now = 0;
        var reader = Reader(world, () => now);

        var before = world.Memory.Reads.Count;
        reader.Read();
        var firstRead = world.Memory.Reads.Count - before;
        before = world.Memory.Reads.Count;
        reader.Read();
        var secondRead = world.Memory.Reads.Count - before;
        now = Offsets.RectChildRetryMilliseconds - 1;
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
        var reader = Reader(world, () => now);
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
        var reader = Reader(world, () => now);
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(Vector2.Zero, new Vector2(900, 1000)), shared: 0);
        Assert.Equal(Vector2.Zero, Assert.Single(reader.Read().Panels).Min);

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(new Vector2(2400, 200), new Vector2(900, 1000)));
        now = Offsets.RectChildRetryMilliseconds;
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);
    }

    static (FakeWorld World, FakeUi Ui, long Page, long Root) PageWithCell(string name, uint number)
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
        var reader = Reader(world);

        Assert.Equal((new Vector2(2450, 300), new Vector2(2530, 380)), reader.FindRect((nint)page, "Cell_0"));
        Assert.Null(reader.FindRect((nint)page, "Cell"));
    }

    [Fact]
    public void FNameNumberZeroMatchesThePlainName()
    {
        var (world, _, page, _) = PageWithCell("Cell", 0);
        var reader = Reader(world);

        Assert.NotNull(reader.FindRect((nint)page, "Cell"));
        Assert.Null(reader.FindRect((nint)page, "Cell_0"));
    }

    [Fact]
    public void ALeadingZeroSuffixStaysPartOfThePlainName()
    {
        var (world, _, page, _) = PageWithCell("Foo_01", 0);

        Assert.NotNull(Reader(world).FindRect((nint)page, "Foo_01"));
    }

    [Fact]
    public void RectChildLookupRejectsADifferentFNameNumber()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        var numbered = ui.Widget(ui.UserClass("Image"), RectChild, new Vector2(2000, 100), new Vector2(500, 500), nameNumber: 1);
        ui.SetChildren(root, numbered);

        Assert.Equal(Vector2.Zero, Assert.Single(Reader(world).Read().Panels).Min);
    }

    [Fact]
    public void FindRectCachesTheChildPerPageAndName()
    {
        var (world, _, page, _) = PageWithCell("Cell", 1);
        var reader = Reader(world);

        var before = world.Memory.Reads.Count;
        var first = reader.FindRect((nint)page, "Cell_0");
        var firstReads = world.Memory.Reads.Count - before;
        before = world.Memory.Reads.Count;
        var second = reader.FindRect((nint)page, "Cell_0");
        var secondReads = world.Memory.Reads.Count - before;

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.True(secondReads * 3 < firstReads, $"first {firstReads} second {secondReads}");
    }

    [Fact]
    public void FindRectDoesNotCacheMisses()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var reader = Reader(world);
        Assert.Null(reader.FindRect((nint)page, "Late"));

        var late = ui.Widget(ui.UserClass("Image"), "Late", new Vector2(10, 20), new Vector2(30, 40));
        ui.SetChildren(root, child, late);

        Assert.Equal((new Vector2(10, 20), new Vector2(40, 60)), reader.FindRect((nint)page, "Late"));
    }

    [Fact]
    public void AMissedLookupWalksTheTreeAgainWithoutReadingNamesAgain()
    {
        var (world, _, page, _, _) = OpenNamedPanel();
        var reader = Reader(world);
        var blockPointer = FakeWorld.Module + Offsets.FNamePoolRva + Offsets.FNameBlocks;

        Assert.Null(reader.FindRect((nint)page, "Nope"));
        Assert.Contains(world.Memory.Reads, r => r.Address == blockPointer);
        world.Memory.Reads.Clear();
        Assert.Null(reader.FindRect((nint)page, "Nope"));

        Assert.NotEmpty(world.Memory.Reads);
        Assert.DoesNotContain(world.Memory.Reads, r => r.Address == blockPointer);
    }

    [Fact]
    public void FindRectFindsAgainWhenTheCachedChildSlateDied()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var reader = Reader(world);
        Assert.Equal((new Vector2(2400, 200), new Vector2(3300, 1200)), reader.FindRect((nint)page, RectChild));

        ui.WeakPtr(child + Offsets.UWidgetSlate, ui.Slate(Vector2.Zero, new Vector2(900, 1000)), shared: 0);
        Assert.Null(reader.FindRect((nint)page, RectChild));

        var replacement = ui.Widget(ui.UserClass("Image"), RectChild, new Vector2(2500, 250), new Vector2(900, 1000));
        ui.SetChildren(root, replacement);
        Assert.Equal((new Vector2(2500, 250), new Vector2(3400, 1250)), reader.FindRect((nint)page, RectChild));
    }

    [Fact]
    public void FindRectIgnoresACollapsedChild()
    {
        var (world, ui, page, root, child) = OpenNamedPanel();
        var hidden = ui.Widget(ui.UserClass("Image"), "Cell", new Vector2(2450, 300), new Vector2(80, 80), visibility: 1);
        ui.SetChildren(root, child, hidden);

        Assert.Null(Reader(world).FindRect((nint)page, "Cell"));
    }

    [Fact]
    public void ReadGivesTheSharedEmptyUiWhenNothingIsOpen()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);

        Assert.Same(GameUi.Empty, Reader(world).Read());
    }

    [Fact]
    public void RectChildCacheIsDroppedWhenThePageGoesAway()
    {
        var (world, ui, _, root, _) = OpenNamedPanel();
        var reader = Reader(world);
        var slotCount = ui.Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum;
        Assert.Equal(new Vector2(2400, 200), Assert.Single(reader.Read().Panels).Min);

        world.Memory.Write(slotCount, 0);
        Assert.Empty(reader.Read().Panels);

        var moved = ui.Widget(ui.UserClass("Image"), RectChild, new Vector2(2500, 250), new Vector2(900, 1000));
        ui.SetChildren(root, moved);
        world.Memory.Write(slotCount, 1);
        Assert.Equal(new Vector2(2500, 250), Assert.Single(reader.Read().Panels).Min);
    }

    static (FakeWorld World, FakeUi Ui, long Box, long DetailRoot) HudWithMapPanel(FakeWorld? into = null, bool hosted = false)
    {
        var world = into ?? new FakeWorld();
        var ui = new FakeUi(world);
        var hud = ui.Widget(ui.UserClass(Offsets.HudPageClassName), Offsets.HudPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var hudRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(3440, 1440));
        var detail = ui.Widget(ui.UserClass("FightMapDetailView_C"), "FightMapDetailView_C_1", Vector2.Zero, new Vector2(400, 300));
        var detailRoot = ui.Widget(ui.PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(400, 300));
        var xp = ui.Widget(ui.PanelClass("UIShrinkTextBlock"), Offsets.MapXpWidget, Vector2.Zero, new Vector2(200, 20));
        var box = ui.Widget(ui.PanelClass("VerticalBox"), Offsets.MapAffixWidget, Vector2.Zero, new Vector2(400, 200));
        var richClass = ui.UserClass("UIRichTextBlock");
        var first = ui.Widget(richClass, "UIRichTextBlock_1", Vector2.Zero, new Vector2(400, 20));
        var second = ui.Widget(richClass, "UIRichTextBlock_2", Vector2.Zero, new Vector2(400, 20));
        var stale = ui.Widget(richClass, "UIRichTextBlock_3", Vector2.Zero, new Vector2(400, 20), visibility: 1);
        ui.Text(xp, Offsets.ShrinkTextContent, "Map Base XP gain: 100%");
        ui.Text(first, Offsets.TextBlockText, "<RichText Id=\"100004\">+60</>% <RichText Id=\"100007\" HyperLinkType=\"1\" EventId=\"509\">Drop Quantity</>");
        ui.Text(second, Offsets.TextBlockText, "<RichText>-6% Max Elemental Resistance for you</>");
        ui.Text(stale, Offsets.TextBlockText, "<RichText>There are Coffins awaiting escort in this stage</>");
        ui.SetTree(hud, hudRoot);
        ui.SetChildren(hudRoot, detail);
        ui.SetTree(detail, detailRoot);
        ui.SetChildren(detailRoot, xp, box);
        ui.SetChildren(box, first, second, stale);
        ui.AddRoot(hosted ? ui.Host(hud) : hud);
        return (world, ui, box, detailRoot);
    }

    [Fact]
    public void HudTextFindsTheHudInsideAPageHost()
    {
        var (world, _, _, _) = HudWithMapPanel(hosted: true);
        var reader = Reader(world);
        Assert.Equal("Map Base XP gain: 100%", reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        Assert.Equal(["+60% Drop Quantity", "-6% Max Elemental Resistance for you"], reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void HudTextReadsANamedWidgetInsideANestedUserWidget()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Equal("Map Base XP gain: 100%", Reader(world).HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
    }

    [Fact]
    public void HudTextsListsVisibleItemsWithMarkupStripped()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Equal(["+60% Drop Quantity", "-6% Max Elemental Resistance for you"], Reader(world).HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void NoHudGivesNothing()
    {
        var world = new FakeWorld();
        _ = new FakeUi(world);
        var reader = Reader(world);
        Assert.Null(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        Assert.Empty(reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void AMissingChildGivesNothing()
    {
        var (world, _, _, _) = HudWithMapPanel();
        Assert.Null(Reader(world).HudText("Nope", Offsets.TextBlockText));
        Assert.Empty(Reader(world).HudTexts("Nope", Offsets.TextBlockText));
    }

    [Fact]
    public void ABadStringIsSkipped()
    {
        var (world, ui, box, _) = HudWithMapPanel();
        var huge = ui.Widget(ui.UserClass("UIRichTextBlock"), "UIRichTextBlock_9", Vector2.Zero, new Vector2(400, 20));
        ui.Text(huge, Offsets.TextBlockText, "x");
        world.Memory.TryReadPtr((nint)(huge + Offsets.TextBlockText), out var data);
        world.Memory.Write(data + Offsets.TextDataString + Offsets.FStringNum, Offsets.MaxFStringChars + 1);
        var empty = ui.Widget(ui.UserClass("UIRichTextBlock"), "UIRichTextBlock_10", Vector2.Zero, new Vector2(400, 20));
        ui.SetChildren(box, huge, empty);
        Assert.Empty(Reader(world).HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }

    [Fact]
    public void FoundChildrenAreCachedNotWalkedAgain()
    {
        var (world, _, _, _) = HudWithMapPanel();
        var reader = Reader(world);
        Assert.NotNull(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        var first = world.Memory.Reads.Count;
        Assert.NotNull(reader.HudText(Offsets.MapXpWidget, Offsets.ShrinkTextContent));
        var second = world.Memory.Reads.Count - first;
        Assert.True(second < first / 2, $"cached lookup took {second} reads, the walk took {first}");
    }

    static long ReplaceAffixBox(FakeUi ui, long detailRoot, string line)
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
        var reader = Reader(world);
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
        var reader = Reader(world);
        Assert.Equal(2, reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText).Length);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GEngineGlobalRva, 0L);
        Assert.Empty(reader.Read().Panels);
        var (_, ui, _, detailRoot) = HudWithMapPanel(world);
        ReplaceAffixBox(ui, detailRoot, "+20% Drop Quantity");
        Assert.Equal(["+20% Drop Quantity"], reader.HudTexts(Offsets.MapAffixWidget, Offsets.TextBlockText));
    }
}
