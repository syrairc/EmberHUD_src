using System;
using Hexa.NET.ImGui;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Sample;

public sealed class SampleTests
{
    static int Drawn(Action draw)
    {
        var drawn = 0;
        TestFrame.Run(() =>
        {
            var before = ImGui.GetBackgroundDrawList().VtxBuffer.Size;
            draw();
            drawn = ImGui.GetBackgroundDrawList().VtxBuffer.Size - before;
        });
        return drawn;
    }

    [Test]
    public void StatusTextShowsTheGameStatus()
    {
        Check.Equal("Status: starting", Main.StatusText(GameState.Empty));
        Check.Equal("Status: in map", Main.StatusText(GameState.Empty with { Status = "in map" }));
    }

    [Test]
    public void TickAndRenderDoNotThrowWithAnEmptyGame()
    {
        var plugin = new Main();
        TestGame.Set(GameState.Empty);
        TestInput.Hold();
        plugin.Tick();
        TestFrame.Run(plugin.Render);
    }

    [Test]
    public void TickTogglesVisibilityOnTheHotkey()
    {
        var plugin = new Main();
        try
        {
            TestInput.Hold();
            plugin.Tick();
            Check.That(plugin.Settings.Display.Show.Value, "no key, no toggle");
            TestInput.Hold(Key.F8);
            plugin.Tick();
            Check.That(!plugin.Settings.Display.Show.Value, "press hides");
            TestInput.Hold(Key.F8);
            plugin.Tick();
            Check.That(!plugin.Settings.Display.Show.Value, "held key does not toggle again");
            TestInput.Hold();
            TestInput.Hold(Key.F8);
            plugin.Tick();
            Check.That(plugin.Settings.Display.Show.Value, "second press shows");
        }
        finally
        {
            TestInput.Hold();
        }
    }

    [Test]
    public void RenderDrawsOnlyWhileShown()
    {
        var plugin = new Main();
        Check.That(Drawn(plugin.Render) > 0, "shown draws the marker and status");
        plugin.Settings.Display.Show.Value = false;
        Check.That(Drawn(plugin.Render) == 0, "hidden draws nothing");
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        var drawn = 0;
        TestFrame.Run(() =>
        {
            ImGui.Begin("sample settings");
            var before = ImGui.GetWindowDrawList().VtxBuffer.Size;
            new Settings().Draw();
            drawn = ImGui.GetWindowDrawList().VtxBuffer.Size - before;
            ImGui.End();
        });
        Check.That(drawn > 0, "settings page draws");
    }

    [Test]
    public void TheStatusTextIsABoxedWorldLabelByDefault()
    {
        var label = new Settings().Display.Label;
        Check.Equal(FontFace.Label, label.Face);
        Check.That(label.BgEnabled, "the label sits in a box");
    }
}
