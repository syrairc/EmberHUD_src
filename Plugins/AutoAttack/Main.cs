using System;
using System.Numerics;
using EmberHUD;

namespace AutoAttack;

public sealed class Main : Plugin<Settings>
{
    bool _toggled;
    bool _active;
    Key _held;

    internal static bool Active(GameState state, Settings settings, bool toggled) =>
        (state.Status.StartsWith("live") || state.Status == "fake game") && state.Paused == false && !state.Ui.AnyFullScreen && state.Player is not null &&
        (toggled || settings.NearbyEnabled && state.CountNear(settings.Radius, settings.MinRarity) > 0);

    internal static ScreenRect? SlotRect(GameState state, int slot) =>
        state.SkillSlots is { } slots && slot >= 1 && slot <= slots.Length ? slots[slot - 1] : null;

    public override void Tick()
    {
        Game.WantCharacter();
        if (Input.Pressed(Settings.ToggleKey)) _toggled = !_toggled;
        _active = Active(Game.State, Settings, _toggled);
        // the host drops every held key when the game loses focus
        if (!Send.Allowed) _held = Key.None;
        if (_held != Key.None && (!_active || _held != Settings.HoldKey.Key)) Release();
        if (_active && _held == Key.None && Send.Allowed && Send.Down(Settings.HoldKey.Key)) _held = Settings.HoldKey.Key;
    }

    public override void Render()
    {
        if (_active && Settings.ShowHighlight && SlotRect(Game.State, Settings.Slot) is { } rect)
            Draw.GlowRect(rect.Min, rect.Max, Settings.HighlightColor, 3, 10);
    }

    void Release()
    {
        Send.Up(_held);
        _held = Key.None;
    }

    public override void OnDisable() => Release();

    public override void Dispose() => Release();
}
