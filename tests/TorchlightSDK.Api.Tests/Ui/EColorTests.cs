using System.Drawing;
using TorchlightSDK.Testing;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Api.Tests;

public sealed class EColorTests
{
    [Fact]
    public void AColourSurvivesTheVectorRoundTrip()
    {
        var colour = Color.FromArgb(40, 10, 20, 30);
        Assert.Equal(colour, EColor.FromVector4(EColor.ToVector4(colour)));
        for (var i = 0; i < 256; i++)
        {
            var each = Color.FromArgb(i, 255 - i, i / 2, 7);
            Assert.Equal(each, EColor.FromVector4(EColor.ToVector4(each)));
        }
    }

    [Fact]
    public void U32PacksLikeImCol32() => Assert.Equal(0x40302010u, EColor.U32(Color.FromArgb(0x40, 0x10, 0x20, 0x30)));

    [Fact]
    public void FadeReplacesTheAlpha()
    {
        Assert.Equal(0, EColor.Fade(Color.FromArgb(40, 10, 20, 30), 0f).A);
        Assert.Equal(128, EColor.Fade(Color.White, 0.5f).A);
    }

    [Fact]
    public void CategoryColourIsOpaqueAndFixedPerKey()
    {
        var map = EColor.CategoryColor("map");
        Assert.Equal(255, map.A);
        Assert.Equal(Color.FromArgb(255, 242, 109, 195), map);
        Assert.NotEqual(map, EColor.CategoryColor("currency"));
    }

    [Fact]
    public void ScaleKeepsAlphaAndClamps()
    {
        Assert.Equal(Color.FromArgb(7, 200, 200, 200), EColor.Scale(Color.FromArgb(7, 100, 100, 100), 2f));
        Assert.Equal(255, EColor.Scale(Color.FromArgb(255, 200, 200, 200), 4f).R);
    }

    [Fact]
    public void ContrastPicksBlackOnLightAndWhiteOnDark()
    {
        Assert.Equal(0, EColor.Contrast(Color.FromArgb(255, 240, 240, 240)).R);
        Assert.Equal(255, EColor.Contrast(Color.FromArgb(255, 20, 20, 20)).R);
    }

    [Fact]
    public void DesaturateGreysAndKeepsAlpha()
    {
        var grey = EColor.Desaturate(Color.FromArgb(128, 200, 40, 40));
        Assert.Equal(Color.FromArgb(128, 87, 87, 87), grey);
    }

    [Fact]
    public void StyleColorScopePopsWhatItPushed() => TestFrame.Run(() =>
    {
        using (new EColor.StyleColorScope((ImGuiCol.Text, EColor.U32(EColor.Bad)), (ImGuiCol.Button, 0xFF000000u))) ImGui.TextUnformatted("red");
    });
}
