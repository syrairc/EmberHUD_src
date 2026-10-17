using System.Diagnostics;

namespace EmberHUD.Updater;

public sealed record UpdaterSeams(
    Func<int, bool> WaitForExit,
    Func<string, IReadOnlyList<string>> RunningBlockers,
    Func<string, bool> Prompt,
    Action<ProcessStartInfo> Start,
    Action<int, int, string> Progress,
    Action<TimeSpan> Sleep);

public sealed class UpdaterRun(UpdaterSeams seams, Action<string> log)
{
    const int QuietBlockerRetries = 10;
    const int QuietLockAttempts = 5;

    public UpdateResult Run(UpdaterArgs args)
    {
        UpdateResult result;
        try
        {
            result = Execute(args);
        }
        catch (Exception ex)
        {
            result = Failed(StagedUpdate.Load(args.Updates)?.Version ?? "unknown", ex.Message);
        }
        log($"{result.Outcome} {result.Version} {result.Reason}".TrimEnd());
        try
        {
            result.Save(args.Updates);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"result.json could not be written - {ex.Message}");
        }
        if (args.Mode == "restart" && result.Outcome == "updated") Relaunch(args.Install);
        return result;
    }

    static UpdateResult Failed(string version, string reason) => new() { Outcome = "failed", Version = version, Reason = reason };

    UpdateResult Execute(UpdaterArgs args)
    {
        var restart = args.Mode == "restart";
        if (StagedUpdate.Load(args.Updates) is not { } staged) return Failed("", "nothing staged");
        if (!seams.WaitForExit(args.HostPid)) return Failed(staged.Version, "overlay did not exit");

        var tries = 0;
        while (seams.RunningBlockers(args.Install) is { Count: > 0 } blockers)
        {
            if (restart)
            {
                if (!seams.Prompt($"close {blockers[0]} and retry")) return Failed(staged.Version, "cancelled");
            }
            else if (++tries > QuietBlockerRetries) return Failed(staged.Version, $"{blockers[0]} is still running");
            else seams.Sleep(TimeSpan.FromSeconds(1));
        }

        SwapOutcome outcome;
        UpdatePlan plan;
        try
        {
            var incoming = Manifest.Load(staged.ManifestPath, log) ?? throw new InvalidDataException("staged manifest is unreadable");
            var installed = Manifest.Load(Path.Combine(args.Install, "manifest.json"), log);
            plan = UpdatePlan.Build(args.Install, installed, incoming, args.OverwritePlugins);
            var backup = Path.Combine(args.Updates, "backup");
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            outcome = Swap.Apply(args.Install, staged.FilesDirectory, plan, incoming, backup, restart ? OnLockedPrompt : OnLockedQuiet, seams.Progress);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return Failed(staged.Version, ex.Message);
        }
        if (!outcome.Ok)
        {
            if (outcome.Reason?.Contains("ROLLBACK INCOMPLETE") == true) KeepBackup(args.Updates);
            return Failed(staged.Version, outcome.Reason ?? "swap failed");
        }

        Cleanup(args.Updates, staged);
        return new UpdateResult { Outcome = "updated", Version = staged.Version, KeptPlugins = plan.KeptModifiedPlugins };
    }

    bool OnLockedPrompt(string path, int attempt) => seams.Prompt($"close whatever is using {path} and retry");

    bool OnLockedQuiet(string path, int attempt)
    {
        if (attempt >= QuietLockAttempts) return false;
        seams.Sleep(TimeSpan.FromSeconds(1));
        return true;
    }

    void KeepBackup(string updatesDir)
    {
        var backup = Path.Combine(updatesDir, "backup");
        var kept = backup + ".incomplete";
        try
        {
            if (!Directory.Exists(backup)) return;
            if (Directory.Exists(kept)) Directory.Delete(kept, true);
            Directory.Move(backup, kept);
        }
        catch (Exception ex)
        {
            log($"backup could not be kept - {ex.Message}");
        }
    }

    void Cleanup(string updatesDir, StagedUpdate staged)
    {
        try
        {
            var stagedDir = Path.GetFullPath(Path.GetDirectoryName(staged.FilesDirectory) ?? "");
            var ownsStagedDir = string.Equals(Path.GetDirectoryName(stagedDir), Path.GetFullPath(updatesDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
            foreach (var dir in new[] { Path.Combine(updatesDir, "backup"), ownsStagedDir ? stagedDir : "" })
                if (dir.Length > 0 && Directory.Exists(dir)) Directory.Delete(dir, true);
            StagedUpdate.Clear(updatesDir);
        }
        catch (Exception ex)
        {
            log($"cleanup incomplete - {ex.Message}");
        }
    }

    void Relaunch(string installDir)
    {
        var launcher = Path.Combine(installDir, "EmberLauncher.exe");
        var exe = File.Exists(launcher) ? launcher : Path.Combine(installDir, "Ember.exe");
        try
        {
            seams.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = installDir });
        }
        catch (Exception ex)
        {
            log($"relaunch failed - {ex.Message}");
        }
    }
}
