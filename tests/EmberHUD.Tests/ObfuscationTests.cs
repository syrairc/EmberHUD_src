using System.Reflection;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public class ObfuscationTests
{
    static readonly Assembly HostAssembly = typeof(CheckRunner).Assembly;

    [Theory]
    [InlineData("SdkSettings")]
    [InlineData("ThemeFile")]
    [InlineData("PluginStage")]
    public void KeptTypeAndEveryHostTypeItHoldsKeepTheirNames(string name)
    {
        var root = Assert.Single(HostAssembly.GetTypes(), type => type.Name == name);

        var unmarked = ObfuscationCheck.Unmarked(root, HostAssembly);

        Assert.True(unmarked.Count == 0, $"missing [Obfuscation(Exclude = true, ApplyToMembers = true)] on {string.Join(", ", unmarked.Select(type => type.FullName))}");
    }

    [Fact]
    public void UnmarkedFindsTypesBehindDictionariesArraysAndProperties()
    {
        var unmarked = ObfuscationCheck.Unmarked(typeof(SyntheticRoot), typeof(ObfuscationTests).Assembly);

        Assert.Equal(new HashSet<Type> { typeof(DictionaryValueLeaf), typeof(ArrayLeaf), typeof(PropertyLeaf), typeof(HalfMarkedLeaf) }, unmarked.ToHashSet());
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    sealed class SyntheticRoot
    {
        public Dictionary<string, DictionaryValueLeaf> ByName = [];
        public ArrayLeaf[] Many = [];
        public PropertyHolder Holder { get; init; } = new();
        public HalfMarkedLeaf Half = new();
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    sealed class PropertyHolder
    {
        public PropertyLeaf Leaf { get; init; } = new();
    }

    sealed class DictionaryValueLeaf;

    sealed class ArrayLeaf;

    sealed class PropertyLeaf;

    [Obfuscation(Exclude = true, ApplyToMembers = false)]
    sealed class HalfMarkedLeaf;
}
