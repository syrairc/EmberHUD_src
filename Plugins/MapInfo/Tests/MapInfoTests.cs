using System;
using System.Collections.Generic;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace MapInfoPlugin;

public sealed class MapInfoTests
{
    static readonly MapInfo Rainforest = new("Rainforest", 78, 272, "1%", false,
        [new MapContent("Vigil", "svg:quest", ContentState.Active)], ["+15% Drop Quantity", "1 additional Rare monsters"]);

    static string BarText(Main plugin, MapInfo map) => plugin.LaidFor(map, 0).Single(l => l.Row.Kind == RowKind.Bar).Row.Text;

    [Test]
    public void AllSectionsGiveTitleLevelXpContentAndModifiers()
    {
        var rows = Main.Rows(Rainforest, new SectionsTab());
        Check.Equal("Title,Line,Line,Content,Modifier,Modifier", string.Join(',', rows.Select(r => r.Kind)));
        Check.Equal("Monster level 78 · 272 enemies", rows[1].Text);
        Check.Equal("Map Base XP gain: 1%", rows[2].Text);
        Check.Equal("Vigil", rows[3].Content.Name);
    }

    [Test]
    public void AnUnknownMapNameGivesNoTitleRow()
    {
        var rows = Main.Rows(Rainforest with { Name = "" }, new SectionsTab());
        Check.Equal("Line,Line,Content,Modifier,Modifier", string.Join(',', rows.Select(r => r.Kind)));
    }

    [Test]
    public void TheBarReplacesTheEnemyCount()
    {
        var sections = new SectionsTab();
        sections.EnemiesBar = true;
        var rows = Main.Rows(Rainforest with { Remaining = 148 }, sections, 421);
        Check.Equal("Title,Line,Bar,Line,Content,Modifier,Modifier", string.Join(',', rows.Select(r => r.Kind)));
        Check.Equal("Monster level 78", rows[1].Text);
        Check.Equal("148 / 421", rows[2].Text);
        Check.Equal(148, rows[2].Remaining);
        Check.Equal(421, rows[2].Max);
    }

    [Test]
    public void TheMaxIsTheHighestSeenInTheArea()
    {
        var plugin = new Main();
        plugin.Settings.Sections.EnemiesBar = true;
        string inArea1 = "", inArea2 = "", afterGap = "";
        TestFrame.Run(() =>
        {
            plugin.EnemyMax(1, 421);
            plugin.EnemyMax(1, 300);
            inArea1 = BarText(plugin, Rainforest with { Remaining = 300 });
            plugin.EnemyMax(2, 50);
            inArea2 = BarText(plugin, Rainforest with { Remaining = 50 });
            plugin.EnemyMax(2, null);
            afterGap = BarText(plugin, Rainforest with { Remaining = 20 });
        });
        Check.Equal("300 / 421", inArea1);
        Check.Equal("50 / 50", inArea2);
        Check.Equal("20 / 50", afterGap);
    }

    [Test]
    public void NoRemainingNoBar()
    {
        var sections = new SectionsTab();
        sections.EnemiesBar = true;
        var rows = Main.Rows(Rainforest with { Remaining = null }, sections, 421);
        Check.That(rows.All(r => r.Kind != RowKind.Bar), "no bar row");
    }

    [Test]
    public void TheBarRowHasTheBarHeight()
    {
        var look = new LookTab();
        float height = 0;
        TestFrame.Run(() => height = Main.Layout([new Row(RowKind.Bar, "148 / 421", Remaining: 148, Max: 421)], look, 360).Single().Height);
        Check.Equal(look.BarHeight + Main.Gap, height);
    }

    [Test]
    public void TheMaxIsTrackedWhileThePanelIsHidden()
    {
        var plugin = new Main();
        plugin.Settings.Sections.EnemiesBar = true;
        plugin.Settings.General.Show = false;
        try
        {
            TestGame.Set(GameState.Empty with { AreaId = 7, Area = Rainforest with { Remaining = 421 } });
            plugin.Tick();
            TestGame.Set(GameState.Empty with { AreaId = 7, Area = Rainforest with { Remaining = 300 } });
            plugin.Tick();
            plugin.Settings.General.Show = true;
            plugin.Tick();
            var text = "";
            TestFrame.Run(() => text = BarText(plugin, Rainforest with { Remaining = 300 }));
            Check.Equal("300 / 421", text);
        }
        finally { TestGame.Set(GameState.Empty); }
    }

    [Test]
    public void SectionsTurnOff()
    {
        var sections = new SectionsTab();
        sections.Enemies = false;
        sections.Xp = false;
        sections.Modifiers = false;
        var rows = Main.Rows(Rainforest, sections);
        Check.Equal("Title,Line,Content", string.Join(',', rows.Select(r => r.Kind)));
        Check.Equal("Monster level 78", rows[1].Text);
    }

    [Test]
    public void ACardTypeWithSeveralCardsShowsItsCount()
    {
        var rows = Main.Rows(Rainforest with { Content = [new MapContent("The Devil", "game:x", ContentState.Active, 1, 2), new MapContent("The Hermit", "game:x", ContentState.Done, 1, 1)] }, new SectionsTab());
        Check.Equal("The Devil  1/2", rows.Single(r => r.Content.Name == "The Devil").Text);
        Check.Equal("The Hermit", rows.Single(r => r.Content.Name == "The Hermit").Text);
    }

    [Test]
    public void UnknownCountsDropTheirPart()
    {
        var rows = Main.Rows(Rainforest with { Remaining = null, XpGain = null }, new SectionsTab());
        Check.Equal("Title,Line,Content,Modifier,Modifier", string.Join(',', rows.Select(r => r.Kind)));
        Check.Equal("Monster level 78", rows[1].Text);
    }

    [Test]
    public void HiddenRules()
    {
        var settings = new Settings();
        var inMap = GameState.Empty with { Area = Rainforest };
        Check.That(!Main.Hidden(settings, inMap), "map shows");
        Check.That(Main.Hidden(settings, GameState.Empty), "no map hides");
        Check.That(Main.Hidden(settings, inMap with { Area = Rainforest with { IsTown = true } }), "town hides");
        Check.That(Main.Hidden(settings, inMap with { Ui = new GameUi([new GamePanel("Big_C", PanelKind.WorldMap, default, new(100, 100), true)]) }), "full-screen panel hides");
        settings.General.HideInTown = false;
        Check.That(!Main.Hidden(settings, inMap with { Area = Rainforest with { IsTown = true } }), "town shows when the rule is off");
        settings.General.Show = false;
        Check.That(Main.Hidden(settings, inMap), "toggled off hides");
    }

    [Test]
    public void LayoutHeightGrowsWithContent()
    {
        float few = 0, many = 0;
        TestFrame.Run(() =>
        {
            var look = new LookTab();
            few = Main.Layout(Main.Rows(Rainforest, new SectionsTab()), look, 360).Sum(l => l.Height);
            many = Main.Layout(Main.Rows(Rainforest with { Modifiers = [.. Enumerable.Repeat("+15% Drop Quantity", 10)] }, new SectionsTab()), look, 360).Sum(l => l.Height);
        });
        Check.That(few > 0, "laid out");
        Check.That(many > few, "more modifiers, taller panel");
    }

    [Test]
    public void NarrowWidthStillWraps()
    {
        int lines = 0;
        TestFrame.Run(() => lines = Main.Layout(Main.Rows(Rainforest, new SectionsTab()), new LookTab(), 100).Sum(l => l.Lines.Length));
        Check.That(lines > 6, "something wrapped at 100 px");
    }

    [Test]
    public void TheTitleIsNeverWrapped()
    {
        int titleLines = 0;
        TestFrame.Run(() =>
        {
            var longName = string.Join(' ', Enumerable.Repeat("Rainforest", 20));
            var laid = Main.Layout(Main.Rows(Rainforest with { Name = longName }, new SectionsTab()), new LookTab(), 160);
            titleLines = laid.First(l => l.Row.Kind == RowKind.Title).Lines.Length;
        });
        Check.Equal(1, titleLines);
    }

    [Test]
    public void ALongWordDoesNotThrow()
    {
        TestFrame.Run(() => Main.Layout(Main.Rows(Rainforest with { Modifiers = [new string('W', 400)] }, new SectionsTab()), new LookTab(), 160));
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        TestFrame.Run(() =>
        {
            Hexa.NET.ImGui.ImGui.Begin("mapinfo settings");
            new Settings().Draw();
            Hexa.NET.ImGui.ImGui.End();
        });
    }

    [Test]
    public void RenderDrawsInAMapAndNothingWithoutOne()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty);
        Check.That(TestFrame.Vertices(plugin.Render) == 0, "no map, nothing drawn");
        TestGame.Set(GameState.Empty with { Area = Rainforest });
        Check.That(TestFrame.Vertices(plugin.Render) > 0, "map drawn");
        TestGame.Set(GameState.Empty);
    }

    [Test]
    public void EveryStateMarkDraws()
    {
        var look = new LookTab();
        foreach (var state in Enum.GetValues<ContentState>())
            Check.That(TestFrame.Vertices(() => Main.DrawMark(new System.Numerics.Vector2(50, 50), 8, state, look)) > 0, $"{state} draws");
    }

    [Test]
    public void RegistersTheToggleHotkeyOnF4()
    {
        var plugin = new Main();
        plugin.Initialize();
        var binding = plugin.Hotkeys.Single();
        Check.Equal("Toggle panel", binding.Id);
        Check.Equal(Key.F4, binding.Default.Key);
    }

    [Test]
    public void ALockedPanelKeepsItsPlace()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty with { Area = Rainforest });
        TestInput.Hold();
        TestFrame.Vertices(plugin.Render);
        Check.Equal(40, plugin.Settings.General.X);
        Check.Equal(360, plugin.Settings.General.Width);
        TestGame.Set(GameState.Empty);
    }

    [Test]
    public void TheLayoutIsKeptUntilTheMapTheWidthOrTheRefreshChanges()
    {
        var plugin = new Main();
        List<Laid> first = [], again = [], later = [], widened = [], other = [];
        TestFrame.Run(() =>
        {
            first = plugin.LaidFor(Rainforest, 1000);
            again = plugin.LaidFor(Rainforest, 1100);
            later = plugin.LaidFor(Rainforest, 1250);
            plugin.Settings.General.Width = 500;
            widened = plugin.LaidFor(Rainforest, 1251);
            other = plugin.LaidFor(Rainforest with { Remaining = 5 }, 1252);
        });

        Check.That(ReferenceEquals(first, again), "same map 100 ms later");
        Check.That(!ReferenceEquals(again, later), "250 ms later");
        Check.That(!ReferenceEquals(later, widened), "the width changed");
        Check.That(!ReferenceEquals(widened, other), "a new map snapshot");
    }

    [Test]
    public void TheLayoutPicksUpAStyleEditWithinTheRefresh()
    {
        var plugin = new Main();
        float before = 0, held = 0, after = 0;
        TestFrame.Run(() =>
        {
            before = plugin.LaidFor(Rainforest, 1000).Sum(l => l.Height);
            plugin.Settings.Look.Title.Scale = 3;
            held = plugin.LaidFor(Rainforest, 1100).Sum(l => l.Height);
            after = plugin.LaidFor(Rainforest, 1250).Sum(l => l.Height);
        });

        Check.Equal(before, held);
        Check.That(after > before, "a bigger title makes a taller panel after the refresh");
    }
}
