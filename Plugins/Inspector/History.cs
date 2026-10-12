using System.Collections.Generic;

namespace Inspector;

internal sealed class History
{
    const int Cap = 200;

    readonly List<nint> _items = [];
    int _at = -1;

    public nint Current => _at >= 0 ? _items[_at] : 0;

    public bool CanBack => _at > 0;

    public bool CanForward => _at < _items.Count - 1;

    public void Push(nint address)
    {
        if (address == Current) return;
        _items.RemoveRange(_at + 1, _items.Count - _at - 1);
        _items.Add(address);
        _at++;
        if (_items.Count <= Cap) return;
        _items.RemoveAt(0);
        _at--;
    }

    public nint Back()
    {
        if (CanBack) _at--;
        return Current;
    }

    public nint Forward()
    {
        if (CanForward) _at++;
        return Current;
    }
}
