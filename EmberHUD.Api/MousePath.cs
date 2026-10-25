namespace EmberHUD;

public readonly record struct Waypoint(int X, int Y, int DelayMs);

public static class MousePath
{
    static readonly float Sqrt3 = MathF.Sqrt(3f);
    static readonly float Sqrt5 = MathF.Sqrt(5f);

    const float GravityPerMaxStep = 0.6f;
    const int StepBudget = 10_000;

    public static IReadOnlyList<Waypoint> Generate((int X, int Y) from, (int X, int Y) to, HumanizerConfig config, Random rng)
    {
        var cfg = config.Clamped();
        var points = new List<Waypoint>();
        if (from != to) WindMouse(from, to, cfg, rng, points);
        if (cfg.StopTolerance <= 0 || points.Count == 0 || Dist(points[^1].X, points[^1].Y, to.X, to.Y) > MathF.Max(cfg.StopTolerance, 1f)) points.Add(new Waypoint(to.X, to.Y, StepDelay(cfg, rng)));
        return points;
    }

    static void WindMouse((int X, int Y) start, (int X, int Y) end, HumanizerConfig cfg, Random rng, List<Waypoint> output)
    {
        float windX = 0, windY = 0, veloX = 0, veloY = 0;
        float maxStep = cfg.MaxStep;
        float curX = start.X, curY = start.Y;
        float dist;
        float gravity = MathF.Max(cfg.Gravity, cfg.MaxStep * GravityPerMaxStep);

        float axisLength = MathF.Max(Dist(start.X, start.Y, end.X, end.Y), 1f);
        float axisX = (end.X - start.X) / axisLength, axisY = (end.Y - start.Y) / axisLength;
        float stopRadius = MathF.Max(cfg.StopTolerance, 1f);
        float placedX = start.X, placedY = start.Y;
        int budget = StepBudget;
        while (NotArrived(Dist(placedX, placedY, end.X, end.Y), cfg.StopTolerance, stopRadius) && budget-- > 0)
        {
            dist = MathF.Max(Dist(curX, curY, end.X, end.Y), 1f);
            float w = MathF.Min(cfg.Wind, dist);
            if (dist >= cfg.SlowDistance)
            {
                windX = windX / Sqrt3 + (2f * (float)rng.NextDouble() - 1f) * w / Sqrt5;
                windY = windY / Sqrt3 + (2f * (float)rng.NextDouble() - 1f) * w / Sqrt5;
            }
            else
            {
                windX /= Sqrt3;
                windY /= Sqrt3;
                if (maxStep < 3f) maxStep = (float)rng.NextDouble() * 3f + 3f;
                else maxStep /= Sqrt5;
            }

            veloX += windX + gravity * (end.X - curX) / dist;
            veloY += windY + gravity * (end.Y - curY) / dist;

            float cap = MathF.Min(maxStep, dist);
            float veloMag = MathF.Sqrt(veloX * veloX + veloY * veloY);
            if (veloMag > cap)
            {
                float randStep = cap / 2f + (float)rng.NextDouble() * cap / 2f;
                veloX = veloX / veloMag * randStep;
                veloY = veloY / veloMag * randStep;
            }

            float excess = (curX + veloX - start.X) * axisX + (curY + veloY - start.Y) * axisY - axisLength;
            if (excess > 0)
            {
                veloX -= excess * axisX;
                veloY -= excess * axisY;
            }

            curX += veloX;
            curY += veloY;
            placedX = MathF.Round(curX);
            placedY = MathF.Round(curY);
            output.Add(new Waypoint((int)placedX, (int)placedY, StepDelay(cfg, rng)));
        }
    }

    static bool NotArrived(float placedDist, float tolerance, float stopRadius) => tolerance > 0 ? placedDist > stopRadius : placedDist >= 1f;
    static int StepDelay(HumanizerConfig cfg, Random rng) => rng.Next(cfg.StepDelayMin, cfg.StepDelayMax + 1);
    static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
}
