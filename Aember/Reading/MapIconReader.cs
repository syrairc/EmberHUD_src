using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class MapIconReader(IReadBackend memory, Func<long>? clockMilliseconds = null)
{
    private static readonly HashSet<int> NoTaskTypes = [];
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<int, string> _textures = [];
    private readonly Dictionary<nint, string> _textureNames = [];
    private HashSet<int>? _taskTypes;
    private nint _widget;
    private long _nextIconMapRead;

    public MapIcon[] Read(MinimapReader minimap, IReadOnlyDictionary<int, Vector2> positions, Func<int, Vector2?>? findPosition = null)
    {
        var widget = minimap.Widget;
        if (widget != _widget)
        {
            _widget = widget;
            _textures.Clear();
            _textureNames.Clear();
            _taskTypes = null;
            _nextIconMapRead = 0;
        }

        if (widget == 0 || !memory.TryRead(widget + Offsets.MinimapIconArea, out uint area))
            return [];

        _taskTypes ??= ReadTaskTypes(widget);
        if (ReadList(widget, area, _taskTypes ?? NoTaskTypes, positions) is not { } icons)
            return [];

        AddFollowIcons(widget, area, positions, findPosition, icons);
        ResolveTextures(minimap, widget, icons);
        for (var i = 0; i < icons.Count; i++)
            icons[i] = icons[i] with { Texture = _textures.GetValueOrDefault(icons[i].Type, "") };
        return [.. icons];
    }

    private List<MapIcon>? ReadList(nint widget, uint area, HashSet<int> taskTypes, IReadOnlyDictionary<int, Vector2> positions)
    {
        if (!memory.TryRead(widget + Offsets.MinimapIconList + Offsets.TArrayNum, out int count) || count < 0 || count > Offsets.MaxMapIcons)
            return null;
        if (count == 0)
            return [];
        if (!memory.TryReadPtr(widget + Offsets.MinimapIconList, out var data) || !memory.TryReadArray(data, count * Offsets.IconEntryStride, out byte[] bytes))
            return null;

        var entries = new List<(int Zorder, MapIcon Icon)>(count);
        for (var i = 0; i < count; i++)
        {
            var at = i * Offsets.IconEntryStride;
            var type = BitConverter.ToInt32(bytes, at + Offsets.IconEntryType);
            if (type <= 0 || BitConverter.ToUInt32(bytes, at + Offsets.IconEntryArea) != area || taskTypes.Contains(type))
                continue;

            var id = (long)BitConverter.ToInt32(bytes, at + Offsets.IconEntryKey) - (long)type * Offsets.IconKeyStride;
            var entityId = id is > 0 and <= int.MaxValue ? (int)id : 0;
            var position = entityId != 0 && positions.TryGetValue(entityId, out var live)
                ? live
                : new Vector2(BitConverter.ToSingle(bytes, at + Offsets.IconEntryX), BitConverter.ToSingle(bytes, at + Offsets.IconEntryY));
            if (!Plausible(position))
                continue;

            var size = BitConverter.ToSingle(bytes, at + Offsets.IconEntrySize);
            var rotation = BitConverter.ToSingle(bytes, at + Offsets.IconEntryRotation);
            entries.Add((BitConverter.ToInt32(bytes, at + Offsets.IconEntryZorder),
                new MapIcon(type, "", position, float.IsFinite(size) && size > 0 ? size : 1, float.IsFinite(rotation) ? rotation : 0, entityId)));
        }

        return [.. entries.OrderBy(entry => entry.Zorder).Select(entry => entry.Icon)];
    }

    private static bool Plausible(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        MathF.Abs(position.X) <= Offsets.MaxIconCoordinate && MathF.Abs(position.Y) <= Offsets.MaxIconCoordinate;

    private HashSet<int>? ReadTaskTypes(nint widget)
    {
        var types = new HashSet<int>();
        foreach (var list in (ReadOnlySpan<int>)[Offsets.MinimapTaskTypesA, Offsets.MinimapTaskTypesB, Offsets.MinimapTaskTypesC])
        {
            if (!memory.TryRead(widget + list + Offsets.TArrayNum, out int count) || count < 0 || count > Offsets.MaxTaskTypes)
                return null;
            if (count == 0)
                continue;
            if (!memory.TryReadPtr(widget + list, out var data) || !memory.TryReadArray(data, count, out int[] values))
                return null;
            types.UnionWith(values);
        }

        return types;
    }

    private void AddFollowIcons(nint widget, uint area, IReadOnlyDictionary<int, Vector2> positions, Func<int, Vector2?>? findPosition, List<MapIcon> icons)
    {
        if (!TryReadIdMap(widget + Offsets.MinimapFollowTypes, out var types) || types.Count == 0 ||
            !TryReadIdMap(widget + Offsets.MinimapFollowAreas, out var areas))
            return;

        foreach (var (id, type) in types)
        {
            if (id <= 0 || type is 0 or > int.MaxValue || !areas.TryGetValue(id, out var serial) || serial != area)
                continue;
            if ((positions.TryGetValue(id, out var live) ? live : findPosition?.Invoke(id)) is not { } position || !Plausible(position))
                continue;
            icons.RemoveAll(icon => icon.EntityId == id);
            icons.Add(new MapIcon((int)type, "", position, 1, 0, id));
        }
    }

    private bool TryReadIdMap(nint map, out Dictionary<int, uint> values)
    {
        values = [];
        if (!memory.TryRead(map + Offsets.FollowMapBucketCount, out uint bucketCount))
            return false;
        if (bucketCount == 0)
            return true;
        if (bucketCount > Offsets.MaxBuckets || !memory.TryReadPtr(map, out var bucketArray) ||
            !memory.TryReadArray(bucketArray, (int)bucketCount, out nint[] buckets))
            return false;

        var nodes = 0;
        foreach (var head in buckets)
        {
            var chainLength = 0;
            for (var node = head; node != 0;)
            {
                if (++chainLength > Offsets.MaxChain || ++nodes > Offsets.MaxFollowIcons ||
                    !memory.TryReadArray(node, Offsets.FollowNodeSize, out byte[] bytes))
                    return false;
                values[BitConverter.ToInt32(bytes, Offsets.FollowNodeId)] = BitConverter.ToUInt32(bytes, Offsets.FollowNodeValue);
                node = (nint)BitConverter.ToInt64(bytes, Offsets.FollowNodeNext);
            }
        }

        return true;
    }

    private void ResolveTextures(MinimapReader minimap, nint widget, List<MapIcon> icons)
    {
        if (icons.TrueForAll(icon => _textures.ContainsKey(icon.Type)))
            return;
        var now = _clock();
        if (now < _nextIconMapRead)
            return;
        _nextIconMapRead = now + Offsets.IconMapRereadMilliseconds;

        if (!memory.TryReadPtr(widget + Offsets.MinimapActiveIconMap, out var map) ||
            !memory.TryReadPtr(map, out var data) ||
            !memory.TryRead(map + Offsets.TMapNum, out int count) ||
            count <= 0 || count > Offsets.MaxIconMapElements ||
            !memory.TryReadArray(data, count * Offsets.IconMapElementStride, out byte[] bytes))
            return;

        for (var i = 0; i < count; i++)
        {
            var at = i * Offsets.IconMapElementStride;
            var type = BitConverter.ToInt32(bytes, at + Offsets.IconMapElementKey);
            if (_textures.ContainsKey(type) || !icons.Exists(icon => icon.Type == type))
                continue;
            if (TryTextureName(minimap, (nint)BitConverter.ToInt64(bytes, at + Offsets.IconMapElementTexture), out var name))
                _textures[type] = name;
        }
    }

    private bool TryTextureName(MinimapReader minimap, nint texture, out string name)
    {
        if (_textureNames.TryGetValue(texture, out var known))
        {
            name = known;
            return true;
        }

        name = "";
        if (texture == 0 || !memory.TryRead(texture + Offsets.UObjectName, out uint index) || !minimap.TryReadName(index, out name))
            return false;
        _textureNames[texture] = name;
        return true;
    }
}
