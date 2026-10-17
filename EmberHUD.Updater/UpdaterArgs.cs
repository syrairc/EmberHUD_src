namespace EmberHUD.Updater;

public sealed record UpdaterArgs(string Install, string Updates, string Mode, int HostPid, bool OverwritePlugins)
{
    public static UpdaterArgs? Parse(string[] args)
    {
        string? install = null, updates = null, mode = null;
        int? pid = null;
        var overwrite = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--overwrite-plugins") overwrite = true;
            else if (i + 1 >= args.Length) return null;
            else if (args[i] == "--install") install = args[++i];
            else if (args[i] == "--updates") updates = args[++i];
            else if (args[i] == "--mode") mode = args[++i];
            else if (args[i] == "--pid" && int.TryParse(args[++i], out var value) && value > 0) pid = value;
            else return null;
        }
        if (string.IsNullOrEmpty(install) || string.IsNullOrEmpty(updates) || pid is null || mode is not ("quiet" or "restart")) return null;
        return new UpdaterArgs(install, updates, mode, pid.Value, overwrite);
    }
}
