using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record MapRewardWidgets(nint Page, bool Boss, string[] Names, nint[] Roots);

    private static readonly string[] MapRewardScrollNames = Offsets.MapRewardScrolls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static readonly string[] BossRewardRootNames = [Offsets.BossRewardGrid];
    private static readonly Regex MapRewardMemoryName = MemoryNameOf(Offsets.MapRewardMemoryIcon);

    private nint _mapRewardPage;
    private nint _bossRewardPage;
    private int _bossRewardMisses;
    private int _mapRewardMisses;
    private MapRewardWidgets? _mapRewardWidgets;
    private MapRewardView? _mapRewardShown;
    private long _mapRewardReadAt = long.MinValue;

    public MapRewardView? ReadMapReward()
    {
        var boss = _bossRewardPage != 0 && (_mapRewardPage == 0 || _bossRewardMisses < _mapRewardMisses);
        var page = boss ? _bossRewardPage : _mapRewardPage;
        if (page == 0)
        {
            _mapRewardWidgets = null;
            _mapRewardShown = null;
            _mapRewardReadAt = long.MinValue;
            return null;
        }

        var now = _clock();
        if (now < _mapRewardReadAt) return _mapRewardShown;
        _mapRewardReadAt = now + Offsets.BagRefreshMilliseconds;

        var widgets = _mapRewardWidgets;
        if (widgets is null || widgets.Page != page || widgets.Boss != boss || !MapRewardRootsStillThere(widgets))
            _mapRewardWidgets = widgets = MapRewardWidgetsOf(page, boss);
        if (widgets is null)
        {
            _mapRewardReadAt = now + Offsets.RectChildRetryMilliseconds;
            return _mapRewardShown = null;
        }
        return _mapRewardShown = TryOverlay(out _, out var origin) ? ReadMapRewardItems(widgets, origin) : null;
    }

    private static Regex MemoryNameOf(string pattern)
    {
        var parts = pattern.Split("{id}", 2);
        return new Regex($"^{Regex.Escape(parts[0])}(\\d+){Regex.Escape(parts.Length > 1 ? parts[1] : "")}(?:_\\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool MapRewardRootsStillThere(MapRewardWidgets widgets)
    {
        for (var i = 0; i < widgets.Roots.Length; i++)
            if (!NameMatches(widgets.Roots[i], widgets.Names[i])) return false;
        return true;
    }

    private MapRewardWidgets? MapRewardWidgetsOf(nint page, bool boss)
    {
        var names = boss ? BossRewardRootNames : MapRewardScrollNames;
        var roots = new nint[names.Length];
        for (var i = 0; i < roots.Length; i++)
            if ((roots[i] = FindChild(page, names[i])) == 0) return null;
        return new MapRewardWidgets(page, boss, names, roots);
    }

    private IEnumerable<(nint Item, nint Frame)> MapRewardWidgetsIn(nint root, bool boss)
    {
        if (!boss)
        {
            foreach (var widget in PanelContents(root, Offsets.MaxMapRewardItems)) yield return (widget, widget);
            yield break;
        }
        foreach (var cell in PanelContents(root, Offsets.MaxMapRewardItems))
            foreach (var item in PanelContents(cell, 1)) yield return (item, cell);
    }

    private MapRewardView? ReadMapRewardItems(MapRewardWidgets widgets, Vector2 origin)
    {
        List<MapRewardItem> found = [];
        foreach (var root in widgets.Roots)
        {
            if (Geometry(root, origin) is not { } clip) continue;
            foreach (var (widget, frame) in MapRewardWidgetsIn(root, widgets.Boss))
                if (MapRewardItemOf(widget, frame, origin, widgets.Boss) is { } item &&
                    item.Max.X > clip.Min.X && item.Min.X < clip.Max.X && item.Max.Y > clip.Min.Y && item.Min.Y < clip.Max.Y)
                    found.Add(item);
        }
        return found.Count == 0 ? null : new MapRewardView([.. found], widgets.Boss);
    }

    private MapRewardItem? MapRewardItemOf(nint widget, nint frame, Vector2 origin, bool boss)
    {
        if (!Visible(widget) || Geometry(frame, origin) is not { } rect || MapRewardFileName(widget) is not { } fileName || MapRewardBaseId(fileName, boss) is not { } id || MapRewardCount(widget, boss) is not { } count)
            return null;
        return new MapRewardItem(id.BaseId, count, rect.Min, rect.Max, id.SpecialId);
    }

    private (int BaseId, int SpecialId)? MapRewardBaseId(string fileName, bool boss)
    {
        if (MapRewardMemoryName.Match(fileName) is { Success: true } match)
            return int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? (id, 0) : null;
        if (items?.ByIcon(fileName) is { } baseId) return (baseId, 0);
        return boss && items?.ByUniqueIcon(fileName) is var (uniqueId, uniqueBase) ? (uniqueBase, uniqueId) : null;
    }

    private int? MapRewardCount(nint widget, bool boss)
    {
        var countWidget = FindChild(widget, Offsets.MapRewardCountChild);
        var count = countWidget != 0 && TryText(countWidget + Offsets.TextBlockText, out var text) &&
                    int.TryParse(text.Trim(), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : (int?)null;
        return count ?? (boss ? 1 : null);
    }

    private string? MapRewardFileName(nint widget)
    {
        if (!memory.TryReadPtr(widget + Offsets.MapRewardItemIcon, out var icon) || icon == 0 ||
            !memory.TryRead(icon + Offsets.MapRewardIconPath, out uint index) ||
            !memory.TryRead(icon + Offsets.MapRewardIconPath + Offsets.FNameNumber, out uint number) ||
            !names.TryRead(index, out var path))
            return null;

        var slash = path.LastIndexOf('/');
        if (slash < 0) return null;
        var dot = path.IndexOf('.', slash + 1);
        var fileName = dot < 0 ? path[(slash + 1)..] + (number > 0 ? $"_{number - 1}" : "") : path[(slash + 1)..dot];
        return fileName.Length == 0 ? null : fileName;
    }
}
