using System.Drawing;
using Hexa.NET.ImGui;
using TorchlightSDK;

namespace Minimap;

public sealed class MarkerStyle
{
    public ToggleNode Show = new(true);
    public ColorNode Colour = new(Color.White);
    public RangeNode<float> Radius = new(3, 1, 20);
}

public sealed class ViewTab
{
    public ToggleNode Show = new(true);
    public HotkeyNode ToggleMap = new(new Hotkey(Key.F10));
    public HotkeyNode ZoomIn = new(new Hotkey(Key.F11));
    public HotkeyNode ZoomOut = new(new Hotkey(Key.F9));
    public ToggleNode FollowPlayer = new(true);
    public RangeNode<float> Zoom = new(0.5f, 0.05f, 4);
    public RangeNode<float> OffsetX = new(0, -960, 960);
    public RangeNode<float> OffsetY = new(0, -960, 960);
    public ColorNode WallColour = new(Color.FromArgb(0xE6, 0xD8, 0xD0, 0x90));
    public RangeNode<float> WallThickness = new(1.5f, 0.5f, 6);
    public ColorNode PlayerColour = new(Color.FromArgb(0xFF, 0xFF, 0x40, 0x40));
    public RangeNode<float> PlayerSize = new(10, 4, 30);
}

public sealed class MarkerTab
{
    public MarkerStyle NormalMonster = new();
    public MarkerStyle MagicMonster = new();
    public MarkerStyle RareMonster = new();
    public MarkerStyle BossMonster = new();
    public MarkerStyle Ally = new();
    public MarkerStyle OtherPlayer = new();
    public MarkerStyle Portal = new();
    public MarkerStyle Npc = new();
    public MarkerStyle Interactive = new();
}

public sealed class Settings : PluginSettings
{
    public ViewTab View = new();
    public MarkerTab Markers = new();

    public MarkerStyle? MarkerFor(Entity entity)
    {
        var style = entity.Kind switch
        {
            EntityKind.Monster => entity.Rarity switch
            {
                Rarity.Normal => Markers.NormalMonster,
                Rarity.Magic => Markers.MagicMonster,
                Rarity.Rare => Markers.RareMonster,
                _ => Markers.BossMonster,
            },
            EntityKind.Ally => Markers.Ally,
            EntityKind.Player => Markers.OtherPlayer,
            EntityKind.Portal => Markers.Portal,
            EntityKind.Npc => Markers.Npc,
            _ => Markers.Interactive,
        };
        return style.Show.Value ? style : null;
    }

    public static MarkerShape ShapeFor(EntityKind kind) => kind switch
    {
        EntityKind.Portal => MarkerShape.Diamond,
        EntityKind.Npc => MarkerShape.Square,
        EntityKind.Interactive => MarkerShape.Triangle,
        _ => MarkerShape.Dot,
    };

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("Map", ImGuiTreeNodeFlags.DefaultOpen)) SettingsDrawer.DrawNodes(View);
        if (!ImGui.CollapsingHeader("Markers", ImGuiTreeNodeFlags.DefaultOpen)) return;
        DrawStyle("Normal monster", Markers.NormalMonster);
        DrawStyle("Magic monster", Markers.MagicMonster);
        DrawStyle("Rare monster", Markers.RareMonster);
        DrawStyle("Boss", Markers.BossMonster);
        DrawStyle("Ally", Markers.Ally);
        DrawStyle("Other player", Markers.OtherPlayer);
        DrawStyle("Portal", Markers.Portal);
        DrawStyle("NPC", Markers.Npc);
        DrawStyle("Interactive", Markers.Interactive);
    }

    static void DrawStyle(string label, MarkerStyle style)
    {
        ImGui.PushID(label);
        ImGui.SeparatorText(label);
        SettingsDrawer.DrawNodes(style);
        ImGui.PopID();
    }
}
