using EmberHUD.Host.Icons;

namespace EmberHUD.Tests;

public sealed class IconSheetTests : IDisposable
{
    const string Square = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="8" y="8" width="48" height="48" fill="currentColor"/></svg>""";

    readonly string _directory = Path.Combine(Path.GetTempPath(), "emberhud-sheet-" + Guid.NewGuid().ToString("N"));
    readonly LogCapture _log = new();

    public void Dispose()
    {
        _log.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void EveryIconGetsARowOfSizesOnDarkAndLightAndABrokenOneStaysEmpty()
    {
        var path = IconSheet.Write(_directory, new Dictionary<string, string> { ["svg:a"] = Square, ["svg:b"] = Square, ["svg:broken"] = "<svg><rect" });

        Assert.Equal(Path.Combine(_directory, "icons.png"), path);
        Assert.True(PngDecoder.TryDecode(File.ReadAllBytes(path), out var sheet, out var failure), failure);
        Assert.Equal(6 * 72, sheet.Width);
        Assert.Equal(3 * 72, sheet.Height);

        (int R, int G, int B) At(int x, int y)
        {
            var i = (y * sheet.Width + x) * 4;
            return (sheet.Rgba[i], sheet.Rgba[i + 1], sheet.Rgba[i + 2]);
        }

        Assert.Equal((0x1B, 0x22, 0x30), At(1, 1));
        Assert.Equal((0xD9, 0xD9, 0xD9), At(3 * 72 + 1, 1));
        Assert.Equal((0xF9, 0xDD, 0x75), At(2 * 72 + 36, 36));
        Assert.Equal((0xF9, 0xDD, 0x75), At(5 * 72 + 36, 72 + 36));
        Assert.Equal((0x1B, 0x22, 0x30), At(2 * 72 + 36, 2 * 72 + 36));
        Assert.Contains("svg:broken", Assert.Single(_log.Messages()));
    }
}
