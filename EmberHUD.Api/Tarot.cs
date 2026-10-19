namespace EmberHUD;

public enum TarotPhase : byte { Collect, Board, Final }

public readonly record struct TarotTile(int Index, int CfgId, int Chest);

public readonly record struct TarotCard(int Id, int CfgId);

public sealed record TarotState(TarotPhase Phase, int Round, int Progress, int Hp, int Position, TarotTile[] Tiles, TarotCard[] Hand, int[] Taken, int ChallengesLeft)
{
    public int Playing { get; init; }
}
