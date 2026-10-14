using System.Text;
using EmberHUD.Host;
using EmberHUD.Host.Memory;

namespace EmberHUD.Tests;

internal static class FakeMapInfo
{
    public const int MapId = 4602;
    public const int TownId = 110;
    public const int MapSceneType = 3;
    public const string Coffins = "There are Coffins awaiting escort in this stage";
    public static readonly string[] VigilSpec = Offsets.MapContent.Split(';')[0].Split(',');
    public static readonly int Vigil = int.Parse(VigilSpec[2]);

    public static FakeWorld Demiman(bool withPanel = true, string[]? panelLines = null)
    {
        var world = new FakeWorld();
        world.SetLanguageTable(2, 7,
            (Offsets.LevelNameKey + MapId, "Demiman Village"),
            (Offsets.LevelNameKey + TownId, "Ember's Rest"),
            (Offsets.ModifierTextKey + Vigil, Coffins));
        world.SetMapInfo(MapId, MapSceneType, 82, 112);
        world.SetModifiers(5111003, Vigil, 71020000);
        if (withPanel) new FakeUi(world).MapHud(panelLines ?? ["<RichText Id=\"100004\">+60</>% Drop Quantity", "-6% Max Elemental Resistance for you", Coffins]);
        return world;
    }

    public static nint Utf8(this FakeWorld world, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var at = world.Alloc(Math.Max(1, bytes.Length));
        world.Memory.WriteArray(at, bytes);
        return at;
    }

    public static void SetLanguage(this FakeWorld world, byte language)
    {
        world.Memory.Map(FakeWorld.Module + Offsets.LanguageRva, 1);
        world.Memory.Write(FakeWorld.Module + Offsets.LanguageRva, language);
    }

    public static void SetLanguageTable(this FakeWorld world, byte current, int bucketCount, params (string Key, string Text)[] entries) =>
        world.SetLanguageTable(current, bucketCount, entries.Select(e => (e.Key, e.Text, LocText.Hash(Encoding.UTF8.GetBytes(e.Key)))).ToArray());

    public static void SetLanguageTable(this FakeWorld world, byte current, int bucketCount, (string Key, string Text, uint Hash)[] entries)
    {
        world.SetLanguage(current);
        var buckets = world.Alloc(bucketCount * 8);
        foreach (var (key, text, hash) in entries)
        {
            var value = world.Alloc(0x40);
            world.Memory.Write(value + Offsets.LocValueText, world.Utf8(text));
            world.Memory.Write(value + Offsets.LocValueLength, Encoding.UTF8.GetByteCount(text));
            var node = world.Alloc(0x40);
            world.Memory.Write(node + Offsets.LocNodeKey, world.Utf8(key));
            world.Memory.Write(node + Offsets.LocNodeKeyLength, Encoding.UTF8.GetByteCount(key));
            world.Memory.Write(node + Offsets.LocNodeValue, value);
            world.Memory.Write(node + Offsets.LocNodeHash, (ulong)hash);
            var slot = buckets + (int)(hash % (uint)bucketCount) * 8;
            world.Memory.TryReadPtr(slot, out var head);
            world.Memory.Write(node + Offsets.LocNodeNext, head);
            world.Memory.Write(slot, node);
        }

        var languages = world.Alloc(0x40);
        var languageBuckets = world.Alloc(8);
        world.Memory.Map(FakeWorld.Module + Offsets.LanguageBucketsRva, 8);
        world.Memory.Map(FakeWorld.Module + Offsets.LanguageBucketCountRva, 4);
        world.Memory.Write(FakeWorld.Module + Offsets.LanguageBucketsRva, languageBuckets);
        world.Memory.Write(FakeWorld.Module + Offsets.LanguageBucketCountRva, 1u);
        world.Memory.Write(languages, current);
        world.Memory.Write(languages + Offsets.LanguageNodeMap, buckets);
        world.Memory.Write(languages + Offsets.LanguageNodeMap + Offsets.HashBucketCount, (uint)bucketCount);
        world.Memory.Write(languageBuckets, languages);
    }

    public static nint Info(this FakeWorld world)
    {
        if (world.Memory.TryReadPtr(world.Scene + Offsets.SceneGeometry, out var info) && info != 0) return info;
        var at = world.Alloc(0x200);
        world.Memory.Write(world.Scene + Offsets.SceneGeometry, at);
        return at;
    }

    public static nint Module(this FakeWorld world)
    {
        if (world.Memory.TryReadPtr(world.Scene + Offsets.SceneGridModule, out var module) && module != 0) return module;
        var at = world.Alloc(0x200);
        world.Memory.Write(world.Scene + Offsets.SceneGridModule, at);
        return at;
    }

    public static void SetMapInfo(this FakeWorld world, int levelId, int sceneType, int monsterLevel, int toSpawn)
    {
        world.Memory.Write(world.Info() + Offsets.GeometryLevelId, levelId);
        world.Memory.Write(world.Info() + Offsets.GeometrySceneType, sceneType);
        world.Memory.Write(world.Info() + Offsets.GeometryMonsterLevel, monsterLevel);
        world.Memory.Write(world.Module() + Offsets.GridModuleToSpawn, toSpawn);
    }

    public static void SetModifiers(this FakeWorld world, params int[] ids)
    {
        var array = world.Alloc(Math.Max(1, ids.Length) * Offsets.ModifierStride);
        for (var i = 0; i < ids.Length; i++)
            world.Memory.Write(array + i * Offsets.ModifierStride + Offsets.ModifierId, ids[i]);
        world.Memory.Write(world.Module() + Offsets.GridModuleModifiers, array);
        world.Memory.Write(world.Module() + Offsets.GridModuleModifierCount, (uint)ids.Length);
    }

    public static void SetGameplays(this FakeWorld world, params (int Type, (string Name, long Value)[] Vars)[] gameplays)
    {
        var array = world.Alloc(Math.Max(1, gameplays.Length) * 8);
        for (var i = 0; i < gameplays.Length; i++)
        {
            var record = world.Alloc(0x80);
            world.Memory.Write(record + Offsets.GameplayType, gameplays[i].Type);
            const int bucketCount = 4;
            var buckets = world.Alloc(bucketCount * 8);
            world.Memory.Write(record + Offsets.GameplayVarBuckets, buckets);
            world.Memory.Write(record + Offsets.GameplayVarBuckets + Offsets.HashBucketCount, bucketCount);
            var slot = 0;
            foreach (var (name, value) in gameplays[i].Vars)
            {
                var node = world.Alloc(0x40);
                world.Memory.Write(node + Offsets.VarNodeName, world.Utf8(name));
                world.Memory.Write(node + Offsets.VarNodeNameLength, Encoding.UTF8.GetByteCount(name));
                world.Memory.Write(node + Offsets.VarNodeValue, value);
                var at = buckets + slot++ % bucketCount * 8;
                world.Memory.TryReadPtr(at, out var head);
                world.Memory.Write(node + Offsets.VarNodeNext, head);
                world.Memory.Write(at, node);
            }
            world.Memory.Write(array + i * 8, record);
        }
        world.Memory.Write(world.Scene + Offsets.SceneGameplays, array);
        world.Memory.Write(world.Scene + Offsets.SceneGameplayCount, (uint)gameplays.Length);
    }

    static (int Key, nint Value)[] GamePlayEntities(FakeWorld world) => world.Memory.ReadTMapPointers(world.FightMgr + Offsets.FightMapGamePlay);

    public static nint AddStatue(this FakeWorld world, int entityId, string className, string componentClass = "", int activeOffset = 0, bool active = false)
    {
        var statue = world.AddObject(world.AddClass(className), className);
        if (componentClass != "")
        {
            var component = world.AddObject(world.AddClass(componentClass), componentClass);
            world.Memory.Write(component + activeOffset, (byte)(active ? 1 : 0));
            world.WriteMapAt(statue + Offsets.EntityComponents, (1, component));
        }
        world.WriteMap(Offsets.FightMapGamePlay, [.. GamePlayEntities(world).Select(e => ((int, nint)?)(e.Key, e.Value)), (entityId, statue)]);
        return statue;
    }

    public static void RemoveStatue(this FakeWorld world, int entityId) =>
        world.WriteMap(Offsets.FightMapGamePlay, [.. GamePlayEntities(world).Where(e => e.Key != entityId).Select(e => ((int, nint)?)(e.Key, e.Value))]);
}
