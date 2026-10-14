using System.Globalization;
using System.Reflection;
using EmberHUD.Host;

namespace EmberHUD.Tests;

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
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void TheTMapHeaderSizeCoversItsFields()
    {
        Assert.True(Offsets.TMapHeaderSize >= nint.Size);
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TArrayNum + sizeof(int));
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TMapInlineBits + Offsets.TMapInlineBitCount / 8);
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TMapSecondaryBits + nint.Size);
    }

    [Fact]
    public void TheLocAndVarNodeSizesCoverTheirFields()
    {
        Assert.True(Offsets.LocNodeSize >= Offsets.LocNodeValue + nint.Size);
        Assert.True(Offsets.LocNodeSize >= Offsets.LocNodeNext + nint.Size);
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeName + nint.Size);
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeNameLength + sizeof(int));
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeValue + sizeof(int));
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeNext + nint.Size);
    }

    [Fact]
    public void TheCameraPovSizeCoversItsFields()
    {
        Assert.True(Offsets.CameraPovSize >= Offsets.PovLocation + 3 * sizeof(float));
        Assert.True(Offsets.CameraPovSize >= Offsets.PovRotation + 3 * sizeof(float));
        Assert.True(Offsets.CameraPovSize >= Offsets.PovFov + sizeof(float));
    }

    [Fact]
    public void TheGeometryAndHybridHeaderSizesCoverTheirFields()
    {
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryCellSize + sizeof(int));
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryOriginX + sizeof(double));
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryOriginY + sizeof(double));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridCols + sizeof(int));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridRows + sizeof(int));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridCells + nint.Size);
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridIsHybrid + sizeof(byte));
    }
}
