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
    [InlineData("map chaos", "chaos map roll", true)]
    [InlineData("map zzz", "chaos map roll", false)]
    [InlineData("xyz", "chaos map", false)]
    [InlineData("x", null, false)]
    public void MatchesNeedsEveryWordSomewhereInAnyOrder(string filter, string? text, bool matches) => Assert.Equal(matches, Text.Matches(filter, text));

    [Theory]
    [InlineData("(", "rare (small)", true)]
    [InlineData("[a", "x[ab]", true)]
    [InlineData("(", "no brackets", false)]
    public void MatchesFallsBackToContainsOnABadPattern(string filter, string text, bool matches) => Assert.Equal(matches, Text.Matches(filter, text));

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
        var window = UiFrame.NextId("tip");
        var at = Vector2.Zero;
        void Body()
        {
            ImGui.Button("tip");
            at = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            Text.Tip(raw);
        }
        UiFrame.Run(window, Body);
        var tip = UiFrame.TooltipAt(window, at, Body);
        Assert.NotNull(tip);
        Assert.Equal(UiFrame.TextWidth(raw) + UiFrame.Style.WindowPadding.X * 2, tip.Size.X, 1f);
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

    [Fact]
    public void WrapWithAStyleBreaksAtTheStyledWidth()
    {
        string[] small = [], big = [];
        TestFrame.Run(() =>
        {
            var width = Draw.MeasureText("one two three", new TextStyle()).X + 1;
            small = Text.Wrap("one two three four five six", width, new TextStyle());
            big = Text.Wrap("one two three four five six", width, new TextStyle { Scale = 2f });
        });
        Assert.Equal(["one two three", "four five six"], small);
        Assert.True(big.Length > small.Length);
    }

    [Fact]
    public void WrapWithAStyleKeepsALongWordWhole()
    {
        string[] lines = [];
        TestFrame.Run(() => lines = Text.Wrap("Supercalifragilistic", 5, new TextStyle()));
        Assert.Equal(["Supercalifragilistic"], lines);
    }
}
