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
    readonly Action<GameInstall, Action<string>> _applyFlag;
    readonly Func<GameInstall, bool> _flagApplied;
    bool _flagIsApplied;
    readonly string? _registered = GameInstall.Registered();
    GameInstall? _game;
    string? _checkedFolder;
    Task? _extract;
    volatile string _progress = "";
    volatile string? _iconStatus;
    const string DumpPopup = "Dump debug information";
    string _dump = "";

    public SetupPage(string root, IEnumerable<string> users, Func<string, string?> pickFolder, Func<string, string?> pickProgram, Func<ProcessStartInfo, Process?> start, Action<GameInstall, Action<string>>? applyFlag = null, Func<GameInstall, bool>? flagApplied = null)
    {
        _applyFlag = applyFlag ?? Launch.AllowNonElevated;
        _flagApplied = flagApplied ?? Launch.NonElevatedApplied;
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
            _flagIsApplied = _game is { Steam: true } && _flagApplied(_game);
        }
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        if (ImGui.Begin("EmberHUD Setup", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            if (Tables.Begin("form", [("label", ImGui.CalcTextSize("Programs").X), ("value", 0)], false, ImGuiTableFlags.None))
            {
                Row(0, "Game", DrawGame);
                Row(1, "Run as", DrawRunAs);
                Row(2, "Launch", DrawLaunch);
                Row(3, "Programs", DrawPrograms);
                Row(4, "Icons", DrawIcons);
                Tables.End();
            }
            DrawFooter();
        }
        ImGui.End();
    }

    static void Row(int id, string label, Action body)
    {
        using (Tables.Row(id))
        {
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.TableNextColumn();
            body();
        }
    }

    void DrawGame()
    {
        var status = _game is null ? $"{GameInstall.ExeName} not found" : $"{(_game.Steam ? "Steam, " : "")}build {_game.Build?.ToString() ?? "unknown"}";
        var style = ImGui.GetStyle();
        var trailing = ImGui.CalcTextSize("Browse").X + ImGui.CalcTextSize("Detect").X + ImGui.CalcTextSize(status).X + style.FramePadding.X * 4 + style.ItemSpacing.X * 3;
        if (_game is { Steam: true }) trailing += ImGui.CalcTextSize("Run without admin").X + style.FramePadding.X * 2 + style.ItemSpacing.X;
        ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - trailing));
        ImGui.InputTextWithHint("##folder", $"auto - {_registered ?? GameInstall.DefaultFolder}", ref Config.GameDirectory, 260);
        ImGui.SameLine();
        if (ImGui.Button("Browse") && _pickFolder(_game?.Folder ?? Config.GameDirectory) is { } picked) Config.GameDirectory = picked;
        ImGui.SameLine();
        if (ImGui.Button("Detect")) Config.GameDirectory = GameInstall.Find("", _registered)?.Folder ?? "";
        if (_game is { Steam: true })
        {
            ImGui.SameLine();
            ImGui.BeginDisabled(_flagIsApplied || Config.RunAsAdmin);
            if (ImGui.Button("Run without admin")) ApplyFlag(_game);
            ImGui.EndDisabled();
            Text.Tip(_flagIsApplied ? "already applied" : @"The game asks for administrator rights, so Steam starts it elevated and the overlay cannot read it. This sets a per-user Windows compatibility flag (RunAsInvoker) on the game exe so it starts as you. Same as the reg edit under HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers.");
        }
        ImGui.SameLine();
        if (_game is null) ImGui.TextColored(EColor.ToVector4(EColor.Bad), status);
        else ImGui.TextDisabled(status);
    }

    public void ApplyFlag(GameInstall game)
    {
        _applyFlag(game, text => Message = text.StartsWith("game - could not") ? text : "applied");
        _flagIsApplied = _flagApplied(game);
    }

    void DrawRunAs()
    {
        var index = Math.Max(0, Array.FindIndex(_choices, choice => choice.Equals(Config.RunAsUser, StringComparison.OrdinalIgnoreCase)));
        if (Combo.Option(ref index, _choices, "runas", 220)) Config.RunAsUser = index == 0 ? "" : _choices[index];
        ImGui.SameLine();
        ImGui.BeginDisabled(Config.RunAsUser.Length == 0);
        if (ImGui.Button("Save password")) SavePassword();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.Checkbox("Run as administrator", ref Config.RunAsAdmin);
        Text.Tip("Starts EmberHUD elevated (a UAC prompt appears). Standalone installs still run the game as a normal user. Steam installs run the game elevated too unless the compatibility flag is applied.");
    }

    void DrawLaunch()
    {
        ImGui.Checkbox("Game", ref Config.LaunchGame);
        ImGui.SameLine();
        ImGui.Checkbox("EmberHUD", ref Config.LaunchEmberHUD);
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("Run EmberHUD as", "Ember.exe", ref Config.EmberProcessName, 64);
        Text.Tip("Process name EmberHUD reports itself as. Leave Ember.exe unless you want it to show up under a different name.");
    }

    void DrawPrograms()
    {
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
        var busy = _extract is { IsCompleted: false };
        if (!busy) _iconStatus ??= _game is null ? "set the game folder first" : IconExport.Describe(Path.Combine(_root, "GameIcons"), _game.Paks(), _game.Build);
        ImGui.BeginDisabled(busy || _game is null);
        if (ImGui.Button("Extract")) Extract(_game!);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextUnformatted(busy ? _progress : _iconStatus ?? "");
        if (!busy && _progress.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Text.EscPct(_progress));
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

    void DrawDump()
    {
        if (ImGui.Button("Dump debug info"))
        {
            _dump = DebugInfo.Describe("setup", typeof(SetupPage).Assembly.GetName().Version ?? new Version(0, 0, 0), _root, Config.GameDirectory, _registered, DebugInfo.IsElevated(), DebugInfo.GameAccess());
            ImGui.OpenPopup(DumpPopup);
        }
        Text.Tip("Copies your EmberHUD and Torchlight versions, the game folder, and whether each runs as administrator to the clipboard. You see exactly what first.");
        ImGui.SetNextWindowSizeConstraints(new Vector2(480, 0), new Vector2(760, 600));
        if (!Layout.ConfirmModal(DumpPopup, $"{DebugInfo.Disclosure}{Environment.NewLine}{Environment.NewLine}{_dump}{Environment.NewLine}{Environment.NewLine}Copy this to the clipboard?", out var confirmed) || !confirmed) return;
        ImGui.SetClipboardText(_dump);
        Message = "debug info copied";
    }

    void DrawFooter()
    {
        ImGui.Separator();
        if (ImGui.Button("Save")) Save();
        ImGui.SameLine();
        ImGui.BeginDisabled(_extract is { IsCompleted: false });
        if (Controls.PrimaryButton("Save & Launch")) SaveAndLaunch();
        ImGui.EndDisabled();
        ImGui.SameLine();
        DrawDump();
        if (Message.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(Text.EscPct(Message));
        }
    }
}
