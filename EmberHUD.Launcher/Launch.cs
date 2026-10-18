using System.Diagnostics;
using Microsoft.Win32;

namespace EmberHUD.Launcher;

public static class Launch
{
    public static string GameInner(GameInstall game) =>
        $"set __COMPAT_LAYER=RunAsInvoker&& cd /d \"{game.Binaries}\" && start /high {GameInstall.ExeName} -SkipGameLauncher -UserDir=Torchlight{(game.Build is { } build ? $" -Version={build}" : "")}";

    public static ProcessStartInfo Game(GameInstall game, string runAsUser) =>
        game.Steam
            ? new ProcessStartInfo($"steam://rungameid/{GameInstall.SteamAppId}") { UseShellExecute = true }
            : runAsUser.Length == 0
                ? new ProcessStartInfo("cmd") { Arguments = "/c " + GameInner(game), UseShellExecute = false, CreateNoWindow = true }
                : RunAs(runAsUser, "cmd /c " + GameInner(game));

    public const string CompatLayersKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    public static string? WithRunAsInvoker(string? layers)
    {
        var old = (layers ?? "~").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var flags = old.Where(flag => flag != "RUNASADMIN").ToList();
        if (!flags.Contains("RUNASINVOKER")) flags.Add("RUNASINVOKER");
        return flags.SequenceEqual(old) ? null : string.Join(' ', flags);
    }

    public const string LauncherFlag = "~ RUNASINVOKER";

    public static void RestoreElevation(GameInstall game, Action<string> log)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CompatLayersKey, true);
            if (key?.GetValue(game.Exe) as string != LauncherFlag) return;
            key.DeleteValue(game.Exe);
            log("game - removed the RunAsInvoker flag the launcher set for the Steam exe");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log($"game - could not remove the RunAsInvoker flag ({ex.Message})");
        }
    }

    public static bool NonElevatedApplied(GameInstall game)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CompatLayersKey);
            return (key?.GetValue(game.Exe) as string)?.Split(' ').Contains("RUNASINVOKER") == true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    public static void AllowNonElevated(GameInstall game, Action<string> log)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(CompatLayersKey);
            if (WithRunAsInvoker(key.GetValue(game.Exe) as string) is not { } layers) return;
            key.SetValue(game.Exe, layers, RegistryValueKind.String);
            log("game - set RunAsInvoker for the Steam exe so the overlay can read it");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            log($"game - could not set RunAsInvoker for the Steam exe ({ex.Message})");
        }
    }

    public static ProcessStartInfo SavePassword(string runAsUser) => RunAs(runAsUser, "cmd /c exit");

    public static TextWriter OpenLog(string root, string name)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            return new StreamWriter(new FileStream(Path.Combine(root, "Logs", name + ".log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return TextWriter.Null;
        }
    }

    static ProcessStartInfo RunAs(string user, string command)
    {
        var start = new ProcessStartInfo("runas") { UseShellExecute = false };
        start.ArgumentList.Add($"/user:{user}");
        start.ArgumentList.Add("/savecred");
        start.ArgumentList.Add(command);
        return start;
    }

    public static List<(string Name, ProcessStartInfo Start)> Steps(LauncherConfig config, GameInstall game, string root, Func<string, bool> isRunning, Action<string> log)
    {
        List<(string Name, ProcessStartInfo Start)> steps = [];
        if (!config.LaunchGame) log("game - off");
        else if (isRunning(GameInstall.ExeName)) log("game - already running");
        else
        {
            if (game.Steam && config.RunAsUser.Length > 0) log("game - RunAsUser is ignored for a Steam install, Steam starts it as the current user");
            else if (!game.Steam && game.Build is null) log("game - no CurrentBuild in live_patch_version.json, starting without -Version");
            steps.Add(("game", Game(game, config.RunAsUser)));
        }

        if (!config.LaunchEmberHUD) log("EmberHUD - off");
        else if (isRunning("Ember.exe")) log("EmberHUD - already running");
        else steps.Add(("EmberHUD", new ProcessStartInfo(Path.Combine(root, "Ember.exe")) { Arguments = config.EmberProcessName.Length > 0 ? $"--as \"{config.EmberProcessName}\"" : "", UseShellExecute = config.RunAsAdmin, Verb = config.RunAsAdmin ? "runas" : "", WorkingDirectory = root }));

        foreach (var program in config.Programs.Where(program => program.Enabled && program.Path.Length > 0))
        {
            if (isRunning(Path.GetFileName(program.Path))) log($"{program.Path} - already running");
            else steps.Add((program.Path, new ProcessStartInfo(program.Path, program.Arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(program.Path)) ?? "" }));
        }
        return steps;
    }
}
