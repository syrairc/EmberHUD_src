using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private readonly nint[] _skillSlots = new nint[Offsets.SkillSlotCount];
    private long _skillSlotsRetryAt;

    public ScreenRect?[]? ReadSkillSlots()
    {
        if (!TryOverlay(out _, out var origin) || !TryRootPage(Offsets.PadPageClassName, out var pad) || !Visible(pad)) return null;
        var now = _clock();
        var slots = new ScreenRect?[Offsets.SkillSlotCount];
        for (var i = 0; i < slots.Length; i++)
        {
            var name = Offsets.SkillSlotPrefix + i;
            if (!NameIs(_skillSlots[i], name))
            {
                _skillSlots[i] = 0;
                if (now < _skillSlotsRetryAt) continue;
                _skillSlots[i] = FindChild(pad, name);
            }
            if (_skillSlots[i] != 0 && Visible(_skillSlots[i]) && Geometry(_skillSlots[i], origin) is { } rect)
                slots[i] = new ScreenRect(rect.Min, rect.Max);
        }
        if (now >= _skillSlotsRetryAt) _skillSlotsRetryAt = now + Offsets.RectChildRetryMilliseconds;
        return slots;
    }
}
