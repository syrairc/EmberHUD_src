using System.Diagnostics;
using System.Runtime.InteropServices;
using EmberHUD.Launcher;

var root = AppContext.BaseDirectory;
using var logFile = Launch.OpenLog(root, "launcher");
List<string> problems = [];
Process? runas = null;

var config = LauncherConfig.Load(Path.Combine(root, "Config", "launcher.json"), Log);
var game = config is null ? null : GameInstall.Find(config.GameDirectory, GameInstall.Registered());
if (config is null || game is null)
{
    Log(config is null ? "no usable launcher.json - opening Setup" : $"{GameInstall.ExeName} not found ({(config.GameDirectory.Length > 0 ? config.GameDirectory : "auto-detect")}) - opening Setup");
    Start("Setup", new ProcessStartInfo(Path.Combine(root, "EmberSetup.exe")) { UseShellExecute = false, WorkingDirectory = root });
}
else
{
    Log($"game {game.Folder}, build {game.Build?.ToString() ?? "unknown"}, run as {(config.RunAsUser.Length > 0 ? config.RunAsUser : "yourself")}");
    foreach (var (name, start) in Launch.Steps(config, game, root, IsRunning, Log)) Start(name, start);
}
if (runas is not null)
{
    if (!runas.WaitForExit(TimeSpan.FromMinutes(2))) Log("runas - still running after 2 minutes");
    else if (runas.ExitCode != 0)
    {
        var failure = $"game - runas failed (exit {runas.ExitCode}) - password not saved or wrong? use Save password in Setup";
        problems.Add(failure);
        Log(failure);
    }
    runas.Dispose();
}
if (problems.Count > 0) Native.MessageBox(0, string.Join("\n", problems), "EmberHUD Launcher", 0x10);
return problems.Count > 0 ? 1 : 0;

void Log(string line) => logFile.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");

void Start(string name, ProcessStartInfo start)
{
    try
    {
        var process = Process.Start(start);
        if (start.FileName == "runas" && process is not null) runas = process;
        else process?.Dispose();
        Log($"{name} - started {start.FileName}");
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
    {
        problems.Add($"{name} - {ex.Message} ({start.FileName})");
        Log($"{name} - failed - {ex.Message}");
    }
}

static bool IsRunning(string image)
{
    var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(image));
    foreach (var process in processes) process.Dispose();
    return processes.Length > 0;
}

static partial class Native
{
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBox(nint owner, string text, string caption, uint type);
}
