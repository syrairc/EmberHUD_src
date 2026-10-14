using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class RpcTests
{
    static readonly Tool Boom = new("boom", "Always fails", Schema.Of([]), _ => throw new ToolError("nope"));
    static readonly Tool Crash = new("crash", "Throws", Schema.Of([]), _ => throw new InvalidOperationException("bad"));
    static readonly Tool Big = new("big", "Huge", Schema.Of([]), _ => new JsonObject { ["text"] = new string('x', Rpc.MaxResultChars + 1) });
    static readonly Tool Mutates = new("mutates", "Changes state", Schema.Of([]), _ => new JsonObject(), ReadOnly: false, Idempotent: false);
    static readonly Rpc Server = new([Echo, Boom, Crash, Big, Mutates], "9.9");

    static Reply Send(string body, params (string Name, string Value)[] headers) =>
        Server.Handle("POST", name => headers.Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value).FirstOrDefault(), body);

    static Reply Modern(string method, JsonObject? parameters = null, string? name = null)
    {
        List<(string, string)> headers = [("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", method)];
        if (name is not null) headers.Add(("Mcp-Name", name));
        return Send(Wire.Body(method, parameters), [.. headers]);
    }

    static JsonObject CallParams(string tool, JsonObject? arguments = null) => new() { ["name"] = tool, ["arguments"] = arguments ?? new JsonObject() };

    static Reply CallTool(string tool, JsonObject? arguments = null) => Modern("tools/call", CallParams(tool, arguments), tool);

    static int ErrorCode(Reply reply) => (int)Wire.Json(reply)["error"]!["code"]!;

    [Test]
    public void DiscoverNamesBothVersionsAndIsCacheable()
    {
        var reply = Modern("server/discover");
        Check.Equal(200, reply.Status);
        var result = Wire.Json(reply)["result"]!.AsObject();
        Check.Equal("complete", (string?)result["resultType"]);
        Check.Equal("2026-07-28", (string?)result["supportedVersions"]![0]);
        Check.Equal("2025-11-25", (string?)result["supportedVersions"]![1]);
        Check.That(result["capabilities"]!["tools"] is JsonObject, "tools capability");
        Check.Equal("emberhud", (string?)result["_meta"]!["io.modelcontextprotocol/serverInfo"]!["name"]);
        Check.Equal("9.9", (string?)result["_meta"]!["io.modelcontextprotocol/serverInfo"]!["version"]);
        Check.That((int)result["ttlMs"]! >= 0, "ttlMs");
        Check.Equal("public", (string?)result["cacheScope"]);
        Check.That(((string?)result["instructions"])!.Length > 20, "instructions");
    }

    [Test]
    public void ToolsListIsOrderedAnnotatedAndCacheable()
    {
        var result = Wire.Json(Modern("tools/list"))["result"]!.AsObject();
        Check.Equal("complete", (string?)result["resultType"]);
        Check.That((int)result["ttlMs"]! >= 0 && (string?)result["cacheScope"] == "public", "cache hints");
        var tools = result["tools"]!.AsArray();
        Check.Equal("echo,boom,crash,big,mutates", string.Join(',', tools.Select(tool => (string?)tool!["name"])));
        Check.Equal("object", (string?)tools[0]!["inputSchema"]!["type"]);
        Check.Equal(true, (bool)tools[0]!["annotations"]!["readOnlyHint"]!);
        Check.Equal(false, (bool)tools[0]!["annotations"]!["destructiveHint"]!);
        Check.Equal(false, (bool)tools[0]!["annotations"]!["openWorldHint"]!);
        Check.Equal(false, (bool)tools[4]!["annotations"]!["readOnlyHint"]!);
        Check.Equal(false, (bool)tools[4]!["annotations"]!["idempotentHint"]!);
    }

    [Test]
    public void CallReturnsTextAndStructuredContent()
    {
        var reply = CallTool("echo", new JsonObject { ["text"] = "hi" });
        Check.Equal(200, reply.Status);
        var result = Wire.Json(reply)["result"]!.AsObject();
        Check.Equal("complete", (string?)result["resultType"]);
        Check.Equal(false, (bool)result["isError"]!);
        Check.Equal("hi", (string?)result["structuredContent"]!["text"]);
        Check.Equal("text", (string?)result["content"]![0]!["type"]);
        Check.Equal("hi", (string?)JsonNode.Parse((string)result["content"]![0]!["text"]!)!["text"]);
    }

    [Test]
    public void ToolFailuresAreResultsNotProtocolErrors()
    {
        var failed = Wire.Json(CallTool("boom"))["result"]!.AsObject();
        Check.Equal(true, (bool)failed["isError"]!);
        Check.Equal("nope", (string?)failed["content"]![0]!["text"]);
        var crashed = Wire.Json(CallTool("crash"))["result"]!.AsObject();
        Check.Equal(true, (bool)crashed["isError"]!);
        Check.That(((string?)crashed["content"]![0]!["text"])!.Contains("bad"), "message kept");
        var missing = Wire.Json(CallTool("echo"))["result"]!.AsObject();
        Check.Equal(true, (bool)missing["isError"]!);
        Check.That(((string?)missing["content"]![0]!["text"])!.Contains("text is required"), "names the argument");
    }

    [Test]
    public void OversizeResultIsAnError()
    {
        var result = Wire.Json(CallTool("big"))["result"]!.AsObject();
        Check.Equal(true, (bool)result["isError"]!);
        Check.That(((string?)result["content"]![0]!["text"])!.Contains("too large"), "says why");
    }

    [Test]
    public void UnknownToolAndBadArgumentsAreInvalidParams()
    {
        var unknown = Modern("tools/call", CallParams("nothing"), "nothing");
        Check.Equal(200, unknown.Status);
        Check.Equal(-32602, ErrorCode(unknown));
        var notObject = Modern("tools/call", new JsonObject { ["name"] = "echo", ["arguments"] = "text" }, "echo");
        Check.Equal(-32602, ErrorCode(notObject));
    }

    [Test]
    public void MismatchedHeadersAreRejected()
    {
        var body = Wire.Body("tools/call", CallParams("echo", new JsonObject { ["text"] = "x" }));
        var wrongMethod = Send(body, ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", "tools/list"), ("Mcp-Name", "echo"));
        Check.Equal(400, wrongMethod.Status);
        Check.Equal(-32020, ErrorCode(wrongMethod));
        var wrongName = Send(body, ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", "tools/call"), ("Mcp-Name", "boom"));
        Check.Equal(-32020, ErrorCode(wrongName));
        var noName = Send(body, ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", "tools/call"));
        Check.Equal(-32020, ErrorCode(noName));
        var noMethod = Send(body, ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Name", "echo"));
        Check.Equal(-32020, ErrorCode(noMethod));
    }

    [Test]
    public void MissingVersionIsAHeaderMismatch()
    {
        var noHeader = Send(Wire.Body("tools/list"), ("Mcp-Method", "tools/list"));
        Check.Equal(400, noHeader.Status);
        Check.Equal(-32020, ErrorCode(noHeader));
        var differs = Send(Wire.Body("tools/list"), ("MCP-Protocol-Version", "2025-11-25"), ("Mcp-Method", "tools/list"));
        Check.Equal(-32020, ErrorCode(differs));
        var bareLegacy = Send(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/list" }.ToJsonString());
        Check.Equal(-32020, ErrorCode(bareLegacy));
    }

    [Test]
    public void Base64NamesAreDecoded()
    {
        var name = "=?base64?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("echo")) + "?=";
        var reply = Send(Wire.Body("tools/call", CallParams("echo", new JsonObject { ["text"] = "x" })), ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", "tools/call"), ("Mcp-Name", name));
        Check.Equal(200, reply.Status);
        Check.Equal(false, (bool)Wire.Json(reply)["result"]!["isError"]!);
        var broken = Send(Wire.Body("tools/call", CallParams("echo")), ("MCP-Protocol-Version", Rpc.Modern), ("Mcp-Method", "tools/call"), ("Mcp-Name", "=?base64?!!!?="));
        Check.Equal(-32020, ErrorCode(broken));
    }

    [Test]
    public void UnknownVersionListsWhatIsSupported()
    {
        var reply = Send(Wire.Body("tools/list", null, "1900-01-01"), ("MCP-Protocol-Version", "1900-01-01"), ("Mcp-Method", "tools/list"));
        Check.Equal(400, reply.Status);
        Check.Equal(-32022, ErrorCode(reply));
        var data = Wire.Json(reply)["error"]!["data"]!;
        Check.Equal("1900-01-01", (string?)data["requested"]);
        Check.Equal("2026-07-28,2025-11-25", string.Join(',', data["supported"]!.AsArray().Select(version => (string?)version)));
    }

    [Test]
    public void UnknownMethodIsNotFound()
    {
        var reply = Modern("resources/list");
        Check.Equal(404, reply.Status);
        Check.Equal(-32601, ErrorCode(reply));
    }

    [Test]
    public void NotificationsGetAcceptedAndOtherVerbsAreRefused()
    {
        var notification = Send(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }.ToJsonString());
        Check.Equal(202, notification.Status);
        Check.That(notification.Body is null, "no body");
        Check.Equal(405, Server.Handle("GET", _ => null, "").Status);
        Check.Equal(405, Server.Handle("DELETE", _ => null, "").Status);
    }

    [Test]
    public void BadJsonIsAParseError()
    {
        var reply = Send("{", ("MCP-Protocol-Version", Rpc.Modern));
        Check.Equal(400, reply.Status);
        Check.Equal(-32700, ErrorCode(reply));
        Check.Equal(-32700, ErrorCode(Send("[1,2]")));
        Check.Equal(-32600, ErrorCode(Send("""{"jsonrpc":"2.0","id":1}""")));
    }

    [Test]
    public void LegacyInitializeAlwaysAnswersTheOneLegacyVersionTheServerAccepts()
    {
        static string Version(string requested) => (string)Wire.Json(Send($$$"""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"{{{requested}}}"}}"""))["result"]!["protocolVersion"]!;
        foreach (var requested in new[] { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" }) Check.Equal("2025-11-25", Version(requested), requested);
        var discovered = Wire.Json(Modern("server/discover"))["result"]!["supportedVersions"]!.AsArray();
        Check.Equal(2, discovered.Count);
    }

    [Test]
    public void EveryVersionInitializeAnswersIsAcceptedOnTheNextCall()
    {
        foreach (var requested in new[] { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" })
        {
            var init = Send($$$"""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"{{{requested}}}"}}""");
            var answered = (string)Wire.Json(init)["result"]!["protocolVersion"]!;
            var next = Send("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", ("MCP-Protocol-Version", answered));
            Check.Equal(200, next.Status, $"asked {requested}, answered {answered}");
        }
    }

    [Test]
    public void PingAnswersEmptyOnBothPaths()
    {
        var modern = Wire.Json(Modern("ping"))["result"]!.AsObject();
        Check.Equal("complete", (string?)modern["resultType"]);
        var legacy = Wire.Json(Send("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", ("MCP-Protocol-Version", "2025-11-25")))["result"]!.AsObject();
        Check.Equal(0, legacy.Count);
    }

    [Test]
    public void LegacyClientCanInitializeListAndCall()
    {
        var init = Send("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""");
        Check.Equal(200, init.Status);
        var result = Wire.Json(init)["result"]!;
        Check.Equal("2025-11-25", (string?)result["protocolVersion"]);
        Check.Equal("emberhud", (string?)result["serverInfo"]!["name"]);
        Check.That(result["capabilities"]!["tools"] is JsonObject, "tools capability");

        var legacy = ("MCP-Protocol-Version", "2025-11-25");
        var list = Send("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", legacy);
        Check.Equal(200, list.Status);
        var listed = Wire.Json(list)["result"]!.AsObject();
        Check.Equal(5, listed["tools"]!.AsArray().Count);
        Check.That(listed["resultType"] is null && listed["ttlMs"] is null, "no 2026 fields in a legacy result");

        var call = Send("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"old"}}}""", legacy);
        var called = Wire.Json(call)["result"]!.AsObject();
        Check.Equal(false, (bool)called["isError"]!);
        Check.Equal("old", (string?)called["structuredContent"]!["text"]);
        Check.That(called["resultType"] is null, "no resultType");

        Check.Equal(404, Send("""{"jsonrpc":"2.0","id":4,"method":"server/discover"}""", legacy).Status);
    }
}
