using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;
using Hexa.NET.ImGui;

namespace Mcp;

public sealed class Main : Plugin<Settings>
{
    static readonly string HostVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0";
    Recorder? _recorder;
    Recordings? _recordings;
    CancellationTokenSource? _recordLoop;
    string _token = "";
    Entity[]? _lastEntities;
    string? _lastStatus;
    int _lastAreaId;
    long _stateChangedAt;
    long _startedAt;
    long? _quitAt;

    internal string DataDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Config", "Mcp");

    internal Func<long> Clock { get; set; } = () => Environment.TickCount64;

    internal Http? Server { get; private set; }

    internal Watches Watching { get; } = new();

    internal Marks Marking { get; } = new();

    internal bool StartFailed { get; private set; }

    string RecordingsDirectory => Path.Combine(DataDirectory, "recordings");

    internal Recorder Recorder => _recorder ??= new Recorder(RecordingsDirectory, Clock);

    Recordings Recordings => _recordings ??= new Recordings(RecordingsDirectory);

    public override void Tick()
    {
        if (_quitAt is { } quitAt && Clock() >= quitAt)
        {
            _quitAt = null;
            App.Quit();
        }
        var state = Game.State;
        if (state.AreaId != _lastAreaId) Marking.Clear(null);
        if (!ReferenceEquals(state.Entities, _lastEntities) || state.Status != _lastStatus || state.AreaId != _lastAreaId)
        {
            _lastEntities = state.Entities;
            _lastStatus = state.Status;
            _lastAreaId = state.AreaId;
            _stateChangedAt = Clock();
        }
        if (!Settings.Serve)
        {
            if (Server is not null) Stop();
            StartFailed = false;
            return;
        }
        if (Server is null && !StartFailed) Start();
        Watching.Sample();
        Marking.Tick(Clock());
    }

    public override void Render()
    {
        if (Server is not { } http) return;
        Marking.Render(ImGui.GetIO().DisplaySize);
        if (!Settings.ShowStatusDot) return;
        var colour = Clock() - _stateChangedAt > 2000 ? GameColors.Negative
            : http.InFlight > 0 || http.MsSinceRequest < 500 ? GameColors.Gold
            : GameColors.Positive;
        Draw.CircleFilled(new Vector2(12, 12), 6, colour, 16, Draw.Foreground);
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        ImGui.Separator();
        ImGui.TextDisabled("While on, anything holding the token can read this game's memory through EmberHUD. Read-only, 127.0.0.1 only.");
        if (Server is not { } http)
        {
            ImGui.TextDisabled(StartFailed ? "Failed to start - see the log" : "Stopped");
            return;
        }
        ImGui.TextUnformatted($"Listening on {http.Url}");
        ImGui.TextUnformatted($"Watches {Watching.Count}, marks {Marking.Count}, recorder {(Recorder.Active ? $"{Recorder.CurrentFile}, {Recorder.Frames} frames" : "idle")}");
        if (ImGui.SmallButton("Copy token")) ImGui.SetClipboardText(_token);
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy claude mcp add command")) ImGui.SetClipboardText($"claude mcp add --transport http emberhud {http.Url} --header \"X-Bridge-Token: {_token}\"");
    }

    public override void OnDisable() => Stop();

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    internal void RequestShutdown() => _quitAt ??= Clock() + 500;

    internal Status CurrentStatus() => new(Server?.Url ?? "", Server?.InFlight ?? 0, Clock() - _startedAt, Clock() - _stateChangedAt, Watching.Count, Recorder.Active);

    void Start()
    {
        try
        {
            _token = Auth.LoadToken(DataDirectory, Settings.CustomToken);
            Tool[] tools =
            [
                .. StateTools.Create(CurrentStatus),
                .. MemoryTools.Create(() => Settings.MaxReadBytes),
                .. MapTools.Create(),
                .. UeTools.Create(Path.Combine(DataDirectory, "reflection")),
                .. LocTools.Create(),
                .. WatchTools.Create(Watching),
                .. RecordTools.Create(Recorder, Recordings),
                .. HostTools.Create(RequestShutdown),
                .. MarkTools.Create(Marking, () => Clock()),
                .. ScreenTools.Create(() => Settings.AllowScreenshots, Path.Combine(DataDirectory, "screenshots")),
            ];
            Server = Http.Start(new Rpc(tools, HostVersion), _token, Settings.Port);
            if (Server is null)
            {
                StartFailed = true;
                Log.Error($"MCP: no free port from {Settings.Port} to {Settings.Port + 19}");
                return;
            }
            File.WriteAllText(Path.Combine(DataDirectory, "bridge-port.txt"), Server.Port.ToString());
            _startedAt = Clock();
            Log.Info($"MCP listening on {Server.Url}");
            _recordLoop = new CancellationTokenSource();
            var stop = _recordLoop.Token;
            _ = Task.Run(() => Recorder.Follow(() => Game.State, () => Settings.RecordHz, stop));
        }
        catch (Exception error)
        {
            StartFailed = true;
            Stop();
            Log.Error(error, "MCP start");
        }
    }

    void Stop()
    {
        _recordLoop?.Cancel();
        _recordLoop?.Dispose();
        _recordLoop = null;
        Server?.Dispose();
        Server = null;
        _recorder?.Close();
        Watching.StopAll();
        Marking.Clear(null);
    }
}
