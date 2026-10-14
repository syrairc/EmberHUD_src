using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;

namespace Mcp;

public sealed class MainTests
{
    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static string Temp() => Path.Combine(Path.GetTempPath(), "mcp-test-" + Guid.NewGuid().ToString("N"));

    static void Clean(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    static HttpListener[] BlockTwenty(Main main)
    {
        while (true)
        {
            var port = FreePort();
            if (port > 65480) continue;
            var blockers = new List<HttpListener>();
            try
            {
                for (var i = 0; i < 20; i++)
                {
                    var blocker = new HttpListener();
                    blocker.Prefixes.Add($"http://127.0.0.1:{port + i}/mcp/");
                    blocker.Start();
                    blockers.Add(blocker);
                }
                main.Settings.Port = port;
                return [.. blockers];
            }
            catch (HttpListenerException)
            {
                foreach (var blocker in blockers) blocker.Close();
            }
        }
    }

    static Main Plugin(string directory, long[]? clock = null)
    {
        var main = new Main { DataDirectory = directory };
        if (clock is not null) main.Clock = () => clock[0];
        main.Settings.Port = FreePort();
        return main;
    }

    static HttpStatusCode Call(Main main, string token)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, main.Server!.Url) { Content = new StringContent(Wire.Body("tools/list"), Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Bridge-Token", token);
        request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
        request.Headers.Add("Mcp-Method", "tools/list");
        return client.Send(request).StatusCode;
    }

    [Test]
    public void ServesOnlyWhileToggledOn()
    {
        var directory = Temp();
        var main = Plugin(directory);
        try
        {
            main.Tick();
            Check.That(main.Server is null, "off by default");
            main.Settings.Serve = true;
            main.Tick();
            Check.That(main.Server is not null, "started");
            var token = File.ReadAllText(Path.Combine(directory, "bridge-token.txt")).Trim();
            Check.Equal(HttpStatusCode.OK, Call(main, token));
            Check.Equal(main.Server!.Port.ToString(), File.ReadAllText(Path.Combine(directory, "bridge-port.txt")).Trim());
            main.Settings.Serve = false;
            main.Tick();
            Check.That(main.Server is null, "stopped");
        }
        finally
        {
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void ReloadFreesThePort()
    {
        var directory = Temp();
        var first = Plugin(directory);
        var second = Plugin(directory);
        try
        {
            first.Settings.Serve = true;
            first.Tick();
            var port = first.Server!.Port;
            first.Dispose();
            second.Settings.Port = port;
            second.Settings.Serve = true;
            second.Tick();
            Check.Equal(port, second.Server!.Port);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void AFailedStartIsNotRetriedEveryTick()
    {
        var directory = Temp();
        var main = Plugin(directory);
        main.Settings.Serve = true;
        var blockers = BlockTwenty(main);
        try
        {
            main.Tick();
            Check.That(main.Server is null && main.StartFailed, "failed once");
            blockers[0].Stop();
            main.Tick();
            Check.That(main.Server is null, "no retry while Serve stays on");
            main.Settings.Serve = false;
            main.Tick();
            main.Settings.Serve = true;
            main.Tick();
            Check.That(main.Server is not null, "toggling retries");
        }
        finally
        {
            foreach (var blocker in blockers) blocker?.Close();
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void StatusTracksWhenTheStateLastChanged()
    {
        var clock = new long[] { 1000 };
        var directory = Temp();
        var main = Plugin(directory, clock);
        try
        {
            TestGame.Set(new GameState { Status = "a", AreaId = 1 });
            main.Tick();
            clock[0] = 4000;
            main.Tick();
            Check.Equal(3000L, main.CurrentStatus().MsSinceStateChange);
            TestGame.Set(new GameState { Status = "b", AreaId = 1 });
            main.Tick();
            Check.Equal(0L, main.CurrentStatus().MsSinceStateChange);
        }
        finally
        {
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void AFreshStateRecordWithTheSameContentDoesNotResetTheTimer()
    {
        var clock = new long[] { 1000 };
        var directory = Temp();
        var main = Plugin(directory, clock);
        try
        {
            Entity[] entities = [];
            TestGame.Set(new GameState { Status = "a", AreaId = 1, Entities = entities });
            main.Tick();
            clock[0] = 4000;
            TestGame.Set(new GameState { Status = "a", AreaId = 1, Entities = entities });
            main.Tick();
            Check.Equal(3000L, main.CurrentStatus().MsSinceStateChange);
            TestGame.Set(new GameState { Status = "a", AreaId = 2, Entities = entities });
            main.Tick();
            Check.Equal(0L, main.CurrentStatus().MsSinceStateChange);
            clock[0] = 5000;
            TestGame.Set(new GameState { Status = "a", AreaId = 2, Entities = new Entity[1] });
            main.Tick();
            Check.Equal(0L, main.CurrentStatus().MsSinceStateChange);
        }
        finally
        {
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void TurningServeOffStopsWatchesAndTheRecording()
    {
        var directory = Temp();
        var main = Plugin(directory);
        main.Settings.Serve = true;
        try
        {
            main.Tick();
            TestMemory.Clear();
            var at = TestMemory.Alloc(16);
            TestMemory.Write(at, 0);
            main.Watching.Start("w", at, 8);
            main.Recorder.Start("r", new EntityFilter(null, null, null, null));
            Check.That(main.Watching.Count == 1 && main.Recorder.Active, "running");
            main.Settings.Serve = false;
            main.Tick();
            Check.Equal(0, main.Watching.Count);
            Check.That(!main.Recorder.Active, "recording closed");
        }
        finally
        {
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void DotAndSettingsPageDrawInEveryState()
    {
        var directory = Temp();
        var main = Plugin(directory);
        try
        {
            TestFrame.Run(() =>
            {
                main.Render();
                main.DrawSettings();
            });
            main.Settings.Serve = true;
            main.Tick();
            TestFrame.Run(() =>
            {
                main.Render();
                main.DrawSettings();
            });
            main.Settings.ShowStatusDot = false;
            TestFrame.Run(main.Render);
        }
        finally
        {
            main.Dispose();
            Clean(directory);
        }
    }

    [Test]
    public void DefaultsKeepTheServerOff()
    {
        Check.Equal(false, new Settings().Serve);
        Check.Equal(50902, new Settings().Port);
    }

    [Test]
    public void EveryRegisteredToolIsListedWithAValidName()
    {
        var directory = Temp();
        var main = Plugin(directory);
        main.Settings.Serve = true;
        try
        {
            main.Tick();
            var token = File.ReadAllText(Path.Combine(directory, "bridge-token.txt")).Trim();
            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, main.Server!.Url) { Content = new StringContent(Wire.Body("tools/list"), Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Bridge-Token", token);
            request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
            request.Headers.Add("Mcp-Method", "tools/list");
            var tools = ((JsonObject)JsonNode.Parse(client.Send(request).Content.ReadAsStringAsync().GetAwaiter().GetResult())!)["result"]!["tools"]!.AsArray();
            Check.Equal(24, tools.Count);
            var names = tools.Select(tool => (string)tool!["name"]!).ToArray();
            Check.Equal(names.Length, names.Distinct().Count());
            Check.That(names.All(name => System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_.-]{1,128}$")), "names are header and spec safe");
            Check.That(tools.All(tool => (string?)tool!["inputSchema"]!["type"] == "object" && ((string)tool["description"]!).Length > 10), "schemas and descriptions");
        }
        finally
        {
            main.Dispose();
            Directory.Delete(directory, true);
        }
    }
}
