using Hexa.NET.ImGui;

namespace EmberHUD;

public enum FontFace { Regular, Bold, Label, Mono }

public static class Fonts
{
    static readonly (ImFontPtr Font, float Size)[] Faces = new (ImFontPtr, float)[Enum.GetValues<FontFace>().Length];

    internal static ref (ImFontPtr Font, float Size) Of(FontFace face) => ref Faces[(uint)face < (uint)Faces.Length ? (int)face : 0];

    public static FontScope Use(FontFace face, float size = 0)
    {
        var (font, faceSize) = Of(face);
        return new FontScope(font, size > 0 ? size : faceSize);
    }
}

public readonly struct FontScope : IDisposable
{
    internal FontScope(ImFontPtr font, float size) => ImGui.PushFont(font, size);

    public void Dispose() => ImGui.PopFont();
}
