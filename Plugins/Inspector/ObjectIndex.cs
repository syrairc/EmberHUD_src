using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EmberHUD;

namespace Inspector;

internal sealed record ObjectRow(int Index, string Name, string Class, string Outer, nint Address, nint ClassAddress, bool IsDefault);

internal sealed record ClassNode(nint Address, string Name, nint Super, int Own, int Total);

internal sealed class ObjectIndex
{
    public static ObjectIndex Empty { get; } = Build([]);

    readonly Dictionary<nint, ClassNode> _nodes;
    readonly Dictionary<nint, List<nint>> _children;
    readonly Dictionary<nint, List<ObjectRow>> _byClass;

    ObjectIndex(ObjectRow[] rows, Dictionary<nint, ClassNode> nodes, Dictionary<nint, List<nint>> children, Dictionary<nint, List<ObjectRow>> byClass, List<nint> roots)
    {
        Rows = rows;
        _nodes = nodes;
        _children = children;
        _byClass = byClass;
        Roots = roots;
    }

    public ObjectRow[] Rows { get; }

    public IReadOnlyList<nint> Roots { get; }

    public ClassNode? Node(nint classAt) => _nodes.GetValueOrDefault(classAt);

    public IReadOnlyList<nint> Children(nint classAt) => _children.TryGetValue(classAt, out var kids) ? kids : [];

    public IEnumerable<ObjectRow> Instances(nint classAt)
    {
        Stack<nint> pending = new([classAt]);
        while (pending.Count > 0)
        {
            var at = pending.Pop();
            if (_byClass.TryGetValue(at, out var own))
                foreach (var row in own) yield return row;
            foreach (var kid in Children(at)) pending.Push(kid);
        }
    }

    public ObjectRow[] Search(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(query[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) ? [.. Rows.Where(r => r.Address == (nint)address)] : [];
        return [.. Rows.Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || r.Class.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    public static ObjectIndex Scan() =>
        Build([.. Ue.Objects().Select(o => new ObjectRow(o.Index, o.Name, o.Class?.Name ?? "?", o.Outer?.Name ?? "", o.Address, o.ClassAddress, o.IsDefault))]);

    public static ObjectIndex Build(ObjectRow[] rows)
    {
        Dictionary<nint, List<ObjectRow>> byClass = [];
        foreach (var row in rows.Where(r => !r.IsDefault && r.ClassAddress != 0))
        {
            if (!byClass.TryGetValue(row.ClassAddress, out var list)) byClass[row.ClassAddress] = list = [];
            list.Add(row);
        }

        Dictionary<nint, (string Name, nint Super, int Total)> totals = [];
        foreach (var (classAt, list) in byClass)
            for (var cls = Ue.Struct(classAt); cls is not null; cls = cls.Super)
                totals[cls.Address] = (cls.Name, cls.Super?.Address ?? 0, (totals.TryGetValue(cls.Address, out var known) ? known.Total : 0) + list.Count);

        var nodes = totals.ToDictionary(p => p.Key, p => new ClassNode(p.Key, p.Value.Name, p.Value.Super, byClass.TryGetValue(p.Key, out var own) ? own.Count : 0, p.Value.Total));
        var children = nodes.Values.Where(n => n.Super != 0).GroupBy(n => n.Super).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).Select(n => n.Address).ToList());
        var roots = nodes.Values.Where(n => n.Super == 0).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).Select(n => n.Address).ToList();
        return new ObjectIndex(rows, nodes, children, byClass, roots);
    }
}
