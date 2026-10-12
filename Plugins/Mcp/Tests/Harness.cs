using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mcp;

internal static class Wire
{
    public static string Body(string method, JsonObject? parameters = null, string version = "2026-07-28")
    {
        var p = parameters ?? new JsonObject();
        p["_meta"] = new JsonObject { ["io.modelcontextprotocol/protocolVersion"] = version };
        return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method, ["params"] = p }.ToJsonString();
    }

    public static JsonObject Json(Reply reply) => (JsonObject)JsonNode.Parse(reply.Body!)!;

    public static JsonObject Run(Tool[] tools, string name, JsonObject? args = null) => tools.Single(tool => tool.Name == name).Run(new Args(args));

    public static string Fails(Tool[] tools, string name, JsonObject? args = null)
    {
        try
        {
            Run(tools, name, args);
        }
        catch (ToolError error)
        {
            return error.Message;
        }
        throw new InvalidOperationException($"{name} did not fail");
    }
}
