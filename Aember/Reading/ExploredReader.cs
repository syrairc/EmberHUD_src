using System.Globalization;
using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class ExploredReader(IReadBackend memory)
{
    private readonly List<Vector2> _points = [];
    private nint _widget;
    private string _key = "";
    private ExploredTrail? _trail;

    public ExploredTrail? Read(nint widget, nint scene)
    {
        if (widget == 0 || !TryReadKey(scene, out var key))
            return null;
        if (widget != _widget || key != _key)
        {
            _widget = widget;
            _key = key;
            _points.Clear();
            _trail = null;
        }

        if (!TryReadRadius(widget, out var radius) ||
            !memory.TryReadPtr(widget + Offsets.MinimapSaveObject, out var save) ||
            !TryFindRecord(save + Offsets.SaveRecords, key, out var record) ||
            !memory.TryRead(record + Offsets.SaveRecordPos, out nint data) ||
            !memory.TryRead(record + Offsets.SaveRecordPos + Offsets.TArrayNum, out int count) || count < 0)
            return _trail;

        count = Math.Min(count, Offsets.MaxExploredPoints);
        if (count < _points.Count)
        {
            _points.Clear();
            _trail = null;
        }

        if (count > _points.Count)
        {
            if (!memory.TryReadArray(data + _points.Count * Offsets.SavePointStride, (count - _points.Count) * 3, out float[] xyz))
                return _trail;
            for (var i = 0; i < xyz.Length; i += 3)
                _points.Add(new Vector2(xyz[i], xyz[i + 1]));
            _trail = null;
        }

        if (_trail is null || _trail.Radius != radius)
            _trail = new ExploredTrail([.. _points], radius);
        return _trail;
    }

    private bool TryReadKey(nint scene, out string key)
    {
        key = "";
        if (!memory.TryReadPtr(scene + Offsets.SceneGeometry, out var geometry) ||
            !memory.TryRead(geometry + Offsets.GeometryLevelId, out int level) ||
            !memory.TryRead(geometry + Offsets.GeometryLevelInstance, out int instance))
            return false;

        key = string.Format(CultureInfo.InvariantCulture, Offsets.ExploredRecordKey, level, instance);
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

    private bool TryFindRecord(nint map, string key, out nint record)
    {
        record = 0;
        if (!memory.TryRead(map + Offsets.TMapNum, out int num) || num <= 0 || num > Offsets.MaxSaveRecords ||
            !memory.TryReadTMapAllocationBits(map, num, out var bits) ||
            !memory.TryReadPtr(map, out var data) ||
            !memory.TryReadArray(data, num * Offsets.SaveRecordStride, out byte[] elements))
            return false;

        for (var i = 0; i < num; i++)
        {
            if ((bits[i >> 5] >> (i & 31) & 1) == 0)
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
