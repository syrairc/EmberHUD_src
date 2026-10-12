using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aember;

namespace Inspector;

internal sealed record Candidate(string Label, Vector3 Position, object Target);

internal sealed record ActorSpot(nint Address, string Class, Vector3 Position);

internal static class Pick
{
    const int MaxUnder = 5;

    public static List<Candidate> Candidates(GameState state, bool entities, bool icons, IReadOnlyList<ActorSpot> actors)
    {
        List<Candidate> list = [];
        if (state.Player is { } player) list.Add(new Candidate("Player", player.Position, player));
        if (entities)
            foreach (var entity in state.Entities)
                list.Add(new Candidate($"{entity.Kind} {entity.Id}", entity.Position, entity));
        if (icons)
        {
            var ids = state.Entities.Select(e => e.Id).ToHashSet();
            var z = state.Player?.Position.Z ?? 0;
            foreach (var icon in state.MapIcons.Where(i => i.EntityId == 0 || !ids.Contains(i.EntityId)))
                list.Add(new Candidate($"Icon {icon.Texture}", new Vector3(icon.Position, z), icon));
        }
        foreach (var actor in actors) list.Add(new Candidate(actor.Class, actor.Position, actor));
        return list;
    }

    public static (Candidate Candidate, Vector2 At)[] Under(IEnumerable<Candidate> candidates, Camera camera, Vector2 viewport, Vector2 mouse, float radius) =>
    [
        .. candidates
            .Select(c => (Candidate: c, At: camera.ToScreen(c.Position, viewport)))
            .Where(p => p.At is { } at && Vector2.Distance(at, mouse) <= radius)
            .Select(p => (p.Candidate, At: p.At!.Value))
            .OrderBy(p => Vector2.Distance(p.At, mouse))
            .Take(MaxUnder),
    ];

    public static ActorSpot[] ScanActors()
    {
        List<ActorSpot> spots = [];
        foreach (var obj in Ue.Objects())
        {
            if (!obj.IsA("Actor") || obj.IsDefault || obj.Class is not { } cls || cls.Property("RootComponent") is not { } root ||
                !Memory.TryRead(obj.Address + root.Offset, out nint component) || !Ue.TryObject(component, out var comp) ||
                comp.Class?.Property("RelativeLocation") is not { } location ||
                !Memory.TryRead(component + location.Offset, out Vector3 at) || !float.IsFinite(at.X + at.Y + at.Z))
                continue;
            spots.Add(new ActorSpot(obj.Address, cls.Name, at));
        }
        return [.. spots];
    }
}
