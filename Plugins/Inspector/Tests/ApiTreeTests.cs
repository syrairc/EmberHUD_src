using System;
using System.Collections;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;
using Hexa.NET.ImGui;

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

    static void Draw(ApiTree tree, GameState state) => TestFrame.Run(() =>
    {
        ImGui.SetNextWindowSize(new Vector2(900, 600));
        ImGui.Begin("tree");
        tree.BeginFrame();
        foreach (var section in ApiTree.All) tree.Show(section, state);
        ImGui.End();
    });

    [Test]
    public void EverySectionDrawsARichStateAndAnEmptyOne()
    {
        var tree = new ApiTree();
        Draw(tree, Samples.Rich);
        Draw(tree, GameState.Empty);
    }

    [Test]
    public void NestedValuesDrawWhenEverythingIsOpen() => Draw(new ApiTree { OpenAll = true }, Samples.Rich);

    [Test]
    public void TheMapSectionDrawsWithAMapView()
    {
        Map.View = new MapView(new Vector2(10, 20), 2f);
        Draw(new ApiTree(), Samples.Rich);
    }

    [Test]
    public void SectionNamesAreTheFixedList() =>
        Check.That(ApiTree.All.Select(s => s.Name).SequenceEqual(["Player", "Entities", "Map icons", "Terrain", "Explored", "Camera", "Game UI", "Map view"]), "section list changed");

    [Test]
    public void SelectedAndHoveredRowsHighlightAtTheirPosition()
    {
        var tree = new ApiTree { Selected = Samples.Entities[0], Hover = Samples.Player };
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
        var tree = new ApiTree();
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(900, 600));
            ImGui.Begin("selection");
            tree.Show(ApiTree.All[1], Samples.Rich);
            tree.Selected = Samples.Entities[0];
            tree.Show(ApiTree.All[2], Samples.Rich);
            ImGui.End();
        });
        Check.That(tree.Selected is null, "selection survived a section change");
    }

    [Test]
    public void ShowsPositionsFollowsTheTableThatDrew()
    {
        var tree = new ApiTree();
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(900, 600));
            ImGui.Begin("positions");
            tree.Show(ApiTree.All[1], Samples.Rich);
            Check.That(tree.ShowsPositions, "Entities have positions");
            tree.Show(ApiTree.All[2], Samples.Rich);
            Check.That(!tree.ShowsPositions, "map icons are 2D");
            ImGui.End();
        });
    }

    [Test]
    public void SortKeysAreCachedPerRowAndDistanceFollowsTheOrigin()
    {
        var row = new Row(new Counted());
        var member = Walker.Members(typeof(Counted)).Single();
        Counted.Reads = 0;
        row.Key(typeof(Counted), member);
        row.Key(typeof(Counted), member);
        Check.Equal(1, Counted.Reads);

        var entity = new Row(Samples.Entities[0]);
        var near = entity.Dist(Samples.Entities[0].Position);
        var far = entity.Dist(Vector3.Zero);
        Check.That(near != far, "distance did not follow the origin");
        Check.Equal(far, entity.Dist(Vector3.Zero));
    }

    [Test]
    public void HugeCollectionsAreSummarisedAndNeverEnumerated()
    {
        var huge = Samples.Rich with
        {
            Terrain = new Terrain(2000, 2000, 32, Vector2.Zero, false, new BitArray(4_000_000)),
            Explored = new ExploredTrail(new Vector2[30000], 250),
        };
        Draw(new ApiTree { OpenAll = true }, huge);

        Unlistable.Enumerated = false;
        var tree = new ApiTree { OpenAll = true };
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(900, 600));
            ImGui.Begin("huge");
            tree.Show(new Section("huge", _ => new Unlistable()), Samples.Rich);
            tree.Show(new Section("nested", _ => new Tuple<Unlistable>(new Unlistable())), Samples.Rich);
            ImGui.End();
        });
        Check.That(!Unlistable.Enumerated, "a huge collection was enumerated");
    }

    [Test]
    public void AThrowingGetterDrawsAsAMarkerAndLogsOnce()
    {
        Walker.Clear();
        var tree = new ApiTree();
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(900, 600));
            ImGui.Begin("thrower");
            tree.Show(new Section("thrower", _ => new Thrower()), Samples.Rich);
            ImGui.End();
        });
        Check.Equal(1, Walker.ErrorsLogged);
    }
}
