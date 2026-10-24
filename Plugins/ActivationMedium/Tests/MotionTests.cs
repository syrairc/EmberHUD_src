using System.Numerics;
using EmberHUD.Testing;

namespace ActivationMedium;

public sealed class MotionTests
{
    [Test]
    public void CountsUpWhileStandingStill()
    {
        var motion = new Motion();
        Check.Equal(0L, motion.StillMs(new Vector3(10, 10, 0), 1000), "first sight starts the clock");
        Check.Equal(80L, motion.StillMs(new Vector3(10, 10, 0), 1080));
        Check.Equal(80L, motion.StillMs(new Vector3(10.4f, 10, 5), 1080), "sub-unit drift and height changes do not count as moving");
    }

    [Test]
    public void MovingRestartsTheClock()
    {
        var motion = new Motion();
        motion.StillMs(new Vector3(0, 0, 0), 0);
        Check.Equal(0L, motion.StillMs(new Vector3(30, 0, 0), 100), "moved");
        Check.Equal(50L, motion.StillMs(new Vector3(30, 0, 0), 150));
    }

    [Test]
    public void NoPlayerResets()
    {
        var motion = new Motion();
        motion.StillMs(new Vector3(0, 0, 0), 0);
        Check.Equal(0L, motion.StillMs(null, 500));
        Check.Equal(0L, motion.StillMs(new Vector3(0, 0, 0), 600), "back, clock restarts");
    }
}
