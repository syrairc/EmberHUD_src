namespace EmberHUD.Updater.Tests;

public class SwapTests
{
    static readonly Func<string, int, bool> Never = (_, _) => throw new InvalidOperationException("onLocked called");
    static readonly Action<int, int, string> Quiet = (_, _, _) => { };

    static string Read(TempDir dir, string relative) => File.ReadAllText(Path.Combine(dir.Path, relative));

    static (TempDir Install, TempDir Staging, TempDir Backup) Layout()
    {
        var install = new TempDir();
        install.File("a.txt", "old a");
        install.File("b.txt", "old b");
        var staging = new TempDir();
        staging.File("a.txt", "new a");
        staging.File("sub/c.txt", "new c");
        return (install, staging, new TempDir());
    }

    static UpdatePlan PlanOf(string[] replace, string[] remove) => new() { Replace = [.. replace], Remove = [.. remove] };

    [Fact]
    public void ReplacesAddsAndRemovesPerThePlan()
    {
        var (install, staging, backup) = Layout();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt", "sub/c.txt"], ["b.txt"]), new Manifest { Version = "2" }, backup.Path, Never, Quiet);
        Assert.True(outcome.Ok);
        Assert.Equal("new a", Read(install, "a.txt"));
        Assert.Equal("new c", Read(install, "sub/c.txt"));
        Assert.False(File.Exists(Path.Combine(install.Path, "b.txt")));
    }

    [Fact]
    public void WritesTheIncomingManifestLastAndOnlyOnSuccess()
    {
        var (install, staging, backup) = Layout();
        var manifestPath = Path.Combine(install.Path, "manifest.json");
        var seenWhileWorking = new List<bool>();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt"], []), new Manifest { Version = "2" }, backup.Path, Never, (_, _, _) => seenWhileWorking.Add(File.Exists(manifestPath)));
        Assert.True(outcome.Ok);
        Assert.All(seenWhileWorking, seen => Assert.False(seen));
        Assert.Equal("2", Manifest.Load(manifestPath, _ => { })!.Version);

        var (install2, staging2, backup2) = Layout();
        var failed = Swap.Apply(install2.Path, staging2.Path, PlanOf(["missing.txt"], []), new Manifest { Version = "2" }, backup2.Path, Never, Quiet);
        Assert.False(failed.Ok);
        Assert.False(File.Exists(Path.Combine(install2.Path, "manifest.json")));
    }

    [Fact]
    public void LockedFileRetriesUntilTheCallbackSaysStop()
    {
        var (install, staging, backup) = Layout();
        using var held = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var attempts = new List<int>();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt"], []), new Manifest { Version = "2" }, backup.Path, (_, attempt) => { attempts.Add(attempt); return attempt < 3; }, Quiet);
        Assert.False(outcome.Ok);
        Assert.Equal([1, 2, 3], attempts);
    }

    [Fact]
    public void DeclinedLockRollsBackEveryFileAndNewFilesAreDeleted()
    {
        var (install, staging, backup) = Layout();
        staging.File("z.txt", "new z");
        install.File("z.txt", "old z");
        var plan = PlanOf(["a.txt", "sub/c.txt", "z.txt"], ["b.txt"]);
        using var held = new FileStream(Path.Combine(install.Path, "z.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var outcome = Swap.Apply(install.Path, staging.Path, plan, new Manifest { Version = "2" }, backup.Path, (_, _) => false, Quiet);
        Assert.False(outcome.Ok);
        Assert.Equal(Path.Combine(install.Path, "z.txt"), Path.GetFullPath(outcome.Blocker!));
        Assert.Equal("old a", Read(install, "a.txt"));
        Assert.Equal("old b", Read(install, "b.txt"));
        Assert.False(File.Exists(Path.Combine(install.Path, "sub", "c.txt")));
        Assert.False(Directory.Exists(Path.Combine(install.Path, "sub")));
        held.Dispose();
        Assert.Equal("old z", Read(install, "z.txt"));
        Assert.False(File.Exists(Path.Combine(install.Path, "manifest.json")));
    }

    [Fact]
    public void UnlockingInsideTheCallbackLetsTheSwapFinish()
    {
        var (install, staging, backup) = Layout();
        var held = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt"], []), new Manifest { Version = "2" }, backup.Path, (_, _) => { held.Dispose(); return true; }, Quiet);
        Assert.True(outcome.Ok);
        Assert.Equal("new a", Read(install, "a.txt"));
    }

    [Fact]
    public void ProgressReportsEveryFile()
    {
        var (install, staging, backup) = Layout();
        var seen = new List<(int, int, string)>();
        Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt", "sub/c.txt"], ["b.txt"]), new Manifest { Version = "2" }, backup.Path, Never, (done, total, path) => seen.Add((done, total, path)));
        Assert.Equal([(1, 3, "b.txt"), (2, 3, "a.txt"), (3, 3, "sub/c.txt")], seen);
    }

    [Fact]
    public void UnsafePlanPathTouchesNothing()
    {
        var (install, staging, backup) = Layout();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt", "../evil.dll"], []), new Manifest { Version = "2" }, backup.Path, Never, Quiet);
        Assert.False(outcome.Ok);
        Assert.Equal("old a", Read(install, "a.txt"));
    }

    [Fact]
    public void RollbackFailureIsReportedInTheReason()
    {
        var (install, staging, backup) = Layout();
        staging.File("z.txt", "new z");
        install.File("z.txt", "old z");
        using var heldZ = new FileStream(Path.Combine(install.Path, "z.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        FileStream? heldA = null;
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt", "z.txt"], []), new Manifest { Version = "2" }, backup.Path, (_, _) =>
        {
            heldA = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }, Quiet);
        heldA?.Dispose();
        Assert.False(outcome.Ok);
        Assert.Contains("ROLLBACK INCOMPLETE", outcome.Reason);
        Assert.Contains("a.txt", outcome.Reason!.Split("ROLLBACK INCOMPLETE")[1]);
    }

    [Fact]
    public void RetryDoesNotBackUpAgainAndOriginalIsRestored()
    {
        var (install, staging, backup) = Layout();
        using var readOnlyHandle = new FileStream(Path.Combine(install.Path, "a.txt"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var attempts = new List<int>();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt"], []), new Manifest { Version = "2" }, backup.Path, (_, attempt) => { attempts.Add(attempt); return attempt < 2; }, Quiet);
        Assert.False(outcome.Ok);
        Assert.DoesNotContain("ROLLBACK INCOMPLETE", outcome.Reason);
        Assert.Equal([1, 2], attempts);
        Assert.Equal("old a", Read(install, "a.txt"));
        Assert.Equal("old a", Read(backup, "a.txt"));
    }

    [Fact]
    public void ThrowingProgressCallbackRollsBack()
    {
        var (install, staging, backup) = Layout();
        var outcome = Swap.Apply(install.Path, staging.Path, PlanOf(["a.txt", "sub/c.txt"], ["b.txt"]), new Manifest { Version = "2" }, backup.Path, Never, (done, _, _) =>
        {
            if (done == 2) throw new InvalidOperationException("boom");
        });
        Assert.False(outcome.Ok);
        Assert.Contains("boom", outcome.Reason);
        Assert.Equal("old a", Read(install, "a.txt"));
        Assert.Equal("old b", Read(install, "b.txt"));
        Assert.False(Directory.Exists(Path.Combine(install.Path, "sub")));
        Assert.False(File.Exists(Path.Combine(install.Path, "manifest.json")));
    }
}
