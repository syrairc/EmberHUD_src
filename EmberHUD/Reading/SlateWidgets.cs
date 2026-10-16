using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class SlateWidgets(IReadBackend memory)
{
    public bool TryWeak(nint weakPtr, out nint target)
    {
        target = 0;
        return memory.TryReadPtr(weakPtr, out target) && target != 0 &&
               memory.TryReadPtr(weakPtr + Offsets.WeakPtrController, out var controller) && controller != 0 &&
               memory.TryRead(controller + Offsets.RefControllerShared, out int shared) && shared > 0;
    }

    public bool TryRect(nint slate, Vector2 origin, out (Vector2 Min, Vector2 Max) rect)
    {
        rect = default;
        Span<float> size = stackalloc float[2];
        Span<float> transform = stackalloc float[6];
        if (!memory.TryRead(slate + Offsets.SWidgetSize, size) || !memory.TryRead(slate + Offsets.SWidgetTransform, transform))
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
