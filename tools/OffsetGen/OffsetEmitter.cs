using System.Text;

namespace OffsetGen;

internal static class OffsetEmitter
{
    public static string Emit(IReadOnlyList<string> names, byte[] seed, IReadOnlyList<string> stringNames)
    {
        var source = new StringBuilder();
        source.AppendLine("namespace EmberHUD.Host;");
        source.AppendLine();
        source.AppendLine("internal static partial class Offsets");
        source.AppendLine("{");
        source.AppendLine($"    internal static ReadOnlySpan<byte> Seed => [{string.Join(", ", seed.Select(b => $"0x{b:X2}"))}];");
        for (var i = 0; i < names.Count; i++) source.AppendLine($"    internal static int {names[i]} => T[{i}];");
        for (var i = 0; i < stringNames.Count; i++) source.AppendLine($"    internal static string {stringNames[i]} => S[{i}];");
        source.AppendLine("}");
        return source.ToString();
    }
}
