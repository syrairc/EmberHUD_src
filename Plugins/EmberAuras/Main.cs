using EmberHUD;

namespace EmberAuras;

public sealed class Main : Plugin<Settings>
{
    public override void Initialize()
    {
        Settings.Normalize();
    }
}
