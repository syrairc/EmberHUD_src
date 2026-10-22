using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record Pin(int Id, string Label, object Api);

public sealed class Main : Plugin<Settings>
{
    static readonly Vector4 Amber = new(1f, 0.75f, 0.2f, 1f);
    GameState? _frozen;
    int _nextPin;

    internal bool Open;
    internal ApiTree Browser { get; }
    internal PickMode Pick { get; }
    internal List<Pin> Pins { get; } = [];
    readonly Dictionary<int, (MapIcon[] Source, MapIcon[] Icons)> _icons = [];

    internal MapIcon[] IconsOf(Pin pin, Entity entity, GameState state)
    {
        if (_icons.TryGetValue(pin.Id, out var cached) && ReferenceEquals(cached.Source, state.MapIcons)) return cached.Icons;
        var icons = state.MapIcons.Where(icon => icon.EntityId == entity.Id).ToArray();
        _icons[pin.Id] = (state.MapIcons, icons);
        return icons;
    }
    internal GameState Shown => _frozen ?? Game.State;
    internal bool Frozen => _frozen is not null;

    public Main()
    {
        Browser = new ApiTree(PinThing);
        Pick = new PickMode(PinThing);
    }

    internal void Freeze() => _frozen = Shown;

    internal void Resume() => _frozen = null;

    internal void PinThing(object target)
    {
        var label = target switch
        {
            Entity entity => $"Entity {entity.Id}",
            Player => "Player",
            MapIcon { EntityId: 0 } icon => $"Icon {icon.Texture} (snapshot)",
            MapIcon icon => $"Icon {icon.Texture}",
            _ => target.GetType().Name,
        };
        if (Pins.Any(pin => Equals(PinKey(pin.Api), PinKey(target)))) return;
        Pins.Add(new Pin(_nextPin++, label, target));
    }

    static object PinKey(object api) => api switch
    {
        Player => typeof(Player),
        Entity entity => entity.Id,
        MapIcon { EntityId: not 0 } icon => ("icon", icon.EntityId),
        MapIcon icon => (icon.Texture, icon.Position),
        _ => api,
    };

    internal static object? Current(Pin pin, GameState state) => pin.Api switch
    {
        Entity entity => state.Entities.Where(e => e.Id == entity.Id).Cast<object?>().FirstOrDefault(),
        Player => state.Player,
        MapIcon { EntityId: not 0 } icon => state.MapIcons.Where(i => i.EntityId == icon.EntityId).Cast<object?>().FirstOrDefault(),
        var api => api,
    };

    public override void Initialize()
    {
        RegisterHotkey("Toggle window", "Opens and closes the Inspector", () => Open = !Open, new Hotkey(Key.F6));
        RegisterHotkey("Pick mode", "Hover things in the world and click to pin a window for them. The game gets no clicks while it's on, Esc leaves", () => Pick.Active = !Pick.Active, new Hotkey(Key.F7));
    }

    public override void Tick() => Pick.Tick();

    public override void Render()
    {
        if (Pick.Active) Pick.Render(Settings);
        var state = Shown;
        Browser.BeginFrame();
        var shown = false;
        if (Open)
        {
            ImGui.SetNextWindowSize(new Vector2(900, 600), ImGuiCond.FirstUseEver);
            shown = ImGui.Begin("Inspector###inspector", ref Open);
            if (shown)
            {
                DrawHeader(state);
                Browser.Show(state);
            }
            ImGui.End();
        }
        DrawPins(state);
        if (shown && Open) DrawHighlights();
    }

    public override void Dispose()
    {
        Browser.Clear();
        Walker.Clear();
        Pins.Clear();
        _icons.Clear();
    }

    void DrawPins(GameState state)
    {
        foreach (var pin in Pins.ToArray())
        {
            var open = true;
            ImGui.SetNextWindowSize(new Vector2(560, 480), ImGuiCond.FirstUseEver);
            if (ImGui.Begin($"{pin.Label}###pin{pin.Id}", ref open))
            {
                if (Current(pin, state) is { } live) Browser.Draw($"api{pin.Id}", live);
                else ImGui.TextDisabled("gone");
                if (pin.Api is Entity entity && IconsOf(pin, entity, state) is { Length: > 0 } icons) Browser.Draw($"icons{pin.Id}", icons);
            }
            ImGui.End();
            if (open) continue;
            Pins.Remove(pin);
            _icons.Remove(pin.Id);
        }
    }

    void DrawHeader(GameState state)
    {
        ImGui.TextUnformatted($"Status: {state.Status}    Area: {state.AreaId}    {(Memory.Attached ? "attached" : "not attached")}");
        ImGui.SameLine();
        if (Frozen)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Amber);
            ImGui.TextUnformatted("FROZEN");
            ImGui.PopStyleColor();
            ImGui.SameLine();
        }
        if (ImGui.Button(Frozen ? "Resume" : "Freeze"))
        {
            if (Frozen) Resume();
            else Freeze();
        }
        ImGui.SameLine();
        ImGui.Checkbox("Show not provided", ref Browser.ShowNulls);
        if (!Browser.ShowNulls)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"{Browser.RootHidden} not provided");
        }
        ImGui.Separator();
    }

    void DrawHighlights()
    {
        var camera = Game.State.Camera;
        var map = Map.View;
        if (camera is null && map is null) return;
        var viewport = ImGui.GetIO().DisplaySize;
        using var layer = Draw.Layer(DrawLayer.Front);
        foreach (var (position, label) in Browser.Highlights())
        {
            if (camera?.ToScreen(position, viewport) is { } world) Ring(world, label);
            if (map?.ToScreen(position) is { } onMap) Ring(onMap, label);
        }
    }

    static void Ring(Vector2 at, string label)
    {
        Draw.Circle(at, 12f, Color.Yellow, 2f);
        Draw.TextOutlined(label, at + new Vector2(14f, -8f), Color.White, Color.Black);
    }
}
