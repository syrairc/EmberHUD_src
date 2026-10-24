using EmberHUD;
using EmberHUD.Testing;

namespace AutoAttack;

public sealed class SettingsTests
{
    [Test]
    public void PageDraws()
    {
        TestGame.Set(GameState.Empty);
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0);
    }

    [Test]
    public void ShowRadiusIsOffByDefault()
    {
        Check.That(!new Settings().ShowRadius);
    }
}
