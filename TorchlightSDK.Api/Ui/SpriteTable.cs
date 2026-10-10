using System.Numerics;
using Hexa.NET.ImGui;

namespace TorchlightSDK.Ui;

public readonly record struct SpriteColumn(string Header, Func<string, bool> Cell);

public static partial class Controls
{
    public static bool SpriteTable(string id, IEnumerable<string> sprites, IconAtlas? atlas, IReadOnlySet<string> highlight, ref string filter, params ReadOnlySpan<SpriteColumn> columns)
    {
        ImGui.PushID(id);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##filter", "Filter", ref filter, 128);
        var changed = false;

        if (ImGui.BeginTable("##sprites", columns.Length + 2, ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("##sprite", ImGuiTableColumnFlags.WidthFixed, 24);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            foreach (var column in columns) ImGui.TableSetupColumn(column.Header, ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            var accent = ImGui.GetColorU32(ImGuiCol.CheckMark);
            var rows = SpriteRows(sprites, filter);
            var clipper = ImGui.ImGuiListClipper();
            clipper.Begin(rows.Count);
            while (clipper.Step())
                for (var k = clipper.DisplayStart; k < clipper.DisplayEnd; k++)
                {
                    var sprite = rows[k];
                    ImGui.PushID(sprite);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    if (atlas is not null && atlas.Sprites.TryGetValue(sprite, out var image)) ImGui.Image(image.Texture, new Vector2(20), image.Uv0, image.Uv1);

                    ImGui.TableNextColumn();
                    var lit = highlight.Contains(sprite);
                    if (lit) ImGui.PushStyleColor(ImGuiCol.Text, accent);
                    ImGui.TextUnformatted(sprite);
                    if (lit) ImGui.PopStyleColor();

                    foreach (var column in columns)
                    {
                        ImGui.TableNextColumn();
                        changed |= column.Cell(sprite);
                    }

                    ImGui.PopID();
                }
            clipper.End();
            clipper.Destroy();
            ImGui.EndTable();
        }

        ImGui.PopID();
        return changed;
    }

    internal static List<string> SpriteRows(IEnumerable<string> sprites, string filter) =>
        sprites.Distinct().Where(sprite => Text.Matches(filter, sprite)).Order(StringComparer.Ordinal).ToList();
}
