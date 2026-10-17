using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Reactions;

static class SettingsUI
{
    const string BuffTip = "Matched against the buff name and id as a regex, e.g. shield|1234 - if it does not compile it matches the name or id exactly";

    static int selected;
    static PickerState<Buff>? buffPicker;

    static readonly PickerOpts<Buff> BuffPickerOpts = new()
    {
        Primary = buff => buff.Name ?? "(no name)",
        Secondary = buff => $"id {buff.Id}",
        Key = buff => buff.Id.ToString(),
        EmptyText = "no buffs on the player right now.",
        Size = new Vector2(360, 300),
    };

    public static void Draw(Settings s)
    {
        s.Normalize();
        Controls.Section("Reactions");
        Setting.Slider("Global cooldown (ms)", ref s.GlobalCooldownMs, 0, 2000, "Minimum gap between any two reactions firing");
        selected = Math.Clamp(selected, 0, Math.Max(0, s.Reactions.Count - 1));
        Layout.MasterDetail("reactions", 260, () => DrawList(s), () => DrawEditor(s));
    }

    internal static void Select(int index) => selected = index;

    internal static int AddReaction(Settings s)
    {
        s.Reactions.Add(new Reaction { Name = $"Reaction {s.Reactions.Count + 1}" });
        return s.Reactions.Count - 1;
    }

    internal static void DeleteReaction(Settings s, ref int index)
    {
        if (index >= 0 && index < s.Reactions.Count) s.Reactions.RemoveAt(index);
        index = Math.Clamp(index, 0, Math.Max(0, s.Reactions.Count - 1));
    }

    internal static Condition NewCondition() => new();

    internal static string AddExactId(string text, int id) => string.IsNullOrEmpty(text) ? $"^{id}$" : $"{text}|^{id}$";

    internal static bool SkillMissing(int cfgId) => Conditions.FindSkill(Game.State, cfgId) is null;

    static void DrawList(Settings s)
    {
        if (ImGui.Button("+ Add")) selected = AddReaction(s);
        ImGui.SameLine();
        if (ImGui.Button("Delete")) DeleteReaction(s, ref selected);
        ListEditor.ReorderableList("list", s.Reactions, ref selected, (reaction, _) =>
        {
            ImGui.Checkbox("##enabled", ref reaction.Enabled);
            ImGui.SameLine();
            ImGui.TextUnformatted(reaction.Name.Length == 0 ? "(unnamed)" : reaction.Name);
        }, new ListOpts { ShowIndex = false });
    }

    static void DrawEditor(Settings s)
    {
        if (s.Reactions.Count == 0)
        {
            ImGui.TextDisabled("No reactions yet - use + Add on the left");
            return;
        }
        var reaction = s.Reactions[selected];
        Setting.Input("Name", ref reaction.Name);
        Setting.Hotkey("Key", ref reaction.Trigger, "The key pressed in game when every condition holds - modifiers are ignored");
        Setting.Slider("Cooldown (ms)", ref reaction.CooldownMs, 0, 10000, "Minimum gap before this reaction can fire again");
        ImGui.SeparatorText("Conditions - all must hold, none means it never fires");
        ListEditor.Compact($"conditions{selected}", reaction.Conditions, DrawCondition, AddSource<Condition>.Button(NewCondition, "+ Add condition"));
    }

    static bool DrawCondition(Condition condition, int index)
    {
        if (index > 0) ImGui.SeparatorText("AND");
        var changed = Combo.Enum(ref condition.Kind, "kind", 150);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(170);
        switch (condition.Kind)
        {
            case ConditionKind.Health or ConditionKind.Shield or ConditionKind.HealthAndShield:
                changed |= ImGui.SliderFloat("##percent", ref condition.Percent, 1, 100, "below %.0f%%");
                break;
            case ConditionKind.Buff:
                var missing = condition.Missing ? 1 : 0;
                if (Combo.Option(ref missing, ["Present", "Missing"], "presence", 90)) { condition.Missing = missing == 1; changed = true; }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(170);
                changed |= ImGui.InputText("##text", ref condition.Text, 128);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(BuffTip);
                ImGui.SameLine();
                Game.WantCharacter();
                if (Picker.Button("Current buffs", Game.State.Buffs, ref buffPicker, out var buff, BuffPickerOpts, "buffs"))
                {
                    condition.Text = AddExactId(condition.Text, buff.Id);
                    changed = true;
                }
                break;
            case ConditionKind.SkillCooldown:
                changed |= SkillPicker(condition);
                ImGui.SameLine();
                var notReady = condition.Ready ? 0 : 1;
                if (Combo.Option(ref notReady, ["Ready", "Not ready"], "ready", 100)) { condition.Ready = notReady == 0; changed = true; }
                break;
            case ConditionKind.SkillCharges:
                changed |= SkillPicker(condition);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(140);
                changed |= ImGui.SliderInt("##charges", ref condition.Charges, 1, 20, "at least %d");
                break;
            case ConditionKind.NearbyEnemies:
                changed |= ImGui.SliderInt("##count", ref condition.Count, 1, 50, "at least %d");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(170);
                changed |= ImGui.SliderFloat("##radius", ref condition.Radius, 50, 2000, "within %.0f");
                ImGui.SameLine();
                changed |= Combo.Enum(ref condition.MinRarity, "rarity", 90);
                break;
        }
        if (condition.Kind is ConditionKind.SkillCooldown or ConditionKind.SkillCharges && SkillMissing(condition.SkillCfgId))
            ImGui.TextColored(new Vector4(0.86f, 0.31f, 0.31f, 1), $"Skill cfg {condition.SkillCfgId} is not on the bar - this condition is never met");
        return changed;
    }

    static bool SkillPicker(Condition condition)
    {
        Game.WantCharacter();
        var skills = Game.State.Skills ?? [];
        var labels = skills.Select(skill => $"Slot {skill.Slot} - {skill.CfgId}").ToList();
        var index = Array.FindIndex(skills, skill => skill.CfgId == condition.SkillCfgId);
        if (index < 0)
        {
            labels.Add($"cfg {condition.SkillCfgId} (not on bar)");
            index = labels.Count - 1;
        }
        ImGui.SetNextItemWidth(170);
        if (!Combo.Option(ref index, [.. labels], "skill", 170) || index >= skills.Length) return false;
        condition.SkillCfgId = skills[index].CfgId;
        return true;
    }
}
