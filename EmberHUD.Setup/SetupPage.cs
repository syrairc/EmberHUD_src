using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using EmberHUD.Host;
using EmberHUD.Launcher;
using EmberHUD.Setup.Icons;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Setup;

internal sealed class SetupPage
{
    readonly string _root;
    readonly string _configPath;
    readonly string[] _choices;
    readonly Func<string, string?> _pickFolder;
    readonly Func<string, string?> _pickProgram;
    readonly Func<ProcessStartInfo, Process?> _start;
    readonly string? _registered = GameInstall.Registered();
    GameInstall? _game;
    string? _checkedFolder;
    Task? _extract;
    volatile string _progress = "";
    volatile string? _iconStatus;

    public SetupPage(string root, IEnumerable<string> users, Func<string, string?> pickFolder, Func<string, string?> pickProgram, Func<ProcessStartInfo, Process?> start)
    {
        _root = root;
        _configPath = Path.Combine(root, "Config", "launcher.json");
        _pickFolder = pickFolder;
        _pickProgram = pickProgram;
        _start = start;
        string[] listed = [.. users];
        var problem = "";
        Config = LauncherConfig.Load(_configPath, text => problem = text) ?? LauncherConfig.FirstRun(listed);
        Message = problem;
        _choices = ["(yourself)", .. listed, .. Config.RunAsUser.Length > 0 && !listed.Contains(Config.RunAsUser, StringComparer.OrdinalIgnoreCase) ? [Config.RunAsUser] : Array.Empty<string>()];
    }

    public LauncherConfig Config { get; }

    public string Message { get; private set; }

    public bool CloseRequested { get; private set; }

    public bool Save()
    {
        try
        {
            Config.Save(_configPath);
            Message = "saved";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = $"could not save launcher.json - {ex.Message}";
            return false;
        }
    }

    public void SaveAndLaunch()
    {
        if (!Save() || !TryStart(new ProcessStartInfo(Path.Combine(_root, "EmberLauncher.exe")) { UseShellExecute = false, WorkingDirectory = _root })) return;
        CloseRequested = true;
    }

    public void SavePassword()
    {
        if (Config.RunAsUser.Length > 0) TryStart(Launch.SavePassword(Config.RunAsUser));
    }

    bool TryStart(ProcessStartInfo start)
    {
        try
        {
            _start(start)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Message = $"could not start {start.FileName} - {ex.Message}";
            return false;
        }
    }

    public void Draw()
    {
        if (_checkedFolder != Config.GameDirectory)
        {
            _checkedFolder = Config.GameDirectory;
            _game = GameInstall.Find(Config.GameDirectory, _registered);
            _iconStatus = null;
        }
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        if (ImGui.Begin("EmberHUD Setup", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            DrawGame();
            DrawRunAs();
            DrawLaunch();
            DrawPrograms();
            DrawIcons();
            DrawFooter();
        }
        ImGui.End();
    }

    void DrawGame()
    {
        Controls.Section("Game");
        var buttons = ImGui.CalcTextSize("Browse").X + ImGui.CalcTextSize("Detect").X + ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X * 2;
        ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - buttons));
        ImGui.InputTextWithHint("##folder", $"auto - {_registered ?? GameInstall.DefaultFolder}", ref Config.GameDirectory, 260);
        ImGui.SameLine();
        if (ImGui.Button("Browse") && _pickFolder(_game?.Folder ?? Config.GameDirectory) is { } picked) Config.GameDirectory = picked;
        ImGui.SameLine();
        if (ImGui.Button("Detect")) Config.GameDirectory = GameInstall.Find("", _registered)?.Folder ?? "";
        if (_game is null) ImGui.TextColored(EColor.ToVector4(EColor.Bad), $"{GameInstall.ExeName} not found");
        else ImGui.TextUnformatted($"ok - build {_game.Build?.ToString() ?? "unknown"}");
    }

    void DrawRunAs()
    {
        Controls.Section("Run as");
        var index = Math.Max(0, Array.FindIndex(_choices, choice => choice.Equals(Config.RunAsUser, StringComparison.OrdinalIgnoreCase)));
        if (Combo.Option(ref index, _choices, "runas", 220)) Config.RunAsUser = index == 0 ? "" : _choices[index];
        ImGui.SameLine();
        ImGui.BeginDisabled(Config.RunAsUser.Length == 0);
        if (ImGui.Button("Save password")) SavePassword();
        ImGui.EndDisabled();
        ImGui.TextDisabled("Windows Home has no saved runas passwords - pick (yourself) there");
    }

    void DrawLaunch()
    {
        Controls.Section("Launch");
        ImGui.Checkbox("Game", ref Config.LaunchGame);
        ImGui.SameLine();
        ImGui.Checkbox("EmberHUD", ref Config.LaunchEmberHUD);
    }

    void DrawPrograms()
    {
        Controls.Section("Other programs");
        ListEditor.Compact("programs", Config.Programs, DrawProgram, AddSource<LauncherProgram>.Button(() => new LauncherProgram()));
    }

    bool DrawProgram(LauncherProgram program, int index)
    {
        var changed = ImGui.Checkbox("##on", ref program.Enabled);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.55f);
        changed |= ImGui.InputTextWithHint("##path", "program .exe", ref program.Path, 260);
        ImGui.SameLine();
        if (ImGui.Button("...") && _pickProgram(program.Path) is { } picked)
        {
            program.Path = picked;
            changed = true;
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(60, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X));
        changed |= ImGui.InputTextWithHint("##args", "arguments", ref program.Arguments, 260);
        return changed;
    }

    void DrawIcons()
    {
        Controls.Section("Game icons");
        var busy = _extract is { IsCompleted: false };
        if (!busy) _iconStatus ??= _game is null ? "set the game folder first" : IconExport.Describe(Path.Combine(_root, "GameIcons"), _game.Paks(), _game.Build);
        ImGui.TextUnformatted(busy ? _progress : _iconStatus ?? "");
        ImGui.BeginDisabled(busy || _game is null);
        if (ImGui.Button("Extract")) Extract(_game!);
        ImGui.EndDisabled();
        if (!busy && _progress.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_progress);
        }
    }

    void Extract(GameInstall game)
    {
        var paks = game.Paks();
        var target = Path.Combine(_root, "GameIcons");
        _progress = "starting";
        _extract = Task.Run(() =>
        {
            try
            {
                var result = IconExport.Run(paks, target, Convert.FromHexString(Offsets.PakAesKey), text => _progress = text);
                _progress = $"done - {result.Icons} icons, {result.Missing} missing, {result.Failed} failed";
            }
            catch (Exception ex)
            {
                _progress = $"extract failed - {ex.Message}";
                Log.Warn($"icon extract failed - {ex}");
            }
            finally
            {
                _iconStatus = null;
            }
        });
    }

    void DrawFooter()
    {
        ImGui.Separator();
        if (ImGui.Button("Save")) Save();
        ImGui.SameLine();
        ImGui.BeginDisabled(_extract is { IsCompleted: false });
        if (Controls.PrimaryButton("Save & Launch")) SaveAndLaunch();
        ImGui.EndDisabled();
        if (Message.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Message);
        }
    }
}
