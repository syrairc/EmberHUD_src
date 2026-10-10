using System.Runtime.InteropServices;

namespace TorchlightSDK;

public static partial class Input
{
    static bool[] _down = new bool[256];
    static bool[] _was = new bool[256];
    static KeyModifiers _mods;
    static bool _blocked;
    static bool _polled;
    static Hotkey? _captured;

    internal static Func<int, bool> KeyDown { get; set; } = vk => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public static bool Typing { get; private set; }

    internal static bool Capturing { get; private set; }

    public static bool IsDown(Key key) => _down[(byte)key];

    public static bool Pressed(Hotkey hotkey) => Pressed(hotkey, false);

    internal static bool Pressed(Hotkey hotkey, bool ignoreSuppression) =>
        hotkey.Key != Key.None && !_blocked && (ignoreSuppression || !Typing) && _mods == hotkey.Mods && _down[(byte)hotkey.Key] && !_was[(byte)hotkey.Key];

    internal static void NewFrame(bool suppressed)
    {
        (_was, _down) = (_down, _was);
        for (var vk = 1; vk < 256; vk++) _down[vk] = KeyDown(vk);
        _mods = (_down[0x11] ? KeyModifiers.Ctrl : 0) | (_down[0x10] ? KeyModifiers.Shift : 0) | (_down[0x12] ? KeyModifiers.Alt : 0);
        Typing = suppressed;
        if (Capturing && (suppressed || !_polled)) Capturing = false;
        _polled = false;
        _blocked = Capturing;
        if (Capturing) Capture();
    }

    internal static void BeginCapture()
    {
        Capturing = true;
        _polled = true;
        _captured = null;
        for (var vk = 1; vk < 256; vk++) _down[vk] = KeyDown(vk);
    }

    internal static bool TryCapture(out Hotkey hotkey)
    {
        _polled = true;
        hotkey = _captured ?? default;
        var taken = _captured is not null;
        _captured = null;
        return taken;
    }

    internal static void CancelCapture() => Capturing = false;

    static void Capture()
    {
        for (var vk = 1; vk <= 0xFE; vk++)
        {
            if (!_down[vk] || _was[vk]) continue;
            var cancels = vk is 0x01 or 0x02 or >= 0x04 and <= 0x06 || vk == (int)Key.Escape || vk == (int)Key.Tab && _mods.HasFlag(KeyModifiers.Alt);
            if (!cancels && !Hotkey.IsBindable(vk)) continue;
            Capturing = false;
            if (!cancels) _captured = new Hotkey((Key)vk, _mods);
            return;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vk);
}
