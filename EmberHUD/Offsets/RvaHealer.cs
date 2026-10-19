namespace EmberHUD.Host;

internal static class RvaHealer
{
    private static readonly IReadOnlyDictionary<string, int> None = new Dictionary<string, int>();

    public static bool TryHeal(byte[] file, IReadOnlyList<RvaSig> sigs, IReadOnlyList<RvaRelation> relations, out IReadOnlyDictionary<string, int> healed, out string? failure)
    {
        healed = None;
        failure = null;
        if (!PeImage.TryParse(file, out var image))
        {
            failure = "not a readable PE file";
            return false;
        }

        var result = new Dictionary<string, int>();
        foreach (var group in sigs.GroupBy(s => s.Key))
        {
            var votes = group.Select(s => Vote(file, image, s)).Where(v => v > 0).Distinct().ToList();
            if (votes.Count != 1)
            {
                failure = $"{group.Key}: {(votes.Count == 0 ? "no signature matched" : "signatures disagree")}";
                return false;
            }

            result[group.Key] = votes[0];
        }

        var sigKeys = result.Keys.ToHashSet();
        foreach (var relation in relations)
        {
            if (!result.TryGetValue(relation.Other, out var other) || (sigKeys.Contains(relation.Key) && result[relation.Key] != other + relation.Delta))
            {
                failure = $"{relation.Key}: relation to {relation.Other} broken";
                return false;
            }

            result[relation.Key] = other + relation.Delta;
        }

        healed = result;
        return true;
    }

    private static int Vote(byte[] file, PeImage image, RvaSig sig)
    {
        var hits = image.Find(sig.Pattern, 2);
        if (hits.Count != 1)
        {
            return 0;
        }

        var match = hits[0];
        var disp = (long)match + sig.DispOffset;
        var end = (long)match + sig.EndOffset;
        if (disp < 0 || disp + 4 > file.Length || end < 0 || end > int.MaxValue)
        {
            return 0;
        }

        var endRva = image.RvaOf((int)end);
        if (endRva < 0)
        {
            return 0;
        }

        var target = (long)endRva + BitConverter.ToInt32(file, (int)disp);
        return target >= 1 && target < image.SizeOfImage && image.IsDataRva((int)target) ? (int)target : 0;
    }
}
