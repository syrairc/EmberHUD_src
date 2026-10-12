using System.Diagnostics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class LiveGameSource : IGameSource
{
    private static readonly long AttachRetryTicks = Stopwatch.Frequency;

    private GameProcess? _game;
    private LiveReader? _reader;
    private long _nextAttach;
    private int _lastAreaId;
    private string? _attachFailure;
    private (int Pid, string? Failure) _rejectedBuild;

    public bool TryRead(out GameState state, out string? failure)
    {
        if (_game is { IsAlive: false })
            Detach();

        if (_reader is null && !TryAttach())
        {
            state = GameState.Empty;
            failure = _attachFailure;
            return false;
        }

        var ok = _reader!.TryRead(out state, out failure);
        if (ok) _lastAreaId = state.AreaId;
        return ok;
    }

    public void Dispose() => Detach();

    private bool TryAttach()
    {
        var now = Stopwatch.GetTimestamp();
        if (now < _nextAttach) return false;
        _nextAttach = now + AttachRetryTicks;
        if (!GameProcess.TryFindPid(out var pid, out _attachFailure)) return false;
        if (!GameProcess.TryAttach(pid, ref _rejectedBuild, out _game, out _attachFailure)) return false;
        _reader = new LiveReader(_game!.Backend, _game.ModuleBase, _lastAreaId);
        Aember.Memory.Use(SourceFor(_game.Backend, _game.ModuleBase));
        GameUi.Finder = _reader.FindRect;
        return true;
    }

    private void Detach()
    {
        GameUi.Finder = null;
        Aember.Memory.Use(null);
        _reader?.Dispose();
        _reader = null;
        _game?.Dispose();
        _game = null;
        _attachFailure = "game closed - waiting for it to start again";
    }

    internal static MemorySource SourceFor(IReadBackend backend, nint moduleBase)
    {
        int[] rvas = [Offsets.FNamePoolRva, Offsets.GObjectsGlobalRva, Offsets.LogicWorldGlobalRva, Offsets.FightMgrGlobalRva, Offsets.PlayerMgrGlobalRva, Offsets.MiniMapMgrGlobalRva];
        var globals = Offsets.GlobalLabels.Split(' ').Zip(rvas).ToDictionary(pair => pair.First, pair => moduleBase + pair.Second);
        var layout = new UeLayout(
            Offsets.UObjectInternalIndex, Offsets.UObjectClass, Offsets.UObjectName, Offsets.UObjectOuter,
            Offsets.UStructSuper, Offsets.UStructChildProperties, Offsets.UStructPropertiesSize,
            Offsets.FFieldClass, Offsets.FFieldNext, Offsets.FFieldName, Offsets.FFieldClassName,
            Offsets.FPropertyArrayDim, Offsets.FPropertyElementSize, Offsets.FPropertyOffset, Offsets.FPropertyInner, Offsets.FBoolByteOffset, Offsets.FBoolFieldMask,
            Offsets.GObjectsNumElements, Offsets.GObjectsPerChunk, Offsets.GObjectsItemSize, Offsets.GObjectsItemFlags, Offsets.GObjectsPendingKill, Offsets.MaxGObjectChunks,
            Offsets.FNameBlocks, Offsets.FNameBlockSize, Offsets.FNameEntryStride, Offsets.FNameLengthShift, Offsets.MaxFNameBlocks, Offsets.MaxNameLength,
            Offsets.TArrayNum, Offsets.UeClassClassName, Offsets.DefaultObjectPrefix,
            Offsets.UClassDefaultObject, Offsets.UEnumNames, Offsets.UEnumNameStride, Offsets.FEnumPropertyEnum, Offsets.FBytePropertyEnum,
            Offsets.FMapPropertyValue, Offsets.FMapElementSize, Offsets.FSetElementSize,
            Offsets.SparseFlagsInline, Offsets.SparseFlagsSecondary, Offsets.SparseNumBits, Offsets.SparseInlineBits,
            Offsets.GObjectsItemSerial, Offsets.WeakPtrSerial, Offsets.SoftPathName);
        return new MemorySource(ReadOrFalse(backend), moduleBase, globals, layout, moduleBase + Offsets.FNamePoolRva, moduleBase + Offsets.GObjectsGlobalRva);
    }

    private static MemoryRead ReadOrFalse(IReadBackend backend) => (address, into) =>
    {
        try
        {
            return backend.TryRead(address, into);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    };
}
