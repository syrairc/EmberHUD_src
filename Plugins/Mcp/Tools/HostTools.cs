using System;
using System.Text.Json.Nodes;

namespace Mcp;

internal static class HostTools
{
    public static Tool[] Create(Action requestShutdown) =>
    [
        new("shutdown", "Ask EmberHUD to quit, the same clean exit as the Quit hotkey (held keys released, settings saved, server stopped). The reply comes back first, the overlay closes about half a second later and this server goes away with it - the user (or you, with their OK) starts it again, e.g. to test a rebuild. Nothing here launches the game or the overlay.",
            Schema.Of([]), _ =>
            {
                requestShutdown();
                return new JsonObject { ["quitting"] = true };
            }, ReadOnly: false, Idempotent: false),
    ];
}
