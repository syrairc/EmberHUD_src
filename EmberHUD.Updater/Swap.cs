namespace EmberHUD.Updater;

public sealed record SwapOutcome(bool Ok, string? Reason, string? Blocker);

public static class Swap
{
    public static SwapOutcome Apply(string installDir, string stagingDir, UpdatePlan plan, Manifest incoming, string backupDir, Func<string, int, bool> onLocked, Action<int, int, string> progress)
    {
        var work = plan.Remove.Select(p => (Path: p, Delete: true)).Concat(plan.Replace.Select(p => (Path: p, Delete: false))).ToList();
        var unsafePath = work.Select(w => w.Path).FirstOrDefault(p => !Manifest.SafeRelative(p));
        if (unsafePath is not null) return new SwapOutcome(false, $"unsafe path - {unsafePath}", null);

        var touched = new List<(string Path, bool HadBackup)>();
        var createdDirs = new List<string>();
        string? blocker = null;
        string reason;
        try
        {
            for (var i = 0; i < work.Count; i++)
            {
                var (path, delete) = work[i];
                var target = Path.Combine(installDir, path);
                var backup = Path.Combine(backupDir, path);
                var backedUp = false;
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        if (!backedUp)
                        {
                            var existed = File.Exists(target);
                            if (existed)
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                                File.Copy(target, backup, overwrite: true);
                            }
                            touched.Add((path, existed));
                            backedUp = true;
                        }
                        if (delete) File.Delete(target);
                        else
                        {
                            CreateDirectory(Path.GetDirectoryName(target)!, createdDirs);
                            File.Copy(Path.Combine(stagingDir, path), target, overwrite: true);
                        }
                        break;
                    }
                    catch (IOException ex) when (ex is not (FileNotFoundException or DirectoryNotFoundException))
                    {
                        if (onLocked(path, attempt)) continue;
                        blocker = target;
                        throw new IOException($"{path} is in use - {ex.Message}", ex);
                    }
                }
                progress(i + 1, work.Count, path);
            }
            incoming.Save(Path.Combine(installDir, "manifest.json"));
            return new SwapOutcome(true, null, null);
        }
        catch (Exception ex)
        {
            reason = ex.Message;
        }

        var failed = RollBack(installDir, backupDir, touched, createdDirs);
        if (failed.Count > 0) reason += $" ROLLBACK INCOMPLETE: {string.Join(", ", failed)}";
        return new SwapOutcome(false, reason, blocker);
    }

    static void CreateDirectory(string dir, List<string> createdDirs)
    {
        var missing = new Stack<string>();
        for (var current = dir; !Directory.Exists(current); current = Path.GetDirectoryName(current)!) missing.Push(current);
        foreach (var path in missing)
        {
            Directory.CreateDirectory(path);
            createdDirs.Add(path);
        }
    }

    static List<string> RollBack(string installDir, string backupDir, List<(string Path, bool HadBackup)> touched, List<string> createdDirs)
    {
        var failed = new List<string>();
        for (var i = touched.Count - 1; i >= 0; i--)
        {
            var (path, hadBackup) = touched[i];
            var target = Path.Combine(installDir, path);
            try
            {
                if (hadBackup)
                {
                    var saved = Path.Combine(backupDir, path);
                    if (File.Exists(target) && Manifest.Hash(target) == Manifest.Hash(saved)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(saved, target, overwrite: true);
                }
                else File.Delete(target);
            }
            catch (Exception)
            {
                failed.Add(path);
            }
        }
        for (var i = createdDirs.Count - 1; i >= 0; i--)
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(createdDirs[i]).Any()) Directory.Delete(createdDirs[i]);
            }
            catch (Exception)
            {
            }
        }
        return failed;
    }
}
