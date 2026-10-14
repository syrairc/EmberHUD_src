using System.Reflection;
using EmberHUD.Host;

namespace EmberHUD.Tests;

internal sealed class OffsetPatch : IDisposable
{
    private readonly int[] _table = (int[])typeof(Offsets).GetField("T", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private readonly int _index;
    private readonly int _old;

    public OffsetPatch(string name, int value)
    {
        var property = typeof(Offsets).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        var before = (int)property.GetValue(null)!;
        _index = Enumerable.Range(0, _table.Length).Single(i =>
        {
            var old = _table[i];
            _table[i] = ~old;
            var hit = (int)property.GetValue(null)! != before;
            _table[i] = old;
            return hit;
        });
        _old = _table[_index];
        _table[_index] = value;
    }

    public void Dispose() => _table[_index] = _old;
}
