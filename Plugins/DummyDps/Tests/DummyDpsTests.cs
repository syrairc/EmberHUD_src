using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace DummyDpsPlugin;

public sealed class DummyDpsTests
{
    static readonly WoodStakeStats Capped = new(true, 75, 5_000_000_000_000, 5_000_000_000_000, 5_000_000_000_000, 7_250_000_000_000, 8_900_000_000_000, 6_100_000_000_000);

    [Test]
    public void CompactUsesThreeSignificantDigits()
    {
        Check.Equal("0", Main.Compact(0));
        Check.Equal("999", Main.Compact(999));
        Check.Equal("1.00K", Main.Compact(1000));
        Check.Equal("12.3K", Main.Compact(12_345));
        Check.Equal("26.8M", Main.Compact(26_767_917));
        Check.Equal("5.00T", Main.Compact(5_000_000_000_000));
        Check.Equal("7.25T", Main.Compact(7_250_000_000_000));
        Check.Equal("123Qa", Main.Compact(123_000_000_000_000_000));
        Check.Equal("1.00M", Main.Compact(999_600));
    }

    [Test]
    public void ExactGroupsDigits()
    {
        Check.Equal("7,250,000,000,000", Main.Exact(7_250_000_000_000));
    }

    [Test]
    public void ClockIsMinutesAndSeconds()
    {
        Check.Equal("1:15", Main.Clock(75));
        Check.Equal("0:05", Main.Clock(5));
    }

    [Test]
    public void RowsShowTheUnlimitedNumbersAndFlagACappedGameTracker()
    {
        var rows = Main.Rows(Capped, false);
        Check.Equal("Average DPS in a span of time,Duration,Highest DPS in 5s,Average DPS in 5s,Game tracker is capped, shows", string.Join(',', rows.Select(r => r.Label)));
        Check.Equal("6.10T", rows[0].Value);
        Check.Equal("1:15", rows[1].Value);
        Check.Equal("8.90T", rows[2].Value);
        Check.Equal("7.25T", rows[3].Value);
        Check.Equal("5.00T avg", rows[4].Value);
        Check.That(rows[4].Note, "the note row is marked");
    }

    [Test]
    public void NoNoteWhileTheGameTrackerAgrees()
    {
        var rows = Main.Rows(new WoodStakeStats(true, 10, 1_000_000, 2_000_000, 3_000_000, 2_000_000, 3_000_000, 1_000_000), false);
        Check.Equal(4, rows.Count);
    }

    [Test]
    public void ExactRowsUseFullDigits()
    {
        Check.Equal("6,100,000,000,000", Main.Rows(Capped, true)[0].Value);
    }

    [Test]
    public void BossLineShowsTheCurrentDps()
    {
        var meter = new CombatMeter();
        meter.Add(0, 0, 1, 5);
        meter.Add(1, 1_000_000, 1, 5);
        meter.Add(2, 3_000_000, 1, 5);
        Check.Equal("DPS 3.00M", Main.BossLine(meter, false));
        Check.Equal("DPS 3,000,000", Main.BossLine(meter, true));
    }

    [Test]
    public void BossLineWaitsForAFight()
    {
        Check.Equal("", Main.BossLine(new CombatMeter(), false));
    }

    [Test]
    public void BossReadoutNeedsTheOptionAndABossBar()
    {
        var bar = new BossBar(new ScreenRect(new Vector2(10, 10), new Vector2(110, 30)), 0.5f);
        var state = new GameState { BossBar = bar };
        Check.That(Main.BossReadout(state, new CombatTab { ShowOnBossBar = true }, true), "option on and bar up");
        Check.That(!Main.BossReadout(state, new CombatTab(), true), "option off");
        Check.That(!Main.BossReadout(new GameState(), new CombatTab { ShowOnBossBar = true }, true), "no bar");
        var covered = state with { Ui = new GameUi([new GamePanel("Map", PanelKind.WorldMap, Vector2.Zero, new Vector2(100, 100), true)]) };
        Check.That(!Main.BossReadout(covered, new CombatTab { ShowOnBossBar = true }, true), "hidden behind a full-screen panel");
        Check.That(Main.BossReadout(covered, new CombatTab { ShowOnBossBar = true }, false), "panel hiding off");
    }

    [Test]
    public void CombatIsReadForTheBossReadoutEvenWithThePanelOff()
    {
        var state = new GameState();
        Check.That(!Main.CombatWanted(state, new CombatTab { Show = false }, true), "nothing asks for it");
        Check.That(Main.CombatWanted(state, new CombatTab { Show = false, ShowOnBossBar = true }, true), "boss readout asks for it");
        Check.That(Main.CombatWanted(state, new CombatTab(), true), "panel asks for it");
    }

    [Test]
    public void VisibleWhileRunningOrKeptInTheSameArea()
    {
        var stopped = Capped with { Running = false };
        Check.That(Main.Visible(Capped, 1, int.MinValue, false), "running shows");
        Check.That(Main.Visible(stopped, 1, 1, true), "stopped in the same area is kept");
        Check.That(!Main.Visible(stopped, 2, 1, true), "another area hides it");
        Check.That(!Main.Visible(stopped, 1, 1, false), "keep off hides it");
        Check.That(!Main.Visible(stopped with { Seconds = 0 }, 1, 1, true), "nothing recorded hides it");
        Check.That(!Main.Visible(null, 1, 1, true), "no manager hides it");
    }

    [Test]
    public void TickAndRenderDoNotThrow()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty);
        TestInput.Hold();
        plugin.Tick();
        TestFrame.Run(plugin.Render);
        TestGame.Set(GameState.Empty with { WoodStake = Capped });
        plugin.Tick();
        TestFrame.Run(plugin.Render);
    }

    static readonly MapInfo Town = new("Ember's Rest", 1, null, null, true, [], []);

    [Test]
    public void CombatRowsFormatTheMeter()
    {
        var meter = new CombatMeter();
        meter.Add(0, 0, 1, 5);
        meter.Add(1, 12_345, 1, 5);
        meter.Add(3, 20_000, 1, 5);
        var rows = Main.CombatRows(meter, false);
        Check.Equal("DPS,Peak DPS,Average DPS,Total damage,Fight time", string.Join(',', rows.Select(r => r.Label)));
        Check.Equal("20.0K", rows[3].Value);
        Check.Equal("0:02", rows[4].Value);
        Check.Equal("20,000", Main.CombatRows(meter, true)[3].Value);
    }

    [Test]
    public void CombatVisibleHidesInTown()
    {
        var inTown = GameState.Empty with { Area = Town };
        Check.That(!Main.CombatVisible(inTown, new CombatTab { HideInTown = true }, false), "town hides");
        Check.That(Main.CombatVisible(inTown, new CombatTab { HideInTown = false }, false), "rule off shows");
    }

    [Test]
    public void CombatVisibleStaysOnAtTheRunningDummyInTown()
    {
        var atDummy = GameState.Empty with { Area = Town, WoodStake = new WoodStakeStats(true, 5, 0, 0, 0, 0, 0, 0) };
        Check.That(Main.CombatVisible(atDummy, new CombatTab { HideInTown = true }, false), "dummy running shows in town");
    }

    [Test]
    public void CombatVisibleHidesBehindFullScreenPanels()
    {
        var full = GameState.Empty with { Ui = new GameUi([new GamePanel("Map", PanelKind.WorldMap, default, default, true)]) };
        Check.That(!Main.CombatVisible(full, new CombatTab(), true), "full-screen panel hides");
        Check.That(Main.CombatVisible(full, new CombatTab(), false), "rule off shows");
    }

    [Test]
    public void CombatVisibleNeedsAFightAndShowOn()
    {
        Check.That(!Main.CombatVisible(GameState.Empty, new CombatTab { Show = false }, false), "show off hides");
        Check.That(Main.CombatVisible(GameState.Empty, new CombatTab(), false), "default shows");
    }

    [Test]
    public void TickOnlyAsksForCombatDataWhereThePanelCanShow()
    {
        var plugin = new Main();
        TestInput.Hold();
        TestGame.Set(GameState.Empty with { Area = Town });
        Check.That(!TestGame.CombatAskedDuring(plugin.Tick), "town does not ask");
        TestGame.Set(GameState.Empty);
        Check.That(TestGame.CombatAskedDuring(plugin.Tick), "outside town asks");
    }

    [Test]
    public void TogglingCountOverkillResetsTheMeter()
    {
        var plugin = new Main();
        TestInput.Hold();
        var second = System.Diagnostics.Stopwatch.Frequency;
        TestGame.Set(GameState.Empty with { Combat = new CombatStats(0, 0, 0, 0) });
        plugin.Tick();
        TestGame.Set(GameState.Empty with { Combat = new CombatStats(second, 1000, 1500, 1) });
        plugin.Tick();
        Check.That(plugin._meter.HasFight, "fight running");
        plugin.Settings.Combat.CountOverkill = true;
        TestGame.Set(GameState.Empty with { Combat = new CombatStats(2 * second, 1000, 1500, 1) });
        plugin.Tick();
        Check.That(!plugin._meter.HasFight, "toggle cleared the fight");
    }
}
