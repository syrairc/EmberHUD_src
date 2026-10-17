using System.Reflection;
using EmberHUD.Tests;

namespace EmberHUD.Updater.Tests;

public class ObfuscationTests
{
    static readonly Assembly UpdaterAssembly = typeof(Manifest).Assembly;

    [Theory]
    [InlineData("Manifest")]
    [InlineData("StagedUpdate")]
    [InlineData("UpdateResult")]
    public void KeptTypeAndEveryHostTypeItHoldsKeepTheirNames(string name)
    {
        var root = Assert.Single(UpdaterAssembly.GetTypes(), type => type.Name == name);

        var unmarked = ObfuscationCheck.Unmarked(root, UpdaterAssembly);

        Assert.True(unmarked.Count == 0, $"missing [Obfuscation(Exclude = true, ApplyToMembers = true)] on {string.Join(", ", unmarked.Select(type => type.FullName))}");
    }
}
