using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;

namespace Mcp;

internal static class Wire
{
    public static string Body(string method, JsonObject? parameters = null, string version = Rpc.Modern)
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

internal static class Fixture
{
    public static readonly Tool Echo = new("echo", "Returns its text", Schema.Of(["text"], ("text", "string", "what to echo")), args => new JsonObject { ["text"] = args.Str("text") });

    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string TempPath() => Path.Combine(Path.GetTempPath(), "mcp-test-" + Guid.NewGuid().ToString("N"));

    public static Entity Mob(int id, int cfgId, float x, EntityKind kind = EntityKind.Monster, Rarity rarity = Rarity.Normal, nint address = 0) =>
        new(id, cfgId, new Vector3(x, 0, 0), kind, rarity) { Address = address };

    public static nint Block(int size = 128)
    {
        TestMemory.Clear();
        var at = TestMemory.Alloc(size);
        TestMemory.Write(at, 0);
        return at;
    }
}
