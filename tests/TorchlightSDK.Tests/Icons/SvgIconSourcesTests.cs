using TorchlightSDK.Host.Icons;

namespace TorchlightSDK.Tests;

public sealed class SvgIconSourcesTests : IDisposable
{
    const string Plain = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" fill="currentColor"/></svg>""";

    readonly string _root = Directory.CreateTempSubdirectory("torchlightsdk-svg-").FullName;
    readonly LogCapture _log = new();

    string Overrides => Path.Combine(_root, "Icons");

    string Plugins => Path.Combine(_root, "Plugins");

    public void Dispose()
    {
        _log.Dispose();
        Directory.Delete(_root, true);
    }

    void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void AnOverrideReplacesABuiltInAndPluginIconsGetTheirPluginsPrefix()
    {
        Write(@"Icons\quest.svg", Plain.Replace("64\" fill", "63\" fill"));
        Write(@"Plugins\Alpha\Main.cs", "class Main {}");
        Write(@"Plugins\Alpha\Icons\ring.svg", Plain);
        Write(@"Icons\notes.txt", "not an icon");

        var sources = SvgIconSources.Load([("quest", Plain), ("gem", Plain)], Overrides, Plugins);

        Assert.Equal(3, sources.Count);
        Assert.Contains("63\" fill", sources["svg:quest"]);
        Assert.Equal(Plain, sources["svg:gem"]);
        Assert.Equal(Plain, sources["SVG:ALPHA/RING"]);
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void MissingFoldersAreFine()
    {
        Assert.Single(SvgIconSources.Load([("gem", Plain)], Path.Combine(_root, "none"), Path.Combine(_root, "none")));
        Assert.Empty(_log.Text);
    }

    [Fact]
    public void OversizedAndExternalReferencingFilesAreSkippedWithOneWarningEach()
    {
        Write(@"Icons\big.svg", Plain.Replace("<rect", $"<!-- {new string('x', 300 * 1024)} --><rect"));
        Write(@"Icons\image.svg", Plain.Replace("<rect", """<image href="C:\Windows\win.ini" width="1" height="1"/><rect"""));
        Write(@"Icons\css.svg", Plain.Replace("<rect", """<style>@import "http://example.com/a.css";</style><rect"""));
        Write(@"Icons\paint.svg", Plain.Replace("currentColor", "url(http://example.com/a.svg#g)"));
        Write(@"Icons\local.svg", Plain.Replace("<rect", """<defs><linearGradient id="g"/></defs><use href="#g"/><rect""").Replace("currentColor", "url(#g)"));

        var sources = SvgIconSources.Load([("bad", Plain.Replace("<rect", """<image xlink:href="https://example.com/a.png"/><rect"""))], Overrides, Plugins);

        Assert.Equal(["svg:local"], sources.Keys.ToArray());
        var warnings = _log.Messages();
        Assert.Equal(5, warnings.Count);
        foreach (var name in new[] { "big.svg", "image.svg", "css.svg", "paint.svg", "bad" }) Assert.Contains(warnings, line => line.Contains(name));
    }

    [Theory]
    [InlineData("""<use href="#a"/>""", null)]
    [InlineData("""<use xlink:href = '#a'/>""", null)]
    [InlineData("""<rect fill="url(#g)"/>""", null)]
    [InlineData("""<rect fill="url( '#g' )"/>""", null)]
    [InlineData("""<image href="a.png"/>""", "href=\"")]
    [InlineData("""<image xlink:href='http://x'/>""", "xlink:href='")]
    [InlineData("""<rect style="fill:url(file.svg#g)"/>""", "url(")]
    [InlineData("""<style>@IMPORT url(x.css);</style>""", "@IMPORT")]
    public void ExternalReferencesAreAnythingThatDoesNotPointInsideTheFile(string svg, string? found) => Assert.Equal(found, SvgIconSources.ExternalReference(svg));
}
