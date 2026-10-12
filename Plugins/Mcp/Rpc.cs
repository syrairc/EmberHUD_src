using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aember;

namespace Mcp;

internal sealed record Reply(int Status, string? Body);

internal sealed class Rpc(IReadOnlyList<Tool> tools, string serverVersion)
{
    public const string Modern = "2026-07-28";
    public const string Legacy = "2025-11-25";
    static readonly string[] LegacyVersions = [Legacy, "2025-06-18", "2025-03-26"];
    public const int MaxResultChars = 1048576;
    const string VersionKey = "io.modelcontextprotocol/protocolVersion";
    const string ServerInfoKey = "io.modelcontextprotocol/serverInfo";
    const string Instructions = "Read-only probe of a running Torchlight: Infinite process through Aember. Addresses are hex strings like 0x1F2E3D4C. Start with get_status. get_state and get_entities work on the fake game too, memory and ue tools need a live attach. watch_* samples every frame, record_* writes Game.State frames to disk and recording_* queries them.";
    static readonly string[] Supported = [Modern, Legacy];
    readonly Dictionary<string, Tool> _byName = tools.ToDictionary(tool => tool.Name);

    public Reply Handle(string httpMethod, Func<string, string?> header, string body)
    {
        if (httpMethod != "POST") return new Reply(405, null);
        JsonObject request;
        try
        {
            request = JsonNode.Parse(body) as JsonObject ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return Fail(null, 400, -32700, "Parse error");
        }
        var id = request["id"]?.DeepClone();
        if (id is null) return new Reply(202, null);
        var method = Text(request["method"]);
        if (method is null) return Fail(id, 400, -32600, "Invalid Request");
        var parameters = request["params"] as JsonObject;
        if (method == "initialize") return Ok(id, Initialize(Text(parameters?["protocolVersion"])), false);

        var headerVersion = header("MCP-Protocol-Version");
        var bodyVersion = Text((parameters?["_meta"] as JsonObject)?[VersionKey]);
        if (bodyVersion is not null && bodyVersion != headerVersion) return Mismatch(id, $"MCP-Protocol-Version is {headerVersion ?? "missing"} but the body says {bodyVersion}");
        var version = bodyVersion ?? headerVersion;
        if (version is null) return Mismatch(id, "MCP-Protocol-Version is missing");
        if (!Supported.Contains(version))
            return Fail(id, 400, -32022, "Unsupported protocol version", new JsonObject { ["supported"] = new JsonArray(Supported.Select(supported => (JsonNode)supported).ToArray()), ["requested"] = version });

        var modern = version == Modern;
        if (modern)
        {
            if (header("Mcp-Method") != method) return Mismatch(id, "Mcp-Method does not match the body method");
            if (method == "tools/call" && DecodeName(header("Mcp-Name")) != Text(parameters?["name"])) return Mismatch(id, "Mcp-Name does not match params.name");
        }
        return method switch
        {
            "server/discover" when modern => Ok(id, Discover(), true, true),
            "ping" => Ok(id, new JsonObject(), modern),
            "tools/list" => Ok(id, List(), modern, true),
            "tools/call" => Call(id, parameters, modern),
            _ => Fail(id, 404, -32601, $"Method not found: {method}"),
        };
    }

    Reply Call(JsonNode id, JsonObject? parameters, bool modern)
    {
        var name = Text(parameters?["name"]);
        if (name is null || !_byName.TryGetValue(name, out var tool)) return Fail(id, 200, -32602, $"Unknown tool: {name}");
        if (parameters!["arguments"] is not (null or JsonObject)) return Fail(id, 200, -32602, "arguments must be an object");
        JsonObject value;
        var isError = false;
        try
        {
            value = tool.Run(new Args(parameters["arguments"] as JsonObject));
        }
        catch (ToolError error)
        {
            value = new JsonObject { ["error"] = error.Message };
            isError = true;
        }
        catch (Exception error)
        {
            Log.Error(error, tool.Name);
            value = new JsonObject { ["error"] = $"{tool.Name} failed: {error.Message}" };
            isError = true;
        }
        var text = isError ? (string)value["error"]! : value.ToJsonString();
        if (!isError && text.Length > MaxResultChars)
        {
            text = $"result too large ({text.Length} characters, the cap is {MaxResultChars}) - ask for less";
            isError = true;
        }
        var result = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }), ["isError"] = isError };
        if (!isError) result["structuredContent"] = value;
        return Ok(id, result, modern);
    }

    JsonObject Discover() => new()
    {
        ["supportedVersions"] = new JsonArray(Supported.Select(supported => (JsonNode)supported).ToArray()),
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["_meta"] = new JsonObject { [ServerInfoKey] = Info() },
        ["instructions"] = Instructions,
    };

    JsonObject Initialize(string? requested) => new()
    {
        ["protocolVersion"] = requested is not null && LegacyVersions.Contains(requested) ? requested : Legacy,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
        ["serverInfo"] = Info(),
        ["instructions"] = Instructions,
    };

    JsonObject Info() => new() { ["name"] = "aember", ["version"] = serverVersion };

    JsonObject List() => new()
    {
        ["tools"] = new JsonArray(tools.Select(tool => (JsonNode)new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.Schema.DeepClone(),
            ["annotations"] = new JsonObject { ["readOnlyHint"] = tool.ReadOnly, ["destructiveHint"] = false, ["idempotentHint"] = tool.Idempotent, ["openWorldHint"] = false },
        }).ToArray()),
    };

    static Reply Ok(JsonNode id, JsonObject result, bool modern, bool cacheable = false)
    {
        if (modern)
        {
            result["resultType"] = "complete";
            if (cacheable)
            {
                result["ttlMs"] = 60000;
                result["cacheScope"] = "public";
            }
        }
        return new Reply(200, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString());
    }

    static Reply Fail(JsonNode? id, int status, int code, string message, JsonObject? data = null)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = message };
        if (data is not null) error["data"] = data;
        return new Reply(status, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = error }.ToJsonString());
    }

    static Reply Mismatch(JsonNode id, string message) => Fail(id, 400, -32020, "Header mismatch: " + message);

    static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    static string? DecodeName(string? header)
    {
        if (header is null || !header.StartsWith("=?base64?", StringComparison.Ordinal) || !header.EndsWith("?=", StringComparison.Ordinal)) return header;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(header[9..^2]));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
