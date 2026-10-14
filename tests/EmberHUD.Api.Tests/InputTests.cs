using EmberHUD.Testing;

namespace EmberHUD.Api.Tests;

public sealed class InputTests : IDisposable
{

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
        TestInput.Hold();
    }

    [Fact]
    public void PressedFiresOnTheFrameTheKeyGoesDownOnly()
    {
        _keys.Add((int)Key.F12);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12)));
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
        Assert.True(Input.IsDown(Key.F12));
    }

    [Fact]
    public void PressedFiresAgainAfterARelease()
    {
        _keys.Add((int)Key.F12);
        Input.NewFrame(false);
        _keys.Remove((int)Key.F12);
        Input.NewFrame(false);
        _keys.Add((int)Key.F12);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12)));
    }

    [Fact]
    public void ModifiersMatchExactly()
    {
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.F12]);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl | KeyModifiers.Shift)));
    }

    [Fact]
    public void AStrayModifierBlocksTheBareHotkey()
    {
        _keys.UnionWith([(int)Key.Shift, (int)Key.F12]);
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F12)));
    }

    [Fact]
    public void ASuppressedFrameBlocksPressedUnlessAskedToIgnoreIt()
    {
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.F12]);
        Input.NewFrame(true);
        Assert.True(Input.Typing);
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl)));
        Assert.True(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl), ignoreSuppression: true));
        Assert.True(Input.IsDown(Key.F12));
    }

    [Fact]
    public void HeldModifiersReadThroughIsDown()
    {
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.Alt]);
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
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.F5]);
        Input.NewFrame(false);

        Assert.False(Input.Capturing);
        Assert.True(Input.TryCapture(out var hotkey));
        Assert.Equal(new Hotkey(Key.F5, KeyModifiers.Ctrl), hotkey);
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void CaptureIgnoresAKeyThatWasAlreadyDownWhenItStarted()
    {
        _keys.Add((int)Key.F5);
        Input.BeginCapture();
        Input.NewFrame(false);
        Assert.True(Input.Capturing);
        Assert.False(Input.TryCapture(out _));

        _keys.Add((int)Key.F6);
        Input.NewFrame(false);
        Assert.True(Input.TryCapture(out var hotkey));
        Assert.Equal(Key.F6, hotkey.Key);
    }

    [Fact]
    public void CaptureIgnoresModifiersAlone()
    {
        Input.BeginCapture();
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.Shift, 0xA2]);
        Input.NewFrame(false);

        Assert.True(Input.Capturing);
        Assert.False(Input.TryCapture(out _));
    }

    [Theory]
    [InlineData(new[] { 0x01 })]
    [InlineData(new[] { 0x02 })]
    [InlineData(new[] { 0x1B })]
    [InlineData(new[] { (int)Key.Alt, 0x09 })]
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
        _keys.Add((int)Key.F5);
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
        _keys.Add((int)Key.F5);
        Input.NewFrame(false);

        Assert.True(Input.Pressed(new Hotkey(Key.F5)));
        Assert.False(Input.TryCapture(out _));
    }

    [Fact]
    public void NoHotkeyFiresWhileCapturingNorRightAfterIt()
    {
        Input.BeginCapture();
        _keys.UnionWith([(int)Key.Ctrl, (int)Key.F12, (int)Key.F5]);
        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F5, KeyModifiers.Ctrl)));
        Assert.False(Input.Pressed(new Hotkey(Key.F12, KeyModifiers.Ctrl), ignoreSuppression: true));
        Assert.True(Input.TryCapture(out _));

        Input.NewFrame(false);
        Assert.False(Input.Pressed(new Hotkey(Key.F5, KeyModifiers.Ctrl)));

        _keys.Clear();
        Input.NewFrame(false);
        _keys.Add((int)Key.F5);
        Input.NewFrame(false);
        Assert.True(Input.Pressed(new Hotkey(Key.F5)));
    }
}
