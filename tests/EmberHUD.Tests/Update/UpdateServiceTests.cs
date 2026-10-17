using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmberHUD.Host.Ui;
using EmberHUD.Host.Update;
using EmberHUD.Updater;

namespace EmberHUD.Tests;

public class UpdateServiceTests
{
    const string LatestUrl = "https://api.github.com/repos/syrairc/EmberHUD/releases/latest";

    sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    static HttpResponseMessage Ok(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    static FakeHandler Releasing(string tag)
    {
        var manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Manifest { Version = tag, Files = { ["a.txt"] = Sha("alpha"u8.ToArray()) } }, new JsonSerializerOptions { IncludeFields = true }));
        var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            using (var stream = archive.CreateEntry("a.txt").Open()) stream.Write("alpha"u8);
            using (var stream = archive.CreateEntry("manifest.json").Open()) stream.Write(manifestBytes);
        }
        var zipBytes = zip.ToArray();
        var json = JsonSerializer.Serialize(new
        {
            tag_name = tag,
            assets = new[]
            {
                new { name = $"EmberHUD-x-v{tag}.zip", browser_download_url = "https://x/z.zip", digest = "sha256:" + Sha(zipBytes) },
                new { name = "manifest.json", browser_download_url = "https://x/manifest.json", digest = "sha256:" + Sha(manifestBytes) },
            },
        });
        return new FakeHandler(request => request.RequestUri!.ToString() switch
        {
            LatestUrl => Ok(Encoding.UTF8.GetBytes(json)),
            "https://x/z.zip" => Ok(zipBytes),
            "https://x/manifest.json" => Ok(manifestBytes),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
    }

    sealed class Setup : IDisposable
    {
        public TempDir Root = new();
        public List<string> Logs = [];
        public List<ProcessStartInfo> Started = [];
        public SdkSettings Sdk = new();

        public Setup(bool manifest = true)
        {
            if (manifest) Root.File("manifest.json", "{}");
        }

        public UpdateService Create(HttpMessageHandler handler, string installed = "1.0.6", TimeSpan? tick = null) =>
            new(Sdk, Root.Path, Version.Parse(installed), new HttpClient(handler), info =>
            {
                Started.Add(info);
                return null;
            }, Logs.Add, tick);

        public void Dispose() => Root.Dispose();
    }

    [Fact]
    public async Task DisabledWithoutAManifestInTheRoot()
    {
        using var setup = new Setup(manifest: false);
        var handler = Releasing("1.0.7");
        using var service = setup.Create(handler);
        await service.CheckOnce();
        Assert.Empty(handler.Requests);
        Assert.Null(service.Available);
        Assert.Contains("release", service.Status);
    }

    [Fact]
    public async Task DisabledWhenTheToggleIsOff()
    {
        using var setup = new Setup();
        setup.Sdk.UpdaterEnabled = false;
        var handler = Releasing("1.0.7");
        using var service = setup.Create(handler);
        await service.CheckOnce();
        Assert.Empty(handler.Requests);
        Assert.Contains("off", service.Status);
    }

    [Fact]
    public async Task NewerReleaseIsStagedAndReported()
    {
        using var setup = new Setup();
        var handler = Releasing("1.0.7");
        using var service = setup.Create(handler);
        await service.CheckOnce();
        Assert.Equal(new Version(1, 0, 7), service.Available!.Version);
        Assert.Equal("1.0.7", service.Staged!.Version);
        Assert.Contains("1.0.7", service.Status);
        var request = handler.Requests[0];
        Assert.Contains(request.Headers.UserAgent, product => product.ToString() == "EmberHUD-Updater");
        Assert.Contains(request.Headers.Accept, media => media.MediaType == "application/vnd.github+json");
        Assert.Equal("1.0.7", StagedUpdate.Load(Path.Combine(setup.Root.Path, "Updates"))!.Version);
    }

    [Theory]
    [InlineData("1.0.6")]
    [InlineData("1.0.5")]
    public async Task SameOrOlderReleaseIsIgnored(string tag)
    {
        using var setup = new Setup();
        using var service = setup.Create(Releasing(tag));
        await service.CheckOnce();
        Assert.Null(service.Available);
        Assert.Null(service.Staged);
        Assert.False(Directory.Exists(Path.Combine(setup.Root.Path, "Updates", tag)));
    }

    [Fact]
    public async Task FourPartAssemblyVersionDoesNotLookNewerThanTheSameTag()
    {
        using var setup = new Setup();
        var assembly = new Version(1, 0, 19, 0);
        using var service = setup.Create(Releasing("1.0.19"), new Version(assembly.Major, assembly.Minor, Math.Max(assembly.Build, 0)).ToString());
        await service.CheckOnce();
        Assert.Null(service.Available);
    }

    [Fact]
    public async Task NetworkFailureSetsStatusWithoutThrowing()
    {
        using var setup = new Setup();
        using var service = setup.Create(new FakeHandler(_ => throw new HttpRequestException("no route")));
        await service.CheckOnce();
        Assert.Contains("no route", service.Status);
        Assert.Null(service.Staged);
        Assert.NotEmpty(setup.Logs);
    }

    [Fact]
    public async Task RateLimit403SetsStatusWithoutThrowing()
    {
        using var setup = new Setup();
        using var service = setup.Create(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        await service.CheckOnce();
        Assert.Contains("403", service.Status);
        Assert.Null(service.Available);
    }

    [Fact]
    public void IntervalClampsToTheFloorAndCeiling()
    {
        Assert.Equal(TimeSpan.FromHours(1), UpdateService.Interval(0));
        Assert.Equal(TimeSpan.FromHours(1), UpdateService.Interval(-5));
        Assert.Equal(TimeSpan.FromHours(1), UpdateService.Interval(1));
        Assert.Equal(TimeSpan.FromHours(24), UpdateService.Interval(24));
        Assert.Equal(TimeSpan.FromHours(168), UpdateService.Interval(100000));
    }

    static void Stage(Setup setup, string version)
    {
        var updates = Path.Combine(setup.Root.Path, "Updates");
        var files = Path.GetDirectoryName(setup.Root.File($"Updates/{version}/files/a.txt", "alpha"))!;
        var manifest = setup.Root.File($"Updates/{version}/manifest.json", "{}");
        new StagedUpdate { Version = version, FilesDirectory = files, ManifestPath = manifest }.Save(updates);
    }

    [Fact]
    public void LaunchOnExitDoesNothingWhenUpdatesAreTurnedOff()
    {
        using var setup = new Setup();
        Stage(setup, "1.0.7");
        using var service = setup.Create(Releasing("1.0.7"));
        setup.Sdk.UpdaterEnabled = false;
        service.LaunchOnExit();
        Assert.Empty(setup.Started);
    }

    [Fact]
    public async Task FailedDownloadOfNewerReleaseClearsTheDeletedStagedUpdate()
    {
        using var setup = new Setup();
        Stage(setup, "1.0.7");
        var json = JsonSerializer.Serialize(new
        {
            tag_name = "1.0.8",
            assets = new[]
            {
                new { name = "EmberHUD-x-v1.0.8.zip", browser_download_url = "https://x/z.zip", digest = "sha256:" + new string('a', 64) },
                new { name = "manifest.json", browser_download_url = "https://x/manifest.json", digest = "sha256:" + new string('b', 64) },
            },
        });
        using var service = setup.Create(new FakeHandler(request => request.RequestUri!.ToString() == LatestUrl ? Ok(Encoding.UTF8.GetBytes(json)) : new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.NotNull(service.Staged);
        await service.CheckOnce();
        Assert.Null(service.Staged);
        service.LaunchOnExit();
        Assert.Empty(setup.Started);
    }

    [Fact]
    public void InstalledVersionPrefersTheManifestAndFallsBackToTheAssembly()
    {
        using var root = new TempDir();
        var assembly = new Version(1, 0, 19, 0);
        Assert.Equal(new Version(1, 0, 19), UpdateService.InstalledVersion(root.Path, assembly, _ => { }));
        root.File("manifest.json", "not json");
        Assert.Equal(new Version(1, 0, 19), UpdateService.InstalledVersion(root.Path, assembly, _ => { }));
        root.File("manifest.json", "{\"Version\":\"1.0.5\",\"Files\":{}}");
        Assert.Equal(new Version(1, 0, 5), UpdateService.InstalledVersion(root.Path, assembly, _ => { }));
    }

    [Fact]
    public void LaunchOnExitDoesNothingWithoutAStagedUpdate()
    {
        using var setup = new Setup();
        using var service = setup.Create(Releasing("1.0.7"));
        service.LaunchOnExit();
        Assert.Empty(setup.Started);
        Assert.False(Directory.Exists(Path.Combine(setup.Root.Path, "Updates", "run")));
    }

    [Fact]
    public void LaunchOnExitCopiesTheRunFolderAndPassesQuietArgs()
    {
        using var setup = new Setup();
        foreach (var name in new[] { "Ember.exe", "Ember.dll", "EmberLauncher.exe", "EmberLauncher.dll", "EmberSetup.exe", "EmberUpdater.exe", "EmberHUD.Api.dll", "ImGui.dll" }) setup.Root.File(name, name);
        setup.Root.File("Fonts/f.ttf", "f");
        setup.Root.File("Themes/t.json", "t");
        setup.Root.File("Plugins/P/Main.cs", "p");
        Stage(setup, "1.0.7");
        using var service = setup.Create(Releasing("1.0.7"));

        service.LaunchOnExit();

        var run = Path.Combine(setup.Root.Path, "Updates", "run");
        var copied = Directory.GetFiles(run).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(["EmberHUD.Api.dll", "EmberLauncher.dll", "EmberLauncher.exe", "EmberUpdater.exe", "ImGui.dll", "manifest.json"], copied);
        Assert.True(File.Exists(Path.Combine(run, "Fonts", "f.ttf")));
        Assert.True(File.Exists(Path.Combine(run, "Themes", "t.json")));
        Assert.False(Directory.Exists(Path.Combine(run, "Plugins")));
        var info = Assert.Single(setup.Started);
        Assert.Equal(Path.Combine(run, "EmberUpdater.exe"), info.FileName);
        Assert.Equal(run, info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        var args = UpdaterArgs.Parse([.. info.ArgumentList])!;
        Assert.Equal(setup.Root.Path, args.Install);
        Assert.Equal(Path.Combine(setup.Root.Path, "Updates"), args.Updates);
        Assert.Equal("quiet", args.Mode);
        Assert.Equal(Environment.ProcessId, args.HostPid);
        Assert.False(args.OverwritePlugins);
    }

    [Fact]
    public void LaunchOnExitAfterRequestRestartPassesRestartAndOverwriteFlag()
    {
        using var setup = new Setup();
        setup.Sdk.OverwriteModifiedPlugins = true;
        setup.Root.File("EmberUpdater.exe", "u");
        Stage(setup, "1.0.7");
        using var service = setup.Create(Releasing("1.0.7"));
        service.RequestRestart();

        service.LaunchOnExit();

        var args = UpdaterArgs.Parse([.. Assert.Single(setup.Started).ArgumentList])!;
        Assert.Equal("restart", args.Mode);
        Assert.True(args.OverwritePlugins);
    }

    [Fact]
    public void StaleStagedStateIsDroppedWhenAlreadyInstalled()
    {
        using var setup = new Setup();
        Stage(setup, "1.0.6");
        using var service = setup.Create(Releasing("1.0.6"), "1.0.6");
        Assert.Null(service.Staged);
        Assert.Null(StagedUpdate.Load(Path.Combine(setup.Root.Path, "Updates")));
        service.LaunchOnExit();
        Assert.Empty(setup.Started);
    }

    [Fact]
    public void LastResultLoadsAndDismissDeletesIt()
    {
        using var setup = new Setup();
        var updates = Path.Combine(setup.Root.Path, "Updates");
        new UpdateResult { Outcome = "updated", Version = "1.0.7" }.Save(updates);
        using var service = setup.Create(Releasing("1.0.7"));
        Assert.Equal("1.0.7", service.LastResult!.Version);
        service.DismissResult();
        Assert.Null(service.LastResult);
        Assert.Null(UpdateResult.Load(updates));
    }

    static async Task<bool> Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10, TestContext.Current.CancellationToken);
        return condition();
    }

    [Fact]
    public async Task TickingTheToggleOnStartsChecksAndOffStopsThem()
    {
        using var setup = new Setup();
        setup.Sdk.UpdaterEnabled = false;
        var handler = Releasing("1.0.6");
        using var service = setup.Create(handler, tick: TimeSpan.FromMilliseconds(10));
        service.Start();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Empty(handler.Requests);

        setup.Sdk.UpdaterEnabled = true;
        Assert.True(await Eventually(() => handler.Requests.Count == 1));

        setup.Sdk.UpdaterEnabled = false;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        setup.Sdk.UpdaterEnabled = true;
        Assert.True(await Eventually(() => handler.Requests.Count == 2));
    }

    [Fact]
    public async Task ChangingTheIntervalAppliesWithoutWaitingTheOldOne()
    {
        using var setup = new Setup();
        setup.Sdk.UpdateIntervalHours = 168;
        var handler = Releasing("1.0.6");
        using var service = setup.Create(handler, tick: TimeSpan.FromMilliseconds(10));
        service.Start();
        Assert.True(await Eventually(() => handler.Requests.Count == 1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Single(handler.Requests);
        setup.Sdk.UpdateIntervalHours = 1;
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AFailedCheckStillWaitsAFullInterval()
    {
        using var setup = new Setup();
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var service = setup.Create(handler, tick: TimeSpan.FromMilliseconds(10));
        service.Start();
        Assert.True(await Eventually(() => handler.Requests.Count == 1));
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CheckNowWorksOnceTheToggleIsOn()
    {
        using var setup = new Setup();
        setup.Sdk.UpdaterEnabled = false;
        var handler = Releasing("1.0.7");
        using var service = setup.Create(handler);
        await service.CheckOnce();
        Assert.Empty(handler.Requests);
        setup.Sdk.UpdaterEnabled = true;
        Assert.True(service.Enabled);
        await service.CheckOnce();
        Assert.Equal("1.0.7", service.Staged!.Version);
    }

    [Fact]
    public void DisposeTwiceAndLaunchOnExitAfterDisposeStillLaunches()
    {
        using var setup = new Setup();
        setup.Root.File("EmberUpdater.exe", "u");
        Stage(setup, "1.0.7");
        var service = setup.Create(Releasing("1.0.7"));
        service.Start();
        service.Dispose();
        service.Dispose();
        service.LaunchOnExit();
        Assert.Single(setup.Started);
    }

    [Fact]
    public async Task StatusKeepsReadyWhileAnUpdateIsStagedAndTheLatestIsNotNewer()
    {
        using var setup = new Setup();
        Stage(setup, "1.0.7");
        using var service = setup.Create(Releasing("1.0.6"));
        await service.CheckOnce();
        Assert.Contains("ready", service.Status);
    }
}
