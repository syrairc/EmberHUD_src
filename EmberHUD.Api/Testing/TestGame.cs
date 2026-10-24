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

    public static bool InventoryAskedDuring(Action run)
    {
        Game.InventoryAsked = 0;
        run();
        return Game.InventoryAsked != 0;
    }

    public static bool TartarusAskedDuring(Action run)
    {
        Game.TartarusAsked = 0;
        run();
        return Game.TartarusAsked != 0;
    }

    public static bool TarotAskedDuring(Action run)
    {
        Game.TarotAsked = 0;
        run();
        return Game.TarotAsked != 0;
    }

    public static bool VoraxAskedDuring(Action run)
    {
        Game.VoraxAsked = 0;
        run();
        return Game.VoraxAsked != 0;
    }

    public static bool FrozenCanvasAskedDuring(Action run)
    {
        Game.FrozenCanvasAsked = 0;
        run();
        return Game.FrozenCanvasAsked != 0;
    }

    public static bool XpAskedDuring(Action run)
    {
        Game.XpAsked = 0;
        run();
        return Game.XpAsked != 0;
    }

    public static bool GroundItemsAskedDuring(Action run)
    {
        Game.GroundItemsAsked = 0;
        run();
        return Game.GroundItemsAsked != 0;
    }

    public static void UseGameIcons(Func<string, Sprite?>? resolver) => Draw.GameIcons = resolver is null ? null : texture => resolver(texture.ToString());
    public static void UseSvgIcons(Func<string, int, Color, Color, Sprite?>? resolver) => Draw.SvgIcons = resolver;

    public static void UseLoc(Func<string, string?>? get, Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? find)
    {
        Loc.Getter = get;
        Loc.Finder = find;
    }

    public static void UseItems(Func<int, ItemInfo?>? byBase, Func<int, ItemInfo?>? byUnique)
    {
        Items.BaseGetter = byBase;
        Items.UniqueGetter = byUnique;
        GameAssets.ResetNames();
    }

    public static void UseSkills(Func<int, SkillInfo?>? get)
    {
        Skills.Getter = get;
        GameAssets.ResetNames();
    }

    public static void UseBuffs(Func<int, BuffInfo?>? get)
    {
        Buffs.Getter = get;
        GameAssets.ResetNames();
    }

    public static void UseGameAssets(Func<AssetKind, bool>? probe, Func<AssetKind, string, Sprite?>? resolve, Func<long>? clock = null)
    {
        GameAssets.Probe = probe;
        GameAssets.Resolver = resolve;
        GameAssets.Clock = clock ?? (() => Environment.TickCount64);
        GameAssets.ResetNames();
    }

    public static void UseQuit(Action? quit) => App.Quitter = quit;

    public static void UseScene(Func<SceneEntity[]>? list) => Scene.Lister = list;

    public static void UseFightScan(Func<System.Numerics.Vector2, float, FightProbe[]>? list) => FightScan.Lister = list;

    public static void UseLua(Func<string, int, int, string, LuaDump?>? dump) => Lua.Dumper = dump;

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
