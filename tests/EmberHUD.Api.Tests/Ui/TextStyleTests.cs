using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using EmberHUD.Testing;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Api.Tests;

public class TextStyleTests : IDisposable
{
    record Vtx(Vector2 Pos, Vector2 Uv, uint Col);

    const string Sentence = "Boss";
    static readonly Vector2 At = new(100, 100);

    static TextStyle Sample() => new()
    {
        Scale = 1.5f,
        Color = Color.FromArgb(255, 200, 100, 50),
        BgEnabled = true,
        BgColor = Color.FromArgb(180, 10, 20, 30),
        BorderEnabled = true,
        BorderThickness = 3f,
        StrokeEnabled = true,
    };

    static TextStyle Full() => new()
    {
        Scale = 1.75f,
        Color = Color.FromArgb(250, 11, 22, 33),
        BgEnabled = true,
        BgColor = Color.FromArgb(170, 44, 55, 66),
        BorderEnabled = true,
        BorderColor = Color.FromArgb(160, 77, 88, 99),
        BorderThickness = 4f,
        BorderRounding = 6f,
        StrokeEnabled = true,
        StrokeColor = Color.FromArgb(150, 111, 122, 133),
        Face = FontFace.Bold,
        ShadowEnabled = true,
        ShadowColor = Color.FromArgb(140, 144, 155, 166),
    };

    static TextStyle Distinct(float scale = 2f) => new()
    {
        Scale = scale,
        Color = Color.FromArgb(255, 230, 40, 40),
        BgColor = Color.FromArgb(255, 40, 230, 40),
        BorderColor = Color.FromArgb(255, 40, 40, 230),
        BorderThickness = 3f,
        BorderRounding = 5f,
        StrokeColor = Color.FromArgb(255, 230, 230, 40),
        ShadowColor = Color.FromArgb(255, 40, 230, 230),
    };

    static string Json(object value) => JsonSerializer.Serialize(value, SettingsJson.Options);

    static List<Vtx> Verts(ImDrawListPtr list, int from) => UiFrame.Verts(list).Skip(from).Select(v => new Vtx(v.Pos, v.Uv, v.Col)).ToList();

    static List<Vtx> Drawn(ImDrawListPtr list, Action draw) => UiFrame.Drawn(list, draw).Select(v => new Vtx(v.Pos, v.Uv, v.Col)).ToList();

    static Vector2 Box(float size) => ImGui.CalcTextSizeA(ImGui.GetFont(), size, float.MaxValue, 0, Sentence);

    static Vector2 Factor(Align align) => new(align.HasFlag(Align.Right) ? 1f : align.HasFlag(Align.Center) ? 0.5f : 0f, align.HasFlag(Align.Bottom) ? 1f : align.HasFlag(Align.Middle) ? 0.5f : 0f);

    static void GlyphsAt(ImDrawListPtr list, Vector2 min, float size, Color colour) => ImGui.AddText(list, ImGui.GetFont(), size, min, EColor.U32(colour), Sentence);

    static void StrokeRing(ImDrawListPtr list, Vector2 min, float size, Color colour)
    {
        foreach (var offset in new Vector2[] { new(-1, -1), new(0, -1), new(1, -1), new(-1, 0), new(1, 0), new(-1, 1), new(0, 1), new(1, 1) })
            GlyphsAt(list, min + offset, size, colour);
    }

    static void AssertDraws(TextStyle style, Align align, Action<ImDrawListPtr, Vector2, Vector2, float> expected)
    {
        TestFrame.Run(() => Draw.Text(Sentence, At, style, align, Draw.Foreground));
        TestFrame.Run(() =>
        {
            var list = Draw.Foreground;
            var size = ImGui.GetFontSize() * style.Scale;
            var box = Box(size);
            var min = At - box * Factor(align);
            var actual = Drawn(list, () => Draw.Text(Sentence, At, style, align, list));
            var reference = Drawn(list, () => expected(list, min, box, size));
            Assert.NotEmpty(actual);
            Assert.Equal(reference, actual);
        });
    }

    public void Dispose() => UiFrame.CloseAllPopups();

    [Fact]
    public void EveryFieldOfAStyleAndAnOverrideRoundTripsThroughSettingsJson()
    {
        var style = Full();
        var ov = TextStyleOverride.FromStyle(style);
        var styleBack = JsonSerializer.Deserialize<TextStyle>(Json(style), SettingsJson.Options)!;
        var overrideBack = JsonSerializer.Deserialize<TextStyleOverride>(Json(ov), SettingsJson.Options)!;

        Assert.Equal(Json(style), Json(styleBack));
        Assert.Equal(style.BorderRounding, styleBack.BorderRounding);
        Assert.Equal(style.StrokeColor.ToArgb(), styleBack.StrokeColor.ToArgb());
        Assert.Equal(Json(ov), Json(overrideBack));
        Assert.True(overrideBack.OverrideScale && overrideBack.OverrideColor && overrideBack.OverrideBg && overrideBack.OverrideBorder && overrideBack.OverrideStroke && overrideBack.OverrideFace && overrideBack.OverrideShadow);
    }

    [Fact]
    public void AMissingJsonFieldKeepsItsDefaultAndTheOverrideStartsFromTheSameDefaults()
    {
        var back = JsonSerializer.Deserialize<TextStyle>("{\"Scale\":2}", SettingsJson.Options)!;
        var fresh = new TextStyle();

        Assert.Equal(2f, back.Scale);
        Assert.Equal(Json(new TextStyle { Scale = 2f }), Json(back));
        var ov = new TextStyleOverride();
        Assert.Equal(
            (fresh.Scale, fresh.Face, fresh.Color, fresh.BgColor, fresh.BorderColor, fresh.BorderThickness, fresh.BorderRounding, fresh.StrokeColor, fresh.ShadowColor),
            (ov.Scale, ov.Face, ov.Color, ov.BgColor, ov.BorderColor, ov.BorderThickness, ov.BorderRounding, ov.StrokeColor, ov.ShadowColor));
    }

    [Fact]
    public void ThePersistedOverrideHasNoDerivedIsEmptyField()
    {
        Assert.DoesNotContain("IsEmpty", Json(new TextStyleOverride()));
        Assert.DoesNotContain("IsEmpty", Json(AllValues()));
        Assert.Contains("OverrideScale", Json(new TextStyleOverride()));
    }

    [Fact]
    public void AnUnknownJsonFieldIsIgnored()
    {
        var back = JsonSerializer.Deserialize<TextStyle>("{\"Scale\":2,\"Bogus\":1}", SettingsJson.Options)!;
        var ov = JsonSerializer.Deserialize<TextStyleOverride>("{\"OverrideScale\":true,\"IsEmpty\":true,\"Bogus\":[1]}", SettingsJson.Options)!;

        Assert.Equal(2f, back.Scale);
        Assert.True(ov.OverrideScale);
        Assert.False(ov.IsEmpty);
    }

    static void WithRegularFont(Action body)
    {
        TestFrame.Run(() => { });
        var font = Fonts.Of(FontFace.Regular).Font;
        var size = Fonts.Of(FontFace.Regular).Size;
        try
        {
            TestFrame.Run(() =>
            {
                Fonts.Of(FontFace.Regular).Font = ImGui.GetFont();
                Fonts.Of(FontFace.Regular).Size = ImGui.GetFontSize() * 1.7f;
                body();
            });
            TestFrame.Run(() =>
            {
                Fonts.Of(FontFace.Regular).Font = ImGui.GetFont();
                Fonts.Of(FontFace.Regular).Size = ImGui.GetFontSize() * 1.7f;
                body();
            });
        }
        finally
        {
            Fonts.Of(FontFace.Regular).Font = font;
            Fonts.Of(FontFace.Regular).Size = size;
        }
    }

    [Fact]
    public void ScaleOneDrawsExactlyLikePlainTextWhenARegularFontIsSet()
    {
        var style = Distinct(1f);
        WithRegularFont(() =>
        {
            var list = Draw.Foreground;
            var plain = Drawn(list, () => Draw.Text(Sentence, At, style.Color, Align.TopLeft, list: list));
            var styled = Drawn(list, () => Draw.Text(Sentence, At, style, Align.TopLeft, list));

            Assert.NotEmpty(plain);
            Assert.Equal(plain, styled);
        });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-2f)]
    [InlineData(float.NaN)]
    public void ANonPositiveOrNaNScaleDrawsAtScaleOne(float scale)
    {
        var style = Distinct(scale);
        WithRegularFont(() =>
        {
            var list = Draw.Foreground;
            var plain = Drawn(list, () => Draw.Text(Sentence, At, style.Color, Align.TopLeft, list: list));
            Vector2 box = default;
            var styled = Drawn(list, () => box = Draw.Text(Sentence, At, style, Align.TopLeft, list));

            Assert.NotEmpty(plain);
            Assert.Equal(plain, styled);
            Assert.Equal(Draw.MeasureText(Sentence), box);
        });
    }

    [Fact]
    public void ScaleMultipliesTheRegularFontSize()
    {
        var style = Distinct(2f);
        WithRegularFont(() =>
        {
            var list = Draw.Foreground;
            var plain = Drawn(list, () => Draw.Text(Sentence, At, style.Color, Align.TopLeft, Fonts.Of(FontFace.Regular).Size * 2f, list: list));
            Vector2 box = default;
            var styled = Drawn(list, () => box = Draw.Text(Sentence, At, style, Align.TopLeft, list));

            Assert.Equal(plain, styled);
            Assert.Equal(Draw.MeasureText(Sentence, Fonts.Of(FontFace.Regular).Size * 2f), box);
            Assert.Equal(Draw.MeasureText(Sentence).X * 2f, box.X, 1f);
        });
    }

    [Fact]
    public void ANewOverrideChangesNothing()
    {
        var style = Sample();
        var ov = new TextStyleOverride();
        ov.ApplyTo(style);
        Assert.True(ov.IsEmpty);
        Assert.Equal(1.5f, style.Scale);
        Assert.True(style.BgEnabled);
    }

    [Fact]
    public void AnOverrideLaysOnlyItsTickedGroupsOver()
    {
        var style = Sample();
        var ov = new TextStyleOverride { OverrideScale = true, Scale = 2f, OverrideBg = true, BgEnabled = false, Color = Color.Red };
        ov.ApplyTo(style);
        Assert.False(ov.IsEmpty);
        Assert.Equal(2f, style.Scale);
        Assert.False(style.BgEnabled);
        Assert.Equal(Color.FromArgb(255, 200, 100, 50), style.Color);
    }

    static TextStyleOverride AllValues() => TextStyleOverride.FromStyle(Full());

    [Theory]
    [InlineData("scale")]
    [InlineData("color")]
    [InlineData("bg")]
    [InlineData("border")]
    [InlineData("stroke")]
    [InlineData("face")]
    [InlineData("shadow")]
    public void EachOverrideGroupCarriesExactlyItsOwnFields(string group)
    {
        var ov = AllValues();
        ov.OverrideScale = group == "scale";
        ov.OverrideColor = group == "color";
        ov.OverrideBg = group == "bg";
        ov.OverrideBorder = group == "border";
        ov.OverrideStroke = group == "stroke";
        ov.OverrideFace = group == "face";
        ov.OverrideShadow = group == "shadow";
        var target = new TextStyle();
        var full = Full();

        ov.ApplyTo(target);

        var untouched = new TextStyle();
        Assert.Equal(group == "scale" ? full.Scale : untouched.Scale, target.Scale);
        Assert.Equal(group == "color" ? full.Color : untouched.Color, target.Color);
        Assert.Equal(group == "bg" ? full.BgEnabled : untouched.BgEnabled, target.BgEnabled);
        Assert.Equal(group == "bg" ? full.BgColor : untouched.BgColor, target.BgColor);
        Assert.Equal(group == "border" ? full.BorderEnabled : untouched.BorderEnabled, target.BorderEnabled);
        Assert.Equal(group == "border" ? full.BorderColor : untouched.BorderColor, target.BorderColor);
        Assert.Equal(group == "border" ? full.BorderThickness : untouched.BorderThickness, target.BorderThickness);
        Assert.Equal(group == "border" ? full.BorderRounding : untouched.BorderRounding, target.BorderRounding);
        Assert.Equal(group == "stroke" ? full.StrokeEnabled : untouched.StrokeEnabled, target.StrokeEnabled);
        Assert.Equal(group == "stroke" ? full.StrokeColor : untouched.StrokeColor, target.StrokeColor);
        Assert.Equal(group == "face" ? full.Face : untouched.Face, target.Face);
        Assert.Equal(group == "shadow" ? full.ShadowEnabled : untouched.ShadowEnabled, target.ShadowEnabled);
        Assert.Equal(group == "shadow" ? full.ShadowColor : untouched.ShadowColor, target.ShadowColor);
    }

    [Theory]
    [InlineData("scale")]
    [InlineData("color")]
    [InlineData("bg")]
    [InlineData("border")]
    [InlineData("stroke")]
    [InlineData("face")]
    [InlineData("shadow")]
    public void AnyOneTickedGroupMakesAnOverrideNonEmpty(string group)
    {
        var ov = new TextStyleOverride
        {
            OverrideScale = group == "scale",
            OverrideColor = group == "color",
            OverrideBg = group == "bg",
            OverrideBorder = group == "border",
            OverrideStroke = group == "stroke",
            OverrideFace = group == "face",
            OverrideShadow = group == "shadow",
        };

        Assert.False(ov.IsEmpty);
    }

    [Fact]
    public void ApplyingToNothingDoesNotThrow()
    {
        AllValues().ApplyTo(null);
        new TextStyle().CopyFrom(null);
    }

    [Fact]
    public void FromStyleReproducesEveryFieldEvenWhenNoneIsDefault()
    {
        var source = Full();
        var resolved = new TextStyle();
        var ov = TextStyleOverride.FromStyle(source);
        ov.ApplyTo(resolved);

        Assert.False(ov.IsEmpty);
        Assert.True(ov.OverrideScale && ov.OverrideColor && ov.OverrideBg && ov.OverrideBorder && ov.OverrideStroke && ov.OverrideFace && ov.OverrideShadow);
        Assert.Equal(Json(source), Json(resolved));
        Assert.Equal(source.BorderRounding, resolved.BorderRounding);
        Assert.Equal(source.StrokeColor, resolved.StrokeColor);
    }

    [Fact]
    public void CloneDoesNotAliasTheOriginal()
    {
        var source = Sample();
        var copy = source.Clone();
        copy.Scale = 9f;
        Assert.Equal(1.5f, source.Scale);
    }

    [Fact]
    public void CloneKeepsEveryField()
    {
        var source = Full();
        Assert.Equal(Json(source), Json(source.Clone()));
        var ov = AllValues();
        var copy = ov.Clone();
        copy.Scale = 9f;
        Assert.Equal(Json(ov), Json(AllValues()));
        Assert.Equal(9f, copy.Scale);
    }

    [Fact]
    public void CopyFromTakesEveryFieldAndLeavesTheSourceAlone()
    {
        var source = Full();
        var target = new TextStyle();
        target.CopyFrom(source);

        Assert.Equal(Json(source), Json(target));
        target.Scale = 9f;
        Assert.Equal(1.75f, source.Scale);
    }

    [Fact]
    public void DrawTextWithAStyleReturnsTheScaledTextBox()
    {
        Vector2 box = default, expected = default;
        TestFrame.Run(() =>
        {
            box = Draw.Text("Boss", new Vector2(100, 100), Sample(), Align.Center | Align.Middle, Draw.Foreground);
            expected = Draw.MeasureText("Boss", ImGui.GetFontSize() * 1.5f);
        });
        Assert.Equal(expected, box);
    }

    [Fact]
    public void TheReturnedBoxIsTheBareTextWithoutThePaddingOfTheBackgroundOrBorder()
    {
        Vector2 box = default, expected = default;
        TestFrame.Run(() =>
        {
            box = Draw.Text(Sentence, At, Full(), Align.TopLeft, Draw.Foreground);
            expected = Box(ImGui.GetFontSize() * Full().Scale);
        });
        Assert.Equal(expected, box);
        Assert.True(box.X > 0 && box.Y > 0);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2.5f)]
    public void PlainStyleDrawsTheGlyphsAtTheScaledSizeExactlyLikeAnAddText(float scale)
    {
        var style = Distinct(scale);
        AssertDraws(style, Align.TopLeft, (list, min, _, size) => GlyphsAt(list, min, size, style.Color));
    }

    [Fact]
    public void ScalingChangesTheGlyphExtentsAndUvs()
    {
        List<Vtx> small = [], large = [];
        TestFrame.Run(() => Draw.Text(Sentence, At, Distinct(1f), Align.TopLeft, Draw.Foreground));
        TestFrame.Run(() =>
        {
            small = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, Distinct(1f), Align.TopLeft, Draw.Foreground));
            large = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, Distinct(3f), Align.TopLeft, Draw.Foreground));
        });
        var smallWidth = small.Max(v => v.Pos.X) - small.Min(v => v.Pos.X);
        var largeWidth = large.Max(v => v.Pos.X) - large.Min(v => v.Pos.X);

        Assert.True(largeWidth > smallWidth * 2.5f);
        Assert.NotEqual(small.Select(v => v.Uv).ToList(), large.Select(v => v.Uv).ToList());
    }

    [Theory]
    [InlineData(Align.TopLeft)]
    [InlineData(Align.Center)]
    [InlineData(Align.Right)]
    [InlineData(Align.Middle)]
    [InlineData(Align.Bottom)]
    [InlineData(Align.Center | Align.Middle)]
    [InlineData(Align.Right | Align.Bottom)]
    public void TheBoxIsAlignedAroundThePosition(Align align)
    {
        var style = Distinct();
        AssertDraws(style, align, (list, min, _, size) => GlyphsAt(list, min, size, style.Color));
    }

    [Theory]
    [InlineData(Align.TopLeft)]
    [InlineData(Align.Center | Align.Middle)]
    [InlineData(Align.Right | Align.Bottom)]
    public void ABackgroundIsAFilledBoxPaddedByItsSizeBehindTheText(Align align)
    {
        var style = Distinct();
        style.BgEnabled = true;
        AssertDraws(style, align, (list, min, box, size) =>
        {
            list.AddRectFilled(min - Draw.TextPad(size), min + box + Draw.TextPad(size), EColor.U32(style.BgColor), style.BorderRounding);
            GlyphsAt(list, min, size, style.Color);
        });
    }

    [Fact]
    public void ABorderIsAFrameWithItsThicknessAndRoundingPaddedByItsSize()
    {
        var style = Distinct();
        style.BorderEnabled = true;
        AssertDraws(style, Align.Center, (list, min, box, size) =>
        {
            list.AddRect(min - Draw.TextPad(size), min + box + Draw.TextPad(size), EColor.U32(style.BorderColor), style.BorderRounding, ImDrawFlags.None, style.BorderThickness);
            GlyphsAt(list, min, size, style.Color);
        });
    }

    [Fact]
    public void AStrokeIsTheEightCopyRingOneUnitOutUnderTheText()
    {
        var style = Distinct();
        style.StrokeEnabled = true;
        AssertDraws(style, Align.Middle, (list, min, _, size) =>
        {
            StrokeRing(list, min, size, style.StrokeColor);
            GlyphsAt(list, min, size, style.Color);
        });
    }

    [Fact]
    public void TheStrokeRingReachesOneUnitBeyondTheTextOnEverySide()
    {
        var style = Distinct();
        style.StrokeEnabled = true;
        TestFrame.Run(() => Draw.Text(Sentence, At, style, Align.TopLeft, Draw.Foreground));
        TestFrame.Run(() =>
        {
            var verts = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, style, Align.TopLeft, Draw.Foreground));
            var ring = verts.Where(v => v.Col == EColor.U32(style.StrokeColor)).ToList();
            var ink = verts.Where(v => v.Col == EColor.U32(style.Color)).ToList();

            Assert.Equal(ink.Count * 8, ring.Count);
            Assert.Equal(ink.Min(v => v.Pos.X) - 1f, ring.Min(v => v.Pos.X));
            Assert.Equal(ink.Max(v => v.Pos.X) + 1f, ring.Max(v => v.Pos.X));
            Assert.Equal(ink.Min(v => v.Pos.Y) - 1f, ring.Min(v => v.Pos.Y));
            Assert.Equal(ink.Max(v => v.Pos.Y) + 1f, ring.Max(v => v.Pos.Y));
        });
    }

    [Fact]
    public void BackgroundBorderShadowAndStrokeDrawInThatOrderUnderTheText()
    {
        var style = Distinct();
        style.BgEnabled = style.BorderEnabled = style.ShadowEnabled = style.StrokeEnabled = true;
        AssertDraws(style, Align.Center | Align.Middle, (list, min, box, size) =>
        {
            var pad = Draw.TextPad(size);
            list.AddRectFilled(min - pad, min + box + pad, EColor.U32(style.BgColor), style.BorderRounding);
            list.AddRect(min - pad, min + box + pad, EColor.U32(style.BorderColor), style.BorderRounding, ImDrawFlags.None, style.BorderThickness);
            GlyphsAt(list, min + Vector2.One, size, style.ShadowColor);
            StrokeRing(list, min, size, style.StrokeColor);
            GlyphsAt(list, min, size, style.Color);
        });
    }

    [Fact]
    public void AShadowIsOneCopyOnePixelDownAndRightUnderTheText()
    {
        var style = Distinct();
        style.ShadowEnabled = true;
        AssertDraws(style, Align.TopLeft, (list, min, _, size) =>
        {
            GlyphsAt(list, min + Vector2.One, size, style.ShadowColor);
            GlyphsAt(list, min, size, style.Color);
        });
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    public void ABadScaleWithABackgroundPadsAtScaleOne(float scale)
    {
        var bad = Distinct(scale);
        var good = Distinct(1f);
        bad.BgEnabled = good.BgEnabled = true;
        TestFrame.Run(() => Draw.Text(Sentence, At, good, Align.TopLeft, Draw.Foreground));
        TestFrame.Run(() =>
        {
            var expected = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, good, Align.TopLeft, Draw.Foreground));
            var actual = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, bad, Align.TopLeft, Draw.Foreground));
            Assert.Equal(expected, actual);
            Assert.All(actual, v => Assert.True(float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y)));
        });
    }

    [Fact]
    public void TheFaceSetsTheFontAndScaleMultipliesItsSize()
    {
        var style = Distinct(2f);
        style.Face = FontFace.Label;
        var font = Fonts.Of(FontFace.Label).Font;
        var size = Fonts.Of(FontFace.Label).Size;
        try
        {
            for (var frame = 0; frame < 2; frame++)
                TestFrame.Run(() =>
                {
                    Fonts.Of(FontFace.Label).Font = ImGui.GetFont();
                    Fonts.Of(FontFace.Label).Size = ImGui.GetFontSize() * 1.3f;
                    var list = Draw.Foreground;
                    var plain = Drawn(list, () => Draw.Text(Sentence, At, style.Color, Align.TopLeft, Fonts.Of(FontFace.Label).Size * 2f, FontFace.Label, list));
                    Vector2 box = default;
                    var styled = Drawn(list, () => box = Draw.Text(Sentence, At, style, Align.TopLeft, list));
                    Assert.Equal(plain, styled);
                    Assert.Equal(Draw.MeasureText(Sentence, Fonts.Of(FontFace.Label).Size * 2f, FontFace.Label), box);
                    Assert.NotEqual(Draw.MeasureText(Sentence, ImGui.GetFontSize() * 2f), box);
                });
        }
        finally
        {
            Fonts.Of(FontFace.Label).Font = font;
            Fonts.Of(FontFace.Label).Size = size;
        }
    }

    [Fact]
    public void AFaceWhoseSlotIsNotLoadedFallsBackToTheCurrentFont()
    {
        var style = Distinct(1f);
        style.Face = FontFace.Label;
        var font = Fonts.Of(FontFace.Label).Font;
        var size = Fonts.Of(FontFace.Label).Size;
        try
        {
            for (var frame = 0; frame < 2; frame++)
                TestFrame.Run(() =>
                {
                    Fonts.Of(FontFace.Label).Font = default;
                    Fonts.Of(FontFace.Label).Size = 0;
                    var list = Draw.Foreground;
                    var plain = Drawn(list, () => Draw.Text(Sentence, At, style.Color, Align.TopLeft, ImGui.GetFontSize(), list: list));
                    var styled = Drawn(list, () => Draw.Text(Sentence, At, style, Align.TopLeft, list));
                    Assert.NotEmpty(styled);
                    Assert.Equal(plain, styled);
                });
        }
        finally
        {
            Fonts.Of(FontFace.Label).Font = font;
            Fonts.Of(FontFace.Label).Size = size;
        }
    }

    [Fact]
    public void TheFaceIsWrittenByName()
    {
        Assert.Contains("\"Face\": \"Label\"", Json(TextStyle.WorldLabel()));
        Assert.Equal(FontFace.Bold, JsonSerializer.Deserialize<TextStyle>("{\"Face\":\"Bold\"}", SettingsJson.Options)!.Face);
    }

    [Fact]
    public void ADisabledGroupDrawsNothingEvenWithItsColourSet()
    {
        var style = Distinct();
        TestFrame.Run(() =>
        {
            var verts = Drawn(Draw.Foreground, () => Draw.Text(Sentence, At, style, Align.TopLeft, Draw.Foreground));
            Assert.NotEmpty(verts);
            Assert.All(verts, v => Assert.Equal(EColor.U32(style.Color), v.Col));
        });
    }

    [Fact]
    public void EverythingGoesToTheGivenListAndNothingToTheBackground()
    {
        var style = Distinct();
        style.BgEnabled = style.BorderEnabled = style.StrokeEnabled = true;
        TestFrame.Run(() =>
        {
            var background = Draw.Background.VtxBuffer.Size;
            var foreground = Draw.Foreground.VtxBuffer.Size;
            Draw.Text(Sentence, At, style, Align.TopLeft, Draw.Foreground);
            Assert.Equal(background, Draw.Background.VtxBuffer.Size);
            Assert.True(Draw.Foreground.VtxBuffer.Size > foreground);
        });
    }

    [Fact]
    public void WithoutAListEverythingGoesToTheBackground()
    {
        var style = Distinct();
        style.BgEnabled = style.BorderEnabled = style.StrokeEnabled = true;
        TestFrame.Run(() =>
        {
            var background = Draw.Background.VtxBuffer.Size;
            var foreground = Draw.Foreground.VtxBuffer.Size;
            Draw.Text(Sentence, At, style);
            Assert.Equal(foreground, Draw.Foreground.VtxBuffer.Size);
            Assert.True(Draw.Background.VtxBuffer.Size > background);
        });
    }

    static List<Vtx> WindowVerts(Action body, uint colour)
    {
        List<Vtx> verts = [];
        UiFrame.Run("swatch", () =>
        {
            verts = UiFrame.Drawn(body).Where(v => v.Col == colour).Select(v => new Vtx(v.Pos, v.Uv, v.Col)).ToList();
        });
        return verts;
    }

    [Fact]
    public void EditorsDrawOneBalancedFrameAndChangeNothingUntouched()
    {
        var style = Sample();
        var ov = new TextStyleOverride();
        var changed = false;
        UiFrame.Run("test", () =>
        {
            changed |= TextStyle.Edit("Label 100%", style);
            changed |= TextStyle.EditOverride("Override", ov, style, scratch: new TextStyle());
        });
        Assert.False(changed);
    }

    [Fact]
    public void NothingIsDrawnForANullStyleOrOverride()
    {
        var changed = true;
        var before = Vector2.Zero;
        var after = Vector2.One;
        UiFrame.Run("nulls", () =>
        {
            before = ImGui.GetCursorScreenPos();
            changed = TextStyle.Edit("a", null) | TextStyle.EditOverride("b", null, new TextStyle()) | TextStyle.EditOverride("c", new TextStyleOverride(), null);
            after = ImGui.GetCursorScreenPos();
        });
        Assert.False(changed);
        Assert.Equal(before, after);
    }

    [Fact]
    public void TheSwatchDrawsTheSampleTextInTheStyleAtFourUnitsInMiddleAligned()
    {
        var style = Distinct();
        style.StrokeEnabled = true;
        var start = Vector2.Zero;
        var height = 0f;
        var actual = WindowVerts(() =>
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            TextStyle.Edit("", style, "Hi", "sw", 90f);
        }, EColor.U32(style.Color));
        var expected = WindowVerts(() => Draw.Text("Hi", new Vector2(start.X + 4f, start.Y + height * 0.5f), style, Align.Middle, ImGui.GetWindowDrawList()), EColor.U32(style.Color));

        Assert.NotEmpty(actual);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AnEmptySampleFallsBackToSampleText()
    {
        var style = Distinct();
        var empty = WindowVerts(() => TextStyle.Edit("", style, "", "sw"), EColor.U32(style.Color));
        var named = WindowVerts(() => TextStyle.Edit("", style, "Sample Text", "sw"), EColor.U32(style.Color));

        Assert.NotEmpty(empty);
        Assert.Equal(named, empty);
    }

    [Fact]
    public void TheOverrideSwatchPreviewsTheBaseWithTheOverrideLaidOver()
    {
        var baseStyle = Distinct();
        var ov = new TextStyleOverride { OverrideColor = true, Color = Color.FromArgb(255, 5, 250, 250) };
        var scratch = new TextStyle();
        var drawn = WindowVerts(() => TextStyle.EditOverride("", ov, baseStyle, "Hi", "sw", 90f, scratch), EColor.U32(ov.Color));
        var baseDrawn = WindowVerts(() => TextStyle.EditOverride("", ov, baseStyle, "Hi", "sw", 90f, scratch), EColor.U32(baseStyle.Color));

        Assert.NotEmpty(drawn);
        Assert.Empty(baseDrawn);
        Assert.Equal(ov.Color, scratch.Color);
        Assert.Equal(Distinct().Color, baseStyle.Color);
        Assert.Equal(baseStyle.Scale, scratch.Scale);
    }

    [Fact]
    public void WithoutAScratchTheResolveStillWorksAndTheBaseIsNeverChanged()
    {
        var baseStyle = Distinct();
        var ov = new TextStyleOverride { OverrideScale = true, Scale = 3f };
        UiFrame.Run("noscratch", () => TextStyle.EditOverride("x", ov, baseStyle));
        Assert.Equal(2f, baseStyle.Scale);
    }

    static void AssertSwatchOpens(Action<string, float> editor, string popup)
    {
        const float width = 90f;
        var start = Vector2.Zero;
        var height = 0f;
        bool IsOpen() => ImGui.IsPopupOpen(popup);
        var open = false;
        void Body()
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            editor("lbl", width);
            ImGui.PushID("lbl");
            open = IsOpen();
            ImGui.PopID();
        }

        UiFrame.Run("swatch click", Body);
        UiFrame.Click("swatch click", start + new Vector2(width + 1f, height / 2), Body);
        Assert.False(open);
        UiFrame.Click("swatch click", start + new Vector2(width / 2, height + 1f), Body);
        Assert.False(open);
        UiFrame.Click("swatch click", start + new Vector2(width - 1f, height - 1f), Body);
        Assert.True(open);
        UiFrame.ClickAway("swatch click", Body);
        Assert.False(open);
    }

    [Fact]
    public void ClickingTheSwatchOpensTheEditPopupAndNothingOutsideItDoes() =>
        AssertSwatchOpens((id, width) => TextStyle.Edit("Label", new TextStyle(), "S", id, width), "##edit");

    [Fact]
    public void ClickingTheOverrideSwatchOpensItsOwnPopup() =>
        AssertSwatchOpens((id, width) => TextStyle.EditOverride("Label", new TextStyleOverride(), new TextStyle(), "S", id, width), "##editov");

    [Fact]
    public void WithoutAnIdTheLabelScopesTheEditPopup() =>
        AssertSwatchOpens((_, width) => TextStyle.Edit("lbl", new TextStyle(), "S", null, width), "##edit");

    [Fact]
    public void WithoutAnIdTheLabelScopesTheOverridePopup() =>
        AssertSwatchOpens((_, width) => TextStyle.EditOverride("lbl", new TextStyleOverride(), new TextStyle(), "S", null, width), "##editov");

    [Fact]
    public void AnEmptyLabelAddsNothingAfterTheSwatchButALabelDoes()
    {
        const float width = 90f;
        float spacing = 0, start = 0, afterEmpty = 0, afterEmptyOverride = 0, afterLabel = 0;
        UiFrame.Run("labels", () =>
        {
            spacing = ImGui.GetStyle().ItemSpacing.X;
            start = ImGui.GetCursorScreenPos().X;
            TextStyle.Edit("", new TextStyle(), "S", "e", width);
            ImGui.SameLine();
            afterEmpty = ImGui.GetCursorScreenPos().X;
            ImGui.NewLine();
            TextStyle.EditOverride("", new TextStyleOverride(), new TextStyle(), "S", "o", width);
            ImGui.SameLine();
            afterEmptyOverride = ImGui.GetCursorScreenPos().X;
            ImGui.NewLine();
            TextStyle.Edit("Name", new TextStyle(), "S", "n", width);
            ImGui.SameLine();
            afterLabel = ImGui.GetCursorScreenPos().X;
        });

        Assert.Equal(start + width + spacing, afterEmpty);
        Assert.Equal(start + width + spacing, afterEmptyOverride);
        Assert.True(afterLabel > start + width + spacing + 1f);
    }

    [Fact]
    public void TheSwatchTextIsClippedToTheSwatchRect()
    {
        const float width = 90f;
        var swatch = Vector4.Zero;
        var clips = new List<Vector4>();
        UiFrame.Run("clip", () =>
        {
            var start = ImGui.GetCursorScreenPos();
            var size = new Vector2(width, ImGui.GetFrameHeight());
            swatch = new Vector4(start.X, start.Y, start.X + size.X, start.Y + size.Y);
            TextStyle.Edit("", Distinct(3f), "Overflowing sample", "c", width);
            var list = ImGui.GetWindowDrawList();
            for (var i = 0; i < list.CmdBuffer.Size; i++)
                if (list.CmdBuffer[i].ElemCount > 0) clips.Add(list.CmdBuffer[i].ClipRect);
        });

        Assert.Contains(swatch, clips);
    }

    [Fact]
    public void TheEditPopupHoldsALivePreviewAndTheBackgroundTickEditsTheStyle()
    {
        var style = Distinct();
        var changed = false;
        var start = Vector2.Zero;
        var height = 0f;
        void Body()
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            changed |= TextStyle.Edit("Label", style, "Hi", "ed", 90f);
        }

        UiFrame.Run("popup", Body);
        UiFrame.Click("popup", start + new Vector2(10f, height / 2), Body);
        var previewInk = 0;
        UiFrame.Run("popup", Body);
        var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup);
        if (!popup.IsNull) previewInk = Verts(popup.DrawList, 0).Count(v => v.Col == EColor.U32(style.Color));
        Assert.True(previewInk > 0);

        var rows = PopupColumnRuns("popup", Body);
        var backgroundRow = rows[3].Centre;
        changed = false;
        UiFrame.Click("popup", backgroundRow, Body);

        Assert.True(style.BgEnabled);
        Assert.False(style.BorderEnabled);
        Assert.False(style.StrokeEnabled);
        Assert.True(changed);
    }

    static List<UiFrame.ItemRun> PopupColumnRuns(string window, Action body)
    {
        var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup);
        var column = popup.Pos.X + ImGui.GetStyle().WindowPadding.X + ImGui.GetFrameHeight() / 2;
        return UiFrame.ItemRuns(window, body, new Vector2(column, popup.Pos.Y), new Vector2(column, popup.Pos.Y + popup.Size.Y));
    }

    static List<Vtx> ActivePopupVerts()
    {
        var popup = UiFrame.ActiveWindow(ImGuiWindowFlags.Popup);
        if (popup.IsNull) throw new InvalidOperationException("no popup");
        return Verts(popup.DrawList, 0);
    }

    static List<Vtx> PopupAfterClick(string window, Action<string> editor)
    {
        var start = Vector2.Zero;
        var height = 0f;
        void Body()
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            editor("pp");
        }

        UiFrame.Run(window, Body);
        UiFrame.Click(window, start + new Vector2(10f, height / 2), Body);
        List<Vtx> verts = [];
        UiFrame.Run(window, () =>
        {
            Body();
            verts = ActivePopupVerts();
        });
        return verts;
    }

    static uint Faded(Color colour)
    {
        uint faded = 0;
        UiFrame.Run("faded", () =>
        {
            ImGui.BeginDisabled();
            faded = ImGui.GetColorU32(EColor.ToVector4(colour));
            ImGui.EndDisabled();
        });
        return faded;
    }

    static int GlyphUvCount(List<Vtx> verts, Color colour) => verts.Where(v => v.Col == EColor.U32(colour)).Select(v => v.Uv).Distinct().Count();

    static Vector2 WhiteUv()
    {
        var uv = Vector2.Zero;
        TestFrame.Run(() => uv = Drawn(Draw.Foreground, () => Draw.RectFilled(Vector2.Zero, new Vector2(8), Color.White, list: Draw.Foreground))[0].Uv);
        return uv;
    }

    static bool HasSwatch(List<Vtx> verts, uint colour)
    {
        var white = WhiteUv();
        return verts.Any(v => v.Col == colour && v.Uv == white);
    }

    [Fact]
    public void TheEditPopupPreviewDrawsTheSampleGlyphsInTheStyleColourAboveASeparator()
    {
        var style = Distinct();
        var separator = new Vector4(0.1f, 0.9f, 0.3f, 1f);
        using var colours = UiFrame.WithColors((ImGuiCol.Separator, separator));
        var verts = PopupAfterClick("preview", id => TextStyle.Edit("Label", style, "Hi", id, 90f));

        Assert.True(GlyphUvCount(verts, style.Color) > 1);
        Assert.Contains(verts, v => v.Col == ImGui.ColorConvertFloat4ToU32(separator));
    }

    [Fact]
    public void TheEditPopupShowsEachStyleColourOnItsOwnRow()
    {
        var style = Distinct();
        var verts = PopupAfterClick("colours", id => TextStyle.Edit("Label", style, "Hi", id, 90f));

        Assert.True(HasSwatch(verts, EColor.U32(style.Color)));
        Assert.True(HasSwatch(verts, Faded(style.BgColor)));
        Assert.True(HasSwatch(verts, Faded(style.BorderColor)));
        Assert.True(HasSwatch(verts, Faded(style.StrokeColor)));
    }

    [Fact]
    public void TheOverridePopupShowsEachOverrideColourOnItsOwnRow()
    {
        var ov = new TextStyleOverride
        {
            Color = Color.FromArgb(255, 20, 200, 90),
            BgColor = Color.FromArgb(255, 90, 20, 200),
            BorderColor = Color.FromArgb(255, 200, 90, 20),
            StrokeColor = Color.FromArgb(255, 20, 90, 200),
        };
        var verts = PopupAfterClick("ov colours", id => TextStyle.EditOverride("Label", ov, Distinct(), "Hi", id, 90f));

        Assert.True(HasSwatch(verts, Faded(ov.Color)));
        Assert.True(HasSwatch(verts, Faded(ov.BgColor)));
        Assert.True(HasSwatch(verts, Faded(ov.BorderColor)));
        Assert.True(HasSwatch(verts, Faded(ov.StrokeColor)));
    }

    [Fact]
    public void TheOverridePopupPreviewIsTheResolvedStyleNotTheBase()
    {
        var baseStyle = Distinct();
        var ov = new TextStyleOverride { OverrideColor = true, Color = Color.FromArgb(255, 20, 200, 90) };
        var verts = PopupAfterClick("ov preview", id => TextStyle.EditOverride("Label", ov, baseStyle, "Hi", id, 90f));

        Assert.True(GlyphUvCount(verts, ov.Color) > 1);
        Assert.Equal(0, GlyphUvCount(verts, baseStyle.Color));
    }

    [Fact]
    public void TheOverridePopupScaleTickEditsTheOverrideNotTheBase()
    {
        var baseStyle = Distinct();
        var ov = new TextStyleOverride();
        var changed = false;
        var start = Vector2.Zero;
        var height = 0f;
        void Body()
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            changed |= TextStyle.EditOverride("Label", ov, baseStyle, "Hi", "eo", 90f);
        }

        UiFrame.Run("override popup", Body);
        UiFrame.Click("override popup", start + new Vector2(10f, height / 2), Body);
        UiFrame.Run("override popup", Body);
        var rows = PopupColumnRuns("override popup", Body);
        var scaleRow = rows[0].Centre;
        changed = false;
        UiFrame.Click("override popup", scaleRow, Body);

        Assert.True(ov.OverrideScale);
        Assert.False(ov.OverrideColor || ov.OverrideBg || ov.OverrideBorder || ov.OverrideStroke);
        Assert.True(changed);
        Assert.Equal(2f, baseStyle.Scale);
    }

    [Fact]
    public void EditorsLeaveTheClipStackBalancedWithThePopupOpenAndClosed()
    {
        var style = Distinct();
        var ov = new TextStyleOverride();
        var start = Vector2.Zero;
        var height = 0f;
        int clips = -1, clipsBefore = -2;
        void Body()
        {
            start = ImGui.GetCursorScreenPos();
            height = ImGui.GetFrameHeight();
            var list = ImGui.GetWindowDrawList();
            clipsBefore = list.ClipRectStack.Size;
            TextStyle.Edit("A", style);
            TextStyle.EditOverride("B", ov, style);
            clips = list.ClipRectStack.Size;
        }

        UiFrame.Run("stacks", Body);
        Assert.Equal(clipsBefore, clips);
        UiFrame.Click("stacks", start + new Vector2(10f, height / 2), Body);
        UiFrame.Run("stacks", Body);
        Assert.Equal(clipsBefore, clips);
    }
}
