namespace EmberHUD;

public sealed class HotkeyBinding
{
    public Hotkey Value;

    internal HotkeyBinding(string name, string description, Action callback, Hotkey defaultKey, string? category)
    {
        Name = name;
        Description = description;
        Callback = callback;
        Default = defaultKey;
        Category = category;
        Value = defaultKey;
    }

    public string Name { get; }

    public string Description { get; }

    public string? Category { get; }

    public Hotkey Default { get; }

    public string Id => Category is null ? Name : $"{Category}/{Name}";

    internal Action Callback { get; }
}
