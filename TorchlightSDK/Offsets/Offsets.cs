namespace TorchlightSDK.Host;

internal static partial class Offsets
{
    private static readonly int[] T;

    static Offsets()
    {
        using var stream = typeof(Offsets).Assembly.GetManifestResourceStream("t") ?? throw new InvalidDataException("resource t missing");
        var box = new byte[stream.Length];
        stream.ReadExactly(box);
        T = OffsetTable.Unpack(OffsetTable.Open(box, Seed));
    }
}
