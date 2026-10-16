using EmberHUD.Testing;

namespace NameplatesPlugin;

public sealed class TemplateTests
{
    static string Render(string text, double life = 50, double maxLife = 100) =>
        Template.Parse(text).Render(new PlateData("Ichi Militant", life, maxLife, 12.5f, "Rare", "Monster"));

    [Test]
    public void NameAndPercent() => Check.Equal("Ichi Militant 50%", Render("{name} {hp%}%"));

    [Test]
    public void AbbreviatesThousandsAndMillions()
    {
        Check.Equal("999", NumberFormat.Abbrev(999));
        Check.Equal("1.5K", NumberFormat.Abbrev(1500));
        Check.Equal("2.5M", NumberFormat.Abbrev(2_500_000));
    }

    [Test]
    public void PercentIsZeroWhenMaxLifeIsZero() => Check.Equal("0", Render("{hp%}", 50, 0));

    [Test]
    public void PercentClampsToTheRange()
    {
        Check.Equal("100", Render("{hp%}", 300, 100));
        Check.Equal("0", Render("{hp%}", -5, 100));
    }

    [Test]
    public void UnknownTokenStaysLiteral() => Check.Equal("{wat}", Render("{wat}"));

    [Test]
    public void UnclosedBraceStaysLiteral() => Check.Equal("a {name", Render("a {name"));

    [Test]
    public void EmptyTemplateRendersEmpty() => Check.Equal("", Render(""));

    [Test]
    public void ConstantTemplateRendersWithoutParsingTokens() => Check.Equal("plain text", Render("plain text"));
}
