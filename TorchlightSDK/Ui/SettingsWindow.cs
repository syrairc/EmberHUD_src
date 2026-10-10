using System.Numerics;
using TorchlightSDK.Host.Plugins;
using TorchlightSDK.Ui;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Host.Ui;

internal enum SettingsPage { General, Theme, Plugin }

internal sealed class SettingsWindow(SdkSettings sdk, PluginHost plugins, ThemeStore themes, LogBuffer logs, Func<string?> buildWarning)
{
    bool _open;
    string _saveAs = "";

    public SettingsPage Page { get; set; }

    public PluginSlot? Selected { get; set; }

    public void Toggle() => _open = !_open;

    public void Draw()
    {
        if (!_open) return;
        ImGui.SetNextWindowSize(new Vector2(860, 560), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("TorchlightSDK###settings", ref _open, ImGuiWindowFlags.NoCollapse)) Windows.MasterDetail("settings", 190, DrawList, DrawPage);
        ImGui.End();
    }

    void DrawList()
    {
        if (ImGui.Selectable("General", Page == SettingsPage.General)) Page = SettingsPage.General;
        if (ImGui.Selectable("Theme", Page == SettingsPage.Theme)) Page = SettingsPage.Theme;
        ImGui.Separator();
        foreach (var slot in plugins.Slots.OrderByDescending(slot => slot.Enabled).ThenBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase))
        {
            var errored = slot.ErrorStage is not null;
            var colour = errored ? EColor.U32(EColor.Bad) : ImGui.GetColorU32(slot.Enabled ? ImGuiCol.Text : ImGuiCol.TextDisabled);
            using (new EColor.StyleColorScope((ImGuiCol.Text, colour)))
            {
                if (ImGui.Selectable($"{(errored ? "! " : "")}{slot.Name}{(slot.Compiling ? " (compiling)" : "")}###{slot.Name}", Page == SettingsPage.Plugin && Selected == slot))
                {
                    Page = SettingsPage.Plugin;
                    Selected = slot;
                }
            }
        }
    }

    void DrawPage()
    {
        switch (Page)
        {
            case SettingsPage.General:
                DrawGeneral();
                break;
            case SettingsPage.Theme:
                DrawTheme();
                break;
            case SettingsPage.Plugin when Selected is { } slot:
                DrawPlugin(slot);
                break;
        }
    }

    void DrawPlugin(PluginSlot slot)
    {
        ImGui.PushID(slot.Name);
        ImGui.TextUnformatted(slot.Name);
        ImGui.SameLine();
        ImGui.BeginDisabled(slot.Compiling);
        if (ImGui.Button(slot.ErrorStage is null ? "Reload###reload" : "Retry###reload")) plugins.Reload(slot);
        ImGui.EndDisabled();
        Layout.BeginTrailing("enable");
        var enabled = slot.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) plugins.SetEnabled(slot, enabled);
        Layout.EndTrailing();

        if (slot.ErrorStage is { } stage)
        {
            using (new EColor.StyleColorScope((ImGuiCol.Text, EColor.U32(EColor.Bad))))
            {
                ImGui.BeginChild("error", Vector2.Zero, ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
                ImGui.PushTextWrapPos(0);
                ImGui.TextUnformatted($"{stage} - {slot.Error}");
                foreach (var line in slot.Diagnostics) ImGui.TextUnformatted(line);
                ImGui.PopTextWrapPos();
                ImGui.EndChild();
            }
        }

        ImGui.BeginDisabled(slot.Instance is null);
        if (ImGui.Button("Write defaults.json")) plugins.WriteDefaults(slot);
        ImGui.SameLine();
        if (ImGui.Button("Reset to defaults")) ImGui.OpenPopup("reset");
        ImGui.EndDisabled();
        if (Windows.ConfirmModal("reset", $"Reset {slot.Name} to its defaults?", out var confirmed) && confirmed) plugins.ResetSettings(slot);
        ImGui.Separator();

        if (ImGui.BeginChild("body")) plugins.DrawSettings(slot);
        ImGui.EndChild();
        ImGui.PopID();
    }

    void DrawGeneral()
    {
        ImGui.SeparatorText("Hotkeys");
        Controls.Hotkey("Settings window", ref sdk.Hotkeys.Settings, "settings");
        Controls.Hotkey("Quit", ref sdk.Hotkeys.Quit, "quit");

        ImGui.SeparatorText("Game");
        ImGui.TextUnformatted(Game.State.Status);
        if (buildWarning() is { } warning) ImGui.TextColored(new Vector4(1, 0.85f, 0.3f, 1), Text.EscPct(warning));

        ImGui.SeparatorText("Plugins");
        ImGui.Checkbox("Reload plugins when their source changes", ref sdk.AutoReload);
        if (ImGui.Button("Clear compile cache")) plugins.ClearCache();

        ImGui.SeparatorText("Log");
        Controls.LogView("log", logs.Snapshot(), 0);
    }

    void DrawTheme()
    {
        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("Theme", themes.Name))
        {
            foreach (var name in themes.Names())
                if (ImGui.Selectable(name, name == themes.Name)) themes.Use(name);
            ImGui.EndCombo();
        }
        sdk.Theme = themes.Name;
        ImGui.SameLine();
        if (ImGui.Button("Save")) themes.Save();
        ImGui.SameLine();
        if (ImGui.Button("Revert")) themes.Use(themes.Name);
        ImGui.SetNextItemWidth(260);
        ImGui.InputTextWithHint("##saveas", "New theme name", ref _saveAs, 64);
        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_saveAs) || _saveAs.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0);
        if (ImGui.Button("Save As"))
        {
            themes.SaveAs(_saveAs.Trim());
            _saveAs = "";
        }
        ImGui.EndDisabled();

        ImGui.SeparatorText("Fonts");
        ThemeFont[] fonts = [themes.Current.Fonts.Regular, themes.Current.Fonts.Mono];
        string[] labels = ["Regular", "Mono"];
        var changed = false;
        for (var i = 0; i < fonts.Length; i++)
        {
            ImGui.PushID(i);
            ImGui.SetNextItemWidth(260);
            if (ImGui.BeginCombo("##file", fonts[i].File))
            {
                foreach (var file in themes.FontFiles())
                {
                    if (!ImGui.Selectable(file, file == fonts[i].File)) continue;
                    fonts[i] = fonts[i] with { File = file };
                    changed = true;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(140);
            var size = fonts[i].Size;
            if (ImGui.SliderFloat(labels[i], ref size, 8, 40, "%.0f px"))
            {
                fonts[i] = fonts[i] with { Size = size };
                changed = true;
            }
            ImGui.PopID();
        }
        if (changed) themes.UseFonts(new ThemeFonts { Regular = fonts[0], Mono = fonts[1] });

        ImGui.SeparatorText("Style");
        ImGui.ShowStyleEditor();
    }
}
