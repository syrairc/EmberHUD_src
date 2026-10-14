namespace EmberHUD.Host.Overlay;

internal sealed class OverlayWindow : IDisposable
{
    readonly Win32.WndProcDelegate _wndProc;
    string _className = "";
    bool _quit;
    bool _disposed;
    bool _clickable;

    public nint Handle { get; private set; }

    public Func<nint, uint, nint, nint, nint?>? WndProcHook { get; set; }

    OverlayWindow(Win32.WndProcDelegate wndProc) => _wndProc = wndProc;

    public static OverlayWindow Create(string title, int x, int y, int width, int height)
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DpiPerMonitorV2);

        OverlayWindow? self = null;
        Win32.WndProcDelegate proc = (hwnd, msg, wparam, lparam) =>
        {
            var handled = self?.WndProcHook?.Invoke(hwnd, msg, wparam, lparam);
            if (handled.HasValue) return handled.Value;
            if (msg == Win32.WM_CLOSE) return 0;
            return Win32.DefWindowProc(hwnd, msg, wparam, lparam);
        };
        self = new OverlayWindow(proc);

        var exStyle = (uint)(Win32.WS_EX_NOREDIRECTIONBITMAP | Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT);
        (self.Handle, self._className) = Win32.CreateWindow("EmberHUDOverlay", proc, exStyle, Win32.WS_POPUP, title, x, y, width, height);
        Win32.SetLayeredWindowAttributes(self.Handle, 0, 255, Win32.LWA_ALPHA);
        Win32.ShowWindow(self.Handle, Win32.SW_SHOWNOACTIVATE);
        return self;
    }

    public bool PumpMessages() => Win32.PumpMessages(ref _quit);

    public void SetClickable(bool clickable)
    {
        if (clickable == _clickable || Handle == 0) return;
        _clickable = clickable;
        var ex = Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE).ToInt64();
        ex = clickable ? ex & ~Win32.WS_EX_TRANSPARENT : ex | Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLongPtr(Handle, Win32.GWL_EXSTYLE, (nint)ex);
    }

    public void SetBounds(int x, int y, int width, int height) =>
        Win32.SetWindowPos(Handle, 0, x, y, width, height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != 0) { Win32.DestroyWindowAndClass(Handle, _className); Handle = 0; }
        GC.KeepAlive(_wndProc);
    }
}
