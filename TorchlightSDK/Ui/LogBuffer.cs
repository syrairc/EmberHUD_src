namespace TorchlightSDK.Host.Ui;

internal sealed class LogBuffer(int capacity = 50)
{
    readonly Queue<string> _lines = new();
    readonly Lock _gate = new();

    public void Add(string line)
    {
        lock (_gate)
        {
            if (_lines.Count == capacity) _lines.Dequeue();
            _lines.Enqueue(line);
        }
    }

    public string[] Snapshot()
    {
        lock (_gate) return [.. _lines];
    }
}
