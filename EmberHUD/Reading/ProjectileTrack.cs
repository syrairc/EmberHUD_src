using System.Numerics;

namespace EmberHUD.Host.Reading;

internal sealed class ProjectileTrack(Vector2 position, Vector2 facing)
{
    private Vector2 _position = position;
    private Vector2 _startHeading;
    private float _radius;
    private int _streak;

    public Vector2 Heading { get; private set; } = facing;

    public bool Orbiting { get; private set; }

    public void Update(Vector2 position, Vector2 centre)
    {
        var moved = position - _position;
        if (moved.Length() < Offsets.MinHeadingStep) return;
        _position = position;
        Heading = Vector2.Normalize(moved);
        var offset = position - centre;
        var radius = offset.Length();
        var circling = radius >= Offsets.MinOrbitRadius
            && MathF.Abs(Vector2.Dot(Heading, offset / radius)) * 10 <= Offsets.MaxOrbitRadialTenths
            && (_streak == 0 || MathF.Abs(radius - _radius) * 100 <= radius * Offsets.OrbitRadiusTolerancePercent);
        if (!circling)
        {
            _streak = 0;
            Orbiting = false;
            return;
        }
        if (_streak++ == 0)
        {
            _radius = radius;
            _startHeading = Heading;
        }
        if (!Orbiting) Orbiting = _streak >= Offsets.OrbitFrames && MathF.Acos(Math.Clamp(Vector2.Dot(_startHeading, Heading), -1, 1)) * 100 >= Offsets.MinOrbitTurnHundredths;
    }
}
