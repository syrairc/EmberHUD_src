using Hexa.NET.ImGui;

namespace EmberHUD.Tests;

internal static class ImGuiScene
{
    static readonly Lazy<ImGuiStyle> Captured = new(CaptureStyle);

    public static unsafe void ResetStyle() => *ImGui.GetStyle().Handle = Captured.Value;

    static unsafe ImGuiStyle CaptureStyle()
    {
        var main = ImGui.GetCurrentContext();
        var scratch = ImGui.CreateContext();
        ImGui.SetCurrentContext(scratch);
        var style = *ImGui.GetStyle().Handle;
        ImGui.SetCurrentContext(main);
        ImGui.DestroyContext(scratch);
        return style;
    }

    public static List<ImDrawVert> Vertices()
    {
        var data = ImGui.GetDrawData();
        var vertices = new List<ImDrawVert>();
        for (var n = 0; n < data.CmdListsCount; n++)
        {
            var list = data.CmdLists[n];
            for (var i = 0; i < list.VtxBuffer.Size; i++) vertices.Add(list.VtxBuffer[i]);
        }

        return vertices;
    }

    public static void Click(Action frame, Vector2 at)
    {
        var io = ImGui.GetIO();
        io.AddMousePosEvent(at.X, at.Y);
        frame();
        io.AddMouseButtonEvent(0, true);
        frame();
        io.AddMouseButtonEvent(0, false);
        frame();
        io.AddMousePosEvent(-10000, -10000);
        frame();
        frame();
    }

    public static void Drag(Action frame, Vector2 from, Vector2 to)
    {
        var io = ImGui.GetIO();
        io.AddMousePosEvent(from.X, from.Y);
        frame();
        io.AddMouseButtonEvent(0, true);
        frame();
        io.AddMousePosEvent(to.X, to.Y);
        frame();
        frame();
        io.AddMouseButtonEvent(0, false);
        frame();
        io.AddMousePosEvent(-10000, -10000);
        frame();
    }

    public readonly record struct Quad(Vector2 Min, Vector2 Max, uint Col, bool Glyph)
    {
        public Vector2 Centre => (Min + Max) / 2;

        public float Width => Max.X - Min.X;

        public float Height => Max.Y - Min.Y;

        public bool Contains(Quad other) => other.Centre.X >= Min.X && other.Centre.X <= Max.X && other.Centre.Y >= Min.Y && other.Centre.Y <= Max.Y;

        public static List<Quad> Capture()
        {
            var quads = new List<Quad>();
            var data = ImGui.GetDrawData();
            for (var i = 0; i < data.CmdListsCount; i++)
            {
                var list = data.CmdLists[i];
                var index = list.IdxBuffer;
                for (var at = 0; at < index.Size;)
                {
                    if (at + 6 <= index.Size && IsQuad(index, at))
                    {
                        var corners = Enumerable.Range(0, 4).Select(corner => list.VtxBuffer[index[at] + corner]).ToArray();
                        quads.Add(new Quad(
                            new Vector2(corners.Min(corner => corner.Pos.X), corners.Min(corner => corner.Pos.Y)),
                            new Vector2(corners.Max(corner => corner.Pos.X), corners.Max(corner => corner.Pos.Y)),
                            corners[0].Col,
                            corners[0].Uv != corners[2].Uv));
                        at += 6;
                    }
                    else at += 3;
                }
            }
            return quads;
        }

        static bool IsQuad(ImVector<ushort> index, int at) =>
            index[at + 1] == index[at] + 1 && index[at + 2] == index[at] + 2 && index[at + 3] == index[at] && index[at + 4] == index[at] + 2 && index[at + 5] == index[at] + 3;
    }
}
