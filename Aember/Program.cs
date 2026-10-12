using System.Diagnostics;
using Hexa.NET.ImGui;
using Aember;
using Aember.Host;
using Aember.Host.Icons;
using Aember.Host.Overlay;
using Aember.Host.Plugins;
using Aember.Host.Reading;
using Aember.Host.Ui;
using Aember.Ui;

var root = AppContext.BaseDirectory;
var pluginsDirectory = Path.Combine(root, "Plugins");
var check = false;
var fake = false;
var live = false;
string? iconSheet = null;
string? processName = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--check") check = true;
    else if (args[i] == "--fake") fake = true;
    else if (args[i] == "--live") live = true;
    else if (args[i] == "--plugins" && i + 1 < args.Length) pluginsDirectory = Path.GetFullPath(args[++i], root);
    else if (args[i] == "--icon-sheet" && i + 1 < args.Length) iconSheet = Path.GetFullPath(args[++i], root);
    else if (args[i] == "--as" && i + 1 < args.Length && args[i + 1].Length > 0) processName = args[++i];
    else
    {
        Console.Error.WriteLine($"unknown argument {args[i]} - usage: Aember [--plugins <dir>] [--fake] [--live] [--check] [--icon-sheet <dir>] [--as <name>]");
        return 2;
    }
}
if (processName is not null) ProcessName.Spoof(processName);
if (fake && live)
{
    Console.Error.WriteLine("--fake and --live cannot be used together - usage: Aember [--plugins <dir>] [--fake] [--live] [--check] [--icon-sheet <dir>] [--as <name>]");
    return 2;
}
if (iconSheet is not null && !check)
{
    Console.Error.WriteLine("--icon-sheet needs --check");
    return 2;
}
Directory.CreateDirectory(Path.Combine(root, "Logs"));

if (check)
{
    var text = new StringWriter();
    var report = TextWriter.Synchronized(text);
    Log.Sink = (level, plugin, message) => report.WriteLine($"{level} {(plugin is null ? "" : $"[{plugin}] ")}{message}");
    var code = CheckRunner.Run(pluginsDirectory, report);
    if (iconSheet is not null) report.WriteLine($"icon sheet {IconSheet.Write(iconSheet, SvgIconSources.Load(SvgIconSources.BuiltIns(typeof(SvgIconStore).Assembly), Path.Combine(root, "Icons"), pluginsDirectory))}");
    File.WriteAllText(Path.Combine(root, "Logs", "check.txt"), text.ToString());
    Console.Write(text.ToString());
    return code;
}

var logs = new LogBuffer();
using var logFile = new StreamWriter(new FileStream(Path.Combine(root, "Logs", "aember.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
var logGate = new Lock();
Log.Sink = (level, plugin, message) =>
{
    var line = $"{DateTime.Now:HH:mm:ss.fff} {level} {(plugin is null ? "" : $"[{plugin}] ")}{message}";
    lock (logGate)
    {
        try
        {
            logFile.WriteLine(line);
        }
        catch (ObjectDisposedException)
        {
        }
        Console.WriteLine(line);
    }
    logs.Add(line);
};

var sdkPath = Path.Combine(root, "Config", "sdk.json");
var sdk = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, File.Exists(sdkPath) ? File.ReadAllText(sdkPath) : null, problem => Log.Warn($"sdk.json - {problem}"));

using var reader = new ReaderLoop(() => fake ? new FakeGameSource(Stopwatch.GetTimestamp) : new LiveGameSource(), Log.Info, fake ? 8 : 33);
reader.Start();
using var overlay = OverlayLoop.Create("UnrealWindow", "Torchlight: Infinite");
var gameIconsDirectory = sdk.GameIconsDirectory.Length > 0 ? Path.GetFullPath(sdk.GameIconsDirectory, root) : File.Exists(Path.Combine(root, "GameIcons", "index.json")) ? Path.Combine(root, "GameIcons") : null;
var gameIcons = gameIconsDirectory is null ? null : IconStore.Open(gameIconsDirectory, overlay.UploadRgba, overlay.TryGetTexture);
Draw.GameIcons = gameIcons is null ? null : gameIcons.Resolve;
Draw.GameIconNames = gameIcons?.Names ?? [];
var svgIcons = new SvgIconStore(SvgIconSources.Load(SvgIconSources.BuiltIns(typeof(SvgIconStore).Assembly), Path.Combine(root, "Icons"), pluginsDirectory), overlay.UploadRgba, overlay.TryGetTexture, overlay.ReleaseTexture);
Draw.SvgIcons = svgIcons.Resolve;
Draw.SvgIconNames = svgIcons.Names;
var textures = new PluginTextures(overlay.BeforeFrame, overlay.UploadRgba, overlay.TryGetTexture, overlay.ReleaseTexture, () => overlay.UploadError);
Textures.Uploader = textures.Upload;
Textures.Resolver = textures.Resolve;
Textures.Releaser = textures.Release;
using var plugins = new PluginHost(sdk, root, textures.ReleaseAll);
Controls.HotkeyConflict = plugins.ConflictFor;
plugins.Discover(pluginsDirectory);
Log.Info($"plugins: {plugins.Slots.Count} in {pluginsDirectory}");
var themes = new ThemeStore(Path.Combine(root, "Themes"), Path.Combine(root, "Fonts"), sdk.Theme, overlay.BeforeFrame);
overlay.OnStyle(() =>
{
    ImGui.GetIO().ConfigErrorRecoveryEnableAssert = false;
    themes.Apply();
});
var window = new SettingsWindow(sdk, plugins, themes, logs, () => null);
using var tray = TrayIcon.Create("Aember", () => overlay.TargetHandle, command =>
{
    if (command == TrayCommand.Settings) window.Toggle();
    else overlay.Stop();
});
var smoother = new PlayerSmoother();

void Frame()
{
    Input.NewFrame(suppressed: !overlay.IsOverlayActive || overlay.WantTextInput);
    if (Input.Pressed(sdk.Hotkeys.Quit, ignoreSuppression: true))
    {
        overlay.Stop();
        return;
    }
    if (Input.Pressed(sdk.Hotkeys.Settings)) window.Toggle();

    var snapshot = reader.Current;
    var state = snapshot.State;
    if (state.Player is { } player) state = state with { Player = smoother.Update(player.Position, snapshot.PlayerAt, Stopwatch.GetTimestamp(), Stopwatch.Frequency) is { } position ? player with { Position = position } : null };
    Game.State = state;
    Map.View = null;
    gameIcons?.Pump();
    svgIcons.Pump();
    plugins.TickAll();
    plugins.RenderAll();
    window.Draw();
}

try
{
    overlay.Run(Frame, themes.Shade);
}
catch (Exception ex)
{
    Log.Error(ex, "fatal");
    Environment.ExitCode = 1;
}
return Environment.ExitCode;
