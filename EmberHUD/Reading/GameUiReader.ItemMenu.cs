using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private sealed record MenuObject(nint Address, bool IsTip);

    private readonly Dictionary<nint, string> _classNames = [];
    private readonly Dictionary<nint, (nint Widget, long RetryAt)> _menuPriceChecks = [];
    private nint[] _menuTips = [];
    private nint[] _menuMoreButtons = [];
    private ItemMenu? _menuShown;
    private long _menuReadAt = long.MinValue;
    private long _menuScanAt = long.MinValue;

    public ItemMenu? ReadItemMenu()
    {
        var now = _clock();
        if (now < _menuReadAt) return _menuShown;
        _menuReadAt = now + Offsets.ItemMenuRefreshMilliseconds;
        if (!TryOverlay(out var overlay, out var origin) || !_slate.TryRect(overlay, Vector2.Zero, out var window))
            return _menuShown = null;

        var screen = window.Max - window.Min;
        var read = EvaluateMenu(origin, screen);
        if (now >= _menuScanAt && (read.Stale || _menuTips.Length == 0 || read.Menu is not null && read.MoreRects < 3))
        {
            _menuScanAt = now + Offsets.ItemMenuScanMilliseconds;
            ScanMenuObjects();
            read = EvaluateMenu(origin, screen);
        }
        return _menuShown = read.Menu;
    }

    private (ItemMenu? Menu, bool Stale, int MoreRects) EvaluateMenu(Vector2 origin, Vector2 screen)
    {
        var stale = false;
        List<ItemMenu> popups = [];
        foreach (var tip in _menuTips)
        {
            if (!IsLiveInstanceOf(tip, Offsets.ItemMenuTipClass)) stale = true;
            else if (ReadPopup(tip, origin, screen) is { } popup) popups.Add(popup);
        }

        List<ScreenRect> moreRects = [];
        foreach (var button in _menuMoreButtons)
        {
            if (!IsLiveInstanceOf(button, Offsets.ItemMenuMoreButtonClass)) stale = true;
            else if (OnScreen(button, origin, screen) is { } rect) moreRects.Add(rect);
        }
        return (popups.Count == 1 ? WithMoreRow(popups[0], moreRects) : null, stale, moreRects.Count);
    }

    private ItemMenu? ReadPopup(nint tip, Vector2 origin, Vector2 screen)
    {
        var handle = HandleOf(tip);
        if (handle == 0) return null;

        var takeOut = FieldRect(handle, Offsets.ItemMenuTakeOut, origin, screen);
        var lockButton = FieldRect(handle, Offsets.ItemMenuLock, origin, screen);
        var more = FieldRect(handle, Offsets.ItemMenuMore, origin, screen);
        if (takeOut is null && lockButton is null && more is null) return null;
        return new ItemMenu(takeOut, lockButton, more, null, null, null, OnScreen(PriceCheckOf(tip), origin, screen));
    }

    private nint HandleOf(nint tip) =>
        memory.TryReadPtr(tip + Offsets.ItemMenuHandWidget, out var hand) && hand != 0 &&
        PanelContents(hand, 1).FirstOrDefault() is var handle && handle != 0 && IsLiveInstanceOf(handle, Offsets.ItemMenuHandleClass) ? handle : 0;

    private nint PriceCheckOf(nint tip)
    {
        var now = _clock();
        if (!_menuPriceChecks.TryGetValue(tip, out var cached) || cached.Widget != 0 && !NameMatches(cached.Widget, Offsets.ItemMenuPriceCheck) || cached.Widget == 0 && now >= cached.RetryAt)
            _menuPriceChecks[tip] = cached = (FindChild(tip, Offsets.ItemMenuPriceCheck), now + Offsets.RectChildRetryMilliseconds);
        return cached.Widget;
    }

    private ScreenRect? FieldRect(nint owner, int offset, Vector2 origin, Vector2 screen) =>
        memory.TryReadPtr(owner + offset, out var widget) ? OnScreen(widget, origin, screen) : null;

    private ScreenRect? OnScreen(nint widget, Vector2 origin, Vector2 screen) =>
        widget != 0 && Geometry(widget, origin) is { } rect && rect.Max.X > 0 && rect.Max.Y > 0 && rect.Min.X < screen.X && rect.Min.Y < screen.Y
            ? new ScreenRect(rect.Min, rect.Max)
            : null;

    private static ItemMenu WithMoreRow(ItemMenu menu, List<ScreenRect> moreRects)
    {
        if (moreRects.Count is 0 or > 3) return menu;

        var sorted = moreRects.OrderBy(r => r.Min.Y).ToList();
        List<List<ScreenRect>> rows = [];
        for (var i = 0; i < sorted.Count;)
        {
            var row = sorted.Skip(i).TakeWhile(r => r.Min.Y - sorted[i].Min.Y < sorted[i].Height / 2).OrderBy(r => r.Min.X).ToList();
            rows.Add(row);
            i += row.Count;
        }
        if (rows.Count > 2 || rows[0].Count > 2 || rows.Count == 2 && rows[1].Count > 1) return menu;

        return menu with
        {
            DropSource = rows[0][0],
            Details = rows[0].Count > 1 ? rows[0][1] : null,
            Share = rows.Count > 1 ? rows[1][0] : null,
        };
    }

    private void ScanMenuObjects()
    {
        var found = ScanObjects(obj =>
            IsLiveInstanceOf(obj, Offsets.ItemMenuTipClass) ? new MenuObject(obj, true) :
            IsLiveInstanceOf(obj, Offsets.ItemMenuMoreButtonClass) ? new MenuObject(obj, false) : null);
        _menuTips = [.. found.Where(o => o.IsTip).Select(o => o.Address)];
        _menuMoreButtons = [.. found.Where(o => !o.IsTip).Select(o => o.Address)];
        _menuPriceChecks.Clear();
    }

    private bool IsLiveInstanceOf(nint obj, string className) =>
        memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && ClassNameOf(cls) == className &&
        memory.TryRead(obj + Offsets.UObjectName, out uint nameIndex) && names.TryRead(nameIndex, out var name) &&
        !name.StartsWith(Offsets.DefaultObjectPrefix, StringComparison.Ordinal);

    private string? ClassNameOf(nint cls)
    {
        if (_classNames.TryGetValue(cls, out var known)) return known;
        return memory.TryRead(cls + Offsets.UObjectName, out uint nameIndex) && names.TryRead(nameIndex, out var name) ? _classNames[cls] = name : null;
    }

    private List<T> ScanObjects<T>(Func<nint, T?> pick) where T : class
    {
        List<T> found = [];
        var table = moduleBase + Offsets.GObjectsGlobalRva;
        if (!memory.TryReadPtr(table, out var chunks) ||
            !memory.TryRead(table + Offsets.GObjectsNumElements, out int count) ||
            count <= 0 || count > Offsets.MaxGObjectChunks * Offsets.GObjectsPerChunk)
            return found;

        for (var chunk = (count - 1) / Offsets.GObjectsPerChunk; chunk >= 0; chunk--)
        {
            var inChunk = Math.Min(Offsets.GObjectsPerChunk, count - chunk * Offsets.GObjectsPerChunk);
            if (!memory.TryReadPtr(chunks + chunk * nint.Size, out var items) ||
                !memory.TryReadArray(items, inChunk * Offsets.GObjectsItemSize, out byte[] bytes))
                continue;

            for (var i = inChunk - 1; i >= 0; i--)
            {
                var obj = (nint)BitConverter.ToInt64(bytes, i * Offsets.GObjectsItemSize + Offsets.GObjectsItemObject);
                if (obj != 0 && pick(obj) is { } hit) found.Add(hit);
            }
        }
        return found;
    }
}
