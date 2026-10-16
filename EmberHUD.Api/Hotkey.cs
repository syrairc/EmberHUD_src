using System.Globalization;

namespace EmberHUD;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

public enum Key
{
    None = 0,
    Backspace = 0x08, Tab = 0x09, Enter = 0x0D,
    Shift = 0x10, Ctrl = 0x11, Alt = 0x12,
    Escape = 0x1B, Space = 0x20,
    PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24,
    Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28,
    Insert = 0x2D, Delete = 0x2E,
    D0 = 0x30, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A = 0x41, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Numpad0 = 0x60, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    F1 = 0x70, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Plus = 0xBB, Minus = 0xBD, Tilde = 0xC0,
}

public readonly record struct Hotkey(Key Key, KeyModifiers Mods = KeyModifiers.None)
{
    public override string ToString() =>
        (Mods.HasFlag(KeyModifiers.Ctrl) ? "Ctrl+" : "")
        + (Mods.HasFlag(KeyModifiers.Shift) ? "Shift+" : "")
        + (Mods.HasFlag(KeyModifiers.Alt) ? "Alt+" : "")
        + (Enum.IsDefined(Key) ? Key.ToString() : $"0x{(int)Key:X2}");

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (text is null) return false;
        if (text.Trim().Equals("None", StringComparison.OrdinalIgnoreCase)) return true;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var mods = KeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            var mod = part.ToLowerInvariant() switch { "ctrl" => KeyModifiers.Ctrl, "shift" => KeyModifiers.Shift, "alt" => KeyModifiers.Alt, _ => KeyModifiers.None };
            if (mod == KeyModifiers.None) return false;
            mods |= mod;
        }
        var name = parts[^1];
        var vk = name.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(name.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) ? hex : 0
            : name.Length > 0 && !char.IsDigit(name[0]) && name.All(char.IsLetterOrDigit) && Enum.TryParse(name, true, out Key known) ? (int)known : 0;
        if (!IsBindable(vk) && !(mods == KeyModifiers.None && vk is (int)Key.Shift or (int)Key.Ctrl or (int)Key.Alt)) return false;
        hotkey = new Hotkey((Key)vk, mods);
        return true;
    }

    internal static bool IsBindable(int virtualKey) =>
        virtualKey is >= 0x08 and <= 0xFE && virtualKey != (int)Key.Escape && virtualKey is not ((int)Key.Shift or (int)Key.Ctrl or (int)Key.Alt or >= 0xA0 and <= 0xA5 or 0x5B or 0x5C);
}

public sealed class HotkeyBinding
{
    public Hotkey Value;

    internal HotkeyBinding(string name, string description, Action callback, Hotkey defaultKey, string? category)
    {
        Name = name;
        Description = description;
        Callback = callback;
        Default = defaultKey;
        Category = category;
        Value = defaultKey;
        Id = category is null ? name : $"{category}/{name}";
    }

    public string Name { get; }

    public string Description { get; }

    public string? Category { get; }

    public Hotkey Default { get; }

    public string Id { get; }

    internal Action Callback { get; }
}
