using EmberHUD.Host.Reading;

namespace EmberHUD.Tests;

public sealed class VoraxTextTests
{
    [Fact]
    public void FillParamsFillsInOrder() =>
        Assert.Equal("+41 Activity to 1 random", VoraxText.FillParams("+{%s:2} Activity to {%s:1} random", [1, 41]));

    [Fact]
    public void FillParamsKeepsTokensOutOfRange() => Assert.Equal("{%s:3}", VoraxText.FillParams("{%s:3}", [1]));

    [Fact]
    public void FillParamsKeepsHugeIndexToken() => Assert.Equal("{%s:99999999999}", VoraxText.FillParams("{%s:99999999999}", [1]));

    [Fact]
    public void FillParamsTurnsDoublePercentIntoOne() => Assert.Equal("50% chance", VoraxText.FillParams("{%s:1}%% chance", [50]));

    [Fact]
    public void StripDropsIconsAndTags() =>
        Assert.Equal(
            "Add 1 group(s) of Pests and +73 to their Quantity",
            VoraxText.StripRichText("<RichText>Add 1 group(s) of </><Img Id=\"0060\"/><RichText>Pests</><RichText> and +73 to their </><Img Id=\"0059\"/><RichText>Quantity</>"));

    [Fact]
    public void StripDropsHyperlinkTags() =>
        Assert.Equal("mutate", VoraxText.StripRichText("<RichText Id=\"100077\" Udl=\"1\" HyperLinkType=\"1\" EventId=\"13000004\">mutate</>"));

    [Fact]
    public void StripKeepsPlainText() => Assert.Equal("Aberrant Sprout", VoraxText.StripRichText("Aberrant Sprout"));

    [Fact]
    public void StripOfUnclosedTagDropsTheTail() => Assert.Equal("a", VoraxText.StripRichText("a<b"));

    [Fact]
    public void GrayMatterReadsLikeTheCard()
    {
        const string template = "<RichText>+{%s:2} </><Img Id=\"0058\"/><RichText>Activity to {%s:1} random group(s) of monster and </><RichText Id=\"100077\" Udl=\"1\" HyperLinkType=\"1\" EventId=\"13000004\">mutate</><RichText> them into random monsters. There is a {%s:3}%% chance to </><RichText Id=\"100077\" Udl=\"1\" HyperLinkType=\"1\" EventId=\"13000004\">mutate</><RichText> them again into </><Img Id=\"0061\"/><RichText>Aemberons</><RichText> and +{%s:4} to their </><Img Id=\"0058\"/><RichText>Activity</>";
        Assert.Equal(
            "+41 Activity to 1 random group(s) of monster and mutate them into random monsters. There is a 50% chance to mutate them again into Aemberons and +30 to their Activity",
            VoraxText.StripRichText(VoraxText.FillParams(template, [1, 41, 50, 30])));
    }
}
