using System.Numerics;

namespace TorchlightSDK.Host.Reading;

internal sealed class PlayerSmoother
{
    public const float SnapDistance = 30f;
    public const double MinIntervalSeconds = 0.005;
    public const double MaxIntervalSeconds = 0.25;

    private Vector3 _from;
    private Vector3 _to;
    private long _sampleAt;
    private double _intervalSeconds = MaxIntervalSeconds;
    private bool _started;
    private Vector3 _shown;
    private Vector3 _last;

    public Vector3 Update(Vector3 target, long sampleAt, long now, long frequency)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z)) return _last;

        if (!_started || Vector3.Distance(target, _shown) > SnapDistance)
        {
            _from = _to = _shown = target;
            _sampleAt = sampleAt;
            _started = true;
            return _last = target;
        }

        if (sampleAt != _sampleAt)
        {
            _intervalSeconds = Math.Clamp((sampleAt - _sampleAt) / (double)frequency, MinIntervalSeconds, MaxIntervalSeconds);
            _from = _shown;
            _to = target;
            _sampleAt = sampleAt;
        }

        var t = Math.Clamp((now - _sampleAt) / (double)frequency / _intervalSeconds, 0, 1);
        _shown = Vector3.Lerp(_from, _to, (float)t);
        return _last = _shown;
    }
}
