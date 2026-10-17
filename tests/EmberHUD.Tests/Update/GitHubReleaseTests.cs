using EmberHUD.Host.Update;

namespace EmberHUD.Tests;

public class GitHubReleaseTests
{
    private const string ZipAsset = """{"name":"EmberHUD-20261016-v1.0.12.zip","browser_download_url":"https://github.com/syrairc/EmberHUD/releases/download/1.0.12/EmberHUD-20261016-v1.0.12.zip","digest":"sha256:7da1c7a36d59cfea3cd7ed62023acccc410bdc7c8bf6e4ca6ae3d500ed0b1f48"}""";
    private const string ManifestAsset = """{"name":"manifest.json","browser_download_url":"https://github.com/syrairc/EmberHUD/releases/download/1.0.12/manifest.json","digest":"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""";

    private static string Release(string tag, params string[] assets) =>
        $$"""{"tag_name":"{{tag}}","assets":[{{string.Join(",", assets)}}]}""";

    private static (ReleaseInfo? Info, List<string> Reasons) Parse(string json)
    {
        var reasons = new List<string>();
        var info = GitHubRelease.Parse(json, reasons.Add);
        return (info, reasons);
    }

    [Fact]
    public void ParsesTagZipManifestAndDigests()
    {
        var (info, reasons) = Parse(Release("1.0.12", ZipAsset, ManifestAsset));

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 0, 12), info.Version);
        Assert.Equal("EmberHUD-20261016-v1.0.12.zip", info.ZipName);
        Assert.Equal("https://github.com/syrairc/EmberHUD/releases/download/1.0.12/EmberHUD-20261016-v1.0.12.zip", info.ZipUrl);
        Assert.Equal("7da1c7a36d59cfea3cd7ed62023acccc410bdc7c8bf6e4ca6ae3d500ed0b1f48", info.ZipSha256);
        Assert.Equal("https://github.com/syrairc/EmberHUD/releases/download/1.0.12/manifest.json", info.ManifestUrl);
        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", info.ManifestSha256);
        Assert.Empty(reasons);
    }

    [Fact]
    public void VPrefixedTagIsRejected()
    {
        var (info, reasons) = Parse(Release("v1.0.12", ZipAsset, ManifestAsset));

        Assert.Null(info);
        Assert.NotEmpty(reasons);
    }

    [Fact]
    public void NonVersionTagIsRejected()
    {
        var (info, reasons) = Parse(Release("abc", ZipAsset, ManifestAsset));

        Assert.Null(info);
        Assert.NotEmpty(reasons);
    }

    [Fact]
    public void MissingZipAssetIsRejected()
    {
        var (info, reasons) = Parse(Release("1.0.12", ManifestAsset));

        Assert.Null(info);
        Assert.Contains(reasons, reason => reason.Contains("zip"));
    }

    [Fact]
    public void MissingManifestAssetIsRejected()
    {
        var (info, reasons) = Parse(Release("1.0.12", ZipAsset));

        Assert.Null(info);
        Assert.Contains(reasons, reason => reason.Contains("manifest"));
    }

    [Fact]
    public void MissingOrNonSha256DigestIsRejected()
    {
        const string noDigest = """{"name":"manifest.json","browser_download_url":"https://github.com/syrairc/EmberHUD/releases/download/1.0.12/manifest.json"}""";

        Assert.Null(Parse(Release("1.0.12", ZipAsset, noDigest)).Info);
        Assert.Null(Parse(Release("1.0.12", ZipAsset, ManifestAsset.Replace("sha256:", "md5:"))).Info);
        Assert.Null(Parse(Release("1.0.12", ZipAsset.Replace("sha256:", "sha256:xyz"), ManifestAsset)).Info);
    }

    [Fact]
    public void NonHttpsDownloadIsRejected()
    {
        var (info, reasons) = Parse(Release("1.0.12", ZipAsset.Replace("https://", "http://"), ManifestAsset));

        Assert.Null(info);
        Assert.NotEmpty(reasons);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"tag_name":5,"assets":[]}""")]
    [InlineData("""{"tag_name":"1.0.12","assets":{}}""")]
    public void JunkJsonReportsAndReturnsNull(string json)
    {
        var (info, reasons) = Parse(json);

        Assert.Null(info);
        Assert.NotEmpty(reasons);
    }
}
