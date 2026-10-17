using System.Numerics;

namespace EmberHUD;

public readonly record struct GroundItem(int DropId, Vector2 LabelMin, Vector2 LabelMax, Vector3 Position, bool Owned, int[] PrefixIds, int[] SuffixIds)
{
    public bool Legendary { get; init; }

    public const int MaxAffixes = 12;

    // unchecked against the tooltip
    public static int Tier(int affixId) => affixId % 100;
}
