using System.Numerics;

namespace EmberHUD;

public readonly record struct GroundItem(int DropId, Vector2 LabelMin, Vector2 LabelMax, Vector3 Position, bool Owned, int[] PrefixIds, int[] SuffixIds)
{
    public bool Legendary { get; init; }

    public int SpecialId { get; init; }

    public int BaseId { get; init; }

    public int Count { get; init; } = 1;

    public int RareValue { get; init; }

    public bool Equipment { get; init; }

    public const int MaxAffixes = 12;

    // unchecked against the tooltip
    public static int Tier(int affixId) => affixId % 100;
}
