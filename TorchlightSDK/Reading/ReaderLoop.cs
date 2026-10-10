using System.Diagnostics;

namespace TorchlightSDK.Host.Reading;

internal sealed class ReaderLoop(Func<IGameSource> createSource, Action<string> log, int tickMilliseconds = 8) : IDisposable
{
    private readonly ManualResetEventSlim _stop = new();
    private readonly HashSet<string> _logged = [];
    private Thread? _thread;
    private IGameSource? _source;
    private volatile ReaderSnapshot _current = new(GameState.Empty, 0);

    public ReaderSnapshot Current => _current;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "TorchlightSDK reader" };
        _thread.Start();
    }

    private void Run()
    {
        var lastGood = _current;
        while (!_stop.Wait(tickMilliseconds))
        {
            try
            {
                _source ??= createSource();
                if (_source.TryRead(out var state, out var failure))
                {
                    lastGood = new ReaderSnapshot(state, Stopwatch.GetTimestamp());
                    _current = lastGood;
                }
                else
                {
                    _current = lastGood with { State = lastGood.State with { Status = failure ?? "read failed" } };
                }
            }
            catch (Exception ex)
            {
                if (_logged.Add(ex.Message)) log($"reader - {ex.Message}");
                _current = lastGood with { State = lastGood.State with { Status = $"read failed - {ex.Message}" } };
            }
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join();
        _source?.Dispose();
        _stop.Dispose();
    }
}
