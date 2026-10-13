using System.Runtime.InteropServices;

namespace EmberHUD.Host.Overlay;

internal enum TrayCommand
{
    Settings = 1,
    Quit = 2,
}

internal sealed class TrayIcon : IDisposable
{
    const uint CallbackMessage = Win32.WM_APP + 1;

    readonly Win32.WndProcDelegate _wndProc;
    readonly string _tooltip;
    readonly Func<nint> _returnFocusTo;
    readonly Action<TrayCommand> _onCommand;
    readonly uint _taskbarCreated;
    nint _hwnd;
    nint _icon;
    bool _disposed;

    TrayIcon(string tooltip, Func<nint> returnFocusTo, Action<TrayCommand> onCommand)
    {
        _tooltip = tooltip;
        _returnFocusTo = returnFocusTo;
        _onCommand = onCommand;
        _wndProc = WndProc;
        _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
    }

    public static TrayIcon Create(string tooltip, Func<nint> returnFocusTo, Action<TrayCommand> onCommand)
    {
        var self = new TrayIcon(tooltip, returnFocusTo, onCommand);
        try
        {
            self.CreateWindow();
            self._icon = LoadAppIcon();
            self.Add();
            return self;
        }
        catch
        {
            self.Dispose();
            throw;
        }
    }

    void CreateWindow()
    {
        var className = $"EmberHUDTray_{Guid.NewGuid():N}";
        var classNamePtr = Marshal.StringToHGlobalUni(className);
        try
        {
            var cls = new Win32.WndClassEx
            {
                CbSize = (uint)Marshal.SizeOf<Win32.WndClassEx>(),
                WndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                Instance = Win32.GetModuleHandle(null),
                ClassName = classNamePtr,
            };
            if (Win32.RegisterClassExW(ref cls) == 0)
                throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

            _hwnd = Win32.CreateWindowExW((uint)Win32.WS_EX_TOOLWINDOW, className, "EmberHUD tray", 0, 0, 0, 0, 0, 0, 0, Win32.GetModuleHandle(null), 0);
            if (_hwnd == 0)
                throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

            Win32.ChangeWindowMessageFilterEx(_hwnd, _taskbarCreated, Win32.MSGFLT_ALLOW, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }
    }

    static unsafe nint LoadAppIcon()
    {
        if (Environment.ProcessPath is not { } exe) return 0;
        nint small = 0;
        return Win32.ExtractIconEx(exe, 0, null, &small, 1) > 0 ? small : 0;
    }

    unsafe void Add()
    {
        var data = NewData();
        data.Flags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP;
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        var length = Math.Min(_tooltip.Length, 127);
        for (var i = 0; i < length; i++) data.Tip[i] = _tooltip[i];
        Win32.ShellNotifyIcon(Win32.NIM_ADD, &data);
    }

    unsafe Win32.NotifyIconData NewData() => new()
    {
        CbSize = (uint)sizeof(Win32.NotifyIconData),
        Hwnd = _hwnd,
        Id = 1,
    };

    nint WndProc(nint hwnd, uint msg, nint wparam, nint lparam)
    {
        if (msg == CallbackMessage)
        {
            var mouse = (uint)(lparam & 0xFFFF);
            if (mouse == Win32.WM_RBUTTONUP || mouse == Win32.WM_LBUTTONUP) ShowMenu();
            return 0;
        }
        if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            Add();
            return 0;
        }
        return Win32.DefWindowProc(hwnd, msg, wparam, lparam);
    }

    void ShowMenu()
    {
        var menu = Win32.CreatePopupMenu();
        if (menu == 0) return;
        int command;
        try
        {
            Win32.AppendMenu(menu, Win32.MF_STRING, (nint)TrayCommand.Settings, "Settings");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, 0, null);
            Win32.AppendMenu(menu, Win32.MF_STRING, (nint)TrayCommand.Quit, "Quit");

            Win32.GetCursorPos(out var cursor);
            Win32.SetForegroundWindow(_hwnd);
            command = Win32.TrackPopupMenu(menu, Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | Win32.TPM_RIGHTBUTTON, cursor.X, cursor.Y, 0, _hwnd, 0);
            Win32.PostMessage(_hwnd, Win32.WM_NULL, 0, 0);
            var target = _returnFocusTo();
            if (target != 0 && !Win32.IsIconic(target)) Win32.SetForegroundWindow(target);
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
        if (Enum.IsDefined((TrayCommand)command)) _onCommand((TrayCommand)command);
    }

    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hwnd != 0)
        {
            var data = NewData();
            Win32.ShellNotifyIcon(Win32.NIM_DELETE, &data);
            Win32.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
        if (_icon != 0) { Win32.DestroyIcon(_icon); _icon = 0; }
        GC.KeepAlive(_wndProc);
    }
}
