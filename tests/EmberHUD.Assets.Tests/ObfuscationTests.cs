using System.Reflection;
using EmberHUD.Tests;

namespace EmberHUD.Assets.Tests;

public class ObfuscationTests
{
    static readonly Assembly SetupAssembly = typeof(IconIndex).Assembly;

    [Theory]
    [InlineData("IconIndex")]
    public void KeptTypeAndEveryHostTypeItHoldsKeepTheirNames(string name)
    {
        var root = Assert.Single(SetupAssembly.GetTypes(), type => type.Name == name);

        var unmarked = ObfuscationCheck.Unmarked(root, SetupAssembly);

        Assert.True(unmarked.Count == 0, $"missing [Obfuscation(Exclude = true, ApplyToMembers = true)] on {string.Join(", ", unmarked.Select(type => type.FullName))}");
    }
}
