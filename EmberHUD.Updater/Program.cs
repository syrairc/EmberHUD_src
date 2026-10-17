using System.ComponentModel;
using System.Diagnostics;
using EmberHUD.Host.Overlay;
using EmberHUD.Launcher;
using EmberHUD.Updater;
using EmberHUD.Updater.Ui;

var parsed = UpdaterArgs.Parse(args);
if (parsed is null) return 2;

var root = Path.GetFullPath(parsed.Install);
using var logFile = Launch.OpenLog(root, "updater");
var logGate = new object();
void Log(string line)
{
    lock (logGate) logFile.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
}

var state = new ProgressState();
var seams = new UpdaterSeams(
    pid =>
    {
        if (pid == Environment.ProcessId) return true;
        try
        {
            using var host = Process.GetProcessById(pid);
            return host.WaitForExit(30000);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return true;
        }
    },
    RunningBlockers,
    parsed.Mode == "restart" ? state.Ask : _ => false,
    start => Process.Start(start),
    state.Report,
    Thread.Sleep);

UpdateResult RunGuarded()
{
    try
    {
        return new UpdaterRun(seams, Log).Run(parsed);
    }
    catch (Exception ex)
    {
        Log($"updater crashed: {ex}");
        return new UpdateResult { Outcome = "failed", Reason = ex.Message };
    }
}

if (parsed.Mode == "quiet")
{
    var quiet = RunGuarded();
    if (quiet.Outcome == "updated") return 0;
    ShowFailure(quiet);
    return 1;
}

var windowFailed = false;
var worker = Task.Run(() => state.Finish(RunGuarded()));
try
{
    ProgressWindow.Run(state);
}
catch (Exception ex)
{
    Log($"window failed: {ex}");
    windowFailed = true;
}
finally
{
    state.Close();
    worker.Wait();
}
if (windowFailed && state.Result is { Outcome: not "updated" } failed) ShowFailure(failed);
return state.Result?.Outcome == "updated" ? 0 : 1;

static void ShowFailure(UpdateResult result) =>
    Win32.MessageBox(0, $"Update to v{result.Version} failed: {result.Reason}{Environment.NewLine}see Logs\\updater.log", "EmberHUD Updater", Win32.MB_ICONERROR);

static List<string> RunningBlockers(string install)
{
    var root = Path.GetFullPath(install).TrimEnd('\\') + "\\";
    var found = new List<string>();
    foreach (var name in new[] { "Ember", "EmberLauncher", "EmberSetup" })
        foreach (var process in Process.GetProcessesByName(name))
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } file && file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) found.Add(name);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                }
            }
    return found;
}
