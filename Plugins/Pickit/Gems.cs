using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;

namespace Pickit;

static class Gems
{
    public readonly record struct Gem(Vector2 Centre, float Radius, int Tier, bool Prefix);

    public static Gem[] Layout(GroundItem item, Settings s)
    {
        var height = item.LabelMax.Y - item.LabelMin.Y;
        if (item.LabelMax.X <= item.LabelMin.X || height <= 0) return [];
        var gems = new List<Gem>();
        Side(item.PrefixIds, true, item.LabelMin.X + s.InsetX, 1);
        Side(item.SuffixIds, false, item.LabelMax.X - s.InsetX, -1);
        return [.. gems];

        void Side(int[] ids, bool prefix, float x, float direction)
        {
            var ordered = s.BestIsLowest ? ids.OrderBy(GroundItem.Tier) : ids.OrderByDescending(GroundItem.Tier);
            foreach (var id in ordered.Take(GroundItem.MaxAffixes))
            {
                var tier = GroundItem.Tier(id);
                var radius = s.GemSize * height / 2 * s.Tiers[Math.Clamp(tier, 0, s.Tiers.Count - 1)].Scale;
                gems.Add(new Gem(new Vector2(x, item.LabelMin.Y + s.AnchorY * height), radius, tier, prefix));
                x += direction * radius * 2 * (1 + s.GemSpacing);
            }
        }
    }
}
