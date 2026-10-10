using System.Numerics;
using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace TorchlightSDK.Ui;

public static partial class Controls
{
    public sealed class IconSheet
    {
        public string Name = "";
        public ImTextureRef? Texture;
        public int Count;
        public int First;
        public Func<int, (Vector2 uv0, Vector2 uv1)>? Uv;
        public Func<int, string>? Label;
        public Action<int, ImDrawListPtr, Vector2, float, SColor>? DrawCell;

        public static IconSheet Grid(string name, ImTextureRef? texture, GridAtlas atlas, int count, Func<int, string>? label = null) =>
            new() { Name = name, Texture = texture, Count = count, Uv = atlas.UVPair, Label = label };

        public static IconSheet Markers(string name = "Custom")
        {
            var shapes = Enum.GetValues<MarkerShape>();
            return new()
            {
                Name = name,
                Count = shapes.Length,
                Label = i => TorchlightSDK.Draw.MarkerPrefix + shapes[i],
                DrawCell = (i, list, min, size, tint) => TorchlightSDK.Draw.Marker(min + new Vector2(size / 2), size * 0.4f, tint, shapes[i], list: list),
            };
        }

        public static IconSheet Atlas(string name, IconAtlas? atlas)
        {
            if (atlas is null || atlas.Sprites.Count == 0) return new() { Name = name };
            var texture = atlas.Sprites.Values.GroupBy(s => s.Texture).MaxBy(g => g.Count())!.Key;
            var names = atlas.Sprites.Where(pair => pair.Value.Texture.Equals(texture)).Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();
            var sprites = Array.ConvertAll(names, n => atlas.Sprites[n]);
            return new()
            {
                Name = name,
                Texture = texture,
                Count = names.Length,
                Uv = i => (sprites[i].Uv0, sprites[i].Uv1),
                Label = i => names[i],
            };
        }
    }

    public static void IconImage(ImTextureRef? texture, Vector2 uv0, Vector2 uv1, float size, SColor tint)
    {
        if (texture is { } tex) ImGui.ImageWithBg(tex, new Vector2(size), uv0, uv1, Vector4.Zero, EColor.ToVector4(tint));
        else ImGui.Dummy(new Vector2(size));
    }

    public static void IconImage(Sprite? sprite, float size, SColor tint)
    {
        if (sprite is { } s) IconImage(s.Texture, s.Uv0, s.Uv1, size, tint);
        else ImGui.Dummy(new Vector2(size));
    }

    public static void IconImage(ImTextureRef? texture, GridAtlas atlas, int index, float size, SColor tint)
    {
        var (uv0, uv1) = atlas.UVPair(index);
        IconImage(texture, uv0, uv1, size, tint);
    }

    public static void IconImage(IconSheet[]? sheets, string? sheet, int index, float size, SColor tint)
    {
        if (sheets is not { Length: > 0 })
        {
            ImGui.Dummy(new Vector2(size));
            return;
        }
        var sh = sheets[SheetIndex(sheets, sheet)];
        var (uv0, uv1) = CellUv(sh, index);
        IconImage(sh.Texture, uv0, uv1, size, tint);
    }

    static readonly Dictionary<uint, int> _pickerSheet = [];
    static readonly Dictionary<uint, string> _pickerFilter = [];

    public static bool IconPicker(string id, IconSheet[]? sheets, ref string sheet, ref int index,
        SColor tint, float size = 22f, float cell = 32f, int columns = 8)
    {
        int noSize = 0;
        return IconPicker(id, sheets, ref sheet, ref index, ref tint, ref noSize, false, 0, 0, false, size, cell, columns);
    }

    public static bool IconPicker(string id, IconSheet[]? sheets, ref string sheet, ref int index,
        ref SColor tint, ref int drawSize, int minSize = 8, int maxSize = 96,
        float size = 22f, float cell = 32f, int columns = 8) =>
        IconPicker(id, sheets, ref sheet, ref index, ref tint, ref drawSize, true, minSize, maxSize, true,
            size, cell, columns);

    public static bool IconPicker(string id, IconSheet[]? sheets, ref string sheet, ref int index,
        ref SColor tint, bool tintEnabled = true, float size = 22f, float cell = 32f, int columns = 8)
    {
        int px = 48;
        return IconPicker(id, sheets, ref sheet, ref index, ref tint, ref px, true, px, px, false,
            size, cell, columns, tintEnabled);
    }

    public static bool IconPicker(string id, IconSheet[]? sheets, ref string icon, SColor tint, float size = 22f, float cell = 32f, int columns = 8)
    {
        if (sheets is not { Length: > 0 }) return false;
        var (sheet, index) = FindIcon(sheets, icon);
        if (!IconPicker(id, sheets, ref sheet, ref index, tint, size, cell, columns)) return false;
        if (sheets[SheetIndex(sheets, sheet)].Label?.Invoke(index) is not { } picked) return false;
        icon = picked;
        return true;
    }

    internal static (string Sheet, int Index) FindIcon(IconSheet[] sheets, string? icon)
    {
        foreach (var sh in sheets)
            if (sh.Label is { } label)
                for (var i = sh.First; i < sh.First + sh.Count; i++)
                    if (label(i) == icon) return (sh.Name, i);
        return (sheets[0].Name, sheets[0].First);
    }

    static bool CellButton(string id, IconSheet sheet, int index, float size, Vector4 tint)
    {
        if (sheet.Texture is { } texture)
        {
            var (uv0, uv1) = CellUv(sheet, index);
            return ImGui.ImageButton(id, texture, new Vector2(size), uv0, uv1, Vector4.Zero, tint);
        }

        var padding = ImGui.GetStyle().FramePadding;
        var clicked = ImGui.Button((sheet.DrawCell is null ? "?" : "") + "##" + id, new Vector2(size) + padding * 2);
        sheet.DrawCell?.Invoke(index, ImGui.GetWindowDrawList(), ImGui.GetItemRectMin() + padding, size, EColor.FromVector4(tint));
        return clicked;
    }

    static bool IconPicker(string id, IconSheet[]? sheets, ref string sheet, ref int index, ref SColor tint, ref int drawSize, bool preview, int minSize, int maxSize, bool showSize, float size, float cell, int columns, bool tintEnabled = true)
    {
        if (sheets is not { Length: > 0 }) return false;
        var key = ImGui.GetID(id);
        var changed = false;
        var white = SColor.FromArgb(255, 255, 255, 255);
        var shown = tintEnabled ? tint : white;
        var tintV = EColor.ToVector4(shown);
        var cur = SheetIndex(sheets, sheet);

        ImGui.PushID(id);
        bool clicked;
        using (new EColor.StyleColorScope((ImGuiCol.Button, 0u)))
            clicked = CellButton("pick", sheets[cur], index, size, tintV);
        if (clicked)
        {
            _pickerSheet[key] = cur;
            _pickerFilter[key] = "";
            ImGui.OpenPopup("##iconpick");
        }

        if (ImGui.BeginPopup("##iconpick"))
        {
            var active = _pickerSheet.TryGetValue(key, out var a) ? Math.Clamp(a, 0, sheets.Length - 1) : cur;
            for (var i = 0; i < sheets.Length; i++)
            {
                if (i > 0) ImGui.SameLine(0, 12);
                if (ImGui.RadioButton(sheets[i].Name + "##s" + i, active == i)) active = i;
            }
            _pickerSheet[key] = active;
            var sh = sheets[active];
            ImGui.Separator();

            if (preview)
            {
                float box = Math.Clamp(maxSize, 16, 96);
                var org = ImGui.GetCursorScreenPos();
                ImGui.Dummy(new Vector2(box));
                var dl = ImGui.GetWindowDrawList();
                dl.AddRectFilled(org, org + new Vector2(box), ImGui.GetColorU32(ImGuiCol.FrameBg), 3f);
                dl.AddRect(org, org + new Vector2(box), ImGui.GetColorU32(ImGuiCol.Border), 3f);
                if (sheets[cur].Texture is { } stored)
                {
                    float px = Math.Clamp(drawSize, minSize, maxSize);
                    var (p0, p1) = CellUv(sheets[cur], index);
                    var at = org + new Vector2((box - px) * 0.5f);
                    dl.AddImage(stored, at, at + new Vector2(px), p0, p1, EColor.U32(shown));
                }
                else
                {
                    float px = Math.Clamp(drawSize, minSize, maxSize);
                    sheets[cur].DrawCell?.Invoke(index, dl, org + new Vector2((box - px) * 0.5f), px, shown);
                }
                ImGui.SameLine();
                ImGui.BeginGroup();
                if (showSize)
                {
                    ImGui.SetNextItemWidth(160f);
                    changed |= ImGui.SliderInt("Size", ref drawSize, minSize, maxSize);
                }
                changed |= Color("Tint", ref tint, "ptint");
                if (ImGui.Button("Done")) ImGui.CloseCurrentPopup();
                ImGui.EndGroup();
                shown = tintEnabled ? tint : white;
                tintV = EColor.ToVector4(shown);
                ImGui.Separator();
            }

            var filter = _pickerFilter.GetValueOrDefault(key, "");
            if (sh.Label is not null)
            {
                ImGui.SetNextItemWidth(180f);
                ImGui.InputTextWithHint("##filter", "search", ref filter, 64);
                _pickerFilter[key] = filter;
            }

            if (sh.Texture is null && sh.DrawCell is null) ImGui.TextDisabled("sheet not loaded");
            else
            {
                var style = ImGui.GetStyle();
                var step = cell + style.ItemSpacing.X + style.FramePadding.X * 2;
                ImGui.BeginChild("##grid", new Vector2(columns * step + 12f, 320f), ImGuiChildFlags.Borders);
                var drawn = 0;
                using (new EColor.StyleColorScope((ImGuiCol.Button, 0u)))
                    for (var i = sh.First; i < sh.First + sh.Count; i++)
                    {
                        var name = sh.Label?.Invoke(i);
                        if (name is not null && !Text.Matches(filter, name)) continue;
                        if (CellButton("c" + i, sh, i, cell, tintV))
                        {
                            sheet = sh.Name;
                            index = i;
                            changed = true;
                            if (!preview) ImGui.CloseCurrentPopup();
                        }
                        if (sh.Name == sheet && i == index)
                            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.CheckMark), 2f);
                        Text.Tip(name);
                        drawn++;
                        if (drawn % columns != 0) ImGui.SameLine();
                    }
                ImGui.EndChild();
            }
            ImGui.EndPopup();
        }
        ImGui.PopID();
        return changed;
    }

    static int SheetIndex(IconSheet[] sheets, string? name) => Math.Max(0, Array.FindIndex(sheets, s => s.Name == name));

    internal static (Vector2 uv0, Vector2 uv1) CellUv(IconSheet s, int index) =>
        s.Uv is null || s.Count <= 0 ? (Vector2.Zero, Vector2.One) : s.Uv(Math.Clamp(index, s.First, s.First + s.Count - 1));
}
