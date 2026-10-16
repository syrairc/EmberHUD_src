using System;
using System.IO;
using EmberHUD;
using Hexa.NET.ImGui;

namespace NameplatesPlugin;

public sealed class Main : Plugin<Settings>
{
    public override void Initialize()
    {
        Settings.Normalize();
        BarTextures.Load(BarTextures.DropInFolder);
    }

    public override void Render()
    {
        var state = Game.State;
        if (Settings.HideBehindPanels && state.Ui.AnyFullScreen || state.Camera is not { } camera) return;
        var viewport = ImGui.GetIO().DisplaySize;
        var player = state.Player;
        var entities = state.Entities;
        for (var i = 0; i < entities.Length; i++)
        {
            ref readonly var entity = ref entities[i];
            var distance = Settings.Displayed(player?.FlatDistance(entity.Position) ?? 0);
            if (Resolver.Resolve(Settings, entity, distance) is not { } style) continue;
            if (camera.ToScreen(entity.Position, viewport) is not { } anchor) continue;
            var data = new PlateData(entity.Name, entity.Life, entity.MaxLife, distance, entity.Rarity.ToString(), entity.Kind.ToString());
            PlateRenderer.DrawPlate(data, style, anchor, viewport);
        }
        if (player is null) return;
        if (Resolver.ResolveBucket(Settings, Bucket.Self, "", 0, player.Life, player.MaxLife, 0) is not { } selfStyle) return;
        if (camera.ToScreen(player.Position, viewport) is not { } selfAnchor) return;
        PlateRenderer.DrawPlate(new PlateData("", player.Life, player.MaxLife, 0, "", "Player"), selfStyle, selfAnchor, viewport);
    }
}
