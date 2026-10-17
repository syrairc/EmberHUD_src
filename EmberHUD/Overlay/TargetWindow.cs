using System.Drawing;

namespace EmberHUD.Host.Overlay;

internal interface ITargetSource
{
    nint Find();
    bool Exists(nint hwnd);
    bool TryGetBounds(nint hwnd, out Rectangle bounds);
    bool IsForeground(nint hwnd);
    bool IsMinimized(nint hwnd);
}

internal sealed class Win32TargetSource(string? className, string? windowTitle) : ITargetSource
{
    public static bool TitleMatches(string? wanted, ReadOnlySpan<char> actual) => string.IsNullOrEmpty(wanted) || actual.StartsWith(wanted, StringComparison.Ordinal);

    public unsafe nint Find()
    {
        Span<char> buffer = stackalloc char[256];
        for (var hwnd = Win32.FindWindowEx(0, 0, className, null); hwnd != 0; hwnd = Win32.FindWindowEx(0, hwnd, className, null))
        {
            int length;
            fixed (char* text = buffer)
                length = Win32.GetWindowText(hwnd, text, buffer.Length);
            if (TitleMatches(windowTitle, buffer[..Math.Max(0, length)]))
                return hwnd;
        }

        return 0;
    }

    public bool TryGetBounds(nint hwnd, out Rectangle bounds)
    {
        bounds = default;
        if (hwnd == 0 || !Win32.GetClientRect(hwnd, out var r)) return false;
        var origin = new Win32.Point { X = r.Left, Y = r.Top };
        if (!Win32.ClientToScreen(hwnd, ref origin)) return false;
        bounds = new Rectangle(origin.X, origin.Y, r.Right - r.Left, r.Bottom - r.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    public bool Exists(nint hwnd) => Win32.IsWindow(hwnd);

    public bool IsForeground(nint hwnd) => Win32.GetForegroundWindow() == hwnd;

    public bool IsMinimized(nint hwnd) => Win32.IsIconic(hwnd);
}

internal sealed class TargetWindow(ITargetSource source, Func<long>? clock = null)
{
    readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    long _searchedAt;

    public nint Handle { get; private set; }
    public Rectangle Bounds { get; private set; }
    public bool IsForeground { get; private set; }
    public bool IsMinimized { get; private set; }

    public bool IsVisibleTarget => Handle != 0 && IsForeground && !IsMinimized;

    public bool GameIsForeground() => Handle != 0 && source.IsForeground(Handle) && !source.IsMinimized(Handle);

    public bool Poll()
    {
        var found = Locate();
        if (found == 0)
        {
            if (Handle == 0) return false;
            Handle = 0;
            IsForeground = false;
            IsMinimized = false;
            return true;
        }

        var changed = false;
        if (found != Handle)
        {
            if (!source.TryGetBounds(found, out var bounds))
                return false;

            Handle = found;
            Bounds = bounds;
            changed = true;
        }
        else if (source.TryGetBounds(found, out var bounds) && bounds != Bounds)
        {
            Bounds = bounds;
            changed = true;
        }

        IsForeground = source.IsForeground(found);
        IsMinimized = source.IsMinimized(found);
        return changed;
    }

    nint Locate()
    {
        var now = _clock();
        if (Handle != 0 && now - _searchedAt < 1000 && source.Exists(Handle)) return Handle;
        _searchedAt = now;
        return source.Find();
    }
}
