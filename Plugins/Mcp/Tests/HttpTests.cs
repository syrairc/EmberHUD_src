using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using EmberHUD.Testing;
using static Mcp.Fixture;

namespace Mcp;

public sealed class HttpTests
{
    const string Token = "tok-for-tests-tok-for-tests-tok-for-tests";
    static readonly Rpc Server = new([Echo], "1.0");

    static Http Open() => Http.Start(Server, Token, FreePort()) ?? throw new InvalidOperationException("no port");

    static HttpResponseMessage Post(Http http, string body, string? token = Token, Action<HttpRequestMessage>? tweak = null, string path = "/mcp", HttpMethod? method = null)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(method ?? HttpMethod.Post, $"http://127.0.0.1:{http.Port}{path}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (token is not null) request.Headers.Add("X-Bridge-Token", token);
        request.Headers.Add("MCP-Protocol-Version", Rpc.Modern);
        request.Headers.Add("Mcp-Method", "tools/list");
        tweak?.Invoke(request);
        return client.Send(request);
    }

    [Test]
    public void AValidRequestIsServed()
    {
        using var http = Open();
        using var reply = Post(http, Wire.Body("tools/list"));
        Check.Equal(HttpStatusCode.OK, reply.StatusCode);
        Check.Equal("application/json", reply.Content.Headers.ContentType!.MediaType);
        var body = (JsonObject)JsonNode.Parse(reply.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        Check.Equal("echo", (string?)body["result"]!["tools"]![0]!["name"]);
        Check.Equal($"http://127.0.0.1:{http.Port}/mcp", http.Url);
    }

    [Test]
    public void BothPathSpellingsWorkAndOtherPathsDoNot()
    {
        using var http = Open();
        Check.Equal(HttpStatusCode.OK, Post(http, Wire.Body("tools/list"), path: "/mcp").StatusCode);
        Check.Equal(HttpStatusCode.OK, Post(http, Wire.Body("tools/list"), path: "/mcp/").StatusCode);
        Check.Equal(HttpStatusCode.NotFound, Post(http, Wire.Body("tools/list"), path: "/other").StatusCode);
    }

    [Test]
    public void TheGateRejectsBeforeTheProtocolRuns()
    {
        using var http = Open();
        Check.Equal(HttpStatusCode.Unauthorized, Post(http, Wire.Body("tools/list"), token: null).StatusCode);
        Check.Equal(HttpStatusCode.Unauthorized, Post(http, Wire.Body("tools/list"), token: "wrong").StatusCode);
        Check.Equal(HttpStatusCode.Forbidden, Post(http, Wire.Body("tools/list"), tweak: request => request.Headers.Add("Origin", "http://evil.example")).StatusCode);
        Check.Equal(HttpStatusCode.OK, Post(http, Wire.Body("tools/list"), tweak: request => request.Headers.Add("Origin", "http://localhost:5173")).StatusCode);
    }

    [Test]
    public void OtherVerbsAreRefusedAndBigBodiesAreCapped()
    {
        using var http = Open();
        using var get = Post(http, "", method: HttpMethod.Get);
        Check.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Check.Equal("POST", string.Join(',', get.Content.Headers.Allow));
        Check.Equal(HttpStatusCode.RequestEntityTooLarge, Post(http, new string('x', Http.MaxBodyChars + 10)).StatusCode);
    }

    [Test]
    public void ABodyAtTheCapIsServedAndOneOverIsRefused()
    {
        using var http = Open();
        var body = Wire.Body("tools/list");
        using var atCap = Post(http, body + new string(' ', Http.MaxBodyChars - body.Length));
        Check.Equal(HttpStatusCode.OK, atCap.StatusCode);
        using var over = Post(http, body + new string(' ', Http.MaxBodyChars - body.Length + 1));
        Check.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
    }

    [Test]
    public void AMultiByteBodyIsReadWhole()
    {
        using var http = Open();
        var text = new string('\u00e9', 20000);
        var call = Wire.Body("tools/call", new JsonObject { ["name"] = "echo", ["arguments"] = new JsonObject { ["text"] = text } }).Replace("\\u00E9", "\u00e9");
        using var reply = Post(http, call, tweak: request =>
        {
            request.Headers.Remove("Mcp-Method");
            request.Headers.Add("Mcp-Method", "tools/call");
            request.Headers.Add("Mcp-Name", "echo");
        });
        Check.Equal(HttpStatusCode.OK, reply.StatusCode);
        var result = (JsonObject)JsonNode.Parse(reply.Content.ReadAsStringAsync().GetAwaiter().GetResult())!;
        Check.Equal(text, (string?)result["result"]!["structuredContent"]!["text"]);
    }

    [Test]
    public void ProtocolErrorsKeepTheirStatusOverTheWire()
    {
        using var http = Open();
        using var reply = Post(http, Wire.Body("tools/list"), tweak: request =>
        {
            request.Headers.Remove("Mcp-Method");
            request.Headers.Add("Mcp-Method", "tools/call");
        });
        Check.Equal(HttpStatusCode.BadRequest, reply.StatusCode);
        Check.Equal(-32020, (int)JsonNode.Parse(reply.Content.ReadAsStringAsync().GetAwaiter().GetResult())!["error"]!["code"]!);
    }

    [Test]
    public void TheNextPortIsUsedWhenTheFirstIsBusy()
    {
        var port = FreePort();
        using var first = Http.Start(Server, Token, port)!;
        using var second = Http.Start(Server, Token, port)!;
        Check.Equal(port, first.Port);
        Check.That(second.Port > port && second.Port < port + 20, "walked forward");
    }

    [Test]
    public void DisposeStopsListeningAndFreesThePort()
    {
        var http = Open();
        var port = http.Port;
        http.Dispose();
        http.Dispose();
        Check.Throws<HttpRequestException>(() => Post(http, Wire.Body("tools/list")));
        using var again = Http.Start(Server, Token, port)!;
        Check.Equal(port, again.Port);
    }

    [Test]
    public void BusyAndIdleAreReported()
    {
        using var http = Open();
        Check.Equal(long.MaxValue, http.MsSinceRequest);
        Post(http, Wire.Body("tools/list")).Dispose();
        for (var waited = 0; http.InFlight != 0 && waited < 1000; waited += 5) System.Threading.Thread.Sleep(5);
        Check.That(http.MsSinceRequest < 5000, "recent");
        Check.Equal(0, http.InFlight);
    }
}
