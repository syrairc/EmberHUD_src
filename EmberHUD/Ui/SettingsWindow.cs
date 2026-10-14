using System.Numerics;
using EmberHUD.Host.Plugins;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Ui;

internal enum SettingsPage { General, Plugin }

internal sealed class SettingsWindow(SdkSettings sdk, PluginHost plugins, ThemeStore themes, LogBuffer logs)
{
    internal const float ListWidth = 190f;

    bool _open;
    string _saveAs = "";
    readonly string[] _fontFilters = ["", "", "", ""];
    IReadOnlyList<string>? _fontFiles;
    bool _fontsListed;

    public SettingsPage Page { get; set; }

    public PluginSlot? Selected { get; set; }

    public void Toggle() => _open = !_open;

    public void Draw()
    {
        if (!_open) return;
        ImGui.SetNextWindowSize(new Vector2(860, 560), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("EmberHUD###settings", ref _open, ImGuiWindowFlags.NoCollapse)) Windows.MasterDetail("settings", ListWidth, DrawList, DrawPage);
        ImGui.End();
    }

    void DrawList()
    {
        if (ImGui.Selectable("EmberHUD Settings", Page == SettingsPage.General)) Page = SettingsPage.General;
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
                Windows.TabBar("emberhud", ("General", DrawGeneral), ("Theme", DrawTheme), ("Hotkeys", DrawHotkeys));
                break;
            case SettingsPage.Plugin when Selected is { } slot:
                DrawPlugin(slot);
                break;
        }
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

    bool DrawHotkeys()
    {
        Controls.Section("EmberHUD");
        Controls.Hotkey("Settings window", ref sdk.Hotkeys.Settings, "settings");
        Controls.Hotkey("Quit", ref sdk.Hotkeys.Quit, "quit");
        foreach (var slot in plugins.Slots.Where(slot => slot.Enabled && slot.Initialized && slot.Instance is { Hotkeys.Count: > 0 }).OrderBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase))
        {
            Controls.Section(slot.Name);
            ImGui.PushID(slot.Name);
            Controls.HotkeyTable(slot.Instance!);
            ImGui.PopID();
        }
        return false;
    }

    bool DrawGeneral()
    {
        Controls.Section("Game");
        ImGui.TextUnformatted(Game.State.Status);

        Controls.Section("Plugins");
        ImGui.Checkbox("Reload plugins when their source changes", ref sdk.AutoReload);
        if (ImGui.Button("Clear compile cache")) plugins.ClearCache();

        Controls.Section("Log");
        Controls.LogView("log", logs.Snapshot(), 0);
        return false;
    }

    IEnumerable<(string, string)> FontCandidates()
    {
        _fontsListed = true;
        foreach (var file in _fontFiles ??= themes.FontFiles()) yield return (file, file);
    }

    bool DrawTheme()
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
        if (Controls.PrimaryButton("Save")) themes.SaveAs(themes.Name);
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

        Controls.Section("Fonts");
        ThemeFont[] fonts = [themes.Current.Fonts.Regular, themes.Current.Fonts.Bold, themes.Current.Fonts.Label, themes.Current.Fonts.Mono];
        string[] labels = ["Regular", "Bold", "Label", "Mono"];
        var changed = false;
        _fontsListed = false;
        for (var i = 0; i < fonts.Length; i++)
        {
            ImGui.PushID(i);
            if (Combo.SearchCombo("file", fonts[i].File, FontCandidates(), ref _fontFilters[i], out var file, 260) && file is not null)
            {
                fonts[i] = fonts[i] with { File = file };
                changed = true;
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
        if (!_fontsListed) _fontFiles = null;
        if (changed) themes.UseFonts(new ThemeFonts { Regular = fonts[0], Bold = fonts[1], Label = fonts[2], Mono = fonts[3] });

        Controls.Section("Gradients");
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

        Controls.Section("Style");
        ImGui.ShowStyleEditor();
        return false;
    }
}
