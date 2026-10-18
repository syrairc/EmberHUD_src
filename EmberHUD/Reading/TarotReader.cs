using System.Diagnostics;
using EmberHUD.Host.Memory;

namespace EmberHUD.Host.Reading;

internal sealed class TarotReader(IReadBackend memory, nint moduleBase, Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private readonly LuaReader _lua = new(memory, moduleBase);
    private long _nextRefresh;
    private TarotState? _state;

    public TarotState? Read()
    {
        var now = _clock();
        if (now < _nextRefresh) return _state;
        _nextRefresh = now + Stopwatch.Frequency * Offsets.TarotRefreshMilliseconds / 1000;
        return _state = Build();
    }

    private TarotState? Build()
    {
        if (!TryOpenStateData(out var manager, out var data, out var stateData)) return null;
        var result = Read(manager, data, stateData);
        return _lua.TryGetTable(data, Offsets.LuaTarotStateData, out var again) && again == stateData ? result : _state;
    }

    private bool TryOpenStateData(out LuaTable manager, out LuaTable data, out LuaTable stateData)
    {
        manager = data = stateData = default;
        return _lua.TryOpen(out var globals) &&
            _lua.TryGetTable(globals, Offsets.LuaTarotManager, out manager) &&
            _lua.TryGetTable(manager, Offsets.LuaTarotData, out data) &&
            _lua.TryGetTable(data, Offsets.LuaTarotStateData, out stateData);
    }

    private TarotState? Read(LuaTable manager, LuaTable data, LuaTable stateData)
    {
        if (!_lua.TryGetString(data, Offsets.LuaTarotState, out var state)) return null;

        TryGetInt(manager, Offsets.LuaTarotRound, out var round);
        if (state == Offsets.TarotStateCollect)
        {
            TryGetInt(stateData, Offsets.LuaTarotPlayNum, out var progress);
            return new TarotState(TarotPhase.Collect, round, progress, 0, 0, [], [], [], 0);
        }

        if (state == Offsets.TarotStateFinal)
        {
            TryGetInt(stateData, Offsets.LuaTarotChallengeTimes, out var challenges);
            return new TarotState(TarotPhase.Final, round, 0, 0, 0, [], [], [], challenges);
        }

        return state == Offsets.TarotStateBoard ? ReadBoard(stateData, round) : null;
    }

    private TarotState? ReadBoard(LuaTable stateData, int round)
    {
        if (!_lua.TryGetTable(stateData, Offsets.LuaTarotCardPlay, out var cardPlay) ||
            !TryReadTileMap(cardPlay, out var tiles, out var position))
            return null;

        TryGetInt(cardPlay, Offsets.LuaTarotHp, out var hp);

        return new TarotState(TarotPhase.Board, round, 0, hp, position, tiles, ReadHand(cardPlay), ReadTaken(cardPlay), 0);
    }

    private bool TryReadTileMap(LuaTable cardPlay, out TarotTile[] tiles, out int position)
    {
        tiles = [];
        position = 0;
        if (!_lua.TryGetTable(cardPlay, Offsets.LuaTarotTileMap, out var tileMap)) return false;
        (bool ok, TarotTile tile)[] reads;
        if (TryLazy(tileMap, out var fields))
        {
            if (fields is null) return false;
            reads = [.. Messages(fields, Offsets.TarotProtoTileMapTiles).Take(Offsets.MaxTarotTiles)
                .Select((bytes, index) => (TryReadTile(index, Parse(bytes), out var tile), tile))];
            position = (int)Varint(fields, Offsets.TarotProtoTileMapPosition);
        }
        else
        {
            if (!_lua.TryGetTable(tileMap, Offsets.LuaTarotTiles, out var tileTable)) return false;
            reads = [.. _lua.IntKeyedTables(tileTable).Where(t => t.Key >= 1 && t.Key <= int.MaxValue).OrderBy(t => t.Key).Take(Offsets.MaxTarotTiles)
                .Select(t => (TryReadTile(t.Key, t.Value, out var tile), tile))];
            TryGetInt(tileMap, Offsets.LuaTarotPosition, out position);
        }

        tiles = [.. reads.Select(r => r.tile)];
        return reads.All(r => r.ok) && tiles.Select((t, i) => t.Index == i).All(inOrder => inOrder) && position >= 0 && position < tiles.Length;
    }

    private bool TryReadTile(long key, LuaTable table, out TarotTile tile)
    {
        tile = default;
        if (TryLazy(table, out var fields)) return fields is not null && TryReadTile((int)key - 1, fields, out tile);
        if (!TryGetInt(table, Offsets.LuaTarotCfg, out var cfg)) return false;
        tile = new TarotTile((int)(key - 1), cfg, ReadChest(table));
        return true;
    }

    private static bool TryReadTile(int index, ProtoField[]? fields, out TarotTile tile)
    {
        tile = default;
        if (fields is null) return false;
        var chest = Messages(fields, Offsets.TarotProtoTileReward).LastOrDefault();
        tile = new TarotTile(index, (int)Varint(fields, Offsets.TarotProtoTileCfg), chest is null ? 0 : Rarity((int)Varint(Parse(chest), Offsets.TarotProtoRewardCfg)));
        return true;
    }

    private int ReadChest(LuaTable tile) =>
        _lua.TryGetTable(tile, Offsets.LuaTarotReward, out var reward) && TryReadRewardRarity(reward, out var rarity) ? rarity : 0;

    private bool TryReadRewardRarity(LuaTable reward, out int rarity)
    {
        rarity = 0;
        if (TryLazy(reward, out var fields))
        {
            if (fields is null) return false;
            rarity = Rarity((int)Varint(fields, Offsets.TarotProtoRewardCfg));
            return true;
        }

        if (!TryGetInt(reward, Offsets.LuaTarotCfg, out var cfg)) return false;
        rarity = Rarity(cfg);
        return true;
    }

    private TarotCard[] ReadHand(LuaTable cardPlay)
    {
        if (!_lua.TryGetTable(cardPlay, Offsets.LuaTarotCardGroup, out var group)) return [];
        if (TryLazy(group, out var fields))
            return fields is null ? [] : [.. Messages(fields, Offsets.TarotProtoGroupCards).Select(b => Parse(b)).Select(CardFrom).Where(c => c is not null).Select(c => c!.Value).Take(Offsets.MaxTarotCards)];
        return _lua.TryGetTable(group, Offsets.LuaTarotCards, out var cards)
            ? [.. _lua.IntKeyedTables(cards).OrderBy(c => c.Key)
                .Select(c => (ok: TryReadCard(c.Value, out var card), card)).Where(c => c.ok).Select(c => c.card).Take(Offsets.MaxTarotCards)]
            : [];
    }

    private static TarotCard? CardFrom(ProtoField[]? fields) =>
        fields is null ? null : new TarotCard((int)Varint(fields, Offsets.TarotProtoCardId), (int)Varint(fields, Offsets.TarotProtoCardCfg));

    private bool TryReadCard(LuaTable table, out TarotCard card)
    {
        card = default;
        if (TryLazy(table, out var fields))
        {
            if (CardFrom(fields) is not { } lazy) return false;
            card = lazy;
            return true;
        }

        if (!TryGetInt(table, Offsets.LuaTarotId, out var id) || !TryGetInt(table, Offsets.LuaTarotCfg, out var cfg)) return false;
        card = new TarotCard(id, cfg);
        return true;
    }

    private bool TryLazy(LuaTable table, out ProtoField[]? fields)
    {
        fields = null;
        if (!_lua.TryGetBytesAt(table, Offsets.TarotLazyBytesKey, out var bytes)) return false;
        fields = Parse(bytes);
        return true;
    }

    private static ProtoField[]? Parse(byte[] bytes) => Protobuf.TryParse(bytes, out var fields) ? fields : null;

    private static byte[][] Messages(ProtoField[] fields, int number) => [.. fields.Where(f => f.Number == number).Select(f => f.Bytes)];

    private static long Varint(ProtoField[]? fields, int number) => fields?.LastOrDefault(f => f.Number == number).Varint ?? 0;

    private static int Rarity(int cfg) => cfg >= 1 ? (cfg - 1) % Offsets.TarotChestRarities + 1 : 0;

    private int[] ReadTaken(LuaTable cardPlay) =>
        _lua.TryGetTable(cardPlay, Offsets.LuaTarotRecvRewards, out var rewards)
            ? [.. _lua.IntKeyedTables(rewards).OrderBy(r => r.Key)
                .Select(r => (ok: TryReadRewardRarity(r.Value, out var rarity), rarity)).Where(r => r.ok && r.rarity > 0).Select(r => r.rarity).Take(Offsets.MaxTarotRewards)]
            : [];

    private bool TryGetInt(LuaTable table, string key, out int value)
    {
        value = 0;
        if (!_lua.TryGetInt(table, key, out var raw) || raw < int.MinValue || raw > int.MaxValue) return false;
        value = (int)raw;
        return true;
    }
}
