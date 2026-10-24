using EmberHUD;
using EmberHUD.Testing;

namespace XpTrackerPlugin;

public sealed class FormatTests
{
    const string All = "{xphr}|{xpmap}|{mapshr}|{etah}|{etamaps}|{maps}|{level}|{pct}";

    static XpSample At(double seconds, long exp, bool town = false, long toNext = 10_000_000) =>
        new(new XpState(92, exp, toNext), 1, "Map", town, false, seconds);

    static XpSession OneMapInAnHour(long toNext = 10_000_000)
    {
        var session = new XpSession();
        session.Add(At(0, 0, toNext: toNext));
        session.Add(At(1800, 1_000_000, toNext: toNext));
        session.Add(At(3600, 2_000_000, town: true, toNext: toNext));
        return session;
    }

    [Test]
    public void TokensRenderFromTheSession()
    {
        Check.Equal("2.00M|2.00M|1.0|4.0h|4|1|92|20.0", Main.Render(All, OneMapInAnHour()));
    }

    [Test]
    public void MissingValuesRenderDashes()
    {
        Check.Equal("--|--|--|--|--|0|--|--", Main.Render(All, new XpSession()));
    }

    [Test]
    public void EtaHoursUnderAnHourIsMinutes()
    {
        Check.Equal("15m", Main.Render("{etah}", OneMapInAnHour(2_500_000)));
    }

    [Test]
    public void UnknownTokenStaysLiteral()
    {
        Check.Equal("{nope} 2.00M", Main.Render("{nope} {xphr}", OneMapInAnHour()));
    }
}
