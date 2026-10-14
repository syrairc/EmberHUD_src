using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace EmberHUD.Host.Overlay;

internal sealed class OverlayLoop : IDisposable
{
    readonly ITargetSource _source;
    readonly TargetWindow _target;
    readonly ConcurrentQueue<Action> _beforeFrame = new();
    readonly Dictionary<string, (byte[] Data, int Width, int Height)> _textureData = [];
    readonly Dictionary<string, (ID3D11Texture2D Texture, ID3D11ShaderResourceView View)> _textures = [];

    OverlayWindow _window = null!;
    GraphicsDevice _gd = null!;
    ImGuiHost _imgui = null!;
    Action? _style;
    bool _stop;
    bool _disposed;

    public nint TargetHandle => _target.Handle;
    public bool IsOverlayActive { get; private set; }
    public bool WantTextInput => _imgui.WantTextInput;
    string UploadError { get; set; } = "";

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
            foreach (var (key, data) in _textureData)
                if (TryCreate(data.Data, data.Width, data.Height) is { } created) _textures[key] = created;
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

    public bool UploadRgba(string key, byte[] pixels, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException($"texture size {width}x{height} is not positive");
        var expected = (long)width * height * 4;
        if (pixels.Length < expected) throw new ArgumentException($"RGBA data is {pixels.Length} bytes, {width}x{height} needs {expected}");
        if (TryCreate(pixels, width, height) is not { } created) return false;
        DisposeTexture(key);
        _textures[key] = created;
        _textureData[key] = (pixels, width, height);
        return true;
    }

    public bool TryUpload(string key, byte[] rgba, int width, int height, out string failure)
    {
        try
        {
            if (UploadRgba(key, rgba, width, height))
            {
                failure = "";
                return true;
            }
            failure = $"upload failed - {UploadError}";
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }
        return false;
    }

    public unsafe Sprite? Resolve(string key)
    {
        if (!_textures.TryGetValue(key, out var entry)) return null;
        return new Sprite(new ImTextureRef(null, new ImTextureID(entry.View.NativePointer)), Vector2.Zero, Vector2.One);
    }

    (ID3D11Texture2D, ID3D11ShaderResourceView)? TryCreate(byte[] data, int width, int height)
    {
        try
        {
            return Create(data, width, height);
        }
        catch (Exception ex)
        {
            UploadError = ex.Message;
            return null;
        }
    }

    (ID3D11Texture2D, ID3D11ShaderResourceView) Create(byte[] data, int width, int height)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var texture = _gd.Device.CreateTexture2D(
                new Texture2DDescription
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R8G8B8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource,
                },
                [new SubresourceData(handle.AddrOfPinnedObject(), (uint)(width * 4))]);
            return (texture, _gd.Device.CreateShaderResourceView(texture));
        }
        finally
        {
            handle.Free();
        }
    }

    void DisposeTexture(string key)
    {
        if (!_textures.Remove(key, out var old)) return;
        old.View.Dispose();
        old.Texture.Dispose();
    }

    public void ReleaseTexture(string key)
    {
        DisposeTexture(key);
        _textureData.Remove(key);
    }

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
        _gd.Resize(b.Width, b.Height);
    }

    void Rebuild()
    {
        var bounds = _target.Bounds;
        DisposeGraphics();
        BuildGraphics(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
    }

    void DisposeGraphics()
    {
        foreach (var key in _textures.Keys.ToList()) DisposeTexture(key);
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
