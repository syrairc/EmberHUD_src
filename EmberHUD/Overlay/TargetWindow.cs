using System.Drawing;

namespace EmberHUD.Host.Overlay;

[Flags]
internal enum TargetChange
{
    None = 0,
    Found = 1,
    Resized = 2,
    Lost = 4,
}

internal interface ITargetSource
{
    nint Find();
    bool TryGetBounds(nint hwnd, out Rectangle bounds);
    bool IsForeground(nint hwnd);
    bool IsMinimized(nint hwnd);
}

internal sealed class Win32TargetSource(string? className, string? windowTitle) : ITargetSource
{
    public static bool TitleMatches(string? wanted, string actual) => string.IsNullOrEmpty(wanted) || actual.StartsWith(wanted, StringComparison.Ordinal);

    public unsafe nint Find()
    {
        var buffer = new char[256];
        for (var hwnd = Win32.FindWindowEx(0, 0, className, null); hwnd != 0; hwnd = Win32.FindWindowEx(0, hwnd, className, null))
        {
            int length;
            fixed (char* text = buffer)
                length = Win32.GetWindowText(hwnd, text, buffer.Length);
            if (TitleMatches(windowTitle, new string(buffer, 0, Math.Max(0, length))))
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

    public bool IsForeground(nint hwnd) => Win32.GetForegroundWindow() == hwnd;

    public bool IsMinimized(nint hwnd) => Win32.IsIconic(hwnd);
}

internal sealed class TargetWindow(ITargetSource source)
{
    public nint Handle { get; private set; }
    public Rectangle Bounds { get; private set; }
    public bool IsForeground { get; private set; }
    public bool IsMinimized { get; private set; }

    public bool IsVisibleTarget => Handle != 0 && IsForeground && !IsMinimized;

    public TargetChange Poll()
    {
        var found = source.Find();
        if (found == 0)
        {
            if (Handle == 0) return TargetChange.None;
            Handle = 0;
            IsForeground = false;
            IsMinimized = false;
            return TargetChange.Lost;
        }

        var change = TargetChange.None;
        if (found != Handle)
        {
            if (!source.TryGetBounds(found, out var bounds))
                return TargetChange.None;

            Handle = found;
            Bounds = bounds;
            change |= TargetChange.Found;
        }
        else if (source.TryGetBounds(found, out var bounds) && bounds != Bounds)
        {
            Bounds = bounds;
            change |= TargetChange.Resized;
        }

        IsForeground = source.IsForeground(found);
        IsMinimized = source.IsMinimized(found);
        return change;
    }
}
