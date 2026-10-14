using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class MapIconReader(IReadBackend memory, nint moduleBase, Names names, Func<long>? clockMilliseconds = null)
{
    private static readonly HashSet<int> NoTaskTypes = [];
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<int, string> _textures = [];
    private readonly Dictionary<int, string> _names = [];
    private readonly Dictionary<nint, string> _textureNames = [];
    private readonly List<(int Zorder, int Index, MapIcon Icon)> _listed = [];
    private readonly List<MapIcon> _icons = [];
    private readonly List<MapIcon> _follow = [];
    private readonly Dictionary<int, uint> _followTypes = [];
    private readonly Dictionary<int, uint> _followAreas = [];
    private readonly HashSet<int> _followIds = [];
    private HashSet<int>? _taskTypes;
    private nint _widget;
    private int? _generation;
    private long _nextIconMapRead;
    private bool _missingNames;
    private bool _missingTextures;

    public MapIcon[] Read(MinimapReader minimap, int generation, IReadOnlyDictionary<int, Vector2> positions, Func<int, Vector2?> findPosition)
    {
        var widget = minimap.Widget;
        if (widget != _widget)
        {
            _widget = widget;
            _textures.Clear();
            _names.Clear();
            _textureNames.Clear();
            _taskTypes = null;
            _nextIconMapRead = 0;
        }

        if (generation != _generation)
        {
            _generation = generation;
            _taskTypes = null;
        }

        if (widget == 0 || !memory.TryRead(widget + Offsets.MinimapIconArea, out uint area))
            return [];

        _taskTypes ??= ReadTaskTypes(widget);
        _missingNames = false;
        _missingTextures = false;
        if (!TryReadList(widget, area, _taskTypes ?? NoTaskTypes, positions))
            return [];

        AddFollowIcons(widget, area, positions, findPosition);
        if (ResolveTypes(widget))
            for (var i = 0; i < _icons.Count; i++)
                _icons[i] = _icons[i] with { Texture = _textures.GetValueOrDefault(_icons[i].Type, ""), Name = _names.GetValueOrDefault(_icons[i].Type, "") };
        return [.. _icons];
    }

    private MapIcon NewIcon(int type, Vector2 position, float size, float rotation, int entityId)
    {
        _missingNames |= !_names.TryGetValue(type, out var name);
        _missingTextures |= !_textures.TryGetValue(type, out var texture);
        return new MapIcon(type, texture ?? "", position, size, rotation, entityId) { Name = name ?? "" };
    }

    private bool TryReadList(nint widget, uint area, HashSet<int> taskTypes, IReadOnlyDictionary<int, Vector2> positions)
    {
        _icons.Clear();
        if (!memory.TryRead(widget + Offsets.MinimapIconList + Offsets.TArrayNum, out int count) || count < 0 || count > Offsets.MaxMapIcons)
            return false;
        if (count == 0)
            return true;
        if (!memory.TryReadPtr(widget + Offsets.MinimapIconList, out var data) || !memory.TryReadArray(data, count * Offsets.IconEntryStride, out byte[] bytes))
            return false;

        _listed.Clear();
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
            _listed.Add((BitConverter.ToInt32(bytes, at + Offsets.IconEntryZorder), i,
                NewIcon(type, position, float.IsFinite(size) && size > 0 ? size : 1, float.IsFinite(rotation) ? rotation : 0, entityId)));
        }

        _listed.Sort(CompareListed);
        foreach (var (_, _, icon) in _listed)
            _icons.Add(icon);
        return true;
    }

    private static int CompareListed((int Zorder, int Index, MapIcon Icon) a, (int Zorder, int Index, MapIcon Icon) b) =>
        a.Zorder != b.Zorder ? a.Zorder.CompareTo(b.Zorder) : a.Index.CompareTo(b.Index);

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

    private void AddFollowIcons(nint widget, uint area, IReadOnlyDictionary<int, Vector2> positions, Func<int, Vector2?> findPosition)
    {
        if (!TryReadIdMap(widget + Offsets.MinimapFollowTypes, _followTypes) || _followTypes.Count == 0 ||
            !TryReadIdMap(widget + Offsets.MinimapFollowAreas, _followAreas))
            return;

        _follow.Clear();
        _followIds.Clear();
        foreach (var (id, type) in _followTypes)
        {
            if (id <= 0 || type is 0 or > int.MaxValue || !_followAreas.TryGetValue(id, out var serial) || serial != area)
                continue;
            if ((positions.TryGetValue(id, out var live) ? live : findPosition(id)) is not { } position || !Plausible(position))
                continue;
            _followIds.Add(id);
            _follow.Add(NewIcon((int)type, position, 1, 0, id));
        }

        var kept = 0;
        for (var i = 0; i < _icons.Count; i++)
            if (!_followIds.Contains(_icons[i].EntityId))
                _icons[kept++] = _icons[i];
        _icons.RemoveRange(kept, _icons.Count - kept);
        _icons.AddRange(_follow);
    }

    private bool TryReadIdMap(nint map, Dictionary<int, uint> into)
    {
        into.Clear();
        return memory.TryWalkBuckets(map + Offsets.HashBucketCount, map, Offsets.MaxBuckets, Offsets.FollowNodeSize, Offsets.FollowNodeNext, Offsets.MaxFollowIcons,
            (_, node) =>
            {
                into[BitConverter.ToInt32(node[Offsets.FollowNodeId..])] = BitConverter.ToUInt32(node[Offsets.FollowNodeValue..]);
                return true;
            });
    }

    private bool ResolveTypes(nint widget)
    {
        if (!_missingNames && !_missingTextures)
            return false;
        var now = _clock();
        if (now < _nextIconMapRead)
            return false;
        _nextIconMapRead = now + Offsets.IconMapRereadMilliseconds;

        var resolved = false;
        if (_missingNames)
            foreach (var type in _icons.Select(icon => icon.Type).Distinct())
                if (!_names.ContainsKey(type) && TryReadIconName(type, out var name))
                {
                    _names[type] = name;
                    resolved = true;
                }

        if (_missingTextures)
            resolved |= ResolveTextures(widget);
        return resolved;
    }

    private bool TryReadIconName(int type, out string name)
    {
        name = "";
        if (type <= 0 || !memory.TryFindHashValue(moduleBase + Offsets.MapIconInfoBucketCountRva, moduleBase + Offsets.MapIconInfoBucketsRva, (uint)type, out var row) ||
            !memory.TryReadPtr(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringData, out var data) ||
            !memory.TryRead(row + Offsets.MapIconInfoEnum + Offsets.ConfigStringLength, out long length) ||
            data == 0 || length <= 0 || length > Offsets.MaxIconNameLength ||
            !memory.TryReadArray(data, (int)length, out byte[] bytes) ||
            Array.Exists(bytes, b => b < 0x20 || b > 0x7E))
            return false;
        name = System.Text.Encoding.ASCII.GetString(bytes);
        return true;
    }

    private bool ResolveTextures(nint widget)
    {
        if (!memory.TryReadPtr(widget + Offsets.MinimapActiveIconMap, out var map) ||
            !memory.TryReadTMapElements(map, Offsets.IconMapElementStride, Offsets.MaxIconMapElements, out _, out var bytes, out var bits))
            return false;

        var resolved = false;
        for (var i = 0; i < bytes.Length / Offsets.IconMapElementStride; i++)
        {
            var at = i * Offsets.IconMapElementStride;
            var type = BitConverter.ToInt32(bytes, at + Offsets.IconMapElementKey);
            if (!ReadExtensions.IsAllocated(bits, i) || _textures.ContainsKey(type) || !_icons.Exists(icon => icon.Type == type))
                continue;
            if (TryTextureName((nint)BitConverter.ToInt64(bytes, at + Offsets.IconMapElementTexture), out var name))
            {
                _textures[type] = name;
                resolved = true;
            }
        }

        return resolved;
    }

    private bool TryTextureName(nint texture, out string name)
    {
        if (_textureNames.TryGetValue(texture, out var known))
        {
            name = known;
            return true;
        }

        name = "";
        if (texture == 0 || !memory.TryRead(texture + Offsets.UObjectName, out uint index) || !names.TryRead(index, out name))
            return false;
        _textureNames[texture] = name;
        return true;
    }
}
