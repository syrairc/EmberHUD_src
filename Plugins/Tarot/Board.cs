using System;
using System.Drawing;
using EmberHUD;

namespace Tarot;

public enum TileKind { Empty, Trap, Heal, Chest, ChestUp, Note, TrapClear, ChestMaker, Other }

public static class Board
{
    static readonly int[] NamedAdvances = [2, 1, 4, 3, 2, 3, 5, 2, 6];

    public static int? Steps(int cardCfg) => cardCfg switch
    {
        >= 1 and <= 6 => cardCfg,
        >= 11 and <= 16 => -(cardCfg - 10),
        >= 101 and <= 106 => cardCfg - 100,
        >= 1001 and <= 1009 => NamedAdvances[cardCfg - 1001],
        _ => null,
    };

    public static TileKind Kind(TarotTile tile) => tile.Chest > 0 ? TileKind.Chest : tile.CfgId switch
    {
        10000 => TileKind.Empty,
        1000 => TileKind.Trap,
        (>= 1051 and <= 1053) or (>= 9205 and <= 9207) => TileKind.Heal,
        (>= 9201 and <= 9204) or (>= 9217 and <= 9219) => TileKind.ChestUp,
        9221 or 9222 => TileKind.TrapClear,
        >= 9223 and <= 9225 => TileKind.ChestMaker,
        (>= 1001 and <= 1043) or (>= 1101 and <= 1104) or 1201 or 1202 or (>= 3001 and <= 3003) or 5002 or 5003 or (>= 9208 and <= 9216) or 9220 or (>= 9301 and <= 9312) => TileKind.Note,
        _ => TileKind.Other,
    };

    public static int? Landing(TarotState state, TarotCard card) =>
        state.Tiles.Length > 0 && Steps(card.CfgId) is { } steps ? Math.Clamp(state.Position + steps, 0, state.Tiles.Length - 1) : null;

    public static string CardLabel(int cardCfg) => Steps(cardCfg) switch
    {
        > 0 and var steps => $"Advance {steps}",
        < 0 and var steps => $"Retreat {-steps}",
        _ => cardCfg switch
        {
            >= 21 and <= 24 => "Pick a move",
            >= 31 and <= 33 => $"Life +{cardCfg - 30}",
            _ => $"Card {cardCfg}",
        },
    };

    public static Color ChestColor(int rarity) => rarity switch
    {
        1 => Color.FromArgb(0xD5, 0xD5, 0xD5),
        2 => Color.FromArgb(0x5F, 0xD3, 0x5F),
        3 => Color.FromArgb(0x4F, 0xA3, 0xFF),
        4 => Color.FromArgb(0xB2, 0x66, 0xFF),
        5 => GameColors.Gold,
        6 => GameColors.Negative,
        7 => Color.FromArgb(0xFF, 0x7F, 0xD4),
        _ => GameColors.TextDim,
    };
}
