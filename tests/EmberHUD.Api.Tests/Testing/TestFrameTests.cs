using EmberHUD.Testing;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class TestFrameTests
{
    [Fact]
    public void RunsTheDrawInsideAHeadlessFrame()
    {
        var inFrame = false;
        TestFrame.Run(() =>
        {
            ImGui.Begin("headless");
            ImGui.TextUnformatted("hello");
            ImGui.End();
            inFrame = ImGui.GetCurrentContext().WithinFrameScope;
        });
        Assert.True(inFrame);
    }

    [Fact]
    public void FramesKeepComing()
    {
        for (var i = 0; i < 3; i++) TestFrame.Run(() => ImGui.Button("again"));
    }

    [Fact]
    public void AWindowLeftOpenFailsTheFrameAndTheNextFrameIsClean()
    {
        var error = Assert.Throws<InvalidOperationException>(() => TestFrame.Run(() => ImGui.Begin("leak")));
        Assert.Contains("SizeOfWindowStack", error.Message);
        TestFrame.Run(() => ImGui.TextUnformatted("recovered"));
    }

    [Fact]
    public void ThePushedIdLeftOpenIsReported()
    {
        var error = Assert.Throws<InvalidOperationException>(() => TestFrame.Run(() => ImGui.PushID("leak")));
        Assert.Contains("SizeOfIDStack", error.Message);
    }

    [Fact]
    public void TheDrawsOwnExceptionComesBackUnchanged()
    {
        var error = Assert.Throws<FormatException>(() => TestFrame.Run(() =>
        {
            ImGui.Begin("throws");
            throw new FormatException("bad draw");
        }));
        Assert.Equal("bad draw", error.Message);
        TestFrame.Run(() => ImGui.TextUnformatted("recovered"));
    }
}
