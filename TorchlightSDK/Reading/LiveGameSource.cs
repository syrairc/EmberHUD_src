using System.Diagnostics;
using TorchlightSDK.Host.Memory;

namespace TorchlightSDK.Host.Reading;

internal sealed class LiveGameSource : IGameSource
{
    private static readonly long AttachRetryTicks = Stopwatch.Frequency;

    private GameProcess? _game;
    private LiveReader? _reader;
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
        if (!GameProcess.TryAttach(out _game, out _attachFailure)) return false;
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
