namespace EmberHUD.Host.Reading;

internal sealed class EntityNamer(LocText loc)
{
    private readonly HashSet<int> _missing = [];
    private byte _language = byte.MaxValue;

    public Entity[] Apply(Entity[] entities)
    {
        if (entities.Length == 0 || !loc.TryReadLanguage(out var language)) return entities;
        if (language != _language)
        {
            _missing.Clear();
            _language = language;
        }

        Dictionary<int, string> frame = [];
        var named = false;
        var result = entities;
        for (var i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (entity.Kind is not (EntityKind.Monster or EntityKind.Ally) || entity.CfgId <= 0 || _missing.Contains(entity.CfgId)) continue;
            if (!frame.TryGetValue(entity.CfgId, out var name))
            {
                var readable = loc.TryGet(Offsets.NpcNameKey + entity.CfgId, out var text);
                if (readable && text is null) _missing.Add(entity.CfgId);
                name = text ?? "";
                frame[entity.CfgId] = name;
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
