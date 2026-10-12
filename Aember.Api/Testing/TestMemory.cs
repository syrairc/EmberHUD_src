namespace Aember.Testing;

public static class TestMemory
{
    static readonly Lock Gate = new();
    static readonly Dictionary<nint, byte> Bytes = [];
    static readonly Dictionary<string, nint> Globals = [];
    static nint _next = 0x10000;
    static UeLayout? _layout;
    static nint _namePool;
    static nint _objectArray;

    public static void Clear()
    {
        lock (Gate)
        {
            Bytes.Clear();
            Globals.Clear();
            _next = 0x10000;
            _layout = null;
            _namePool = 0;
            _objectArray = 0;
        }
        Memory.Use(null);
    }

    public static nint Alloc(int size)
    {
        nint at;
        lock (Gate)
        {
            at = _next;
            _next += (size + 0x1F) & ~0xF;
            for (var i = 0; i < size; i++) Bytes[at + i] = 0;
        }
        Attach();
        return at;
    }

    public static unsafe void Write<T>(nint at, T value) where T : unmanaged => WriteBytes(at, new ReadOnlySpan<byte>(&value, sizeof(T)));

    public static void WriteBytes(nint at, ReadOnlySpan<byte> bytes)
    {
        lock (Gate)
        {
            for (var i = 0; i < bytes.Length; i++) Bytes[at + i] = bytes[i];
        }
        Attach();
    }

    public static void Global(string name, nint at)
    {
        lock (Gate) Globals[name] = at;
        Attach();
    }

    internal static void Use(UeLayout layout, nint namePool, nint objectArray)
    {
        lock (Gate)
        {
            _layout = layout;
            _namePool = namePool;
            _objectArray = objectArray;
        }
        Attach();
    }

    static void Attach()
    {
        lock (Gate) Memory.Use(new MemorySource(Read, 0, new Dictionary<string, nint>(Globals), _layout, _namePool, _objectArray));
    }

    static bool Read(nint address, Span<byte> into)
    {
        lock (Gate)
        {
            for (var i = 0; i < into.Length; i++)
            {
                if (!Bytes.TryGetValue(address + i, out var value)) return false;
                into[i] = value;
            }
            return true;
        }
    }
}
