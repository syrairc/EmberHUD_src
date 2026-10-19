using System;
using System.Numerics;
using EmberHUD;
using Hexa.NET.ImGui;

namespace Threats;

static class Overdraw
{
    public static readonly Camera Looking = new(new Vector3(-1000, 0, 1000), new Vector3(-45, 0, 0), 90);

    public static float Area(ImDrawListPtr list, uint colour)
    {
        var total = 0f;
        for (var i = 0; i + 2 < list.IdxBuffer.Size; i += 3)
        {
            var a = list.VtxBuffer[list.IdxBuffer[i]];
            var b = list.VtxBuffer[list.IdxBuffer[i + 1]];
            var c = list.VtxBuffer[list.IdxBuffer[i + 2]];
            if (a.Col != colour || b.Col != colour || c.Col != colour) continue;
            total += MathF.Abs(Cross(b.Pos - a.Pos, c.Pos - a.Pos)) / 2;
        }
        return total;
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
