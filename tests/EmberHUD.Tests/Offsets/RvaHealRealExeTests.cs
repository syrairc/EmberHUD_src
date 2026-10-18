using EmberHUD.Host;
using EmberHUD.Host.Reading;

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

    [Theory]
    [InlineData(true, false, 20_000, LogicWorldReader.NotCreated, true)]
    [InlineData(true, false, 19_999, LogicWorldReader.NotCreated, false)]
    [InlineData(true, true, 30_000, LogicWorldReader.NotCreated, false)]
    [InlineData(false, false, 30_000, LogicWorldReader.NotCreated, false)]
    [InlineData(true, false, 30_000, "no scene - not in a map", false)]
    [InlineData(true, false, 30_000, null, false)]
    public void RetryHealOnlyOnceOnAnExactBuildAfterTheDelay(bool exact, bool tried, long ms, string? failure, bool expected) =>
        Assert.Equal(expected, RvaHeal.ShouldRetryHeal(exact, tried, ms, failure));
}
