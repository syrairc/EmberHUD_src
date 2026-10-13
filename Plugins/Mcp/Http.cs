using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;

namespace Mcp;

internal sealed class Http : IDisposable
{
    public const int MaxBodyChars = 65536;
    readonly HttpListener _listener;
    readonly Rpc _rpc;
    readonly string _token;
    int _inFlight;
    long _lastRequest;
    int _disposed;

    Http(HttpListener listener, int port, Rpc rpc, string token)
    {
        _listener = listener;
        Port = port;
        _rpc = rpc;
        _token = token;
    }

    public int Port { get; }

    public string Url => $"http://127.0.0.1:{Port}/mcp";

    public int InFlight => Volatile.Read(ref _inFlight);

    public long MsSinceRequest => Interlocked.Read(ref _lastRequest) is var last and not 0 ? Environment.TickCount64 - last : long.MaxValue;

    public static Http? Start(Rpc rpc, string token, int preferredPort)
    {
        for (var port = preferredPort; port < preferredPort + 20 && port <= 65535; port++)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                listener.Close();
                continue;
            }
            var http = new Http(listener, port, rpc, token);
            _ = Task.Run(http.Loop);
            return http;
        }
        return null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _listener.Stop();
        _listener.Close();
    }

    async Task Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                if (_listener.IsListening) Log.Error(error, "mcp accept loop ended");
                return;
            }
            _ = Task.Run(() => Serve(context));
        }
    }

    void Serve(HttpListenerContext context)
    {
        Interlocked.Increment(ref _inFlight);
        Interlocked.Exchange(ref _lastRequest, Environment.TickCount64);
        try
        {
            Write(context.Response, Answer(context.Request));
        }
        catch (Exception error)
        {
            Log.Error(error, "mcp request");
            try
            {
                Write(context.Response, new Reply(500, null));
            }
            catch (Exception)
            {
                context.Response.Abort();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    Reply Answer(HttpListenerRequest request)
    {
        if (Auth.Reject(request.Headers["Origin"], request.Headers["Host"], request.Headers["X-Bridge-Token"], Port, _token) is { } rejected) return rejected;
        var body = "";
        if (request.HttpMethod == "POST")
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            var buffer = new char[MaxBodyChars + 1];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            if (read > MaxBodyChars) return new Reply(413, null);
            body = new string(buffer, 0, read);
        }
        return _rpc.Handle(request.HttpMethod, name => request.Headers[name], body);
    }

    static void Write(HttpListenerResponse response, Reply reply)
    {
        response.StatusCode = reply.Status;
        if (reply.Status == 405) response.AddHeader("Allow", "POST");
        var bytes = reply.Body is { } body ? Encoding.UTF8.GetBytes(body) : [];
        if (bytes.Length > 0) response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
        response.Close();
    }
}
