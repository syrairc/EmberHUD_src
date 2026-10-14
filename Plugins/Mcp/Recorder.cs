using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;

namespace Mcp;

internal sealed class Recorder(string directory, Func<long> clock, long maxBytes = Recorder.MaxBytes)
{
    public const long MaxBytes = 64L * 1024 * 1024;
    internal readonly Lock Gate = new();
    readonly ArrayBufferWriter<byte> _line = new(4096);
    volatile FileStream? _stream;
    volatile string? _file;
    Utf8JsonWriter? _json;
    EntityFilter _filter = new(null, null, null, null);
    string? _stoppedBecause;
    long _startedAt;
    long _nextAt;
    long _flushedAt;
    long _bytes;
    int _frames;

    public bool Active => _stream is not null;

    public int Frames => Volatile.Read(ref _frames);

    public string? CurrentFile => _stream is null ? null : _file;

    public string Start(string label, EntityFilter filter)
    {
        lock (Gate)
        {
            if (_stream is not null) throw new ToolError($"already recording to {_file}");
            Directory.CreateDirectory(directory);
            var safe = new string(label.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray());
            var stem = $"{DateTime.Now:yyyyMMdd-HHmmss}-{(safe.Length == 0 ? "rec" : safe[..Math.Min(safe.Length, 40)])}";
            var name = $"{stem}.jsonl";
            for (var suffix = 2; File.Exists(Path.Combine(directory, name)); suffix++) name = $"{stem}-{suffix}.jsonl";
            var stream = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            _json = new Utf8JsonWriter(_line);
            _file = name;
            _filter = filter;
            _stoppedBecause = null;
            _startedAt = _nextAt = _flushedAt = clock();
            _bytes = 0;
            Volatile.Write(ref _frames, 0);
            _stream = stream;
            return name;
        }
    }

    public JsonObject Stop()
    {
        lock (Gate)
        {
            if (_stream is null) throw new ToolError("not recording");
            StopLocked("stopped by request");
            return StatusLocked();
        }
    }

    public JsonObject Status()
    {
        lock (Gate) return StatusLocked();
    }

    public void Close()
    {
        lock (Gate)
            if (_stream is not null) StopLocked("plugin closed");
    }

    public void Sample(GameState state, int hz)
    {
        lock (Gate)
        {
            if (_stream is null || _json is null) return;
            var now = clock();
            if (now < _nextAt) return;
            _nextAt = now + 1000 / Math.Max(1, hz);
            try
            {
                var entities = _filter.Radius is not null && state.Player is null ? [] : _filter.Apply(state);
                _line.ResetWrittenCount();
                _json.Reset();
                _json.WriteStartObject();
                _json.WriteNumber("t", now - _startedAt);
                _json.WriteNumber("area", state.AreaId);
                _json.WriteString("status", state.Status);
                if (state.Player is { } player)
                {
                    _json.WriteStartObject("player");
                    Num.Write(_json, "x", player.Position.X);
                    Num.Write(_json, "y", player.Position.Y);
                    Num.Write(_json, "z", player.Position.Z);
                    _json.WriteEndObject();
                }
                else _json.WriteNull("player");
                _json.WriteStartArray("entities");
                foreach (var entity in entities) StateTools.WriteRow(_json, entity);
                _json.WriteEndArray();
                _json.WriteEndObject();
                _json.Flush();
                _line.Write("\n"u8);
                _stream.Write(_line.WrittenSpan);
                _bytes += _line.WrittenCount;
                Interlocked.Increment(ref _frames);
                if (_bytes >= maxBytes) StopLocked($"size cap of {maxBytes} bytes reached");
                else if (now - _flushedAt >= 2000)
                {
                    _stream.Flush();
                    _flushedAt = now;
                }
            }
            catch (Exception error)
            {
                StopLocked($"frame failed: {error.Message}");
            }
        }
    }

    public async Task Follow(Func<GameState> state, Func<int> hz, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                Sample(state(), hz());
                await Task.Delay(Math.Max(5, 500 / Math.Max(1, hz())), stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                lock (Gate)
                    if (_stream is not null) StopLocked($"record loop failed: {error.Message}");
                return;
            }
        }
    }

    void StopLocked(string reason)
    {
        _json?.Dispose();
        _json = null;
        try
        {
            _stream?.Dispose();
        }
        catch (Exception error)
        {
            reason = $"{reason} (close failed: {error.Message})";
        }
        _stream = null;
        _stoppedBecause = reason;
    }

    JsonObject StatusLocked() => new()
    {
        ["active"] = _stream is not null,
        ["file"] = _file,
        ["frames"] = _frames,
        ["bytes"] = _bytes,
        ["elapsedMs"] = _stream is null ? 0 : clock() - _startedAt,
        ["stoppedBecause"] = _stoppedBecause,
    };
}
