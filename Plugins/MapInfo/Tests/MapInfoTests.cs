using System;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace MapInfoPlugin;

public sealed class MapInfoTests
{
    static readonly MapInfo Rainforest = new("Rainforest", 78, 272, "1%", false,
        [new MapContent("Vigil", "svg:quest", ContentState.Active)], ["+15% Drop Quantity", "1 additional Rare monsters"]);

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
        Check.Equal(421, plugin.EnemyMax(1, 421));
        Check.Equal(421, plugin.EnemyMax(1, 300));
        Check.Equal(50, plugin.EnemyMax(2, 50));
        Check.Equal(50, plugin.EnemyMax(2, null));
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
            TestGame.Set(GameState.Empty with { AreaId = 7, Map = Rainforest with { Remaining = 421 } });
            plugin.Tick();
            TestGame.Set(GameState.Empty with { AreaId = 7, Map = Rainforest with { Remaining = 300 } });
            plugin.Tick();
            plugin.Settings.General.Show = true;
            plugin.Tick();
            var rows = Main.Rows(Rainforest with { Remaining = 300 }, plugin.Settings.Sections, plugin.SeenMax);
            Check.Equal("300 / 421", rows.Single(r => r.Kind == RowKind.Bar).Text);
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
        var inMap = GameState.Empty with { Map = Rainforest };
        Check.That(!Main.Hidden(settings, inMap), "map shows");
        Check.That(Main.Hidden(settings, GameState.Empty), "no map hides");
        Check.That(Main.Hidden(settings, inMap with { Map = Rainforest with { IsTown = true } }), "town hides");
        Check.That(Main.Hidden(settings, inMap with { Ui = new GameUi([new GamePanel("Big_C", PanelKind.WorldMap, default, new(100, 100), true)]) }), "full-screen panel hides");
        settings.General.HideInTown = false;
        Check.That(!Main.Hidden(settings, inMap with { Map = Rainforest with { IsTown = true } }), "town shows when the rule is off");
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

    static int Drawn(Action draw)
    {
        var drawn = 0;
        TestFrame.Run(() =>
        {
            var before = Hexa.NET.ImGui.ImGui.GetBackgroundDrawList().VtxBuffer.Size;
            draw();
            drawn = Hexa.NET.ImGui.ImGui.GetBackgroundDrawList().VtxBuffer.Size - before;
        });
        return drawn;
    }

    [Test]
    public void RenderDrawsInAMapAndNothingWithoutOne()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty);
        Check.That(Drawn(plugin.Render) == 0, "no map, nothing drawn");
        TestGame.Set(GameState.Empty with { Map = Rainforest });
        Check.That(Drawn(plugin.Render) > 0, "map drawn");
        TestGame.Set(GameState.Empty);
    }

    [Test]
    public void EveryStateMarkDraws()
    {
        var look = new LookTab();
        foreach (var state in Enum.GetValues<ContentState>())
            Check.That(Drawn(() => Main.DrawMark(new System.Numerics.Vector2(50, 50), 8, state, look)) > 0, $"{state} draws");
    }

    [Test]
    public void RegistersTheToggleHotkeyUnbound()
    {
        var plugin = new Main();
        plugin.Initialize();
        var binding = plugin.Hotkeys.Single();
        Check.Equal("Toggle panel", binding.Id);
        Check.Equal(Key.None, binding.Default.Key);
    }

    [Test]
    public void ALockedPanelKeepsItsPlace()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty with { Map = Rainforest });
        TestInput.Hold();
        Drawn(plugin.Render);
        Check.Equal(40, plugin.Settings.General.X);
        Check.Equal(360, plugin.Settings.General.Width);
        TestGame.Set(GameState.Empty);
    }
}
