using System;
using System.Drawing;

namespace EmberHUD.Testing;

public static class TestGame
{
    public static void Set(GameState state) => Game.State = state;

    public static bool CombatAskedDuring(Action run)
    {
        Game.CombatAsked = 0;
        run();
        return Game.CombatAsked != 0;
    }

    public static bool CharacterAskedDuring(Action run)
    {
        Game.CharacterAsked = 0;
        run();
        return Game.CharacterAsked != 0;
    }

    public static void UseGameIcons(Func<string, Sprite?>? resolver) => Draw.GameIcons = resolver is null ? null : texture => resolver(texture.ToString());
    public static void UseSvgIcons(Func<string, int, Color, Color, Sprite?>? resolver) => Draw.SvgIcons = resolver;

    public static void UseLoc(Func<string, string?>? get, Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? find)
    {
        Loc.Getter = get;
        Loc.Finder = find;
    }

    public static void UseScene(Func<SceneEntity[]>? list) => Scene.Lister = list;

    public static void UseLua(Func<string, int, LuaDump?>? dump) => Lua.Dumper = dump;

    public static void UseTextures(Action<string, byte[], int, int>? upload, Func<string, Sprite?>? resolve, Action<string>? release = null)
    {
        Textures.Uploader = upload is null ? null : (_, key, rgba, width, height) => upload(key, rgba, width, height);
        Textures.Resolver = resolve is null ? null : (_, key) => resolve(key);
        Textures.Releaser = release is null ? null : (_, key) => release(key);
    }
}

public static class TestInput
{
    public static void Hold(params Key[] keys)
    {
        var held = keys.Select(key => (int)key).ToHashSet();
        Input.KeyDown = held.Contains;
        Input.NewFrame(false);
    }

    public static void Dispatch(Plugin plugin) => plugin.DispatchHotkeys();
}
