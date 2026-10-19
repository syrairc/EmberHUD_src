using System.Globalization;

namespace EmberHUD.Host;

internal static partial class Offsets
{
    private static readonly int[] T;
    private static readonly string[] S;

    internal static int Build;

    internal static bool SelectBuild(long exeSize)
    {
        var best = 0;
        for (var i = 1; i < BuildSizes.Length; i++)
            if (Math.Abs(BuildSizes[i] - exeSize) <= Math.Abs(BuildSizes[best] - exeSize)) best = i;
        Build = best;
        return BuildSizes[best] == exeSize;
    }

    private static readonly Lazy<(IReadOnlyList<RvaSig> Sigs, IReadOnlyList<RvaRelation> Relations)> Parsed = new(() => ParseSigLines(SigLines));

    internal static IReadOnlyList<RvaSig> Sigs => Parsed.Value.Sigs;

    internal static IReadOnlyList<RvaRelation> Relations => Parsed.Value.Relations;

    internal static (IReadOnlyList<RvaSig> Sigs, IReadOnlyList<RvaRelation> Relations) ParseSigLines(IEnumerable<string> lines)
    {
        var sigs = new List<RvaSig>();
        var relations = new List<RvaRelation>();
        foreach (var parts in lines.Select(line => line.Split('|')))
        {
            if (parts[0] == "S") sigs.Add(new RvaSig(parts[1], parts[2], int.Parse(parts[3], CultureInfo.InvariantCulture), int.Parse(parts[4], CultureInfo.InvariantCulture)));
            else if (parts[0] == "R") relations.Add(new RvaRelation(parts[1], parts[2], int.Parse(parts[3], CultureInfo.InvariantCulture)));
        }
        return (sigs, relations);
    }

    private static int PerBuildSlot(string key)
    {
        var index = Array.IndexOf(BuildKeys, key);
        return index < 0 ? -1 : PerBuildStart + index + Build * BuildKeys.Length;
    }

    internal static bool TryGetPerBuild(string key, out int value)
    {
        var slot = PerBuildSlot(key);
        value = slot < 0 ? 0 : T[slot];
        return slot >= 0;
    }

    internal static bool SetPerBuild(string key, int value)
    {
        var slot = PerBuildSlot(key);
        if (slot >= 0) T[slot] = value;
        return slot >= 0;
    }

    internal static int[] Snapshot() => (int[])T.Clone();

    internal static void Restore(int[] snapshot) => snapshot.CopyTo(T, 0);

    static Offsets()
    {
        using var stream = typeof(Offsets).Assembly.GetManifestResourceStream("t") ?? throw new InvalidDataException("resource t missing");
        var box = new byte[stream.Length];
        stream.ReadExactly(box);
        var packed = OffsetTable.Open(box, Seed);
        T = OffsetTable.Unpack(packed);
        S = OffsetTable.UnpackStrings(packed);
    }
}
