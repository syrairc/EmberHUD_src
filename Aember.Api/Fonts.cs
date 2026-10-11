using Hexa.NET.ImGui;

namespace Aember;

public enum FontFace { Regular, Bold, Label, Mono }

public static class Fonts
{
    public static ImFontPtr Regular { get; internal set; }

    public static float RegularSize { get; internal set; }

    public static ImFontPtr Bold { get; internal set; }

    public static float BoldSize { get; internal set; }

    public static ImFontPtr Label { get; internal set; }

    public static float LabelSize { get; internal set; }

    public static ImFontPtr Mono { get; internal set; }

    public static float MonoSize { get; internal set; }

    public static FontScope UseRegular(float size = 0) => new(Regular, size > 0 ? size : RegularSize);

    public static FontScope UseBold(float size = 0) => new(Bold, size > 0 ? size : BoldSize);

    public static FontScope UseLabel(float size = 0) => new(Label, size > 0 ? size : LabelSize);

    public static FontScope UseMono(float size = 0) => new(Mono, size > 0 ? size : MonoSize);

    internal static (ImFontPtr Font, float Size) Of(FontFace face) => face switch
    {
        FontFace.Bold => (Bold, BoldSize),
        FontFace.Label => (Label, LabelSize),
        FontFace.Mono => (Mono, MonoSize),
        _ => (Regular, RegularSize),
    };
}

public readonly struct FontScope : IDisposable
{
    internal FontScope(ImFontPtr font, float size) => ImGui.PushFont(font, size);

    public void Dispose() => ImGui.PopFont();
}
