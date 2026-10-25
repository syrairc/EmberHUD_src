using System.Numerics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed partial class GameUiReader
{
    private readonly record struct CompareRowWidgets(nint Row, nint Damage, nint Survival);

    private sealed record CompareWidgets(nint Root, nint Items, CompareRowWidgets[] Rows);

    private static readonly string[] CompareRowNames = [Offsets.CompareRow1, Offsets.CompareRow2];

    private CompareWidgets[] _compareTips = [];
    private EquipCompare? _compareShown;
    private bool _compareTipPanel;
    private int _compareTipPanelMisses;
    private long _compareBaselineVersion;
    private bool _compareBaselineSet;
    private long _compareReadAt = long.MinValue;
    private long _compareScanAt = long.MinValue;

    public EquipCompare? ReadCompare()
    {
        if (_tradePage == 0 || !_compareTipPanel)
        {
            _compareTips = [];
            _compareShown = null;
            _compareReadAt = _compareScanAt = long.MinValue;
            ResetCompareBaseline();
            return null;
        }

        var now = _clock();
        if (now < _compareReadAt) return _compareShown;
        _compareReadAt = now + Offsets.CompareRefreshMilliseconds;

        var found = CompareCandidates();
        if ((found.Stale || found.Candidates.Count == 0 || _compareTips.Length < 2) && now >= _compareScanAt)
        {
            _compareScanAt = now + Offsets.CompareScanMilliseconds;
            _compareTips = ScanCompareTips();
            found = CompareCandidates();
        }
        return _compareShown = AfterBaseline(PickCompare(found.Candidates));
    }

    private void ResetCompareBaseline() => _compareBaselineSet = false;

    private EquipCompare? AfterBaseline(EquipCompare? picked)
    {
        if (picked is null) return null;
        if (_compareBaselineSet) return picked.Version != _compareBaselineVersion ? picked : null;
        _compareBaselineVersion = picked.Version;
        _compareBaselineSet = true;
        return null;
    }

    private (List<(CompareWidgets Tip, CompareRow[] Rows)> Candidates, bool Stale) CompareCandidates()
    {
        List<(CompareWidgets, CompareRow[])> candidates = [];
        var stale = false;
        foreach (var tip in _compareTips)
        {
            if (!CompareStillValid(tip)) stale = true;
            else if (ReadCompareRows(tip) is { Length: > 0 } rows) candidates.Add((tip, rows));
        }
        return (candidates, stale);
    }

    private EquipCompare? PickCompare(List<(CompareWidgets Tip, CompareRow[] Rows)> candidates)
    {
        if (candidates.Count == 0 || !TryOverlay(out var overlay, out var origin) || !_slate.TryRect(overlay, Vector2.Zero, out var screen)) return null;

        var size = screen.Max - screen.Min;
        var onScreen = candidates.Select(c => (c.Rows, Rect: Geometry(c.Tip.Root, origin)))
            .Where(c => c.Rect is { } rect && rect.Max.X > rect.Min.X && rect.Max.Y > rect.Min.Y &&
                        rect.Max.X > 0 && rect.Max.Y > 0 && rect.Min.X < size.X && rect.Min.Y < size.Y).ToList();
        if (onScreen.Count != 1) return null;
        var tip = candidates.First(c => ReferenceEquals(c.Rows, onScreen[0].Rows)).Tip;
        return new EquipCompare(onScreen[0].Rows) { Min = onScreen[0].Rect!.Value.Min, Max = onScreen[0].Rect!.Value.Max, Version = CompareVersion(tip) };
    }

    private long CompareVersion(CompareWidgets tip)
    {
        var hash = 17L;
        foreach (var row in tip.Rows)
        {
            if (!Visible(row.Row)) continue;
            foreach (var value in (nint[])[row.Damage, row.Survival])
            {
                memory.TryReadPtr(value + Offsets.TextBlockText, out var data);
                hash = unchecked((hash * 31 + data) * 1099511628211L);
            }
        }
        return hash == 0 ? 1 : hash;
    }

    private CompareRow[] ReadCompareRows(CompareWidgets tip)
    {
        List<CompareRow> rows = [];
        foreach (var row in tip.Rows)
        {
            if (!Visible(row.Row)) continue;
            var damage = CompareValueOf(row.Damage);
            var survival = CompareValueOf(row.Survival);
            if (damage is null && survival is null) continue;
            rows.Add(new CompareRow(damage, survival));
        }
        return [.. rows];
    }

    private float? CompareValueOf(nint value) =>
        TryText(value + Offsets.TextBlockText, out var text) ? CompareText.Parse(text) : null;

    private bool CompareStillValid(CompareWidgets tip)
    {
        if (!IsCompareTip(tip.Root) || !NameMatches(tip.Items, Offsets.CompareItems)) return false;
        for (var i = 0; i < tip.Rows.Length; i++)
        {
            var row = tip.Rows[i];
            if (!NameMatches(row.Row, CompareRowNames[i]) || !NameMatches(row.Damage, Offsets.CompareValue) || !NameMatches(row.Survival, Offsets.CompareValue)) return false;
        }
        return true;
    }

    private bool IsCompareTip(nint obj) => IsLiveInstanceOf(obj, Offsets.CompareTipClassName);

    private CompareWidgets? CompareWidgetsOf(nint root)
    {
        var items = FindChild(root, Offsets.CompareItems);
        if (items == 0) return null;

        List<CompareRowWidgets> rows = [];
        foreach (var rowName in CompareRowNames)
        {
            var row = FindChild(items, rowName);
            var damage = row == 0 ? 0 : FindChild(FindChild(row, Offsets.CompareDamage), Offsets.CompareValue);
            var survival = row == 0 ? 0 : FindChild(FindChild(row, Offsets.CompareSurvival), Offsets.CompareValue);
            if (damage == 0 || survival == 0) return null;
            rows.Add(new CompareRowWidgets(row, damage, survival));
        }
        return new CompareWidgets(root, items, [.. rows]);
    }

    private CompareWidgets[] ScanCompareTips() =>
        [.. ScanObjects(obj => IsCompareTip(obj) ? CompareWidgetsOf(obj) : null)];
}
