namespace EmberHUD.Testing;

public static class TestSend
{
    public static List<SentInput> Record(bool allowed = true)
    {
        var sent = new List<SentInput>();
        Send.Sink = sent.Add;
        Send.GameForeground = () => allowed;
        return sent;
    }

    public static void Reset() => Send.Reset();
}
