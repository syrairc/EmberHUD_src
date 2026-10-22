using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class TreeModelTests
{
    static readonly Player Origin = new(Vector3.Zero, Vector2.UnitX);

    static Entity[] Many(int count, System.Func<int, Vector3> position, System.Func<int, string>? name = null) =>
        Enumerable.Range(0, count).Select(i => new Entity(i, i, position(i), EntityKind.Monster, Rarity.Normal) { Name = name?.Invoke(i) ?? "" }).ToArray();

    [Test]
    public void LabelsFormatKnownTypes()
    {
        var entity = Labels.Of(Samples.Entities[0], 0);
        Check.Equal("0  Monster  cfg 100  13, 24", entity);
        var icon = Labels.Of(Samples.Icons[0], 3);
        Check.That(icon.Contains("UI_MiniMap_Portal"), icon);
    }

    [Test]
    public void LabelsFallBackForUnknownTypes()
    {
        Check.Equal("4  Thrower", Labels.Of(new Thrower(), 4));
        Check.Equal("5", Labels.Of(5, 2));
    }

    [Test]
    public void LabelsKeepPercentAndBraces()
    {
        var label = Labels.Of(Samples.Entities[0] with { Name = "50% {x}" }, 0);
        Check.That(label.Contains("50% {x}"), label);
    }

    [Test]
    public void FilterRunsOverTheWholeListThenClips()
    {
        var entities = Many(450, i => new Vector3(i, 0, 0), i => i == 430 ? "needle" : "hay");
        var rows = new ListModel().Build(entities, "needle", SortMode.Index, null);
        Check.Equal(1, rows.Length);
        Check.Equal(1, ListModel.Clip(rows, ListModel.Page).Length);
        Check.Equal(430, rows[0].Index);
    }

    [Test]
    public void ClipShowsMoreInPages()
    {
        var rows = new ListModel().Build(Many(450, i => Vector3.Zero), "", SortMode.Index, null);
        Check.Equal(200, ListModel.Clip(rows, 200).Length);
        Check.Equal(400, ListModel.Clip(rows, 400).Length);
        Check.Equal(450, ListModel.Clip(rows, 600).Length);
    }

    [Test]
    public void DistanceSortPutsTheNearestFirstEvenFromPastTheClip()
    {
        var entities = Many(450, i => new Vector3(1000 - i, 0, 0));
        var rows = new ListModel().Build(entities, "", SortMode.Distance, Origin);
        Check.Equal(449, rows[0].Index);
        Check.Equal(449, ((Entity)rows[0].Item).Id);
    }

    [Test]
    public void DistanceSortSendsNaNAndPositionlessItemsLast()
    {
        object[] items = [5, new Entity(1, 1, new Vector3(float.NaN, 0, 0), EntityKind.Monster, Rarity.Normal), new Entity(2, 2, new Vector3(50, 0, 0), EntityKind.Monster, Rarity.Normal), new Entity(3, 3, new Vector3(10, 0, 0), EntityKind.Monster, Rarity.Normal)];
        var rows = new ListModel().Build(items, "", SortMode.Distance, Origin);
        Check.Equal("3,2,0,1", string.Join(",", rows.Select(r => r.Index)));
    }

    [Test]
    public void DistanceSortWorksOverTwoDimensionalPositions()
    {
        var icons = new[] { new MapIcon(1, "far", new Vector2(90, 0), 1, 0, 0), new MapIcon(2, "near", new Vector2(3, 4), 1, 0, 0), new MapIcon(3, "mid", new Vector2(30, 0), 1, 0, 0) };
        var model = new ListModel();
        var rows = model.Build(icons, "", SortMode.Distance, Origin);
        Check.That(model.Positional, "icons are not positional");
        Check.Equal("1,2,0", string.Join(",", rows.Select(r => r.Index)));
    }

    [Test]
    public void CollectionsGetTheBigCapAndLazySequencesTheSmallOne()
    {
        var big = new ListModel();
        Check.That(big.Build(new int[ListModel.MaxRows + 5], "", SortMode.Index, null).Length == ListModel.MaxRows + 5 && !big.TooMany, "a big collection was capped");
        Check.Equal(ListModel.MaxRows, ListModel.LimitOf(Enumerable.Range(0, 3)));
    }

    [Test]
    public void RowsAreRebuiltOnlyWhenTheSourceChanges()
    {
        var model = new ListModel();
        var entities = Many(3, i => Vector3.Zero);
        var first = model.Build(entities, "", SortMode.Index, null);
        var second = model.Build(entities, "", SortMode.Index, null);
        Check.That(first.Zip(second).All(p => ReferenceEquals(p.First, p.Second)), "rows were rebuilt for the same array");
        var third = model.Build(Many(3, i => Vector3.Zero), "", SortMode.Index, null);
        Check.That(!ReferenceEquals(first[0], third[0]), "a new array reused old rows");
    }

    [Test]
    public void VisibleHidesAndCountsNulls()
    {
        var state = Samples.Rich with { Terrain = null };
        var (shown, hidden) = Walker.Visible(state, false);
        Check.That(shown.All(m => m.Member.Name != "Terrain") && shown.Any(m => m.Member.Name == "Player"), "null Terrain shown");
        Check.That(hidden >= 1, "null not counted");

        var (all, none) = Walker.Visible(state, true);
        Check.That(all.Any(m => m.Member.Name == "Terrain"), "Terrain missing with showNulls");
        Check.Equal(0, none);
    }

    [Test]
    public void VisibleReadsEachMemberOnce()
    {
        Counted.Reads = 0;
        Walker.Visible(new Counted(), false);
        Check.Equal(1, Counted.Reads);
    }

    [Test]
    public void ANullItemLabelsAsNullAndNeverThrows()
    {
        Check.Equal("null", Labels.Of(null, 3));
        var rows = new ListModel().Build(new object?[] { null, 5 }, "", SortMode.Distance, Origin);
        Check.Equal("null", rows.Single(r => r.Index == 0).Label);
        Check.Equal("5", rows.Single(r => r.Index == 1).Label);
    }

    [Test]
    public void ABitArraySummaryIsCachedPerInstance()
    {
        var bits = new System.Collections.BitArray(1000);
        Check.That(ReferenceEquals(Walker.Format(bits), Walker.Format(bits)), "summary rebuilt");
        Check.That(Walker.Json(Samples.Terrain).Contains("4 bits, 3 set"), Walker.Json(Samples.Terrain));
    }

    [Test]
    public void AnEnormousSequenceIsCappedAndNeverListed()
    {
        var enumerated = 0;
        IEnumerable<int> Endless()
        {
            for (var i = 0; i < 50000; i++)
            {
                enumerated++;
                yield return i;
            }
        }
        var model = new ListModel();
        var rows = model.Build(Endless(), "", SortMode.Index, null);
        Check.That(model.TooMany && rows.Length == 0, "an over-cap sequence was listed");
        Check.That(enumerated <= ListModel.MaxRows + 1, $"enumerated {enumerated}");
    }
}
