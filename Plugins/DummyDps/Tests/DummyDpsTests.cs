using System.Linq;
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
        Check.Equal("Current DPS,Average DPS,Max DPS,Time,Game tracker is capped, shows", string.Join(',', rows.Select(r => r.Label)));
        Check.Equal("7.25T", rows[0].Value);
        Check.Equal("6.10T", rows[1].Value);
        Check.Equal("8.90T", rows[2].Value);
        Check.Equal("1:15", rows[3].Value);
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
        Check.Equal("7,250,000,000,000", Main.Rows(Capped, true)[0].Value);
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
}
