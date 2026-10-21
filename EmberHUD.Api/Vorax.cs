namespace EmberHUD;

public enum VoraxPhase : byte { Other, Instrument, Round }

public enum VoraxCardKind : byte { Unknown, Monster, Potion, Instrument, Modifier, Bundle }

public readonly record struct VoraxMonster(int Slot, int Id, int Species, int Rarity, int Count, int Activity)
{
    public int Total => Count * Activity;
    public string Name { get; init; } = "";
    public string RarityName { get; init; } = "";
}

public readonly record struct VoraxCard(int Slot, int Id, VoraxCardKind Kind, int Rarity)
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string TypeName { get; init; } = "";
}

public sealed record VoraxState(VoraxPhase Phase, int Round, int Rounds, int Activity, int NextRewardActivity, int RewardsTaken, int RewardsTotal, int Rerolls, int MonsterSlots, VoraxMonster[] Monsters, VoraxCard[] Offered, VoraxCard[] Instruments, int[] Chests);
