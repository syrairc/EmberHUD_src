using System.Collections.Generic;
using System.Drawing;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace NameplatesPlugin;

static class SettingsUI
{
    const string Tokens = "{name} {hp} {hpmax} {hp%} {dist} {rarity} {type}";

    internal static bool OpenAllBuckets;

    internal static readonly HashSet<StyleOverride> OpenEditors = [];

    public static void Draw(Settings s)
    {
        if (OpenAllBuckets)
        {
            DrawGeneral(s);
            for (var i = 0; i < s.Buckets.Length; i++) DrawBucket(s, i);
            DrawOverrides(s);
            DrawTextures();
        }
        else if (ImGui.BeginTabBar("##nameplates"))
        {
            if (ImGui.BeginTabItem("General")) { DrawGeneral(s); ImGui.EndTabItem(); }
            for (var i = 0; i < s.Buckets.Length; i++)
            {
                if (!ImGui.BeginTabItem($"{(Bucket)i}##bucket{i}")) continue;
                DrawBucket(s, i);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Overrides")) { DrawOverrides(s); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Textures")) { DrawTextures(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    static void DrawGeneral(Settings s)
    {
        Setting.Slider("Distance unit", ref s.DistanceUnit, 1, 1000, "World units per displayed distance unit, 100 = metres", log: true);
        Setting.Slider("Max distance", ref s.MaxDistance, 1, 10000, "In distance units, plates beyond this distance from the player are not drawn", log: true);
        Setting.Checkbox("Hide behind full-screen panels", ref s.HideBehindPanels);
    }

    static void DrawOverrides(Settings s)
    {
        ListEditor.Compact("overrides", s.Overrides, DrawOverrideRow, AddSource<StyleOverride>.Button(() => NewOverride(s), "+ Add override"), reorderable: true);
        for (var i = 0; i < s.Overrides.Count; i++)
        {
            var row = s.Overrides[i];
            if (!OpenEditors.Contains(row)) continue;
            var id = $"override{i}";
            ImGui.PushID(id);
            ImGui.SeparatorText($"Style for override {i + 1}");
            EditStyle(id, row.Style, s);
            ImGui.PopID();
        }
        OpenEditors.RemoveWhere(row => !s.Overrides.Contains(row));
    }

    static void DrawTextures()
    {
        var folder = BarTextures.DropInFolder;
        ImGui.TextUnformatted("Drop PNG files in:");
        ImGui.InputText("##texturefolder", ref folder, 512, ImGuiInputTextFlags.ReadOnly);
        if (ImGui.Button("Rescan")) BarTextures.Load(BarTextures.DropInFolder);
        ImGui.TextUnformatted(string.Join(", ", BarTextures.Names));
    }

    static void DrawBucket(Settings s, int i)
    {
        var bucket = s.Buckets[i];
        var id = $"bucket{i}";
        ImGui.PushID(id);
        Setting.Checkbox("Show", ref bucket.Show);
        Setting.Checkbox("Damaged only", ref bucket.DamagedOnly, "Only draw the plate once the target has lost health");
        EditStyle(id, bucket.Style, s);
        ImGui.PopID();
    }

    internal static StyleOverride NewOverride(Settings s) => new() { Style = s.Buckets[(int)Bucket.Normal].Style.Clone() };

    static bool DrawOverrideRow(StyleOverride row, int index)
    {
        var changed = ImGui.Checkbox("##enabled", ref row.Enabled);
        ImGui.SameLine();
        changed |= Combo.Enum(ref row.Kind, "kind", 80);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        if (row.Kind == MatchKind.Name)
        {
            row.Name ??= "";
            changed |= ImGui.InputText("##name", ref row.Name, 128);
        }
        else changed |= ImGui.InputInt("##cfgid", ref row.CfgId);
        ImGui.SameLine();
        var open = OpenEditors.Contains(row);
        if (ImGui.SmallButton(open ? "Edit style <" : "Edit style >")) { if (open) OpenEditors.Remove(row); else OpenEditors.Add(row); }
        return changed;
    }

    static void EditStyle(string id, PlateStyle style, Settings s)
    {
        ImGui.PushID(id + "style");
        var bar = style.Bar;
        Setting.Slider("Bar width", ref bar.Width, 10, 400);
        Setting.Slider("Bar height", ref bar.Height, 2, 80);
        Setting.Slider("Border width", ref bar.BorderWidth, 0, 8);
        Setting.Color("Fill", ref bar.Fill);
        Setting.Color("Back", ref bar.Back);
        Setting.Color("Border", ref bar.Border);
        TexturePicker.Combo("Texture", ref bar.Texture, BarTextures.Names, BarTextures.Get, id, bar.Fill);
        Setting.Slider("Offset Y", ref style.OffsetY, -200, 200, "Moves the whole plate up (negative) or down");
        EditLine("Above", style.Above);
        EditLine("On bar", style.OnBar);
        EditLine("Below", style.Below);
        ImGui.PopID();
    }

    static void EditLine(string label, TextLine line)
    {
        ImGui.PushID(label);
        Setting.Checkbox($"{label} text", ref line.Enabled);
        Setting.Input($"{label} template", ref line.Template, $"Tokens: {Tokens}");
        Setting.Style($"{label} style", line.Style);
        Setting.Choice($"{label} align", ref line.Align);
        ImGui.PopID();
    }
}
