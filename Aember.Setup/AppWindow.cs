using System.Runtime.InteropServices;
using Aember.Host.Overlay;

namespace Aember.Setup;

internal sealed class AppWindow : IDisposable
{
    readonly Win32.WndProcDelegate _wndProc;
    bool _quit;

    AppWindow(Win32.WndProcDelegate wndProc) => _wndProc = wndProc;

    public nint Handle { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Func<nint, uint, nint, nint, nint?>? WndProcHook { get; set; }

    public static unsafe AppWindow Create(string title, int width, int height)
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
        var className = $"AemberSetup_{Guid.NewGuid():N}";
        var classNamePtr = Marshal.StringToHGlobalUni(className);
        try
        {
            var cls = new Win32.WndClassEx
            {
                CbSize = (uint)Marshal.SizeOf<Win32.WndClassEx>(),
                WndProc = Marshal.GetFunctionPointerForDelegate(proc),
                Instance = Win32.GetModuleHandle(null),
                Icon = large,
                IconSm = small,
                Cursor = Win32.LoadCursor(0, 32512),
                ClassName = classNamePtr,
            };
            if (Win32.RegisterClassExW(ref cls) == 0) throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
            self.Handle = Win32.CreateWindowExW((uint)Win32.WS_EX_NOREDIRECTIONBITMAP, className, title, Win32.WS_OVERLAPPEDWINDOW, Win32.CW_USEDEFAULT, Win32.CW_USEDEFAULT, width, height, 0, 0, Win32.GetModuleHandle(null), 0);
            if (self.Handle == 0) throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
            Win32.ShowWindow(self.Handle, 5);
            Win32.GetClientRect(self.Handle, out var client);
            self.Width = Math.Max(1, client.Right - client.Left);
            self.Height = Math.Max(1, client.Bottom - client.Top);
            return self;
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    public bool PumpMessages()
    {
        while (Win32.PeekMessage(out var msg, 0, 0, 0, Win32.PM_REMOVE))
        {
            if (msg.Message == Win32.WM_QUIT)
            {
                _quit = true;
                break;
            }
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
        return !_quit;
    }

    public void Dispose()
    {
        if (Handle != 0 && !_quit) Win32.DestroyWindow(Handle);
        Handle = 0;
        GC.KeepAlive(_wndProc);
    }
}
