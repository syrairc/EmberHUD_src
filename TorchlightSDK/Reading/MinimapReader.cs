using System.Text;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class MinimapReader(IReadBackend memory, nint moduleBase, Func<long>? clockMilliseconds = null)
{
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<nint, bool> _isMinimapClass = [];
    private readonly Dictionary<uint, bool> _isRotateName = [];
    private nint _widget;
    private long _nextScan;
    private int? _generation;

    public nint Widget => _widget;

    public float? ReadHeadingDegrees() =>
        _widget != 0 && memory.TryRead(_widget + Offsets.MinimapHeading, out float heading) && float.IsFinite(heading) ? heading : null;

    public float? ReadRotationDegrees(int generation)
    {
        if (_generation != generation)
        {
            _generation = generation;
            _widget = 0;
            _nextScan = 0;
        }

        if (_widget == 0 && !TryFindWidget())
            return null;

        if (TryReadTurns(out var turns))
        {
            var degrees = -(double)turns * 360;
            return (float)(((degrees + 180) % 360 + 360) % 360 - 180);
        }

        _widget = 0;
        return null;
    }

    private bool TryFindWidget()
    {
        var now = _clock();
        if (now < _nextScan)
            return false;

        _nextScan = now + Offsets.MinimapRescanMilliseconds;
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
                var obj = (nint)BitConverter.ToInt64(bytes, i * Offsets.GObjectsItemSize);
                if (obj != 0 && IsWidget(obj))
                    return obj;
            }
        }

        return 0;
    }

    private bool IsWidget(nint obj) =>
        memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && IsMinimapClass(cls) &&
        memory.TryRead(obj + Offsets.UObjectName, out uint nameIndex) && TryReadName(nameIndex, out var name) &&
        !name.StartsWith(Offsets.MinimapDefaultPrefix, StringComparison.Ordinal);

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
        name = "";
        var block = index / Offsets.FNameBlockSize;
        if (block >= Offsets.MaxFNameBlocks ||
            !memory.TryReadPtr(moduleBase + Offsets.FNamePoolRva + Offsets.FNameBlocks + (nint)block * nint.Size, out var blockStart))
            return false;

        var entry = blockStart + (nint)(index % Offsets.FNameBlockSize) * Offsets.FNameEntryStride;
        if (!memory.TryRead(entry, out ushort header) || (header & 1) != 0)
            return false;

        var length = header >> Offsets.FNameLengthShift;
        if (length == 0 || length > Offsets.MaxNameLength || !memory.TryReadArray(entry + sizeof(ushort), length, out byte[] chars))
            return false;

        name = Encoding.ASCII.GetString(chars);
        return true;
    }
}
