using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EmberHUD;

public enum MouseButton : byte { Left, Right, Middle }

public readonly record struct SentInput(Key Key, MouseButton? Button, bool Down, int X, int Y);

public static partial class Send
{
    const int EventsPerSecond = 30;

    static readonly Lock Gate = new();
    static readonly HashSet<Key> HeldKeys = [];
    static readonly HashSet<MouseButton> HeldButtons = [];
    static long _windowStart;
    static int _windowCount;

    internal static Func<bool>? GameForeground { get; set; }

    internal static Func<int, int, (int X, int Y)?>? ToScreen { get; set; }

    internal static Action<SentInput> Sink { get; set; } = SendReal;

    internal static Func<long> Clock { get; set; } = Stopwatch.GetTimestamp;

    public static bool Allowed => GameForeground?.Invoke() == true && Game.State.ChatOpen == false;

    public static bool Press(Key key)
    {
        if (!Down(key)) return false;
        Up(key);
        return true;
    }

    public static bool Down(Key key)
    {
        if (key == Key.None || !Admit()) return false;
        lock (Gate)
        {
            HeldKeys.Add(key);
            Sink(new SentInput(key, null, true, 0, 0));
        }
        return true;
    }

    public static bool Up(Key key)
    {
        if (key == Key.None) return false;
        lock (Gate)
        {
            HeldKeys.Remove(key);
            Sink(new SentInput(key, null, false, 0, 0));
        }
        return true;
    }

    public static bool MouseMove(int x, int y)
    {
        if (ToScreen?.Invoke(x, y) is not var (screenX, screenY) || !Admit()) return false;
        Sink(new SentInput(Key.None, null, false, screenX, screenY));
        return true;
    }

    public static bool Click(MouseButton button)
    {
        if (!MouseDown(button)) return false;
        MouseUp(button);
        return true;
    }

    public static bool MouseDown(MouseButton button)
    {
        if (!Admit()) return false;
        lock (Gate)
        {
            HeldButtons.Add(button);
            Sink(new SentInput(Key.None, button, true, 0, 0));
        }
        return true;
    }

    public static bool MouseUp(MouseButton button)
    {
        lock (Gate)
        {
            HeldButtons.Remove(button);
            Sink(new SentInput(Key.None, button, false, 0, 0));
        }
        return true;
    }

    public static void ReleaseAll()
    {
        lock (Gate)
        {
            foreach (var key in HeldKeys) Sink(new SentInput(key, null, false, 0, 0));
            foreach (var button in HeldButtons) Sink(new SentInput(Key.None, button, false, 0, 0));
            HeldKeys.Clear();
            HeldButtons.Clear();
        }
    }

    internal static void Tick()
    {
        if (!Allowed) ReleaseAll();
    }

    internal static void Reset()
    {
        Sink = SendReal;
        GameForeground = null;
        ToScreen = null;
        Clock = Stopwatch.GetTimestamp;
        lock (Gate)
        {
            HeldKeys.Clear();
            HeldButtons.Clear();
            _windowStart = 0;
            _windowCount = 0;
        }
    }

    static bool Admit()
    {
        if (!Allowed) return false;
        var now = Clock();
        lock (Gate)
        {
            if (now - _windowStart >= Stopwatch.Frequency)
            {
                _windowStart = now;
                _windowCount = 0;
            }
            if (_windowCount >= EventsPerSecond) return false;
            _windowCount++;
            return true;
        }
    }

    static void SendReal(SentInput input)
    {
        var record = new NativeInput();
        if (input.Button is { } button)
        {
            record.Type = 0;
            record.Mouse.Flags = button switch
            {
                MouseButton.Left => input.Down ? 0x0002u : 0x0004u,
                MouseButton.Right => input.Down ? 0x0008u : 0x0010u,
                _ => input.Down ? 0x0020u : 0x0040u,
            };
        }
        else if (input.Key == Key.None)
        {
            var left = GetSystemMetrics(76);
            var top = GetSystemMetrics(77);
            var width = Math.Max(GetSystemMetrics(78) - 1, 1);
            var height = Math.Max(GetSystemMetrics(79) - 1, 1);
            record.Type = 0;
            record.Mouse.X = (int)((input.X - left) * 65535L / width);
            record.Mouse.Y = (int)((input.Y - top) * 65535L / height);
            record.Mouse.Flags = 0x0001 | 0x4000 | 0x8000;
        }
        else
        {
            record.Type = 1;
            record.Keyboard.Vk = (ushort)input.Key;
            record.Keyboard.Flags = (input.Down ? 0u : 0x0002u) | (input.Key is Key.Left or Key.Up or Key.Right or Key.Down or Key.Insert or Key.Delete or Key.Home or Key.End or Key.PageUp or Key.PageDown ? 0x0001u : 0u);
        }
        SendInput(1, in record, Marshal.SizeOf<NativeInput>());
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct NativeInput
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public MouseRecord Mouse;
        [FieldOffset(8)] public KeyboardRecord Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseRecord
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KeyboardRecord
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint Extra;
    }

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, in NativeInput inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
