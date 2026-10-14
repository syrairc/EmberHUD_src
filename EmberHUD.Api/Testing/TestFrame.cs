using System.Runtime.ExceptionServices;
using Hexa.NET.ImGui;

namespace EmberHUD.Testing;

public static class TestFrame
{
    static readonly Lock Gate = new();
    static ImGuiContextPtr _context;

    public static void Run(Action draw)
    {
        lock (Gate)
        {
            if (_context.IsNull) _context = CreateContext();
            ImGui.SetCurrentContext(_context);
            ImGui.GetIO().DeltaTime = 1f / 60;
            ImGui.NewFrame();
            var before = default(ImGuiErrorRecoveryState);
            ImGuiP.ErrorRecoveryStoreState(ref before);
            ExceptionDispatchInfo? failure = null;
            try
            {
                draw();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            var after = default(ImGuiErrorRecoveryState);
            ImGuiP.ErrorRecoveryStoreState(ref after);
            ImGuiP.ErrorRecoveryTryToRecoverState(ref before);
            ImGui.Render();
            var textures = ImGui.GetPlatformIO().Textures;
            for (var i = 0; i < textures.Size; i++)
            {
                var texture = textures[i];
                if (texture.Status is ImTextureStatus.WantCreate or ImTextureStatus.WantUpdates)
                {
                    texture.SetTexID(new ImTextureID(1ul));
                    texture.SetStatus(ImTextureStatus.Ok);
                }
                else if (texture.Status == ImTextureStatus.WantDestroy)
                {
                    texture.SetTexID(new ImTextureID(0ul));
                    texture.SetStatus(ImTextureStatus.Destroyed);
                }
            }
            failure?.Throw();
            var leaked = typeof(ImGuiErrorRecoveryState).GetFields().Where(field => !Equals(field.GetValue(before), field.GetValue(after))).Select(field => $"{field.Name} {field.GetValue(before)} -> {field.GetValue(after)}").ToArray();
            if (leaked.Length > 0) throw new InvalidOperationException($"the frame left ImGui stacks open - {string.Join(", ", leaked)}");
        }
    }

    public static int Vertices(Action draw, bool inWindow = false)
    {
        var vertices = 0;
        Run(() =>
        {
            if (inWindow) ImGui.Begin("vertex probe");
            var list = inWindow ? ImGui.GetWindowDrawList() : ImGui.GetBackgroundDrawList();
            var before = list.VtxBuffer.Size;
            draw();
            vertices = list.VtxBuffer.Size - before;
            if (inWindow) ImGui.End();
        });
        return vertices;
    }

    static unsafe ImGuiContextPtr CreateContext()
    {
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        var io = ImGui.GetIO();
        io.IniFilename = null;
        io.DisplaySize = new System.Numerics.Vector2(1920, 1080);
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures | ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigErrorRecoveryEnableAssert = false;
        io.ConfigErrorRecoveryEnableDebugLog = false;
        io.Fonts.AddFontDefault();
        return context;
    }
}
