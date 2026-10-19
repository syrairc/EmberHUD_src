using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EmberHUD.Host;

internal static class RvaHeal
{
    public static bool TryApply(string exePath, string cachePath, out string note)
    {
        try
        {
            return TryApply(exePath, cachePath, Offsets.Sigs, Offsets.Relations, out note);
        }
        catch (Exception e)
        {
            note = e.Message;
            return false;
        }
    }

    public static bool TryApply(string exePath, string cachePath, IReadOnlyList<RvaSig> sigs, IReadOnlyList<RvaRelation> relations, out string note)
    {
        IReadOnlyDictionary<string, int> healed;
        string? failure;
        try
        {
            var info = new FileInfo(exePath);
            var stamp = $"{info.Length} {info.LastWriteTimeUtc.Ticks} {SigSetHash(sigs, relations)}";
            if (!TryReadCache(cachePath, stamp, sigs, relations, out healed))
            {
                if (!RvaHealer.TryHeal(File.ReadAllBytes(exePath), sigs, relations, out healed, out failure))
                {
                    note = failure ?? "heal failed";
                    return false;
                }

                WriteCache(cachePath, stamp, healed);
            }
        }
        catch (Exception e)
        {
            note = e.Message;
            return false;
        }

        foreach (var (key, value) in healed)
        {
            Offsets.SetPerBuild(key, value);
        }

        note = $"Rvas healed from signatures ({healed.Count} keys), struct offsets from build {Offsets.BuildSizes[Offsets.Build]}, unverified";
        return true;
    }

    private static string SigSetHash(IReadOnlyList<RvaSig> sigs, IReadOnlyList<RvaRelation> relations)
    {
        var text = string.Join('\n', sigs.Select(s => $"S|{s.Key}|{s.Pattern}|{s.DispOffset}|{s.EndOffset}").Concat(relations.Select(r => $"R|{r.Key}|{r.Other}|{r.Delta}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    private static void WriteCache(string cachePath, string stamp, IReadOnlyDictionary<string, int> healed)
    {
        try
        {
            var temp = cachePath + ".tmp";
            var directory = Path.GetDirectoryName(Path.GetFullPath(cachePath));
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(temp, new[] { stamp }.Concat(healed.Select(p => $"{p.Key}=0x{p.Value:X}")));
            File.Move(temp, cachePath, true);
        }
        catch (Exception)
        {
        }
    }

    private static bool TryReadCache(string cachePath, string stamp, IReadOnlyList<RvaSig> sigs, IReadOnlyList<RvaRelation> relations, out IReadOnlyDictionary<string, int> values)
    {
        values = new Dictionary<string, int>();
        try
        {
            var lines = File.ReadAllLines(cachePath);
            if (lines.Length == 0 || lines[0] != stamp)
            {
                return false;
            }

            var read = new Dictionary<string, int>();
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split('=');
                if (parts.Length != 2 || !parts[1].StartsWith("0x", StringComparison.Ordinal)
                    || !int.TryParse(parts[1].AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                {
                    return false;
                }

                read[parts[0]] = value;
            }

            var keys = sigs.Select(s => s.Key).Concat(relations.Select(r => r.Key));
            if (keys.Any(k => !read.ContainsKey(k)) || relations.Any(r => !read.TryGetValue(r.Other, out var other) || read[r.Key] != other + r.Delta))
            {
                return false;
            }

            values = keys.Distinct().ToDictionary(k => k, k => read[k]);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
