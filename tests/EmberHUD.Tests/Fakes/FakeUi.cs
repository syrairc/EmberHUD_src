using EmberHUD.Host;

namespace EmberHUD.Tests;

internal sealed class FakeUi
{
    private readonly FakeWorld _world;
    private readonly long _overlaySlots;
    private readonly List<long> _roots = [];
    private readonly long _panelWidget;
    private readonly long _userWidget;

    public FakeUi(FakeWorld world, Vector2? origin = null)
    {
        _world = world;
        world.Memory.Map(FakeWorld.ModuleBase + Offsets.GEngineGlobalRva, 8);
        var engine = world.Alloc(0x1000);
        var viewport = world.Alloc(0x1000);
        world.Memory.Write(FakeWorld.ModuleBase + Offsets.GEngineGlobalRva, engine);
        world.Memory.Write(engine + Offsets.EngineGameViewport, viewport);
        Overlay = Slate(origin ?? Vector2.Zero, new Vector2(3440, 1440));
        WeakPtr(viewport + Offsets.ViewportOverlay, Overlay);
        _overlaySlots = world.Alloc(8 * Offsets.MaxViewportRoots);
        world.Memory.Write(Overlay + Offsets.OverlayChildren + Offsets.PanelChildrenSlots, _overlaySlots);
        _panelWidget = world.AddClass(Offsets.PanelWidgetClassName);
        _userWidget = world.AddClass(Offsets.UserWidgetClassName);
        Engine = engine;
    }

    public long Engine { get; }

    public long Overlay { get; }

    public long Slate(Vector2 position, Vector2 size, float scale = 1)
    {
        var widget = _world.Alloc(0x1000);
        _world.Memory.WriteArray(widget + Offsets.SWidgetSize, new[] { size.X, size.Y });
        _world.Memory.WriteArray(widget + Offsets.SWidgetTransform, new[] { scale, 0, 0, scale, position.X, position.Y });
        return widget;
    }

    public void WeakPtr(long at, long target, int shared = 1)
    {
        var controller = _world.Alloc(0x40);
        _world.Memory.Write(controller + Offsets.RefControllerShared, shared);
        _world.Memory.Write(at, target);
        _world.Memory.Write(at + Offsets.WeakPtrController, controller);
    }

    public long UserClass(string name) => SubClass(name, _userWidget);

    public long PanelClass(string name) => SubClass(name, _panelWidget);

    public long Widget(long cls, string name, Vector2 position, Vector2 size, byte visibility = 4, uint nameNumber = 0)
    {
        var widget = _world.AddObject(cls, name, nameNumber);
        _world.Memory.Write(widget + Offsets.UWidgetVisibility, visibility);
        WeakPtr(widget + Offsets.UWidgetSlate, Slate(position, size));
        return widget;
    }

    public void AddRoot(long userWidget)
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

    public long Host(long page)
    {
        var host = Widget(UserClass(Offsets.PageHostClassName), "UIPageHost_C_1", Vector2.Zero, new Vector2(3440, 1440));
        var content = Widget(PanelClass("NamedSlot"), "ContentHost", Vector2.Zero, new Vector2(3440, 1440));
        _world.Memory.Write(host + Offsets.PageHostContent, content);
        SetChildren(content, page);
        return host;
    }

    public void SetChildren(long panel, params long[] children)
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

    public void SetTree(long userWidget, long root)
    {
        var tree = _world.Alloc(0x40);
        _world.Memory.Write(tree + Offsets.WidgetTreeRoot, root);
        _world.Memory.Write(userWidget + Offsets.UserWidgetTree, tree);
    }

    public void Text(long widget, int offset, string text)
    {
        var chars = _world.Alloc((text.Length + 1) * 2);
        _world.Memory.WriteArray(chars, System.Text.Encoding.Unicode.GetBytes(text + "\0"));
        var data = _world.Alloc(0x40);
        _world.Memory.Write(data + Offsets.TextDataString, chars);
        _world.Memory.Write(data + Offsets.TextDataString + Offsets.FStringNum, text.Length + 1);
        _world.Memory.Write(widget + offset, data);
    }

    private long SubClass(string name, long super)
    {
        var cls = _world.AddClass(name);
        _world.Memory.Write(cls + Offsets.UStructSuper, super);
        return cls;
    }

    private long Slot(long widget)
    {
        var slot = _world.Alloc(0x40);
        _world.Memory.Write(slot + Offsets.SlotWidget, widget);
        return slot;
    }
}
