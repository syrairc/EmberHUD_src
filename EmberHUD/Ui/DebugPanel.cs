using EmberHUD.Launcher;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Ui;

internal sealed class DebugPanel(string root, Version version, LogBuffer logs)
{
    const long RefreshMilliseconds = 2000;
    const string Popup = "Dump debug information";

    readonly string? _registered = GameInstall.Registered();
    string _text = "";
    long _at;

    string Dump()
    {
        var now = Environment.TickCount64;
        if (_text.Length > 0 && now - _at < RefreshMilliseconds) return _text;
        _at = now;
        var gameFolder = LauncherConfig.Load(Path.Combine(root, "Config", "launcher.json"), _ => { })?.GameDirectory ?? "";
        return _text = DebugInfo.Describe("overlay", version, root, gameFolder, _registered, DebugInfo.IsElevated(), DebugInfo.GameAccess(), Game.State.Status);
    }

    public void DrawButton()
    {
        if (ImGui.Button("Dump debug information")) ImGui.OpenPopup(Popup);
        Text.Tip("Copies your EmberHUD and Torchlight versions, the game folder, and whether each runs as administrator to the clipboard. You see exactly what first.");
        ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(520, 0), new System.Numerics.Vector2(900, 900));
        if (!Layout.ConfirmModal(Popup, $"{DebugInfo.Disclosure}{Environment.NewLine}{Environment.NewLine}{Dump()}{Environment.NewLine}{Environment.NewLine}Copy this to the clipboard?", out var confirmed) || !confirmed) return;
        _text = "";
        ImGui.SetClipboardText(Dump());
    }

    public bool Draw()
    {
        Controls.Section("Troubleshooting info");
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(Dump());
        ImGui.PopTextWrapPos();
        DrawButton();

        Controls.Section("Overlay");
        var state = Game.State;
        ImGui.TextUnformatted($"Status: {state.Status}");
        ImGui.TextUnformatted($"Area id: {state.AreaId}");
        ImGui.TextUnformatted($"Entities read: {state.Entities.Length}");
        ImGui.TextUnformatted($"Player read: {(state.Player is null ? "no" : "yes")}");

        Controls.Section("Warnings and errors");
        Controls.LogView("problems", [.. logs.Snapshot().Where(line => line.StartsWith("Warn") || line.StartsWith("Error"))], 0);
        return false;
    }
}
