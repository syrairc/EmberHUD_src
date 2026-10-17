using System.Diagnostics;
using EmberHUD.Host.Ui;
using EmberHUD.Updater;

namespace EmberHUD.Host.Update;

internal sealed class UpdateService : IDisposable
{
    private const string LatestUrl = "https://api.github.com/repos/syrairc/EmberHUD/releases/latest";

    private readonly SdkSettings sdk;
    private readonly string root;
    private readonly string updatesDir;
    private readonly Version installed;
    private readonly HttpClient http;
    private readonly Func<ProcessStartInfo, Process?> start;
    private readonly Action<string> log;
    private readonly string manifestPath;
    private readonly TimeSpan tick;
    private string status;
    private bool disposed;
    private readonly SemaphoreSlim checking = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private Thread? loop;

    public UpdateService(SdkSettings sdk, string root, Version installed, HttpClient http, Func<ProcessStartInfo, Process?> start, Action<string> log, TimeSpan? tick = null)
    {
        this.sdk = sdk;
        this.root = root;
        this.installed = installed;
        this.http = http;
        this.start = start;
        this.log = log;
        this.tick = tick ?? TimeSpan.FromSeconds(30);
        manifestPath = Path.Combine(root, "manifest.json");
        updatesDir = Path.Combine(root, "Updates");
        status = "not checked yet";
        LastResult = UpdateResult.Load(updatesDir);
        if (!Enabled) return;
        Staged = StagedUpdate.Load(updatesDir);
        if (Staged is not null && (!Version.TryParse(Staged.Version, out var stagedVersion) || stagedVersion <= installed))
        {
            Staged = null;
            try
            {
                StagedUpdate.Clear(updatesDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log($"update - could not clear stale state: {ex.Message}");
            }
        }
        if (Staged is not null) status = ReadyStatus(Staged.Version);
    }

    public static Version InstalledVersion(string root, Version assemblyVersion, Action<string> log) =>
        Version.TryParse(Manifest.Load(Path.Combine(root, "manifest.json"), log)?.Version, out var shipped) ? shipped : new Version(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(assemblyVersion.Build, 0));

    public string Status
    {
        get => !sdk.UpdaterEnabled ? "updates are turned off" : !File.Exists(manifestPath) ? "updates unavailable - this is not a release install" : status;
        private set => status = value;
    }

    public bool Enabled => sdk.UpdaterEnabled && File.Exists(manifestPath);
    public Version Installed => installed;
    public ReleaseInfo? Available { get; private set; }
    public StagedUpdate? Staged { get; private set; }
    public UpdateResult? LastResult { get; private set; }
    public bool RestartRequested { get; private set; }

    public static TimeSpan Interval(int hours) => TimeSpan.FromHours(Math.Clamp(hours, 1, 168));

    private static string ReadyStatus(object version) => $"{version} is ready - installs when you close the overlay";

    public void Start()
    {
        if (!File.Exists(manifestPath) || loop is not null) return;
        loop = new Thread(Loop) { IsBackground = true, Name = "UpdateService" };
        loop.Start();
    }

    private void Loop()
    {
        try
        {
            DateTime? lastAttempt = null;
            while (!stop.IsCancellationRequested)
            {
                if (!Enabled) lastAttempt = null;
                else if (lastAttempt is not { } last || DateTime.UtcNow - last >= Interval(sdk.UpdateIntervalHours))
                {
                    lastAttempt = DateTime.UtcNow;
                    CheckOnce().GetAwaiter().GetResult();
                }
                stop.Token.WaitHandle.WaitOne(tick);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    public async Task CheckOnce(bool force = false)
    {
        if (!Enabled || !await checking.WaitAsync(0)) return;
        try
        {
            await Check(force, stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Fail($"update check failed - {ex.Message}");
        }
        finally
        {
            checking.Release();
        }
    }

    private async Task Check(bool force, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl);
        request.Headers.UserAgent.ParseAdd("EmberHUD-Updater");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            Fail($"update check failed - GitHub answered {(int)response.StatusCode}");
            return;
        }
        string? problem = null;
        if (GitHubRelease.Parse(await response.Content.ReadAsStringAsync(ct), reason => problem = reason) is not { } release)
        {
            Fail($"update check ignored the release - {problem}");
            return;
        }
        if (release.Version < installed || release.Version == installed && !force)
        {
            Available = null;
            if (Staged is null) Status = $"up to date ({installed})";
            return;
        }
        Available = release;
        if (Staged is not null && Version.TryParse(Staged.Version, out var stagedVersion) && stagedVersion == release.Version)
        {
            Status = ReadyStatus(release.Version);
            return;
        }
        Staged = null;
        Status = $"downloading {release.Version}";
        string? stageProblem = null;
        var staged = await new UpdateStager(http, updatesDir).Stage(release, reason => stageProblem = reason, ct);
        if (staged is null)
        {
            Fail($"update {release.Version} could not be staged - {stageProblem}");
            return;
        }
        Staged = staged;
        Status = ReadyStatus(release.Version);
        log($"update - {Status}");
    }

    private void Fail(string reason)
    {
        Status = reason;
        log($"update - {reason}");
    }

    public void RequestRestart() => RestartRequested = true;

    public void DismissResult()
    {
        LastResult = null;
        try
        {
            UpdateResult.Delete(updatesDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"update - could not delete result.json: {ex.Message}");
        }
    }

    public void LaunchOnExit()
    {
        if (Staged is null || !sdk.UpdaterEnabled) return;
        try
        {
            var run = Path.Combine(updatesDir, "run");
            if (Directory.Exists(run)) Directory.Delete(run, true);
            Directory.CreateDirectory(run);
            foreach (var file in Directory.GetFiles(root))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("Ember.", StringComparison.OrdinalIgnoreCase) || name.StartsWith("EmberSetup.", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(file, Path.Combine(run, name), true);
            }
            foreach (var folder in new[] { "Fonts", "Themes" })
            {
                if (Directory.Exists(Path.Combine(root, folder))) CopyDirectory(Path.Combine(root, folder), Path.Combine(run, folder));
            }

            var info = new ProcessStartInfo(Path.Combine(run, "EmberUpdater.exe")) { UseShellExecute = false, WorkingDirectory = run };
            foreach (var argument in new[] { "--install", root, "--updates", updatesDir, "--mode", RestartRequested ? "restart" : "quiet", "--pid", Environment.ProcessId.ToString() }) info.ArgumentList.Add(argument);
            if (sdk.OverwriteModifiedPlugins) info.ArgumentList.Add("--overwrite-plugins");
            start(info);
        }
        catch (Exception ex)
        {
            log($"update - could not launch the updater: {ex.Message}");
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stop.Cancel();
        if (loop?.Join(TimeSpan.FromSeconds(5)) ?? true) stop.Dispose();
    }
}
