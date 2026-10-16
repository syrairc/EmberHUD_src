using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class QuestTargetReader(IReadBackend memory)
{
    public QuestTarget[] Read(MinimapReader minimap)
    {
        var widget = minimap.Widget;
        if (widget == 0 || !memory.TryRead(widget + Offsets.MinimapIconArea, out uint area) ||
            !memory.TryReadTMapElements(widget + Offsets.MinimapQuestTargets, Offsets.QuestTargetStride, Offsets.MaxQuestTargets, out _, out var bytes, out var bits))
            return [];

        List<QuestTarget> targets = [];
        for (var i = 0; i < bytes.Length / Offsets.QuestTargetStride; i++)
        {
            var at = i * Offsets.QuestTargetStride;
            var position = new Vector2(BitConverter.ToSingle(bytes, at + Offsets.QuestTargetX), BitConverter.ToSingle(bytes, at + Offsets.QuestTargetY));
            if (ReadExtensions.IsAllocated(bits, i) && BitConverter.ToUInt32(bytes, at + Offsets.QuestTargetArea) == area && Plausible(position))
                targets.Add(new QuestTarget(BitConverter.ToInt32(bytes, at + Offsets.QuestTargetId), position));
        }

        return [.. targets];
    }

    private static bool Plausible(Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        MathF.Abs(position.X) <= Offsets.MaxIconCoordinate && MathF.Abs(position.Y) <= Offsets.MaxIconCoordinate;
}
