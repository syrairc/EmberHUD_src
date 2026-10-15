namespace EmberHUD.Host.Reading;

internal sealed class CombatPoller : IDisposable
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(2);

    private readonly Action _poll;
    private readonly Func<bool> _wanted;
    private readonly Action<string> _log;
    private readonly ManualResetEventSlim _stop = new();
    private readonly HashSet<string> _logged = [];
    private readonly Thread _thread;

    public CombatPoller(Action poll, Func<bool> wanted, Action<string> log)
    {
        (_poll, _wanted, _log) = (poll, wanted, log);
        _thread = new Thread(Run) { IsBackground = true, Name = "EmberHUD combat" };
        _thread.Start();
    }

    private void Run()
    {
        while (!_stop.IsSet)
        {
            var wanted = _wanted();
            if (wanted)
            {
                try
                {
                    _poll();
                }
                catch (Exception ex)
                {
                    var cause = ex.GetBaseException().Message;
                    if (_logged.Add(cause)) _log($"combat - {cause}");
                }
            }

            _stop.Wait(wanted ? Offsets.CombatPollMilliseconds : 100);
        }
    }

    public void Dispose()
    {
        _stop.Set();
        if (_thread.Join(JoinTimeout)) _stop.Dispose();
    }
}
