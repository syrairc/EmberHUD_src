using System;
using System.Numerics;
using Hexa.NET.ImGui;
using TorchlightSDK;

namespace Minimap;

public sealed class Main : Plugin<Settings>
{
    internal BackgroundJob<Walls> WallJob = new();
    Walls _walls = Walls.Empty;

    public override void Tick()
    {
        var view = Settings.View;
        if (view.ToggleMap.Pressed) view.Show.Value = !view.Show.Value;
        if (view.ZoomIn.Pressed) view.Zoom.Value = MathF.Min(view.Zoom.Value * 1.25f, view.Zoom.Max);
        if (view.ZoomOut.Pressed) view.Zoom.Value = MathF.Max(view.Zoom.Value / 1.25f, view.Zoom.Min);

        var state = Game.State;
        if (WallJob.TakeFinished() is { } done) _walls = done.GetAwaiter().GetResult();
        if (_walls.AreaId != state.AreaId) _walls = Walls.Empty;
        if (!WallJob.Running && !_walls.Matches(state.AreaId, state.Terrain))
        {
            var area = state.AreaId;
            var terrain = state.Terrain;
            WallJob.Start(() => Walls.Build(area, terrain));
        }

        if (view.Show.Value) Map.View = ViewFor(Settings, state.Player?.Position, _walls, ImGui.GetIO().DisplaySize);
    }

    public static MapView? ViewFor(Settings settings, Vector3? player, Walls walls, Vector2 viewport)
    {
        var view = settings.View;
        var map = view.FollowPlayer.Value && player is { } at
            ? MapView.Follow(Map.Project(at), viewport, view.Zoom.Value)
            : MapView.Fit(walls.Min, walls.Max, viewport, 40);
        return map.Scale > 0 ? map with { Offset = map.Offset + new Vector2(view.OffsetX.Value, view.OffsetY.Value) } : null;
    }
}
