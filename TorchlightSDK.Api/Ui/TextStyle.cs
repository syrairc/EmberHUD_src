using System.Drawing;
using System.Numerics;
using System.Text.Json.Serialization;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public sealed class TextStyle
{
    public float Scale = 1f;
    public Color Color = Color.FromArgb(255, 255, 255, 255);
    public bool BgEnabled;
    public Color BgColor = Color.FromArgb(180, 0, 0, 0);
    public bool BorderEnabled;
    public Color BorderColor = Color.FromArgb(255, 255, 255, 255);
    public float BorderThickness = 1f;
    public float BorderRounding = 2f;
    public bool StrokeEnabled;
    public Color StrokeColor = Color.FromArgb(255, 0, 0, 0);

    public static bool Edit(string label, TextStyle? style, string sampleText = "Sample Text", string? id = null, float swatchWidth = 140f)
    {
        if (style is null) return false;
        var changed = false;
        var h = ImGui.GetFrameHeight();

        ImGui.PushID(id ?? label);
        ImGui.InvisibleButton("##swatch", new Vector2(swatchWidth, h));
        var clicked = ImGui.IsItemClicked();
        DrawSample(ImGui.GetWindowDrawList(), style, sampleText, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        if (clicked) ImGui.OpenPopup("##edit");
        Label(label);

        if (ImGui.BeginPopup("##edit"))
        {
            Preview(style, sampleText, h);
            changed |= Controls.SliderFloat("Scale", ref style.Scale, 0.5f, 3f);
            changed |= Controls.Color("Color", ref style.Color);
            changed |= Controls.OverrideRow("bg", "Background", ref style.BgEnabled, () => Controls.Color("##bgcolor", ref style.BgColor, "bgcolor"), "Draw a filled box behind the text.");
            changed |= Controls.OverrideRow("border", "Border", ref style.BorderEnabled, () => Controls.Color("##bordercolor", ref style.BorderColor, "bordercolor"), "Draw a frame around the text.");
            changed |= Controls.SliderFloat("Border width", ref style.BorderThickness, 1f, 6f, fmt: "%.0f");
            changed |= Controls.SliderFloat("Border rounding", ref style.BorderRounding, 0f, 8f, fmt: "%.0f");
            changed |= Controls.OverrideRow("stroke", "Stroke", ref style.StrokeEnabled, () => Controls.Color("##strokecolor", ref style.StrokeColor, "strokecolor"), "Outline each glyph so it reads over any background.");
            ImGui.EndPopup();
        }

        ImGui.PopID();
        return changed;
    }

    public TextStyle Clone() => (TextStyle)MemberwiseClone();

    public void CopyFrom(TextStyle? o)
    {
        if (o is null) return;
        Scale = o.Scale;
        Color = o.Color;
        BgEnabled = o.BgEnabled;
        BgColor = o.BgColor;
        BorderEnabled = o.BorderEnabled;
        BorderColor = o.BorderColor;
        BorderThickness = o.BorderThickness;
        BorderRounding = o.BorderRounding;
        StrokeEnabled = o.StrokeEnabled;
        StrokeColor = o.StrokeColor;
    }

    public static bool EditOverride(string label, TextStyleOverride? ov, TextStyle? baseStyle, string sampleText = "Sample Text", string? id = null, float swatchWidth = 140f, TextStyle? scratch = null)
    {
        if (ov is null || baseStyle is null) return false;
        var changed = false;
        var h = ImGui.GetFrameHeight();

        var resolved = scratch ?? new TextStyle();
        resolved.CopyFrom(baseStyle);
        ov.ApplyTo(resolved);

        ImGui.PushID(id ?? label);
        ImGui.InvisibleButton("##swatch", new Vector2(swatchWidth, h));
        var clicked = ImGui.IsItemClicked();
        DrawSample(ImGui.GetWindowDrawList(), resolved, sampleText, ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        if (clicked) ImGui.OpenPopup("##editov");
        Label(label);

        if (ImGui.BeginPopup("##editov"))
        {
            Preview(resolved, sampleText, h);
            changed |= Controls.OverrideRow("sc", "Scale", ref ov.OverrideScale, () => Controls.SliderFloat("##scale", ref ov.Scale, 0.5f, 3f, "scale"));
            changed |= Controls.OverrideRow("col", "Color", ref ov.OverrideColor, () => Controls.Color("##color", ref ov.Color, "color"));
            changed |= Controls.OverrideRow("bg", "Background", ref ov.OverrideBg, () =>
            {
                var c = ImGui.Checkbox("##bgon", ref ov.BgEnabled);
                Text.Tip("Draw a filled box behind the text.");
                ImGui.SameLine();
                return c | Controls.Color("##bgcolor", ref ov.BgColor, "bgcolor");
            });
            changed |= Controls.OverrideRow("bd", "Border", ref ov.OverrideBorder, () =>
            {
                var c = ImGui.Checkbox("##bdon", ref ov.BorderEnabled);
                Text.Tip("Draw a frame around the text.");
                ImGui.SameLine();
                c |= Controls.Color("##bdcolor", ref ov.BorderColor, "bdcolor");
                c |= Controls.SliderFloat("Width", ref ov.BorderThickness, 1f, 6f, "bdw", "%.0f");
                c |= Controls.SliderFloat("Rounding", ref ov.BorderRounding, 0f, 8f, "bdr", "%.0f");
                return c;
            });
            changed |= Controls.OverrideRow("st", "Stroke", ref ov.OverrideStroke, () =>
            {
                var c = ImGui.Checkbox("##ston", ref ov.StrokeEnabled);
                Text.Tip("Outline each glyph so it reads over any background.");
                ImGui.SameLine();
                return c | Controls.Color("##stcolor", ref ov.StrokeColor, "stcolor");
            });
            ImGui.EndPopup();
        }

        ImGui.PopID();
        return changed;
    }

    static void Label(string label)
    {
        if (string.IsNullOrEmpty(label)) return;
        ImGui.SameLine();
        Controls.AlignMid(ImGui.GetTextLineHeight());
        ImGui.TextUnformatted(label);
    }

    static void Preview(TextStyle style, string sampleText, float h)
    {
        var pos = ImGui.GetCursorScreenPos();
        var boxSize = new Vector2(220f, h * 2f);
        ImGui.Dummy(boxSize);
        DrawSample(ImGui.GetWindowDrawList(), style, sampleText, pos, pos + boxSize);
        ImGui.Separator();
    }

    static void DrawSample(ImDrawListPtr dl, TextStyle style, string? sampleText, Vector2 min, Vector2 max)
    {
        var text = string.IsNullOrEmpty(sampleText) ? "Sample Text" : sampleText;
        using (Draw.Clip(min, max, dl))
            Draw.Text(text, new Vector2(min.X + 4f, (min.Y + max.Y) * 0.5f), style, Align.Middle, dl);
    }
}

public sealed class TextStyleOverride
{
    public bool OverrideScale;
    public float Scale = 1f;
    public bool OverrideColor;
    public Color Color = Color.FromArgb(255, 255, 255, 255);
    public bool OverrideBg;
    public bool BgEnabled;
    public Color BgColor = Color.FromArgb(180, 0, 0, 0);
    public bool OverrideBorder;
    public bool BorderEnabled;
    public Color BorderColor = Color.FromArgb(255, 255, 255, 255);
    public float BorderThickness = 1f;
    public float BorderRounding = 2f;
    public bool OverrideStroke;
    public bool StrokeEnabled;
    public Color StrokeColor = Color.FromArgb(255, 0, 0, 0);

    public void ApplyTo(TextStyle? s)
    {
        if (s is null) return;
        if (OverrideScale) s.Scale = Scale;
        if (OverrideColor) s.Color = Color;
        if (OverrideBg)
        {
            s.BgEnabled = BgEnabled;
            s.BgColor = BgColor;
        }
        if (OverrideBorder)
        {
            s.BorderEnabled = BorderEnabled;
            s.BorderColor = BorderColor;
            s.BorderThickness = BorderThickness;
            s.BorderRounding = BorderRounding;
        }
        if (OverrideStroke)
        {
            s.StrokeEnabled = StrokeEnabled;
            s.StrokeColor = StrokeColor;
        }
    }

    [JsonIgnore]
    public bool IsEmpty => !OverrideScale && !OverrideColor && !OverrideBg && !OverrideBorder && !OverrideStroke;

    public TextStyleOverride Clone() => (TextStyleOverride)MemberwiseClone();

    public static TextStyleOverride FromStyle(TextStyle s) => new()
    {
        OverrideScale = true,
        Scale = s.Scale,
        OverrideColor = true,
        Color = s.Color,
        OverrideBg = true,
        BgEnabled = s.BgEnabled,
        BgColor = s.BgColor,
        OverrideBorder = true,
        BorderEnabled = s.BorderEnabled,
        BorderColor = s.BorderColor,
        BorderThickness = s.BorderThickness,
        BorderRounding = s.BorderRounding,
        OverrideStroke = true,
        StrokeEnabled = s.StrokeEnabled,
        StrokeColor = s.StrokeColor,
    };
}
