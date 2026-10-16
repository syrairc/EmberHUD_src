namespace EmberHUD.Host.Reading;

internal sealed class EntityNamer(LocText loc)
{
    private readonly HashSet<(string Table, int Id)> _missing = [];
    private byte _language = byte.MaxValue;

    public Entity[] Apply(Entity[] entities)
    {
        if (entities.Length == 0 || !loc.TryReadLanguage(out var language)) return entities;
        if (language != _language)
        {
            _missing.Clear();
            _language = language;
        }

        Dictionary<(string, int), string> frame = [];
        var named = false;
        var result = entities;
        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            var table = entity.Kind switch
            {
                EntityKind.Monster => Offsets.NpcNameKey,
                EntityKind.Npc => Offsets.NpcViewNameKey,
                EntityKind.Ally => Offsets.ServantNameKey,
                _ => null,
            };
            if (table is null || entity.CfgId <= 0 || _missing.Contains((table, entity.CfgId))) continue;
            if (!frame.TryGetValue((table, entity.CfgId), out var name))
            {
                var readable = loc.TryGet(table + entity.CfgId, out var text);
                if (readable && text is null) _missing.Add((table, entity.CfgId));
                name = text ?? "";
                frame[(table, entity.CfgId)] = name;
            }
            if (name.Length == 0) continue;
            if (!named)
            {
                result = [.. entities];
                named = true;
            }
            result[i] = entity with { Name = name };
        }
        return result;
    }
}
