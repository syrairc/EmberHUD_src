namespace EmberHUD.Host;

internal static partial class Offsets
{
    private static readonly int[] T;
    private static readonly string[] S;

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
