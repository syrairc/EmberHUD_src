using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class RunWriterTests
{
    static readonly DateTimeOffset At = new(2026, 10, 25, 13, 45, 7, TimeSpan.Zero);

    static string NewDirectory() => Path.Combine(Path.GetTempPath(), "mouselab-test-" + Guid.NewGuid().ToString("N"));

    static MoveResult Move(string label, double finalError = 2.5) => new(
        "Row sweep",
        label,
        new PathScores(420.5, 310, 335.25, 0.73, 1.9, finalError, 0.5, 1.5, 0.12, 0.34, 16),
        new Verdict(true, true, true, false),
        [new PathSample(0, 10, 20), new PathSample(4, 12.5, 21), new PathSample(8, 300, 40)]);

    static RunResult Finished(string preset = "Normal") => new(preset, [Move("(6,0)"), Move("(0,0)", 7)], ["Diagonal: no stash cell (6,4)"], false, null);

    static RunResult Cancelled(string preset = "Fast") => new(preset, [Move("(6,0)")], [], true, null);

    static void With(Action<string> body)
    {
        var directory = NewDirectory();
        try
        {
            body(directory);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void WriterCreatesTheDirectory()
    {
        With(directory =>
        {
            var nested = Path.Combine(directory, "a", "b");
            var path = RunWriter.Write(nested, [Finished()], At);
            Check.That(File.Exists(path), "json written");
            Check.Equal("run-20261025-134507.json", Path.GetFileName(path));
        });
    }

    [Test]
    public void WriterProducesJsonAndCsvAndMarksCancelled()
    {
        With(directory =>
        {
            var path = RunWriter.Write(directory, [Finished(), Cancelled(), new RunResult("Slow", [], [], true, null)], At);
            var csv = Path.ChangeExtension(path, ".csv");
            Check.That(File.Exists(csv), "csv beside the json");
            var lines = File.ReadAllLines(csv);
            Check.That(lines[0].StartsWith("Preset,Scenario,Label,"), lines[0]);
            Check.Equal(5, lines.Length, "header + 2 + 1 + 1 empty cancelled");
            Check.That(lines.Skip(1).Take(2).All(line => line.EndsWith(",complete")), "finished rows complete");
            Check.That(lines[3].StartsWith("Fast,") && lines[3].EndsWith(",cancelled"), lines[3]);
            Check.That(lines[4].StartsWith("Slow,") && lines[4].EndsWith(",cancelled"), lines[4]);
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var results = json.RootElement.GetProperty("Results");
            Check.That(!results[0].GetProperty("Cancelled").GetBoolean() && results[1].GetProperty("Cancelled").GetBoolean(), "cancelled flag in the json");
        });
    }

    [Test]
    public void CsvQuotesLabelsThatHoldCommas()
    {
        With(directory =>
        {
            var csv = Path.ChangeExtension(RunWriter.Write(directory, [Finished()], At), ".csv");
            Check.That(File.ReadAllLines(csv)[1].StartsWith("Normal,Row sweep,\"(6,0)\",420.5"), File.ReadAllLines(csv)[1]);
        });
    }

    [Test]
    public void JsonRoundTripsTheScores()
    {
        With(directory =>
        {
            var written = Finished();
            var path = RunWriter.Write(directory, [written], At);
            var read = JsonSerializer.Deserialize<RunFile>(File.ReadAllText(path))!;
            Check.Equal(At, read.At);
            var result = read.Results.Single();
            Check.Equal("Normal", result.Preset);
            Check.Equal(2, result.Moves.Count);
            Check.That(result.Moves[0].Scores == written.Moves[0].Scores, "scores");
            Check.That(result.Moves[1].Verdict == written.Moves[1].Verdict, "verdict");
            Check.That(result.Moves[0].Trail.SequenceEqual(written.Moves[0].Trail), "trail points");
            Check.Equal("Diagonal: no stash cell (6,4)", result.Skipped.Single());
        });
    }
}
