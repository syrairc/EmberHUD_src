namespace EmberHUD;

public enum FrozenCanvasPhase : byte { Idle, Draw, Battle }

public enum FrozenCanvasScreen : byte { None, Paper, Preview, Bag, Placing, Choice, Complete, Battle }

public enum FrozenCanvasPlotKind : byte { Unknown, Free, Void, Ruins, Block, Edge }

public readonly record struct FrozenCanvasPaint(int Id, int Series, int Level, bool IsMonster)
{
    public string Name { get; init; } = "";
}

public readonly record struct FrozenCanvasPlot(int Slot, int X, int Y, FrozenCanvasPlotKind Kind, int PaintId, int Level, bool Servant, bool Player)
{
    public string PaintName { get; init; } = "";
    public FrozenCanvasPaint[] Stacked { get; init; } = [];
}

public readonly record struct FrozenCanvasPaper(int Level, string Name, string Description, int Cost, bool Locked, bool Selected);

public readonly record struct FrozenCanvasReward(int ItemId, int Count);

public sealed record FrozenCanvasObjective(string Description, int Done, float SecondsLeft);

public sealed record FrozenCanvasDraw(FrozenCanvasScreen Screen, int Placed, int Total, int MaxLevel, int Revives, int ReviveMax, int Rerolls, int RerollsMax, int Uninstalls, bool CanUninstall, string Mission, string MissionDetail, FrozenCanvasPaint[] Bag, FrozenCanvasPaper[] Papers, int MaxPaper);

public sealed record FrozenCanvasState(FrozenCanvasPhase Phase, int Difficulty, FrozenCanvasPlot[] Plots, int PlotsDone, int PlotsTotal, int PlotsTarget, int ChallengesLeft, int ChallengesTotal, FrozenCanvasReward[] Rewards)
{
    public FrozenCanvasDraw? Draw { get; init; }
    public FrozenCanvasObjective? Objective { get; init; }
}
