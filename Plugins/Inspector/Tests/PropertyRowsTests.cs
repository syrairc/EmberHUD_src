using System.Linq;
using Aember;
using Aember.Testing;

namespace Inspector;

public sealed class PropertyRowsTests
{
    static (UStructInfo Info, byte[] Bytes, byte[] Defaults) Thing()
    {
        var ue = new TestUe();
        var vector = ue.ScriptStruct("Vector", 12, new TestProp("X", "FloatProperty", 0), new TestProp("Y", "FloatProperty", 4), new TestProp("Z", "FloatProperty", 8));
        var actor = ue.Class("Actor", 0, 0x50,
            new TestProp("bHidden", "BoolProperty", 0x30, 1, BoolMask: 1),
            new TestProp("bLocked", "BoolProperty", 0x30, 1, BoolMask: 2),
            new TestProp("OnDestroyed", "MulticastSparseDelegateProperty", 0x31, 1));
        var monster = ue.Class("EMonster", actor, 0x70,
            new TestProp("Level", "IntProperty", 0x54),
            new TestProp("Spot", "StructProperty", 0x58, 12, Struct: vector));
        var bytes = new byte[0x70];
        var defaults = new byte[0x70];
        bytes[0x30] = 1;
        defaults[0x30] = 1;
        bytes[0x54] = 5;
        System.BitConverter.GetBytes(7.5f).CopyTo(bytes, 0x5C);
        return (Ue.Struct(monster)!, bytes, defaults);
    }

    [Test]
    public void RowsGroupByDeclaringClassMostDerivedFirstAndDropDelegates()
    {
        var (info, bytes, _) = Thing();

        var groups = PropertyRows.Group(info, bytes, null, false, "");

        Check.Equal("EMonster,Actor", string.Join(",", groups.Select(g => g.Owner)));
        Check.Equal("bHidden,bLocked", string.Join(",", groups[1].Rows.Select(r => r.Prop.Name)));
    }

    [Test]
    public void HideDefaultsKeepsOnlyPropsThatDifferFromTheDefaultObject()
    {
        var (info, bytes, defaults) = Thing();

        var groups = PropertyRows.Group(info, bytes, defaults, true, "");

        Check.Equal("Level,Spot", string.Join(",", groups.Single().Rows.Select(r => r.Prop.Name)));
        Check.Equal("EMonster", groups.Single().Owner);
    }

    [Test]
    public void BoolBitsCompareTheirOwnBitOnly()
    {
        var (info, bytes, defaults) = Thing();
        bytes[0x30] = 3;

        var rows = PropertyRows.Group(info, bytes, defaults, true, "").SelectMany(g => g.Rows).Select(r => r.Prop.Name);

        Check.That(rows.Contains("bLocked") && !rows.Contains("bHidden"), "only bLocked differs");
    }

    [Test]
    public void WithoutADefaultObjectEveryRowShows()
    {
        var (info, bytes, _) = Thing();

        var count = PropertyRows.Group(info, bytes, null, true, "").Sum(g => g.Rows.Count);

        Check.Equal(4, count);
    }

    [Test]
    public void SearchMatchesNameTypeValueAndInlineStructMembers()
    {
        var (info, bytes, _) = Thing();

        Check.Equal("Level", string.Join(",", PropertyRows.Group(info, bytes, null, false, "level").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal("Level", string.Join(",", PropertyRows.Group(info, bytes, null, false, "intprop").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal("Spot", string.Join(",", PropertyRows.Group(info, bytes, null, false, "7.5").SelectMany(g => g.Rows).Select(r => r.Prop.Name)));
        Check.Equal(0, PropertyRows.Group(info, bytes, null, false, "zzz").Count);
    }

    [Test]
    public void ClassesWithoutPropsAndShortBuffersDoNotThrow()
    {
        var ue = new TestUe();
        var empty = Ue.Struct(ue.Class("Empty"))!;
        var (info, _, _) = Thing();

        Check.Equal(0, PropertyRows.Group(empty, new byte[8], null, false, "").Count);
        Check.That(PropertyRows.Group(info, new byte[4], new byte[2], true, "x").Count >= 0, "short buffers are fine");
    }
}
