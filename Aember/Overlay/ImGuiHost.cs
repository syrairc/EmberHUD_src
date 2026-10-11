using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.D3D11;
using Hexa.NET.ImGui.Backends.Win32;

namespace Aember.Host.Overlay;

internal sealed class ImGuiHost : IDisposable
{
    ImGuiContextPtr _context;
    nint _hwnd;
    Action<Func<nint, uint, nint, nint, nint?>?> _setHook = null!;
    bool _disposed;

    public bool WantCaptureMouse { get; private set; }

    public bool WantTextInput { get; private set; }

    public static unsafe ImGuiHost Create(nint hwnd, Action<Func<nint, uint, nint, nint, nint?>?> setHook, GraphicsDevice gd)
    {
        var host = new ImGuiHost { _hwnd = hwnd, _setHook = setHook };
        host._context = ImGui.CreateContext();
        ImGui.SetCurrentContext(host._context);

        var win32Ready = false;
        try
        {
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.DockingEnable;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

            ImGuiImplWin32.SetCurrentContext(host._context);
            ImGuiImplD3D11.SetCurrentContext(host._context);

            if (!ImGuiImplWin32.Init((void*)hwnd))
                throw new InvalidOperationException("ImGuiImplWin32.Init failed");
            win32Ready = true;

            if (!ImGuiImplD3D11.Init(
                    (ID3D11Device*)gd.Device.NativePointer,
                    (ID3D11DeviceContext*)gd.Context.NativePointer))
                throw new InvalidOperationException("ImGuiImplD3D11.Init failed");

            setHook((h, msg, wparam, lparam) => ImGuiImplWin32.WndProcHandler(h, msg, (nuint)wparam, lparam) != 0 ? 1 : null);

            return host;
        }
        catch
        {
            if (win32Ready) ImGuiImplWin32.Shutdown();
            ImGui.DestroyContext(host._context);
            throw;
        }
    }

    public void NewFrame(int width, int height)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(width, height);
        FeedCursor(io);
        ImGuiImplD3D11.NewFrame();
        ImGuiImplWin32.NewFrame();
        ImGui.NewFrame();
    }

    void FeedCursor(ImGuiIOPtr io)
    {
        if (!Win32.GetCursorPos(out var p)) return;
        if (!Win32.ScreenToClient(_hwnd, ref p)) return;
        io.AddMousePosEvent(p.X, p.Y);
    }

    public void Render()
    {
        ImGui.Render();
        var io = ImGui.GetIO();
        WantCaptureMouse = io.WantCaptureMouse;
        WantTextInput = io.WantTextInput;

        if (!WantCaptureMouse && ImGui.IsAnyMouseDown())
            for (var i = 0; i < 5; i++) io.AddMouseButtonEvent(i, false);
    }

    public void RenderDrawData() => ImGuiImplD3D11.RenderDrawData(ImGui.GetDrawData());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _setHook(null);
        ImGuiImplD3D11.Shutdown();
        ImGuiImplWin32.Shutdown();
        ImGui.DestroyContext(_context);
    }
}
