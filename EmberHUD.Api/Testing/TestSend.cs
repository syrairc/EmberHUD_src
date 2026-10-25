namespace EmberHUD.Testing;

public static class TestSend
{
    public static List<SentInput> Record(bool allowed = true)
    {
        var sent = new List<SentInput>();
        Send.Sink = input =>
        {
            lock (sent) sent.Add(input);
        };
        Send.GameForeground = () => allowed;
        return sent;
    }

    public static List<int> Instant(int seed = 1, (int X, int Y)? cursor = null)
    {
        var waits = new List<int>();
        var position = cursor ?? (0, 0);
        Send.Wait = waits.Add;
        Send.Rng = new Random(seed);
        Send.CursorPosition = () => position;
        Send.ToScreen = (x, y) => (x, y);
        return waits;
    }

    public static void OnWait(Action<int> wait) => Send.Wait = wait;

    public static void Reset() => Send.Reset();
}
