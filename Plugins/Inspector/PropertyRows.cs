using System;
using System.Collections.Generic;
using System.Linq;
using Aember;

namespace Inspector;

internal readonly record struct PropRow(UProp Prop, string Value, bool Differs);

internal static class PropertyRows
{
    const int MaxSearchDepth = 4;

    public static List<(string Owner, List<PropRow> Rows)> Group(UStructInfo info, byte[] bytes, byte[]? defaults, bool hideDefaults, string search)
    {
        List<(string Owner, List<PropRow> Rows)> groups = [];
        for (var cls = info; cls is not null; cls = cls.Super) groups.Add((cls.Name, []));
        foreach (var prop in info.Properties)
        {
            if (prop.Type.EndsWith("DelegateProperty", StringComparison.Ordinal)) continue;
            var differs = defaults is null || Differs(bytes, defaults, prop);
            if (hideDefaults && !differs) continue;
            var value = Ue.Format(bytes, prop);
            if (search.Length > 0 && !Matches(prop, bytes, value, search, 0)) continue;
            var group = groups.FindIndex(g => g.Owner == prop.Owner);
            if (group < 0)
            {
                groups.Add((prop.Owner, []));
                group = groups.Count - 1;
            }
            groups[group].Rows.Add(new PropRow(prop, value, differs));
        }
        groups.RemoveAll(g => g.Rows.Count == 0);
        return groups;
    }

    static bool Differs(byte[] bytes, byte[] defaults, UProp prop)
    {
        if (prop.Offset < 0 || prop.Size <= 0 || (long)prop.Offset + prop.Size > Math.Min(bytes.Length, defaults.Length)) return true;
        if (prop.Type == "BoolProperty") return ((bytes[prop.Offset] ^ defaults[prop.Offset]) & prop.BoolMask) != 0;
        return !bytes.AsSpan(prop.Offset, prop.Size).SequenceEqual(defaults.AsSpan(prop.Offset, prop.Size));
    }

    static bool Matches(UProp prop, byte[] bytes, string value, string search, int depth)
    {
        if (prop.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || prop.Type.Contains(search, StringComparison.OrdinalIgnoreCase) || value.Contains(search, StringComparison.OrdinalIgnoreCase)) return true;
        if (depth >= MaxSearchDepth || prop.Type != "StructProperty" || Ue.Struct(prop.Struct) is not { } inner) return false;
        return inner.Properties.Any(member =>
        {
            var shifted = member with { Offset = member.Offset + prop.Offset };
            return Matches(shifted, bytes, Ue.Format(bytes, shifted), search, depth + 1);
        });
    }
}
