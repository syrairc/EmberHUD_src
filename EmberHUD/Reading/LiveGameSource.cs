using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class LiveGameSource : IGameSource
{
    private static readonly long AttachRetryTicks = Stopwatch.Frequency;

    private GameProcess? _game;
    private LiveReader? _reader;
    private CombatReader? _combat;
    private CombatPoller? _poller;
    private long _nextAttach;
    private int _lastAreaId;
    private string? _attachFailure;

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
        if (!GameProcess.TryAttach(pid, out _game, out _attachFailure)) return false;
        if (!_game!.ExactBuild) Log.Warn($"{Offsets.GameProcessName}.exe is {_game.ExeSize} bytes, no offsets for that exact build - using the {Offsets.BuildSizes[Offsets.Build]} byte set, reads that miss are skipped");
        _combat = new CombatReader(_game.Backend, _game.ModuleBase);
        _poller = new CombatPoller(_combat.Poll, () => Game.CombatWanted(Stopwatch.GetTimestamp(), Offsets.CombatWantedSeconds), Log.Warn);
        _reader = new LiveReader(_game.Backend, _game.ModuleBase, _lastAreaId, _combat, new InventoryReader(_game.Backend, _game.ModuleBase), new CharacterReader(_game.Backend, _game.ModuleBase), new TartarusReader(_game.Backend, _game.ModuleBase));
        EmberHUD.Memory.Use(SourceFor(_game.Backend, _game.ModuleBase));
        UseLookups(_game.Backend, _game.ModuleBase);
        return true;
    }

    private void Detach()
    {
        UseLookups(null, 0);
        EmberHUD.Memory.Use(null);
        _reader = null;
        _poller?.Dispose();
        _poller = null;
        _combat = null;
        _game?.Dispose();
        _game = null;
        _attachFailure = "game closed - waiting for it to start again";
    }

    internal static void UseLookups(IReadBackend? backend, nint moduleBase)
    {
        if (backend is null)
        {
            Loc.Getter = null;
            Loc.Finder = null;
            Scene.Lister = null;
            Lua.Dumper = null;
            return;
        }

        var loc = new LocText(backend, moduleBase);
        var finder = new LocText(backend, moduleBase);
        var world = new LogicWorldReader(backend, moduleBase);
        var names = new Names(backend, moduleBase);
        var locGate = new Lock();
        var findGate = new Lock();
        var luaReader = new LuaReader(backend, moduleBase);
        var sceneGate = new Lock();
        var luaGate = new Lock();
        Loc.Getter = key => Guarded(locGate, () => loc.Get(key), null);
        Loc.Finder = (needle, max) => Guarded(findGate, () => finder.Find(needle, max), ([], false));
        Scene.Lister = () => Guarded(sceneGate, () => world.TryReadScene(out var scene, out _) ? world.ListScene(scene, names) : [], []);
        Lua.Dumper = (path, max) => Guarded(luaGate, () => DumpLua(luaReader, path, max), new LuaDump(false, "", [], 0));
    }

    private static LuaDump DumpLua(LuaReader reader, string path, int max)
    {
        var failedAt = "";
        if (!reader.TryOpen(out var globals) || !reader.TryResolve(globals, path, out var table, out failedAt)) return new LuaDump(false, failedAt, [], 0);
        var rows = reader.Entries(table, max, out var total);
        return new LuaDump(true, "", [.. rows.Select(row => new LuaEntry(row.Key, row.Type, row.Value))], total);
    }

    private static T Guarded<T>(Lock gate, Func<T> read, T fallback)
    {
        lock (gate)
        {
            try
            {
                return read();
            }
            catch (ObjectDisposedException)
            {
                return fallback;
            }
        }
    }

    internal static MemorySource SourceFor(IReadBackend backend, nint moduleBase)
    {
        int[] rvas = [Offsets.FNamePoolRva, Offsets.GObjectsGlobalRva, Offsets.LogicWorldGlobalRva, Offsets.FightMgrGlobalRva, Offsets.PlayerMgrGlobalRva, Offsets.MiniMapMgrGlobalRva];
        var globals = Offsets.GlobalLabels.Split(' ').Zip(rvas).ToDictionary(pair => pair.First, pair => moduleBase + pair.Second);
        var layout = new UeLayout(
            ObjectIndex: Offsets.UObjectInternalIndex,
            ObjectClass: Offsets.UObjectClass,
            ObjectName: Offsets.UObjectName,
            ObjectOuter: Offsets.UObjectOuter,
            StructSuper: Offsets.UStructSuper,
            StructChildProperties: Offsets.UStructChildProperties,
            StructSize: Offsets.UStructPropertiesSize,
            FieldClass: Offsets.FFieldClass,
            FieldNext: Offsets.FFieldNext,
            FieldName: Offsets.FFieldName,
            FieldClassName: Offsets.FFieldClassName,
            PropertyArrayDim: Offsets.FPropertyArrayDim,
            PropertyElementSize: Offsets.FPropertyElementSize,
            PropertyOffset: Offsets.FPropertyOffset,
            PropertyInner: Offsets.FPropertyInner,
            BoolByteOffset: Offsets.FBoolByteOffset,
            BoolFieldMask: Offsets.FBoolFieldMask,
            ObjectsNum: Offsets.GObjectsNumElements,
            ObjectsPerChunk: Offsets.GObjectsPerChunk,
            ObjectItemSize: Offsets.GObjectsItemSize,
            ObjectItemFlags: Offsets.GObjectsItemFlags,
            PendingKillFlag: Offsets.GObjectsPendingKill,
            MaxObjectChunks: Offsets.MaxGObjectChunks,
            NameBlocks: Offsets.FNameBlocks,
            NameBlockSize: Offsets.FNameBlockSize,
            NameEntryStride: Offsets.FNameEntryStride,
            NameLengthShift: Offsets.FNameLengthShift,
            MaxNameBlocks: Offsets.MaxFNameBlocks,
            MaxNameLength: Offsets.MaxNameLength,
            ArrayNum: Offsets.TArrayNum,
            ClassClassName: Offsets.UeClassClassName,
            DefaultPrefix: Offsets.DefaultObjectPrefix,
            ClassDefaultObject: Offsets.UClassDefaultObject,
            EnumNames: Offsets.UEnumNames,
            EnumNameStride: Offsets.UEnumNameStride,
            EnumPropertyEnum: Offsets.FEnumPropertyEnum,
            BytePropertyEnum: Offsets.FBytePropertyEnum,
            MapPropertyValue: Offsets.FMapPropertyValue,
            MapElementSize: Offsets.FMapElementSize,
            SetElementSize: Offsets.FSetElementSize,
            SparseFlagsInline: Offsets.SparseFlagsInline,
            SparseFlagsSecondary: Offsets.SparseFlagsSecondary,
            SparseNumBits: Offsets.SparseNumBits,
            SparseInlineBits: Offsets.SparseInlineBits,
            ObjectItemSerial: Offsets.GObjectsItemSerial,
            WeakSerial: Offsets.WeakPtrSerial,
            SoftPathName: Offsets.SoftPathName);
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
