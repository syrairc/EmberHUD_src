using EmberHUD.Testing;

namespace XpTrackerPlugin;

public sealed class XpSessionTests
{
    const long Far = 1_000_000_000;

    static XpSample At(double seconds, long exp, int area = 1, string name = "Map", bool town = false, bool paused = false, int level = 92, long toNext = Far) =>
        new(new EmberHUD.XpState(level, exp, toNext), area, name, town, paused, seconds);

    static XpSample Null(double seconds, int area = 0) => new(null, area, "", false, false, seconds);

    static XpSession Fed(params XpSample[] samples)
    {
        var session = new XpSession();
        foreach (var sample in samples) session.Add(sample);
        return session;
    }

    [Test]
    public void FirstSampleIsOnlyABaseline()
    {
        var session = Fed(At(0, 5_000_000));
        Check.Equal(0L, session.Gained);
        Check.Equal(0.0, session.ClockSeconds);
        Check.That(session.Latest is not null, "latest kept");
    }

    [Test]
    public void LevelUpAddsTheRemainderPlusTheOverflow()
    {
        var session = Fed(At(0, 90_000_000, level: 92, toNext: 90_077_482), At(1, 4_631_182, level: 93, toNext: 100_000_000));
        Check.Equal(77_482L + 4_631_182L, session.Gained);
    }

    [Test]
    public void GarbageSampleIsDropped()
    {
        var session = Fed(At(0, 100), At(1, 5_000, toNext: 1_000), At(2, 150));
        Check.Equal(50L, session.Gained);
        Check.Equal(1.0, session.ClockSeconds);
    }

    [Test]
    public void NullXpDuringAreaLoadKeepsTheMapOpen()
    {
        var session = Fed(At(0, 0), At(10, 100), Null(20), At(30, 300));
        Check.Equal(0, session.CompletedMaps);
        Check.Equal(300L, session.CurrentMapXp);
    }

    [Test]
    public void TownXpIgnoredWhenMapTimeOnly()
    {
        var session = new XpSession { MapTimeOnly = true };
        session.Add(At(0, 0));
        session.Add(At(1, 100));
        session.Add(At(2, 200, town: true));
        session.Add(At(3, 5_200, town: true));
        Check.Equal(200L, session.Gained);
        Check.Equal(2.0, session.ClockSeconds);
    }

    [Test]
    public void TownXpCountedWhenAlways()
    {
        var session = new XpSession { MapTimeOnly = false };
        session.Add(At(0, 0));
        session.Add(At(1, 100));
        session.Add(At(2, 200, town: true));
        session.Add(At(3, 5_200, town: true));
        Check.Equal(5_200L, session.Gained);
        Check.Equal(3.0, session.ClockSeconds);
    }

    [Test]
    public void PausedSamplesAddNothing()
    {
        var session = Fed(At(0, 0), At(1, 100, paused: true), At(2, 150));
        Check.Equal(50L, session.Gained);
        Check.Equal(1.0, session.ClockSeconds);
    }

    [Test]
    public void LeavingTheMapCreditsTheTickToThatMap()
    {
        var session = new XpSession { MapTimeOnly = true };
        session.Add(At(0, 0, area: 1, name: "A"));
        session.Add(At(60, 100, area: 1, name: "A"));
        session.Add(At(120, 300, area: 2, name: "B"));
        Check.Equal(1, session.CompletedMaps);
        Check.Equal(0L, session.CurrentMapXp);
        Check.Equal(300L, session.Gained);
        Check.Equal(300.0, session.XpPerMap!.Value);
    }

    [Test]
    public void MapChangeByNameClosesAndOpens()
    {
        var session = Fed(At(0, 0, area: 7, name: "One"), At(1, 10, area: 7, name: "Two"));
        Check.Equal(1, session.CompletedMaps);
    }

    [Test]
    public void RatesAreNullUnderSixtySeconds()
    {
        var session = Fed(At(0, 0), At(59, 1_000));
        Check.That(session.XpPerHour is null && session.XpPerMap is null && session.MapsPerHour is null && session.EtaHours is null && session.EtaMaps is null, "all null");
    }

    [Test]
    public void RatesFromGainedAndClock()
    {
        var session = Fed(At(0, 0), At(3600, 3_600_000));
        Check.Equal(3_600_000.0, session.XpPerHour!.Value);
        Check.That(session.MapsPerHour is null, "no completed maps");
        Check.Equal(3_600_000.0, session.XpPerMap!.Value);
    }

    [Test]
    public void EtaHoursAndMaps()
    {
        var session = Fed(At(0, 0, toNext: 7_200_000), At(3600, 3_600_000, toNext: 7_200_000));
        Check.Equal(1.0, session.EtaHours!.Value);
        Check.Equal(1.0, session.EtaMaps!.Value);
    }

    [Test]
    public void ClockBackwardsOrRepeatedIsIgnored()
    {
        var session = Fed(At(0, 0), At(10, 100), At(10, 150), At(5, 200), At(6, 210));
        Check.Equal(110L, session.Gained);
        Check.Equal(11.0, session.ClockSeconds);
    }

    [Test]
    public void CompletedMapsWithNoXpGiveNullPerMapAndEta()
    {
        var session = Fed(At(0, 0, area: 1, name: "A"), At(60, 0, area: 1, name: "A"), At(120, 0, area: 2, name: "B"));
        Check.Equal(1, session.CompletedMaps);
        Check.That(session.XpPerMap is null && session.EtaMaps is null, "zero average is null");
    }

    [Test]
    public void ResetClearsEverything()
    {
        var session = Fed(At(0, 0, area: 1, name: "A"), At(100, 500, area: 2, name: "B"));
        session.Reset();
        Check.Equal(0L, session.Gained);
        Check.Equal(0.0, session.ClockSeconds);
        Check.Equal(0, session.CompletedMaps);
        Check.Equal(0L, session.CurrentMapXp);
        Check.That(session.Latest is null && session.XpPerHour is null, "latest and rates cleared");
    }
}
