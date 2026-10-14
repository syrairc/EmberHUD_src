using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EmberHUD.Host;

namespace OffsetGen;

internal static class LayoutParser
{
    internal sealed record BuildTable(string[] Names, long[] Sizes, int[] Values);

    public static IReadOnlyList<(string Name, int Value)> Parse(string text) => Ints(Sections(text).Base);

    public static IReadOnlyList<(string Name, string Value)> ParseStrings(string text)
    {
        var entries = new List<(string Name, string Value)>();
        foreach (var (name, raw) in Lines(Sections(text).Base))
        {
            if (!raw.StartsWith('"')) continue;
            if (raw.Length < 3 || !raw.EndsWith('"')) throw new FormatException($"bad string '{raw}' for {name}");
            entries.Add((name, raw[1..^1]));
        }

        return entries;
    }

    public static BuildTable ParseBuilds(string text, IEnumerable<string> baseNames)
    {
        var (baseText, sections) = Sections(text);
        var baseSet = baseNames.ToHashSet();
        if (baseSet.FirstOrDefault(name => name.EndsWith("Rva")) is { } stray) throw new FormatException($"'{stray}' ends in Rva so it belongs in a [build] section");
        if (sections.Count == 0) throw new FormatException("no [build <exe size>] section");
        var builds = sections.Select(section => (section.Size, Entries: Ints(section.Body))).ToList();
        var names = builds[0].Entries.Select(entry => entry.Name).ToArray();
        if (names.Length == 0) throw new FormatException($"build {builds[0].Size} has no keys");
        foreach (var (size, entries) in builds)
        {
            foreach (var entry in entries)
            {
                if (!entry.Name.EndsWith("Rva")) throw new FormatException($"build {size}: '{entry.Name}' does not end in Rva, only Rva keys differ per build");
                if (baseSet.Contains(entry.Name)) throw new FormatException($"build {size}: '{entry.Name}' is also in the base");
            }
            var missing = names.Except(entries.Select(entry => entry.Name)).FirstOrDefault();
            if (missing is not null) throw new FormatException($"build {size} is missing '{missing}'");
            var extra = entries.Select(entry => entry.Name).Except(names).FirstOrDefault();
            if (extra is not null) throw new FormatException($"build {size} has '{extra}' which build {builds[0].Size} does not");
        }

        return new BuildTable(names, [.. builds.Select(build => build.Size)], [.. builds.SelectMany(build => names.Select(name => build.Entries.First(entry => entry.Name == name).Value))]);
    }

    private static (string Base, List<(long Size, string Body)> Builds) Sections(string text)
    {
        var baseText = new StringBuilder();
        var builds = new List<(long Size, StringBuilder Body)>();
        var current = baseText;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("[build", StringComparison.Ordinal))
            {
                current.AppendLine(line);
                continue;
            }

            if (!line.EndsWith(']') || !long.TryParse(line[6..^1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size <= 0)
                throw new FormatException($"bad build header '{line}'");
            if (builds.Any(build => build.Size == size)) throw new FormatException($"duplicate build {size}");
            current = new StringBuilder();
            builds.Add((size, current));
        }

        return (baseText.ToString(), [.. builds.Select(build => (build.Size, build.Body.ToString()))]);
    }

    private static List<(string Name, int Value)> Ints(string body)
    {
        var entries = new List<(string Name, int Value)>();
        foreach (var (name, raw) in Lines(body))
        {
            if (raw.StartsWith('"')) continue;
            var parsed = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) && hex <= int.MaxValue ? (int?)hex : null
                : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : null;
            entries.Add((name, parsed ?? throw new FormatException($"bad value '{raw}' for {name}")));
        }

        return entries;
    }

    private static IEnumerable<(string Name, string Raw)> Lines(string text)
    {
        var seen = new HashSet<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) throw new FormatException($"expected 'Name = value': {line}");
            var name = parts[0];
            if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_') || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new FormatException($"bad name '{name}'");
            if (!seen.Add(name)) throw new FormatException($"duplicate name '{name}'");
            yield return (name, parts[1]);
        }
    }
}

internal static class OffsetEmitter
{
    public static string Emit(IReadOnlyList<string> names, byte[] seed, IReadOnlyList<string> stringNames, IReadOnlyList<string>? rvaNames = null, IReadOnlyList<long>? buildSizes = null)
    {
        rvaNames ??= [];
        var source = new StringBuilder();
        source.AppendLine("namespace EmberHUD.Host;");
        source.AppendLine();
        source.AppendLine("internal static partial class Offsets");
        source.AppendLine("{");
        source.AppendLine($"    internal static ReadOnlySpan<byte> Seed => [{string.Join(", ", seed.Select(b => $"0x{b:X2}"))}];");
        for (var i = 0; i < names.Count; i++) source.AppendLine($"    internal static int {names[i]} => T[{i}];");
        for (var j = 0; j < rvaNames.Count; j++) source.AppendLine($"    internal static int {rvaNames[j]} => T[{names.Count + j} + Build * {rvaNames.Count}];");
        for (var i = 0; i < stringNames.Count; i++) source.AppendLine($"    internal static string {stringNames[i]} => S[{i}];");
        if (buildSizes is { Count: > 0 }) source.AppendLine($"    internal static readonly long[] BuildSizes = [{string.Join(", ", buildSizes)}];");
        source.AppendLine("}");
        return source.ToString();
    }

    public static byte[] Pack(IReadOnlyList<int> ints, IReadOnlyList<string> strings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(ints.Count);
        foreach (var value in ints) writer.Write(value);
        writer.Write(strings.Count);
        foreach (var text in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        return stream.ToArray();
    }

    public static byte[] Seal(ReadOnlySpan<byte> plain, ReadOnlySpan<byte> key)
    {
        var box = new byte[OffsetTable.NonceSize + OffsetTable.TagSize + plain.Length];
        RandomNumberGenerator.Fill(box.AsSpan(0, OffsetTable.NonceSize));
        using var aes = new AesGcm(key, OffsetTable.TagSize);
        aes.Encrypt(box.AsSpan(0, OffsetTable.NonceSize), plain, box.AsSpan(OffsetTable.NonceSize + OffsetTable.TagSize), box.AsSpan(OffsetTable.NonceSize, OffsetTable.TagSize));
        return box;
    }
}
