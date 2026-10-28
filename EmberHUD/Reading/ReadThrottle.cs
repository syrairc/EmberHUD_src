namespace EmberHUD.Host.Reading;

internal sealed class ReadThrottle<T> where T : class
{
    private T? _last;
    private long _nextRead;
    private int _generation;

    public T? Read(bool wanted, int intervalMilliseconds, long now, int generation, Func<T> read)
    {
        if (!wanted)
        {
            _last = null;
            return null;
        }

        if (intervalMilliseconds > 0 && _last is not null && generation == _generation && now < _nextRead) return _last;
        _generation = generation;
        _nextRead = now + intervalMilliseconds;
        return _last = read();
    }
}
