using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class XpReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private XpState? _state;

    public XpState? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _state;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.XpRefreshMilliseconds / 1000;
        return _state = Build();
    }

    internal static bool Valid(int level, long exp, long toNext) => level is >= 1 and <= 200 && toNext > 0 && exp >= 0 && exp <= toNext;

    private XpState? Build() =>
        _lua.TryOpen(out var globals) && _lua.TryGetTable(globals, Offsets.LuaExpManager, out var manager) &&
        _lua.TryGetInt(manager, Offsets.LuaExpLevel, out var level) && _lua.TryGetInt(manager, Offsets.LuaExpCurrent, out var exp) && _lua.TryGetInt(manager, Offsets.LuaExpNext, out var toNext) &&
        level is >= 1 and <= 200 && Valid((int)level, exp, toNext)
            ? new XpState((int)level, exp, toNext)
            : null;
}
