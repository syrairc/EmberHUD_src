using System.Numerics;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace Inspector;

public sealed class SettingsTests
{
    [Test]
    public void ThePageDraws() => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(700, 500));
        ImGui.Begin("inspector settings");
        new Settings().Draw();
        ImGui.End();
    });
}
