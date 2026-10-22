using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class ApiTreeTests
{
    sealed class Unlistable : ICollection
    {
        public static bool Enumerated;
        public int Count => 2_000_000;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) => Enumerated = true;

        public IEnumerator GetEnumerator()
        {
            Enumerated = true;
            return Array.Empty<int>().GetEnumerator();
        }
    }

    static void Show(ApiTree tree, GameState state) => Samples.Frame(() =>
    {
        tree.BeginFrame();
        tree.Show(state);
    });

    [Test]
    public void RootsAreEveryGameStateMemberPlusMapView()
    {
        var names = ApiTree.Roots(Samples.Rich).Select(r => r.Name).ToArray();
        foreach (var expected in new[] { "Entities", "Player", "Terrain", "Vorax" }) Check.That(names.Contains(expected), $"{expected} missing");
        Check.Equal("Map view", names[^1]);
    }

    [Test]
    public void EveryRootDrawsARichStateAndAnEmptyOne()
    {
        foreach (var showNulls in new[] { false, true })
        {
            var tree = new ApiTree(Samples.NoPin) { ShowNulls = showNulls };
            Show(tree, Samples.Rich);
            Show(tree, GameState.Empty);
        }
    }

    [Test]
    public void TheMapViewRootDrawsWithAMapView()
    {
        Map.View = new MapView(new Vector2(10, 20), 2f);
        Show(new ApiTree(Samples.NoPin), Samples.Rich);
    }

    [Test]
    public void NullRootsAreHiddenAndCounted()
    {
        var tree = new ApiTree(Samples.NoPin);
        Show(tree, Samples.Rich);
        Check.That(tree.RootHidden > 0, "no hidden roots counted");
        tree.ShowNulls = true;
        Show(tree, Samples.Rich);
        Check.Equal(0, tree.RootHidden);
    }

    [Test]
    public void SelectionSurvivesFramesAndClearDropsIt()
    {
        var tree = new ApiTree(Samples.NoPin) { Selected = Samples.Entities[0] };
        Show(tree, Samples.Rich);
        Check.That(tree.Selected is not null, "selection lost over a frame");
        tree.Clear();
        Check.That(tree.Selected is null, "Clear kept the selection");
    }

    [Test]
    public void ListRowsDrawFromTheLabelNotTheMembers()
    {
        var items = Enumerable.Range(0, 450).Select(_ => new Counted()).ToArray();
        Counted.Reads = 0;
        Samples.Frame(() => new ApiTree(Samples.NoPin).Draw("list", items));
        Check.Equal(0, Counted.Reads);
    }

    [Test]
    public void ShowMoreExtendsTheListByAPage()
    {
        var tree = new ApiTree(Samples.NoPin);
        Check.Equal(200, tree.ShownOf(7));
        tree.ShowMore(7);
        Check.Equal(400, tree.ShownOf(7));
        tree.Clear();
        Check.Equal(200, tree.ShownOf(7));
    }

    [Test]
    public void ListRowsAreBuiltOnceNotEveryFrame()
    {
        var tree = new ApiTree(Samples.NoPin);
        var items = Enumerable.Range(0, 450).Select(i => new Entity(i, i, Vector3.Zero, EntityKind.Monster, Rarity.Normal)).ToArray();
        for (var i = 0; i < 3; i++) Samples.Frame(() => tree.Draw("list", items));
        Check.Equal(1, tree.Builds);
    }

    [Test]
    public void ANullElementDrawsAsNull() => Samples.Frame(() => new ApiTree(Samples.NoPin).Draw("nulls", new object?[] { null, Samples.Entities[0] }));

    [Test]
    public void SelectedAndHoveredRowsHighlightAtTheirPosition()
    {
        var tree = new ApiTree(Samples.NoPin) { Selected = Samples.Entities[0], Hover = Samples.Player };
        var highlights = tree.Highlights().ToArray();
        Check.Equal(2, highlights.Length);
        Check.Equal(Samples.Entities[0].Position, highlights[0].Position);
        Check.Equal("Entity", highlights[0].Label);
        Check.Equal(Samples.Player.Position, highlights[1].Position);

        tree.Selected = Samples.Camera;
        tree.Hover = null;
        Check.Equal(0, tree.Highlights().Count());
    }

    [Test]
    public void DistanceFollowsThePlayer()
    {
        var entity = new Row(Samples.Entities[0], 0);
        var near = entity.Dist(new Player(Samples.Entities[0].Position, Vector2.UnitX));
        var far = entity.Dist(new Player(Vector3.Zero, Vector2.UnitX));
        Check.That(near != far, "distance did not follow the player");
        Check.Equal(far, entity.Dist(new Player(Vector3.Zero, Vector2.UnitX)));
        Check.Equal(Walker.Format(far!.Value), entity.DistText);
        Check.Equal(5f, new Row(new Entity(1, 1, new Vector3(3, 4, 9), EntityKind.Monster, Rarity.Normal), 0).Dist(new Player(Vector3.Zero, Vector2.UnitX))!.Value);
    }

    [Test]
    public void TreeLabelsAreRawBecauseTreeNodeIsNotAFormatString()
    {
        var entity = Samples.Entities[0] with { Name = "50% {x}" };
        var row = new Row(entity, 3);
        var label = ApiTree.RowLabel(row, true, new Player(Vector3.Zero, Vector2.UnitX));
        Check.That(label.Contains("50% {x}") && !label.Contains("%%"), label);
        Check.That(label.EndsWith("###row3") && label.Contains("  d "), label);
        Check.Equal("50% {x} (2)###tree", ApiTree.MemberLabel("50% {x} (2)"));
        Samples.Frame(() => new ApiTree(Samples.NoPin).Draw("named", new[] { entity }));
    }

    [Test]
    public void NestedNullsReadLikeRoots() => Check.Equal("Terrain  not provided right now", ApiTree.NotProvided("Terrain"));

    [Test]
    public void SelectionFollowsTheLivePositionByKey()
    {
        var tree = new ApiTree(Samples.NoPin) { Selected = Samples.Entities[0] };
        var moved = Samples.Entities[0] with { Position = new Vector3(70, 80, 0) };
        Samples.Frame(() => tree.Draw("list", new[] { moved, Samples.Entities[1] }));
        Check.Equal(moved.Position, tree.Highlights().Single().Position);

        var icon = new MapIcon(5, "T", new Vector2(1, 2), 1, 0, 9);
        Check.Equal(ApiTree.Key(icon), ApiTree.Key(icon with { Position = new Vector2(5, 5) }));
        Check.That(!Equals(ApiTree.Key(icon), ApiTree.Key(icon with { EntityId = 10 })), "different entity ids share a key");
    }

    [Test]
    public void MemberWidthsAreCachedPerTypeAndFilter()
    {
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() =>
        {
            var all = tree.MembersFor(typeof(Entity), "");
            Check.That(ReferenceEquals(all, tree.MembersFor(typeof(Entity), "")), "same type and filter, same set");
            var one = tree.MembersFor(typeof(Entity), "cfg");
            Check.Equal("CfgId", string.Join(",", one.Members.Select(m => m.Name)));
            Check.That(one.NameWidth > 0f && one.NameWidth < all.NameWidth, "the width follows the filtered names");
        });
    }

    [Test]
    public void HugeCollectionsAreSummarisedAndNeverEnumerated()
    {
        var huge = Samples.Rich with
        {
            Terrain = new Terrain(2000, 2000, 32, Vector2.Zero, new BitArray(4_000_000)),
            Explored = new ExploredTrail(new Vector2[30000], 250),
        };
        Show(new ApiTree(Samples.NoPin), huge);
        Check.Equal("4000000 bits, 0 set", Walker.Format(huge.Terrain!.Walkable));

        Unlistable.Enumerated = false;
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() =>
        {
            tree.Draw("huge", new Unlistable());
            tree.Draw("nested", new Tuple<Unlistable>(new Unlistable()));
        });
        Check.That(!Unlistable.Enumerated, "a huge collection was enumerated");
    }

    [Test]
    public void AThrowingGetterDrawsAsAMarkerAndLogsOnce()
    {
        Walker.Clear();
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() => tree.Draw("thrower", new Thrower()));
        Check.Equal(1, Walker.ErrorsLogged);
    }

    static int _enumerated;

    static IEnumerable<int> TwentyFiveThousand()
    {
        for (var i = 0; i < 25000; i++)
        {
            _enumerated++;
            yield return i;
        }
    }

    [Test]
    public void ALazySequenceIsCappedNotEnumeratedToTheEnd()
    {
        _enumerated = 0;
        var tree = new ApiTree(Samples.NoPin);

        Samples.Frame(() => tree.Draw("lazy", TwentyFiveThousand()));

        Check.That(_enumerated <= 20001, $"enumerated {_enumerated} items");
    }
}
