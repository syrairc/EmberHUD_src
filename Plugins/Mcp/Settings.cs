using EmberHUD;
using EmberHUD.Ui;

namespace Mcp;

public sealed class Settings : PluginSettings
{
    public bool Serve;
    public int Port = 50902;
    public string CustomToken = "";
    public int MaxReadBytes = 16384;
    public int RecordHz = 5;
    public bool ShowStatusDot = true;

    public override void Draw()
    {
        Setting.Checkbox("Serve MCP", ref Serve, "Starts the local MCP server. Off by default. Read-only, 127.0.0.1 only, needs the token. Toggle off and on to apply a new port");
        Setting.Slider("Port", ref Port, 1024, 65500, "First port to try, walks up to 19 more when it is busy");
        Setting.Input("Custom token", ref CustomToken, "Empty uses the generated token in Config\\Mcp\\bridge-token.txt");
        Setting.Slider("Max read bytes", ref MaxReadBytes, 256, 65536, "Biggest single read_bytes the server will do");
        Setting.Slider("Record rate", ref RecordHz, 1, 30, "Frames per second a recording writes");
        Setting.Checkbox("Status dot", ref ShowStatusDot, "Small dot in the top left while the server runs - green idle, yellow busy, red when the game state stopped updating");
    }
}
