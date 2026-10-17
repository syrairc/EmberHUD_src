using System;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class RunRecorderTests
{
    static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    static DateTime At(double seconds) => T0.AddSeconds(seconds);

    static TartarusCell Cell(int index, int tier, TartarusBeast beast = TartarusBeast.Wolf, bool open = true) => new(index, open, tier, beast, false, false);

    static TartarusState Board(params TartarusCell[] cells) => new(0, 16, 1, 9, cells, null, [], 0);

    static RunEntry? Step(RunRecorder recorder, TartarusState? state, double seconds, int? nameplates = null) => recorder.Update(state, nameplates, At(seconds), seconds);

    [Test]
    public void RunStartsWhenTartarusAppears()
    {
        var recorder = new RunRecorder();
        Check.That(Step(recorder, null, 0) is null);
        Check.That(Step(recorder, Board(), 1) is null);
        var run = Step(recorder, null, 5);
        Check.That(run is null);
        run = Step(recorder, null, 9);
        Check.Equal(At(1), run!.Start);
    }

    [Test]
    public void RunEndsOnlyAfterThreeSecondsOfNull()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(), 10);
        Check.That(Step(recorder, null, 11) is null);
        Check.That(Step(recorder, null, 13.9) is null);
        var run = Step(recorder, null, 14);
        Check.That(run is not null);
        Check.Equal(At(10), run!.End);
        Check.That(Step(recorder, null, 20) is null);
    }

    [Test]
    public void ABriefNullDoesNotEndTheRun()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(2, 1)), 10);
        Step(recorder, null, 11);
        Step(recorder, null, 12.5);
        Check.That(Step(recorder, Board(Cell(5, 2)), 13) is null);
        Step(recorder, null, 14);
        var run = Step(recorder, null, 17);
        Check.Equal(At(10), run!.Start);
        Check.Equal(2, run.Cells.Length);
    }

    [Test]
    public void OpenCellsAreRecordedOnce()
    {
        var recorder = new RunRecorder();
        var board = Board(Cell(3, 2, TartarusBeast.Lion), Cell(7, 0, TartarusBeast.Rat), Cell(9, 1, TartarusBeast.Unknown), Cell(11, 1, open: false), Cell(12, -1));
        Step(recorder, board, 1);
        Step(recorder, board, 2);
        Step(recorder, null, 3);
        var run = Step(recorder, null, 6)!;
        Check.Equal(2, run.Cells.Length);
        Check.Equal(new RunCell(3, (int)TartarusBeast.Lion, 2), run.Cells[0]);
        Check.Equal(new RunCell(7, (int)TartarusBeast.Rat, 0), run.Cells[1]);
    }

    [Test]
    public void SameBeastTierRiseKeepsTheHighestTier()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(4, 1, TartarusBeast.Wolf)), 1);
        Step(recorder, Board(Cell(4, 3, TartarusBeast.Wolf)), 2);
        Step(recorder, Board(Cell(4, 2, TartarusBeast.Wolf)), 3);
        Step(recorder, null, 4);
        var run = Step(recorder, null, 7)!;
        Check.Equal(new RunCell(4, (int)TartarusBeast.Wolf, 3), run.Cells.Single());
    }

    [Test]
    public void ABeastChangeAtTheSameIndexIsANewPrisoner()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(4, 1, TartarusBeast.Wolf)), 1);
        Step(recorder, Board(Cell(4, 3, TartarusBeast.Rat)), 2);
        Step(recorder, null, 4);
        var run = Step(recorder, null, 7)!;
        Check.Equal(2, run.Cells.Length);
        Check.Equal(new RunCell(4, (int)TartarusBeast.Wolf, 1), run.Cells[0]);
        Check.Equal(new RunCell(4, (int)TartarusBeast.Rat, 3), run.Cells[1]);
    }

    [Test]
    public void LockedThenReopenedIsANewPrisoner()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(2, 0, TartarusBeast.Mammoth)), 1);
        Step(recorder, Board(Cell(2, 0, TartarusBeast.Mammoth, open: false)), 2);
        Step(recorder, Board(Cell(2, 0, TartarusBeast.Mammoth, open: false)), 3);
        Step(recorder, Board(Cell(2, 1, TartarusBeast.Lion)), 4);
        Step(recorder, Board(Cell(2, 2, TartarusBeast.Lion)), 5);
        Step(recorder, null, 6);
        var run = Step(recorder, null, 9)!;
        Check.Equal(2, run.Cells.Length);
        Check.Equal(new RunCell(2, (int)TartarusBeast.Mammoth, 0), run.Cells[0]);
        Check.Equal(new RunCell(2, (int)TartarusBeast.Lion, 2), run.Cells[1]);
    }

    [Test]
    public void SameBeastReopenedAfterLockIsANewPrisoner()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(2, 1, TartarusBeast.Lion)), 1);
        Step(recorder, Board(Cell(2, 0, TartarusBeast.Lion, open: false)), 2);
        Step(recorder, Board(Cell(2, 1, TartarusBeast.Lion)), 3);
        Step(recorder, null, 4);
        Check.Equal(2, Step(recorder, null, 7)!.Cells.Length);
    }

    [Test]
    public void DevouredCellKeepsItsLastRecord()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(Cell(6, 2, TartarusBeast.Lion)), 1);
        Step(recorder, Board(Cell(6, 0, TartarusBeast.Lion, open: false)), 2);
        Step(recorder, null, 3);
        var run = Step(recorder, null, 6)!;
        Check.Equal(new RunCell(6, (int)TartarusBeast.Lion, 2), run.Cells.Single());
    }

    [Test]
    public void NameplatesBeforeAndAfter()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(), 1, 40);
        Step(recorder, Board(), 2, 55);
        Step(recorder, null, 3, 58);
        var run = Step(recorder, null, 6, 61)!;
        Check.Equal(40, run.NameplatesBefore);
        Check.Equal(61, run.NameplatesAfter);

        Step(recorder, Board(), 10, 70);
        Step(recorder, Board(), 11, 75);
        Step(recorder, null, 12, null);
        run = Step(recorder, null, 15, null)!;
        Check.Equal(70, run.NameplatesBefore);
        Check.Equal(75, run.NameplatesAfter);
    }

    [Test]
    public void NoNameplatesIsNull()
    {
        var recorder = new RunRecorder();
        Step(recorder, Board(), 1, null);
        Step(recorder, Board(), 2, 33);
        Step(recorder, null, 3);
        var run = Step(recorder, null, 6)!;
        Check.That(run.NameplatesBefore is null);
        Check.Equal(33, run.NameplatesAfter);
        Step(recorder, Board(), 7);
        Step(recorder, null, 8);
        run = Step(recorder, null, 11)!;
        Check.That(run.NameplatesBefore is null);
        Check.That(run.NameplatesAfter is null);
    }

    [Test]
    public void NameplateCountSumsTheMoneyItem()
    {
        InventoryItem Item(int baseId, int count) => new(InventoryBag.Other, 0, "", baseId, 1, 0, 0, count, 0, 0);
        Check.Equal(130, Nameplates.Count([Item(828, 100), Item(77, 9), Item(828, 30)]));
        Check.That(Nameplates.Count([Item(77, 9)]) is null);
        Check.That(Nameplates.Count(null) is null);
    }
}
