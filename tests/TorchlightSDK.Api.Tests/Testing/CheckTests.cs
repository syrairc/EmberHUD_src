using TorchlightSDK.Testing;

namespace TorchlightSDK.Api.Tests;

public sealed class CheckTests
{
    [Fact]
    public void ThatPassesOnTrueAndThrowsWithTheMessageOnFalse()
    {
        Check.That(true);
        Assert.Equal("walls missing", Assert.Throws<InvalidOperationException>(() => Check.That(false, "walls missing")).Message);
    }

    [Fact]
    public void EqualNamesBothValues()
    {
        Check.Equal("a", "a");
        Check.Equal<string?>(null, null);
        Assert.Equal("zoom - expected 1, got 2", Assert.Throws<InvalidOperationException>(() => Check.Equal(1, 2, "zoom")).Message);
    }

    [Fact]
    public void ThrowsReturnsTheExceptionAndFailsOtherwise()
    {
        Assert.Equal("x", Check.Throws<FormatException>(() => throw new FormatException("x")).Message);
        Assert.Contains("nothing was thrown", Assert.Throws<InvalidOperationException>(() => Check.Throws<FormatException>(() => { })).Message);
        Assert.Contains("got ArgumentException", Assert.Throws<InvalidOperationException>(() => Check.Throws<FormatException>(() => throw new ArgumentException())).Message);
    }

    [Fact]
    public void TestGameSetsTheState()
    {
        var state = GameState.Empty with { AreaId = 7 };
        try
        {
            TestGame.Set(state);
            Assert.Same(state, Game.State);
        }
        finally
        {
            TestGame.Set(GameState.Empty);
        }
    }

    [Fact]
    public void TestInputPressesOnceWhileAKeyIsHeld()
    {
        try
        {
            TestInput.Hold();
            Assert.False(Input.Pressed(new Hotkey(Key.F8)));
            TestInput.Hold(Key.F8);
            Assert.True(Input.Pressed(new Hotkey(Key.F8)));
            TestInput.Hold(Key.F8);
            Assert.False(Input.Pressed(new Hotkey(Key.F8)));
            Assert.True(Input.IsDown(Key.F8));
            TestInput.Hold();
            Assert.False(Input.IsDown(Key.F8));
        }
        finally
        {
            TestInput.Hold();
        }
    }
}
