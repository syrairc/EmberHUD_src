using System.Drawing;
using System.Numerics;

namespace TorchlightSDK;

public sealed class ToggleNode(bool value)
{
    public bool Value = value;
}

public sealed class RangeNode<T>(T value, T min, T max) where T : struct, INumber<T>
{
    public T Value = value;

    public T Min => min;

    public T Max => max;

    internal void Clamp() => Value = T.Clamp(Value, min, max);
}

public sealed class ColorNode(Color value)
{
    public Color Value = value;
}

public sealed class HotkeyNode(Hotkey value)
{
    public Hotkey Value = value;

    public bool Pressed => Input.Pressed(Value);
}

public sealed class TextNode(string? value)
{
    public string Value = value ?? "";
}

public sealed class ChoiceNode(string value, params string[] options)
{
    public string Value = value;

    public string[] Options => options;
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class MenuAttribute(string label, string? tooltip = null) : Attribute
{
    public string Label => label;

    public string? Tooltip => tooltip;
}
