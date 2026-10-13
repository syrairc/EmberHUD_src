using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using Aember;

namespace Mcp;

internal sealed record Status(string Url, int InFlight, long UptimeMs, long MsSinceStateChange, int Watches, bool Recording);

internal sealed record EntityFilter(EntityKind? Kind, Rarity? Rarity, int? CfgId, float? Radius)
{
    public static EntityFilter From(Args args) => new(Parse<EntityKind>(args, "kind"), Parse<Rarity>(args, "rarity"), args.OptInt("cfgId"), args.OptFloat("radius"));

    public static float Distance(Player player, Entity entity) =>
        Vector2.Distance(new Vector2(player.Position.X, player.Position.Y), new Vector2(entity.Position.X, entity.Position.Y));

    public IEnumerable<Entity> Apply(GameState state)
    {
        if (Radius is not null && state.Player is null) throw new ToolError("radius needs a player position and there is none yet");
        return state.Entities.Where(entity =>
            (Kind is null || entity.Kind == Kind) &&
            (Rarity is null || entity.Rarity == Rarity) &&
            (CfgId is null || entity.CfgId == CfgId) &&
            (Radius is null || Distance(state.Player!, entity) <= Radius));
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
        new("get_state", "Summary of Game.State: status, area id, player position and forward, entity counts by kind and rarity, terrain / map icon presence, minimap rotation, open game panels, camera, a map block when there is one (name, monster level, remaining enemies, XP gain, isTown, content rows with name / icon / state / done / total, leftover modifier lines), and the market trade page (page, cellsPerPage, priceIcon, first 10 listings) when open.", Schema.Of([]), _ => GetState()),
        new("get_entities", "Entities from Game.State, nearest to the player first. Filters combine: kind, rarity, cfgId, id, address (hex), radius (world units around the player). limit defaults to 100, max 1000.",
            Schema.Of([], ("kind", "string", "EntityKind name, e.g. Monster, Portal, Npc"), ("rarity", "string", "Normal, Magic, Rare or Boss"), ("cfgId", "integer", "config id"), ("id", "integer", "entity id"), ("address", "string", "logic entity address, hex"), ("radius", "number", "world units around the player"), ("limit", "integer", "rows to return, 1 to 1000")), GetEntities),
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
        };
        if (player is not null) row["distance"] = Num.F(EntityFilter.Distance(player, entity));
        if (entity.Address != 0) row["address"] = Hex.Of(entity.Address);
        return row;
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
            result["player"] = new JsonObject { ["x"] = Num.F(player.Position.X), ["y"] = Num.F(player.Position.Y), ["z"] = Num.F(player.Position.Z), ["forwardX"] = Num.F(player.Forward.X), ["forwardY"] = Num.F(player.Forward.Y) };
        if (state.Camera is { } camera)
            result["camera"] = new JsonObject { ["x"] = Num.F(camera.Location.X), ["y"] = Num.F(camera.Location.Y), ["z"] = Num.F(camera.Location.Z), ["pitch"] = Num.F(camera.Rotation.X), ["yaw"] = Num.F(camera.Rotation.Y), ["roll"] = Num.F(camera.Rotation.Z), ["fov"] = Num.F(camera.Fov) };
        if (state.Map is { } map)
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
        var ordered = state.Player is { } player ? matches.OrderBy(entity => EntityFilter.Distance(player, entity)) : matches.OrderBy(entity => entity.Id);
        var all = ordered.ToArray();
        return new JsonObject
        {
            ["total"] = all.Length,
            ["returned"] = Math.Min(limit, all.Length),
            ["entities"] = new JsonArray(all.Take(limit).Select(entity => (JsonNode)Row(entity, state.Player)).ToArray()),
        };
    }

    static JsonObject Counts(IEnumerable<IGrouping<string, Entity>> groups) =>
        new(groups.OrderBy(group => group.Key).Select(group => KeyValuePair.Create(group.Key, (JsonNode?)group.Count())));
}
