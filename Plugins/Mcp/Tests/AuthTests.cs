using System;
using System.IO;
using System.Linq;
using EmberHUD.Testing;

namespace Mcp;

public sealed class AuthTests
{
    const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    static string TempDirectory() => Path.Combine(Path.GetTempPath(), "mcp-test-" + Guid.NewGuid().ToString("N"));

    [Test]
    public void NewTokensAre64HexAndDifferent()
    {
        var first = Auth.NewToken();
        Check.Equal(64, first.Length);
        Check.That(first.All(Uri.IsHexDigit) && first == first.ToLowerInvariant(), "lowercase hex");
        Check.That(first != Auth.NewToken(), "random");
    }

    [Test]
    public void TokenFileIsCreatedThenReusedAndCustomWins()
    {
        var directory = TempDirectory();
        try
        {
            var first = Auth.LoadToken(directory, "");
            Check.Equal(first, File.ReadAllText(Path.Combine(directory, "bridge-token.txt")).Trim());
            Check.Equal(first, Auth.LoadToken(directory, ""));
            Check.Equal("my-own-token", Auth.LoadToken(directory, " my-own-token "));
            File.WriteAllText(Path.Combine(directory, "bridge-token.txt"), "short");
            Check.That(Auth.LoadToken(directory, "").Length == 64, "a too-short file is replaced");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void TheGatePassesOnlyLoopbackWithTheRightToken()
    {
        Check.That(Auth.Reject(null, "127.0.0.1:50902", Token, 50902, Token) is null, "no origin");
        Check.That(Auth.Reject("", "127.0.0.1:50902", Token, 50902, Token) is null, "empty origin");
        Check.That(Auth.Reject("http://localhost:5173", "127.0.0.1:50902", Token, 50902, Token) is null, "loopback origin");
        Check.That(Auth.Reject("http://127.0.0.1:3000", "localhost:50902", Token, 50902, Token) is null, "localhost host");
        Check.Equal(403, Auth.Reject("http://evil.example", "127.0.0.1:50902", Token, 50902, Token)!.Status);
        Check.Equal(403, Auth.Reject("null", "127.0.0.1:50902", Token, 50902, Token)!.Status);
        Check.Equal(403, Auth.Reject(null, "evil.example:50902", Token, 50902, Token)!.Status);
        Check.Equal(403, Auth.Reject(null, "127.0.0.1:1", Token, 50902, Token)!.Status);
        Check.Equal(403, Auth.Reject(null, null, Token, 50902, Token)!.Status);
        Check.Equal(401, Auth.Reject(null, "127.0.0.1:50902", null, 50902, Token)!.Status);
        Check.Equal(401, Auth.Reject(null, "127.0.0.1:50902", Token[..63] + "0", 50902, Token)!.Status);
        Check.Equal(401, Auth.Reject(null, "127.0.0.1:50902", "", 50902, Token)!.Status);
    }
}
