using System.Reflection;
using EmberHUD.Tests;

namespace EmberHUD.Launcher.Tests;

public class ObfuscationTests
{
    static readonly Assembly LauncherAssembly = typeof(LauncherConfig).Assembly;

    [Theory]
    [InlineData("LauncherConfig")]
    public void KeptTypeAndEveryHostTypeItHoldsKeepTheirNames(string name)
    {
        var root = Assert.Single(LauncherAssembly.GetTypes(), type => type.Name == name);

        var unmarked = ObfuscationCheck.Unmarked(root, LauncherAssembly);

        Assert.True(unmarked.Count == 0, $"missing [Obfuscation(Exclude = true, ApplyToMembers = true)] on {string.Join(", ", unmarked.Select(type => type.FullName))}");
    }
}
