using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using EmberHUD;
using EmberHUD.Testing;

namespace Mcp;

public sealed class RecordTests
{
    sealed class Rig : IDisposable
    {
        public readonly string Directory = Path.Combine(Path.GetTempPath(), "mcp-test-" + Guid.NewGuid().ToString("N"));
        public readonly long[] Clock = [0];
        public readonly Recorder Recorder;
        public readonly Recordings Recordings;
        public readonly Tool[] Tools;

        public Rig(long maxBytes = Recorder.MaxBytes)
        {
            Recorder = new Recorder(Directory, () => Clock[0], maxBytes);
            Recordings = new Recordings(Directory);
            Tools = RecordTools.Create(Recorder, Recordings);
        }

        public void Dispose()
        {
            Recorder.Close();
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
        }
    }

    static GameState State(float playerX, params Entity[] entities) => new("playing", 7, new Player(new Vector3(playerX, 0, 0), Vector2.UnitX), entities, null);

    static Entity Mob(int id, int cfgId, float x, EntityKind kind = EntityKind.Monster) => new(id, cfgId, new Vector3(x, 0, 0), kind, Rarity.Normal);

    static string Start(Rig rig, string label = "run", JsonObject? filter = null)
    {
        var args = filter ?? new JsonObject();
        args["label"] = label;
        return (string)Wire.Run(rig.Tools, "record_start", args)["file"]!;
    }

    static void Frame(Rig rig, long at, GameState state)
    {
        rig.Clock[0] = at;
        rig.Recorder.Sample(state, 10);
    }

    [Test]
    public void FramesAreWrittenAtTheRequestedRate()
    {
        using var rig = new Rig();
        var file = Start(rig);
        Check.That(file.EndsWith("-run.jsonl"), file);
        Frame(rig, 0, State(0, Mob(1, 5, 1)));
        Frame(rig, 50, State(1));
        Frame(rig, 100, State(2, Mob(1, 5, 2), Mob(2, 6, 9)));
        Frame(rig, 250, State(3));
        var stopped = Wire.Run(rig.Tools, "record_stop");
        Check.Equal(3, (int)stopped["frames"]!);
        var lines = File.ReadAllLines(Path.Combine(rig.Directory, file));
        Check.Equal(3, lines.Length);
        var second = (JsonObject)JsonNode.Parse(lines[1])!;
        Check.Equal(100, (int)second["t"]!);
        Check.Equal(7, (int)second["area"]!);
        Check.Equal(2, second["entities"]!.AsArray().Count);
        Check.That(second["entities"]![0]!["distance"] is null, "frames carry no distance");
        Check.Equal(2d, (double)second["player"]!["x"]!);
    }

    [Test]
    public void StatusAndLifecycle()
    {
        using var rig = new Rig();
        Check.Equal(false, (bool)Wire.Run(rig.Tools, "record_status")["active"]!);
        Check.That(Wire.Fails(rig.Tools, "record_stop").Contains("not recording"), "stop without start");
        var file = Start(rig);
        Check.Equal(true, rig.Recorder.Active);
        Check.That(Wire.Fails(rig.Tools, "record_start", new JsonObject { ["label"] = "again" }).Contains(file), "already recording names the file");
        Frame(rig, 0, State(0));
        var status = Wire.Run(rig.Tools, "record_status");
        Check.Equal(true, (bool)status["active"]!);
        Check.Equal(file, (string?)status["file"]);
        Check.Equal(1, (int)status["frames"]!);
        Wire.Run(rig.Tools, "record_stop");
        Check.Equal(false, rig.Recorder.Active);
    }

    [Test]
    public void TheFilterAppliesToEveryFrame()
    {
        using var rig = new Rig();
        var file = Start(rig, "mobs", new JsonObject { ["kind"] = "Monster", ["radius"] = 10 });
        Frame(rig, 0, State(0, Mob(1, 5, 5), Mob(2, 5, 50), Mob(3, 5, 2, EntityKind.Portal)));
        Wire.Run(rig.Tools, "record_stop");
        var frame = (JsonObject)JsonNode.Parse(File.ReadAllLines(Path.Combine(rig.Directory, file))[0])!;
        Check.Equal("1", string.Join(',', frame["entities"]!.AsArray().Select(row => (int)row!["id"]!)));
    }

    [Test]
    public void ARadiusFilterWithoutAPlayerRecordsNoEntities()
    {
        using var rig = new Rig();
        var file = Start(rig, "mobs", new JsonObject { ["radius"] = 10 });
        rig.Clock[0] = 0;
        rig.Recorder.Sample(new GameState("loading", 0, null, [Mob(1, 5, 5)], null), 10);
        Wire.Run(rig.Tools, "record_stop");
        var frame = (JsonObject)JsonNode.Parse(File.ReadAllLines(Path.Combine(rig.Directory, file))[0])!;
        Check.Equal(0, frame["entities"]!.AsArray().Count);
        Check.That(frame["player"] is null, "no player");
    }

    [Test]
    public void LabelsAreMadeFileSafe()
    {
        using var rig = new Rig();
        var file = Start(rig, "..\\..\\evil name?");
        Check.That(!file.Contains('\\') && !file.Contains('/') && !file.Contains('?') && !file.Contains(' '), file);
        Check.That(File.Exists(Path.Combine(rig.Directory, file)), "created inside the folder");
    }

    [Test]
    public void RecorderStopsAtItsSizeCap()
    {
        using var rig = new Rig(maxBytes: 600);
        Start(rig);
        for (var i = 0; i < 20 && rig.Recorder.Active; i++) Frame(rig, i * 100, State(i, Mob(1, 5, i), Mob(2, 6, i)));
        Check.Equal(false, rig.Recorder.Active);
        var status = Wire.Run(rig.Tools, "record_status");
        Check.That(((string?)status["stoppedBecause"])!.Contains("size"), "says why");
        Check.That(new FileInfo(Directory.GetFiles(rig.Directory)[0]).Length < 1500, "the file stopped growing");
    }

    [Test]
    public void ListSummaryRangeAndSearchReadItBack()
    {
        using var rig = new Rig();
        var file = Start(rig);
        Frame(rig, 0, State(0, Mob(1, 5, 1), Mob(2, 6, 4, EntityKind.Portal)));
        Frame(rig, 100, State(3, Mob(1, 5, 2)));
        Frame(rig, 200, State(3, Mob(1, 5, 2), Mob(3, 5, 8)));
        Wire.Run(rig.Tools, "record_stop");

        var list = Wire.Run(rig.Tools, "recording_list")["recordings"]!.AsArray();
        Check.Equal(1, list.Count);
        Check.Equal(file, (string?)list[0]!["file"]);
        Check.Equal(3, (int)list[0]!["frames"]!);
        Check.Equal(200, (int)list[0]!["durationMs"]!);

        var summary = Wire.Run(rig.Tools, "recording_summary", new JsonObject { ["file"] = file });
        Check.Equal(3, (int)summary["frames"]!);
        Check.Equal("7", string.Join(',', summary["areas"]!.AsArray().Select(area => (int)area!)));
        Check.Equal(3d, (double)summary["playerPathLength"]!);
        var monster = summary["kinds"]!["Monster"]!;
        Check.Equal(1, (int)monster["distinctCfgIds"]!);
        Check.Equal(3, (int)monster["cfgIds"]![0]!["frames"]!);
        Check.Equal(0, (int)monster["cfgIds"]![0]!["firstT"]!);
        Check.Equal(200, (int)monster["cfgIds"]![0]!["lastT"]!);

        var range = Wire.Run(rig.Tools, "recording_range", new JsonObject { ["file"] = file, ["from"] = 1, ["to"] = 5 })["frames"]!.AsArray();
        Check.Equal("1,2", string.Join(',', range.Select(frame => (int)frame!["index"]!)));

        var search = Wire.Run(rig.Tools, "recording_search", new JsonObject { ["file"] = file, ["cfgId"] = 6 })["frames"]!.AsArray();
        Check.Equal(1, search.Count);
        Check.Equal(0, (int)search[0]!["index"]!);
        Check.Equal(1, search[0]!["matches"]!.AsArray().Count);
        var byKind = Wire.Run(rig.Tools, "recording_search", new JsonObject { ["file"] = file, ["kind"] = "Monster", ["limit"] = 2 })["frames"]!.AsArray();
        Check.Equal(2, byKind.Count);
    }

    [Test]
    public void RecordingNamesCannotEscapeTheFolder()
    {
        using var rig = new Rig();
        Start(rig);
        Wire.Run(rig.Tools, "record_stop");
        var outside = Path.Combine(Path.GetTempPath(), "mcp-outside-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(outside, "{\"t\":0}\n");
        try
        {
            foreach (var bad in new[] { "..\\x.jsonl", "../x.jsonl", outside, "sub\\x.jsonl", "notes.txt", "", "x.jsonl\0" })
                foreach (var tool in new[] { "recording_summary", "recording_range", "recording_search" })
                    Check.That(Wire.Fails(rig.Tools, tool, new JsonObject { ["file"] = bad }).Contains("file"), $"{tool} {bad}");
            Check.That(Wire.Fails(rig.Tools, "recording_summary", new JsonObject { ["file"] = "missing.jsonl" }).Contains("no recording"), "missing");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Test]
    public void ARecordingBeingWrittenCanBeReadAndTornLinesAreSkipped()
    {
        using var rig = new Rig();
        var file = Start(rig);
        Frame(rig, 0, State(0, Mob(1, 5, 1)));
        Frame(rig, 3000, State(1, Mob(1, 5, 1)));
        var summary = Wire.Run(rig.Tools, "recording_summary", new JsonObject { ["file"] = file });
        Check.That((int)summary["frames"]! >= 1, "readable while open");
        Wire.Run(rig.Tools, "record_stop");
        File.AppendAllText(Path.Combine(rig.Directory, file), "{\"t\":9999,\"ent");
        Check.Equal(2, (int)Wire.Run(rig.Tools, "recording_summary", new JsonObject { ["file"] = file })["frames"]!);
    }

    [Test]
    public void QueryLimitsAreChecked()
    {
        using var rig = new Rig();
        var file = Start(rig);
        Wire.Run(rig.Tools, "record_stop");
        Check.That(Wire.Fails(rig.Tools, "recording_range", new JsonObject { ["file"] = file, ["from"] = 0, ["to"] = 5, ["limit"] = 500 }).Contains("between"), "range limit");
        Check.That(Wire.Fails(rig.Tools, "recording_search", new JsonObject { ["file"] = file }).Contains("cfgId or kind"), "search needs a criterion");
        Check.That(Wire.Fails(rig.Tools, "recording_range", new JsonObject { ["file"] = file, ["from"] = 5, ["to"] = 1 }).Contains("from"), "from after to");
    }

    [Test]
    public void GarbageLinesAreSkippedByEveryQuery()
    {
        using var rig = new Rig();
        var file = Start(rig);
        Frame(rig, 0, State(0, Mob(1, 5, 1)));
        Frame(rig, 100, State(3, Mob(1, 5, 2)));
        Wire.Run(rig.Tools, "record_stop");
        var path = Path.Combine(rig.Directory, file);
        File.AppendAllLines(path,
        [
            "{\"t\":200,\"area\":7,\"status\":\"playing\",\"player\":null,\"entities\":[null]}",
            "{\"t\":210,\"area\":7,\"status\":\"playing\",\"player\":null,\"entities\":[{\"cfgId\":\"x\",\"kind\":\"Monster\"}]}",
            "{\"t\":220,\"area\":7,\"status\":\"playing\",\"player\":null,\"entities\":[{\"cfgId\":5,\"kind\":3}]}",
            "{\"t\":230,\"area\":7,\"status\":\"playing\",\"player\":{\"x\":\"a\",\"y\":0,\"z\":0},\"entities\":[]}",
            "{\"t\":240,\"area\":7,\"status\":\"playing\",\"player\":{\"x\":1e308,\"y\":-1e308,\"z\":0},\"entities\":[]}",
            "{\"t\":250,\"area\":7,\"status\":\"playing\",\"player\":{\"x\":-1e308,\"y\":1e308,\"z\":0},\"entities\":[]}",
        ]);
        var summary = Wire.Run(rig.Tools, "recording_summary", new JsonObject { ["file"] = file });
        Check.That((int)summary["frames"]! >= 2, "good frames counted");
        Check.That(double.IsFinite((double)summary["playerPathLength"]!), "finite path");
        var good = (int)summary["frames"]!;
        Check.Equal(good, Wire.Run(rig.Tools, "recording_range", new JsonObject { ["file"] = file, ["from"] = 0, ["to"] = 50 })["frames"]!.AsArray().Count);
        Check.Equal(good, (int)Wire.Run(rig.Tools, "recording_list")["recordings"]![0]!["frames"]!);
        Check.Equal(2, Wire.Run(rig.Tools, "recording_search", new JsonObject { ["file"] = file, ["cfgId"] = 5 })["frames"]!.AsArray().Count);
        Check.Equal(2, Wire.Run(rig.Tools, "recording_search", new JsonObject { ["file"] = file, ["kind"] = "Monster" })["frames"]!.AsArray().Count);
    }

    [Test]
    public void ListReportsFramesAndDurationOfAMultiFrameFile()
    {
        using var rig = new Rig();
        Start(rig);
        for (var i = 0; i < 5; i++) Frame(rig, i * 100, State(i, Mob(1, 5, i)));
        Wire.Run(rig.Tools, "record_stop");
        var entry = Wire.Run(rig.Tools, "recording_list")["recordings"]![0]!;
        Check.Equal(5, (int)entry["frames"]!);
        Check.Equal(400, (int)entry["durationMs"]!);
    }

    [Test]
    public void ASamplingFailureStopsTheRecordingInsteadOfThrowing()
    {
        using var rig = new Rig();
        Start(rig);
        rig.Clock[0] = 0;
        rig.Recorder.Sample(new GameState("playing", 7, null, null!, null), 10);
        Check.Equal(false, rig.Recorder.Active);
        Check.That(((string?)Wire.Run(rig.Tools, "record_status")["stoppedBecause"])!.Contains("frame failed"), "says why");
    }

    [Test]
    public void StartingAgainInTheSameSecondWithTheSameLabelGetsANewFile()
    {
        using var rig = new Rig();
        var first = Start(rig);
        Wire.Run(rig.Tools, "record_stop");
        var second = Start(rig);
        Wire.Run(rig.Tools, "record_stop");
        var third = Start(rig);
        Check.That(first != second && second != third && first != third, $"{first} {second} {third}");
        Check.Equal(3, Directory.GetFiles(rig.Directory).Length);
    }

    [Test]
    public void AFailedStartLeavesNoCurrentFile()
    {
        using var rig = new Rig();
        Directory.CreateDirectory(rig.Directory);
        var blocker = Path.Combine(rig.Directory, "sub");
        File.WriteAllText(blocker, "x");
        var recorder = new Recorder(blocker, () => 0);
        try
        {
            recorder.Start("run", new EntityFilter(null, null, null, null));
        }
        catch (IOException)
        {
        }
        Check.That(recorder.CurrentFile is null && !recorder.Active, "nothing active");
        Check.That(recorder.Status()["file"] is null, "file not assigned");
    }
}
