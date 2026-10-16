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
    sealed class Counted
    {
        public static int Reads;
        public int Value
        {
            get
            {
                Reads++;
                return 5;
            }
        }
    }

    sealed class Unlistable : ICollection
    {
        public static bool Enumerated;
        public int Count => 100000;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) => Enumerated = true;

        public IEnumerator GetEnumerator()
        {
            Enumerated = true;
            return Array.Empty<int>().GetEnumerator();
        }
    }

    static void Draw(ApiTree tree, GameState state) => Samples.Frame(() =>
    {
        tree.BeginFrame();
        foreach (var section in ApiTree.All) tree.Show(section, state);
    });

    [Test]
    public void EverySectionDrawsARichStateAndAnEmptyOne()
    {
        var tree = new ApiTree(Samples.NoPin);
        Draw(tree, Samples.Rich);
        Draw(tree, GameState.Empty);
    }

    [Test]
    public void TheMapSectionDrawsWithAMapView()
    {
        Map.View = new MapView(new Vector2(10, 20), 2f);
        Draw(new ApiTree(Samples.NoPin), Samples.Rich);
    }

    [Test]
    public void SectionNamesAreTheFixedList() =>
        Check.That(ApiTree.All.Select(s => s.Name).SequenceEqual(["Player", "Entities", "Map icons", "Quest targets", "Terrain", "Explored", "Camera", "Game UI", "Map view"]), "section list changed");

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
    public void ChangingSectionDropsTheSelection()
    {
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() =>
        {
            tree.Show(ApiTree.All[1], Samples.Rich);
            tree.Selected = Samples.Entities[0];
            tree.Show(ApiTree.All[2], Samples.Rich);
        });
        Check.That(tree.Selected is null, "selection survived a section change");
    }

    [Test]
    public void SortKeysAreCachedPerRowAndDistanceFollowsThePlayer()
    {
        var row = new Row(new Counted());
        var member = Walker.Members(typeof(Counted)).Single();
        Counted.Reads = 0;
        row.Key(typeof(Counted), member);
        row.Key(typeof(Counted), member);
        Check.Equal(1, Counted.Reads);

        var entity = new Row(Samples.Entities[0]);
        var near = entity.Dist(new Player(Samples.Entities[0].Position, Vector2.UnitX));
        var far = entity.Dist(new Player(Vector3.Zero, Vector2.UnitX));
        Check.That(near != far, "distance did not follow the player");
        Check.Equal(far, entity.Dist(new Player(Vector3.Zero, Vector2.UnitX)));
        Check.Equal(Walker.Format(far!.Value), entity.DistText);
        Check.Equal(5f, new Row(new Entity(1, 1, new Vector3(3, 4, 9), EntityKind.Monster, Rarity.Normal)).Dist(new Player(Vector3.Zero, Vector2.UnitX))!.Value);
    }

    [Test]
    public void CellsAreReadAndFormattedOncePerRow()
    {
        var row = new Row(new Counted());
        var member = Walker.Members(typeof(Counted)).Single();
        Counted.Reads = 0;

        Check.Equal("5", row.Formatted(typeof(Counted), member));
        Check.Equal("5", row.Formatted(typeof(Counted), member));
        Check.Equal(5, row.Value(typeof(Counted), member));
        Check.Equal(1, Counted.Reads);
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
        Draw(new ApiTree(Samples.NoPin), huge);

        Unlistable.Enumerated = false;
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() =>
        {
            tree.Show(new Section("huge", _ => new Unlistable()), Samples.Rich);
            tree.Show(new Section("nested", _ => new Tuple<Unlistable>(new Unlistable())), Samples.Rich);
        });
        Check.That(!Unlistable.Enumerated, "a huge collection was enumerated");
    }

    [Test]
    public void AThrowingGetterDrawsAsAMarkerAndLogsOnce()
    {
        Walker.Clear();
        var tree = new ApiTree(Samples.NoPin);
        Samples.Frame(() => tree.Show(new Section("thrower", _ => new Thrower()), Samples.Rich));
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

        Samples.Frame(() => tree.Show(new Section("lazy", _ => TwentyFiveThousand()), Samples.Rich));

        Check.That(_enumerated <= 20001, $"enumerated {_enumerated} items");
    }
}
