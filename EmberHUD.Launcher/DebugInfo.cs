using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EmberHUD.Launcher;

public static partial class DebugInfo
{
    const uint QueryAndVmRead = 0x1010;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int pid);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    public const string Disclosure = "This copies the lines below to your clipboard so you can send them for troubleshooting. Nothing is sent anywhere, and it holds no user name, computer name or network info.";

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string GameAccess()
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameInstall.ExeName));
        var count = processes.Length;
        var pid = count == 1 ? processes[0].Id : 0;
        foreach (var process in processes) process.Dispose();
        if (count == 0) return "not running";
        if (count > 1) return $"{count} processes running";
        var handle = OpenProcess(QueryAndVmRead, false, pid);
        var error = Marshal.GetLastWin32Error();
        if (handle != 0)
        {
            CloseHandle(handle);
            return "running, opened for reading";
        }
        return error == 5 ? "running, access denied (the game is probably elevated)" : $"running, could not open (win32 error {error})";
    }

    public static string RedactPath(string path, string profile, string userName)
    {
        if (profile.Length > 0) path = path.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return string.Join('\\', path.Split('\\').Select(part => userName.Length > 0 && part.Equals(userName, StringComparison.OrdinalIgnoreCase) ? "<user>" : part));
    }

    public static string Describe(string source, Version emberVersion, string emberFolder, string gameFolder, string? registered, bool emberElevated, string gameAccess, string? status = null)
    {
        var game = GameInstall.Find(gameFolder, registered);
        var lines = new List<string>
        {
            $"EmberHUD: v{emberVersion} ({source})",
            $"EmberHUD binary: {SizeOf(Path.Combine(emberFolder, "Ember.dll"))}",
            $"EmberHUD elevated: {(emberElevated ? "yes" : "no")}",
        };
        if (game is null) lines.Add("Torchlight: not found");
        else
        {
            lines.Add($"Torchlight folder: {RedactPath(game.Folder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName)}");
            lines.Add($"Torchlight install: {(game.Steam ? "Steam" : "standalone")}, build {game.Build?.ToString() ?? "unknown"}");
            lines.Add($"Torchlight binary: {SizeOf(game.Exe)}");
        }
        lines.Add($"Torchlight process: {gameAccess}");
        if (status is not null) lines.Add($"Overlay status: {status}");
        return string.Join(Environment.NewLine, lines);
    }

    static string SizeOf(string file)
    {
        try
        {
            return $"{new FileInfo(file).Length} bytes";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "unreadable";
        }
    }
}
