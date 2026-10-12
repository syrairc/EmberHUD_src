using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using Aember;
using Aember.Ui;

namespace Minimap;

public sealed class IconStyle
{
    public string Key = "";
    public string Name = "";
    public bool Show = true;
    public string Icon = "";
    public Color Colour = Color.White;
    public float Scale = 1;
    public bool OverrideGameIcon;
}

public sealed class GeneralTab
{
    public ToggleNode Show = new(true);
    public ToggleNode FollowPlayer = new(true);
    [Menu("Zoom", "Pixels per world unit. Real world units are small, about 0.005 to 0.05")] public RangeNode<float> Zoom = new(0.037f, 0.002f, 4);
    [Menu("Rotation", "Extra clockwise degrees added on top of the game's own minimap rotation, 0 matches the game")] public RangeNode<float> Rotation = new(0, -180, 180);
    [Menu("Vertical scale", "Squashes the map vertically after the rotation, the game camera is isometric")] public RangeNode<float> VerticalScale = new(1, 0.2f, 1.5f);
    public RangeNode<float> OffsetX = new(0, -960, 960);
    public RangeNode<float> OffsetY = new(-50, -960, 960);
}

public sealed class RegionLook
{
    [Menu("Region fill", "Fill of the walkable area you haven't explored yet")] public ColorNode Fill = new(Color.FromArgb(0x1F, GameColors.MinimapUnexplored));
    [Menu("Region outline", "Outline around the whole walkable area")] public ColorNode Outline = new(GameColors.MinimapUnexplored);
    [Menu("Region outline width")] public RangeNode<float> OutlineWidth = new(1.5f, 0.5f, 6);
    [Menu("Explored fill", "Fill of the area you've been through")] public ColorNode ExploredFill = new(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    [Menu("Explored outline", "Outline around the explored area and the islands inside it")] public ColorNode ExploredOutline = new(GameColors.MinimapOutline);
    [Menu("Explored outline width")] public RangeNode<float> ExploredOutlineWidth = new(2.5f, 0.5f, 6);
}

public sealed class WallLook
{
    [Menu("Wall colour", "Only used by the Walls draw style")] public ColorNode Colour = new(Color.FromArgb(0xE6, 0xD8, 0xD0, 0x90));
    [Menu("Wall thickness", "Only used by the Walls draw style")] public RangeNode<float> Thickness = new(1.5f, 0.5f, 6);
}

public sealed class PlayerLook
{
    public ColorNode Colour = new(Color.FromArgb(0xFF, 0xFF, 0x40, 0x40));
    public RangeNode<float> Size = new(10, 4, 30);
}

public sealed class MapTab
{
    [Menu("Draw style", "Region fills the walkable area like the game's Tab map - white where you've been, blue where you haven't. Walls draws the old wall lines")] public ChoiceNode DrawStyle = new("Region", "Region", "Walls");
    public RegionLook Region = new();
    public WallLook Walls = new();
    public PlayerLook Player = new();
}

public sealed class IconsTab
{
    [Menu("Game icons", "Draw the game's own minimap icons when GameIcons are set up, shapes otherwise")] public ToggleNode UseGameIcons = new(true);
    [Menu("Game icon scale", "Scales every game icon on top of its own row scale")] public RangeNode<float> GameIconScale = new(1, 0.25f, 4);
    public IconStyle NormalMonsters = Row("Normal monsters", "marker:Dot", 0xEBEBEB, 0.75f);
    public IconStyle MagicMonsters = Row("Magic monsters", "marker:Dot", 0x598CFF, 0.75f);
    public IconStyle RareMonsters = Row("Rare monsters", "svg:elite", 0xFFD933, 1.25f);
    public IconStyle Bosses = Row("Bosses", "svg:elite", 0xD94DFF, 1.75f);
    public IconStyle Minions = Row("Minions", "marker:Dot", 0x4DE64D, 0.75f, false);
    public IconStyle Npcs = Row("NPCs", "marker:Square", 0x4DFF4D, 1.25f);
    public IconStyle OtherPlayers = Row("Other players", "marker:Dot", 0x4DE6FF, 1);
    public IconStyle MapObjects = Row("Map objects", "marker:Diamond", 0xC0C0C0, 0.75f);
    public IconStyle Destructibles = Row("Destructibles", "marker:Triangle", 0xA0703C, 0.75f);
    public List<IconStyle> Found = [];

    Dictionary<string, IconStyle> _found = new(StringComparer.Ordinal);
    List<IconStyle>? _foundFrom;
    int _foundCount;

    static IconStyle Row(string name, string icon, int rgb, float scale, bool show = true) =>
        new() { Name = name, Icon = icon, Colour = Color.FromArgb(0xFF, Color.FromArgb(rgb)), Scale = scale, Show = show };

    public IEnumerable<IconStyle> Fixed => [NormalMonsters, MagicMonsters, RareMonsters, Bosses, Minions, Npcs, OtherPlayers, MapObjects, Destructibles];

    public IconStyle? FixedFor(Entity entity) => entity.Kind switch
    {
        EntityKind.Monster => entity.Rarity switch
        {
            Rarity.Normal => NormalMonsters,
            Rarity.Magic => MagicMonsters,
            Rarity.Rare => RareMonsters,
            _ => Bosses,
        },
        EntityKind.Ally => Minions,
        EntityKind.Player => OtherPlayers,
        EntityKind.Npc => Npcs,
        EntityKind.MapObject => MapObjects,
        EntityKind.Destructible => Destructibles,
        _ => null,
    };

    public IconStyle? FoundFor(string texture)
    {
        if (string.IsNullOrEmpty(texture)) return null;
        if (!ReferenceEquals(_foundFrom, Found) || _foundCount != Found.Count)
        {
            _found.Clear();
            foreach (var row in Found)
                if (row?.Key is { Length: > 0 }) _found.TryAdd(row.Key, row);
            _foundFrom = Found;
            _foundCount = Found.Count;
        }
        return _found.GetValueOrDefault(texture);
    }

    public void Discover(MapIcon[] icons)
    {
        foreach (var icon in icons)
        {
            if (string.IsNullOrEmpty(icon.Texture)) continue;
            if (FoundFor(icon.Texture) is { } existing)
            {
                if (existing.Name == existing.Key && !string.IsNullOrEmpty(icon.Name)) existing.Name = icon.Name;
                continue;
            }
            var row = new IconStyle { Key = icon.Texture, Name = string.IsNullOrEmpty(icon.Name) ? icon.Texture : icon.Name, Icon = Aember.Draw.GameIconPrefix + icon.Texture };
            Found.Add(row);
            _found[row.Key] = row;
            _foundCount = Found.Count;
        }
    }
}

public sealed class Settings : PluginSettings
{
    public GeneralTab General = new();
    public MapTab Map = new();
    public IconsTab Icons = new();

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

    static readonly IconsTab Defaults = new();
    string _filter = "";
    string? _editing;
    string _editText = "";
    bool _focusEdit;

    internal static IEnumerable<IconStyle> Filtered(IEnumerable<IconStyle> rows, string filter) =>
        rows.Where(r => Text.Matches(filter, r.Name) || Text.Matches(filter, r.Key));

    internal static void Rename(IconStyle row, string typed)
    {
        var name = typed.Trim();
        row.Name = name.Length == 0 ? row.Key : name;
    }

    public override void Draw() => Windows.TabBar("minimap",
        ("General", DrawGeneral),
        ("Map", DrawMap),
        ("Icons", DrawIcons));

    bool DrawGeneral()
    {
        if (ImGui.Button("Re-check map rotation")) Game.RescanMinimap();
        return SettingsDrawer.DrawNodes(General);
    }

    internal bool DrawMap()
    {
        var changed = SettingsDrawer.DrawNodes(Map);
        ImGui.SeparatorText("Region");
        changed |= SettingsDrawer.DrawNodes(Map.Region);
        ImGui.SeparatorText("Walls");
        changed |= SettingsDrawer.DrawNodes(Map.Walls);
        ImGui.SeparatorText("Player");
        changed |= SettingsDrawer.DrawNodes(Map.Player);
        return changed;
    }

    internal bool DrawIcons()
    {
        var changed = SettingsDrawer.DrawNodes(Icons);
        ImGui.SeparatorText("Icons");
        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##filter", "search", ref _filter, 64);
        ImGui.SameLine();
        ImGui.TextDisabled($"{Icons.Fixed.Count()} fixed + {Icons.Found.Count} found");
        if (!ImGui.BeginTable("##icons", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit, new Vector2(0, 360))) return changed;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Scale", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Override game icon", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();
        GroupRow("Fixed");
        foreach (var (row, reset, i) in Icons.Fixed.Zip(Defaults.Fixed, (row, def) => (row, def)).Select((p, i) => (p.row, p.def, i)))
            if (Text.Matches(_filter, row.Name) || Text.Matches(_filter, row.Key)) changed |= DrawRow(row, "f" + i, reset);
        GroupRow("Found in game");
        foreach (var row in Filtered(Icons.Found.Where(r => r?.Key is not null), _filter))
            changed |= DrawRow(row, row.Key, null);
        ImGui.EndTable();
        return changed;
    }

    static void GroupRow(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextDisabled(label);
    }

    bool DrawRow(IconStyle row, string id, IconStyle? fixedDefault)
    {
        var fixedRow = fixedDefault is not null;
        var changed = false;
        ImGui.PushID(id);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        changed |= ImGui.Checkbox("##show", ref row.Show);
        ImGui.TableNextColumn();
        changed |= Controls.IconPicker("icon", ref row.Icon, ref row.Colour, ref row.Scale, resetIcon: fixedDefault?.Icon ?? Aember.Draw.GameIconPrefix + row.Key, resetColour: fixedDefault?.Colour, resetScale: fixedDefault?.Scale ?? 1, baseSize: fixedRow ? Main.MarkerRadius * 2 : 24);
        using (Tables.Dim(!row.Show))
        {
            ImGui.TableNextColumn();
            if (fixedRow) ImGui.TextUnformatted(row.Name);
            else changed |= DrawName(row);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{row.Scale:0.00}");
            ImGui.TableNextColumn();
            if (fixedRow)
            {
                changed |= ImGui.Checkbox("##override", ref row.OverrideGameIcon);
                Text.Tip("Draw this row instead of the game's own icon for these entities. Hidden + override hides both");
            }
        }
        ImGui.PopID();
        return changed;
    }

    bool DrawName(IconStyle row)
    {
        if (_editing != row.Key)
        {
            if (ImGui.Selectable(row.Name))
            {
                if (_editing is { } prev && Icons.FoundFor(prev) is { } pending) Rename(pending, _editText);
                _editing = row.Key;
                _editText = row.Name;
                _focusEdit = true;
            }
            Text.Tip(row.Key);
            return false;
        }
        if (_focusEdit)
        {
            ImGui.SetKeyboardFocusHere();
            _focusEdit = false;
        }
        var escaped = ImGui.IsKeyPressed(ImGuiKey.Escape);
        var entered = ImGui.InputText("##name", ref _editText, 64, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
        if (escaped)
        {
            _editing = null;
            return false;
        }
        if (!entered && !ImGui.IsItemDeactivated()) return false;
        Rename(row, _editText);
        _editing = null;
        return true;
    }
}
