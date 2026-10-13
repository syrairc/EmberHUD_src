using EmberHUD;

namespace Mcp;

public sealed class Settings : PluginSettings
{
    [Menu("Serve MCP", "Starts the local MCP server. Off by default. Read-only, 127.0.0.1 only, needs the token. Toggle off and on to apply a new port")] public ToggleNode Serve = new(false);
    [Menu("Port", "First port to try, walks up to 19 more when it is busy")] public RangeNode<int> Port = new(50902, 1024, 65500);
    [Menu("Custom token", "Empty uses the generated token in Config\\Mcp\\bridge-token.txt")] public TextNode CustomToken = new("");
    [Menu("Max read bytes", "Biggest single read_bytes the server will do")] public RangeNode<int> MaxReadBytes = new(16384, 256, 65536);
    [Menu("Record rate", "Frames per second a recording writes")] public RangeNode<int> RecordHz = new(5, 1, 30);
    [Menu("Status dot", "Small dot in the top left while the server runs - green idle, yellow busy, red when the game state stopped updating")] public ToggleNode ShowStatusDot = new(true);
}
