using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record ActorSpot(nint Address, string Class, Vector3 Position);

internal readonly record struct Hit(object Target, Vector3 Position, Vector2 At, float Distance);

internal sealed class PickMode(Func<long> clock, Action<object> pin)
{
    const int MaxHits = 5;
    static readonly Comparison<Hit> ByDistance = (a, b) => a.Distance.CompareTo(b.Distance);

    readonly List<Hit> _hits = [];
    readonly List<Vector2> _projected = [];
    (Entity[] Source, HashSet<int> Ids) _entityIds = (Array.Empty<Entity>(), new HashSet<int>());
    long _nextScan;

    public bool Active;
    public BackgroundJob<ActorSpot[]> ActorsJob = new();
    public ActorSpot[] Actors { get; set; } = [];
    public IReadOnlyList<Hit> Hits => _hits;

    public void Tick()
    {
        if (Active && Input.Pressed(new Hotkey(Key.Escape))) Active = false;
        if (!Active) Actors = [];
    }

    public void Render(Settings settings)
    {
        if (ActorsJob.TakeFinished() is { } done) Actors = done.IsCompletedSuccessfully ? done.Result : [];
        if (!settings.PickActors) Actors = [];
        else if (!ActorsJob.Running && clock() >= _nextScan)
        {
            _nextScan = clock() + 500;
            ActorsJob.Start(ScanActors);
        }

        var io = ImGui.GetIO();
        var clicked = Overlay.ClickBlocker("inspector-pick", Vector2.Zero, io.DisplaySize);
        using var layer = Draw.Layer(DrawLayer.Front);
        var state = Game.State;
        if (state.Camera is not { } camera)
        {
            Draw.TextOutlined("no camera", io.MousePos + new Vector2(16f, 16f), Color.White, Color.Black);
            return;
        }
        Collect(state, camera, io.DisplaySize, io.MousePos, settings);
        foreach (var at in _projected) Draw.Circle(at, 3f, Color.FromArgb(180, 255, 220, 80), 1.5f);
        if (_hits.Count == 0) return;
        Draw.Circle(_hits[0].At, 14f, Color.Yellow, 2f);
        ImGui.SetTooltip(Text.EscPct(string.Join('\n', _hits.Select(hit => Describe(hit, state.Player)))));
        if (clicked) pin(_hits[0].Target);
    }

    internal void Collect(GameState state, Camera camera, Vector2 viewport, Vector2 mouse, Settings settings)
    {
        _hits.Clear();
        _projected.Clear();
        if (state.Player is { } player) Consider(player.Position, player);
        if (settings.PickEntities)
            foreach (var entity in state.Entities)
                Consider(entity.Position, entity);
        if (settings.PickIcons)
        {
            var ids = EntityIds(state.Entities);
            var z = state.Player?.Position.Z ?? 0;
            foreach (var icon in state.MapIcons)
                if (icon.EntityId == 0 || !ids.Contains(icon.EntityId)) Consider(new Vector3(icon.Position, z), icon);
        }
        foreach (var actor in Actors) Consider(actor.Position, actor);
        _hits.Sort(ByDistance);
        if (_hits.Count > MaxHits) _hits.RemoveRange(MaxHits, _hits.Count - MaxHits);

        void Consider<T>(Vector3 position, T target)
        {
            if (camera.ToScreen(position, viewport) is not { } at) return;
            _projected.Add(at);
            var distance = Vector2.Distance(at, mouse);
            if (distance <= settings.PickRadius) _hits.Add(new Hit(target!, position, at, distance));
        }
    }

    HashSet<int> EntityIds(Entity[] entities)
    {
        if (!ReferenceEquals(_entityIds.Source, entities)) _entityIds = (entities, [.. entities.Select(e => e.Id)]);
        return _entityIds.Ids;
    }

    static string Describe(Hit hit, Player? player)
    {
        var distance = player is null ? "" : $"  {player.FlatDistance(hit.Position):0}u";
        return hit.Target switch
        {
            Entity e => $"{e.Kind} {e.Id}  cfg {e.CfgId}  {e.Rarity}  0x{e.Address:X}{distance}",
            MapIcon i => $"Icon {i.Texture}  type {i.Type}{distance}",
            ActorSpot a => $"{a.Class}  0x{a.Address:X}{distance}",
            _ => "Player",
        };
    }

    public static ActorSpot[] ScanActors()
    {
        List<ActorSpot> spots = [];
        foreach (var obj in Ue.Objects())
        {
            if (!obj.IsA("Actor") || obj.IsDefault || obj.Class is not { } cls) continue;
            var at = ObjectIndex.PositionAddress(obj.Address, cls);
            if (at == 0 || !Memory.TryRead(at, out Vector3 position) || !float.IsFinite(position.X + position.Y + position.Z)) continue;
            spots.Add(new ActorSpot(obj.Address, cls.Name, position));
        }
        return [.. spots];
    }
}
