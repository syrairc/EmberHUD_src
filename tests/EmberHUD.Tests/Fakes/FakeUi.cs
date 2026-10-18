using EmberHUD.Host;

namespace EmberHUD.Tests;

internal sealed class FakeUi
{
    private readonly FakeWorld _world;
    private readonly nint _overlaySlots;
    private readonly List<nint> _roots = [];
    private readonly nint _panelWidget;
    private readonly nint _userWidget;

    public FakeUi(FakeWorld world, Vector2? origin = null)
    {
        _world = world;
        world.Memory.Map(FakeWorld.Module + Offsets.GEngineGlobalRva, 8);
        var engine = world.Alloc(0x1000);
        var viewport = world.Alloc(0x1000);
        world.Memory.Write(FakeWorld.Module + Offsets.GEngineGlobalRva, engine);
        world.Memory.Write(engine + Offsets.EngineGameViewport, viewport);
        Overlay = Slate(origin ?? Vector2.Zero, new Vector2(3440, 1440));
        WeakPtr(viewport + Offsets.ViewportOverlay, Overlay);
        _overlaySlots = world.Alloc(8 * Offsets.MaxViewportRoots);
        world.Memory.Write(Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots, _overlaySlots);
        _panelWidget = world.AddClass(Offsets.PanelWidgetClassName);
        _userWidget = world.AddClass(Offsets.UserWidgetClassName);
        Engine = engine;
    }

    public nint Engine { get; }

    public nint ChatHost { get; private set; }

    public nint Overlay { get; }

    public nint Slate(Vector2 position, Vector2 size, float scale = 1, float? scaleY = null)
    {
        var widget = _world.Alloc(0x1000);
        _world.Memory.WriteArray(widget + Offsets.SWidgetSize, new[] { size.X, size.Y });
        _world.Memory.WriteArray(widget + Offsets.SWidgetTransform, new[] { scale, 0, 0, scaleY ?? scale, position.X, position.Y });
        return widget;
    }

    public void WeakPtr(nint at, nint target, int shared = 1)
    {
        var controller = _world.Alloc(0x40);
        _world.Memory.Write(controller + Offsets.RefControllerShared, shared);
        _world.Memory.Write(at, target);
        _world.Memory.Write(at + Offsets.WeakPtrController, controller);
    }

    public nint UserClass(string name) => SubClass(name, _userWidget);

    public nint PanelClass(string name) => SubClass(name, _panelWidget);

    public nint Widget(nint cls, string name, Vector2 position, Vector2 size, byte visibility = 4, uint nameNumber = 0)
    {
        var widget = _world.AddObject(cls, name, nameNumber);
        _world.Memory.Write(widget + Offsets.UWidgetVisibility, visibility);
        WeakPtr(widget + Offsets.UWidgetSlate, Slate(position, size));
        return widget;
    }

    public (nint Component, nint Widget, nint Track) AddHpBar(FakeEntity monster, string widgetClass, int canvasOffset, int trackOffset, Vector2 position, Vector2 size, float scaleX, float scaleY, byte widgetVisibility = 4, byte trackVisibility = 3, byte canvasVisibility = 3)
    {
        var component = _world.AddComponents(monster.Value, Offsets.HpBarComponentClassName)[0];
        _world.Memory.Write(component + Offsets.ComponentEntity, monster.Value);
        var widget = _world.AddObject(_world.AddClass(widgetClass), "HpBar");
        _world.Memory.Write(widget + Offsets.UWidgetVisibility, widgetVisibility);
        var track = Widget(_world.AddClass("Image"), "Track", position, size, trackVisibility);
        WeakPtr(track + Offsets.UWidgetSlate, Slate(position, size, scaleX, scaleY));
        var canvas = Widget(_world.AddClass("CanvasPanel"), "Canvas", position, size, canvasVisibility);
        _world.Memory.Write(widget + canvasOffset, canvas);
        _world.Memory.Write(widget + trackOffset, track);
        _world.SetObjectSerial(widget, 5);
        _world.Memory.Write(component + Offsets.HpBarWidgetWeak, _world.ObjectIndex(widget));
        _world.Memory.Write(component + Offsets.HpBarWidgetWeak + Offsets.WeakPtrSerial, 5);
        return (component, widget, track);
    }

    public void AddRoot(nint userWidget)
    {
        var objectWidget = _world.Alloc(0x1000);
        _world.Memory.Write(objectWidget + Offsets.ObjectWidgetObject, userWidget);
        var canvas = _world.Alloc(0x1000);
        var canvasSlots = _world.Alloc(8);
        _world.Memory.Write(canvas + Offsets.CanvasChildren + Offsets.PanelChildrenSlots, canvasSlots);
        _world.Memory.Write(canvas + Offsets.CanvasChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum, 1);
        _world.Memory.Write(canvasSlots, Slot(objectWidget));
        _world.Memory.Write(_overlaySlots + 8 * _roots.Count, Slot(canvas));
        _roots.Add(userWidget);
        _world.Memory.Write(Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots + Offsets.TArrayNum, _roots.Count);
    }

    public nint Host(nint page)
    {
        var host = Widget(UserClass(Offsets.PageHostClassName), "UIPageHost_C_1", Vector2.Zero, new Vector2(3440, 1440));
        var content = Widget(PanelClass("NamedSlot"), "ContentHost", Vector2.Zero, new Vector2(3440, 1440));
        _world.Memory.Write(host + Offsets.PageHostContent, content);
        SetChildren(content, page);
        return host;
    }

    public void SetChildren(nint panel, params nint[] children)
    {
        var slots = _world.Alloc(8 * Math.Max(1, children.Length));
        for (var i = 0; i < children.Length; i++)
        {
            var slot = _world.Alloc(0x40);
            _world.Memory.Write(slot + Offsets.PanelSlotContent, children[i]);
            _world.Memory.Write(slots + 8 * i, slot);
        }
        _world.Memory.Write(panel + Offsets.PanelSlots, slots);
        _world.Memory.Write(panel + Offsets.PanelSlots + Offsets.TArrayNum, children.Length);
    }

    public void SetTree(nint userWidget, nint root)
    {
        var tree = _world.Alloc(0x40);
        _world.Memory.Write(tree + Offsets.WidgetTreeRoot, root);
        _world.Memory.Write(userWidget + Offsets.UserWidgetTree, tree);
    }

    public void Text(nint widget, int offset, string text)
    {
        var chars = _world.Alloc((text.Length + 1) * 2);
        _world.Memory.WriteArray(chars, System.Text.Encoding.Unicode.GetBytes(text + "\0"));
        var data = _world.Alloc(0x40);
        _world.Memory.Write(data + Offsets.TextDataString, chars);
        _world.Memory.Write(data + Offsets.TextDataString + Offsets.FStringNum, text.Length + 1);
        _world.Memory.Write(widget + offset, data);
    }

    public (nint Box, nint DetailRoot) MapHud(string[] lines, bool hosted = false, string? hiddenLine = null)
    {
        var hud = Widget(UserClass(Offsets.HudPageClassName), Offsets.HudPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var hudRoot = Widget(PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(3440, 1440));
        var detail = Widget(UserClass("FightMapDetailView_C"), "FightMapDetailView_C_1", Vector2.Zero, new Vector2(400, 300));
        var detailRoot = Widget(PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(400, 300));
        var xp = Widget(PanelClass("UIShrinkTextBlock"), Offsets.MapXpWidget, Vector2.Zero, new Vector2(200, 20));
        var box = Widget(PanelClass("VerticalBox"), Offsets.MapAffixWidget, Vector2.Zero, new Vector2(400, 200));
        var richClass = UserClass("UIRichTextBlock");
        var items = lines.Select((text, i) => RichLine(richClass, i + 1, text, 4)).ToList();
        if (hiddenLine is not null) items.Add(RichLine(richClass, items.Count + 1, hiddenLine, 1));
        Text(xp, Offsets.ShrinkTextContent, "Map Base XP gain: 100%");
        SetTree(hud, hudRoot);
        SetChildren(hudRoot, detail);
        SetTree(detail, detailRoot);
        SetChildren(detailRoot, xp, box);
        SetChildren(box, [.. items]);
        AddRoot(hosted ? Host(hud) : hud);
        return (box, detailRoot);
    }

    public (nint Hud, nint View, nint Bar) AddBossBar(float percent, Vector2 position, Vector2 size, float scale = 1, byte viewVisibility = 4, byte barVisibility = 0, bool hosted = false)
    {
        var hud = Widget(UserClass(Offsets.HudPageClassName), Offsets.HudPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var hudRoot = Widget(PanelClass("CanvasPanel"), "Root", Vector2.Zero, new Vector2(3440, 1440));
        var uiView = Widget(PanelClass("CanvasPanel"), "UIVIew", Vector2.Zero, new Vector2(3440, 1440));
        var view = Widget(UserClass(Offsets.BossBarViewClassName), Offsets.BossBarViewClassName + "_1", Vector2.Zero, new Vector2(1200, 200), viewVisibility);
        var viewRoot = Widget(PanelClass("CanvasPanel"), "CanvasMain", Vector2.Zero, new Vector2(1200, 200));
        var bar = Widget(_world.AddClass("ProgressBar"), Offsets.BossBarTrack, position, size, barVisibility);
        WeakPtr(bar + Offsets.UWidgetSlate, Slate(position, size, scale));
        _world.Memory.Write(bar + Offsets.BossBarPercent, percent);
        SetTree(hud, hudRoot);
        SetChildren(hudRoot, uiView);
        SetChildren(uiView, view);
        SetTree(view, viewRoot);
        SetChildren(viewRoot, bar);
        AddRoot(hosted ? Host(hud) : hud);
        return (hud, view, bar);
    }

    public nint AddPad(int[] cfgIds, bool hosted = false)
    {
        var pad = Widget(UserClass(Offsets.PadPageClassName), Offsets.PadPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        var data = _world.Alloc(Math.Max(8, cfgIds.Length * 4));
        _world.Memory.WriteArray(data, cfgIds);
        _world.Memory.Write(pad + Offsets.SkillCfgIdsOffset, data);
        _world.Memory.Write(pad + Offsets.SkillCfgIdsOffset + Offsets.TArrayNum, cfgIds.Length);
        AddRoot(hosted ? Host(pad) : pad);
        return pad;
    }

    public nint AddChat()
    {
        var chat = Widget(UserClass(Offsets.ChatPageClassName), Offsets.ChatPageClassName + "_1", Vector2.Zero, new Vector2(3440, 1440));
        ChatHost = Host(chat);
        AddRoot(ChatHost);
        return chat;
    }

    private nint RichLine(nint richClass, int number, string text, byte visibility)
    {
        var item = Widget(richClass, $"UIRichTextBlock_{number}", Vector2.Zero, new Vector2(400, 20), visibility);
        Text(item, Offsets.TextBlockText, text);
        return item;
    }

    private nint SubClass(string name, nint super)
    {
        var cls = _world.AddClass(name);
        _world.Memory.Write(cls + Offsets.UStructSuper, super);
        return cls;
    }

    private nint Slot(nint widget)
    {
        var slot = _world.Alloc(0x40);
        _world.Memory.Write(slot + Offsets.SlotWidget, widget);
        return slot;
    }
}
