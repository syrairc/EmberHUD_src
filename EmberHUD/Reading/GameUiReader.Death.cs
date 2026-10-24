using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private (nint Page, nint Switcher) _deathToken;
    private long _deathTokenRetryAt;

    public bool? ReadDeathToken(nint page)
    {
        if (_deathToken.Page != page || _deathToken.Switcher == 0 || !NameIs(_deathToken.Switcher, Offsets.DeathTokenWidget)) FindDeathToken(page);
        return _deathToken.Switcher != 0 && memory.TryRead(_deathToken.Switcher + Offsets.SwitcherActiveIndex, out int index) && index is 0 or 1 ? index == 1 : null;
    }

    private void FindDeathToken(nint page)
    {
        _deathToken = (page, 0);
        var now = _clock();
        if (now < _deathTokenRetryAt) return;
        _deathTokenRetryAt = now + Offsets.RectChildRetryMilliseconds;
        _deathToken = (page, FindChild(page, Offsets.DeathTokenWidget));
    }
}
