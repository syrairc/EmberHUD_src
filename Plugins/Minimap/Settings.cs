using System.Drawing;
using Hexa.NET.ImGui;
using Aember;

namespace Minimap;

public sealed class MarkerStyle
{
    public ToggleNode Show = new(true);
    public ColorNode Colour = new(Color.White);
    public RangeNode<float> Radius = new(3, 1, 20);
    [Menu("Accent", "Second colour of the SVG icon, only portal, interactive, rare, boss, altar and elevator markers use it")] public ColorNode Accent = new(Color.FromArgb(0x1A, 0x1A, 0x1A));
}

public sealed class ViewTab
{
    public ToggleNode Show = new(true);
    public ToggleNode FollowPlayer = new(true);
    [Menu("Zoom", "Pixels per world unit. Real world units are small, about 0.005 to 0.05")] public RangeNode<float> Zoom = new(0.037f, 0.002f, 4);
    [Menu("Rotation", "Extra clockwise degrees added on top of the game's own minimap rotation, 0 matches the game")] public RangeNode<float> Rotation = new(0, -180, 180);
    [Menu("Vertical scale", "Squashes the map vertically after the rotation, the game camera is isometric")] public RangeNode<float> VerticalScale = new(1, 0.2f, 1.5f);
    public RangeNode<float> OffsetX = new(0, -960, 960);
    public RangeNode<float> OffsetY = new(-50, -960, 960);
    [Menu("Draw style", "Region fills the walkable area like the game's Tab map - white where you've been, blue where you haven't. Walls draws the old wall lines")] public ChoiceNode DrawStyle = new("Region", "Region", "Walls");
    [Menu("Region fill", "Fill of the walkable area you haven't explored yet")] public ColorNode RegionFill = new(Color.FromArgb(0x1F, GameColors.MinimapUnexplored));
    [Menu("Region outline", "Outline around the whole walkable area")] public ColorNode RegionOutline = new(GameColors.MinimapUnexplored);
    [Menu("Region outline width")] public RangeNode<float> RegionOutlineWidth = new(1.5f, 0.5f, 6);
    [Menu("Explored fill", "Fill of the area you've been through")] public ColorNode ExploredFill = new(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    [Menu("Explored outline", "Outline around the explored area and the islands inside it")] public ColorNode ExploredOutline = new(GameColors.MinimapOutline);
    [Menu("Explored outline width")] public RangeNode<float> ExploredOutlineWidth = new(2.5f, 0.5f, 6);
    [Menu("Wall colour", "Only used by the Walls draw style")] public ColorNode WallColour = new(Color.FromArgb(0xE6, 0xD8, 0xD0, 0x90));
    [Menu("Wall thickness", "Only used by the Walls draw style")] public RangeNode<float> WallThickness = new(1.5f, 0.5f, 6);
    public ColorNode PlayerColour = new(Color.FromArgb(0xFF, 0xFF, 0x40, 0x40));
    public RangeNode<float> PlayerSize = new(10, 4, 30);
    [Menu("Game icons", "Draw the game's own minimap icons when GameIconsDirectory is set in Config\\sdk.json, drawn shapes otherwise")] public ToggleNode UseGameIcons = new(true);
    [Menu("Icon size", "On-screen size in pixels of a game icon at the game's default size multiplier")] public RangeNode<float> IconSize = new(24, 8, 64);
    public ColorNode IconTint = new(Color.White);
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
    public MarkerStyle Coffin = new();
    public MarkerStyle Altar = new();
    public MarkerStyle Elevator = new();
    public MarkerStyle MapObject = new();
    public MarkerStyle Destructible = new();
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
            EntityKind.Coffin => Markers.Coffin,
            EntityKind.Altar => Markers.Altar,
            EntityKind.Elevator => Markers.Elevator,
            EntityKind.MapObject => Markers.MapObject,
            EntityKind.Destructible => Markers.Destructible,
            _ => Markers.Interactive,
        };
        return style.Show.Value ? style : null;
    }

    public static string IconFor(Entity entity) => entity switch
    {
        { Kind: EntityKind.Portal } => "svg:portal",
        { Kind: EntityKind.Interactive } => "svg:gem",
        { Kind: EntityKind.Monster, Rarity: Rarity.Rare or Rarity.Boss } => "svg:elite",
        { Kind: EntityKind.Npc } => "marker:Square",
        { Kind: EntityKind.Coffin } => "game:UI_MiniMap_S15guancai",
        { Kind: EntityKind.Altar } => "svg:quest",
        { Kind: EntityKind.Elevator } => "svg:portal",
        { Kind: EntityKind.MapObject } => "marker:Diamond",
        { Kind: EntityKind.Destructible } => "marker:Triangle",
        _ => "marker:Dot",
    };

    public static MarkerShape FallbackShape(int type) => type switch
    {
        2 or (>= 1040 and <= 1060) => MarkerShape.Star,
        10 or 11 or 12 or 14 or 32 or 33 or 1000 or 1022 => MarkerShape.Diamond,
        7 or 8 or 9 or 1010 or 2001 or 2002 => MarkerShape.Hexagon,
        3 or 1006 or 1007 or 1009 or 1011 => MarkerShape.Square,
        (>= 15 and <= 24) or 29 => MarkerShape.Triangle,
        1003 or 1008 or 1023 or 1024 => MarkerShape.Sparkle,
        _ => MarkerShape.Dot,
    };

    public override void Draw()
    {
        if (ImGui.Button("Re-check map rotation")) Game.RescanMinimap();
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
        DrawStyle("Vigil coffin", Markers.Coffin);
        DrawStyle("Season altar", Markers.Altar);
        DrawStyle("Elevator", Markers.Elevator);
        DrawStyle("Map object", Markers.MapObject);
        DrawStyle("Destructible", Markers.Destructible);
    }

    static void DrawStyle(string label, MarkerStyle style)
    {
        ImGui.PushID(label);
        ImGui.SeparatorText(label);
        SettingsDrawer.DrawNodes(style);
        ImGui.PopID();
    }
}
