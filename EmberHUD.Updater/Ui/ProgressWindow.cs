using EmberHUD.Host.Overlay;
using EmberHUD.Host.Ui;
using EmberHUD.Setup;
using EmberHUD.Ui;
using Hexa.NET.ImGui;
using Vortice.Mathematics;

namespace EmberHUD.Updater.Ui;

internal static class ProgressWindow
{
    public static unsafe void Run(ProgressState state)
    {
        using var window = AppWindow.Create("EmberHUD Updater", 520, 220, 420, 180);
        using var gd = GraphicsDevice.Create(window.Handle, window.Width, window.Height);
        using var imgui = ImGuiHost.Create(window.Handle, hook => window.WndProcHook = hook, gd);
        var io = ImGui.GetIO();
        io.IniFilename = null;
        io.ConfigErrorRecoveryEnableAssert = false;
        ThemeStore? themes = null;
        try
        {
            themes = new ThemeStore(Path.Combine(AppContext.BaseDirectory, "Themes"), Path.Combine(AppContext.BaseDirectory, "Fonts"), ThemeStore.DefaultTheme, action => action());
            themes.Apply();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            themes = null;
        }
        var closeClicked = false;
        while (window.PumpMessages() && !closeClicked && state.Result is not { Outcome: "updated" })
        {
            if (window.Width <= 0 || window.Height <= 0)
            {
                Thread.Sleep(50);
                continue;
            }
            gd.Resize(window.Width, window.Height);
            imgui.NewFrame(gd.Width, gd.Height);
            closeClicked = Draw(state);
            imgui.Render();
            themes?.Shade();
            gd.Context.ClearRenderTargetView(gd.BackBufferView, new Color4(0f, 0f, 0f, 1f));
            gd.Context.OMSetRenderTargets(gd.BackBufferView);
            imgui.RenderDrawData();
            gd.Present();
        }
    }

    static bool Draw(ProgressState state)
    {
        var close = false;
        ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        if (ImGui.Begin("EmberHUD Updater", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            ImGui.TextUnformatted("Updating EmberHUD");
            var (done, total, file) = state.Snapshot();
            ImGui.ProgressBar(total > 0 ? (float)done / total : 0f, new System.Numerics.Vector2(-1, 0), total > 0 ? $"{done} / {total}" : "");
            ImGui.TextUnformatted(file);
            if (state.PendingPrompt is { } prompt)
            {
                ImGui.Separator();
                ImGui.TextWrapped(prompt);
                if (ImGui.Button("Retry")) state.Answer(true);
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) state.Answer(false);
            }
            else if (state.Result is { Outcome: "failed" } failed)
            {
                ImGui.Separator();
                ImGui.TextColored(EColor.ToVector4(EColor.Bad), $"Update failed: {failed.Reason}");
                close = ImGui.Button("Close");
            }
        }
        ImGui.End();
        return close;
    }
}
