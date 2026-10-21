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

    private static readonly Dictionary<string, string[]> PartNames = Offsets.PanelParts
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.Split(':', 2))
        .ToDictionary(f => f[0], f => f[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static readonly HashSet<string> Hud = [.. Offsets.HudPages.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    private static readonly PanelSpec Unknown = new(PanelKind.Other, "");

    private readonly SlateWidgets _slate = new(memory);
    private readonly Dictionary<(nint Class, string Ancestor), bool> _isA = [];
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<nint, (nint Child, long RetryAt)> _rectChildren = [];
    private readonly Dictionary<(nint Page, string Child), (nint Widget, long RetryAt)> _partWidgets = [];
    private readonly Dictionary<string, nint> _hudChildren = [];
    private readonly Dictionary<nint, Root> _roots = [];
    private readonly HashSet<nint> _rootsSeen = [];
    private readonly HashSet<nint> _pagesSeen = [];
    private readonly Dictionary<nint, long> _failingSince = [];
    private readonly HashSet<nint> _failedNow = [];
    private readonly List<GamePanel> _panels = [];
    private GameUi _last = GameUi.Empty;
    private long _nextRootRefresh;

    public GameUi Read()
    {
        TryRead(out var ui);
        return ui;
    }

    public bool TryRead(out GameUi ui) => TryRead(out ui, out _);

    public bool TryRead(out GameUi ui, out bool chatSeen)
    {
        chatSeen = false;
        if (!TryOverlay(out var overlay, out var origin))
        {
            _roots.Clear();
            _rectChildren.Clear();
            _partWidgets.Clear();
            _hudChildren.Clear();
            _failingSince.Clear();
            if (++_tradeMisses >= 2) _tradePage = 0;
            if (++_compareTipPanelMisses >= 2) _compareTipPanel = false;
            if (++_bagMisses >= 2) _bagPage = 0;
            if (++_stashMisses >= 2) _stashPage = 0;
            ui = _last = GameUi.Empty;
            return false;
        }

        var now = _clock();
        var resolvedAny = now >= _nextRootRefresh;
        if (resolvedAny)
        {
            _roots.Clear();
            _nextRootRefresh = now + Offsets.UiRootRefreshMilliseconds;
        }

        nint tradePage = 0;
        var compareTipPanelSeen = false;
        nint bagPage = 0;
        nint stashPage = 0;
        var gameMinimapHidden = false;
        _panels.Clear();
        _pagesSeen.Clear();
        _rootsSeen.Clear();
        var walkedAny = false;
        var complete = true;
        _failedNow.Clear();
        foreach (var slot in Children(overlay + Offsets.OverlayChildren, Offsets.MaxViewportRoots))
        {
            if (!_roots.TryGetValue(slot, out var root))
            {
                if (ResolveRoot(slot) is not { } resolved)
                {
                    complete &= !Failing(slot, now);
                    continue;
                }
                _roots[slot] = root = resolved;
                resolvedAny = true;
            }

            walkedAny = true;
            _rootsSeen.Add(slot);
            var knownChat = root.PageClass == Offsets.ChatPageClassName;
            var widgetVisible = VisibleOrNull(root.Widget);
            var pageVisible = VisibleOrNull(root.Page);
            if (knownChat)
            {
                if (widgetVisible == true && pageVisible == true) chatSeen = true;
                else if (widgetVisible is null || pageVisible is null) complete = false;
            }

            if (!root.Reached) continue;
            if (root.PageClass == Offsets.GameMinimapPage) gameMinimapHidden = pageVisible != true;
            if (_pagesSeen.Contains(root.Page) || Hud.Contains(root.PageClass)) continue;
            if (widgetVisible is null || pageVisible is null)
            {
                complete &= !knownChat && !Failing(slot, now);
                continue;
            }

            if (!widgetVisible.Value || !pageVisible.Value) continue;
            if (root.PageClass == Offsets.TradePageClassName) tradePage = root.Page;
            if (root.PageClass == Offsets.CompareTipPanelClassName) compareTipPanelSeen = true;
            if (root.Spec.Kind == PanelKind.Inventory) bagPage = root.Page;
            if (root.Spec.Kind == PanelKind.Stash) stashPage = root.Page;
            if (RectOf(root.Page, root.Spec.RectChild, origin) is not { } rect)
            {
                complete &= !knownChat && !Failing(slot, now);
                continue;
            }

            _pagesSeen.Add(root.Page);
            _panels.Add(new GamePanel(root.PageClass, root.Spec.Kind, rect.Min, rect.Max, false) { Address = root.Page, Parts = PartsOf(root.Page, root.PageClass, origin, now) });
        }

        foreach (var recovered in _failingSince.Keys.Where(key => !_failedNow.Contains(key)).ToArray())
            _failingSince.Remove(recovered);

        if (tradePage != 0)
        {
            _tradePage = tradePage;
            _tradeMisses = 0;
        }
        else if (++_tradeMisses >= 2) _tradePage = 0;

        if (compareTipPanelSeen)
        {
            _compareTipPanel = true;
            _compareTipPanelMisses = 0;
        }
        else if (++_compareTipPanelMisses >= 2) _compareTipPanel = false;

        if (bagPage != 0)
        {
            _bagPage = bagPage;
            _bagMisses = 0;
        }
        else if (++_bagMisses >= 2) _bagPage = 0;

        if (stashPage != 0)
        {
            _stashPage = stashPage;
            _stashMisses = 0;
        }
        else if (++_stashMisses >= 2) _stashPage = 0;

        if (resolvedAny || _roots.Count != _rootsSeen.Count)
        {
            foreach (var gone in _roots.Keys.Where(key => !_rootsSeen.Contains(key)).ToArray())
                _roots.Remove(gone);
            foreach (var gone in _rectChildren.Keys.Where(page => !_pagesSeen.Contains(page)).ToArray())
                _rectChildren.Remove(gone);
            foreach (var gone in _partWidgets.Keys.Where(key => !_pagesSeen.Contains(key.Page)).ToArray())
                _partWidgets.Remove(gone);
        }

        if (gameMinimapHidden)
            for (var i = 0; i < _panels.Count; i++)
                _panels[i] = _panels[i] with { FullScreen = true };
        if (!CollectionsMarshal.AsSpan(_panels).SequenceEqual(_last.Panels))
            _last = _panels.Count == 0 ? GameUi.Empty : new GameUi([.. _panels]);
        ui = _last;
        return walkedAny && complete;
    }

    private bool Failing(nint slot, long now)
    {
        _failedNow.Add(slot);
        _failingSince.TryAdd(slot, now);
        return now - _failingSince[slot] < Offsets.UiRootRefreshMilliseconds;
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
        else reached = Specs.ContainsKey(pageClass) || pageClass == Offsets.TradePageClassName || pageClass == Offsets.CompareTipPanelClassName;

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

    private bool TryHud(out nint hud) => TryRootPage(Offsets.HudPageClassName, out hud);

    internal int[]? ReadSkillBar() =>
        TryRootPage(Offsets.PadPageClassName, out var pad) &&
        memory.TryReadPtr(pad + Offsets.SkillCfgIdsOffset, out var data) &&
        memory.TryRead(pad + Offsets.SkillCfgIdsOffset + Offsets.TArrayNum, out int count) && count > 0 && count <= Offsets.MaxSkills &&
        memory.TryReadArray(data, count, out int[] ids) ? ids : null;

    private bool TryRootPage(string className, out nint page)
    {
        page = 0;
        if (!TryOverlay(out var overlay, out _)) return false;
        foreach (var root in Children(overlay + Offsets.OverlayChildren, Offsets.MaxViewportRoots))
        {
            if (!TryUserWidget(root, out var candidate) || !names.TryReadClassName(candidate, out var pageClass)) continue;
            if (pageClass == Offsets.PageHostClassName && (!TryHostedPage(candidate, out candidate) || !names.TryReadClassName(candidate, out pageClass))) continue;
            if (pageClass != className) continue;
            page = candidate;
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

    private NamedRect[] PartsOf(nint page, string pageClass, Vector2 origin, long now)
    {
        if (!PartNames.TryGetValue(pageClass, out var wanted)) return [];
        List<NamedRect> parts = [];
        foreach (var name in wanted)
        {
            var each = name.EndsWith("[]");
            var childName = each ? name[..^2] : name;
            var key = (page, childName);
            if (!_partWidgets.TryGetValue(key, out var cached) || cached.Widget != 0 && !NameMatches(cached.Widget, childName[(childName.LastIndexOf('/') + 1)..]) || cached.Widget == 0 && now >= cached.RetryAt)
                _partWidgets[key] = cached = (FindPart(page, childName), now + Offsets.RectChildRetryMilliseconds);
            if (cached.Widget == 0) continue;
            if (!each)
            {
                if (Visible(cached.Widget) && Geometry(cached.Widget, origin) is { } rect)
                    parts.Add(new NamedRect(name, rect.Min, rect.Max));
                continue;
            }

            if (!Visible(cached.Widget)) continue;
            var index = 0;
            foreach (var item in PanelContents(cached.Widget, Offsets.MaxWidgetNodes))
                if (Visible(item) && Geometry(item, origin) is { } itemRect)
                    parts.Add(new NamedRect($"{childName}[{index++}]", itemRect.Min, itemRect.Max));
        }
        return parts.Count == 0 ? [] : [.. parts];
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

    private static (string BaseName, uint Number) SplitName(string childName)
    {
        var split = childName.LastIndexOf('_');
        if (split > 0 && childName.AsSpan(split + 1) is { Length: > 0 } digits && digits.IndexOfAnyExceptInRange('0', '9') < 0 &&
            (digits.Length == 1 || digits[0] != '0') && uint.TryParse(digits, out var suffix) && suffix < uint.MaxValue)
            return (childName[..split], suffix + 1);
        return (childName, 0);
    }

    private bool NameMatches(nint widget, string childName)
    {
        var (baseName, number) = SplitName(childName);
        return memory.TryRead(widget + Offsets.UObjectName, out uint index) && memory.TryRead(widget + Offsets.UObjectName + Offsets.FNameNumber, out uint nameNumber) &&
               nameNumber == number && names.TryRead(index, out var found) && found == baseName;
    }

    internal nint FindChild(nint page, string childName) => FindWidget(page, Named(childName));

    private nint FindPart(nint page, string path)
    {
        var slash = path.IndexOf('/');
        if (slash < 0) return FindChild(page, path);
        var scope = FindChild(page, path[..slash]);
        return scope == 0 ? 0 : FindWidget(scope, Named(path[(slash + 1)..]), fromWidget: true);
    }

    private Func<nint, bool> Named(string childName)
    {
        var (baseName, number) = SplitName(childName);
        return widget =>
            memory.TryRead(widget + Offsets.UObjectName, out uint nameIndex) && memory.TryRead(widget + Offsets.UObjectName + Offsets.FNameNumber, out uint nameNumber) &&
            nameNumber == number && names.TryRead(nameIndex, out var widgetName) && widgetName == baseName;
    }

    private nint FindWidget(nint page, Func<nint, bool> matches, bool fromWidget = false)
    {
        Queue<(nint Widget, int Depth)> queue = [];
        HashSet<nint> visited = [];
        Enqueue(fromWidget ? page : WidgetTreeRoot(page), 0);
        while (queue.TryDequeue(out var next) && visited.Count < Offsets.MaxWidgetNodes)
        {
            var (widget, depth) = next;
            if (matches(widget)) return widget;
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

    private bool Visible(nint widget) => VisibleOrNull(widget) == true;

    private bool? VisibleOrNull(nint widget) =>
        memory.TryRead(widget + Offsets.UWidgetVisibility, out byte visibility) ? visibility is 0 or 3 or 4 : null;

    private (Vector2 Min, Vector2 Max)? Geometry(nint widget, Vector2 origin) =>
        _slate.TryWeak(widget + Offsets.UWidgetSlate, out var slate) && _slate.TryRect(slate, origin, out var rect) ? rect : null;
}
