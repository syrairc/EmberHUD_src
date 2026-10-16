using System.Runtime.InteropServices;
using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader(IReadBackend memory, nint moduleBase, Names names, Func<long>? clockMilliseconds = null)
{
    private readonly record struct PanelSpec(PanelKind Kind, string RectChild);

    private readonly record struct Root(nint Widget, nint Page, string PageClass, PanelSpec Spec, bool Reached);

    private static readonly Dictionary<string, PanelSpec> Specs = Offsets.GamePanels
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.Split(',', StringSplitOptions.TrimEntries))
        .ToDictionary(f => f[0], f => new PanelSpec(Enum.Parse<PanelKind>(f[1]), f[2]));

    private static readonly HashSet<string> Hud = [.. Offsets.HudPages.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    private static readonly PanelSpec Unknown = new(PanelKind.Other, "");

    private readonly SlateWidgets _slate = new(memory);
    private readonly Dictionary<(nint Class, string Ancestor), bool> _isA = [];
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<nint, (nint Child, long RetryAt)> _rectChildren = [];
    private readonly Dictionary<string, nint> _hudChildren = [];
    private readonly Dictionary<nint, Root> _roots = [];
    private readonly HashSet<nint> _rootsSeen = [];
    private readonly HashSet<nint> _pagesSeen = [];
    private readonly List<GamePanel> _panels = [];
    private GameUi _last = GameUi.Empty;
    private long _nextRootRefresh;

    public GameUi Read()
    {
        if (!TryOverlay(out var overlay, out var origin))
        {
            _roots.Clear();
            _rectChildren.Clear();
            _hudChildren.Clear();
            if (++_tradeMisses >= 2) _tradePage = 0;
            return _last = GameUi.Empty;
        }

        var now = _clock();
        var resolvedAny = now >= _nextRootRefresh;
        if (resolvedAny)
        {
            _roots.Clear();
            _nextRootRefresh = now + Offsets.UiRootRefreshMilliseconds;
        }

        nint tradePage = 0;
        var gameMinimapHidden = false;
        _panels.Clear();
        _pagesSeen.Clear();
        _rootsSeen.Clear();
        foreach (var slot in Children(overlay + Offsets.OverlayChildren, Offsets.MaxViewportRoots))
        {
            if (!_roots.TryGetValue(slot, out var root))
            {
                if (ResolveRoot(slot) is not { } resolved) continue;
                _roots[slot] = root = resolved;
                resolvedAny = true;
            }

            _rootsSeen.Add(slot);
            if (!root.Reached) continue;
            if (root.PageClass == Offsets.GameMinimapPage) gameMinimapHidden = !Visible(root.Page);
            if (_pagesSeen.Contains(root.Page) || Hud.Contains(root.PageClass) || !Visible(root.Widget) || !Visible(root.Page)) continue;
            if (root.PageClass == Offsets.TradePageClassName) tradePage = root.Page;
            if (RectOf(root.Page, root.Spec.RectChild, origin) is not { } rect) continue;
            _pagesSeen.Add(root.Page);
            _panels.Add(new GamePanel(root.PageClass, root.Spec.Kind, rect.Min, rect.Max, false) { Address = root.Page });
        }

        if (tradePage != 0)
        {
            _tradePage = tradePage;
            _tradeMisses = 0;
        }
        else if (++_tradeMisses >= 2) _tradePage = 0;

        if (resolvedAny || _roots.Count != _rootsSeen.Count)
        {
            foreach (var gone in _roots.Keys.Where(key => !_rootsSeen.Contains(key)).ToArray())
                _roots.Remove(gone);
            foreach (var gone in _rectChildren.Keys.Where(page => !_pagesSeen.Contains(page)).ToArray())
                _rectChildren.Remove(gone);
        }

        if (gameMinimapHidden)
            for (var i = 0; i < _panels.Count; i++)
                _panels[i] = _panels[i] with { FullScreen = true };
        if (CollectionsMarshal.AsSpan(_panels).SequenceEqual(_last.Panels))
            return _last;
        return _last = _panels.Count == 0 ? GameUi.Empty : new GameUi([.. _panels]);
    }

    private Root? ResolveRoot(nint canvas)
    {
        if (!TryUserWidget(canvas, out var widget) || !names.TryReadClassName(widget, out var className)) return null;
        var page = widget;
        var pageClass = className;
        var reached = true;
        if (className == Offsets.PageHostClassName)
        {
            if (!TryHostedPage(widget, out page) || !names.TryReadClassName(page, out pageClass)) return null;
        }
        else reached = Specs.ContainsKey(pageClass) || pageClass == Offsets.TradePageClassName;

        return new Root(widget, page, pageClass, Specs.GetValueOrDefault(pageClass, Unknown), reached);
    }

    internal void ForgetHud() => _hudChildren.Clear();

    internal string? HudText(string childName, int textOffset) =>
        HudChild(childName) is var widget && widget != 0 && TryText(widget + textOffset, out var text) ? text : null;

    internal string[] HudTexts(string panelName, int textOffset)
    {
        var panel = HudChild(panelName);
        if (panel == 0) return [];
        List<string> texts = [];
        foreach (var item in PanelContents(panel, Offsets.MaxWidgetNodes))
            if (Visible(item) && TryText(item + textOffset, out var text) && text.Length > 0)
                texts.Add(text);
        return [.. texts];
    }

    private nint HudChild(string childName)
    {
        if (_hudChildren.TryGetValue(childName, out var cached) && NameIs(cached, childName)) return cached;
        _hudChildren.Remove(childName);
        if (!TryHud(out var hud)) return 0;
        var found = FindChild(hud, childName);
        if (found != 0) _hudChildren[childName] = found;
        return found;
    }

    private bool TryHud(out nint hud)
    {
        hud = 0;
        if (!TryOverlay(out var overlay, out _)) return false;
        foreach (var root in Children(overlay + Offsets.OverlayChildren, Offsets.MaxViewportRoots))
        {
            if (!TryUserWidget(root, out var page) || !names.TryReadClassName(page, out var pageClass)) continue;
            if (pageClass == Offsets.PageHostClassName && (!TryHostedPage(page, out page) || !names.TryReadClassName(page, out pageClass))) continue;
            if (pageClass != Offsets.HudPageClassName) continue;
            hud = page;
            return true;
        }
        return false;
    }

    private bool NameIs(nint widget, string childName) =>
        memory.TryRead(widget + Offsets.UObjectName, out uint index) && names.TryRead(index, out var found) && found == childName;

    private bool TryText(nint fText, out string text)
    {
        text = "";
        if (!memory.TryReadPtr(fText, out var data) || data == 0 ||
            !memory.TryReadPtr(data + Offsets.TextDataString, out var chars) || chars == 0 ||
            !memory.TryRead(data + Offsets.TextDataString + Offsets.FStringNum, out int count) || count <= 0 || count > Offsets.MaxFStringChars ||
            !memory.TryReadArray(chars, count * 2, out byte[] bytes))
            return false;
        text = Markup().Replace(System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0'), "");
        return true;
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]*>")]
    private static partial System.Text.RegularExpressions.Regex Markup();

    private (Vector2 Min, Vector2 Max)? RectOf(nint page, string rectChild, Vector2 origin)
    {
        if (rectChild.Length == 0) return Geometry(page, origin);
        var now = _clock();
        if (!_rectChildren.TryGetValue(page, out var cached) || cached.Child == 0 && now >= cached.RetryAt)
            _rectChildren[page] = cached = (FindChild(page, rectChild), now + Offsets.RectChildRetryMilliseconds);
        if (cached.Child != 0)
        {
            if (Geometry(cached.Child, origin) is { } childRect) return childRect;
            _rectChildren[page] = (0, now + Offsets.RectChildRetryMilliseconds);
        }
        return Geometry(page, origin);
    }

    internal Vector2? TryOrigin() => TryOverlay(out _, out var origin) ? origin : null;

    private bool TryOverlay(out nint overlay, out Vector2 origin)
    {
        overlay = 0;
        origin = default;
        if (!memory.TryReadPtr(moduleBase + Offsets.GEngineGlobalRva, out var engine) ||
            !memory.TryReadPtr(engine + Offsets.EngineGameViewport, out var viewport) ||
            !_slate.TryWeak(viewport + Offsets.ViewportOverlay, out overlay) ||
            !_slate.TryRect(overlay, Vector2.Zero, out var rect))
            return false;

        origin = rect.Min;
        return true;
    }

    private bool TryUserWidget(nint canvas, out nint widget)
    {
        widget = 0;
        return Children(canvas + Offsets.CanvasChildren, 1).FirstOrDefault() is var objectWidget && objectWidget != 0 &&
               memory.TryReadPtr(objectWidget + Offsets.ObjectWidgetObject, out widget) && widget != 0;
    }

    private bool TryHostedPage(nint host, out nint page)
    {
        page = 0;
        return memory.TryReadPtr(host + Offsets.PageHostContent, out var content) && content != 0 &&
               PanelContents(content, 1).FirstOrDefault() is var first && (page = first) != 0;
    }

    private IEnumerable<nint> Children(nint panelChildren, int max) =>
        Slots(panelChildren + Offsets.PanelChildrenSlots, Offsets.SlotWidget, Offsets.MaxViewportRoots, max);

    private IEnumerable<nint> PanelContents(nint panel, int max) =>
        Slots(panel + Offsets.PanelSlots, Offsets.PanelSlotContent, Offsets.MaxWidgetNodes, max);

    private IEnumerable<nint> Slots(nint arrayAt, int widgetOffset, int validCount, int take)
    {
        if (!memory.TryReadPtr(arrayAt, out var data) ||
            !memory.TryRead(arrayAt + Offsets.TArrayNum, out int count) || count <= 0 || count > validCount ||
            !memory.TryReadArray(data, Math.Min(count, take), out long[] slots))
            yield break;

        foreach (var slot in slots)
            if (slot != 0 && memory.TryReadPtr((nint)slot + widgetOffset, out var widget))
                yield return widget;
    }

    internal nint FindChild(nint page, string childName)
    {
        var split = childName.LastIndexOf('_');
        var number = 0u;
        var baseName = childName;
        if (split > 0 && childName.AsSpan(split + 1) is { Length: > 0 } digits && digits.IndexOfAnyExceptInRange('0', '9') < 0 &&
            (digits.Length == 1 || digits[0] != '0') && uint.TryParse(digits, out var suffix) && suffix < uint.MaxValue)
        {
            baseName = childName[..split];
            number = suffix + 1;
        }

        Queue<(nint Widget, int Depth)> queue = [];
        HashSet<nint> visited = [];
        Enqueue(WidgetTreeRoot(page), 0);
        while (queue.TryDequeue(out var next) && visited.Count < Offsets.MaxWidgetNodes)
        {
            var (widget, depth) = next;
            if (memory.TryRead(widget + Offsets.UObjectName, out uint nameIndex) && memory.TryRead(widget + Offsets.UObjectName + Offsets.FNameNumber, out uint nameNumber) &&
                nameNumber == number && names.TryRead(nameIndex, out var widgetName) && widgetName == baseName)
                return widget;
            if (depth >= Offsets.MaxWidgetDepth || !memory.TryReadPtr(widget + Offsets.UObjectClass, out var cls)) continue;
            if (IsA(cls, Offsets.PanelWidgetClassName))
                foreach (var child in PanelContents(widget, Offsets.MaxWidgetNodes)) Enqueue(child, depth + 1);
            else if (IsA(cls, Offsets.UserWidgetClassName))
                Enqueue(WidgetTreeRoot(widget), depth + 1);
        }
        return 0;

        void Enqueue(nint widget, int depth)
        {
            if (widget != 0 && visited.Add(widget)) queue.Enqueue((widget, depth));
        }
    }

    private nint WidgetTreeRoot(nint userWidget) =>
        memory.TryReadPtr(userWidget + Offsets.UserWidgetTree, out var tree) && tree != 0 &&
        memory.TryReadPtr(tree + Offsets.WidgetTreeRoot, out var root) ? root : 0;

    private bool IsA(nint cls, string ancestor)
    {
        if (_isA.TryGetValue((cls, ancestor), out var known)) return known;

        var at = cls;
        var result = false;
        for (var depth = 0; at != 0 && depth < Offsets.MaxWidgetDepth * 4; depth++)
        {
            if (memory.TryRead(at + Offsets.UObjectName, out uint index) && names.TryRead(index, out var className) && className == ancestor)
            {
                result = true;
                break;
            }
            if (!memory.TryReadPtr(at + Offsets.UStructSuper, out at)) break;
        }
        return _isA[(cls, ancestor)] = result;
    }

    private bool Visible(nint widget) =>
        memory.TryRead(widget + Offsets.UWidgetVisibility, out byte visibility) && visibility is 0 or 3 or 4;

    private (Vector2 Min, Vector2 Max)? Geometry(nint widget, Vector2 origin) =>
        _slate.TryWeak(widget + Offsets.UWidgetSlate, out var slate) && _slate.TryRect(slate, origin, out var rect) ? rect : null;
}
