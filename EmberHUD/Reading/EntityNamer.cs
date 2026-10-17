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
            var name = table is null ? "" : Lookup(frame, table, entity.CfgId);
            var castName = entity.Cast is { } cast ? Lookup(frame, Offsets.SkillStoneNameKey, cast.StoneId) : "";
            if (name.Length == 0 && castName.Length == 0) continue;
            if (ReferenceEquals(result, entities)) result = [.. entities];
            result[i] = entity with { Name = name.Length > 0 ? name : entity.Name, Cast = castName.Length > 0 ? entity.Cast!.Value with { Name = castName } : entity.Cast };
        }
        return result;
    }

    private string Lookup(Dictionary<(string, int), string> frame, string table, int id)
    {
        if (id <= 0 || _missing.Contains((table, id))) return "";
        if (frame.TryGetValue((table, id), out var name)) return name;
        var readable = loc.TryGet(table + id, out var text);
        if (readable && text is null) _missing.Add((table, id));
        return frame[(table, id)] = text ?? "";
    }
}
