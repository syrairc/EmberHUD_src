using Hexa.NET.ImGui;

namespace TorchlightSDK;

public static class Fonts
{
    public static ImFontPtr Regular { get; internal set; }

    public static float RegularSize { get; internal set; }

    public static ImFontPtr Mono { get; internal set; }

    public static float MonoSize { get; internal set; }

    public static FontScope UseRegular(float size = 0) => new(Regular, size > 0 ? size : RegularSize);

    public static FontScope UseMono(float size = 0) => new(Mono, size > 0 ? size : MonoSize);
}

public readonly struct FontScope : IDisposable
{
    internal FontScope(ImFontPtr font, float size) => ImGui.PushFont(font, size);

    public void Dispose() => ImGui.PopFont();
}
