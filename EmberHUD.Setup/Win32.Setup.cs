using System.Runtime.InteropServices;

namespace EmberHUD.Host.Overlay;

internal static partial class Win32
{
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    public static partial nint LoadCursor(nint instance, nint name);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(nint owner, string text, string caption, uint type);
}
