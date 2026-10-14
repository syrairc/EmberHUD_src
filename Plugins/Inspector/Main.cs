using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record Pin(int Id, string Label, object? Api, nint Address, nint ClassAtPin, RawView View);

public sealed class Main : Plugin<Settings>
{
    static readonly Vector4 Amber = new(1f, 0.75f, 0.2f, 1f);
    GameState? _frozen;
    int _nextPin;

    internal bool Open;
    internal Func<long> Clock = () => Environment.TickCount64;
    internal ApiTree Browser { get; }
    internal Snapshots Snapshots { get; }
    internal RawView Raw { get; }
    internal Explorer Explorer { get; }
    internal PickMode Pick { get; }
    internal List<Pin> Pins { get; } = [];
    readonly Dictionary<int, (MapIcon[] Source, MapIcon[] Icons)> _icons = [];
    readonly (string Label, Func<bool>? Body)[] _tabs;

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
        Snapshots = new Snapshots(() => Clock());
        Browser = new ApiTree(PinThing);
        Raw = new RawView(Snapshots, PinThing);
        Explorer = new Explorer(Snapshots, PinThing);
        Pick = new PickMode(() => Clock(), PinThing);
        _tabs =
        [
            .. ApiTree.All.Select(section => (section.Name, (Func<bool>?)Tab(() => Browser.Show(section, Shown)))),
            ("Globals", Tab(Raw.Globals)),
            ("Objects", Tab(() => Explorer.Draw(Shown.AreaId))),
            ("Address", Tab(Raw.Address)),
        ];
    }

    static Func<bool> Tab(Action draw) => () =>
    {
        draw();
        return false;
    };

    internal void Freeze()
    {
        _frozen = Shown;
        Snapshots.Frozen = true;
    }

    internal void Resume()
    {
        _frozen = null;
        Snapshots.Frozen = false;
    }

    internal void PinThing(object target)
    {
        var (label, api, address) = target switch
        {
            Entity entity => ($"Entity {entity.Id}", (object?)entity, entity.Address),
            Player player => ("Player", player, (nint)0),
            MapIcon icon => ($"Icon {icon.Texture}", icon, (nint)0),
            ActorSpot actor => (actor.Class, null, actor.Address),
            RawPin raw => (raw.Label, null, raw.Address),
            _ => (target.GetType().Name, target, (nint)0),
        };
        if (Pins.Any(pin => address != 0 ? pin.Address == address : Equals(PinKey(pin.Api), PinKey(api)))) return;
        Pins.Add(new Pin(_nextPin++, label, api, address, Ue.TryObject(address, out var obj) ? obj.ClassAddress : 0, new RawView(Snapshots, PinThing)));
    }

    static object? PinKey(object? api) => api switch
    {
        Player => typeof(Player),
        Entity entity => entity.Id,
        MapIcon icon => (icon.Texture, icon.Position),
        _ => api,
    };

    internal static bool Gone(Pin pin)
    {
        if (pin.Address == 0) return false;
        if (!Memory.Attached) return true;
        if (pin.ClassAtPin == 0) return !Memory.TryRead(pin.Address, out long _);
        return !Ue.TryObject(pin.Address, out var obj) || obj.ClassAddress != pin.ClassAtPin;
    }

    public override void Initialize()
    {
        RegisterHotkey("Toggle window", "Opens and closes the Inspector", () => Open = !Open, new Hotkey(Key.F6));
        RegisterHotkey("Pick mode", "Hover things in the world and click to pin a window for them. The game gets no clicks while it's on, Esc leaves", () => Pick.Active = !Pick.Active, new Hotkey(Key.F7));
    }

    public override void Tick() => Pick.Tick();

    public override void Render()
    {
        Snapshots.RefreshRate = Settings.RefreshRate;
        Snapshots.Prune();
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
                Layout.SideTabs("sections", 110f, _tabs);
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
        Snapshots.Clear();
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
                if (pin.Api is { } api) Browser.Draw($"api{pin.Id}", Current(api, state));
                if (pin.Api is Entity entity && IconsOf(pin, entity, state) is { Length: > 0 } icons) Browser.Draw($"icons{pin.Id}", icons);
                if (pin.Address != 0)
                {
                    ImGui.Separator();
                    if (RawView.Attached())
                    {
                        if (Gone(pin)) ImGui.TextDisabled("gone");
                        else
                        {
                            pin.View.Toolbar();
                            pin.View.Node($"raw{pin.Id}", pin.Address, 0);
                        }
                    }
                }
            }
            ImGui.End();
            if (open) continue;
            Pins.Remove(pin);
            _icons.Remove(pin.Id);
        }
    }

    static object Current(object api, GameState state) => api switch
    {
        Entity entity => state.Entities.FirstOrDefault(e => e.Id == entity.Id) is { Id: > 0 } live ? live : entity,
        Player player => state.Player ?? (object)player,
        _ => api,
    };

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
