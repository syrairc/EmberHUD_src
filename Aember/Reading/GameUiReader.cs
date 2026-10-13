using System.Collections.Concurrent;
using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal delegate bool TryName(uint index, out string name);

internal sealed partial class GameUiReader(IReadBackend memory, nint moduleBase, TryName name, Func<long>? clockMilliseconds = null)
{
    private readonly record struct PanelSpec(PanelKind Kind, string RectChild, bool FullScreen);

    private static readonly Dictionary<string, PanelSpec> Specs = Offsets.GamePanels
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry => entry.Split(',', StringSplitOptions.TrimEntries))
        .ToDictionary(f => f[0], f => new PanelSpec(Enum.Parse<PanelKind>(f[1]), f[2], f[3] == "1"));

    private static readonly HashSet<string> Hud = [.. Offsets.HudPages.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    private static readonly PanelSpec Unknown = new(PanelKind.Other, "", false);

    private readonly ConcurrentDictionary<(nint Class, string Ancestor), bool> _isA = [];
    private readonly Func<long> _clock = clockMilliseconds ?? (() => Environment.TickCount64);
    private readonly Dictionary<nint, (nint Child, long RetryAt)> _rectChildren = [];
    private readonly ConcurrentDictionary<(nint Page, string Name), nint> _found = [];
    private readonly Dictionary<string, nint> _hudChildren = [];

    public GameUi Read()
    {
        if (!TryOverlay(out var overlay, out var origin))
        {
            _rectChildren.Clear();
            _found.Clear();
            _hudChildren.Clear();
            return GameUi.Empty;
        }

        List<GamePanel> panels = [];
        HashSet<nint> seen = [];
        foreach (var root in Children(overlay + Offsets.OverlayChildren, Offsets.MaxViewportRoots))
        {
            if (!TryUserWidget(root, out var widget) || !TryClassName(widget, out var className)) continue;
            var page = widget;
            var pageClass = className;
            if (className == Offsets.PageHostClassName)
            {
                if (!TryHostedPage(widget, out page) || !TryClassName(page, out pageClass)) continue;
            }
            else if (!Specs.ContainsKey(pageClass)) continue;

            if (seen.Contains(page) || Hud.Contains(pageClass) || !Visible(widget) || !Visible(page)) continue;
            var spec = Specs.GetValueOrDefault(pageClass, Unknown);
            if (RectOf(page, spec.RectChild, origin) is not { } rect) continue;
            seen.Add(page);
            panels.Add(new GamePanel(pageClass, spec.Kind, rect.Min, rect.Max, spec.FullScreen) { Address = page });
        }

        foreach (var gone in _rectChildren.Keys.Where(page => !seen.Contains(page)).ToArray())
            _rectChildren.Remove(gone);
        foreach (var gone in _found.Keys.Where(key => !seen.Contains(key.Page)))
            _found.TryRemove(gone, out _);
        return panels.Count == 0 ? GameUi.Empty : new GameUi([.. panels]);
    }

    public (Vector2 Min, Vector2 Max)? FindRect(nint page, string childName)
    {
        if (!TryOverlay(out _, out var origin)) return null;
        var key = (page, childName);
        if (_found.TryGetValue(key, out var cached))
        {
            if (Geometry(cached, origin) is { } cachedRect) return Visible(cached) ? cachedRect : null;
            _found.TryRemove(key, out _);
        }

        var child = FindChild(page, childName);
        if (child == 0 || Geometry(child, origin) is not { } rect) return null;
        _found[key] = child;
        return Visible(child) ? rect : null;
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
            if (!TryUserWidget(root, out var page) || !TryClassName(page, out var pageClass)) continue;
            if (pageClass == Offsets.PageHostClassName && (!TryHostedPage(page, out page) || !TryClassName(page, out pageClass))) continue;
            if (pageClass != Offsets.HudPageClassName) continue;
            hud = page;
            return true;
        }
        return false;
    }

    private bool NameIs(nint widget, string childName) =>
        memory.TryRead(widget + Offsets.UObjectName, out uint index) && name(index, out var found) && found == childName;

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

    private bool TryOverlay(out nint overlay, out Vector2 origin)
    {
        overlay = 0;
        origin = default;
        if (!memory.TryReadPtr(moduleBase + Offsets.GEngineGlobalRva, out var engine) ||
            !memory.TryReadPtr(engine + Offsets.EngineGameViewport, out var viewport) ||
            !TryWeak(viewport + Offsets.ViewportOverlay, out overlay) ||
            !TrySlateRect(overlay, Vector2.Zero, out var rect))
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

    private IEnumerable<nint> Children(nint panelChildren, int max)
    {
        if (!memory.TryReadPtr(panelChildren + Offsets.PanelChildrenSlots, out var data) || data == 0 ||
            !memory.TryRead(panelChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum, out int count) || count <= 0 || count > Offsets.MaxViewportRoots ||
            !memory.TryReadArray(data, Math.Min(count, max), out long[] slots))
            yield break;

        foreach (var slot in slots)
            if (slot != 0 && memory.TryReadPtr((nint)slot + Offsets.SlotWidget, out var widget) && widget != 0)
                yield return widget;
    }

    private IEnumerable<nint> PanelContents(nint panel, int max)
    {
        if (!memory.TryReadPtr(panel + Offsets.PanelSlots, out var data) || data == 0 ||
            !memory.TryRead(panel + Offsets.PanelSlots + Offsets.TArrayNum, out int count) || count <= 0 || count > Offsets.MaxWidgetNodes ||
            !memory.TryReadArray(data, Math.Min(count, max), out long[] slots))
            yield break;

        foreach (var slot in slots)
            if (slot != 0 && memory.TryReadPtr((nint)slot + Offsets.PanelSlotContent, out var content) && content != 0)
                yield return content;
    }

    private nint FindChild(nint page, string childName)
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
        Enqueue(Root(page), 0);
        while (queue.TryDequeue(out var next) && visited.Count < Offsets.MaxWidgetNodes)
        {
            var (widget, depth) = next;
            if (memory.TryRead(widget + Offsets.UObjectName, out uint nameIndex) && memory.TryRead(widget + Offsets.UObjectName + Offsets.FNameNumber, out uint nameNumber) &&
                nameNumber == number && name(nameIndex, out var widgetName) && widgetName == baseName)
                return widget;
            if (depth >= Offsets.MaxWidgetDepth || !memory.TryReadPtr(widget + Offsets.UObjectClass, out var cls)) continue;
            if (IsA(cls, Offsets.PanelWidgetClassName))
                foreach (var child in PanelContents(widget, Offsets.MaxWidgetNodes)) Enqueue(child, depth + 1);
            else if (IsA(cls, Offsets.UserWidgetClassName))
                Enqueue(Root(widget), depth + 1);
        }
        return 0;

        void Enqueue(nint widget, int depth)
        {
            if (widget != 0 && visited.Add(widget)) queue.Enqueue((widget, depth));
        }
    }

    private nint Root(nint userWidget) =>
        memory.TryReadPtr(userWidget + Offsets.UserWidgetTree, out var tree) && tree != 0 &&
        memory.TryReadPtr(tree + Offsets.WidgetTreeRoot, out var root) ? root : 0;

    private bool IsA(nint cls, string ancestor) => _isA.GetOrAdd((cls, ancestor), key =>
    {
        var at = key.Class;
        for (var depth = 0; at != 0 && depth < Offsets.MaxWidgetDepth * 4; depth++)
        {
            if (memory.TryRead(at + Offsets.UObjectName, out uint index) && name(index, out var className) && className == key.Ancestor) return true;
            if (!memory.TryReadPtr(at + Offsets.UStructSuper, out at)) return false;
        }
        return false;
    });

    private bool TryClassName(nint obj, out string className)
    {
        className = "";
        return memory.TryReadPtr(obj + Offsets.UObjectClass, out var cls) && cls != 0 &&
               memory.TryRead(cls + Offsets.UObjectName, out uint index) && name(index, out className);
    }

    private bool Visible(nint widget) =>
        memory.TryRead(widget + Offsets.UWidgetVisibility, out byte visibility) && visibility is 0 or 3 or 4;

    private (Vector2 Min, Vector2 Max)? Geometry(nint widget, Vector2 origin) =>
        TryWeak(widget + Offsets.UWidgetSlate, out var slate) && TrySlateRect(slate, origin, out var rect) ? rect : null;

    private bool TryWeak(nint weakPtr, out nint target)
    {
        target = 0;
        return memory.TryReadPtr(weakPtr, out target) && target != 0 &&
               memory.TryReadPtr(weakPtr + Offsets.WeakPtrController, out var controller) && controller != 0 &&
               memory.TryRead(controller + Offsets.RefControllerShared, out int shared) && shared > 0;
    }

    private bool TrySlateRect(nint slate, Vector2 origin, out (Vector2 Min, Vector2 Max) rect)
    {
        rect = default;
        if (!memory.TryReadArray(slate + Offsets.SWidgetSize, 2, out float[] size) ||
            !memory.TryReadArray(slate + Offsets.SWidgetTransform, 6, out float[] transform))
            return false;

        var min = new Vector2(transform[4], transform[5]) - origin;
        var extent = new Vector2(size[0] * transform[0], size[1] * transform[3]);
        if (!float.IsFinite(min.X) || !float.IsFinite(min.Y) || !float.IsFinite(extent.X) || !float.IsFinite(extent.Y) ||
            extent.X <= 0 || extent.Y <= 0 || extent.X > Offsets.MaxPanelPixels || extent.Y > Offsets.MaxPanelPixels ||
            MathF.Abs(min.X) > Offsets.MaxPanelPixels || MathF.Abs(min.Y) > Offsets.MaxPanelPixels)
            return false;

        rect = (min, min + extent);
        return true;
    }
}
