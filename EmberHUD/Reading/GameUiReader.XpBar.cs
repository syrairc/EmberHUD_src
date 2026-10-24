using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private nint _xpView;
    private nint _xpBar;
    private long _xpBarRetryAt;

    public ScreenRect? ReadXpBar()
    {
        if (!TryOverlay(out _, out var origin)) return null;
        if (!XpBarCached()) FindXpBar();
        if (_xpBar == 0 || !Visible(_xpView) || !Visible(_xpBar) || Geometry(_xpBar, origin) is not { } rect)
            return null;

        return new ScreenRect(rect.Min, rect.Max);
    }

    private bool XpBarCached() =>
        _xpBar != 0 && names.TryReadClassName(_xpView, out var viewClass) && viewClass == Offsets.XpBarViewClassName && NameIs(_xpBar, Offsets.XpBarTrack);

    private void FindXpBar()
    {
        _xpView = _xpBar = 0;
        var now = _clock();
        if (now < _xpBarRetryAt) return;
        _xpBarRetryAt = now + Offsets.RectChildRetryMilliseconds;
        if (!TryHud(out var hud)) return;
        var view = FindWidget(hud, widget => names.TryReadClassName(widget, out var widgetClass) && widgetClass == Offsets.XpBarViewClassName);
        if (view == 0) return;
        var bar = FindChild(view, Offsets.XpBarTrack);
        if (bar != 0) (_xpView, _xpBar) = (view, bar);
    }
}
