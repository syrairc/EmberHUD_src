using System;
using System.IO;
using System.Numerics;
using EmberHUD;
using Hexa.NET.ImGui;

namespace Mcp;

public sealed class Main : Plugin<Settings>
{
    static readonly string HostVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0";
    readonly Watches _watches = new();
    Recorder? _recorder;
    Recordings? _recordings;
    Http? _http;
    string _token = "";
    Entity[]? _lastEntities;
    string? _lastStatus;
    int _lastAreaId;
    long _stateChangedAt;
    long _startedAt;

    internal string DataDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Config", "Mcp");

    internal Func<long> Clock { get; set; } = () => Environment.TickCount64;

    internal Http? Server => _http;

    internal Watches Watching => _watches;

    internal bool StartFailed { get; private set; }

    string RecordingsDirectory => Path.Combine(DataDirectory, "recordings");

    internal Recorder Recorder => _recorder ??= new Recorder(RecordingsDirectory, Clock);

    Recordings Recordings => _recordings ??= new Recordings(RecordingsDirectory);

    public override void Tick()
    {
        var state = Game.State;
        if (!ReferenceEquals(state.Entities, _lastEntities) || state.Status != _lastStatus || state.AreaId != _lastAreaId)
        {
            _lastEntities = state.Entities;
            _lastStatus = state.Status;
            _lastAreaId = state.AreaId;
            _stateChangedAt = Clock();
        }
        if (Settings.Serve.Value)
        {
            if (_http is null && !StartFailed) Start();
        }
        else
        {
            Stop();
            StartFailed = false;
        }
        _watches.Sample();
        Recorder.Sample(state, Settings.RecordHz.Value);
    }

    public override void Render()
    {
        if (_http is null || !Settings.ShowStatusDot.Value) return;
        var colour = Clock() - _stateChangedAt > 2000 ? System.Drawing.Color.FromArgb(0xE0, 0x30, 0x30)
            : _http.InFlight > 0 || _http.MsSinceRequest < 500 ? System.Drawing.Color.FromArgb(0xF2, 0xCC, 0x1A)
            : System.Drawing.Color.FromArgb(0x33, 0xCC, 0x4D);
        Draw.CircleFilled(new Vector2(12, 12), 6, colour, 16, Draw.Foreground);
    }

    public override void DrawSettings()
    {
        base.DrawSettings();
        ImGui.Separator();
        ImGui.TextDisabled("While on, anything holding the token can read this game's memory through EmberHUD. Read-only, 127.0.0.1 only.");
        if (_http is null)
        {
            ImGui.TextDisabled(StartFailed ? "Failed to start - see the log" : "Stopped");
            return;
        }
        ImGui.Text($"Listening on {_http.Url}");
        ImGui.Text($"Watches {_watches.Count}, recorder {(Recorder.Active ? $"{Recorder.CurrentFile}, {Recorder.Frames} frames" : "idle")}");
        if (ImGui.SmallButton("Copy token")) ImGui.SetClipboardText(_token);
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy claude mcp add command")) ImGui.SetClipboardText($"claude mcp add --transport http emberhud {_http.Url} --header \"X-Bridge-Token: {_token}\"");
    }

    public override void OnDisable() => Stop();

    public override void Dispose()
    {
        Stop();
        _recorder?.Close();
        base.Dispose();
    }

    internal Status CurrentStatus() => new(_http?.Url ?? "", _http?.InFlight ?? 0, Clock() - _startedAt, Clock() - _stateChangedAt, _watches.Count, Recorder.Active);

    void Start()
    {
        try
        {
            _token = Auth.LoadToken(DataDirectory, Settings.CustomToken.Value);
            var rpc = new Rpc(Registry.Build(CurrentStatus, () => Settings.MaxReadBytes.Value, _watches, Recorder, Recordings), HostVersion);
            _http = Http.Start(rpc, _token, Settings.Port.Value);
            if (_http is null)
            {
                StartFailed = true;
                Log.Error($"MCP: no free port from {Settings.Port.Value} to {Settings.Port.Value + 19}");
                return;
            }
            File.WriteAllText(Path.Combine(DataDirectory, "bridge-port.txt"), _http.Port.ToString());
            _startedAt = Clock();
            Log.Info($"MCP listening on {_http.Url}");
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
        _http?.Dispose();
        _http = null;
        _recorder?.Close();
        _watches.StopAll();
    }
}
