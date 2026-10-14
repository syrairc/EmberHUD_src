using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD;

public static partial class SettingsDrawer
{
    sealed record Row(FieldInfo Field, string Text, string Label, string? Tooltip);

    static readonly ConditionalWeakTable<Type, Row[]> Rows = [];

    public static bool DrawNodes(object holder)
    {
        ImGui.PushID(holder.GetType().Name);
        try
        {
            return DrawFields(holder);
        }
        finally
        {
            ImGui.PopID();
        }
    }

    static bool DrawFields(object holder)
    {
        var changed = false;
        foreach (var (field, text, label, tooltip) in Rows.GetValue(holder.GetType(), RowsOf))
        {
            switch (field.GetValue(holder))
            {
                case ToggleNode node:
                    changed |= ImGui.Checkbox(label, ref node.Value);
                    break;
                case RangeNode<int> node:
                    changed |= ImGui.SliderInt(label, ref node.Value, node.Min, node.Max);
                    break;
                case RangeNode<float> node:
                    var wide = node.Min > 0 && node.Max / node.Min >= 100;
                    var format = wide || node.Max - node.Min <= 10 ? "%.3f" : "%.1f";
                    changed |= ImGui.SliderFloat(label, ref node.Value, node.Min, node.Max, format, wide ? ImGuiSliderFlags.Logarithmic : ImGuiSliderFlags.None);
                    break;
                case ColorNode node:
                    var colour = EColor.ToVector4(node.Value);
                    if (ImGui.ColorEdit4(label, ref colour, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                    {
                        node.Value = EColor.FromVector4(colour);
                        changed = true;
                    }
                    break;
                case HotkeyNode node:
                    changed |= Controls.Hotkey(text, ref node.Value, field.Name);
                    break;
                case TextNode node:
                    node.Value ??= "";
                    changed |= ImGui.InputText(label, ref node.Value, 256);
                    break;
                case ChoiceNode node:
                    var index = Array.IndexOf(node.Options, node.Value);
                    if (ImGui.Combo(label, ref index, node.Options, node.Options.Length))
                    {
                        node.Value = node.Options[index];
                        changed = true;
                    }
                    break;
                case TextStyle style:
                    changed |= TextStyle.Edit(text, style, id: field.Name);
                    break;
                default:
                    continue;
            }
            Text.Tip(tooltip);
        }
        return changed;
    }

    static Row[] RowsOf(Type type) =>
    [
        .. type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).Select(field =>
        {
            var menu = field.GetCustomAttribute<MenuAttribute>();
            var text = menu?.Label ?? Label(field.Name);
            return new Row(field, text, $"{text}##{field.Name}", menu?.Tooltip);
        }),
    ];

    internal static string Label(string fieldName) => Capitals().Replace(fieldName, " ");

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex Capitals();
}
