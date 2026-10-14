using System.Collections.Concurrent;
using System.Text;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class MinimapReader(IReadBackend memory, nint moduleBase)
{
    private readonly Dictionary<nint, bool> _isMinimapClass = [];
    private readonly Dictionary<uint, bool> _isRotateName = [];
    private readonly ConcurrentDictionary<uint, string> _names = [];
    private nint _widget;
    private bool _scanned;
    private int? _generation;

    public nint Widget => _widget;

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
        memory.TryRead(obj + Offsets.UObjectName, out uint nameIndex) && TryReadName(nameIndex, out var name) &&
        !name.StartsWith(Offsets.DefaultObjectPrefix, StringComparison.Ordinal);

    private bool IsMinimapClass(nint cls)
    {
        if (_isMinimapClass.TryGetValue(cls, out var known))
            return known;
        if (!memory.TryRead(cls + Offsets.UObjectName, out uint nameIndex) || !TryReadName(nameIndex, out var name))
            return false;

        return _isMinimapClass[cls] = name == Offsets.MinimapClassName;
    }

    private bool IsRotateName(uint nameIndex)
    {
        if (_isRotateName.TryGetValue(nameIndex, out var known))
            return known;
        if (!TryReadName(nameIndex, out var name))
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

    internal bool TryReadName(uint index, out string name)
    {
        if (_names.TryGetValue(index, out var known))
        {
            name = known;
            return true;
        }

        if (!TryReadNameFromPool(index, out name))
            return false;

        _names[index] = name;
        return true;
    }

    private bool TryReadNameFromPool(uint index, out string name)
    {
        name = "";
        var block = index / Offsets.FNameBlockSize;
        if (block >= Offsets.MaxFNameBlocks ||
            !memory.TryReadPtr(moduleBase + Offsets.FNamePoolRva + Offsets.FNameBlocks + (nint)block * nint.Size, out var blockStart))
            return false;

        var entry = blockStart + (nint)(index % Offsets.FNameBlockSize) * Offsets.FNameEntryStride;
        if (!memory.TryRead(entry, out ushort header) || (header & Offsets.FNameWideBit) != 0)
            return false;

        var length = header >> Offsets.FNameLengthShift;
        if (length == 0 || length > Offsets.MaxNameLength || !memory.TryReadArray(entry + Offsets.FNameHeaderSize, length, out byte[] chars))
            return false;

        name = Encoding.ASCII.GetString(chars);
        return true;
    }
}
