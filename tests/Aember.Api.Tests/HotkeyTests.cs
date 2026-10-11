namespace Aember.Api.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void ToStringPutsModifiersInCtrlShiftAltOrder()
    {
        Assert.Equal("F10", new Hotkey(Key.F10).ToString());
        Assert.Equal("Ctrl+F12", new Hotkey(Key.F12, KeyModifiers.Ctrl).ToString());
        Assert.Equal("Ctrl+Shift+A", new Hotkey(Key.A, KeyModifiers.Shift | KeyModifiers.Ctrl).ToString());
        Assert.Equal("Ctrl+Alt+A", new Hotkey(Key.A, KeyModifiers.Alt | KeyModifiers.Ctrl).ToString());
        Assert.Equal("0xE2", new Hotkey((Key)0xE2).ToString());
    }

    [Theory]
    [InlineData("F10", Key.F10, KeyModifiers.None)]
    [InlineData("Ctrl+F12", Key.F12, KeyModifiers.Ctrl)]
    [InlineData("Ctrl+Shift+A", Key.A, KeyModifiers.Ctrl | KeyModifiers.Shift)]
    [InlineData("alt + ctrl + d1", Key.D1, KeyModifiers.Ctrl | KeyModifiers.Alt)]
    [InlineData("0x7B", Key.F12, KeyModifiers.None)]
    [InlineData("Shift+0xE2", (Key)0xE2, KeyModifiers.Shift)]
    public void TryParseReadsTheTextForm(string text, Key key, KeyModifiers mods)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(key, mods), hotkey);
    }

    [Theory]
    [InlineData("None")]
    [InlineData(" none ")]
    public void TryParseReadsNoneAsUnbound(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(default, hotkey);
    }

    [Theory]
    [InlineData("Ctrl+None")]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl")]
    [InlineData("Alt")]
    [InlineData("Shift+Ctrl")]
    [InlineData("Meta+F1")]
    [InlineData("Escape")]
    [InlineData("1")]
    [InlineData("A, B")]
    [InlineData("0xZZ")]
    [InlineData("0x1B")]
    [InlineData("0x11")]
    [InlineData("0xFF")]
    public void TryParseRejectsAnythingThatIsNotABindableKey(string text)
    {
        Assert.False(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(default, hotkey);
    }

    [Fact]
    public void TryParseRejectsNull()
    {
        Assert.False(Hotkey.TryParse(null, out var hotkey));
        Assert.Equal(default, hotkey);
    }

    [Fact]
    public void EveryNamedKeyRoundTrips()
    {
        foreach (var key in Enum.GetValues<Key>().Where(key => Hotkey.IsBindable((int)key)))
        {
            var hotkey = new Hotkey(key, KeyModifiers.Ctrl | KeyModifiers.Alt);
            Assert.True(Hotkey.TryParse(hotkey.ToString(), out var back));
            Assert.Equal(hotkey, back);
        }
    }

    [Theory]
    [InlineData(0x01, false)]
    [InlineData(0x1B, false)]
    [InlineData(0x11, false)]
    [InlineData(0xA2, false)]
    [InlineData(0x5B, false)]
    [InlineData(0xFF, false)]
    [InlineData(0x08, true)]
    [InlineData(0x41, true)]
    [InlineData(0x7B, true)]
    public void BindableKeys(int vk, bool bindable) => Assert.Equal(bindable, Hotkey.IsBindable(vk));

    [Fact]
    public void ModifierKeysHaveNamesButNeverBind()
    {
        Assert.Equal(0x10, (int)Key.Shift);
        Assert.Equal(0x11, (int)Key.Ctrl);
        Assert.Equal(0x12, (int)Key.Alt);
        Assert.All([Key.Shift, Key.Ctrl, Key.Alt], key => Assert.False(Hotkey.IsBindable((int)key)));
    }
}
