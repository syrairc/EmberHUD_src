using System;
using System.IO;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace NameplatesPlugin;

public sealed class BarTexturesTests
{
    static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47];

    static byte Luminance(byte[] rgba, int width, int row) => rgba[(row * width) * 4];

    static void WithFolder(Action<string> run, params (string File, byte[] Bytes)[] files)
    {
        var folder = Path.Combine(Path.GetTempPath(), "nameplates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        foreach (var (file, bytes) in files) File.WriteAllBytes(Path.Combine(folder, file), bytes);
        var original = BarTextures.PngLoader;
        BarTextures.PngLoader = (_, path) => File.ReadAllBytes(path).AsSpan().StartsWith(PngBytes);
        try
        {
            run(folder);
        }
        finally
        {
            BarTextures.PngLoader = original;
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void BuiltInTexturesAreGeneratedWithTheRightSize()
    {
        foreach (var name in new[] { "Flat", "Gradient", "Gloss", "Bevel" })
        {
            var rgba = BarTextures.Generate(name, out var width, out var height);
            Check.Equal(8, width);
            Check.Equal(32, height);
            Check.Equal(width * height * 4, rgba.Length);
        }
    }

    [Test]
    public void GradientGetsDarkerDownTheBar()
    {
        var gradient = BarTextures.Generate("Gradient", out var width, out var height);
        Check.That(Luminance(gradient, width, 0) > Luminance(gradient, width, height - 1), "gradient top lighter than bottom");
        var flat = BarTextures.Generate("Flat", out _, out _);
        Check.That(flat.All(value => value == 255), "flat is uniform white");
    }

    [Test]
    public void GlossHasAHighlightBandAndBevelHasEdges()
    {
        var gloss = BarTextures.Generate("Gloss", out var width, out var height);
        Check.That(Luminance(gloss, width, 6) > Luminance(gloss, width, height - 1), "gloss band brighter than the body");
        var bevel = BarTextures.Generate("Bevel", out _, out _);
        Check.That(Luminance(bevel, width, 0) > Luminance(bevel, width, height / 2), "bevel top edge lighter");
        Check.That(Luminance(bevel, width, height - 1) < Luminance(bevel, width, height / 2), "bevel bottom edge darker");
    }

    [Test]
    public void UnknownNameFallsBackToFlat()
    {
        var flat = new Sprite(default, new Vector2(7, 7), Vector2.One);
        TestGame.UseTextures(null, key => key == "nameplates/Flat" ? flat : null);
        try
        {
            WithFolder(folder => BarTextures.Load(folder));
            Check.Equal("Flat", BarTextures.Fallback);
            Check.Equal(flat, BarTextures.Get("NoSuchTexture"));
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void GetIsCaseInsensitiveForKnownNamesAndFallsBackForUnknown()
    {
        var gloss = new Sprite(default, new Vector2(3, 3), Vector2.One);
        var flat = new Sprite(default, new Vector2(7, 7), Vector2.One);
        TestGame.UseTextures(null, key => key == "nameplates/Gloss" ? gloss : key == "nameplates/Flat" ? flat : null);
        try
        {
            WithFolder(folder => BarTextures.Load(folder));
            Check.Equal(gloss, BarTextures.Get("gLOSS"));
            Check.Equal(flat, BarTextures.Get("missing"));
        }
        finally
        {
            TestGame.UseTextures(null, null);
        }
    }

    [Test]
    public void DropInFolderPngsAreListedAfterTheBuiltIns() => WithFolder(folder =>
    {
        BarTextures.Load(folder);
        Check.Equal("Flat,Gradient,Gloss,Bevel,Alpha,Beta", string.Join(',', BarTextures.Names));
    }, ("Beta.png", PngBytes), ("Alpha.png", PngBytes), ("notes.txt", [1]));

    [Test]
    public void BrokenFileInTheFolderIsSkipped() => WithFolder(folder =>
    {
        BarTextures.Load(folder);
        Check.Equal("Flat,Gradient,Gloss,Bevel,Good", string.Join(',', BarTextures.Names));
    }, ("Broken.png", [1, 2, 3]), ("Good.png", PngBytes));

    [Test]
    public void DropInWithABuiltInNameIsIgnored() => WithFolder(folder =>
    {
        BarTextures.Load(folder);
        Check.Equal("Flat,Gradient,Gloss,Bevel,Other", string.Join(',', BarTextures.Names));
    }, ("flat.png", PngBytes), ("Other.png", PngBytes));

    [Test]
    public void MissingFolderIsFine()
    {
        var folder = Path.Combine(Path.GetTempPath(), "nameplates-" + Guid.NewGuid().ToString("N"), "Textures");
        try
        {
            BarTextures.Load(folder);
            Check.Equal(4, BarTextures.Names.Count);
            Check.That(Directory.Exists(folder), "folder created on first load");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, true);
        }
    }
}
