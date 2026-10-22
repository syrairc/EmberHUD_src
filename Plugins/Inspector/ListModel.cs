using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;

namespace Inspector;

internal enum SortMode { Index, Distance }

internal sealed class Row(object? item, int index)
{
    string? _label;
    Vector3? _distFrom;
    float? _dist;
    string _distText = "null";

    public object? Item => item;

    public int Index => index;

    public string Label => _label ??= Labels.Of(item, index);

    public float? Dist(Player from)
    {
        if (_distFrom != from.Position)
        {
            _dist = Walker.Position(item) is { } position && from.FlatDistance(position) is var distance && float.IsFinite(distance) ? distance : null;
            _distText = _dist is { } shown ? Walker.Format(shown) : "null";
            _distFrom = from.Position;
        }
        return _dist;
    }

    public string DistText => _distText;
}

internal sealed class ListModel
{
    public const int Page = 200;
    public const int MaxRows = 20000;
    public const int MaxCollectionRows = 1_000_000;

    object? _source;
    Row[] _rows = [];
    int _limit = MaxRows;

    public static int LimitOf(IEnumerable source) => source is ICollection ? MaxCollectionRows : MaxRows;

    public bool TooMany => _rows.Length > _limit;

    public bool Positional { get; private set; }

    public Row[] Build(IEnumerable source, string filter, SortMode sort, Player? player)
    {
        if (!ReferenceEquals(_source, source))
        {
            _limit = LimitOf(source);
            _rows = source.Cast<object?>().Take(_limit + 1).Select((item, index) => new Row(item, index)).ToArray();
            _source = source;
            var type = Walker.ElementType(source.GetType());
            if (type == typeof(object) && _rows.FirstOrDefault(r => r.Item is not null)?.Item is { } first) type = first.GetType();
            Positional = Walker.HasPosition(type);
        }
        if (TooMany) return [];
        IEnumerable<Row> rows = filter.Length == 0 ? _rows : _rows.Where(r => Text.Matches(filter, r.Label));
        if (sort == SortMode.Distance && player is { } from)
            rows = rows.OrderBy(r => r.Dist(from) is null).ThenBy(r => r.Dist(from) ?? 0f);
        return rows.ToArray();
    }

    public static Row[] Clip(Row[] rows, int shown) => rows.Length <= shown ? rows : rows[..shown];
}
