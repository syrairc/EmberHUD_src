using EmberHUD.Testing;
using EmberHUD.Ui;
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

    [Fact]
    public void WrapMeasuresTheWholeTextOnceWhenItFits()
    {
        var calls = 0;

        var lines = Text.Wrap("one two three", 100, s => { calls++; return s.Length; });

        Assert.Equal(["one two three"], lines);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void WrapMeasuresEachWordAndTheSpaceOnce()
    {
        var calls = 0;

        var lines = Text.Wrap("aa bb cc dd", 5, s => { calls++; return s.Length; });

        Assert.Equal(["aa bb", "cc dd"], lines);
        Assert.Equal(6, calls);
    }

    [Fact]
    public void AWordWiderThanTheLimitStaysWhole()
    {
        Assert.Equal(["Supercalifragilistic"], Text.Wrap("Supercalifragilistic", 5, s => s.Length));
        Assert.Equal(["ab", "Supercalifragilistic", "cd"], Text.Wrap("ab Supercalifragilistic cd", 5, s => s.Length));
    }

    [Fact]
    public void ALineMayFillTheWidthExactly()
    {
        Assert.Equal(["ab cd"], Text.Wrap("ab cd", 5, s => s.Length));
        Assert.Equal(["ab cd", "ef"], Text.Wrap("ab cd ef", 5, s => s.Length));
    }

    [Fact]
    public void ExtraSpacesNeverMakeEmptyLines()
    {
        Assert.Equal(["aa bb", "cc"], Text.Wrap("aa  bb cc", 5, s => s.Length));
        Assert.Equal([""], Text.Wrap("      ", 2, s => s.Length));
    }

    [Fact]
    public void NullAndEmptyGiveOneEmptyLine()
    {
        Assert.Equal([""], Text.Wrap(null, 10, s => s.Length));
        Assert.Equal([""], Text.Wrap("", 10, s => s.Length));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(40)]
    public void WrapNeverLosesTextWhateverTheWidth(int limit)
    {
        const string text = "the quick brown fox jumps over the lazy dog";

        var lines = Text.Wrap(text, limit, s => s.Length);

        Assert.Equal(text, string.Join(" ", lines));
        Assert.All(lines, line => Assert.True(line.Length <= limit || !line.Contains(' '), line));
    }
}
