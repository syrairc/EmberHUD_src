using EmberHUD.Testing;
using EmberHUD.Ui;
using System.Numerics;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public sealed class TextTests
{
    [Theory]
    [InlineData("", "anything", true)]
    [InlineData("   ", "anything", true)]
    [InlineData("MAP", "chaos map roll", true)]
    [InlineData("xyz", "chaos map", false)]
    [InlineData("x", null, false)]
    public void MatchesIsACaseInsensitiveContains(string filter, string? text, bool matches) => Assert.Equal(matches, Text.Matches(filter, text));

    [Fact]
    public void EscPctDoublesPercentSigns()
    {
        Assert.Equal("34%% more", Text.EscPct("34% more"));
        Assert.Equal("", Text.EscPct(null));
    }

    [Fact]
    public void TipShowsRawTextWithPercentSignsWhenHovered()
    {
        const string raw = "50% done %s %d";
        var expected = 0f;
        var actual = 0f;
        TestFrame.Run(() =>
        {
            ImGui.GetIO().AddMousePosEvent(20, 20);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("host", ImGuiWindowFlags.NoTitleBar);
            ImGui.Button("tip");
            ImGui.End();
        });
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("host", ImGuiWindowFlags.NoTitleBar);
            ImGui.Button("tip");
            Text.Tip(raw);
            ImGui.End();
        });
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("host", ImGuiWindowFlags.NoTitleBar);
            ImGui.Button("tip");
            Text.Tip(raw);
            ImGui.End();
        });
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("host", ImGuiWindowFlags.NoTitleBar);
            ImGui.Button("tip");
            Text.Tip(raw);
            ImGui.End();
            var tooltip = ImGuiP.FindWindowByName("##Tooltip_00");
            Assert.False(tooltip.IsNull);
            actual = tooltip.Size.X;
            expected = ImGui.CalcTextSize(raw).X + ImGui.GetStyle().WindowPadding.X * 2;
        });
        Assert.Equal(expected, actual, 1f);
    }

    [Fact]
    public void WrapBreaksOnSpacesAndNeverReturnsNothing() => TestFrame.Run(() =>
    {
        Assert.Equal(["one", "two", "three"], Text.Wrap("one two three", 1, 1));
        Assert.Equal(["one two three"], Text.Wrap("one two three", 10000, 1));
        Assert.Equal([""], Text.Wrap("", 100, 1));
    });
}
