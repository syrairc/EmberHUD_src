using System.Diagnostics;

namespace EmberHUD.Updater.Tests;

public class UpdaterRunTests
{
    sealed class Fake
    {
        public bool Exited = true;
        public List<string> Blockers = [];
        public List<string> Prompts = [];
        public Func<string, bool> PromptAnswer = _ => false;
        public List<ProcessStartInfo> Started = [];
        public int Sleeps;

        public UpdaterSeams Seams => new(
            _ => Exited,
            _ => Blockers,
            message => { Prompts.Add(message); return PromptAnswer(message); },
            Started.Add,
            (_, _, _) => { },
            _ => Sleeps++);
    }

    static (TempDir Install, TempDir Updates) Layout(bool stage = true)
    {
        var install = new TempDir();
        install.File("a.txt", "old a");
        install.File("EmberLauncher.exe", "launcher");
        var updates = new TempDir();
        if (stage)
        {
            var files = Path.Combine(updates.Path, "1.0.5", "files");
            updates.File("1.0.5/files/a.txt", "new a");
            updates.File("1.0.5/files/sub/b.txt", "new b");
            updates.File("1.0.5/files/extra.txt", "not in manifest");
            var manifestPath = Path.Combine(updates.Path, "1.0.5", "manifest.json");
            Manifest.Build(files, "1.0.5", p => p != "extra.txt").Save(manifestPath);
            new StagedUpdate { Version = "1.0.5", FilesDirectory = files, ManifestPath = manifestPath }.Save(updates.Path);
        }
        return (install, updates);
    }

    static UpdaterArgs Args(TempDir install, TempDir updates, string mode = "quiet", bool overwrite = false) => new(install.Path, updates.Path, mode, 1234, overwrite);

    static string Read(TempDir dir, string relative) => File.ReadAllText(Path.Combine(dir.Path, relative));

    [Fact]
    public void ArgsParseAllSwitches()
    {
        var args = UpdaterArgs.Parse(["--install", "C:\\i", "--updates", "C:\\u", "--mode", "restart", "--pid", "42", "--overwrite-plugins"]);
        Assert.Equal(new UpdaterArgs("C:\\i", "C:\\u", "restart", 42, true), args);
        Assert.False(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid", "1"])!.OverwritePlugins);
    }

    [Fact]
    public void ArgsRejectMissingOrUnknown()
    {
        Assert.Null(UpdaterArgs.Parse([]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet"]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "loud", "--pid", "1"]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid", "x"]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid", "1", "--what"]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid"]));
    }

    [Fact]
    public void SuccessReplacesFilesWritesResultAndCleansUp()
    {
        var (install, updates) = Layout();
        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("updated", result.Outcome);
        Assert.Equal("1.0.5", result.Version);
        Assert.Equal("new a", Read(install, "a.txt"));
        Assert.Equal("new b", Read(install, "sub/b.txt"));
        Assert.False(File.Exists(Path.Combine(install.Path, "extra.txt")));
        Assert.Equal("1.0.5", Manifest.Load(Path.Combine(install.Path, "manifest.json"), _ => { })!.Version);
        Assert.False(Directory.Exists(Path.Combine(updates.Path, "backup")));
        Assert.False(Directory.Exists(Path.Combine(updates.Path, "1.0.5")));
        Assert.False(File.Exists(Path.Combine(updates.Path, "state.json")));
        Assert.Equal("updated", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void ModifiedPluginsKeptAreListedInTheResult()
    {
        var (install, updates) = Layout();
        new Manifest { Version = "1.0.4", Files = { ["Plugins/P/x.cs"] = "shipped" } }.Save(Path.Combine(install.Path, "manifest.json"));
        install.File("Plugins/P/x.cs", "user edit");
        var staged = StagedUpdate.Load(updates.Path)!;
        var pluginFile = Path.Combine(staged.FilesDirectory, "Plugins", "P", "x.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(pluginFile)!);
        File.WriteAllText(pluginFile, "upstream");
        var incoming = Manifest.Load(staged.ManifestPath, _ => { })!;
        incoming.Files["Plugins/P/x.cs"] = Manifest.Hash(pluginFile);
        incoming.Save(staged.ManifestPath);

        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("updated", result.Outcome);
        Assert.Equal(["Plugins/P/x.cs"], result.KeptPlugins);
        Assert.Equal("user edit", Read(install, "Plugins/P/x.cs"));
        Assert.Equal(["Plugins/P/x.cs"], UpdateResult.Load(updates.Path)!.KeptPlugins);
    }

    [Fact]
    public void RestartModeStartsTheLauncherAfterSuccess()
    {
        var (install, updates) = Layout();
        var fake = new Fake();
        new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));

        var started = Assert.Single(fake.Started);
        Assert.Equal(Path.Combine(install.Path, "EmberLauncher.exe"), started.FileName);
        Assert.True(started.UseShellExecute);
        Assert.Equal(install.Path, started.WorkingDirectory);
    }

    [Fact]
    public void RestartModeSavesTheResultBeforeStartingTheLauncher()
    {
        var (install, updates) = Layout();
        string? outcomeAtStart = null;
        var seams = new Fake().Seams with { Start = _ => outcomeAtStart = UpdateResult.Load(updates.Path)?.Outcome };
        new UpdaterRun(seams, _ => { }).Run(Args(install, updates, "restart"));
        Assert.Equal("updated", outcomeAtStart);
    }

    [Fact]
    public void QuietModeNeverStartsAnything()
    {
        var (install, updates) = Layout();
        var fake = new Fake();
        new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "quiet"));
        Assert.Empty(fake.Started);
    }

    [Fact]
    public void LauncherMissingFallsBackToEmberExe()
    {
        var (install, updates) = Layout();
        File.Delete(Path.Combine(install.Path, "EmberLauncher.exe"));
        var fake = new Fake();
        new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));
        Assert.Equal(Path.Combine(install.Path, "Ember.exe"), Assert.Single(fake.Started).FileName);
    }

    [Fact]
    public void FailureKeepsTheStagedUpdateAndRestoresFiles()
    {
        var (install, updates) = Layout();
        using var held = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var fake = new Fake();
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));
        held.Dispose();

        Assert.Equal("failed", result.Outcome);
        Assert.Contains("a.txt", result.Reason);
        Assert.Equal("old a", Read(install, "a.txt"));
        Assert.False(File.Exists(Path.Combine(install.Path, "sub", "b.txt")));
        Assert.NotNull(StagedUpdate.Load(updates.Path));
        Assert.Empty(fake.Started);
        Assert.Equal("failed", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void QuietLockedFileSleepsAndRetriesFiveTimes()
    {
        var (install, updates) = Layout();
        using var held = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var fake = new Fake();
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates));
        Assert.Equal("failed", result.Outcome);
        Assert.Equal(4, fake.Sleeps);
    }

    [Fact]
    public void NothingStagedFails()
    {
        var (install, updates) = Layout(stage: false);
        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));
        Assert.Equal("failed", result.Outcome);
        Assert.Equal("nothing staged", result.Reason);
        Assert.Equal("failed", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void HostThatDoesNotExitFails()
    {
        var (install, updates) = Layout();
        var result = new UpdaterRun(new Fake { Exited = false }.Seams, _ => { }).Run(Args(install, updates));
        Assert.Equal("failed", result.Outcome);
        Assert.Equal("overlay did not exit", result.Reason);
        Assert.Equal("old a", Read(install, "a.txt"));
    }

    [Fact]
    public void RunningBlockerPromptsInRestartMode()
    {
        var (install, updates) = Layout();
        var fake = new Fake { Blockers = ["Ember"] };
        fake.PromptAnswer = _ => { fake.Blockers = []; return true; };
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));

        Assert.Equal("updated", result.Outcome);
        Assert.Equal(["close Ember and retry"], fake.Prompts);

        var (install2, updates2) = Layout();
        var cancelled = new UpdaterRun(new Fake { Blockers = ["Ember"] }.Seams, _ => { }).Run(Args(install2, updates2, "restart"));
        Assert.Equal("failed", cancelled.Outcome);
        Assert.Equal("cancelled", cancelled.Reason);
        Assert.Equal("old a", Read(install2, "a.txt"));
    }

    [Fact]
    public void QuietModeGivesUpOnABlockerAfterTenTries()
    {
        var (install, updates) = Layout();
        var fake = new Fake { Blockers = ["EmberSetup"] };
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "quiet"));

        Assert.Equal("failed", result.Outcome);
        Assert.Contains("EmberSetup", result.Reason);
        Assert.Equal(10, fake.Sleeps);
        Assert.Empty(fake.Prompts);
        Assert.Equal("old a", Read(install, "a.txt"));
    }

    [Fact]
    public void ArgsRejectNonPositivePid()
    {
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid", "0"]));
        Assert.Null(UpdaterArgs.Parse(["--install", "i", "--updates", "u", "--mode", "quiet", "--pid", "-5"]));
    }

    [Fact]
    public void StartFailureAfterSuccessfulSwapStaysUpdated()
    {
        var (install, updates) = Layout();
        var seams = new Fake().Seams with { Start = _ => throw new InvalidOperationException("no launcher") };
        var logged = new List<string>();
        var result = new UpdaterRun(seams, logged.Add).Run(Args(install, updates, "restart"));

        Assert.Equal("updated", result.Outcome);
        Assert.Equal("", result.Reason);
        Assert.Contains(logged, line => line.Contains("no launcher"));
        Assert.Equal("new a", Read(install, "a.txt"));
        Assert.False(File.Exists(Path.Combine(updates.Path, "state.json")));
        Assert.Equal("updated", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void ThrowingWaitForExitStillWritesAFailedResult()
    {
        var (install, updates) = Layout();
        var seams = new Fake().Seams with { WaitForExit = _ => throw new InvalidOperationException("lookup blew up") };
        var result = new UpdaterRun(seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("failed", result.Outcome);
        Assert.Equal("1.0.5", result.Version);
        Assert.Contains("lookup blew up", result.Reason);
        Assert.Equal("failed", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void StagedFolderOutsideTheUpdatesFolderIsNeverDeleted()
    {
        var (install, updates) = Layout(stage: false);
        using var elsewhere = new TempDir();
        var files = Path.Combine(elsewhere.Path, "evil", "files");
        elsewhere.File("evil/files/a.txt", "new a");
        var manifestPath = Path.Combine(elsewhere.Path, "evil", "manifest.json");
        Manifest.Build(files, "9.9.9", _ => true).Save(manifestPath);
        new StagedUpdate { Version = "9.9.9", FilesDirectory = files, ManifestPath = manifestPath }.Save(updates.Path);

        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("updated", result.Outcome);
        Assert.True(Directory.Exists(Path.Combine(elsewhere.Path, "evil")));
    }

    static UpdateResult FailWithIncompleteRollback(TempDir install, TempDir updates)
    {
        install.File("sub/b.txt", "old b");
        using var heldB = new FileStream(Path.Combine(install.Path, "sub", "b.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        FileStream? heldA = null;
        var fake = new Fake { PromptAnswer = _ => { heldA = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; } };
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));
        heldA?.Dispose();
        return result;
    }

    [Fact]
    public void BackupFromAnIncompleteRollbackIsKeptAsBackupIncomplete()
    {
        var (install, updates) = Layout();
        updates.File("backup.incomplete/old.txt", "older");

        var result = FailWithIncompleteRollback(install, updates);

        Assert.Contains("ROLLBACK INCOMPLETE", result.Reason);
        Assert.Equal("old a", Read(updates, "backup.incomplete/a.txt"));
        Assert.False(File.Exists(Path.Combine(updates.Path, "backup.incomplete", "old.txt")));
        Assert.False(Directory.Exists(Path.Combine(updates.Path, "backup")));
    }

    [Fact]
    public void LaterEarlyFailureLeavesBackupIncompleteIntact()
    {
        var (install, updates) = Layout();
        FailWithIncompleteRollback(install, updates);

        var second = new UpdaterRun(new Fake { Exited = false }.Seams, _ => { }).Run(Args(install, updates));
        Assert.Equal("overlay did not exit", second.Reason);
        StagedUpdate.Clear(updates.Path);
        var third = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("nothing staged", third.Reason);
        Assert.Equal("old a", Read(updates, "backup.incomplete/a.txt"));
    }

    [Fact]
    public void OddFilesDirectoryAfterASuccessfulSwapStaysUpdated()
    {
        var (install, updates) = Layout();
        var staged = StagedUpdate.Load(updates.Path)!;
        staged.FilesDirectory = Path.Combine(staged.FilesDirectory, "..", "files");
        staged.Save(updates.Path);

        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("updated", result.Outcome);
        Assert.Equal("new a", Read(install, "a.txt"));
    }

    [Fact]
    public void RollbackIncompleteReasonReachesTheResultUnchanged()
    {
        var (install, updates) = Layout();
        install.File("sub/b.txt", "old b");
        using var heldB = new FileStream(Path.Combine(install.Path, "sub", "b.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        FileStream? heldA = null;
        var fake = new Fake { PromptAnswer = _ => { heldA = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; } };
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));
        heldA?.Dispose();

        Assert.Equal("failed", result.Outcome);
        Assert.Contains("ROLLBACK INCOMPLETE", result.Reason);
        Assert.Equal(result.Reason, UpdateResult.Load(updates.Path)!.Reason);
    }

    [Fact]
    public void RestartPromptReturningTrueOnceRetriesTheLockedFile()
    {
        var (install, updates) = Layout();
        var held = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var fake = new Fake { PromptAnswer = _ => { held.Dispose(); return true; } };
        var result = new UpdaterRun(fake.Seams, _ => { }).Run(Args(install, updates, "restart"));

        Assert.Equal("updated", result.Outcome);
        Assert.Single(fake.Prompts);
        Assert.Equal("new a", Read(install, "a.txt"));
    }

    [Fact]
    public void GarbageStagedManifestGivesAFailedResult()
    {
        var (install, updates) = Layout();
        updates.File("1.0.5/manifest.json", "{ nope");
        var result = new UpdaterRun(new Fake().Seams, _ => { }).Run(Args(install, updates));

        Assert.Equal("failed", result.Outcome);
        Assert.Equal("old a", Read(install, "a.txt"));
        Assert.NotNull(StagedUpdate.Load(updates.Path));
        Assert.Equal("failed", UpdateResult.Load(updates.Path)!.Outcome);
    }

    [Fact]
    public void ResultLoadReturnsNullOnMissingOrBadJson()
    {
        using var updates = new TempDir();
        Assert.Null(UpdateResult.Load(updates.Path));
        updates.File("result.json", "{ nope");
        Assert.Null(UpdateResult.Load(updates.Path));
        updates.File("result.json", "null");
        Assert.Null(UpdateResult.Load(updates.Path));
        updates.File("result.json", """{"Outcome":null,"Version":null,"Reason":null,"KeptPlugins":null}""");
        Assert.Null(UpdateResult.Load(updates.Path));
    }
}
