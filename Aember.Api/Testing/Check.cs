using System.Drawing;

namespace Aember.Testing;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute;

public static class Check
{
    public static void That(bool condition, string? message = null)
    {
        if (!condition) throw new InvalidOperationException(message ?? "check failed");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{(message is null ? "" : message + " - ")}expected {expected}, got {actual}");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"expected {typeof(TException).Name}, got {ex.GetType().Name}", ex);
        }
        throw new InvalidOperationException($"expected {typeof(TException).Name}, nothing was thrown");
    }
}

public static class TestGame
{
    public static void Set(GameState state) => Game.State = state;
    public static void UseGameIcons(Func<string, Sprite?>? resolver) => Draw.GameIcons = resolver;
    public static void UseSvgIcons(Func<string, int, Color, Color, Sprite?>? resolver) => Draw.SvgIcons = resolver;

    public static void UseLoc(Func<string, string?>? get, Func<string, int, ((string Key, string Text)[] Matches, bool Truncated)>? find)
    {
        Loc.Getter = get;
        Loc.Finder = find;
    }

    public static void UseScene(Func<SceneEntity[]>? list) => Scene.Lister = list;

    public static void UseTextures(Action<string, byte[], int, int>? upload, Func<string, Sprite?>? resolve, Action<string>? release = null)
    {
        Textures.Uploader = upload is null ? null : (_, key, rgba, width, height) => upload(key, rgba, width, height);
        Textures.Resolver = resolve is null ? null : (_, key) => resolve(key);
        Textures.Releaser = release is null ? null : (_, key) => release(key);
    }
}
