using EmberHUD.Host;

namespace EmberHUD.Tests;

public class RvaHealRealExeTests
{
    [Fact]
    public void HealedRvasMatchTheShippedBuildSections()
    {
        var paths = (Environment.GetEnvironmentVariable("TLI_EXES") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0 || paths.Any(p => !File.Exists(p))) return;

        var snapshot = Offsets.Snapshot();
        var build = Offsets.Build;
        try
        {
            foreach (var path in paths)
            {
                Assert.True(Offsets.SelectBuild(new FileInfo(path).Length), path);
                Assert.True(RvaHealer.TryHeal(File.ReadAllBytes(path), Offsets.Sigs, Offsets.Relations, out var healed, out var failure), $"{path}: {failure}");
                Assert.NotEmpty(healed);
                foreach (var (key, value) in healed)
                {
                    Assert.True(Offsets.TryGetPerBuild(key, out var expected), key);
                    Assert.True(expected == value, $"{path}: {key} healed 0x{value:X} shipped 0x{expected:X}");
                }
            }
        }
        finally
        {
            Offsets.Restore(snapshot);
            Offsets.Build = build;
        }
    }
}
