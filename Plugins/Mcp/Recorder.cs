using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using EmberHUD;

namespace Mcp;

internal sealed class Recorder(string directory, Func<long> clock, long maxBytes = Recorder.MaxBytes)
{
    public const long MaxBytes = 64L * 1024 * 1024;
    readonly Lock _gate = new();
    StreamWriter? _writer;
    EntityFilter _filter = new(null, null, null, null);
    string? _file;
    string? _stoppedBecause;
    long _startedAt;
    long _nextAt;
    long _flushedAt;
    long _bytes;
    int _frames;

    public bool Active
    {
        get
        {
            lock (_gate) return _writer is not null;
        }
    }

    public int Frames
    {
        get
        {
            lock (_gate) return _frames;
        }
    }

    public string? CurrentFile
    {
        get
        {
            lock (_gate) return _writer is null ? null : _file;
        }
    }

    public string Start(string label, EntityFilter filter)
    {
        lock (_gate)
        {
            if (_writer is not null) throw new ToolError($"already recording to {_file}");
            Directory.CreateDirectory(directory);
            var safe = new string(label.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray());
            var stem = $"{DateTime.Now:yyyyMMdd-HHmmss}-{(safe.Length == 0 ? "rec" : safe[..Math.Min(safe.Length, 40)])}";
            var name = $"{stem}.jsonl";
            for (var suffix = 2; File.Exists(Path.Combine(directory, name)); suffix++) name = $"{stem}-{suffix}.jsonl";
            _writer = new StreamWriter(new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
            _file = name;
            _filter = filter;
            _stoppedBecause = null;
            _startedAt = _nextAt = _flushedAt = clock();
            _bytes = 0;
            _frames = 0;
            return name;
        }
    }

    public JsonObject Stop()
    {
        lock (_gate)
        {
            if (_writer is null) throw new ToolError("not recording");
            StopLocked("stopped by request");
            return StatusLocked();
        }
    }

    public JsonObject Status()
    {
        lock (_gate) return StatusLocked();
    }

    public void Close()
    {
        lock (_gate)
            if (_writer is not null) StopLocked("plugin closed");
    }

    public void Sample(GameState state, int hz)
    {
        lock (_gate)
        {
            if (_writer is null) return;
            var now = clock();
            if (now < _nextAt) return;
            _nextAt = now + 1000 / Math.Max(1, hz);
            try
            {
                var entities = _filter.Radius is not null && state.Player is null ? [] : _filter.Apply(state);
                var frame = new JsonObject
                {
                    ["t"] = now - _startedAt,
                    ["area"] = state.AreaId,
                    ["status"] = state.Status,
                    ["player"] = state.Player is { } player ? new JsonObject { ["x"] = Num.F(player.Position.X), ["y"] = Num.F(player.Position.Y), ["z"] = Num.F(player.Position.Z) } : null,
                    ["entities"] = new JsonArray(entities.Select(entity => (JsonNode)StateTools.Row(entity, null)).ToArray()),
                };
                var line = frame.ToJsonString();
                _writer.WriteLine(line);
                _bytes += Encoding.UTF8.GetByteCount(line) + 1;
                _frames++;
                if (_bytes >= maxBytes) StopLocked($"size cap of {maxBytes} bytes reached");
                else if (now - _flushedAt >= 2000)
                {
                    _writer.Flush();
                    _flushedAt = now;
                }
            }
            catch (Exception error)
            {
                StopLocked($"frame failed: {error.Message}");
            }
        }
    }

    void StopLocked(string reason)
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception error)
        {
            reason = $"{reason} (close failed: {error.Message})";
        }
        _writer = null;
        _stoppedBecause = reason;
    }

    JsonObject StatusLocked() => new()
    {
        ["active"] = _writer is not null,
        ["file"] = _file,
        ["frames"] = _frames,
        ["bytes"] = _bytes,
        ["elapsedMs"] = _writer is null ? 0 : clock() - _startedAt,
        ["stoppedBecause"] = _stoppedBecause,
    };
}
