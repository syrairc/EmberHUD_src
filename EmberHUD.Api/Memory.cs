namespace EmberHUD;

internal delegate bool MemoryRead(nint address, Span<byte> into);

internal sealed record MemorySource(MemoryRead Read, nint ModuleBase, IReadOnlyDictionary<string, nint> Globals, UeLayout? Layout, nint NamePool, nint ObjectArray);

public static class Memory
{
    static readonly IReadOnlyDictionary<string, nint> NoGlobals = new Dictionary<string, nint>();
    static volatile MemorySource? _source;
    static int _generation;

    public static bool Attached => _source is not null;

    public static nint ModuleBase => _source?.ModuleBase ?? 0;

    public static IReadOnlyDictionary<string, nint> Globals => _source?.Globals ?? NoGlobals;

    internal static int Generation => Volatile.Read(ref _generation);

    internal static UeLayout? Layout => _source?.Layout;

    internal static nint NamePool => _source?.NamePool ?? 0;

    internal static nint ObjectArray => _source?.ObjectArray ?? 0;

    internal static void Use(MemorySource? source)
    {
        _source = source;
        Interlocked.Increment(ref _generation);
    }

    public static unsafe bool TryRead<T>(nint at, out T value) where T : unmanaged
    {
        value = default;
        fixed (T* p = &value)
        {
            return Read(at, new Span<byte>(p, sizeof(T)));
        }
    }

    public static bool Read(nint at, Span<byte> into) =>
        !into.IsEmpty && at != 0 && _source is { } source && source.Read(at, into);
}
