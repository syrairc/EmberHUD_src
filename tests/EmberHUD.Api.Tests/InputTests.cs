namespace EmberHUD.Api.Tests;

public sealed class InputTests : IDisposable
{
    const int VkControl = 0x11;
    const int VkShift = 0x10;
    const int VkAlt = 0x12;
    const int VkF5 = 0x74;
    const int VkF6 = 0x75;
    const int VkF12 = 0x7B;

    readonly HashSet<int> _keys = [];

    public InputTests()
    {
        Input.KeyDown = _keys.Contains;
        Input.CancelCapture();
        Input.TryCapture(out _);
        Input.NewFrame(false);
        Input.NewFrame(false);
    }

    public void Dispose()
    {
        _keys.Clear();
        Input.CancelCapture();
        Input.NewFrame(false);
        Input.NewFrame(false);
    }

    [Fact]
    public void PressedFiresOnTheFrameTheKeyGoesDownOnly()
    {
        _keys.Add(VkF12);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12)));
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
        Assert.True(Input.IsDown(Key.F12));
    }

    [Fact]
    public void PressedFiresAgainAfterARelease()
    {
        _keys.Add(VkF12);
        Input.NewFrame(false);
        _keys.Remove(VkF12);
        Input.NewFrame(false);
        _keys.Add(VkF12);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12)));
    }

    [Fact]
    public void ModifiersMatchExactly()
    {
        _keys.UnionWith([VkControl, VkF12]);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl | KeyModifiers.Shift)));
    }

    [Fact]
    public void AStrayModifierBlocksTheBareHotkey()
    {
        _keys.UnionWith([VkShift, VkF12]);
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
    }

    [Fact]
    public void ASuppressedFrameBlocksPressedUnlessAskedToIgnoreIt()
    {
        _keys.UnionWith([VkControl, VkF12]);
        Input.NewFrame(true);
        Assert.True(Input.Typing);
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl)));
        Assert.True(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl), ignoreSuppression: true));
        Assert.True(Input.IsDown(Key.F12));
    }

    [Fact]
    public void HeldModifiersReadThroughIsDown()
    {
        _keys.UnionWith([VkControl, VkAlt]);
        Input.NewFrame(false);
        Assert.True(Input.IsDown(Key.Ctrl));
        Assert.True(Input.IsDown(Key.Alt));
        Assert.False(Input.IsDown(Key.Shift));
    }

    [Fact]
    public void KeyNoneNeverFires()
    {
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.None)));
    }

    [Fact]
    public void CaptureReportsTheFirstNewKeyWithItsModifiers()
    {
        Input.BeginCapture();
        _keys.UnionWith([VkControl, VkF5]);
        Input.NewFrame(false);

        Assert.False(Input.Capturing);
        Assert.True(Input.TryCapture(out var hotkey));
        Assert.Equal(new Hotkey(Key.F5, KeyModifiers.Ctrl), hotkey);
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void CaptureIgnoresAKeyThatWasAlreadyDownWhenItStarted()
    {
        _keys.Add(VkF5);
        Input.BeginCapture();
        Input.NewFrame(false);
        Assert.True(Input.Capturing);
        Assert.False(Input.TryCapture(out _));

        _keys.Add(VkF6);
        Input.NewFrame(false);
        Assert.True(Input.TryCapture(out var hotkey));
        Assert.Equal(Key.F6, hotkey.Key);
    }

    [Fact]
    public void CaptureIgnoresModifiersAlone()
    {
        Input.BeginCapture();
        _keys.UnionWith([VkControl, VkShift, 0xA2]);
        Input.NewFrame(false);

        Assert.True(Input.Capturing);
        Assert.False(Input.TryCapture(out _));
    }

    [Theory]
    [InlineData(new[] { 0x01 })]
    [InlineData(new[] { 0x02 })]
    [InlineData(new[] { 0x1B })]
    [InlineData(new[] { VkAlt, 0x09 })]
    public void AMouseClickEscapeOrAltTabCancelsWithoutBinding(int[] keys)
    {
        Input.BeginCapture();
        _keys.UnionWith(keys);
        Input.NewFrame(false);

        Assert.False(Input.Capturing);
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void AMouseButtonAlreadyDownWhenCaptureStartsDoesNotCancelIt()
    {
        _keys.Add(0x01);
        Input.BeginCapture();
        Input.NewFrame(false);

        Assert.True(Input.Capturing);
    }

    [Fact]
    public void ASuppressedFrameCancelsACaptureAndBindsNothing()
    {
        Input.BeginCapture();
        _keys.Add(VkF5);
        Input.NewFrame(true);

        Assert.False(Input.Capturing);
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void ACaptureNobodyPollsCancelsItself()
    {
        Input.BeginCapture();
        Input.NewFrame(false);
        Assert.True(Input.Capturing);

        Input.NewFrame(false);
        Assert.False(Input.Capturing);
    }

    [Fact]
    public void CancellingBindsNothingAndHotkeysWorkAgain()
    {
        Input.BeginCapture();
        Input.NewFrame(false);
        Input.CancelCapture();
        _keys.Add(VkF5);
        Input.NewFrame(false);

        Assert.True(Input.Pressed(new Hotkey(Key.F5)));
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void NoHotkeyFiresWhileCapturingNorRightAfterIt()
    {
        Input.BeginCapture();
        _keys.UnionWith([VkControl, VkF12, VkF5]);
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F5, KeyModifiers.Ctrl)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl), ignoreSuppression: true));
        Assert.True(Input.TryCapture(out _));

        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F5, KeyModifiers.Ctrl)));

        _keys.Clear();
        Input.NewFrame(false);
        _keys.Add(VkF5);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F5)));
    }
}
