using System.Numerics;
using Aember.Host.Plugins;
using Aember.Ui;
using Hexa.NET.ImGui;

namespace Aember.Host.Ui;

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
        if (ImGui.Begin("Aember###settings", ref _open, ImGuiWindowFlags.NoCollapse)) Windows.MasterDetail("settings", 190, DrawList, DrawPage);
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
            var selected = Page == SettingsPage.Plugin && Selected == slot;
            using (new EColor.StyleColorScope(selected ? [] : [(ImGuiCol.Text, colour)]))
            {
                if (ImGui.Selectable($"{(errored ? "! " : "")}{slot.Name}{(slot.Compiling ? " (compiling)" : "")}###{slot.Name}", selected))
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

    static void Section(string text)
    {
        using (Fonts.UseBold()) ImGui.SeparatorText(text);
    }

    void DrawPlugin(PluginSlot slot)
    {
        ImGui.PushID(slot.Name);
        using (Fonts.UseBold()) ImGui.TextUnformatted(slot.Name);
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
        Section("Hotkeys");
        Controls.Hotkey("Settings window", ref sdk.Hotkeys.Settings, "settings");
        Controls.Hotkey("Quit", ref sdk.Hotkeys.Quit, "quit");

        Section("Game");
        ImGui.TextUnformatted(Game.State.Status);
        if (buildWarning() is { } warning) ImGui.TextColored(new Vector4(1, 0.85f, 0.3f, 1), Text.EscPct(warning));

        Section("Plugins");
        ImGui.Checkbox("Reload plugins when their source changes", ref sdk.AutoReload);
        if (ImGui.Button("Clear compile cache")) plugins.ClearCache();

        Section("Log");
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
        if (Controls.PrimaryButton("Save")) themes.Save();
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

        Section("Fonts");
        ThemeFont[] fonts = [themes.Current.Fonts.Regular, themes.Current.Fonts.Bold, themes.Current.Fonts.Label, themes.Current.Fonts.Mono];
        string[] labels = ["Regular", "Bold", "Label", "Mono"];
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
        if (changed) themes.UseFonts(new ThemeFonts { Regular = fonts[0], Bold = fonts[1], Label = fonts[2], Mono = fonts[3] });

        Section("Gradients");
        Dictionary<string, ThemeGradient>? edited = null;
        foreach (var (key, gradient) in themes.Current.Gradients)
        {
            ImGui.PushID(key);
            var top = gradient.Top;
            var bottom = gradient.Bottom;
            var highlight = gradient.Highlight;
            var text = gradient.Text;
            var touched = Controls.Color("##top", ref top, "top");
            ImGui.SameLine();
            touched |= Controls.Color("##bottom", ref bottom, "bottom");
            if (highlight is { } h)
            {
                ImGui.SameLine();
                touched |= Controls.Color("##highlight", ref h, "highlight");
                highlight = h;
            }
            if (text is { } t)
            {
                ImGui.SameLine();
                touched |= Controls.Color("##text", ref t, "text");
                text = t;
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(key);
            if (touched) (edited ??= new Dictionary<string, ThemeGradient>(themes.Current.Gradients))[key] = new ThemeGradient(top, bottom, highlight, text);
            ImGui.PopID();
        }
        if (edited is not null) themes.UseGradients(edited);

        Section("Style");
        ImGui.ShowStyleEditor();
    }
}
