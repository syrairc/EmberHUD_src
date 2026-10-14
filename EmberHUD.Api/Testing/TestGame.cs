using System.Drawing;

namespace EmberHUD.Testing;

public static class TestGame
{
    public static void Set(GameState state) => Game.State = state;
    public static void UseGameIcons(Func<string, Sprite?>? resolver) => Draw.GameIcons = resolver is null ? null : texture => resolver(texture.ToString());
    public static void UseSvgIcons(Func<string, int, Color, Color, Sprite?>? resolver) => Draw.SvgIcons = resolver;

    public static void UseLoc(Func<string, string?>? get, Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? find)
    {
        Loc.Getter = get;
        Loc.Finder = find;
    }

    public static void UseScene(Func<SceneEntity[]>? list) => Scene.Lister = list;

    public static void UseTextures(Action<string, byte[], int, int>? upload, Func<string, Sprite?>? resolve, Action<string>? release = null)
    {
        Textures.Uploader = upload is null ? null : (_, key, rgba, width, height) => upload(key, rgba, width, height);
        Textures.Resolver = resolve is null ? null : (_, key) => resolve(key);
        Textures.Releaser = release is null ? null : (_, key) => release(key);
    }
}
