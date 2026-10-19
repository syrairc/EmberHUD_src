using System.Diagnostics;
using EmberHUD.Host;
using EmberHUD.Host.Reading;

namespace EmberHUD.Tests.Reading;

public class TarotReaderTests
{
    private static readonly nint Module = unchecked((nint)0x1_4000_0000L);

    internal sealed class Fixture
    {
        public readonly FakeMemory Memory = new();
        public readonly FakeLua Lua;
        public readonly LuaTable Manager;
        public readonly LuaTable StateData;
        public readonly LuaTable CardPlay;
        public readonly LuaTable TileMap;
        public readonly LuaTable Tiles;
        public readonly LuaTable CardGroup;
        public readonly LuaTable Cards;
        public readonly LuaTable Received;
        public readonly LuaTable Data;
        public long Now;

        public Fixture(string? state, int? round = 4, bool withManager = true)
        {
            Lua = new FakeLua(Memory, Module);
            Manager = Lua.NewTable(0, 2);
            StateData = Lua.NewTable(0, 3);
            CardPlay = Lua.NewTable(0, 3);
            TileMap = Lua.NewTable(0, 2);
            Tiles = Lua.NewTable(0, 9);
            CardGroup = Lua.NewTable(0, 1);
            Cards = Lua.NewTable(0, 6);
            Received = Lua.NewTable(0, 6);
            var data = Data = Lua.NewTable(0, 2);
            if (round is { } r) Lua.SetInt(Manager, Offsets.LuaTarotRound, r);
            if (state is not null) Lua.SetString(data, Offsets.LuaTarotState, state);
            Lua.SetTable(data, Offsets.LuaTarotStateData, StateData);
            Lua.SetTable(Manager, Offsets.LuaTarotData, data);
            if (withManager) Lua.SetTable(Lua.Globals, Offsets.LuaTarotManager, Manager);
        }

        public TarotReader Reader() => new(Memory, Module, () => Now);

        public static Fixture Board(int? hp = 2, int? position = 1, int tiles = 4, bool noMapOrGroup = false)
        {
            var f = new Fixture(Offsets.TarotStateBoard);
            if (hp is { } h) f.Lua.SetInt(f.CardPlay, Offsets.LuaTarotHp, h);
            if (position is { } p) f.Lua.SetInt(f.TileMap, Offsets.LuaTarotPosition, p);
            f.Lua.SetTable(f.TileMap, Offsets.LuaTarotTiles, f.Tiles);
            f.Lua.SetTable(f.CardGroup, Offsets.LuaTarotCards, f.Cards);
            if (!noMapOrGroup)
            {
                f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, f.TileMap);
                f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotCardGroup, f.CardGroup);
            }

            f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotRecvRewards, f.Received);
            f.Lua.SetTable(f.StateData, Offsets.LuaTarotCardPlay, f.CardPlay);
            for (var i = 1; i <= tiles; i++) f.Tile(i, 10000, null);
            return f;
        }

        public void Tile(int key, int cfg, int? reward, bool emptyReward = false)
        {
            var tile = Lua.NewTable(0, 2);
            Lua.SetInt(tile, Offsets.LuaTarotCfg, cfg);
            if (reward is { } r) Lua.SetTable(tile, Offsets.LuaTarotReward, WithCfg(r));
            else if (emptyReward) Lua.SetTable(tile, Offsets.LuaTarotReward, Lua.NewTable(0, 0));
            Lua.SetIntKeyTable(Tiles, key, tile);
        }

        public void Card(int key, int id, int cfg)
        {
            var card = Lua.NewTable(0, 2);
            Lua.SetInt(card, Offsets.LuaTarotId, id);
            Lua.SetInt(card, Offsets.LuaTarotCfg, cfg);
            Lua.SetIntKeyTable(Cards, key, card);
        }

        public void Taken(int key, int cfg) => Lua.SetIntKeyTable(Received, key, WithCfg(cfg));

        private LuaTable WithCfg(int cfg)
        {
            var table = Lua.NewTable(0, 2);
            Lua.SetInt(table, Offsets.LuaTarotCfg, cfg);
            return table;
        }
    }

    [Fact]
    public void BoardReadsTheCardBeingPlayed()
    {
        var f = Fixture.Board();
        var queue = f.Lua.NewTable(0, 1);
        var current = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(current, Offsets.LuaTarotId, 4);
        f.Lua.SetInt(current, Offsets.LuaTarotCfg, 22);
        f.Lua.SetTable(queue, Offsets.LuaTarotCurrentCard, current);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotQueue, queue);
        Assert.Equal(22, f.Reader().Read()!.Playing);
    }

    [Fact]
    public void BoardWithoutAQueueIsNotPlayingAnything() => Assert.Equal(0, Fixture.Board().Reader().Read()!.Playing);

    [Fact]
    public void NullWithoutManager() => Assert.Null(new Fixture(Offsets.TarotStateCollect, withManager: false).Reader().Read());

    [Theory]
    [InlineData("S9GamePlayStateInit")]
    [InlineData("bogus")]
    [InlineData(null)]
    public void NullForInitAndUnknownState(string? state) => Assert.Null(new Fixture(state).Reader().Read());

    [Fact]
    public void CollectReadsProgress()
    {
        var f = new Fixture(Offsets.TarotStateCollect);
        f.Lua.SetInt(f.StateData, Offsets.LuaTarotPlayNum, 12);
        var s = f.Reader().Read()!;
        Assert.Equal(TarotPhase.Collect, s.Phase);
        Assert.Equal(12, s.Progress);
        Assert.Equal(4, s.Round);
        Assert.Empty(s.Tiles);
        Assert.Empty(s.Hand);
        Assert.Empty(s.Taken);
    }

    [Fact]
    public void CollectWithoutPlayNumIsZero() => Assert.Equal(0, new Fixture(Offsets.TarotStateCollect).Reader().Read()!.Progress);

    [Fact]
    public void MissingRoundIsZero() => Assert.Equal(0, new Fixture(Offsets.TarotStateCollect, round: null).Reader().Read()!.Round);

    [Fact]
    public void FinalReadsChallenges()
    {
        var f = new Fixture(Offsets.TarotStateFinal);
        f.Lua.SetInt(f.StateData, Offsets.LuaTarotChallengeTimes, 5);
        var s = f.Reader().Read()!;
        Assert.Equal(TarotPhase.Final, s.Phase);
        Assert.Equal(5, s.ChallengesLeft);
    }

    [Fact]
    public void BoardReadsTilesHandAndTaken()
    {
        var f = Fixture.Board(hp: 2, position: 1, tiles: 0);
        f.Tile(1, 10000, null);
        f.Tile(2, 2010, 73);
        f.Tile(3, 1000, 1000);
        f.Tile(4, 9217, 9217);
        f.Card(1, 1, 16);
        f.Card(2, 4, 101);
        f.Taken(1, 75);
        var s = f.Reader().Read()!;
        Assert.Equal(TarotPhase.Board, s.Phase);
        Assert.Equal(4, s.Round);
        Assert.Equal(2, s.Hp);
        Assert.Equal(1, s.Position);
        Assert.Equal(
            [new TarotTile(0, 10000, 0), new TarotTile(1, 2010, 3), new TarotTile(2, 1000, 6), new TarotTile(3, 9217, 5)],
            s.Tiles);
        Assert.Equal([new TarotCard(1, 16), new TarotCard(4, 101)], s.Hand);
        Assert.Equal([5], s.Taken);
    }

    [Fact]
    public void TilesSortByKeyAndIndexIsKeyMinusOne()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(3, 30, null);
        f.Tile(1, 10, null);
        f.Tile(2, 20, null);
        var tiles = f.Reader().Read()!.Tiles;
        Assert.Equal([0, 1, 2], tiles.Select(t => t.Index));
        Assert.Equal([10, 20, 30], tiles.Select(t => t.CfgId));
    }

    [Fact]
    public void MissingPositionIsZero() => Assert.Equal(0, Fixture.Board(position: null).Reader().Read()!.Position);

    [Fact]
    public void PositionOutOfRangeIsNull() => Assert.Null(Fixture.Board(position: 4, tiles: 4).Reader().Read());

    [Fact]
    public void LastTileIsAValidPosition() => Assert.Equal(3, Fixture.Board(position: 3, tiles: 4).Reader().Read()!.Position);

    [Fact]
    public void EmptyRewardTableIsNoChest()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(1, 10000, null, emptyReward: true);
        Assert.Equal(0, f.Reader().Read()!.Tiles.Single().Chest);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(8, 1)]
    [InlineData(72, 2)]
    [InlineData(0, 0)]
    public void ChestIsTheRarityOfTheRewardCfg(int cfg, int chest)
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(1, 2010, cfg);
        Assert.Equal(chest, f.Reader().Read()!.Tiles.Single().Chest);
    }

    [Fact]
    public void BoardWithoutCardPlayIsNull() => Assert.Null(new Fixture(Offsets.TarotStateBoard).Reader().Read());

    [Fact]
    public void MissingHpIsZero() => Assert.Equal(0, Fixture.Board(hp: null).Reader().Read()!.Hp);

    [Fact]
    public void BoardWithNoTilesIsNull() => Assert.Null(Fixture.Board(tiles: 0).Reader().Read());

    [Fact]
    public void TilesAreCappedAtTheLimit() =>
        Assert.Equal(Offsets.MaxTarotTiles, Fixture.Board(position: 0, tiles: Offsets.MaxTarotTiles + 5).Reader().Read()!.Tiles.Length);

    [Fact]
    public void MissingHandAndTakenAreEmpty()
    {
        var s = Fixture.Board().Reader().Read()!;
        Assert.Empty(s.Hand);
        Assert.Empty(s.Taken);
    }

    [Fact]
    public void HandAndTakenAreCapped()
    {
        var f = Fixture.Board();
        for (var i = 1; i <= Offsets.MaxTarotCards + 4; i++) f.Card(i, i, 101);
        for (var i = 1; i <= Offsets.MaxTarotRewards + 4; i++) f.Taken(i, 72);
        var s = f.Reader().Read()!;
        Assert.Equal(Offsets.MaxTarotCards, s.Hand.Length);
        Assert.Equal(Offsets.MaxTarotRewards, s.Taken.Length);
    }

    [Fact]
    public void UnreadableCardsAndRewardsAreSkippedAndTheRestPublishes()
    {
        var f = Fixture.Board(tiles: 0, position: 1);
        f.Tile(1, 10000, null);
        f.Tile(2, 2010, 73);
        f.Lua.SetIntKeyTable(f.Cards, 1, f.Lua.NewTable(0, 0));
        f.Card(2, 4, 101);
        f.Lua.SetIntKeyTable(f.Received, 1, f.Lua.NewTable(0, 0));
        f.Taken(2, 75);
        var s = f.Reader().Read()!;
        Assert.Equal([new TarotTile(0, 10000, 0), new TarotTile(1, 2010, 3)], s.Tiles);
        Assert.Equal([new TarotCard(4, 101)], s.Hand);
        Assert.Equal([5], s.Taken);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AnUnreadableTileMakesTheBoardNull(int unreadableKey)
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        for (var key = 1; key <= 3; key++)
            if (key == unreadableKey) f.Lua.SetIntKeyTable(f.Tiles, key, f.Lua.NewTable(0, 0));
            else f.Tile(key, 10000, null);
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void ATileKeyGapMakesTheBoardNull()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(1, 10000, null);
        f.Tile(3, 10000, null);
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void ARewardWithoutCfgIsNotTaken()
    {
        var f = Fixture.Board();
        f.Taken(1, 0);
        f.Lua.SetIntKeyTable(f.Received, 2, f.Lua.NewTable(0, 0));
        f.Taken(3, 75);
        Assert.Equal([5], f.Reader().Read()!.Taken);
    }

    [Fact]
    public void ATornSnapshotKeepsThePreviousState()
    {
        var f = Fixture.Board();
        var hooked = new SwapOnRead(f.Memory);
        var reader = new TarotReader(hooked, Module, () => f.Now);
        var first = reader.Read()!;
        hooked.Trigger = f.CardPlay.Address;
        hooked.Fire = () => f.Lua.ReplaceTable(f.Data, Offsets.LuaTarotStateData, f.Lua.NewTable(0, 0));
        f.Now = long.MaxValue / 2;
        Assert.Same(first, reader.Read());
        Assert.Null(hooked.Fire);
    }

    private sealed class SwapOnRead(FakeMemory inner) : EmberHUD.Host.Memory.IReadBackend
    {
        public nint Trigger;
        public Action? Fire;

        public bool TryRead(nint address, Span<byte> target)
        {
            var ok = inner.TryRead(address, target);
            if (address >= Trigger && address < Trigger + 48 && Fire is { } fire) { Fire = null; fire(); }
            return ok;
        }
    }

    [Fact]
    public void RefreshesAtMostEvery100Ms()
    {
        var f = Fixture.Board();
        var reader = f.Reader();
        var first = reader.Read()!;
        Assert.Equal(4, first.Tiles.Length);

        f.Tile(5, 10000, null);
        f.Now = Stopwatch.Frequency * Offsets.TarotRefreshMilliseconds / 1000 - 1;
        Assert.Same(first, reader.Read());

        f.Now += 1;
        Assert.Equal(5, reader.Read()!.Tiles.Length);
    }

    private static byte[] Hex(string text) => Convert.FromHexString(text.Replace(" ", ""));

    private static LuaTable Lazy(Fixture f, byte[] bytes) => LazyOn(f, f.Lua.NewTable(2, 0), bytes);

    private static LuaTable LazyOn(Fixture f, LuaTable table, byte[] bytes)
    {
        f.Lua.SetIntKeyString(table, 1, "rpc.Lazy");
        f.Lua.SetIntKeyBytes(table, 2, bytes);
        return table;
    }

    private static readonly byte[] BadBlob = Hex("22 09");

    private static LuaTable DecodedWithCfg(Fixture f, int cfg)
    {
        var table = f.Lua.NewTable(2, 2);
        f.Lua.SetInt(table, Offsets.LuaTarotCfg, cfg);
        return table;
    }

    [Fact]
    public void LazyTileMapReadsFromBytes()
    {
        var f = Fixture.Board(tiles: 0, noMapOrGroup: true);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotCardGroup, f.CardGroup);
        var map = Lazy(f, Hex("0A 03 10 90 4E 0A 0D 10 DA 0F 22 08 08 01 10 48 18 01 20 01 0A 03 10 E8 07 10 02"));
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, map);
        var s = f.Reader().Read()!;
        Assert.Equal([new TarotTile(0, 10000, 0), new TarotTile(1, 2010, 2), new TarotTile(2, 1000, 0)], s.Tiles);
        Assert.Equal(2, s.Position);
    }

    [Fact]
    public void LazyRewardNextToDecodedOnes()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(1, 2010, null);
        var tile = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(tile, Offsets.LuaTarotCfg, 2010);
        f.Lua.SetTable(tile, Offsets.LuaTarotReward, Lazy(f, Hex("08 05 10 49 18 01 20 30")));
        f.Lua.SetIntKeyTable(f.Tiles, 2, tile);
        Assert.Equal([0, 3], f.Reader().Read()!.Tiles.Select(t => t.Chest));
    }

    [Fact]
    public void LazyCardGroupReadsCards()
    {
        var f = Fixture.Board(noMapOrGroup: true);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, f.TileMap);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotCardGroup, Lazy(f, Hex("0A 04 08 01 10 10 0A 04 08 02 10 02 20 02")));
        Assert.Equal([new TarotCard(1, 16), new TarotCard(2, 2)], f.Reader().Read()!.Hand);
    }

    [Fact]
    public void BrokenRewardBlobIsNoChestEvenIfDecodedKeysExist()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        var tile = f.Lua.NewTable(0, 2);
        f.Lua.SetInt(tile, Offsets.LuaTarotCfg, 2010);
        f.Lua.SetTable(tile, Offsets.LuaTarotReward, LazyOn(f, DecodedWithCfg(f, 73), BadBlob));
        f.Lua.SetIntKeyTable(f.Tiles, 1, tile);
        Assert.Equal([new TarotTile(0, 2010, 0)], f.Reader().Read()!.Tiles);
    }

    [Fact]
    public void BrokenTileBlobMakesTheBoardNull()
    {
        var f = Fixture.Board(tiles: 0, position: 1);
        f.Lua.SetIntKeyTable(f.Tiles, 1, LazyOn(f, DecodedWithCfg(f, 10000), BadBlob));
        f.Lua.SetIntKeyTable(f.Tiles, 2, Lazy(f, Hex("10 DA 0F")));
        f.Tile(3, 1000, null);
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void BrokenLazyTileMapTileMakesTheBoardNull()
    {
        var f = Fixture.Board(tiles: 0, noMapOrGroup: true);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotCardGroup, f.CardGroup);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, Lazy(f, Hex("0A 03 10 90 4E 0A 02 22 09")));
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void BrokenCardGroupBlobIsAnEmptyHand()
    {
        var f = Fixture.Board(noMapOrGroup: true);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, f.TileMap);
        f.Card(1, 1, 16);
        var group = f.Lua.NewTable(2, 2);
        f.Lua.SetTable(group, Offsets.LuaTarotCards, f.Cards);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotCardGroup, LazyOn(f, group, BadBlob));
        Assert.Empty(f.Reader().Read()!.Hand);
    }

    [Fact]
    public void BrokenCardBlobIsSkipped()
    {
        var f = Fixture.Board();
        var broken = f.Lua.NewTable(2, 2);
        f.Lua.SetInt(broken, Offsets.LuaTarotId, 1);
        f.Lua.SetInt(broken, Offsets.LuaTarotCfg, 16);
        f.Lua.SetIntKeyTable(f.Cards, 1, LazyOn(f, broken, BadBlob));
        f.Lua.SetIntKeyTable(f.Cards, 2, Lazy(f, Hex("08 07 10 09")));
        Assert.Equal([new TarotCard(7, 9)], f.Reader().Read()!.Hand);
    }

    [Fact]
    public void BrokenTileMapBlobIsNull()
    {
        var f = Fixture.Board(noMapOrGroup: true);
        var map = f.Lua.NewTable(2, 3);
        f.Lua.SetInt(map, Offsets.LuaTarotPosition, 1);
        f.Lua.SetTable(map, Offsets.LuaTarotTiles, f.Tiles);
        f.Lua.SetTable(f.CardPlay, Offsets.LuaTarotTileMap, LazyOn(f, map, BadBlob));
        Assert.Null(f.Reader().Read());
    }

    [Fact]
    public void LazyTileInADecodedListUsesItsKeyAsIndex()
    {
        var f = Fixture.Board(tiles: 0, position: 0);
        f.Tile(1, 10000, null);
        f.Tile(2, 10000, null);
        f.Lua.SetIntKeyTable(f.Tiles, 3, Lazy(f, Hex("10 E8 07 22 08 08 01 10 48 18 01 20 01")));
        Assert.Equal(new TarotTile(2, 1000, 2), f.Reader().Read()!.Tiles[2]);
    }

    [Fact]
    public void LazyCardInADecodedGroup()
    {
        var f = Fixture.Board();
        f.Card(1, 1, 16);
        f.Lua.SetIntKeyTable(f.Cards, 2, Lazy(f, Hex("08 07 10 09")));
        Assert.Equal([new TarotCard(1, 16), new TarotCard(7, 9)], f.Reader().Read()!.Hand);
    }

    [Fact]
    public void LazyReceivedRewardInADecodedList()
    {
        var f = Fixture.Board();
        f.Taken(1, 75);
        f.Lua.SetIntKeyTable(f.Received, 2, Lazy(f, Hex("10 48")));
        Assert.Equal([5, 2], f.Reader().Read()!.Taken);
    }
}
