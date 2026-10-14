using System.Diagnostics;

namespace EmberHUD.Launcher;

public static class Launch
{
    public static string GameInner(GameInstall game) =>
        $"set __COMPAT_LAYER=RunAsInvoker&& cd /d \"{game.Binaries}\" && start /high {GameInstall.ExeName} -SkipGameLauncher -UserDir=Torchlight{(game.Build is { } build ? $" -Version={build}" : "")}";

    public static ProcessStartInfo Game(GameInstall game, string runAsUser) =>
        runAsUser.Length == 0
            ? new ProcessStartInfo("cmd") { Arguments = "/c " + GameInner(game), UseShellExecute = false, CreateNoWindow = true }
            : RunAs(runAsUser, "cmd /c " + GameInner(game));

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
            if (game.Build is null) log("game - no CurrentBuild in live_patch_version.json, starting without -Version");
            steps.Add(("game", Game(game, config.RunAsUser)));
        }

        if (!config.LaunchEmberHUD) log("EmberHUD - off");
        else if (isRunning("Ember.exe")) log("EmberHUD - already running");
        else steps.Add(("EmberHUD", new ProcessStartInfo(Path.Combine(root, "Ember.exe")) { UseShellExecute = false, WorkingDirectory = root }));

        foreach (var program in config.Programs.Where(program => program.Enabled && program.Path.Length > 0))
        {
            if (isRunning(Path.GetFileName(program.Path))) log($"{program.Path} - already running");
            else steps.Add((program.Path, new ProcessStartInfo(program.Path, program.Arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(program.Path)) ?? "" }));
        }
        return steps;
    }
}
