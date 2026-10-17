using System.Numerics;
using EmberHUD.Host.Plugins;
using EmberHUD.Host.Update;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Ui;

internal sealed class SettingsWindow(SdkSettings sdk, PluginHost plugins, ThemeStore themes, LogBuffer logs, UpdateService updates, Action applyAndRestart)
{
    internal const float ListWidth = 190f;
    internal const float ControlWidth = 260f;
    internal const float FontSliderWidth = 140f;

    bool _open;
    string _saveAs = "";
    readonly string[] _fontFilters = ["", "", "", ""];
    IReadOnlyList<string>? _fontFiles;
    IReadOnlyList<string>? _themeNames;

    public PluginSlot? Selected { get; set; }

    public void Toggle()
    {
        _open = !_open;
        _fontFiles = null;
        _themeNames = null;
    }

    public void Draw()
    {
        if (!_open) return;
        plugins.MarkSettingsOpen();
        ImGui.SetNextWindowSize(new Vector2(860, 560), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("EmberHUD###settings", ref _open, ImGuiWindowFlags.NoCollapse)) Layout.MasterDetail("settings", ListWidth, DrawList, DrawPage);
        ImGui.End();
    }

    void DrawList()
    {
        if (ImGui.Selectable("EmberHUD Settings", Selected is null)) Selected = null;
        ImGui.Separator();
        var slots = plugins.Slots;
        for (var i = 0; i < slots.Count; i++)
            if (slots[i].Enabled) DrawRow(slots[i]);
        for (var i = 0; i < slots.Count; i++)
            if (!slots[i].Enabled) DrawRow(slots[i]);
    }

    void DrawRow(PluginSlot slot)
    {
        var errored = slot.ErrorStage is not null;
        var colour = errored ? EColor.U32(EColor.Bad) : ImGui.GetColorU32(slot.Enabled ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        var selected = Selected == slot;
        using (new EColor.StyleColorScope(selected ? [] : [(ImGuiCol.Text, colour)]))
        {
            if (ImGui.Selectable($"{(errored ? "! " : "")}{slot.Name}{(slot.Compiling ? " (compiling)" : "")}###{slot.Name}", selected)) Selected = slot;
        }
    }

    void DrawPage()
    {
        if (Selected is { } slot) DrawPlugin(slot);
        else Layout.TabBar("emberhud", ("General", DrawGeneral), ("Theme", DrawTheme), ("Hotkeys", DrawHotkeys), ("Updater", DrawUpdater));
    }

    void DrawPlugin(PluginSlot slot)
    {
        ImGui.PushID(slot.Name);
        using (Fonts.Use(FontFace.Bold)) ImGui.TextUnformatted(slot.Name);
        ImGui.SameLine();
        ImGui.BeginDisabled(slot.Compiling);
        if (ImGui.Button(slot.ErrorStage is null ? "Reload###reload" : "Retry###reload")) plugins.Reload(slot);
        ImGui.EndDisabled();
        var trail = Layout.BeginTrailing("enable");
        var enabled = slot.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) plugins.SetEnabled(slot, enabled);
        Layout.EndTrailing(trail);

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
        if (Layout.ConfirmModal("reset", $"Reset {slot.Name} to its defaults?", out var confirmed) && confirmed) plugins.ResetSettings(slot);
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
        var slots = plugins.Slots;
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!slot.Enabled || !slot.Initialized || slot.Instance is not { } instance || instance.Hotkeys.Count == 0) continue;
            Controls.Section(slot.Name);
            ImGui.PushID(slot.Name);
            Controls.HotkeyTable(instance);
            ImGui.PopID();
        }
        return false;
    }

    void Wrapped(string text)
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    void DrawUpdateNotice()
    {
        if (updates.Staged is { } staged)
        {
            Wrapped($"Version {staged.Version} is ready - it applies when the overlay is closed");
            if (Controls.PrimaryButton("Apply and Restart")) applyAndRestart();
        }
        else if (updates.Available is { } found) Wrapped($"Version {found.Version} found, downloading...");
        if (updates.LastResult is not { } result) return;
        Wrapped(result.Outcome == "updated" ? $"Updated to v{result.Version}" : $"Update to v{result.Version} failed: {result.Reason}");
        if (result.KeptPlugins.Count > 0) Wrapped($"Kept your modified plugins: {string.Join(", ", result.KeptPlugins)}");
        if (ImGui.Button("Dismiss")) updates.DismissResult();
    }

    bool DrawUpdater()
    {
        Controls.Section("Updates");
        ImGui.Checkbox("Check for updates", ref sdk.UpdaterEnabled);
        ImGui.SetNextItemWidth(ControlWidth);
        ImGui.SliderInt("Check every (hours)", ref sdk.UpdateIntervalHours, 1, 168);
        sdk.UpdateIntervalHours = Math.Clamp(sdk.UpdateIntervalHours, 1, 168);
        ImGui.Checkbox("Overwrite plugins I modified (off = keep them)", ref sdk.OverwriteModifiedPlugins);

        Controls.Section("Status");
        ImGui.TextUnformatted($"Installed: v{updates.Installed}");
        Wrapped(updates.Status);
        ImGui.BeginDisabled(!updates.Enabled);
        if (ImGui.Button("Check now")) Check(false);
        ImGui.SameLine();
        if (ImGui.Button("Force update")) Check(true);
        Text.Tip("Downloads the latest release even if it is the installed version, then applies it like a normal update");
        ImGui.EndDisabled();
        DrawUpdateNotice();
        return false;
    }

    void Check(bool force)
    {
        Task.Run(async () =>
        {
            try
            {
                await updates.CheckOnce(force);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
            }
        });
    }

    bool DrawGeneral()
    {
        DrawUpdateNotice();
        Controls.Section("Game");
        ImGui.TextUnformatted(Game.State.Status);

        Controls.Section("Overlay");
        ImGui.Checkbox("Make HUD interactive when game is out of focus", ref sdk.InteractiveOutOfFocus);

        Controls.Section("Plugins");
        ImGui.Checkbox("Reload plugins when their source changes", ref sdk.AutoReload);
        if (ImGui.Button("Clear compile cache")) plugins.ClearCache();

        Controls.Section("Log");
        Controls.LogView("log", logs.Snapshot(), 0);
        return false;
    }

    IEnumerable<(string, string)> FontCandidates()
    {
        foreach (var file in _fontFiles ??= themes.FontFiles()) yield return (file, file);
    }

    bool DrawTheme()
    {
        ImGui.SetNextItemWidth(ControlWidth);
        if (ImGui.BeginCombo("Theme", themes.Name))
        {
            foreach (var name in _themeNames ??= themes.Names())
                if (ImGui.Selectable(name, name == themes.Name)) themes.Use(name);
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (Controls.PrimaryButton("Save")) themes.SaveAs(themes.Name);
        ImGui.SameLine();
        if (ImGui.Button("Revert")) themes.Use(themes.Name);
        ImGui.SetNextItemWidth(ControlWidth);
        ImGui.InputTextWithHint("##saveas", "New theme name", ref _saveAs, 64);
        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_saveAs) || _saveAs.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0);
        if (ImGui.Button("Save As"))
        {
            themes.SaveAs(_saveAs.Trim());
            _themeNames = null;
            _saveAs = "";
        }
        ImGui.EndDisabled();

        Controls.Section("Fonts");
        ThemeFont[] fonts = [themes.Current.Fonts.Regular, themes.Current.Fonts.Bold, themes.Current.Fonts.Label, themes.Current.Fonts.Mono];
        string[] labels = ["Regular", "Bold", "Label", "Mono"];
        var changed = false;
        for (var i = 0; i < fonts.Length; i++)
        {
            ImGui.PushID(i);
            if (Combo.SearchCombo("file", fonts[i].File, FontCandidates(), ref _fontFilters[i], out var file, ControlWidth) && file is not null)
            {
                fonts[i] = fonts[i] with { File = file };
                changed = true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(FontSliderWidth);
            var size = fonts[i].Size;
            if (ImGui.SliderFloat(labels[i], ref size, 8, 40, "%.0f px"))
            {
                fonts[i] = fonts[i] with { Size = size };
                changed = true;
            }
            ImGui.PopID();
        }
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
