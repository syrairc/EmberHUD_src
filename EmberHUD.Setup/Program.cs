using System.Diagnostics;
using EmberHUD.Host.Overlay;
using EmberHUD.Host.Ui;
using Hexa.NET.ImGui;
using Vortice.Mathematics;

namespace EmberHUD.Setup;

internal static class Program
{
    [STAThread]
    static unsafe int Main()
    {
        var root = AppContext.BaseDirectory;
        Directory.CreateDirectory(Path.Combine(root, "Logs"));
        using var logFile = new StreamWriter(new FileStream(Path.Combine(root, "Logs", "setup.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        var logGate = new Lock();
        Log.Sink = (level, plugin, message) =>
        {
            lock (logGate) logFile.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {level} {message}");
        };
        try
        {
            using var window = AppWindow.Create("EmberHUD Setup", 820, 760);
            using var gd = GraphicsDevice.Create(window.Handle, window.Width, window.Height);
            using var imgui = ImGuiHost.Create(window.Handle, hook => window.WndProcHook = hook, gd);
            var io = ImGui.GetIO();
            io.IniFilename = null;
            io.ConfigErrorRecoveryEnableAssert = false;
            var themes = new ThemeStore(Path.Combine(root, "Themes"), Path.Combine(root, "Fonts"), "Torchlight", action => action());
            themes.Apply();
            var page = new SetupPage(root, LocalUsers.List(), start => FileDialog.PickFolder(window.Handle, start), start => FileDialog.PickProgram(window.Handle, start), Process.Start);
            while (window.PumpMessages() && !page.CloseRequested)
            {
                if (window.Width <= 0 || window.Height <= 0)
                {
                    Thread.Sleep(50);
                    continue;
                }
                gd.Resize(window.Width, window.Height);
                imgui.NewFrame(gd.Width, gd.Height);
                page.Draw();
                imgui.Render();
                themes.Shade();
                gd.Context.ClearRenderTargetView(gd.BackBufferView, new Color4(0f, 0f, 0f, 1f));
                gd.Context.OMSetRenderTargets(gd.BackBufferView);
                imgui.RenderDrawData();
                gd.Present();
            }
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "setup");
            Win32.MessageBox(0, $"{ex.Message}{Environment.NewLine}see Logs\\setup.log", "EmberHUD Setup", 0x10);
            return 1;
        }
    }
}
