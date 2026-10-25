namespace EmberHUD;

public readonly record struct PathSample(double TimeMs, double X, double Y);

public readonly record struct MetricsRect(double MinX, double MinY, double MaxX, double MaxY);

public readonly record struct PathScores(double DurationMs, double Distance, double PathLength, double MeanSpeed, double PeakSpeed, double FinalError, double Overshoot, double MaxBacktrack, double HeadingJitter, double SpeedRoughness, double LongestStallMs);

public static class PathMetrics
{
    const double StallPixels = 0.5;

    public static PathScores Measure(IReadOnlyList<PathSample> samples, MetricsRect target)
    {
        if (samples.Count == 0) return default;
        var cx = (target.MinX + target.MaxX) / 2;
        var cy = (target.MinY + target.MaxY) / 2;
        var first = samples[0];
        var last = samples[^1];

        var duration = last.TimeMs - first.TimeMs;
        var distance = Math.Sqrt(Sq(last.X - first.X) + Sq(last.Y - first.Y));
        var inside = last.X >= target.MinX && last.X <= target.MaxX && last.Y >= target.MinY && last.Y <= target.MaxY;
        var finalError = inside ? 0 : Math.Sqrt(Sq(last.X - cx) + Sq(last.Y - cy));

        var lineLength = Math.Sqrt(Sq(cx - first.X) + Sq(cy - first.Y));
        var runningMin = lineLength;
        double overshoot = 0, maxBacktrack = 0;
        foreach (var s in samples)
        {
            var toCentre = Math.Sqrt(Sq(cx - s.X) + Sq(cy - s.Y));
            runningMin = Math.Min(runningMin, toCentre);
            maxBacktrack = Math.Max(maxBacktrack, toCentre - runningMin);
            if (lineLength > 0)
            {
                var along = ((s.X - first.X) * (cx - first.X) + (s.Y - first.Y) * (cy - first.Y)) / lineLength;
                overshoot = Math.Max(overshoot, along - lineLength);
            }
        }

        double pathLength = 0, peakSpeed = 0;
        var speeds = new List<(double Speed, double Dt)>();
        var headings = new List<double>();
        var moving = new List<bool>();
        var stepMs = new List<double>();
        for (var i = 1; i < samples.Count; i++)
        {
            var dx = samples[i].X - samples[i - 1].X;
            var dy = samples[i].Y - samples[i - 1].Y;
            var dt = samples[i].TimeMs - samples[i - 1].TimeMs;
            var step = Math.Sqrt(dx * dx + dy * dy);
            pathLength += step;
            moving.Add(step >= StallPixels);
            stepMs.Add(dt);
            if (step >= StallPixels) headings.Add(Math.Atan2(dy, dx));
            if (dt > 0)
            {
                var speed = step / dt * 1000;
                peakSpeed = Math.Max(peakSpeed, speed);
                speeds.Add((speed, dt));
            }
        }

        var meanSpeed = duration > 0 ? pathLength / duration * 1000 : 0;
        return new PathScores(duration, distance, pathLength, meanSpeed, peakSpeed, finalError, overshoot, maxBacktrack, HeadingJitter(headings), SpeedRoughness(speeds, meanSpeed), LongestStall(moving, stepMs));
    }

    public static IReadOnlyList<PathSample> FromWaypoints((int X, int Y) from, IReadOnlyList<Waypoint> path)
    {
        var samples = new List<PathSample>(path.Count + 1) { new(0, from.X, from.Y) };
        double time = 0;
        foreach (var w in path)
        {
            samples.Add(new PathSample(time, w.X, w.Y));
            time += w.DelayMs;
        }
        return samples;
    }

    static double HeadingJitter(List<double> headings)
    {
        if (headings.Count < 2) return 0;
        double sum = 0;
        for (var i = 1; i < headings.Count; i++)
        {
            var d = Math.Abs(headings[i] - headings[i - 1]);
            sum += d > Math.PI ? 2 * Math.PI - d : d;
        }
        return sum / (headings.Count - 1);
    }

    static double SpeedRoughness(List<(double Speed, double Dt)> speeds, double meanSpeed)
    {
        if (speeds.Count < 2 || meanSpeed <= 0) return 0;
        var accel = new List<double>();
        for (var i = 1; i < speeds.Count; i++) accel.Add((speeds[i].Speed - speeds[i - 1].Speed) / (speeds[i].Dt / 1000));
        var mean = accel.Average();
        var std = Math.Sqrt(accel.Sum(a => Sq(a - mean)) / accel.Count);
        return std / meanSpeed;
    }

    static double LongestStall(List<bool> moving, List<double> stepMs)
    {
        var firstMove = moving.IndexOf(true);
        var lastMove = moving.LastIndexOf(true);
        double longest = 0, run = 0;
        for (var i = firstMove + 1; firstMove >= 0 && i < lastMove; i++)
        {
            run = moving[i] ? 0 : run + stepMs[i];
            longest = Math.Max(longest, run);
        }
        return longest;
    }

    static double Sq(double v) => v * v;
}
