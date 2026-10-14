using EmberHUD.Launcher;

namespace EmberHUD.Setup.Tests;

internal static class RealGame
{
    public static GameInstall Install()
    {
        var folder = Environment.GetEnvironmentVariable("TLI_GAME_DIR");
        Assert.SkipUnless(folder is not null, "TLI_GAME_DIR is not set");
        return GameInstall.At(folder!) ?? throw new InvalidOperationException($"TLI_GAME_DIR '{folder}' has no {GameInstall.ExeName}");
    }

    public static string IconAssets()
    {
        var folder = Environment.GetEnvironmentVariable("TLI_ICON_ASSETS");
        Assert.SkipUnless(folder is not null, "TLI_ICON_ASSETS is not set");
        return File.Exists(Path.Combine(folder!, "index.json")) ? folder! : throw new InvalidOperationException($"TLI_ICON_ASSETS '{folder}' has no index.json");
    }
}
