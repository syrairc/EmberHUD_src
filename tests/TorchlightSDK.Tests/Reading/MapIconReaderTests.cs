using System.Numerics;
using TorchlightSDK.Host;
using TorchlightSDK.Host.Memory;
using TorchlightSDK.Host.Reading;

namespace TorchlightSDK.Tests.Reading;

public class MapIconReaderTests
{
    private sealed class Clock
    {
        public long Now;
    }

    private static readonly Dictionary<int, Vector2> NoEntities = [];

    private static (MinimapReader Minimap, MapIconReader Icons) ReadersFor(FakeWorld world, Clock? clock = null)
    {
        Func<long>? now = clock is null ? null : () => clock.Now;
        return (new MinimapReader(world.Memory, unchecked((nint)FakeWorld.ModuleBase), now), new MapIconReader(world.Memory, now));
    }

    private static MapIcon[] Read((MinimapReader Minimap, MapIconReader Icons) readers, IReadOnlyDictionary<int, Vector2>? positions = null)
    {
        readers.Minimap.ReadRotationDegrees(1);
        return readers.Icons.Read(readers.Minimap, positions ?? NoEntities);
    }

    [Fact]
    public void ListEntriesBecomeIconsInZorderOrderWithStableTies()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 7,
            new FakeIcon(10, 100, 200, Zorder: 5),
            new FakeIcon(1000, 300, 400, Zorder: 1, EntityId: 44, Size: 2, Rotation: 30),
            new FakeIcon(7, 1, 2, Zorder: 5),
            new FakeIcon(2001, 5, 6, Zorder: 1));

        var icons = Read(ReadersFor(world));

        Assert.Equal([1000, 2001, 10, 7], icons.Select(icon => icon.Type));
        Assert.Equal(new MapIcon(1000, "", new Vector2(300, 400), 2, 30, 44), icons[0]);
        Assert.Equal(0, icons[1].EntityId);
    }

    [Fact]
    public void OtherAreasTaskTypesAndNonPositiveTypesAreSkipped()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetTaskTypes(minimap, [1001], [], [1003]);
        world.SetMapIcons(minimap, 7,
            new FakeIcon(10, 1, 1, Area: 6),
            new FakeIcon(0, 1, 1),
            new FakeIcon(-3, 1, 1),
            new FakeIcon(1001, 1, 1),
            new FakeIcon(1003, 1, 1),
            new FakeIcon(11, 1, 1));

        Assert.Equal([11], Read(ReadersFor(world)).Select(icon => icon.Type));
    }

    [Fact]
    public void GarbagePositionsAreSkippedAndABadSizeDrawsAsOne()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1,
            new FakeIcon(10, float.NaN, 1),
            new FakeIcon(10, 2e7f, 1),
            new FakeIcon(10, 1, float.PositiveInfinity),
            new FakeIcon(11, 1, 1, Size: 0),
            new FakeIcon(12, 1, 1, Size: float.NaN),
            new FakeIcon(13, 1, 1, Size: 2.5f, Rotation: float.NaN));

        var icons = Read(ReadersFor(world));

        Assert.Equal([11, 12, 13], icons.Select(icon => icon.Type));
        Assert.Equal([1f, 1f, 2.5f], icons.Select(icon => icon.Size));
        Assert.Equal(0f, icons[2].Rotation);
    }

    [Fact]
    public void AGarbageTypeAndKeyDoNotOverflowIntoAnEntityId()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(1_000_000, 1, 1, EntityId: 5), new FakeIcon(10, 2, 2, EntityId: -5));

        var icons = Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(99, 99) });

        Assert.All(icons, icon => Assert.Equal(0, icon.EntityId));
        Assert.DoesNotContain(icons, icon => icon.Position == new Vector2(99, 99));
    }

    [Fact]
    public void AKnownEntityTakesItsLivePositionAndAnUnknownKeepsTheListPosition()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(1040, 10, 10, EntityId: 5), new FakeIcon(1040, 20, 20, EntityId: 6));

        var icons = Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(99, 88) });

        Assert.Equal([new Vector2(99, 88), new Vector2(20, 20)], icons.Select(icon => icon.Position));
    }

    [Fact]
    public void FollowIconsComeAfterTheListAtTheEntityPositionAndReplaceItsListEntry()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 7, new FakeIcon(1040, 1, 1, Zorder: 9, EntityId: 5), new FakeIcon(10, 2, 2));
        world.SetFollowIcons(minimap, (5, 4, 7), (6, 4, 7), (8, 4, 6), (9, 0, 7), (12, 0x8000_0000, 7));

        var icons = Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(50, 50), [8] = new(80, 80), [9] = new(90, 90), [12] = new(12, 12) });

        Assert.Equal([new MapIcon(10, "", new Vector2(2, 2), 1, 0, 0), new MapIcon(4, "", new Vector2(50, 50), 1, 0, 5)], icons);
    }

    [Fact]
    public void TextureNamesResolveFromTheIconMapAndStayCached()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1), new FakeIcon(1000, 2, 2), new FakeIcon(7, 3, 3));
        world.SetIconMap(minimap, (10, world.AddTexture("UI_MiniMap_Portal")), (1000, world.AddTexture("UI_MiniMap_Entrence")), (7, 0x7777_0000_0000L));
        var clock = new Clock();
        var readers = ReadersFor(world, clock);

        var first = Read(readers);
        world.Memory.Write(minimap.Widget + Offsets.MinimapActiveIconMap, 0L);
        clock.Now = 10_000;
        var second = Read(readers);

        Assert.Equal(["UI_MiniMap_Portal", "UI_MiniMap_Entrence", ""], first.Select(icon => icon.Texture));
        Assert.Equal(["UI_MiniMap_Portal", "UI_MiniMap_Entrence", ""], second.Select(icon => icon.Texture));
    }

    [Fact]
    public void AnUnresolvedTypeRereadsTheIconMapOnlyAfterTheInterval()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        var clock = new Clock();
        var readers = ReadersFor(world, clock);

        Assert.Equal("", Read(readers)[0].Texture);
        world.SetIconMap(minimap, (10, world.AddTexture("UI_MiniMap_Portal")));
        clock.Now = Offsets.IconMapRereadMilliseconds - 1;
        Assert.Equal("", Read(readers)[0].Texture);
        clock.Now = Offsets.IconMapRereadMilliseconds;
        Assert.Equal("UI_MiniMap_Portal", Read(readers)[0].Texture);
    }

    [Fact]
    public void AnIconMapOverItsCapLeavesNamesUnresolvedButTheIconsStay()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        var map = world.SetIconMap(minimap, (10, world.AddTexture("UI_MiniMap_Portal")));
        world.Memory.Write(map + Offsets.TMapNum, Offsets.MaxIconMapElements + 1);

        Assert.Equal("", Assert.Single(Read(ReadersFor(world))).Texture);
    }

    [Fact]
    public void AWidgetChangeClearsTheCaches()
    {
        var world = new FakeWorld();
        var clock = new Clock();
        var readers = ReadersFor(world, clock);
        var old = world.AddMinimap(0f);
        world.SetMapIcons(old, 1, new FakeIcon(10, 1, 1));
        world.SetIconMap(old, (10, world.AddTexture("Old_Portal")));
        Assert.Equal("Old_Portal", Read(readers)[0].Texture);

        world.Memory.Write(old.Widget + Offsets.UObjectClass, 0L);
        var replacement = world.AddMinimap(0f);
        world.SetMapIcons(replacement, 1, new FakeIcon(10, 1, 1));
        world.SetIconMap(replacement, (10, world.AddTexture("New_Portal")));
        Read(readers);
        clock.Now = Offsets.MinimapRescanMilliseconds;

        Assert.Equal("New_Portal", Read(readers)[0].Texture);
    }

    [Fact]
    public void NoWidgetGivesNoIcons() => Assert.Empty(Read(ReadersFor(new FakeWorld())));

    [Theory]
    [InlineData(-1, true)]
    [InlineData(2049, true)]
    [InlineData(2, false)]
    public void ABadListHeaderGivesNoIcons(int count, bool mapped)
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapIconList + Offsets.TArrayNum, count);
        if (!mapped) world.Memory.Write(minimap.Widget + Offsets.MinimapIconList, 0x7777_0000_0000L);

        Assert.Empty(Read(ReadersFor(world)));
    }

    [Fact]
    public void ABadFollowMapDropsOnlyTheFollowIcons()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        world.SetFollowIcons(minimap, (5, 4, 1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapFollowTypes + Offsets.FollowMapBucketCount, (uint)Offsets.MaxBuckets + 1);

        Assert.Equal([10], Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(5, 5) }).Select(icon => icon.Type));
    }

    [Fact]
    public void ACyclicFollowChainDropsOnlyTheFollowIcons()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        world.SetFollowIcons(minimap, (5, 4, 1));
        world.Memory.TryReadPtr(unchecked((nint)(minimap.Widget + Offsets.MinimapFollowTypes)), out var buckets);
        world.Memory.TryReadPtr(buckets + 5 % 3 * 8, out var node);
        world.Memory.Write(node + Offsets.FollowNodeNext, (long)node);

        Assert.Equal([10], Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(5, 5) }).Select(icon => icon.Type));
    }

    [Fact]
    public void MoreFollowNodesThanTheCapEvenWithTheSameIdDropsOnlyTheFollowIcons()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 1, new FakeIcon(10, 1, 1));
        world.SetFollowIcons(minimap, (5, 4, 1));
        var map = unchecked((nint)(minimap.Widget + Offsets.MinimapFollowTypes));
        world.Memory.TryReadPtr(map, out var buckets);
        world.Memory.TryReadPtr(buckets + 5 % 3 * 8, out var node);
        var bucketCount = Offsets.MaxFollowIcons + 1;
        var spread = world.Alloc(bucketCount * 8);
        world.Memory.WriteArray(spread, Enumerable.Repeat((long)node, bucketCount).ToArray());
        world.Memory.Write(map, spread);
        world.Memory.Write(map + Offsets.FollowMapBucketCount, (uint)bucketCount);

        Assert.Equal([10], Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(5, 5) }).Select(icon => icon.Type));
    }

    [Fact]
    public void AnEmptiedListStillPublishesTheFollowIcons()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 7);
        world.Memory.Write(minimap.Widget + Offsets.MinimapIconList, 0L);
        world.SetFollowIcons(minimap, (5, 4, 7));

        var icons = Read(ReadersFor(world), new Dictionary<int, Vector2> { [5] = new(5, 5) });

        Assert.Equal([new MapIcon(4, "", new Vector2(5, 5), 1, 0, 5)], icons);
    }

    [Fact]
    public void AFollowNodeWithIdZeroNeverReplacesListIconsWithoutAnEntity()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 7, new FakeIcon(10, 1, 1));
        world.SetFollowIcons(minimap, (0, 4, 7));

        var icons = Read(ReadersFor(world), new Dictionary<int, Vector2> { [0] = new(9, 9) });

        Assert.Equal([new MapIcon(10, "", new Vector2(1, 1), 1, 0, 0)], icons);
    }

    [Fact]
    public void ABadTaskTypeListRetriesOnTheNextRead()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetTaskTypes(minimap, [1001], [], []);
        world.SetMapIcons(minimap, 1, new FakeIcon(1001, 1, 1), new FakeIcon(10, 1, 1));
        world.Memory.Write(minimap.Widget + Offsets.MinimapTaskTypesA + Offsets.TArrayNum, Offsets.MaxTaskTypes + 1);
        var readers = ReadersFor(world);

        Assert.Equal([1001, 10], Read(readers).Select(icon => icon.Type));
        world.Memory.Write(minimap.Widget + Offsets.MinimapTaskTypesA + Offsets.TArrayNum, 1);
        Assert.Equal([10], Read(readers).Select(icon => icon.Type));
    }

    [Fact]
    public void AFollowIconForAnEntityWeDontTrackUsesTheLookedUpPosition()
    {
        var world = new FakeWorld();
        var minimap = world.AddMinimap(0f);
        world.SetMapIcons(minimap, 108);
        world.SetFollowIcons(minimap, (72, 2642u, 108u));
        var readers = ReadersFor(world);
        readers.Minimap.ReadRotationDegrees(1);

        var icons = readers.Icons.Read(readers.Minimap, NoEntities, id => id == 72 ? new Vector2(-625, -24197) : null);

        Assert.Equal([new MapIcon(2642, "", new Vector2(-625, -24197), 1, 0, 72)], icons);
    }
}
