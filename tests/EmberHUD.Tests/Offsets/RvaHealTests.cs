using EmberHUD.Host;

namespace EmberHUD.Tests;

public class RvaHealTests
{
    private const string KeyA = "LogicWorldGlobalRva";
    private const string KeyB = "FightMgrGlobalRva";
    private static readonly RvaSig[] Sigs = [new(KeyA, "A1 01 ? ? ? ?", 2, 6)];
    private static readonly RvaRelation[] Relations = [new(KeyB, KeyA, 0x20)];

    private static byte[] Exe(int disp)
    {
        var text = new byte[0x100];
        text[0x10] = 0xA1;
        text[0x11] = 0x01;
        BitConverter.GetBytes(disp).CopyTo(text, 0x12);
        return FakePe.Build((".text", 0x1000, text, true));
    }

    private static void WithTempDir(Action<string> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "RvaHealTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var snapshot = Offsets.Snapshot();
        try
        {
            body(dir);
        }
        finally
        {
            Offsets.Restore(snapshot);
            Directory.Delete(dir, true);
        }
    }

    private static int Get(string key)
    {
        Assert.True(Offsets.TryGetPerBuild(key, out var value));
        return value;
    }

    [Fact]
    public void AppliesHealedValuesAndWritesTheCache()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out var note));
            Assert.Equal(0x1516, Get(KeyA));
            Assert.Equal(0x1536, Get(KeyB));
            Assert.Equal($"Rvas healed from signatures (2 keys), struct offsets from build {Offsets.BuildSizes[Offsets.Build]}, unverified", note);
            Assert.True(File.Exists(cache));
        });
    }

    [Fact]
    public void SecondCallUsesTheCacheWithoutReadingTheExe()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            var stamp = File.GetLastWriteTimeUtc(exe);
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Offsets.SetPerBuild(KeyA, 1);
            Offsets.SetPerBuild(KeyB, 1);
            File.WriteAllBytes(exe, new byte[new FileInfo(exe).Length]);
            File.SetLastWriteTimeUtc(exe, stamp);
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out var note));
            Assert.Equal($"Rvas healed from signatures (2 keys), struct offsets from build {Offsets.BuildSizes[Offsets.Build]}, unverified", note);
            Assert.Equal(0x1516, Get(KeyA));
            Assert.Equal(0x1536, Get(KeyB));
        });
    }

    [Fact]
    public void StaleCacheIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            File.WriteAllBytes(exe, Exe(0x700));
            File.SetLastWriteTimeUtc(exe, File.GetLastWriteTimeUtc(exe).AddMinutes(1));
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out var note));
            Assert.Equal($"Rvas healed from signatures (2 keys), struct offsets from build {Offsets.BuildSizes[Offsets.Build]}, unverified", note);
            Assert.Equal(0x1716, Get(KeyA));
        });
    }

    [Fact]
    public void GarbageCacheIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            File.WriteAllText(cache, "banana\n\u0000\u0001 junk=0xZZ\n");
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Assert.Equal(0x1516, Get(KeyA));
        });
    }

    [Fact]
    public void CacheWithABrokenRelationIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            var info = new FileInfo(exe);
            File.WriteAllText(cache, $"{info.Length} {info.LastWriteTimeUtc.Ticks}\n{KeyA}=0x1\n{KeyB}=0x2\n");
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Assert.Equal(0x1516, Get(KeyA));
        });
    }

    [Fact]
    public void CacheWithADifferentSizeIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            var info = new FileInfo(exe);
            File.WriteAllText(cache, $"{info.Length + 1} {info.LastWriteTimeUtc.Ticks}\n{KeyA}=0x1\n{KeyB}=0x21\n");
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Assert.Equal(0x1516, Get(KeyA));
        });
    }

    [Fact]
    public void CacheMissingAKeyIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            var info = new FileInfo(exe);
            File.WriteAllText(cache, $"{info.Length} {info.LastWriteTimeUtc.Ticks}\n{KeyA}=0x1\n");
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Assert.Equal(0x1516, Get(KeyA));
            Assert.Equal(0x1536, Get(KeyB));
        });
    }

    [Fact]
    public void SigsOnlyCacheMissingAKeyIsIgnored()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            var info = new FileInfo(exe);
            File.WriteAllText(cache, $"{info.Length} {info.LastWriteTimeUtc.Ticks}\n{KeyB}=0x1\n");
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, [], out _));
            Assert.Equal(0x1516, Get(KeyA));
            Assert.NotEqual(0x1, Get(KeyB));
        });
    }

    [Fact]
    public void CreatesTheCacheDirectory()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            var cache = Path.Combine(dir, "sub", "rvas.cache");
            File.WriteAllBytes(exe, Exe(0x500));
            Assert.True(RvaHeal.TryApply(exe, cache, Sigs, Relations, out _));
            Assert.True(File.Exists(cache));
            Assert.False(File.Exists(cache + ".tmp"));
        });
    }
    [Fact]
    public void FailureLeavesOffsetsUntouched()
    {
        WithTempDir(dir =>
        {
            var exe = Path.Combine(dir, "game.exe");
            File.WriteAllBytes(exe, Exe(0x500));
            var before = Offsets.Snapshot();
            Assert.False(RvaHeal.TryApply(exe, Path.Combine(dir, "rvas.cache"), [new RvaSig(KeyA, "B7 01 ? ? ? ?", 2, 6)], [], out var note));
            Assert.Equal($"{KeyA}: no signature matched", note);
            Assert.Equal(before, Offsets.Snapshot());
            Assert.False(File.Exists(Path.Combine(dir, "rvas.cache")));
        });
    }

    [Fact]
    public void MissingExeReturnsFalseWithoutThrowing()
    {
        WithTempDir(dir =>
        {
            Assert.False(RvaHeal.TryApply(Path.Combine(dir, "nope.exe"), Path.Combine(dir, "rvas.cache"), Sigs, Relations, out var note));
            Assert.NotEmpty(note);
        });
    }
}
