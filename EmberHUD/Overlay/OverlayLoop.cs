using System.Collections.Concurrent;
using Vortice.Direct3D11;
using Vortice.Mathematics;

namespace EmberHUD.Host.Overlay;

internal sealed class OverlayLoop : IDisposable
{
    readonly ITargetSource _source;
    readonly TargetWindow _target;
    readonly ConcurrentQueue<Action> _beforeFrame = new();

    OverlayWindow _window = null!;
    GraphicsDevice _gd = null!;
    ImGuiHost _imgui = null!;
    Action? _style;
    bool _stop;
    bool _disposed;

    public nint TargetHandle => _target.Handle;
    public bool IsOverlayActive { get; private set; }
    public TextureStore Textures { get; } = new();
    public bool WantTextInput => _imgui.WantTextInput;

    OverlayLoop(string? targetClassName, string? targetWindowTitle)
    {
        _source = new Win32TargetSource(targetClassName, targetWindowTitle);
        _target = new TargetWindow(_source);
    }

    public static OverlayLoop Create(string targetClassName, string targetWindowTitle)
    {
        var self = new OverlayLoop(targetClassName, targetWindowTitle);
        self.BuildGraphics(1, 1, 640, 360);
        return self;
    }

    void BuildGraphics(int x, int y, int width, int height)
    {
        OverlayWindow? window = null;
        GraphicsDevice? gd = null;
        ImGuiHost? imgui = null;
        try
        {
            window = OverlayWindow.Create("EmberHUD", x, y, width, height);
            gd = GraphicsDevice.Create(window.Handle, width, height);
            imgui = ImGuiHost.Create(window.Handle, hook => window.WndProcHook = hook, gd);

            _window = window;
            _gd = gd;
            _imgui = imgui;
            _style?.Invoke();
            Textures.Attach(gd.Device, gd.Context);
        }
        catch
        {
            imgui?.Dispose();
            gd?.Dispose();
            window?.Dispose();
            throw;
        }
    }

    public void OnStyle(Action style)
    {
        _style = style;
        style();
    }

    public void BeforeFrame(Action action) => _beforeFrame.Enqueue(action);

    public void Stop() => _stop = true;

    public void Run(Action onFrame, Action afterRender)
    {
        while (!_stop)
        {
            if (!_window.PumpMessages()) break;

            var change = _target.Poll();
            if (change != TargetChange.None && _target.Handle != 0) FollowTarget();

            IsOverlayActive = _target.IsVisibleTarget || _source.IsForeground(_window.Handle);
            while (_beforeFrame.TryDequeue(out var action)) action();

            _imgui.NewFrame(_gd.Width, _gd.Height);
            onFrame();
            _imgui.Render();
            afterRender();
            _window.SetClickable(_imgui.WantCaptureMouse);

            _gd.Context.ClearRenderTargetView(_gd.BackBufferView, new Color4(0f, 0f, 0f, 0f));
            if (IsOverlayActive)
            {
                _gd.Context.OMSetRenderTargets(_gd.BackBufferView);
                _imgui.RenderDrawData();
            }

            var result = _gd.Present();
            if (result == Vortice.DXGI.ResultCode.DeviceRemoved || result == Vortice.DXGI.ResultCode.DeviceReset) Rebuild();
        }
    }

    void FollowTarget()
    {
        var b = _target.Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        _window.SetBounds(b.X, b.Y, b.Width, b.Height);
        try
        {
            _gd.Resize(b.Width, b.Height);
        }
        catch (Exception ex)
        {
            Log.Warn($"overlay resize to {b.Width}x{b.Height} failed, rebuilding graphics - {ex.Message}");
            Rebuild();
        }
    }

    void Rebuild()
    {
        var bounds = _target.Bounds;
        DisposeGraphics();
        BuildGraphics(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
    }

    void DisposeGraphics()
    {
        Textures.Detach();
        _imgui?.Dispose();
        _gd?.Dispose();
        _window?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeGraphics();
    }
}
