using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mcp;

internal static class Auth
{
    public static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string LoadToken(string directory, string custom)
    {
        if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();
        var path = Path.Combine(directory, "bridge-token.txt");
        Directory.CreateDirectory(directory);
        if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: >= 32 } saved) return saved;
        var token = NewToken();
        File.WriteAllText(path, token);
        return token;
    }

    public static Reply? Reject(string? origin, string? host, string? token, int port, string expected)
    {
        if (!string.IsNullOrEmpty(origin) && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback)) return new Reply(403, null);
        if (!string.Equals(host, $"127.0.0.1:{port}", StringComparison.OrdinalIgnoreCase) && !string.Equals(host, $"localhost:{port}", StringComparison.OrdinalIgnoreCase)) return new Reply(403, null);
        return token is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected)) ? null : new Reply(401, null);
    }
}
