using System.Globalization;
using System.Reflection;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class OffsetsTests
{
    private static string[] WantLines() => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "layout.want")).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();

    private static Dictionary<string, string> WantEntries() =>
        WantLines()
            .TakeWhile(line => !line.StartsWith("[build"))
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    private static Dictionary<long, Dictionary<string, long>> WantBuilds()
    {
        var builds = new Dictionary<long, Dictionary<string, long>>();
        Dictionary<string, long>? current = null;
        foreach (var line in WantLines().SkipWhile(line => !line.StartsWith("[build")))
        {
            if (line.StartsWith("[build"))
            {
                current = builds[long.Parse(line[6..^1], CultureInfo.InvariantCulture)] = [];
                continue;
            }
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            current![parts[0]] = parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? long.Parse(parts[1][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : long.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        return builds;
    }

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
        var rvaNames = WantBuilds().Values.First().Keys;

        Assert.Equal(want.Count + rvaNames.Count, properties.Count);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void EveryBuildHasEveryRvaValueFromTheWantFile()
    {
        var builds = WantBuilds();
        Assert.Equal(builds.Keys.Order(), Offsets.BuildSizes.Order());
        var perBuildNames = builds.Values.First().Keys.ToHashSet();
        var properties = OffsetProperties<int>().Where(p => perBuildNames.Contains(p.Name)).ToDictionary(p => p.Name);
        var original = Offsets.Build;
        try
        {
            foreach (var (size, values) in builds)
            {
                Assert.True(Offsets.SelectBuild(size));
                Assert.Equal(properties.Keys.Order(), values.Keys.Order());
                foreach (var (name, value) in values)
                    Assert.Equal(value, (long)(int)properties[name].GetValue(null)!);
            }
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void TheSteamBuildHasThePortedRvas()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(124465080));
            Assert.Equal(0x6A5F388, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x6A5DCA8, Offsets.FightMgrGlobalRva);
            Assert.Equal(0x6A5FC70, Offsets.PlayerMgrGlobalRva);
            Assert.Equal(0x6A5FC48, Offsets.MiniMapMgrGlobalRva);
            Assert.Equal(0x6A60588, Offsets.WoodStakeMgrGlobalRva);
            Assert.Equal(0x8101AE8, Offsets.GObjectsGlobalRva);
            Assert.Equal(0x80E8E40, Offsets.FNamePoolRva);
            Assert.Equal(0x8248A90, Offsets.GEngineGlobalRva);
            Assert.Equal(0x782ADA0, Offsets.MonsterCfgBucketsRva);
            Assert.Equal(0x782ADAC, Offsets.MonsterCfgBucketCountRva);
            Assert.Equal(0x779A8BA, Offsets.LanguageRva);
            Assert.Equal(0x77DABA0, Offsets.LanguageBucketsRva);
            Assert.Equal(0x77DABAC, Offsets.LanguageBucketCountRva);
            Assert.Equal(0x6F51C80, Offsets.MapIconInfoBucketsRva);
            Assert.Equal(0x6F51C8C, Offsets.MapIconInfoBucketCountRva);

            Assert.True(Offsets.SelectBuild(123144120));
            Assert.Equal(0x69331B8, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x7FBCC80, Offsets.FNamePoolRva);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void TheSeventeenthBuildHasItsOwnRvasAndMovedStructOffsets()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(126258104));
            Assert.Equal(0x6BFC1A8, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x6BFD400, Offsets.WoodStakeMgrGlobalRva);
            Assert.Equal(0x710, Offsets.FightMapNpc);
            Assert.Equal(0x8A0, Offsets.FightMapServant);
            Assert.Equal(142, Offsets.EntityHasPosition);
            Assert.Equal(936, Offsets.EntityPosition);
            Assert.Equal(944, Offsets.EntityHeaderSize);
            Assert.Equal(768, Offsets.EntityRole);
            Assert.Equal(376, Offsets.WoodStakeStatistic);
            Assert.Equal(224, Offsets.WoodStakeUnlimitedTotal);
            Assert.Equal(0xD0, Offsets.FightMapPlayer);

            Assert.True(Offsets.SelectBuild(123144120));
            Assert.Equal(0x6C0, Offsets.FightMapNpc);
            Assert.Equal(920, Offsets.EntityPosition);
            Assert.True(Offsets.SelectBuild(124465080));
            Assert.Equal(0x6C0, Offsets.FightMapNpc);
            Assert.Equal(752, Offsets.EntityRole);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Theory]
    [InlineData(123144120, true, 123144120)]
    [InlineData(124465080, true, 124465080)]
    [InlineData(126258104, true, 126258104)]
    [InlineData(127223224, true, 127223224)]
    [InlineData(123804599, false, 123144120)]
    [InlineData(123804600, false, 124465080)]
    [InlineData(124000000, false, 124465080)]
    [InlineData(125361591, false, 124465080)]
    [InlineData(125361592, false, 126258104)]
    [InlineData(126740663, false, 126258104)]
    [InlineData(126740664, false, 127223224)]
    [InlineData(100, false, 123144120)]
    [InlineData(999999999, false, 127223224)]
    public void SelectBuildPicksExactThenClosestThenLater(long exeSize, bool exact, long expectedBuild)
    {
        var original = Offsets.Build;
        try
        {
            Assert.Equal(exact, Offsets.SelectBuild(exeSize));
            Assert.Equal(expectedBuild, Offsets.BuildSizes[Offsets.Build]);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void NonAsciiStringsSurviveTheSealedTable()
    {
        Assert.Equal("实体ID", Offsets.TarotEntityVar);
    }

    [Fact]
    public void TheHpBarKeysAreTheProbedValues()
    {
        Assert.Equal("ERoleUIComponent", Offsets.HpBarComponentClassName);
        Assert.Equal(0xC0, Offsets.ComponentEntity);
        Assert.Equal(0x128, Offsets.HpBarWidgetWeak);
        Assert.Equal("HpBarMonster_C", Offsets.HpBarNormalClassName);
        Assert.Equal("HpBarMonsterElite_C", Offsets.HpBarEliteClassName);
        Assert.Equal(0x308, Offsets.HpBarNormalTrack);
        Assert.Equal(0x2E0, Offsets.HpBarEliteTrack);
    }

    [Fact]
    public void NpcNameKeyIsTheNpcNameLocPrefix()
    {
        Assert.Equal("npc|name|", Offsets.NpcNameKey);
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
