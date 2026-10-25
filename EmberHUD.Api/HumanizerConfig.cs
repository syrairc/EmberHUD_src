namespace EmberHUD;

public sealed record HumanizerConfig
{
    public float MaxStep { get; init; } = 15;
    public int StepDelayMin { get; init; } = 4;
    public int StepDelayMax { get; init; } = 9;
    public int PreClickSettleMin { get; init; } = 20;
    public int PreClickSettleMax { get; init; } = 60;
    public int ClickDwellMin { get; init; } = 40;
    public int ClickDwellMax { get; init; } = 90;
    public int PostClickSettleMin { get; init; } = 30;
    public int PostClickSettleMax { get; init; } = 90;
    public float Gravity { get; init; } = 9;
    public float Wind { get; init; } = 3;
    public float SlowDistance { get; init; } = 8;

    public HumanizerConfig Clamped()
    {
        var (stepMin, stepMax) = Range(StepDelayMin, StepDelayMax);
        var (preMin, preMax) = Range(PreClickSettleMin, PreClickSettleMax);
        var (dwellMin, dwellMax) = Range(ClickDwellMin, ClickDwellMax);
        var (postMin, postMax) = Range(PostClickSettleMin, PostClickSettleMax);
        return this with
        {
            MaxStep = MathF.Max(1, MaxStep),
            Gravity = MathF.Max(0, Gravity),
            Wind = MathF.Max(0, Wind),
            SlowDistance = MathF.Max(0, SlowDistance),
            StepDelayMin = stepMin, StepDelayMax = stepMax,
            PreClickSettleMin = preMin, PreClickSettleMax = preMax,
            ClickDwellMin = dwellMin, ClickDwellMax = dwellMax,
            PostClickSettleMin = postMin, PostClickSettleMax = postMax,
        };
    }

    static (int Min, int Max) Range(int min, int max)
    {
        min = Math.Max(0, min);
        return (min, Math.Max(min, max));
    }
}
