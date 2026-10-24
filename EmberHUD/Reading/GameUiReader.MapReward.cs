using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record MapRewardWidgets(nint Page, nint[] Scrolls);

    private static readonly string[] MapRewardScrollNames = Offsets.MapRewardScrolls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static readonly Regex MapRewardMemoryName = MemoryNameOf(Offsets.MapRewardMemoryIcon);

    private nint _mapRewardPage;
    private int _mapRewardMisses;
    private MapRewardWidgets? _mapRewardWidgets;
    private MapRewardView? _mapRewardShown;
    private long _mapRewardReadAt = long.MinValue;

    public MapRewardView? ReadMapReward()
    {
        if (_mapRewardPage == 0)
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
        if (widgets is null || widgets.Page != _mapRewardPage || !MapRewardScrollsStillThere(widgets))
            _mapRewardWidgets = widgets = MapRewardWidgetsOf(_mapRewardPage);
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

    private bool MapRewardScrollsStillThere(MapRewardWidgets widgets)
    {
        for (var i = 0; i < widgets.Scrolls.Length; i++)
            if (!NameMatches(widgets.Scrolls[i], MapRewardScrollNames[i])) return false;
        return true;
    }

    private MapRewardWidgets? MapRewardWidgetsOf(nint page)
    {
        var scrolls = new nint[MapRewardScrollNames.Length];
        for (var i = 0; i < scrolls.Length; i++)
            if ((scrolls[i] = FindChild(page, MapRewardScrollNames[i])) == 0) return null;
        return new MapRewardWidgets(page, scrolls);
    }

    private MapRewardView? ReadMapRewardItems(MapRewardWidgets widgets, Vector2 origin)
    {
        List<MapRewardItem> found = [];
        foreach (var scroll in widgets.Scrolls)
        {
            if (Geometry(scroll, origin) is not { } clip) continue;
            foreach (var widget in PanelContents(scroll, Offsets.MaxMapRewardItems))
                if (MapRewardItemOf(widget, origin) is { } item &&
                    item.Max.X > clip.Min.X && item.Min.X < clip.Max.X && item.Max.Y > clip.Min.Y && item.Min.Y < clip.Max.Y)
                    found.Add(item);
        }
        return found.Count == 0 ? null : new MapRewardView([.. found]);
    }

    private MapRewardItem? MapRewardItemOf(nint widget, Vector2 origin)
    {
        if (!Visible(widget) || Geometry(widget, origin) is not { } rect || MapRewardFileName(widget) is not { } fileName || MapRewardBaseId(fileName) is not { } baseId || MapRewardCount(widget) is not { } count)
            return null;
        return new MapRewardItem(baseId, count, rect.Min, rect.Max);
    }

    private int? MapRewardBaseId(string fileName)
    {
        if (MapRewardMemoryName.Match(fileName) is { Success: true } match)
            return int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        return items?.ByIcon(fileName);
    }

    private int? MapRewardCount(nint widget)
    {
        var countWidget = FindChild(widget, Offsets.MapRewardCountChild);
        return countWidget != 0 && TryText(countWidget + Offsets.TextBlockText, out var text) &&
               int.TryParse(text.Trim(), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : null;
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
