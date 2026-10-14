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

        var unmarked = Unmarked(root, HostAssembly);

        Assert.True(unmarked.Count == 0, $"missing [Obfuscation(Exclude = true, ApplyToMembers = true)] on {string.Join(", ", unmarked.Select(type => type.FullName))}");
    }

    [Fact]
    public void UnmarkedFindsTypesBehindDictionariesArraysAndProperties()
    {
        var unmarked = Unmarked(typeof(SyntheticRoot), typeof(ObfuscationTests).Assembly);

        Assert.Equal(new HashSet<Type> { typeof(DictionaryValueLeaf), typeof(ArrayLeaf), typeof(PropertyLeaf), typeof(HalfMarkedLeaf) }, unmarked.ToHashSet());
    }

    static List<Type> Unmarked(Type root, Assembly scope)
    {
        var reached = new HashSet<Type>();
        Walk(root, scope, reached);
        return [.. reached.Where(type => !IsMarked(type))];
    }

    static void Walk(Type type, Assembly scope, HashSet<Type> reached)
    {
        if (type.HasElementType)
        {
            Walk(type.GetElementType()!, scope, reached);
            return;
        }
        foreach (var argument in type.GenericTypeArguments) Walk(argument, scope, reached);
        if (type.Assembly != scope || !reached.Add(type)) return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) Walk(property.PropertyType, scope, reached);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Walk(field.FieldType, scope, reached);
    }

    static bool IsMarked(Type type)
    {
        for (var declaring = type; declaring is not null; declaring = declaring.DeclaringType)
            if (declaring.GetCustomAttribute<ObfuscationAttribute>() is { } attribute)
                return attribute.Exclude && attribute.ApplyToMembers;
        return false;
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
