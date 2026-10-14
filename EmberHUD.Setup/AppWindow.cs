using EmberHUD.Host.Overlay;

namespace EmberHUD.Setup;

internal sealed class AppWindow : IDisposable
{
    readonly Win32.WndProcDelegate _wndProc;
    string _className = "";
    bool _quit;

    AppWindow(Win32.WndProcDelegate wndProc) => _wndProc = wndProc;

    public nint Handle { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Func<nint, uint, nint, nint, nint?>? WndProcHook { get; set; }

    public static unsafe AppWindow Create(string title, int width, int height, int minWidth, int minHeight)
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DpiPerMonitorV2);
        AppWindow? self = null;
        Win32.WndProcDelegate proc = (hwnd, msg, wparam, lparam) =>
        {
            if (self?.WndProcHook?.Invoke(hwnd, msg, wparam, lparam) is { } handled) return handled;
            if (msg == Win32.WM_SIZE && self is not null)
            {
                self.Width = (int)(lparam & 0xFFFF);
                self.Height = (int)(lparam >> 16 & 0xFFFF);
            }
            if (msg == Win32.WM_GETMINMAXINFO)
            {
                ((Win32.MinMaxInfo*)lparam)->MinTrackSize = new Win32.Point { X = minWidth, Y = minHeight };
                return 0;
            }
            if (msg == Win32.WM_DESTROY)
            {
                Win32.PostQuitMessage(0);
                return 0;
            }
            return Win32.DefWindowProc(hwnd, msg, wparam, lparam);
        };
        self = new AppWindow(proc);

        nint large = 0, small = 0;
        if (Environment.ProcessPath is { } exe) Win32.ExtractIconEx(exe, 0, &large, &small, 1);
        (self.Handle, self._className) = Win32.CreateWindow("EmberSetup", proc, (uint)Win32.WS_EX_NOREDIRECTIONBITMAP, Win32.WS_OVERLAPPEDWINDOW, title, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, width, height, large, small, Win32.LoadCursor(0, Win32.IDC_ARROW));
        Win32.ShowWindow(self.Handle, Win32.SW_SHOW);
        Win32.GetClientRect(self.Handle, out var client);
        self.Width = Math.Max(1, client.Right - client.Left);
        self.Height = Math.Max(1, client.Bottom - client.Top);
        return self;
    }

    public bool PumpMessages() => Win32.PumpMessages(ref _quit);

    public void Dispose()
    {
        if (Handle != 0) Win32.DestroyWindowAndClass(Handle, _className);
        Handle = 0;
        GC.KeepAlive(_wndProc);
    }
}
