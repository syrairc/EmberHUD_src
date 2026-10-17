using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class MinimapReader(IReadBackend memory, nint moduleBase, Names names)
{
    private readonly Dictionary<nint, bool> _isMinimapClass = [];
    private readonly Dictionary<uint, bool> _isRotateName = [];
    private nint _widget;
    private bool _scanned;
    private int? _generation;

    public nint Widget => _widget;
    public int Scans { get; private set; }

    public void Reset()
    {
        _widget = 0;
        _scanned = false;
        _generation = null;
        _isMinimapClass.Clear();
        _isRotateName.Clear();
    }

    public float? ReadHeadingDegrees() =>
        _widget != 0 && memory.TryRead(_widget + Offsets.MinimapHeading, out float heading) && float.IsFinite(heading) ? heading : null;

    public GameMinimap? ReadGameMinimap() =>
        _widget != 0
        && memory.TryRead(_widget + Offsets.MinimapMode, out byte mode) && mode <= 1
        && memory.TryRead(_widget + Offsets.MinimapBigZoom, out float zoom) && float.IsFinite(zoom) && zoom > 0
        && memory.TryRead(_widget + Offsets.MinimapMapSide, out int side) && side > 0
            ? new GameMinimap(mode == 1, zoom * side)
            : null;

    public float? ReadRotationDegrees(int generation)
    {
        var bound = BoundWidget();
        if (_generation != generation)
        {
            _generation = generation;
            _widget = 0;
            _scanned = false;
        }

        if (bound != 0)
            _widget = bound;
        else if (_widget == 0 && !TryFindWidget())
            return null;

        if (TryReadTurns(out var turns))
        {
            var degrees = -(double)turns * 360;
            return (float)(((degrees + 180) % 360 + 360) % 360 - 180);
        }

        _widget = 0;
        return null;
    }

    private nint BoundWidget()
    {
        if (!memory.TryReadPtr(moduleBase + Offsets.MiniMapMgrGlobalRva, out var manager) ||
            !memory.TryRead(manager + Offsets.MiniMapMgrWidget, out uint index) ||
            !memory.TryRead(manager + Offsets.MiniMapMgrWidget + Offsets.WeakPtrSerial, out int serial) || serial == 0 ||
            !memory.TryReadGObject(moduleBase, index, out var widget, out var itemSerial) || itemSerial != serial)
            return 0;

        return widget == _widget || IsWidget(widget) ? widget : 0;
    }

    private bool TryFindWidget()
    {
        if (_scanned)
            return false;

        _scanned = true;
        _widget = Scan();
        return _widget != 0;
    }

    private nint Scan()
    {
        var table = moduleBase + Offsets.GObjectsGlobalRva;
        if (!memory.TryReadPtr(table, out var chunks) ||
            !memory.TryRead(table + Offsets.GObjectsNumElements, out int count) ||
            count <= 0 || count > Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk)
            return 0;

        Scans++;
        for (var chunk = (count - 1) / Offsets.GObjectsPerChunk; chunk >= 0; chunk--)
        {
            var inChunk = Math.Min(Offsets.GObjectsPerChunk, count - chunk * Offsets.GObjectsPerChunk);
            if (!memory.TryReadPtr(chunks + chunk * nint.Size, out var items) ||
                !memory.TryReadArray(items, inChunk * Offsets.GObjectsItemSize, out byte[] bytes))
                continue;

            for (var i = inChunk - 1; i >= 0; i--)
            {
                var obj = (nint)BitConverter.ToInt64(bytes, i * Offsets.GObjectsItemSize + Offsets.GObjectsItemObject);
                if (obj != 0 && IsWidget(obj))
                    return obj;
            }
        }

        return 0;
    }

    private bool IsWidget(nint obj) =>
        memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && IsMinimapClass(cls) &&
        memory.TryRead(obj + Offsets.UObjectName, out uint nameIndex) && names.TryRead(nameIndex, out var name) &&
        !name.StartsWith(Offsets.DefaultObjectPrefix, StringComparison.Ordinal);

    private bool IsMinimapClass(nint cls)
    {
        if (_isMinimapClass.TryGetValue(cls, out var known))
            return known;
        if (!memory.TryRead(cls + Offsets.UObjectName, out uint nameIndex) || !names.TryRead(nameIndex, out var name))
            return false;

        return _isMinimapClass[cls] = name == Offsets.MinimapClassName;
    }

    private bool IsRotateName(uint nameIndex)
    {
        if (_isRotateName.TryGetValue(nameIndex, out var known))
            return known;
        if (!names.TryRead(nameIndex, out var name))
            return false;

        return _isRotateName[nameIndex] = name == Offsets.MinimapRotateParam;
    }

    private bool TryReadTurns(out float turns)
    {
        turns = 0;
        var parameters = Offsets.MaterialScalarParams;
        if (!memory.TryReadPtr(_widget + Offsets.UObjectClass, out var cls) || !IsMinimapClass(cls) ||
            !memory.TryReadPtr(_widget + Offsets.MinimapWidgetMaterial, out var material) ||
            !memory.TryReadPtr(material + parameters, out var data) ||
            !memory.TryRead(material + parameters + Offsets.TArrayNum, out int count) ||
            count < 1 || count > Offsets.MaxScalarParams ||
            !memory.TryReadArray(data, count * Offsets.ScalarParamStride, out byte[] bytes))
            return false;

        for (var i = 0; i < count; i++)
        {
            var at = i * Offsets.ScalarParamStride;
            if (!IsRotateName(BitConverter.ToUInt32(bytes, at)))
                continue;

            turns = BitConverter.ToSingle(bytes, at + Offsets.ScalarParamValue);
            return float.IsFinite(turns);
        }

        return false;
    }
}
