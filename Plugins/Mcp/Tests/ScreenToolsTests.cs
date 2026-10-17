using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using EmberHUD.Testing;

namespace Mcp;

public sealed class ScreenToolsTests
{
    [Test]
    public void ScreenshotRefusesWhileTheSettingIsOff()
    {
        var directory = Fixture.TempPath();
        Check.That(Wire.Fails(ScreenTools.Create(() => false, directory), "screenshot").Contains("Allow screenshots"), "names the setting");
        Check.That(!Directory.Exists(directory), "nothing written");
    }

    [Test]
    public void ScreenshotRefusesAnUnknownMode()
    {
        var directory = Fixture.TempPath();
        Check.That(Wire.Fails(ScreenTools.Create(() => true, directory), "screenshot", new JsonObject { ["mode"] = "window" }).Contains("screen or game"), "lists the modes");
        Check.That(!Directory.Exists(directory), "nothing written");
    }

    [Test]
    public void CropCutsTheRegionRows()
    {
        var bgra = Enumerable.Range(0, 3 * 3 * 4).Select(i => (byte)i).ToArray();
        var cut = ScreenTools.Crop(bgra, 3, 1, 1, 2, 2);
        Check.That(cut.SequenceEqual(bgra.Skip(16).Take(8).Concat(bgra.Skip(28).Take(8))), "rows 1-2, columns 1-2");
        Check.That(ReferenceEquals(bgra, ScreenTools.Crop(bgra, 3, 0, 0, 3, 3)), "whole image is not copied");
    }

    [Test]
    public void PngHoldsTheRgbRowsWithValidChunks()
    {
        byte[] bgra = [1, 2, 3, 255, 4, 5, 6, 255, 7, 8, 9, 255, 10, 11, 12, 255];
        var png = ScreenTools.Png(bgra, 2, 2);
        Check.That(png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), "signature");
        Check.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Check.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Check.Equal(0xAE426082u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(png.Length - 4)));
        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        using var inflate = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
        var rows = new MemoryStream();
        inflate.CopyTo(rows);
        Check.That(rows.ToArray().SequenceEqual(new byte[] { 0, 3, 2, 1, 6, 5, 4, 0, 9, 8, 7, 12, 11, 10 }), "filter 0 + RGB per row");
    }
}
