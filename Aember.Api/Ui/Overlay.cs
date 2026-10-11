using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;

namespace Aember.Ui;

public sealed class Overlay
{
    public bool Locked;
    public bool MoveHeld;
    public bool InteractHeld;

    public bool Movable => !Locked || MoveHeld;

    string? _dragId, _resizeId;
    ResizeEdge _resizeEdge;
    Vector2 _dragOffset;
    const float Edge = 6f;

    [Flags]
    public enum ResizeEdge
    {
        None = 0,
        Right = 1,
        Bottom = 2,
        Corner = Right | Bottom,
    }

    public static ResizeEdge HitEdge(Vector2 mouse, Vector2 origin, Vector2 size, ResizeEdge enabled, float edge = Edge)
    {
        var inside = mouse.X >= origin.X && mouse.X <= origin.X + size.X && mouse.Y >= origin.Y && mouse.Y <= origin.Y + size.Y;
        if (!inside) return ResizeEdge.None;
        var nearR = mouse.X >= origin.X + size.X - edge;
        var nearB = mouse.Y >= origin.Y + size.Y - edge;
        var canR = enabled.HasFlag(ResizeEdge.Right);
        var canB = enabled.HasFlag(ResizeEdge.Bottom);
        if (nearR && nearB && canR && canB) return ResizeEdge.Corner;
        if (nearR && canR) return ResizeEdge.Right;
        if (nearB && canB) return ResizeEdge.Bottom;
        return ResizeEdge.None;
    }

    public static (bool hovered, bool onEdge) HitTest(Vector2 mouse, Vector2 origin, Vector2 size, float edge = Edge)
    {
        var hovered = mouse.X >= origin.X && mouse.X <= origin.X + size.X && mouse.Y >= origin.Y && mouse.Y <= origin.Y + size.Y;
        return (hovered, hovered && mouse.X >= origin.X + size.X - edge);
    }

    public (bool hovered, ResizeEdge edge, bool active) Handle(string id, ref Vector2 origin, Vector2 size, ref int posX, ref int posY, ref int width, int minWidth = 60, int maxWidth = 4000)
    {
        var noHeight = 0;
        return Handle(id, ref origin, size, ref posX, ref posY, ref width, minWidth, maxWidth, ref noHeight, 0, 0, ResizeEdge.Right);
    }

    public (bool hovered, ResizeEdge edge, bool active) Handle(string id, ref Vector2 origin, Vector2 size, ref int posX, ref int posY, ref int width, int minWidth, int maxWidth, ref int height, int minHeight, int maxHeight, ResizeEdge resizable = ResizeEdge.Corner)
    {
        if (!Movable && _dragId != id && _resizeId != id) return (false, ResizeEdge.None, false);

        var mouse = ImGui.GetMousePos();
        var (hovered, _) = HitTest(mouse, origin, size);
        var edge = HitEdge(mouse, origin, size, resizable);
        var shown = _resizeId == id ? _resizeEdge : edge;
        if (shown == ResizeEdge.Corner) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
        else if (shown == ResizeEdge.Right) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        else if (shown == ResizeEdge.Bottom) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);

        if (_dragId is null && _resizeId is null && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (edge != ResizeEdge.None)
            {
                _resizeId = id;
                _resizeEdge = edge;
            }
            else
            {
                _dragId = id;
                _dragOffset = mouse - origin;
            }
        }
        if (_dragId == id)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                origin = mouse - _dragOffset;
                posX = (int)Math.Round(origin.X);
                posY = (int)Math.Round(origin.Y);
            }
            else _dragId = null;
        }
        if (_resizeId == id)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (_resizeEdge.HasFlag(ResizeEdge.Right)) width = Math.Clamp((int)Math.Round(mouse.X - origin.X), minWidth, maxWidth);
                if (_resizeEdge.HasFlag(ResizeEdge.Bottom)) height = Math.Clamp((int)Math.Round(mouse.Y - origin.Y), minHeight, maxHeight);
            }
            else _resizeId = null;
        }
        return (hovered, shown, _dragId == id || _resizeId == id);
    }

    public (bool hovered, bool onEdge, bool active) HandleCenter(string id, ref Vector2 min, Vector2 size, ref int posX, ref int posY, ref int width, int minWidth = 60, int maxWidth = 4000)
    {
        if (Locked)
        {
            Release(id);
            return (false, false, false);
        }

        var mouse = ImGui.GetMousePos();
        var (hovered, onEdge) = HitTest(mouse, min, size);
        if (onEdge || _resizeId == id) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

        if (_dragId is null && _resizeId is null && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (onEdge) _resizeId = id;
            else
            {
                _dragId = id;
                _dragOffset = mouse - new Vector2(posX, posY);
            }
        }
        if (_dragId == id)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var c = mouse - _dragOffset;
                posX = (int)Math.Round(c.X);
                posY = (int)Math.Round(c.Y);
                min = new Vector2(posX - size.X / 2f, posY);
            }
            else _dragId = null;
        }
        if (_resizeId == id)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) width = Math.Clamp((int)Math.Round(2 * (mouse.X - posX)), minWidth, maxWidth);
            else _resizeId = null;
        }
        return (hovered, onEdge, _dragId == id || _resizeId == id);
    }

    public void Release(string id)
    {
        if (_dragId == id) _dragId = null;
        if (_resizeId == id) _resizeId = null;
    }

    public static void Chrome(ImDrawListPtr dl, Vector2 min, Vector2 max, Color background, Color border, float thickness = 1f, float radius = 0f)
    {
        if (background.A > 0) dl.AddRectFilled(min, max, EColor.U32(background), radius);
        if (thickness > 0) dl.AddRect(min, max, EColor.U32(border), radius, ImDrawFlags.None, thickness);
    }

    public static void ClickBlocker(string id, Vector2 pos, Vector2 size)
    {
        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.Begin($"##eidrag_{ImGui.GetID(id):X8}", flags);
        ImGui.End();
    }

    public static void DragHint(Vector2 min, Vector2 max, bool active, bool hovered, ResizeEdge edge)
    {
        var hint = active || hovered ? Color.FromArgb(220, 120, 220, 255) : Color.FromArgb(120, 120, 220, 255);
        var dl = ImGui.GetForegroundDrawList();
        dl.AddRect(min, max, EColor.U32(hint), 0f, ImDrawFlags.None, 1.5f);
        var grip = EColor.U32(Color.FromArgb(255, 120, 220, 255));
        if (edge.HasFlag(ResizeEdge.Right)) dl.AddLine(new Vector2(max.X, min.Y), max, grip, 3f);
        if (edge.HasFlag(ResizeEdge.Bottom)) dl.AddLine(new Vector2(min.X, max.Y), max, grip, 3f);
    }
}
