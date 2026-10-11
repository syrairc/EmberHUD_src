namespace Aember.Testing;

public static class TestInput
{
    public static void Hold(params Key[] keys)
    {
        var held = keys.Select(key => (int)key).ToHashSet();
        Input.KeyDown = held.Contains;
        Input.NewFrame(false);
    }
}
