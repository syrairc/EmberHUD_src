using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class PausedReader(IReadBackend memory, nint moduleBase, Func<long>? clockMilliseconds = null)
{
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private bool _seen;
    private uint _frame;
    private long _changedAt;
    private bool? _paused;

    public bool? Read()
    {
        if (!memory.TryReadPtr(moduleBase + Offsets.FightMgrGlobalRva, out var fightMgr) ||
            !memory.TryReadPtr(fightMgr + Offsets.FightWorldFirst, out var world) ||
            !memory.TryReadPtr(world + Offsets.FightWorldFightMsg, out var fightMsg) ||
            !memory.TryRead(fightMsg + Offsets.FightMsgFrame, out uint frame))
        {
            _seen = false;
            return _paused = null;
        }

        var now = _clock();
        if (!_seen || frame != _frame)
        {
            _paused = _seen ? false : null;
            _seen = true;
            _frame = frame;
            _changedAt = now;
        }
        else if (now - _changedAt >= Offsets.PausedAfterMilliseconds)
            _paused = true;

        return _paused;
    }
}
