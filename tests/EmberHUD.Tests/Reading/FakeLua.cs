using System.Text;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

internal sealed class FakeLua
{
    public const uint Seed = 0x2307D0F9;

    private readonly FakeMemory _memory;
    private readonly Dictionary<string, nint> _strings = [];
    private readonly Dictionary<nint, (int LogSize, int ArraySize, nint Array, nint Node)> _tables = [];
    private nint _next = unchecked((nint)0x3_0000_0000L);
    private readonly nint _manager;

    public FakeLua(FakeMemory memory, nint moduleBase)
    {
        _memory = memory;
        var slot = moduleBase + Offsets.LuaGlobalRva;
        memory.Map(slot, 8);
        _manager = Alloc(0x100);
        var state = Alloc(0x100);
        var global = Alloc(0x100);
        memory.Write(slot, _manager);
        memory.Write(_manager + Offsets.LuaMgrState, state);
        memory.Write(state + Offsets.LuaStateGlobal, global);
        memory.Write(global + Offsets.LuaGlobalSeed, Seed);

        var registry = NewTable(Offsets.LuaRegistryGlobals, 0);
        Globals = NewTable(0, 4);
        memory.Write(global + Offsets.LuaGlobalRegistry, (long)registry.Address);
        memory.Write(global + Offsets.LuaGlobalRegistry + Offsets.LuaNodeTag, (uint)Offsets.LuaTagTable);
        var slotAt = _tables[registry.Address].Array + (Offsets.LuaRegistryGlobals - 1) * Offsets.LuaTValueSize;
        memory.Write(slotAt, (long)Globals.Address);
        memory.Write(slotAt + Offsets.LuaNodeTag, (uint)Offsets.LuaTagTable);
    }

    public LuaTable Globals { get; }

    public LuaTable NewTable(int arraySize, int logNodeSize)
    {
        var header = Alloc(48);
        var array = Alloc(Math.Max(arraySize, 1) * Offsets.LuaTValueSize);
        var node = Alloc((1 << logNodeSize) * Offsets.LuaNodeSize);
        _memory.Write(header + Offsets.LuaTableLogSize, (byte)logNodeSize);
        _memory.Write(header + Offsets.LuaTableArraySize, (uint)arraySize);
        _memory.Write(header + Offsets.LuaTableArray, (long)array);
        _memory.Write(header + Offsets.LuaTableNode, (long)node);
        _tables[header] = (logNodeSize, arraySize, array, node);
        return new LuaTable(header);
    }

    public void SetString(LuaTable t, string key, string value) =>
        SetNode(t, MainPosition(t, key), key, (long)Intern(value), value.Length > 40 ? Offsets.LuaTagLongString : Offsets.LuaTagShortString);

    public void SetInt(LuaTable t, string key, long value) => SetNode(t, MainPosition(t, key), key, value, Offsets.LuaTagInt);

    public void SetTable(LuaTable t, string key, LuaTable value) => SetNode(t, MainPosition(t, key), key, value.Address, Offsets.LuaTagTable);

    public void SetIntKeyTable(LuaTable t, long key, LuaTable value) => SetIntKey(t, key, value.Address, Offsets.LuaTagTable);

    public void SetIntKey(LuaTable t, long key, long value, int tag)
    {
        var (logSize, arraySize, array, node) = _tables[t.Address];
        if (key >= 1 && key <= arraySize)
        {
            var at = array + (nint)(key - 1) * Offsets.LuaTValueSize;
            _memory.Write(at, value);
            _memory.Write(at + Offsets.LuaNodeTag, (uint)tag);
            return;
        }

        var nodes = 1 << logSize;
        var main = (int)(key % Math.Max((nodes - 1) | 1, 1));
        var slot = FreeSlot(node, nodes, main);
        _memory.Write(slot + Offsets.LuaNodeValue, value);
        _memory.Write(slot + Offsets.LuaNodeTag, (uint)tag);
        _memory.Write(slot + Offsets.LuaNodeKey, key);
        _memory.Write(slot + Offsets.LuaNodeKeyTag, (uint)Offsets.LuaTagInt);
    }

    public void SetValue(LuaTable t, string key, long value, int tag) => SetNode(t, MainPosition(t, key), key, value, tag);

    public void SetOddKey(LuaTable t, int keyTag, long value, int tag)
    {
        var (logSize, _, _, node) = _tables[t.Address];
        var slot = FreeSlot(node, 1 << logSize, 0);
        _memory.Write(slot + Offsets.LuaNodeValue, value);
        _memory.Write(slot + Offsets.LuaNodeTag, (uint)tag);
        _memory.Write(slot + Offsets.LuaNodeKey, 1L);
        _memory.Write(slot + Offsets.LuaNodeKeyTag, (uint)keyTag);
    }

    public void SetMetatable(LuaTable t, LuaTable meta) => _memory.Write(t.Address + Offsets.LuaTableMetatable, (long)meta.Address);

    public void BreakNodes(LuaTable t) => _memory.Write(t.Address + Offsets.LuaTableNode, 0x7000_0000L);

    public void BreakState() => _memory.Write(_manager + Offsets.LuaMgrState, 0L);

    public int MainPosition(LuaTable t, string key) => (int)(LuaReader.Hash(Seed, key) & ((1u << _tables[t.Address].LogSize) - 1));

    private void SetNode(LuaTable t, int main, string key, long value, int tag)
    {
        var (logSize, _, _, node) = _tables[t.Address];
        var slot = FreeSlot(node, 1 << logSize, main);
        _memory.Write(slot + Offsets.LuaNodeValue, value);
        _memory.Write(slot + Offsets.LuaNodeTag, (uint)tag);
        _memory.Write(slot + Offsets.LuaNodeKey, (long)Intern(key));
        _memory.Write(slot + Offsets.LuaNodeKeyTag, (uint)Offsets.LuaTagShortString);
    }

    private nint FreeSlot(nint node, int nodes, int main)
    {
        for (var i = 0; i < nodes; i++)
        {
            var slot = node + ((main + i) % nodes) * Offsets.LuaNodeSize;
            var keyTagBytes = new byte[4];
            _memory.TryRead(slot + Offsets.LuaNodeKeyTag, keyTagBytes);
            var keyTag = BitConverter.ToUInt32(keyTagBytes);
            if (keyTag == 0) return slot;
        }

        throw new InvalidOperationException("fake lua table is full");
    }

    private nint Intern(string text)
    {
        if (_strings.TryGetValue(text, out var existing)) return existing;
        var bytes = Encoding.UTF8.GetBytes(text);
        var at = Alloc(Offsets.LuaStringChars + bytes.Length + 1);
        _memory.Write(at + Offsets.LuaStringHash, LuaReader.Hash(Seed, text));
        _memory.Write(at + Offsets.LuaStringLength, (long)bytes.Length);
        _memory.WriteArray(at + Offsets.LuaStringChars, bytes);
        return _strings[text] = at;
    }

    private nint Alloc(int size)
    {
        var at = _next;
        _next += (size + 31) & ~15;
        _memory.Map(at, size);
        return at;
    }
}
