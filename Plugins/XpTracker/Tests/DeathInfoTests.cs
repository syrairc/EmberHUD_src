using EmberHUD;
using EmberHUD.Testing;

namespace XpTrackerPlugin;

public sealed class DeathInfoTests
{
    static GameState State(XpState? xp, DeathState? death) => new() { Xp = xp, Death = death };

    static DeathInfo? From(XpState? xp, DeathState? death, XpSession? session = null) => DeathInfo.From(State(xp, death), session ?? new XpSession());

    [Test]
    public void LossIsThePercentOfTheLevel()
    {
        var info = From(new XpState(93, 80_000_000, 100_000_000), new DeathState(10, true, false))!.Value;
        Check.Equal(10_000_000L, info.Loss);
        Check.That(!info.TokenInUse, "no token");
        Check.That(System.Math.Abs(info.Current - 0.8f) < 1e-6, "current fraction");
        Check.That(System.Math.Abs(info.Kept - 0.7f) < 1e-6, "kept fraction");
    }

    [Test]
    public void LossIsCappedAtWhatYouHave()
    {
        var info = From(new XpState(93, 3_000_000, 100_000_000), new DeathState(10, true, false))!.Value;
        Check.Equal(3_000_000L, info.Loss);
        Check.That(info.Kept == 0, "nothing kept");
    }

    [Test]
    public void TokenInUseLosesNothing()
    {
        var info = From(new XpState(93, 80_000_000, 100_000_000), new DeathState(10, true, true))!.Value;
        Check.Equal(0L, info.Loss);
        Check.That(info.TokenInUse, "token in use");
        Check.That(info.Kept == info.Current, "no red segment");
    }

    [Test]
    public void UnknownTokenStillShowsTheLoss()
    {
        var info = From(new XpState(93, 80_000_000, 100_000_000), new DeathState(10, true, null))!.Value;
        Check.Equal(10_000_000L, info.Loss);
        Check.That(!info.TokenInUse, "unknown is not in use");
    }

    [Test]
    public void NeedsBothDeathAndXp()
    {
        Check.That(From(new XpState(93, 1, 100), null) is null, "alive");
        Check.That(From(null, new DeathState(10, true, false)) is null, "no xp");
        Check.That(From(new XpState(93, 1, 0), new DeathState(10, true, false)) is null, "bad level size");
    }

    [Test]
    public void HoursComeFromTheSessionRate()
    {
        var session = new XpSession();
        session.Add(new XpSample(new XpState(92, 0, 1000), 1, "Map A", false, false, 0));
        session.Add(new XpSample(new XpState(92, 600, 1000), 1, "Map A", false, false, 120));
        var info = From(new XpState(92, 600, 1000), new DeathState(10, true, false), session)!.Value;
        Check.Equal(100L, info.Loss);
        Check.That(info.Hours is { } hours && System.Math.Abs(hours - 100.0 / 18000) < 1e-9, "100 xp at 18000 xp/hr");
        Check.That(From(new XpState(92, 600, 1000), new DeathState(10, true, false))!.Value.Hours is null, "no rate yet");
    }
}
