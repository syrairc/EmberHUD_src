using System.Diagnostics;
using System.Numerics;

namespace EmberHUD.Host.Reading;

internal sealed class ReaderLoop(Func<IGameSource> createSource, Action<string> log, int tickMilliseconds) : IDisposable
{
    internal static int WaitMilliseconds(int tickMilliseconds, TimeSpan elapsed) =>
        Math.Max(1, tickMilliseconds - (int)elapsed.TotalMilliseconds);

    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(2);

    private readonly ManualResetEventSlim _stop = new();
    private readonly HashSet<string> _logged = [];
    private Thread? _thread;
    private IGameSource? _source;
    private bool _disposed;
    private volatile ReaderSnapshot _current = new(GameState.Empty, 0);
    private volatile PlayerSample? _fast;

    public ReaderSnapshot Current
    {
        get
        {
            var snapshot = _current;
            return _fast is { } fast && fast.At > snapshot.PlayerAt && snapshot.State.Player is { } player
                ? snapshot with { State = snapshot.State with { Player = player with { Position = fast.Position } }, PlayerAt = fast.At }
                : snapshot;
        }
    }

    public void SamplePlayer()
    {
        try
        {
            _fast = _source is { } source && source.TryReadPlayer(out var position) ? new PlayerSample(position, Stopwatch.GetTimestamp()) : null;
        }
        catch (Exception)
        {
            _fast = null;
        }
    }

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "EmberHUD reader" };
        _thread.Start();
    }

    private void Run()
    {
        var lastGood = _current;
        while (!_stop.IsSet)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                _source ??= createSource();
                if (_source.TryRead(out var state, out var failure))
                {
                    lastGood = new ReaderSnapshot(state, Stopwatch.GetTimestamp());
                    _current = lastGood;
                    _logged.Clear();
                }
                else
                {
                    _current = lastGood with { State = lastGood.State with { Status = failure ?? "read failed", ChatOpen = null, Paused = null } };
                }
            }
            catch (Exception ex)
            {
                var cause = ex.GetBaseException().Message;
                if (_logged.Add(cause)) log($"reader - {cause}");
                _current = lastGood with { State = lastGood.State with { Status = $"read failed - {cause}", ChatOpen = null, Paused = null } };
            }

            _stop.Wait(WaitMilliseconds(tickMilliseconds, Stopwatch.GetElapsedTime(started)));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Set();
        if (_thread?.Join(JoinTimeout) == false) return;
        _source?.Dispose();
        _stop.Dispose();
    }
}

internal interface IGameSource : IDisposable
{
    bool TryRead(out GameState state, out string? failure);

    bool TryReadPlayer(out Vector3 position)
    {
        position = default;
        return false;
    }
}

internal sealed record PlayerSample(Vector3 Position, long At);

internal sealed record ReaderSnapshot(GameState State, long PlayerAt);
