using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class ExploredReader(IReadBackend memory, Func<long>? clockMilliseconds = null)
{
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private Vector2[] _buffer = [];
    private int _count;
    private nint _widget;
    private (int Level, int Instance) _level;
    private string? _key;
    private nint _record;
    private nint _mapData;
    private int? _generation;
    private long _nextAt;
    private ExploredTrail? _trail;

    public ExploredTrail? Read(nint widget, nint geometry, int generation)
    {
        if (widget == 0)
            return null;

        var now = _clock();
        if (widget == _widget && generation == _generation && now < _nextAt)
            return _trail;
        _generation = generation;
        _nextAt = now + Offsets.ExploredRefreshMilliseconds;

        if (!TryReadLevel(geometry, out var level))
            return _trail = null;
        if (widget != _widget || level != _level)
        {
            Restart();
            _widget = widget;
            _level = level;
        }

        if (!TryReadRadius(widget, out var radius) ||
            !memory.TryReadPtr(widget + Offsets.MinimapSaveObject, out var save) ||
            !TryResolveRecord(save + Offsets.SaveRecords, out var record) ||
            !memory.TryRead(record + Offsets.SaveRecordPos, out nint data) ||
            !memory.TryRead(record + Offsets.SaveRecordPos + Offsets.TArrayNum, out int count) || count < 0)
            return _trail;

        count = Math.Min(count, Offsets.MaxExploredPoints);
        if (count < _count)
        {
            _buffer = [];
            _count = 0;
            _trail = null;
        }

        if (count > _count)
        {
            var added = count - _count;
            if (!memory.TryReadArray(data + _count * Offsets.SavePointStride, added * Offsets.SavePointStride, out byte[] bytes))
                return _trail;
            if (_buffer.Length < count)
                Array.Resize(ref _buffer, Math.Max(count, _buffer.Length * 2));
            for (var i = 0; i < added; i++)
                _buffer[_count + i] = MemoryMarshal.Read<Vector2>(bytes.AsSpan(i * Offsets.SavePointStride));
            _count = count;
            _trail = null;
        }

        if (_trail is null || _trail.Radius != radius)
            _trail = new ExploredTrail(_buffer, _count, radius);
        return _trail;
    }

    private void Restart()
    {
        _buffer = [];
        _count = 0;
        _key = null;
        _record = 0;
        _trail = null;
    }

    private bool TryReadLevel(nint geometry, out (int Level, int Instance) level)
    {
        level = default;
        if (geometry == 0 ||
            !memory.TryRead(geometry + Offsets.GeometryLevelId, out int id) ||
            !memory.TryRead(geometry + Offsets.GeometryLevelInstance, out int instance))
            return false;

        level = (id, instance);
        return true;
    }

    private bool TryReadRadius(nint widget, out float radius)
    {
        radius = 0;
        if (!memory.TryRead(widget + Offsets.MinimapBrushSize, out float brush) ||
            !memory.TryRead(widget + Offsets.MinimapMapSide, out int side))
            return false;

        radius = 0.5f * brush * side;
        return float.IsFinite(radius) && radius > 0;
    }

    private bool TryResolveRecord(nint map, out nint record)
    {
        record = 0;
        Span<byte> header = stackalloc byte[Offsets.TMapHeaderSize];
        if (!memory.TryRead(map, header))
            return false;

        var data = (nint)BitConverter.ToInt64(header);
        if (_record != 0 && data == _mapData && RecordHasKey(_record))
        {
            record = _record;
            return true;
        }

        _record = 0;
        _key ??= string.Format(CultureInfo.InvariantCulture, Offsets.ExploredRecordKey, _level.Level, _level.Instance);
        if (!TryFindRecord(map, _key, out record))
            return false;

        _record = record;
        _mapData = data;
        return true;
    }

    private bool RecordHasKey(nint record)
    {
        var key = _key!;
        Span<char> chars = stackalloc char[key.Length];
        return memory.TryReadPtr(record, out var text) && memory.TryRead(record + Offsets.TArrayNum, out int length) &&
               length == key.Length + 1 && memory.TryRead(text, chars) && chars.SequenceEqual(key);
    }

    private bool TryFindRecord(nint map, string key, out nint record)
    {
        record = 0;
        if (!memory.TryReadTMapElements(map, Offsets.SaveRecordStride, Offsets.MaxSaveRecords, out var data, out var elements, out var bits))
            return false;

        for (var i = 0; i < elements.Length / Offsets.SaveRecordStride; i++)
        {
            if (!ReadExtensions.IsAllocated(bits, i))
                continue;
            var at = i * Offsets.SaveRecordStride;
            var text = (nint)BitConverter.ToInt64(elements, at);
            if (BitConverter.ToInt32(elements, at + Offsets.TArrayNum) != key.Length + 1 ||
                !memory.TryReadArray(text, key.Length, out char[] chars) || !chars.AsSpan().SequenceEqual(key))
                continue;
            record = data + at;
            return true;
        }

        return false;
    }
}
