using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    static T? Parse<T>(Args args, string name) where T : struct, Enum
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
        new("get_state", "Summary of Game.State: status, area id, player position, forward, life and shield, entity counts by kind and rarity, terrain / map icon presence, minimap rotation, open game panels, camera, a map block when there is one (name, monster level, remaining enemies, XP gain, isTown, content rows with name / icon / state / done / total, leftover modifier lines), and the market trade page (page, cellsPerPage, priceIcon, first 10 listings) when open.", Schema.Of([]), _ => GetState()),
        new("get_entities", "Entities from Game.State (life / maxLife, 0 = unknown; name only when known), nearest to the player first. Filters combine: kind, rarity, cfgId, id, address (hex), radius (world units around the player). limit defaults to 100, max 1000.",
            Schema.Of([], ("kind", "string", "EntityKind name, e.g. Monster, Portal, Npc"), ("rarity", "string", "Normal, Magic, Rare or Boss"), ("cfgId", "integer", "config id"), ("id", "integer", "entity id"), ("address", "string", "logic entity address, hex"), ("radius", "number", "world units around the player"), ("limit", "integer", "rows to return, 1 to 1000")), GetEntities),
        new("scene_entities", "Every entity in the scene hash map, not just the ones Game.State draws - id, cfgId, kind (null when the overlay doesn't draw it), UObject class (FightMgr map entries only, else empty), alive, position, logic address. Nearest to the player first when there is one. Filters combine: cfgId, class (case-insensitive substring), radius (world units around the player). limit defaults to 200, max 1000. Empty without a live game.",
            Schema.Of([], ("cfgId", "integer", "config id"), ("class", "string", "class name substring, e.g. EMapGodOf"), ("radius", "number", "world units around the player"), ("limit", "integer", "rows to return, 1 to 1000")), SceneEntities),
    ];

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
            ["minimapRotation"] = state.MinimapRotation is { } rotation ? Num.F(rotation) : null,
            ["panels"] = new JsonArray([.. state.Ui.Panels.Select(panel => (JsonNode)new JsonObject
            {
                ["name"] = panel.Name,
                ["kind"] = panel.Kind.ToString(),
                ["min"] = new JsonArray(Num.F(panel.Min.X), Num.F(panel.Min.Y)),
                ["max"] = new JsonArray(Num.F(panel.Max.X), Num.F(panel.Max.Y)),
                ["fullScreen"] = panel.FullScreen,
                ["address"] = Hex.Of(panel.Address),
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

    static JsonObject Page<T>(T[] rows, int limit, Func<T, JsonNode> row) => new()
    {
        ["total"] = rows.Length,
        ["returned"] = Math.Min(limit, rows.Length),
        ["entities"] = new JsonArray([.. rows.Take(limit).Select(row)]),
    };

    static JsonObject Counts(IEnumerable<IGrouping<string, Entity>> groups) =>
        new(groups.OrderBy(group => group.Key).Select(group => KeyValuePair.Create(group.Key, (JsonNode?)group.Count())));
}
