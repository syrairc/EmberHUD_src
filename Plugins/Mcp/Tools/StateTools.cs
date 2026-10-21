using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using EmberHUD;

namespace Mcp;

internal sealed record Status(string Url, int InFlight, long UptimeMs, long MsSinceStateChange, int Watches, bool Recording);

internal sealed record EntityFilter(EntityKind? Kind, Rarity? Rarity, int? CfgId, float? Radius)
{
    public static EntityFilter From(Args args) => new(Parse<EntityKind>(args, "kind"), Parse<Rarity>(args, "rarity"), args.OptInt("cfgId"), args.OptFloat("radius"));

    public IEnumerable<Entity> Apply(GameState state)
    {
        if (Radius is not null && state.Player is null) throw new ToolError("radius needs a player position and there is none yet");
        return state.Entities.Where(entity =>
            (Kind is null || entity.Kind == Kind) &&
            (Rarity is null || entity.Rarity == Rarity) &&
            (CfgId is null || entity.CfgId == CfgId) &&
            (Radius is null || state.Player!.FlatDistance(entity.Position) <= Radius));
    }

    internal static T? Parse<T>(Args args, string name) where T : struct, Enum
    {
        if (args.OptStr(name) is not { } text) return null;
        if (Enum.TryParse<T>(text, true, out var value) && Enum.IsDefined(value)) return value;
        throw new ToolError($"{name} must be one of {string.Join(", ", Enum.GetNames<T>())}");
    }
}

internal static class StateTools
{
    public static Tool[] Create(Func<Status> status) =>
    [
        new("get_status", "Connection and freshness: whether memory is attached, the game status string and area id, how many ms since Game.State last changed, server url and in-flight requests, watch count, recording flag.", Schema.Of([]), _ => GetStatus(status())),
        new("get_state", "Summary of Game.State: status, area id, player position, forward, life and shield, entity counts by kind and rarity, terrain / map icon presence, map icons by type (name, entity ids), quest targets (count and first 10 id / x / y), minimap rotation, open game panels, camera, a map block when there is one (name, monster level, remaining enemies, XP gain, isTown, content rows with name / icon / state / done / total, leftover modifier lines), the market trade page (page, cellsPerPage, priceIcon, first 10 listings, first 10 goods with advanced / itemLevel / flags / affixes) when open, and the open bag (tab, clip min / max, cells count, first 10 cell rects) when one is shown.", Schema.Of([]), _ => GetState()),
        new("get_entities", "Entities from Game.State (life / maxLife, 0 = unknown; name only when known), nearest to the player first. Filters combine: kind, rarity, cfgId, id, address (hex), radius (world units around the player). limit defaults to 100, max 1000.",
            Schema.Of([], ("kind", "string", "EntityKind name, e.g. Monster, Portal, Npc"), ("rarity", "string", "Normal, Magic, Rare or Boss"), ("cfgId", "integer", "config id"), ("id", "integer", "entity id"), ("address", "string", "logic entity address, hex"), ("radius", "number", "world units around the player"), ("limit", "integer", "rows to return, 1 to 1000")), GetEntities),
        new("scene_entities", "Every entity in the scene hash map, not just the ones Game.State draws - id, cfgId, kind (null when the overlay doesn't draw it), UObject class (FightMgr map entries only, else empty), alive, position, logic address. Nearest to the player first when there is one. Filters combine: cfgId, class (case-insensitive substring), radius (world units around the player). limit defaults to 200, max 1000. Empty without a live game.",
            Schema.Of([], ("cfgId", "integer", "config id"), ("class", "string", "class name substring, e.g. EMapGodOf"), ("radius", "number", "world units around the player"), ("limit", "integer", "rows to return, 1 to 1000")), SceneEntities),
        new("fight_entities", "Fighters in the fight world near the player, which the scene list and Game.State often miss (ground rings, bullets, auras). Per fighter: id, x / y, distance, component ids, shape kind (1 circle, 2 rect) with radius and extents, scale (null = no Scale component), side camp (-1 none, 1 hostile, 2 player), owner id. Nearest first. radius is world units around the player (default 600), limit defaults to 100, max 500. Empty without a live game.",
            Schema.Of([], ("radius", "number", "world units around the player, default 600"), ("limit", "integer", "rows to return, 1 to 500")), FightEntities),
        new("get_inventory", "Inventory items from Game.State: bag (Equipment, Skills, Commodities, Other), slot, id, baseId, level, quality, rareValue, count, stackMax, type. Asks the reader for the inventory and waits up to a second for it - ready is false only when the reader did not answer in that time, or when the Lua chain did not resolve - items are empty then, ask again. Optional bag filter.",
            Schema.Of([], ("bag", "string", "Equipment, Skills, Commodities or Other")), GetInventory),
        new("get_vorax", "Season 13 Vorax (surgery minigame) state from Game.State.Vorax: phase, round of rounds, activity, nextRewardActivity, rewardsTaken / rewardsTotal, rerolls, monsterSlots, monsters (slot, id, species, rarity, rarityName, name, count, activity, total), offered cards, owned instruments and obtained protocols (slot, id, kind, rarity, name, description, typeName), rewardBonusPercent, chests. Asks the reader and returns what it has - ready is false when the Vorax page is not open or the reader has not answered yet, ask again.", Schema.Of([]), _ => GetVorax()),
    ];

    static JsonObject GetVorax()
    {
        Game.WantVorax();
        if (Game.State.Vorax is not { } vorax) return new JsonObject { ["ready"] = false };
        static JsonArray Cards(VoraxCard[] cards) => new([.. cards.Select(card => (JsonNode)new JsonObject
        {
            ["slot"] = card.Slot,
            ["id"] = card.Id,
            ["kind"] = card.Kind.ToString(),
            ["rarity"] = card.Rarity,
            ["name"] = card.Name,
            ["description"] = card.Description,
            ["typeName"] = card.TypeName,
        })]);
        return new JsonObject
        {
            ["ready"] = true,
            ["phase"] = vorax.Phase.ToString(),
            ["round"] = vorax.Round,
            ["rounds"] = vorax.Rounds,
            ["activity"] = vorax.Activity,
            ["nextRewardActivity"] = vorax.NextRewardActivity,
            ["rewardsTaken"] = vorax.RewardsTaken,
            ["rewardsTotal"] = vorax.RewardsTotal,
            ["rerolls"] = vorax.Rerolls,
            ["monsterSlots"] = vorax.MonsterSlots,
            ["monsters"] = new JsonArray([.. vorax.Monsters.Select(monster => (JsonNode)new JsonObject
            {
                ["slot"] = monster.Slot,
                ["id"] = monster.Id,
                ["species"] = monster.Species,
                ["rarity"] = monster.Rarity,
                ["rarityName"] = monster.RarityName,
                ["name"] = monster.Name,
                ["count"] = monster.Count,
                ["activity"] = monster.Activity,
                ["total"] = monster.Total,
            })]),
            ["offered"] = Cards(vorax.Offered),
            ["instruments"] = Cards(vorax.Instruments),
            ["protocols"] = Cards(vorax.Protocols),
            ["rewardBonusPercent"] = vorax.RewardBonusPercent,
            ["chests"] = new JsonArray([.. vorax.Chests.Select(chest => (JsonNode)chest)]),
        };
    }

    internal static JsonObject Row(Entity entity, Player? player)
    {
        var row = new JsonObject
        {
            ["id"] = entity.Id,
            ["cfgId"] = entity.CfgId,
            ["kind"] = entity.Kind.ToString(),
            ["rarity"] = entity.Rarity.ToString(),
            ["x"] = Num.F(entity.Position.X),
            ["y"] = Num.F(entity.Position.Y),
            ["z"] = Num.F(entity.Position.Z),
            ["life"] = Math.Round(entity.Life, 1),
            ["maxLife"] = Math.Round(entity.MaxLife, 1),
        };
        if (player is not null) row["distance"] = Num.F(player.FlatDistance(entity.Position));
        if (entity.Name.Length > 0) row["name"] = entity.Name;
        if (entity.Address != 0) row["address"] = Hex.Of(entity.Address);
        if (entity.Kind == EntityKind.Monster && entity.HasGameBar) row["hasBar"] = true;
        if (entity.GameBar is { } bar) row["bar"] = new JsonArray(Num.F(bar.Min.X), Num.F(bar.Min.Y), Num.F(bar.Width), Num.F(bar.Height));
        return row;
    }

    internal static void WriteRow(Utf8JsonWriter json, Entity entity)
    {
        json.WriteStartObject();
        json.WriteNumber("id", entity.Id);
        json.WriteNumber("cfgId", entity.CfgId);
        json.WriteString("kind", entity.Kind.ToString());
        json.WriteString("rarity", entity.Rarity.ToString());
        Num.Write(json, "x", entity.Position.X);
        Num.Write(json, "y", entity.Position.Y);
        Num.Write(json, "z", entity.Position.Z);
        Num.Write(json, "life", entity.Life);
        Num.Write(json, "maxLife", entity.MaxLife);
        if (entity.Name.Length > 0) json.WriteString("name", entity.Name);
        if (entity.Address != 0) json.WriteString("address", Hex.Of(entity.Address));
        if (entity.Kind == EntityKind.Monster && entity.HasGameBar) json.WriteBoolean("hasBar", true);
        if (entity.GameBar is { } bar)
        {
            json.WriteStartArray("bar");
            foreach (var value in (float[])[bar.Min.X, bar.Min.Y, bar.Width, bar.Height])
                Num.WriteValue(json, value);

            json.WriteEndArray();
        }

        json.WriteEndObject();
    }

    static JsonObject GetStatus(Status status)
    {
        var state = Game.State;
            return new JsonObject
        {
            ["memoryAttached"] = Memory.Attached,
            ["gameStatus"] = state.Status,
            ["areaId"] = state.AreaId,
            ["msSinceStateChange"] = status.MsSinceStateChange,
            ["server"] = new JsonObject { ["url"] = status.Url, ["inFlight"] = status.InFlight, ["uptimeMs"] = status.UptimeMs },
            ["watches"] = status.Watches,
            ["recording"] = status.Recording,
        };
    }

    static JsonObject GetState()
    {
        Game.WantTradeGoods();
        var state = Game.State;
        var result = new JsonObject
        {
            ["status"] = state.Status,
            ["areaId"] = state.AreaId,
            ["entityCount"] = state.Entities.Length,
            ["byKind"] = Counts(state.Entities.GroupBy(entity => entity.Kind.ToString())),
            ["byRarity"] = Counts(state.Entities.GroupBy(entity => entity.Rarity.ToString())),
            ["hasTerrain"] = state.Terrain is not null,
            ["mapIcons"] = state.MapIcons.Length,
            ["mapIconTypes"] = new JsonArray([.. state.MapIcons.GroupBy(icon => icon.Type).OrderBy(g => g.Key).Select(g => (JsonNode)new JsonObject
            {
                ["type"] = g.Key,
                ["name"] = g.First().Name,
                ["entityIds"] = new JsonArray([.. g.Select(icon => icon.EntityId).Where(id => id != 0).Order().Select(id => (JsonNode)id)]),
            })]),
            ["questTargets"] = new JsonObject
            {
                ["count"] = state.QuestTargets.Length,
                ["first"] = new JsonArray([.. state.QuestTargets.Take(10).Select(t => (JsonNode)new JsonObject { ["id"] = t.Id, ["x"] = Num.F(t.Position.X), ["y"] = Num.F(t.Position.Y) })]),
            },
            ["projectiles"] = new JsonObject
            {
                ["count"] = state.Projectiles.Length,
                ["first"] = new JsonArray([.. state.Projectiles.Take(10).Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["ownerId"] = p.OwnerId, ["rarity"] = p.OwnerRarity.ToString(), ["x"] = Num.F(p.Position.X), ["y"] = Num.F(p.Position.Y), ["dirX"] = Num.F(p.Direction.X), ["dirY"] = Num.F(p.Direction.Y), ["radius"] = Num.F(p.Radius), ["speed"] = Num.F(p.Speed) })]),
            },
            ["minimapRotation"] = state.MinimapRotation is { } rotation ? Num.F(rotation) : null,
            ["gameMinimap"] = state.GameMinimap is { } gameMinimap ? new JsonObject { ["open"] = gameMinimap.Open, ["worldWidth"] = Num.F(gameMinimap.WorldWidth) } : null,
            ["panels"] = new JsonArray([.. state.Ui.Panels.Select(panel =>
            {
                var node = new JsonObject
                {
                    ["name"] = panel.Name,
                    ["kind"] = panel.Kind.ToString(),
                    ["min"] = new JsonArray(Num.F(panel.Min.X), Num.F(panel.Min.Y)),
                    ["max"] = new JsonArray(Num.F(panel.Max.X), Num.F(panel.Max.Y)),
                    ["fullScreen"] = panel.FullScreen,
                    ["address"] = Hex.Of(panel.Address),
                };
                if (panel.Parts.Length > 0)
                    node["parts"] = new JsonArray([.. panel.Parts.Select(part => (JsonNode)new JsonObject
                    {
                        ["name"] = part.Name,
                        ["min"] = new JsonArray(Num.F(part.Min.X), Num.F(part.Min.Y)),
                        ["max"] = new JsonArray(Num.F(part.Max.X), Num.F(part.Max.Y)),
                    })]);
                return (JsonNode)node;
            })]),
        };
        if (state.Player is { } player)
            result["player"] = new JsonObject { ["x"] = Num.F(player.Position.X), ["y"] = Num.F(player.Position.Y), ["z"] = Num.F(player.Position.Z), ["forwardX"] = Num.F(player.Forward.X), ["forwardY"] = Num.F(player.Forward.Y), ["life"] = Math.Round(player.Life, 1), ["maxLife"] = Math.Round(player.MaxLife, 1), ["shield"] = Math.Round(player.Shield, 1), ["maxShield"] = Math.Round(player.MaxShield, 1) };
        if (state.Camera is { } camera)
            result["camera"] = new JsonObject { ["x"] = Num.F(camera.Location.X), ["y"] = Num.F(camera.Location.Y), ["z"] = Num.F(camera.Location.Z), ["pitch"] = Num.F(camera.Rotation.X), ["yaw"] = Num.F(camera.Rotation.Y), ["roll"] = Num.F(camera.Rotation.Z), ["fov"] = Num.F(camera.Fov) };
        if (state.Area is { } map)
            result["map"] = new JsonObject
            {
                ["name"] = map.Name,
                ["monsterLevel"] = map.MonsterLevel,
                ["remaining"] = map.Remaining,
                ["xpGain"] = map.XpGain,
                ["isTown"] = map.IsTown,
                ["content"] = new JsonArray([.. map.Content.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["icon"] = c.Icon, ["state"] = c.State.ToString(), ["done"] = c.Done, ["total"] = c.Total })]),
                ["modifiers"] = new JsonArray([.. map.Modifiers.Select(m => (JsonNode)m)]),
            };
        if (state.Trade is { } trade)
            result["trade"] = new JsonObject
            {
                ["page"] = trade.Page,
                ["lastPage"] = trade.LastPage,
                ["cellsPerPage"] = trade.CellsPerPage,
                ["priceIcon"] = trade.PriceIcon,
                ["count"] = trade.Listings.Length,
                ["listings"] = new JsonArray([.. trade.Listings.Take(10).Select(l => (JsonNode)new JsonObject
                {
                    ["slot"] = l.Slot,
                    ["name"] = l.Name,
                    ["quantity"] = l.Quantity,
                    ["price"] = l.Price,
                    ["status"] = l.Status,
                    ["min"] = new JsonArray(Num.F(l.Min.X), Num.F(l.Min.Y)),
                    ["max"] = new JsonArray(Num.F(l.Max.X), Num.F(l.Max.Y)),
                })]),
                ["goodsCount"] = state.TradeGoods?.Length ?? 0,
                ["goods"] = new JsonArray([.. (state.TradeGoods ?? []).Take(10).Select(g => (JsonNode)new JsonObject
                {
                    ["goodId"] = g.GoodId.ToString(),
                    ["baseId"] = g.Item.BaseId,
                    ["quantity"] = g.Item.Count,
                    ["price"] = g.Price,
                    ["priceType"] = g.PriceType,
                    ["status"] = g.Status,
                    ["advanced"] = g.Advanced,
                    ["itemLevel"] = g.ItemLevel,
                    ["flags"] = new JsonArray([.. g.Flags.Select(f => (JsonNode)f)]),
                    ["affixes"] = new JsonArray([.. g.Affixes.Select(a => (JsonNode)new JsonObject
                    {
                        ["kind"] = a.Kind.ToString(),
                        ["id"] = a.Id,
                        ["tier"] = a.Tier,
                        ["rolls"] = new JsonArray([.. a.Rolls.Select(Num.F)]),
                        ["text"] = a.Text,
                    })]),
                })]),
            };
        if (state.Bag is { } bag)
            result["bag"] = new JsonObject
            {
                ["tab"] = bag.Tab.ToString(),
                ["clip"] = new JsonObject { ["min"] = new JsonArray(Num.F(bag.ClipMin.X), Num.F(bag.ClipMin.Y)), ["max"] = new JsonArray(Num.F(bag.ClipMax.X), Num.F(bag.ClipMax.Y)) },
                ["cells"] = bag.Cells.Length,
                ["first"] = new JsonArray([.. bag.Cells.Take(10).Select(c => (JsonNode)new JsonObject
                {
                    ["slot"] = c.Slot,
                    ["min"] = new JsonArray(Num.F(c.Min.X), Num.F(c.Min.Y)),
                    ["max"] = new JsonArray(Num.F(c.Max.X), Num.F(c.Max.Y)),
                })]),
            };
        return result;
    }

    static JsonObject GetEntities(Args args)
    {
        var state = Game.State;
        var id = args.OptInt("id");
        var address = args.OptAddr("address");
        var limit = args.Int("limit", 100, 1, 1000);
        var matches = EntityFilter.From(args).Apply(state).Where(entity => (id is null || entity.Id == id) && (address is null || entity.Address == address));
        var ordered = state.Player is { } player ? matches.OrderBy(entity => player.FlatDistance(entity.Position)) : matches.OrderBy(entity => entity.Id);
        return Page(ordered.ToArray(), limit, entity => Row(entity, state.Player));
    }

    static JsonObject GetInventory(Args args)
    {
        var bag = EntityFilter.Parse<InventoryBag>(args, "bag");
        Game.WantInventory();
        var items = Game.State.Inventory;
        for (var waited = 0; items is null && waited < 1000; waited += 50)
        {
            Thread.Sleep(50);
            items = Game.State.Inventory;
        }
        return new JsonObject
        {
            ["ready"] = items is not null,
            ["items"] = new JsonArray([.. (items ?? []).Where(item => bag is null || item.Bag == bag).Select(item => (JsonNode)new JsonObject
            {
                ["bag"] = item.Bag.ToString(),
                ["slot"] = item.Slot,
                ["id"] = item.Id,
                ["baseId"] = item.BaseId,
                ["level"] = item.Level,
                ["quality"] = item.Quality,
                ["rareValue"] = item.RareValue,
                ["count"] = item.Count,
                ["stackMax"] = item.StackMax,
                ["type"] = item.Type,
            })]),
        };
    }

    static JsonObject SceneEntities(Args args)
    {
        var cfgId = args.OptInt("cfgId");
        var className = args.OptStr("class");
        var radius = args.OptFloat("radius");
        var limit = args.Int("limit", 200, 1, 1000);
        var player = Game.State.Player;
        var listed = Scene.Entities();
        if (radius is not null && player is null && listed.Length > 0) throw new ToolError("radius needs a player position and there is none yet");

        float? Distance(SceneEntity entity) => player is not null && entity.Position is { } at ? player.FlatDistance(at) : null;

        var all = listed
            .Where(entity => (cfgId is null || entity.CfgId == cfgId) &&
                             (className is null || entity.Class.Contains(className, StringComparison.OrdinalIgnoreCase)) &&
                             (radius is null || Distance(entity) <= radius))
            .OrderBy(entity => Distance(entity) ?? float.MaxValue).ThenBy(entity => entity.Id)
            .ToArray();
        return Page(all, limit, entity =>
        {
            var at = entity.Position ?? new Vector3(float.NaN);
            return new JsonObject
            {
                ["id"] = entity.Id,
                ["cfgId"] = entity.CfgId,
                ["kind"] = entity.Kind?.ToString(),
                ["class"] = entity.Class,
                ["alive"] = entity.Alive,
                ["x"] = Num.F(at.X),
                ["y"] = Num.F(at.Y),
                ["z"] = Num.F(at.Z),
                ["distance"] = Num.F(Distance(entity) ?? float.NaN),
                ["address"] = entity.Address == 0 ? null : Hex.Of(entity.Address),
            };
        });
    }

    static JsonObject FightEntities(Args args)
    {
        var limit = args.Int("limit", 100, 1, 500);
        var radius = args.OptFloat("radius") ?? 600;
        var player = Game.State.Player ?? throw new ToolError("fight_entities needs a player position and there is none yet");
        var at = new Vector2(player.Position.X, player.Position.Y);
        return Page(FightScan.Near(at, radius), limit, probe => new JsonObject
        {
            ["id"] = probe.Id,
            ["x"] = Num.F(probe.Position.X),
            ["y"] = Num.F(probe.Position.Y),
            ["distance"] = Num.F(Vector2.Distance(probe.Position, at)),
            ["components"] = new JsonArray([.. probe.Components.Select(component => (JsonNode)component)]),
            ["shapeKind"] = probe.ShapeKind,
            ["shapeRadius"] = probe.ShapeRadius,
            ["extentA"] = probe.ExtentA,
            ["extentB"] = probe.ExtentB,
            ["scale"] = double.IsFinite(probe.Scale) ? Math.Round(probe.Scale, 3) : null,
            ["camp"] = probe.Camp,
            ["ownerId"] = probe.OwnerId,
        });
    }

    static JsonObject Page<T>(T[] rows, int limit, Func<T, JsonNode> row) => new()
    {
        ["total"] = rows.Length,
        ["returned"] = Math.Min(limit, rows.Length),
        ["entities"] = new JsonArray([.. rows.Take(limit).Select(row)]),
    };

    static JsonObject Counts(IEnumerable<IGrouping<string, Entity>> groups) =>
        new(groups.OrderBy(group => group.Key).Select(group => KeyValuePair.Create(group.Key, (JsonNode?)group.Count())));
}
