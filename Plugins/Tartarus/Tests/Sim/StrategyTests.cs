using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberHUD.Testing;

namespace Tartarus.Sim;

sealed record StrategyBoard(string Name, OracleBoard Board, OracleAction Action, string Label, string Why);

sealed record StrategyFile(string Reference, StrategyBoard[] Boards);

public sealed class StrategyTests
{
    static readonly Lazy<StrategyFile> Recorded = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Plugins", "Tartarus", "Tests", "Oracle", "strategy.json");
        return JsonSerializer.Deserialize<StrategyFile>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    });

    static Move Ours(StrategyBoard board) => Strategy.Choose(OracleData.ToRun(board.Board, 1), new Rng(1));

    [Test]
    public void LiveBoardOfTheEighteenthPicksTheMagicWolfDoor()
    {
        var board = Recorded.Value.Boards[0];
        Check.Equal(new Move(MoveKind.Door, 11), OracleData.MoveOf(board.Action));
        Check.Equal(new Move(MoveKind.Door, 11), Ours(board));
    }

    [Test]
    public void AMagicInmateWithNoMealIsReplaced()
    {
        var board = Recorded.Value.Boards[1];
        Check.Equal(new Move(MoveKind.Replace), OracleData.MoveOf(board.Action));
        Check.Equal(new Move(MoveKind.Replace), Ours(board));
    }

    [Test]
    public void PicksWhatTheReferenceStrategyPicksOnRecordedBoards()
    {
        var boards = Recorded.Value.Boards;
        Check.That(boards.Length >= 200, "strategy.json has too few boards");
        var disagreements = new List<string>();
        foreach (var board in boards)
        {
            var ours = Ours(board);
            var reference = OracleData.MoveOf(board.Action);
            if (ours != reference) disagreements.Add($"{board.Name}: ours {ours}, reference {reference}");
        }
        Console.WriteLine($"strategy agrees on {boards.Length - disagreements.Count} of {boards.Length} boards");
        Check.That(disagreements.Count * 20 <= boards.Length, $"{disagreements.Count} of {boards.Length} boards disagree: {string.Join(" | ", disagreements)}");
    }
}
