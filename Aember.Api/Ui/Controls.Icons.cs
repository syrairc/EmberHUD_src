using System.Numerics;
using Hexa.NET.ImGui;
using SColor = System.Drawing.Color;

namespace Aember.Ui;

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
                Label = i => Aember.Draw.MarkerPrefix + shapes[i],
                DrawCell = (i, list, min, size, tint) => Aember.Draw.Marker(min + new Vector2(size / 2), size * 0.4f, tint, shapes[i], list: list),
            };
        }

        public static IconSheet GameIcons(string name = "Game") => Named(name, [.. Aember.Draw.GameIconNames.Select(n => Aember.Draw.GameIconPrefix + n).Order(StringComparer.Ordinal)]);

        public static IconSheet SvgIcons(string name = "SVG") => Named(name, [.. Aember.Draw.SvgIconNames.Order(StringComparer.Ordinal)]);

        static IconSheet Named(string name, string[] icons) => new()
        {
            Name = name,
            Count = icons.Length,
            Label = i => icons[i],
            DrawCell = (i, list, min, size, tint) => Aember.Draw.Icon(icons[i], min + new Vector2(size / 2), size / 2, tint, null, list: list),
        };

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
        if (ImGui.IsItemVisible()) sheet.DrawCell?.Invoke(index, ImGui.GetWindowDrawList(), ImGui.GetItemRectMin() + padding, size, EColor.FromVector4(tint));
        return clicked;
    }

    public static bool IconPicker(string id, IconSheet[]? sheets, ref string sheet, ref int index, SColor tint, float size = 22f, float cell = 32f, int columns = 8)
    {
        if (sheets is not { Length: > 0 }) return false;
        var key = ImGui.GetID(id);
        var changed = false;
        var tintV = EColor.ToVector4(tint);
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
            var active = SheetRadios(key, sheets, cur);
            ImGui.Separator();
            changed = DrawGrid(key, sheets[active], ref sheet, ref index, tintV, cell, columns, true);
            ImGui.EndPopup();
        }
        ImGui.PopID();
        return changed;
    }

    static int SheetRadios(uint key, IconSheet[] sheets, int fallback)
    {
        var active = _pickerSheet.TryGetValue(key, out var a) ? Math.Clamp(a, 0, sheets.Length - 1) : fallback;
        for (var i = 0; i < sheets.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 12);
            if (ImGui.RadioButton(sheets[i].Name + "##s" + i, active == i)) active = i;
        }
        _pickerSheet[key] = active;
        return active;
    }

    static bool DrawGrid(uint key, IconSheet sh, ref string sheet, ref int index, Vector4 tint, float cell, int columns, bool closeOnPick)
    {
        var changed = false;
        var filter = _pickerFilter.GetValueOrDefault(key, "");
        if (sh.Label is not null)
        {
            ImGui.SetNextItemWidth(180f);
            ImGui.InputTextWithHint("##filter", "search", ref filter, 64);
            _pickerFilter[key] = filter;
        }

        if (sh.Count == 0)
        {
            ImGui.TextDisabled("not loaded - run Setup");
            return false;
        }

        if (sh.Texture is null && sh.DrawCell is null)
        {
            ImGui.TextDisabled("sheet not loaded");
            return false;
        }

        var style = ImGui.GetStyle();
        var step = cell + style.ItemSpacing.X + style.FramePadding.X * 2;
        ImGui.BeginChild("##grid", new Vector2(columns * step + 12f, 320f), ImGuiChildFlags.Borders);
        var drawn = 0;
        using (new EColor.StyleColorScope((ImGuiCol.Button, 0u)))
            for (var i = sh.First; i < sh.First + sh.Count; i++)
            {
                var name = sh.Label?.Invoke(i);
                if (name is not null && !Text.Matches(filter, name)) continue;
                if (CellButton("c" + i, sh, i, cell, tint))
                {
                    sheet = sh.Name;
                    index = i;
                    changed = true;
                    if (closeOnPick) ImGui.CloseCurrentPopup();
                }
                if (sh.Name == sheet && i == index)
                    ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.CheckMark), 2f);
                Text.Tip(name);
                drawn++;
                if (drawn % columns != 0) ImGui.SameLine();
            }
        ImGui.EndChild();
        return changed;
    }

    internal static int OpacityOf(SColor colour) => (int)MathF.Round(colour.A / 2.55f);

    internal static SColor WithOpacity(SColor colour, int percent) => SColor.FromArgb((int)MathF.Round(Math.Clamp(percent, 0, 100) * 2.55f), colour);

    internal static void ResetStyle(ref string icon, ref SColor colour, ref float scale, string? resetIcon, SColor? resetColour = null, float resetScale = 1)
    {
        icon = resetIcon ?? icon;
        colour = resetColour ?? SColor.White;
        scale = resetScale;
    }

    static IconSheet[]? _styleSheets;
    static IReadOnlyList<string>? _styleGameNames;
    static IReadOnlyList<string>? _styleSvgNames;

    static IconSheet[] StyleSheets()
    {
        if (_styleSheets is null || !ReferenceEquals(Aember.Draw.GameIconNames, _styleGameNames) || !ReferenceEquals(Aember.Draw.SvgIconNames, _styleSvgNames))
        {
            _styleSheets = [IconSheet.GameIcons(), IconSheet.SvgIcons(), IconSheet.Markers("Shapes")];
            _styleGameNames = Aember.Draw.GameIconNames;
            _styleSvgNames = Aember.Draw.SvgIconNames;
        }
        return _styleSheets;
    }

    public static bool IconPicker(string id, ref string icon, ref SColor colour, ref float scale, float minScale = 0.25f, float maxScale = 4f, string? resetIcon = null, SColor? resetColour = null, float resetScale = 1f, float baseSize = 24f, float size = 22f)
    {
        var sheets = StyleSheets();
        var key = ImGui.GetID(id);
        var changed = false;
        ImGui.PushID(id);
        var padding = ImGui.GetStyle().FramePadding;
        bool clicked;
        using (new EColor.StyleColorScope((ImGuiCol.Button, 0u)))
            clicked = ImGui.Button("##pick", new Vector2(size) + padding * 2);
        var min = ImGui.GetItemRectMin() + padding;
        Aember.Draw.Icon(icon, min + new Vector2(size / 2), size / 2, colour, null, list: ImGui.GetWindowDrawList());
        if (clicked)
        {
            _pickerSheet[key] = SheetIndex(sheets, FindIcon(sheets, icon).Sheet);
            _pickerFilter[key] = "";
            ImGui.OpenPopup("##iconstyle");
        }

        if (ImGui.BeginPopup("##iconstyle"))
        {
            const float box = 96f;
            var org = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box));
            var list = ImGui.GetWindowDrawList();
            Checker(list, org, box);
            var px = Math.Clamp(baseSize * scale, 4f, box - 4f);
            Aember.Draw.Icon(icon, org + new Vector2(box / 2), px / 2, colour, null, list: list);

            ImGui.SameLine();
            ImGui.BeginGroup();
            var rgb = EColor.ToVector4(colour);
            var rgb3 = new Vector3(rgb.X, rgb.Y, rgb.Z);
            if (ImGui.ColorEdit3("Colour", ref rgb3, ImGuiColorEditFlags.NoInputs))
            {
                colour = SColor.FromArgb(colour.A, EColor.FromVector4(new Vector4(rgb3, 1)));
                changed = true;
            }
            var opacity = OpacityOf(colour);
            ImGui.SetNextItemWidth(160f);
            if (ImGui.SliderInt("Opacity", ref opacity, 0, 100, "%d%%", ImGuiSliderFlags.AlwaysClamp))
            {
                colour = WithOpacity(colour, opacity);
                changed = true;
            }
            ImGui.SetNextItemWidth(160f);
            if (ImGui.SliderFloat("Scale", ref scale, minScale, maxScale, "%.2fx", ImGuiSliderFlags.AlwaysClamp)) changed = true;
            if (ImGui.Button("Reset"))
            {
                ResetStyle(ref icon, ref colour, ref scale, resetIcon, resetColour, resetScale);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Done")) ImGui.CloseCurrentPopup();
            ImGui.EndGroup();
            ImGui.Separator();

            var active = SheetRadios(key, sheets, 0);

            var (current, index) = FindIcon(sheets, icon);
            if (DrawGrid(key, sheets[active], ref current, ref index, EColor.ToVector4(colour), 32f, 8, false) && sheets[active].Label?.Invoke(index) is { } picked)
            {
                icon = picked;
                changed = true;
            }
            ImGui.EndPopup();
        }
        ImGui.PopID();
        return changed;
    }

    static void Checker(ImDrawListPtr list, Vector2 org, float box)
    {
        const float cell = 8f;
        list.AddRectFilled(org, org + new Vector2(box), ImGui.GetColorU32(ImGuiCol.FrameBg), 3f);
        var dark = ImGui.GetColorU32(ImGuiCol.FrameBgHovered);
        for (var y = 0; y < box / cell; y++)
            for (var x = y & 1; x < box / cell; x += 2)
                list.AddRectFilled(org + new Vector2(x * cell, y * cell), org + new Vector2((x + 1) * cell, (y + 1) * cell), dark);
        list.AddRect(org, org + new Vector2(box), ImGui.GetColorU32(ImGuiCol.Border), 3f);
    }

    static int SheetIndex(IconSheet[] sheets, string? name) => Math.Max(0, Array.FindIndex(sheets, s => s.Name == name));

    internal static (Vector2 uv0, Vector2 uv1) CellUv(IconSheet s, int index) =>
        s.Uv is null || s.Count <= 0 ? (Vector2.Zero, Vector2.One) : s.Uv(Math.Clamp(index, s.First, s.First + s.Count - 1));
}
