using EmberHUD.Host;

namespace EmberHUD.Tests;

public class RvaHealerTests
{
    private const int TextRva = 0x1000;

    private static RvaSig Sig(string key, byte marker) => new(key, $"{marker:X2} 01 ? ? ? ?", 2, 6);

    private static byte[] Pe(params (int At, byte Marker, int Disp)[] instructions)
    {
        var text = new byte[0x100];
        foreach (var (at, marker, disp) in instructions)
        {
            text[at] = marker;
            text[at + 1] = 0x01;
            BitConverter.GetBytes(disp).CopyTo(text, at + 2);
        }

        return FakePe.Build((".text", TextRva, text, true));
    }

    private static bool Heal(byte[] file, RvaSig[] sigs, RvaRelation[] relations, out IReadOnlyDictionary<string, int> healed, out string? failure) =>
        RvaHealer.TryHeal(file, sigs, relations, out healed, out failure);

    [Fact]
    public void HealsFromOneUniqueSig()
    {
        var file = Pe((0x10, 0xA1, 0x500));
        Assert.True(Heal(file, [Sig("A", 0xA1)], [], out var healed, out var failure));
        Assert.Null(failure);
        Assert.Equal(0x1516, healed["A"]);
    }

    [Fact]
    public void IgnoresASigThatMatchesTwice()
    {
        var file = Pe((0x10, 0xA1, 0x500), (0x30, 0xA1, 0x900), (0x50, 0xB2, 0x100));
        Assert.True(Heal(file, [Sig("A", 0xA1), Sig("A", 0xB2)], [], out var healed, out _));
        Assert.Equal(0x1156, healed["A"]);
    }

    [Fact]
    public void RejectsDisagreeingSigs()
    {
        var file = Pe((0x10, 0xA1, 0x500), (0x50, 0xB2, 0x500));
        Assert.False(Heal(file, [Sig("A", 0xA1), Sig("A", 0xB2)], [], out var healed, out var failure));
        Assert.Equal("A: signatures disagree", failure);
        Assert.Empty(healed);
    }

    [Fact]
    public void RejectsTargetOutsideTheImage()
    {
        var file = Pe((0x10, 0xA1, 0x10000));
        Assert.False(Heal(file, [Sig("A", 0xA1)], [], out _, out var failure));
        Assert.Equal("A: no signature matched", failure);
    }

    [Fact]
    public void DerivesAKeyFromARelation()
    {
        var file = Pe((0x10, 0xA1, 0x500));
        Assert.True(Heal(file, [Sig("A", 0xA1)], [new("B", "A", 0x20)], out var healed, out _));
        Assert.Equal(0x1516, healed["A"]);
        Assert.Equal(0x1536, healed["B"]);
    }

    [Fact]
    public void RejectsABrokenRelation()
    {
        var file = Pe((0x10, 0xA1, 0x500), (0x50, 0xB2, 0x100));
        Assert.False(Heal(file, [Sig("A", 0xA1), Sig("B", 0xB2)], [new("B", "A", 0x20)], out var healed, out var failure));
        Assert.Equal("B: relation to A broken", failure);
        Assert.Empty(healed);
    }

    [Fact]
    public void AcceptsAHoldingRelation()
    {
        var file = Pe((0x10, 0xA1, 0x500), (0x50, 0xB2, 0x4E0));
        Assert.True(Heal(file, [Sig("A", 0xA1), Sig("B", 0xB2)], [new("B", "A", 0x20)], out var healed, out _));
        Assert.Equal(0x1536, healed["B"]);
    }

    [Fact]
    public void FailsWhenRelationOtherIsMissing()
    {
        var file = Pe((0x10, 0xA1, 0x500));
        Assert.False(Heal(file, [Sig("A", 0xA1)], [new("B", "Z", 1)], out var healed, out var failure));
        Assert.Equal("B: relation to Z broken", failure);
        Assert.Empty(healed);
    }

    [Fact]
    public void FailsOnGarbageBytes()
    {
        Assert.False(Heal(new byte[10], [Sig("A", 0xA1)], [], out var healed, out var failure));
        Assert.Equal("not a readable PE file", failure);
        Assert.Empty(healed);
    }

    [Fact]
    public void FailsWhenNoSigMatches()
    {
        var file = Pe((0x10, 0xA1, 0x500));
        Assert.False(Heal(file, [Sig("A", 0xC3)], [], out _, out var failure));
        Assert.Equal("A: no signature matched", failure);
    }

    [Fact]
    public void ReturnsNothingOnAnyFailure()
    {
        var file = Pe((0x10, 0xA1, 0x500));
        Assert.False(Heal(file, [Sig("A", 0xA1), Sig("B", 0xC3)], [], out var healed, out var failure));
        Assert.Equal("B: no signature matched", failure);
        Assert.Empty(healed);
    }
}
