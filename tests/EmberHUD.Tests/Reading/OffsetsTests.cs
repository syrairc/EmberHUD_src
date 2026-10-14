using System.Globalization;
using System.Reflection;
using EmberHUD.Host;

namespace EmberHUD.Tests.Reading;

public class OffsetsTests
{
    private static Dictionary<string, string> WantEntries() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "layout.want"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    private static IEnumerable<PropertyInfo> OffsetProperties<T>() =>
        typeof(Offsets).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(p => p.PropertyType == typeof(T));

    [Fact]
    public void EmbeddedTableDecryptsToEveryWantFileValue()
    {
        var want = WantEntries()
            .Where(entry => !entry.Value.StartsWith('"'))
            .ToDictionary(entry => entry.Key, entry => entry.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(entry.Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(entry.Value, CultureInfo.InvariantCulture));

        var properties = OffsetProperties<int>().ToDictionary(p => p.Name, p => (long)(int)p.GetValue(null)!);

        Assert.Equal(want.Count, properties.Count);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void NonAsciiStringsSurviveTheSealedTable()
    {
        Assert.Equal("实体ID", Offsets.TarotEntityVar);
    }

    [Fact]
    public void EmbeddedTableDecryptsToEveryWantFileString()
    {
        var want = WantEntries().Where(entry => entry.Value.StartsWith('"')).ToDictionary(entry => entry.Key, entry => entry.Value.Trim('"'));
        var properties = OffsetProperties<string>().ToDictionary(p => p.Name, p => (string)p.GetValue(null)!);

        Assert.Equal(want.Count, properties.Count);
        Assert.Equal("MiniMap_C", properties["MinimapClassName"]);
        Assert.Equal("torchlight_infinite", properties["GameProcessName"]);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void TheNodeSizeCoversItsFields()
    {
        Assert.True(Offsets.NodeSize >= Offsets.NodeKey + sizeof(uint));
        Assert.True(Offsets.NodeSize >= Offsets.NodeEntity + nint.Size);
        Assert.True(Offsets.NodeSize >= Offsets.NodeNext + nint.Size);
    }

    [Fact]
    public void TheEntityHeaderSizeCoversItsFields()
    {
        Assert.True(Offsets.EntityHeaderSize >= Offsets.EntityCfgId + sizeof(uint));
        Assert.True(Offsets.EntityHeaderSize >= Offsets.EntityAlive + sizeof(byte));
        Assert.True(Offsets.EntityHeaderSize >= Offsets.EntityHasPosition + sizeof(byte));
        Assert.True(Offsets.EntityHeaderSize >= Offsets.EntityRole + nint.Size);
        Assert.True(Offsets.EntityHeaderSize >= Offsets.EntityPosition + nint.Size);
    }
}
