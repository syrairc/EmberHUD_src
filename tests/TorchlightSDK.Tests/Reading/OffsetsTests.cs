using System.Globalization;
using System.Reflection;
using TorchlightSDK.Host;

namespace TorchlightSDK.Tests.Reading;

public class OffsetsTests
{
    [Fact]
    public void EmbeddedTableDecryptsToEveryWantFileValue()
    {
        var want = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "layout.want"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(parts[1][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(parts[1], CultureInfo.InvariantCulture));

        var properties = typeof(Offsets).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.PropertyType == typeof(int))
            .ToDictionary(p => p.Name, p => (long)(int)p.GetValue(null)!);

        Assert.Equal(want.Count, properties.Count);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }
}
