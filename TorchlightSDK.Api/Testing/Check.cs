namespace TorchlightSDK.Testing;

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
}
