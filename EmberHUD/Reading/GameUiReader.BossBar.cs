using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private nint _bossView;
    private nint _bossBar;
    private long _bossBarRetryAt;

    public BossBar? ReadBossBar()
    {
        if (!TryOverlay(out _, out var origin)) return null;
        if (!BossBarCached()) FindBossBar();
        if (_bossBar == 0 || !Visible(_bossView) || !Visible(_bossBar) || BossBarPercent() is not { } percent || Geometry(_bossBar, origin) is not { } rect)
            return null;

        return new BossBar(new ScreenRect(rect.Min, rect.Max), percent);
    }

    private float? BossBarPercent() =>
        memory.TryRead(_bossBar + Offsets.BossBarPercent, out int bits) && BitConverter.Int32BitsToSingle(bits) is var percent and >= 0 and <= 1 ? percent : null;

    private bool BossBarCached() =>
        _bossBar != 0 && names.TryReadClassName(_bossView, out var viewClass) && viewClass == Offsets.BossBarViewClassName && NameIs(_bossBar, Offsets.BossBarTrack);

    private void FindBossBar()
    {
        _bossView = _bossBar = 0;
        var now = _clock();
        if (now < _bossBarRetryAt) return;
        _bossBarRetryAt = now + Offsets.RectChildRetryMilliseconds;
        if (!TryHud(out var hud)) return;
        foreach (var item in PanelContents(WidgetTreeRoot(hud), Offsets.MaxWidgetNodes))
        {
            if (!names.TryReadClassName(item, out var itemClass) || itemClass != Offsets.BossBarViewClassName) continue;
            var bar = FindChild(item, Offsets.BossBarTrack);
            if (bar == 0) continue;
            (_bossView, _bossBar) = (item, bar);
            return;
        }
    }
}
