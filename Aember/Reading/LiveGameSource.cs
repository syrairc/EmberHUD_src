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
        return true;
    }

    private void Detach()
    {
        _reader?.Dispose();
        _reader = null;
        _game?.Dispose();
        _game = null;
        _attachFailure = "game closed - waiting for it to start again";
    }
}
