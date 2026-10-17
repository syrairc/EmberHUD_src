using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmberHUD.Host.Update;
using EmberHUD.Updater;

namespace EmberHUD.Tests;

public class UpdateStagerTests
{
    sealed class FakeHandler(Dictionary<string, byte[]> routes, bool timeout = false) : HttpMessageHandler
    {
        public List<bool> StateSeenPerRequest { get; } = [];
        public string? UpdatesDir;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (UpdatesDir is not null) StateSeenPerRequest.Add(File.Exists(Path.Combine(UpdatesDir, "state.json")));
            if (timeout) throw new TaskCanceledException("timed out");
            if (!routes.TryGetValue(request.RequestUri!.ToString(), out var body)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }

    sealed class Release
    {
        public Dictionary<string, byte[]> Entries = new() { ["a.txt"] = "alpha"u8.ToArray(), ["Plugins/P/Main.cs"] = "beta"u8.ToArray() };
        public Func<Manifest>? ManifestOf;
        public byte[]? ZipManifestOverride;
        public string? ZipDigestOverride;
        public string? ManifestDigestOverride;
        public bool Offline;
        public bool Timeout;
        public FakeHandler? Handler;

        public (ReleaseInfo Info, HttpClient Http, byte[] ManifestBytes) Build()
        {
            var manifest = ManifestOf?.Invoke() ?? new Manifest { Version = "1.0.7", Files = Entries.Where(e => !e.Key.Contains("..") && !e.Key.StartsWith('/') && !e.Key.Contains(':')).ToDictionary(e => e.Key, e => Sha(e.Value)) };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { IncludeFields = true }));
            var zip = new MemoryStream();
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
            {
                foreach (var (name, data) in Entries)
                {
                    using var stream = archive.CreateEntry(name).Open();
                    stream.Write(data);
                }
                using var manifestStream = archive.CreateEntry("manifest.json").Open();
                manifestStream.Write(ZipManifestOverride ?? manifestBytes);
            }
            var zipBytes = zip.ToArray();
            var routes = Offline ? [] : new Dictionary<string, byte[]> { ["https://x/z.zip"] = zipBytes, ["https://x/manifest.json"] = manifestBytes };
            var info = new ReleaseInfo(new Version(1, 0, 7), "z.zip", "https://x/z.zip", ZipDigestOverride ?? Sha(zipBytes), "https://x/manifest.json", ManifestDigestOverride ?? Sha(manifestBytes));
            Handler = new FakeHandler(routes, Timeout);
            return (info, new HttpClient(Handler), manifestBytes);
        }
    }

    static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    static Manifest OnlyA() => new() { Version = "1.0.7", Files = { ["a.txt"] = Sha("alpha"u8.ToArray()) } };

    static async Task<(StagedUpdate? Result, List<string> Reasons)> Run(Release release, string updates)
    {
        var (info, http, _) = release.Build();
        var reasons = new List<string>();
        var result = await new UpdateStager(http, updates).Stage(info, reasons.Add, CancellationToken.None);
        return (result, reasons);
    }

    static void AssertClean(string updates)
    {
        Assert.False(Directory.Exists(Path.Combine(updates, "1.0.7")));
        Assert.Empty(Directory.GetFiles(updates, "*.part", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(updates, "state.json")));
    }

    [Fact]
    public async Task StateJsonDoesNotExistDuringDownloadsAndExistsAfterStaging()
    {
        using var updates = new TempDir();
        var release = new Release();
        var (info, http, manifestBytes) = release.Build();
        release.Handler!.UpdatesDir = updates.Path;

        var staged = await new UpdateStager(http, updates.Path).Stage(info, _ => { }, CancellationToken.None);

        Assert.NotNull(staged);
        Assert.Equal("1.0.7", staged.Version);
        Assert.Equal("alpha", File.ReadAllText(Path.Combine(staged.FilesDirectory, "a.txt")));
        Assert.Equal("beta", File.ReadAllText(Path.Combine(staged.FilesDirectory, "Plugins", "P", "Main.cs")));
        Assert.False(File.Exists(Path.Combine(staged.FilesDirectory, "manifest.json")));
        Assert.Equal(manifestBytes, File.ReadAllBytes(staged.ManifestPath));
        Assert.Empty(Directory.GetFiles(updates.Path, "*.zip*", SearchOption.AllDirectories));
        Assert.NotEmpty(release.Handler.StateSeenPerRequest);
        Assert.DoesNotContain(true, release.Handler.StateSeenPerRequest);
        Assert.Equal("1.0.7", StagedUpdate.Load(updates.Path)!.Version);
    }

    [Fact]
    public async Task DigestMismatchOnTheZipDeletesEverything()
    {
        using var updates = new TempDir();
        var (result, reasons) = await Run(new Release { ZipDigestOverride = new string('0', 64) }, updates.Path);
        Assert.Null(result);
        Assert.NotEmpty(reasons);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task DigestMismatchOnTheManifestDeletesEverything()
    {
        using var updates = new TempDir();
        var (result, _) = await Run(new Release { ManifestDigestOverride = new string('0', 64) }, updates.Path);
        Assert.Null(result);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task UppercaseDigestsAreAccepted()
    {
        using var updates = new TempDir();
        var (info, http, _) = new Release().Build();
        info = info with { ZipSha256 = info.ZipSha256.ToUpperInvariant(), ManifestSha256 = info.ManifestSha256.ToUpperInvariant() };
        Assert.NotNull(await new UpdateStager(http, updates.Path).Stage(info, _ => { }, CancellationToken.None));
    }

    [Theory]
    [InlineData("..\\evil.dll")]
    [InlineData("../evil.dll")]
    [InlineData("sub/../../evil.dll")]
    public async Task ZipSlipEntryIsRejected(string entry)
    {
        using var parent = new TempDir();
        var updates = Path.Combine(parent.Path, "Updates");
        Directory.CreateDirectory(updates);
        var release = new Release { ManifestOf = OnlyA };
        release.Entries[entry] = "x"u8.ToArray();

        var (result, reasons) = await Run(release, updates);

        Assert.Null(result);
        Assert.Contains(reasons, reason => reason.Contains("evil.dll") && reason.Contains("unsafe"));
        Assert.Empty(Directory.GetFiles(parent.Path, "evil.dll", SearchOption.AllDirectories));
        AssertClean(updates);
    }

    [Theory]
    [InlineData("/evil.dll")]
    [InlineData("C:/evil.dll")]
    [InlineData("C:\\evil.dll")]
    public async Task RootedEntryIsRejected(string entry)
    {
        using var parent = new TempDir();
        var updates = Path.Combine(parent.Path, "Updates");
        Directory.CreateDirectory(updates);
        var release = new Release { ManifestOf = OnlyA };
        release.Entries[entry] = "x"u8.ToArray();

        var (result, reasons) = await Run(release, updates);

        Assert.Null(result);
        Assert.Contains(reasons, reason => reason.Contains("evil.dll") && reason.Contains("unsafe"));
        Assert.Empty(Directory.GetFiles(parent.Path, "evil.dll", SearchOption.AllDirectories));
        Assert.False(File.Exists(@"C:\evil.dll"));
        AssertClean(updates);
    }

    [Fact]
    public async Task ZipManifestDifferingFromTheAssetIsRejected()
    {
        using var updates = new TempDir();
        var (result, _) = await Run(new Release { ZipManifestOverride = "{}"u8.ToArray() }, updates.Path);
        Assert.Null(result);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task FileHashNotMatchingTheManifestIsRejected()
    {
        using var updates = new TempDir();
        var (result, _) = await Run(new Release { ManifestOf = () => new Manifest { Version = "1.0.7", Files = { ["a.txt"] = new string('0', 64) } } }, updates.Path);
        Assert.Null(result);
        Assert.Null(StagedUpdate.Load(updates.Path));
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task ManifestFileMissingFromTheZipIsRejected()
    {
        using var updates = new TempDir();
        var (result, _) = await Run(new Release { ManifestOf = () => new Manifest { Version = "1.0.7", Files = { ["a.txt"] = Sha("alpha"u8.ToArray()), ["gone.txt"] = Sha("g"u8.ToArray()) } } }, updates.Path);
        Assert.Null(result);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task ManifestPathEscapingTheInstallIsRejected()
    {
        using var updates = new TempDir();
        var (result, _) = await Run(new Release { ManifestOf = () => new Manifest { Version = "1.0.7", Files = { ["../x"] = Sha("alpha"u8.ToArray()) } } }, updates.Path);
        Assert.Null(result);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task HttpFailureReturnsNullWithoutThrowing()
    {
        using var updates = new TempDir();
        var (result, reasons) = await Run(new Release { Offline = true }, updates.Path);
        Assert.Null(result);
        Assert.NotEmpty(reasons);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task OversizedDownloadIsRejected()
    {
        using var updates = new TempDir();
        var (info, _, _) = new Release().Build();
        var http = new HttpClient(new FakeHandler(new() { ["https://x/manifest.json"] = new byte[1], ["https://x/z.zip"] = new byte[201 * 1024 * 1024] }));
        var result = await new UpdateStager(http, updates.Path).Stage(info with { ManifestSha256 = Sha(new byte[1]) }, _ => { }, CancellationToken.None);
        Assert.Null(result);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task CancellationPropagatesAndLeavesNothingBehind()
    {
        using var updates = new TempDir();
        var (info, http, _) = new Release().Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UpdateStager(http, updates.Path).Stage(info, _ => { }, cts.Token));
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task OlderStagedFolderIsDeletedWhenANewerOneStages()
    {
        using var updates = new TempDir();
        updates.File("1.0.3/files/old.txt", "old");
        updates.File("1.0.3.zip.part", "junk");
        updates.File("backup/keep.txt", "keep");
        var (result, _) = await Run(new Release(), updates.Path);
        Assert.NotNull(result);
        Assert.False(Directory.Exists(Path.Combine(updates.Path, "1.0.3")));
        Assert.False(File.Exists(Path.Combine(updates.Path, "1.0.3.zip.part")));
        Assert.True(File.Exists(Path.Combine(updates.Path, "backup", "keep.txt")));
    }

    [Fact]
    public async Task HttpTimeoutReturnsNullInsteadOfThrowing()
    {
        using var updates = new TempDir();
        var (result, reasons) = await Run(new Release { Timeout = true }, updates.Path);
        Assert.Null(result);
        Assert.NotEmpty(reasons);
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task ManifestVersionDifferingFromTheReleaseIsRejected()
    {
        using var updates = new TempDir();
        var (result, reasons) = await Run(new Release { ManifestOf = () => new Manifest { Version = "9.9.9", Files = { ["a.txt"] = Sha("alpha"u8.ToArray()) } } }, updates.Path);
        Assert.Null(result);
        Assert.Contains(reasons, reason => reason.Contains("9.9.9"));
        AssertClean(updates.Path);
    }

    [Fact]
    public async Task ZipManifestOfADifferentLengthIsRejectedBeforeReading()
    {
        using var updates = new TempDir();
        var (result, reasons) = await Run(new Release { ZipManifestOverride = new byte[5 * 1024 * 1024] }, updates.Path);
        Assert.Null(result);
        Assert.Contains(reasons, reason => reason.Contains("differs"));
        AssertClean(updates.Path);
    }
}
