using System;
using System.Drawing;
using System.Numerics;
using EmberHUD;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace Tarot;

public sealed class Main : Plugin<Settings>
{
    const float Pad = 8;
    const float Gap = 24;
    const float TileGap = 2;
    const int TilesPerRow = 4;
    readonly TextStyle _style = TextStyle.Hud();

    public override void Tick() => Game.WantTarot();

    public override void Render()
    {
        if (!Settings.ShowPanel || Game.State.Tarot is not { } state) return;
        var rowHeight = Draw.RowHeight(_style);
        var origin = ImGui.GetIO().DisplaySize * new Vector2(Settings.PanelX, Settings.PanelY);
        var at = origin + new Vector2(Pad);
        var rows = Rows(state);
        var width = 0f;
        foreach (var (left, right, _) in rows) width = MathF.Max(width, Draw.MeasureText(left, _style).X + Gap + Draw.MeasureText(right, _style).X);
        var side = rowHeight - TileGap;
        var rowStart = state.Position / TilesPerRow * TilesPerRow;
        var ahead = state.Phase == TarotPhase.Board ? Math.Clamp(Settings.AheadTiles, 0, Math.Max(0, state.Tiles.Length - rowStart)) : 0;
        var tileRows = (ahead + TilesPerRow - 1) / TilesPerRow;
        var hasTaken = state.Phase == TarotPhase.Board && state.Taken.Length > 0;
        width = MathF.Max(width, TilesPerRow * (side + TileGap));
        var height = rowHeight * (rows.Count + (hasTaken ? 1 : 0) + tileRows);
        Draw.RectFilled(origin, origin + new Vector2(width, height) + new Vector2(Pad * 2), GameColors.LabelBackground, 4);
        foreach (var (left, right, colour) in rows)
        {
            Draw.Row(at, width, left, "", _style);
            var rightStyle = _style.Clone();
            rightStyle.Color = colour;
            Draw.Text(right, new Vector2(at.X + width, at.Y), rightStyle, Align.Right);
            at.Y += rowHeight;
        }
        if (hasTaken)
        {
            Draw.Text("Taken", at, _style);
            var x = at.X + Draw.MeasureText("Taken", _style).X + TileGap * 3;
            foreach (var rarity in state.Taken)
            {
                Draw.RectFilled(new Vector2(x, at.Y), new Vector2(x + side, at.Y + side), Board.ChestColor(rarity));
                x += side + TileGap;
            }
            at.Y += rowHeight;
        }
        for (var i = 0; i < ahead; i++)
        {
            var tile = state.Tiles[rowStart + i];
            var min = new Vector2(at.X + i % TilesPerRow * (side + TileGap), at.Y + i / TilesPerRow * rowHeight);
            DrawTile(min, side, tile);
            if (rowStart + i == state.Position) Draw.Rect(min - new Vector2(1), min + new Vector2(side + 1), GameColors.Text, 2);
        }
    }

    static void DrawTile(Vector2 min, float side, TarotTile tile)
    {
        var kind = Board.Kind(tile);
        var max = min + new Vector2(side);
        if (kind == TileKind.Empty) { Draw.Rect(min, max, GameColors.TextDim); return; }
        Draw.RectFilled(min, max, kind switch
        {
            TileKind.Chest => Board.ChestColor(tile.Chest),
            TileKind.Trap => GameColors.Negative,
            TileKind.Heal => GameColors.Positive,
            TileKind.ChestUp => GameColors.Gold,
            _ => GameColors.TextDim,
        });
    }

    System.Collections.Generic.List<(string Left, string Right, Color Colour)> Rows(TarotState state)
    {
        var rows = new System.Collections.Generic.List<(string, string, Color)>();
        switch (state.Phase)
        {
            case TarotPhase.Collect: rows.Add(("Tarot", $"trials {state.Progress}", GameColors.Text)); break;
            case TarotPhase.Final: rows.Add(("Tarot", $"challenges left {state.ChallengesLeft}", GameColors.Text)); break;
            default:
                rows.Add(("Tarot", $"HP {state.Hp}  tile {state.Position + 1}/{state.Tiles.Length}  round {state.Round}", GameColors.Text));
                foreach (var card in state.Hand) rows.Add((Board.CardLabel(card.CfgId), Outcome(state, card), OutcomeColour(state, card)));
                break;
        }
        return rows;
    }

    static string Outcome(TarotState state, TarotCard card) =>
        Board.Landing(state, card) is { } landing && landing != state.Position ? $"-> {landing + 1} {Board.Kind(state.Tiles[landing])}" : "";

    static Color OutcomeColour(TarotState state, TarotCard card)
    {
        if (Board.Landing(state, card) is not { } landing || landing == state.Position) return GameColors.TextDim;
        var tile = state.Tiles[landing];
        return Board.Kind(tile) switch
        {
            TileKind.Trap => GameColors.Negative,
            TileKind.Heal => GameColors.Positive,
            TileKind.Chest => Board.ChestColor(tile.Chest),
            _ => GameColors.Text,
        };
    }
}
