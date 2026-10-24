using System.Numerics;

namespace EmberHUD;

public enum VoraxPhase : byte { Other, Instrument, Round }

public enum VoraxCardKind : byte { Unknown, Monster, Potion, Instrument, Modifier, Bundle }

public readonly record struct VoraxMonster(int Slot, int Id, int Species, int Rarity, int Count, int Activity)
{
    public int Total => Count * Activity;
    public string Name { get; init; } = "";
    public string RarityName { get; init; } = "";
    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }
    public bool HasRect => Max.X > Min.X && Max.Y > Min.Y;
}

public readonly record struct VoraxCard(int Slot, int Id, VoraxCardKind Kind, int Rarity)
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string TypeName { get; init; } = "";
    public Vector2 Min { get; init; }
    public Vector2 Max { get; init; }
    public bool HasRect => Max.X > Min.X && Max.Y > Min.Y;
}

public sealed record VoraxState(VoraxPhase Phase, int Round, int Rounds, int Activity, int NextRewardActivity, int RewardsTaken, int RewardsTotal, int Rerolls, int MonsterSlots, VoraxMonster[] Monsters, VoraxCard[] Offered, VoraxCard[] Instruments, int[] Chests)
{
    public VoraxCard[] Protocols { get; init; } = [];
    public string State { get; init; } = "";
    public int RewardBonusPercent { get; init; }
    public int SelectedCard { get; init; } = -1;
    public int[] SelectedMonsters { get; init; } = [];
}
