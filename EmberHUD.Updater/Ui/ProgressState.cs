namespace EmberHUD.Updater.Ui;

internal sealed class ProgressState
{
    readonly object _gate = new();
    (int Done, int Total, string File) _progress = (0, 0, "");
    string? _pendingPrompt;
    bool? _answer;
    bool _closed;
    UpdateResult? _result;

    public string? PendingPrompt
    {
        get { lock (_gate) return _pendingPrompt; }
    }

    public UpdateResult? Result
    {
        get { lock (_gate) return _result; }
    }

    public void Report(int done, int total, string file)
    {
        lock (_gate) _progress = (done, total, file);
    }

    public (int Done, int Total, string File) Snapshot()
    {
        lock (_gate) return _progress;
    }

    public bool Ask(string message)
    {
        lock (_gate)
        {
            if (_closed) return false;
            _pendingPrompt = message;
            _answer = null;
            while (_answer is null && !_closed) Monitor.Wait(_gate);
            _pendingPrompt = null;
            return _answer ?? false;
        }
    }

    public void Answer(bool retry)
    {
        lock (_gate)
        {
            if (_pendingPrompt is null || _answer is not null) return;
            _answer = retry;
            Monitor.PulseAll(_gate);
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            Monitor.PulseAll(_gate);
        }
    }

    public void Finish(UpdateResult result)
    {
        lock (_gate) _result = result;
    }
}
