using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;

namespace EmberHUD.Host.Overlay;

internal sealed class GraphicsDevice : IDisposable
{
    static readonly FeatureLevel[] Levels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0];

    public static bool TryCreate(
        DriverType driverType,
        out ID3D11Device? device,
        out ID3D11DeviceContext? context,
        out string? failure)
    {
        device = null;
        context = null;
        failure = null;

        var flags = DeviceCreationFlags.BgraSupport;
        try
        {
            var hr = D3D11.D3D11CreateDevice(
                null, driverType, flags, Levels, out var d, out _, out var c);
            if (hr.Failure)
            {
                failure = $"D3D11CreateDevice({driverType}) failed: {hr}";
                return false;
            }
            device = d;
            context = c;
            return true;
        }
        catch (Exception ex)
        {
            failure = $"D3D11CreateDevice({driverType}) threw: {ex.Message}";
            return false;
        }
    }

    IDXGISwapChain1 _swapChain = null!;
    IDCompositionDesktopDevice _dcomp = null!;
    IDCompositionTarget _target = null!;
    IDCompositionVisual2 _visual = null!;
    ID3D11Texture2D _backBuffer = null!;
    ID3D11RenderTargetView _backBufferView = null!;

    public ID3D11Device Device { get; private set; } = null!;
    public ID3D11DeviceContext Context { get; private set; } = null!;
    public ID3D11RenderTargetView BackBufferView => _backBufferView;
    public int Width { get; private set; }
    public int Height { get; private set; }

    public static GraphicsDevice Create(nint hwnd, int width, int height)
    {
        if (!TryCreate(DriverType.Hardware, out var device, out var context, out var failure))
        {
            Log.Warn($"falling back to warp: {failure}");
            if (!TryCreate(DriverType.Warp, out device, out context, out failure))
                throw new InvalidOperationException(failure);
        }

        var self = new GraphicsDevice { Device = device!, Context = context!, Width = width, Height = height };
        try
        {
            using var dxgiDevice = self.Device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var desc = new SwapChainDescription1
            {
                Width = (uint)width,
                Height = (uint)height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = Vortice.DXGI.AlphaMode.Premultiplied,
            };
            self._swapChain = factory.CreateSwapChainForComposition(self.Device, desc);

            DComp.DCompositionCreateDevice2(dxgiDevice, out IDCompositionDevice? v1).CheckError();
            using (v1)
            {
                self._dcomp = v1!.QueryInterface<IDCompositionDesktopDevice>();
            }

            self._dcomp.CreateTargetForHwnd(hwnd, true, out IDCompositionTarget target).CheckError();
            self._target = target;
            self._visual = self._dcomp.CreateVisual();
            self._visual.SetContent(self._swapChain);
            self._target.SetRoot(self._visual);
            self._dcomp.Commit();

            self.BindBackBuffer();
            return self;
        }
        catch
        {
            self.Dispose();
            throw;
        }
    }

    void BindBackBuffer()
    {
        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _backBufferView = Device.CreateRenderTargetView(_backBuffer);
    }

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height) return;
        _backBufferView.Dispose();
        _backBuffer.Dispose();
        _backBufferView = null!;
        _backBuffer = null!;
        _swapChain.ResizeBuffers(2, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        Width = width;
        Height = height;
        BindBackBuffer();
    }

    public Result Present() => _swapChain.Present(1, PresentFlags.None);

    public void Dispose()
    {
        _backBufferView?.Dispose();
        _backBuffer?.Dispose();
        _visual?.Dispose();
        _target?.Dispose();
        _dcomp?.Dispose();
        _swapChain?.Dispose();
        Context?.Dispose();
        Device?.Dispose();
    }
}
