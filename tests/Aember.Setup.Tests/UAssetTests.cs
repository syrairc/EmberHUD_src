using Aember.Setup.Icons;

namespace Aember.Setup.Tests;

public class UAssetTests
{
    [Fact]
    public void NamesReadsTheNameMap() =>
        Assert.Equal(["None", "Texture2D", "Default__Texture2D", "/Game/Art/UI/MiniMap/UI_MiniMap_Boss"], UAsset.Names(AssetBuilder.Package("None", "Texture2D", "Default__Texture2D", "/Game/Art/UI/MiniMap/UI_MiniMap_Boss")));

    [Fact]
    public void TextureDecodesBgra()
    {
        var (rgba, width, height, format) = UAsset.Texture(AssetBuilder.Texture("PF_B8G8R8A8", 2, 1, [1, 2, 3, 4, 5, 6, 7, 8]), null);
        Assert.Equal((2, 1, "PF_B8G8R8A8"), (width, height, format));
        Assert.Equal([3, 2, 1, 4, 7, 6, 5, 8], rgba);
    }

    [Fact]
    public void TextureDecodesBc7AndCropsToTheMipSize()
    {
        var block = AssetBuilder.Bc7Solid(200, 100, 50, 254);
        var (rgba, width, height, format) = UAsset.Texture(AssetBuilder.Texture("PF_BC7", 6, 5, [.. block, .. block, .. block, .. block]), null);
        Assert.Equal((6, 5, "PF_BC7"), (width, height, format));
        Assert.Equal(6 * 5 * 4, rgba.Length);
        Assert.All(rgba.Chunk(4), pixel => Assert.Equal([200, 100, 50, 254], pixel));
    }

    [Fact]
    public void TextureReadsAUbulkPayload()
    {
        byte[] ubulk = [9, 9, 9, 1, 2, 3, 4];
        var (rgba, _, _, _) = UAsset.Texture(AssetBuilder.UbulkTexture("PF_B8G8R8A8", 1, 1, 3, 4), ubulk);
        Assert.Equal([3, 2, 1, 4], rgba);
    }

    [Fact]
    public void TextureSkipsMipsWithoutData()
    {
        var (rgba, width, _, _) = UAsset.Texture(AssetBuilder.Texture("PF_B8G8R8A8", 1, 1, [1, 2, 3, 4], emptyMipFirst: []), null);
        Assert.Equal(1, width);
        Assert.Equal([3, 2, 1, 4], rgba);
    }

    [Fact]
    public void TextureWithoutAPixelFormatIsAnError() => Assert.Throws<InvalidDataException>(() => UAsset.Texture(new byte[64], null));

    [Fact]
    public void AnUnsupportedFormatIsAnError() =>
        Assert.Contains("PF_G8", Assert.Throws<InvalidDataException>(() => UAsset.Texture(AssetBuilder.Texture("PF_G8", 1, 1, [1]), null)).Message);

    [Fact]
    public void Vector2DFindsTheTaggedStruct()
    {
        string[] names = ["None", "StructProperty", "BakedSourceUV", "BakedSourceDimension"];
        var uexp = AssetBuilder.Sprite(names, (745, 249), (246, 123.5f));
        Assert.Equal((745f, 249f), UAsset.Vector2D(uexp, names, "BakedSourceUV"));
        Assert.Equal((246f, 123.5f), UAsset.Vector2D(uexp, names, "BakedSourceDimension"));
    }

    [Fact]
    public void Vector2DWithoutTheTagIsAnError() =>
        Assert.Throws<InvalidDataException>(() => UAsset.Vector2D(new byte[100], ["None", "StructProperty", "BakedSourceUV"], "BakedSourceUV"));

    [Fact]
    public void MapIconConfigReadsEveryRow()
    {
        MapIconRow[] rows = [new(1, "wanjia", "玩家", "UI_MiniMap_Player1", 0), new(2542, "S12Door3", "S12叠界门箭头", "UI_MiniMap_S12_Door3", 99)];
        Assert.Equal(rows, UAsset.MapIconConfig(AssetBuilder.MapIconConfig(rows)));
    }

    [Fact]
    public void CropZeroFillsOutsideTheSource()
    {
        byte[] rgba = [.. Enumerable.Range(1, 2 * 2 * 4).Select(i => (byte)i)];
        var cut = UAsset.Crop(rgba, 2, 2, 1, 1, 2, 2);
        Assert.Equal([13, 14, 15, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], cut);
    }

    [Fact]
    public void PngKeepsStraightAlpha()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "a.png");
        byte[] rgba = [10, 20, 30, 128, 255, 0, 0, 255, 0, 0, 0, 0, 1, 2, 3, 1];
        UAsset.WritePng(path, rgba, 2, 2);
        var (pixels, width, height) = AssetBuilder.Pixels(path);
        Assert.Equal((2, 2), (width, height));
        Assert.Equal(rgba, pixels);
    }
}
