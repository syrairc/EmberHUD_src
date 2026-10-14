using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed class Pin(int id, string label, object? api, nint address, nint classAtPin)
{
    public int Id => id;
    public string Label => label;
    public object? Api => api;
    public nint Address => address;
    public nint ClassAtPin => classAtPin;
}

public sealed class Main : Plugin<Settings>
{
    static readonly Vector4 Amber = new(1f, 0.75f, 0.2f, 1f);
    GameState? _frozen;
    int _nextPin;
    long _nextActorScan;

    internal bool Open;
    internal bool Picking;
    internal BackgroundJob<ActorSpot[]> ActorsJob = new();
    internal ActorSpot[] Actors { get; set; } = [];
    internal Func<long> Clock = () => Environment.TickCount64;
    internal ApiTree Browser { get; } = new();
    internal Snapshots Snapshots { get; }
    internal RawTree Raw { get; }
    internal Explorer Explorer { get; }
    internal List<Pin> Pins { get; } = [];
    internal GameState Shown => _frozen ?? Game.State;
    internal bool Frozen => _frozen is not null;

    public Main()
    {
        Snapshots = new Snapshots(() => Clock());
        Raw = new RawTree(Snapshots);
        Explorer = new Explorer(Raw, Snapshots);
    }

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
        Pins.Add(new Pin(_nextPin++, label, api, address, Ue.TryObject(address, out var obj) ? obj.ClassAddress : 0));
    }

    static object? PinKey(object? api) => api switch
    {
        Player => typeof(Player),
        Entity entity => entity.Id,
        MapIcon icon => (icon.Texture, icon.Position),
        _ => api,
    };

    internal static bool Gone(Pin pin) =>
        pin.Address != 0 && (!Memory.Attached || (pin.ClassAtPin != 0 ? !Ue.TryObject(pin.Address, out var obj) || obj.ClassAddress != pin.ClassAtPin : !Memory.TryRead(pin.Address, out long _)));

    public override void Initialize()
    {
        RegisterHotkey("Toggle window", "Opens and closes the Inspector", () => Open = !Open, new Hotkey(Key.F6));
        RegisterHotkey("Pick mode", "Hover things in the world and click to pin a window for them. The game gets no clicks while it's on, Esc leaves", () => Picking = !Picking, new Hotkey(Key.F7));
    }

    public override void Tick()
    {
        if (Picking && Input.Pressed(new Hotkey(Key.Escape))) Picking = false;
        if (!Picking) Actors = [];
    }

    public override void Render()
    {
        Snapshots.RefreshRate = Settings.RefreshRate;
        Snapshots.Prune();
        if (Picking) DrawPick();
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
                SideNav.SideTabs("sections", 110f, [.. Tabs(state)]);
            }
            ImGui.End();
        }
        DrawPins(state);
        TakeRequests();
        if (shown && Open) DrawHighlights();
    }

    public override void Dispose()
    {
        Browser.Clear();
        Walker.Clear();
        Snapshots.Clear();
        Pins.Clear();
    }

    IEnumerable<(string, Func<bool>?)> Tabs(GameState state)
    {
        foreach (var section in ApiTree.All)
            yield return (section.Name, () =>
            {
                Browser.Show(section, state);
                return false;
            });
        yield return ("Globals", () =>
        {
            Raw.Globals();
            return false;
        });
        yield return ("Objects", () =>
        {
            Explorer.Draw(state.AreaId);
            return false;
        });
        yield return ("Address", () =>
        {
            Raw.Address();
            return false;
        });
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
                if (pin.Api is Entity entity && state.MapIcons.Where(icon => icon.EntityId == entity.Id).ToArray() is { Length: > 0 } icons) Browser.Draw($"icons{pin.Id}", icons);
                if (pin.Address != 0)
                {
                    ImGui.Separator();
                    if (Gone(pin)) ImGui.TextDisabled(Memory.Attached ? "gone" : "not attached (--fake or no game)");
                    else
                    {
                        Raw.View.Toolbar();
                        Raw.Node($"raw{pin.Id}", pin.Address, 0);
                    }
                }
            }
            ImGui.End();
            if (!open) Pins.Remove(pin);
        }
    }

    static object Current(object api, GameState state) => api switch
    {
        Entity entity => state.Entities.FirstOrDefault(e => e.Id == entity.Id) is { Id: > 0 } live ? live : entity,
        Player player => state.Player ?? (object)player,
        _ => api,
    };

    void TakeRequests()
    {
        if (Browser.PinRequest is { } api) PinThing(api);
        if (Raw.PinRequest is { } raw) PinThing(raw);
        Browser.PinRequest = null;
        Raw.PinRequest = null;
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
        ImGui.Separator();
    }

    void DrawPick()
    {
        if (ActorsJob.TakeFinished() is { } done) Actors = done.IsCompletedSuccessfully ? done.Result : [];
        if (!Settings.PickActors) Actors = [];
        else if (!ActorsJob.Running && Clock() >= _nextActorScan)
        {
            _nextActorScan = Clock() + 500;
            ActorsJob.Start(Pick.ScanActors);
        }

        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.Begin("##inspector-pick", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);
        ImGuiP.BringWindowToDisplayBack(ImGuiP.GetCurrentWindow());
        var clicked = ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        ImGui.End();

        using var layer = Draw.Layer(DrawLayer.Front);
        if (Game.State.Camera is not { } camera)
        {
            Draw.TextOutlined("no camera", io.MousePos + new Vector2(16f, 16f), Color.White, Color.Black);
            return;
        }
        var candidates = Pick.Candidates(Game.State, Settings.PickEntities, Settings.PickIcons, Actors);
        foreach (var candidate in candidates)
            if (camera.ToScreen(candidate.Position, io.DisplaySize) is { } at) Draw.Circle(at, 3f, Color.FromArgb(180, 255, 220, 80), 1.5f);
        var under = Pick.Under(candidates, camera, io.DisplaySize, io.MousePos, Settings.PickRadius);
        if (under.Length == 0) return;
        Draw.Circle(under[0].At, 14f, Color.Yellow, 2f);
        ImGui.SetTooltip(Text.EscPct(string.Join('\n', under.Select(u => Describe(u.Candidate, Game.State.Player)))));
        if (clicked) PinThing(under[0].Candidate.Target);
    }

    static string Describe(Candidate candidate, Player? player)
    {
        var distance = player is null ? "" : $"  {Vector2.Distance(new Vector2(candidate.Position.X, candidate.Position.Y), new Vector2(player.Position.X, player.Position.Y)):0}u";
        return candidate.Target switch
        {
            Entity e => $"{e.Kind} {e.Id}  cfg {e.CfgId}  {e.Rarity}  0x{e.Address:X}{distance}",
            MapIcon i => $"Icon {i.Texture}  type {i.Type}{distance}",
            ActorSpot a => $"{a.Class}  0x{a.Address:X}{distance}",
            _ => candidate.Label,
        };
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
