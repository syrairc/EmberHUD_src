using System.Diagnostics;
using Hexa.NET.ImGui;
using EmberHUD;
using EmberHUD.Assets;
using EmberHUD.Launcher;
using EmberHUD.Host;
using EmberHUD.Host.Icons;
using EmberHUD.Host.Logging;
using EmberHUD.Host.Overlay;
using EmberHUD.Host.Plugins;
using EmberHUD.Host.Reading;
using EmberHUD.Host.Ui;
using EmberHUD.Host.Update;
using EmberHUD.Ui;

const string usage = "usage: EmberHUD [--plugins <dir>] [--fake] [--live] [--check] [--icon-sheet <dir>] [--as <name>]";
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
        Console.Error.WriteLine($"unknown argument {args[i]} - {usage}");
        return 2;
    }
}
var exePath = Environment.ProcessPath ?? "";
if (processName is not null) ProcessName.Spoof(processName);
if (fake && live)
{
    Console.Error.WriteLine($"--fake and --live cannot be used together - {usage}");
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
    Log.Sink = (level, plugin, message) => report.WriteLine(LogLine(level, plugin, message));
    var code = CheckRunner.Run(pluginsDirectory, report);
    if (iconSheet is not null) report.WriteLine($"icon sheet {SvgIcons.WritePreviewSheet(iconSheet, LoadSvgSources())}");
    File.WriteAllText(Path.Combine(root, "Logs", "check.txt"), text.ToString());
    Console.Write(text.ToString());
    return code;
}

var logsDirectory = Path.Combine(root, "Logs");
var hub = new LogHub(logsDirectory, () => DateTime.Now);
using var writer = new DailyLogWriter(logsDirectory, () => DateTime.Now);
LogStore.Provider = hub;
Log.Sink = (level, plugin, message) =>
{
    var entry = new LogEntry(DateTime.Now, level, plugin ?? "Host", message);
    hub.Add(entry);
    writer.Write(entry.Time, LogFormat.Format(entry));
    Console.WriteLine(LogFormat.Format(entry));
};

var pluginLevels = new PluginLogLevels();
var sdkPath = PluginHost.SdkPath(root);
var sdk = (SdkSettings)SettingsJson.Load(typeof(SdkSettings), null, File.Exists(sdkPath) ? File.ReadAllText(sdkPath) : null, problem => Log.Warn($"sdk.json - {problem}"));
Log.Filter = LogLevels.Filter(sdk, pluginLevels);

using var reader = new ReaderLoop(() => fake ? new FakeGameSource(Stopwatch.GetTimestamp) : new LiveGameSource(), Log.For("Reader").Info, fake ? 8 : 33);
reader.Start();
using var overlay = OverlayLoop.Create(Offsets.TargetWindowClass, Offsets.TargetWindowTitle);
Send.GameForeground = overlay.GameIsForeground;
App.Quitter = overlay.Stop;
Game.WindowSource = () => overlay.TargetHandle;
Send.ToScreen = (x, y) =>
{
    var point = new Win32.Point { X = x, Y = y };
    return overlay.TargetHandle != 0 && Win32.ClientToScreen(overlay.TargetHandle, ref point) ? (point.X, point.Y) : null;
};
if (fake) Send.Sink = _ => { };
var gameIconsDirectory = sdk.GameIconsDirectory.Length > 0 ? Path.GetFullPath(sdk.GameIconsDirectory, root) : Path.Combine(root, "GameIcons");
var gameSets = new GameIconSets(gameIconsDirectory, overlay.Textures.TryUpload, overlay.Textures.Resolve);
Draw.GameIcons = texture => gameSets.Resolve(AssetKind.Minimap, texture);
Draw.GameIconNames = gameSets.MinimapNames;
gameSets.Changed += () => Draw.GameIconNames = gameSets.MinimapNames;
Draw.AssetIconNames = gameSets.Names;
GameAssets.Probe = gameSets.Probe;
GameAssets.Resolver = (kind, name) => gameSets.Resolve(kind, name);
var svgIcons = new SvgIconStore(LoadSvgSources(), overlay.Textures.TryUpload, overlay.Textures.Resolve, overlay.Textures.Release);
Draw.SvgIcons = svgIcons.Resolve;
Draw.SvgIconNames = svgIcons.Names;
var textures = new PluginTextures(overlay.BeforeFrame, overlay.Textures.TryUpload, overlay.Textures.Resolve, overlay.Textures.Release);
Textures.Uploader = textures.Upload;
Textures.PngLoader = new PngFileLoader(textures.Upload).Load;
Textures.Resolver = textures.Resolve;
Textures.Releaser = textures.Release;
using var plugins = new PluginHost(sdk, root, textures.ReleaseAll, pluginLevels);
Controls.HotkeyConflict = plugins.ConflictFor;
plugins.Discover(pluginsDirectory);
Log.Info($"plugins: {plugins.Slots.Count} in {pluginsDirectory}");
var themes = new ThemeStore(Path.Combine(root, "Themes"), Path.Combine(root, "Fonts"), sdk.Theme, overlay.BeforeFrame);
themes.Changed += name => sdk.Theme = name;
sdk.Theme = themes.Name;
overlay.OnStyle(() =>
{
    ImGui.GetIO().ConfigErrorRecoveryEnableAssert = false;
    themes.Apply();
});
var smoother = new PlayerSmoother();
var hostVersion = typeof(SdkSettings).Assembly.GetName().Version ?? new Version(0, 0, 0);
using var updateHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
using var updates = new UpdateService(sdk, root, UpdateService.InstalledVersion(root, hostVersion, Log.For("Updater").Info), updateHttp, Process.Start, Log.For("Updater").Info);
updates.Start();
var registeredGame = GameInstall.Registered();
var gameAssets = new GameAssetsTab(new AssetRows(AssetJobs.Create(Offsets.TradePriceTexture), new JobRunner(Log.Warn), Convert.FromHexString(Offsets.PakAesKey)), gameIconsDirectory, () => GameInstall.Find(LauncherConfig.Load(Path.Combine(root, "Config", "launcher.json"), _ => { })?.GameDirectory ?? "", registeredGame), gameSets.Rescan);
var window = new SettingsWindow(sdk, plugins, themes, hub, updates, () =>
{
    updates.RequestRestart();
    overlay.Stop();
}, new DebugPanel(root, updates.Installed, hub), gameAssets);

using var tray = TrayIcon.Create("EmberHUD", exePath, () => overlay.TargetHandle, command =>
{
    if (command == TrayCommand.Settings) window.Toggle();
    else overlay.Stop();
});

Dictionary<string, string> LoadSvgSources() => SvgIcons.Load(SvgIcons.BuiltIns(typeof(SvgIconStore).Assembly), Path.Combine(root, "Icons"), pluginsDirectory);

string LogLine(LogLevel level, string? plugin, string message) => $"{level} {(plugin is null ? "" : $"[{plugin}] ")}{message}";

void Frame()
{
    overlay.InteractiveOutOfFocus = sdk.InteractiveOutOfFocus;
    Input.NewFrame(suppressed: !overlay.HasFocus || overlay.WantTextInput);
    Send.Tick();
    if (Input.Pressed(sdk.Hotkeys.Quit, ignoreSuppression: true))
    {
        overlay.Stop();
        return;
    }
    if (Input.Pressed(sdk.Hotkeys.Settings)) window.Toggle();

    reader.SamplePlayer();
    var snapshot = reader.Current;
    var state = snapshot.State;
    if (state.Player is { } player)
    {
        var smoothed = smoother.Update(player.Position, snapshot.PlayerAt, Stopwatch.GetTimestamp(), Stopwatch.Frequency);
        state = state with { Player = smoothed is { } position ? player with { Position = position } : null };
    }
    Game.State = state;
    Map.View = null;
    gameSets.Pump();
    gameAssets.Poll();
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
Send.ReleaseAll();
updates.Dispose();
updates.LaunchOnExit();
return Environment.ExitCode;
