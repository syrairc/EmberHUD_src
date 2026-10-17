using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal static class ScreenTools
{
    const int Kept = 20;

    public static Tool[] Create(Func<bool> allowed, string directory) =>
    [
        new("screenshot", "Capture a PNG and return { path, mode, x, y, width, height } - open the path to look at it. mode screen (default) = the whole monitor the game is on, overlay included (primary monitor when there is no game window); mode game = only the game window's client area, no overlay. x, y, width, height crop a region in pixels of that capture, default the whole of it. Only works while the Allow screenshots setting is on. The newest 20 are kept.",
            Schema.Of([], ("mode", "string", "screen or game, default screen"), ("x", "integer", "region left, default 0"), ("y", "integer", "region top, default 0"), ("width", "integer", "region width, default to the right edge"), ("height", "integer", "region height, default to the bottom edge")),
            args => Take(args, allowed(), directory), ReadOnly: false, Idempotent: false),
    ];

    static JsonObject Take(Args args, bool allowed, string directory)
    {
        if (!allowed) throw new ToolError("screenshots are off - turn on Allow screenshots in the Mcp plugin settings");
        var mode = args.OptStr("mode") ?? "screen";
        var (bgra, width, height) = mode switch
        {
            "screen" => CaptureMonitor(Game.WindowHandle),
            "game" => CaptureGame(Game.WindowHandle),
            _ => throw new ToolError("mode must be screen or game"),
        };
        var x = args.Int("x", 0, 0, width - 1);
        var y = args.Int("y", 0, 0, height - 1);
        var w = args.Int("width", width - x, 1, width - x);
        var h = args.Int("height", height - y, 1, height - y);
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png"));
        File.WriteAllBytes(path, Png(Crop(bgra, width, x, y, w, h), w, h));
        foreach (var old in new DirectoryInfo(directory).GetFiles("screenshot-*.png").OrderByDescending(file => file.Name, StringComparer.Ordinal).Skip(Kept)) old.Delete();
        return new JsonObject { ["path"] = path, ["mode"] = mode, ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h };
    }

    static (byte[] Bgra, int Width, int Height) CaptureMonitor(nint game)
    {
        var monitor = game != 0 ? MonitorFromWindow(game, 2) : MonitorFromPoint(default, 1);
        var info = new MonitorInfo { Size = 40 };
        if (!GetMonitorInfo(monitor, ref info)) throw new ToolError("could not find the monitor");
        var area = info.Monitor;
        return Grab(area.Right - area.Left, area.Bottom - area.Top, (memory, width, height) =>
        {
            var screen = GetDC(0);
            try
            {
                return BitBlt(memory, 0, 0, width, height, screen, area.Left, area.Top, 0x40CC0020);
            }
            finally
            {
                ReleaseDC(0, screen);
            }
        });
    }

    static (byte[] Bgra, int Width, int Height) CaptureGame(nint game)
    {
        if (game == 0 || !GetClientRect(game, out var client) || client.Right <= 0 || client.Bottom <= 0) throw new ToolError("no game window to capture - mode game needs the game open and not minimised");
        return Grab(client.Right, client.Bottom, (memory, _, _) => PrintWindow(game, memory, 3));
    }

    static (byte[] Bgra, int Width, int Height) Grab(int width, int height, Func<nint, int, int, bool> paint)
    {
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        ReleaseDC(0, screen);
        try
        {
            var previous = SelectObject(memory, bitmap);
            var painted = paint(memory, width, height);
            SelectObject(memory, previous);
            if (!painted) throw new ToolError($"capture failed, win32 error {Marshal.GetLastWin32Error()}");
            var header = new BitmapInfoHeader { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            var bgra = new byte[width * height * 4];
            if (GetDIBits(memory, bitmap, 0, (uint)height, bgra, ref header, 0) != height) throw new ToolError("GetDIBits failed");
            return (bgra, width, height);
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memory);
        }
    }

    internal static byte[] Crop(byte[] bgra, int width, int x, int y, int w, int h)
    {
        if (w == width && w * h * 4 == bgra.Length) return bgra;
        var cut = new byte[w * h * 4];
        for (var row = 0; row < h; row++) Array.Copy(bgra, ((y + row) * width + x) * 4, cut, row * w * 4, w * 4);
        return cut;
    }

    internal static byte[] Png(byte[] bgra, int width, int height)
    {
        var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 3];
            for (var y = 0; y < height; y++)
            {
                for (int x = 0, from = y * width * 4; x < width; x++, from += 4)
                {
                    row[1 + x * 3] = bgra[from + 2];
                    row[2 + x * 3] = bgra[from + 1];
                    row[3 + x * 3] = bgra[from];
                }
                zlib.Write(row);
            }
        }
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static void Chunk(Stream png, string type, byte[] data)
    {
        var body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        var number = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        png.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(body));
        png.Write(number);
    }

    static readonly uint[] CrcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    })];

    static uint Crc(byte[] bytes)
    {
        var c = 0xFFFFFFFF;
        foreach (var b in bytes) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(nint window, nint dc);

    [DllImport("user32.dll")]
    static extern bool GetClientRect(nint window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PrintWindow(nint window, nint dc, uint flags);

    [DllImport("user32.dll")]
    static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll")]
    static extern nint MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("gdi32.dll")]
    static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    static extern nint CreateCompatibleBitmap(nint dc, int width, int height);

    [DllImport("gdi32.dll")]
    static extern nint SelectObject(nint dc, nint item);

    [DllImport("gdi32.dll", SetLastError = true)]
    static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);

    [DllImport("gdi32.dll")]
    static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(nint item);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(nint dc);
}
