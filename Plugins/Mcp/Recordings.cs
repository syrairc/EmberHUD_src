using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal sealed class Recordings(string directory)
{
    const int ListCap = 50;

    public JsonObject List(string? activeFile)
    {
        if (!Directory.Exists(directory)) return new JsonObject { ["recordings"] = new JsonArray() };
        var files = new DirectoryInfo(directory).GetFiles("*.jsonl").OrderByDescending(file => file.Name, StringComparer.Ordinal).Take(ListCap);
        return new JsonObject
        {
            ["recordings"] = new JsonArray(files.Select(file =>
            {
                var frames = 0;
                long lastT = 0;
                foreach (var (_, frame) in Frames(file.FullName))
                {
                    frames++;
                    lastT = (long)frame["t"]!;
                }
                return (JsonNode)new JsonObject
                {
                    ["file"] = file.Name,
                    ["sizeBytes"] = file.Length,
                    ["frames"] = frames,
                    ["durationMs"] = Ms(lastT),
                    ["active"] = file.Name == activeFile,
                };
            }).ToArray()),
        };
    }

    public JsonObject Summary(string file)
    {
        var areas = new SortedSet<int>();
        var kinds = new Dictionary<string, Dictionary<int, (int Frames, long First, long Last)>>();
        var frames = 0;
        var path = 0d;
        long duration = 0;
        (double X, double Y)? previous = null;
        foreach (var (_, frame) in Frames(Resolve(file)))
        {
            frames++;
            var t = (long)frame["t"]!;
            duration = t;
            areas.Add((int)frame["area"]!);
            if (frame["player"] is JsonObject player)
            {
                var here = (player["x"]!.GetValue<double>(), player["y"]!.GetValue<double>());
                if (previous is { } last)
                {
                    var step = Math.Sqrt(Math.Pow(here.Item1 - last.X, 2) + Math.Pow(here.Item2 - last.Y, 2));
                    if (double.IsFinite(step) && double.IsFinite(path + step)) path += step;
                }
                previous = here;
            }
            var inThisFrame = new HashSet<(string, int)>();
            foreach (var entity in frame["entities"]!.AsArray())
            {
                var kind = (string)entity!["kind"]!;
                var cfgId = (int)entity["cfgId"]!;
                if (!inThisFrame.Add((kind, cfgId))) continue;
                if (!kinds.TryGetValue(kind, out var seen)) kinds[kind] = seen = [];
                seen[cfgId] = seen.TryGetValue(cfgId, out var known) ? (known.Frames + 1, known.First, t) : (1, t, t);
            }
        }
        return new JsonObject
        {
            ["file"] = file,
            ["frames"] = frames,
            ["durationMs"] = Ms(duration),
            ["areas"] = new JsonArray(areas.Select(area => (JsonNode)area).ToArray()),
            ["playerPathLength"] = Math.Round(path, 2),
            ["kinds"] = new JsonObject(kinds.OrderBy(kind => kind.Key, StringComparer.Ordinal).Select(kind => KeyValuePair.Create(kind.Key, (JsonNode?)new JsonObject
            {
                ["distinctCfgIds"] = kind.Value.Count,
                ["cfgIds"] = new JsonArray(kind.Value.OrderByDescending(pair => pair.Value.Frames).ThenBy(pair => pair.Key).Take(30).Select(pair => (JsonNode)new JsonObject
                {
                    ["cfgId"] = pair.Key,
                    ["frames"] = pair.Value.Frames,
                    ["firstT"] = Ms(pair.Value.First),
                    ["lastT"] = Ms(pair.Value.Last),
                }).ToArray()),
            }))),
        };
    }

    public JsonObject Range(string file, int from, int to, int limit)
    {
        if (from > to) throw new ToolError("from must not be after to");
        var frames = new JsonArray();
        foreach (var (index, frame) in Frames(Resolve(file)))
        {
            if (index < from) continue;
            if (index > to || frames.Count >= limit) break;
            frame["index"] = index;
            frames.Add(frame);
        }
        return new JsonObject { ["file"] = file, ["frames"] = frames };
    }

    public JsonObject Search(string file, int? cfgId, EntityKind? kind, int limit)
    {
        var path = Resolve(file);
        if (cfgId is null && kind is null) throw new ToolError("give cfgId or kind");
        var frames = new JsonArray();
        foreach (var (index, frame) in Frames(path))
        {
            var matches = frame["entities"]!.AsArray().Where(entity => (cfgId is null || (int)entity!["cfgId"]! == cfgId) && (kind is null || (string?)entity!["kind"] == kind.ToString())).Take(20).Select(entity => entity!.DeepClone()).ToArray();
            if (matches.Length == 0) continue;
            frames.Add(new JsonObject { ["index"] = index, ["t"] = frame["t"]!.DeepClone(), ["matches"] = new JsonArray(matches) });
            if (frames.Count >= limit) break;
        }
        return new JsonObject { ["file"] = file, ["frames"] = frames };
    }

    static bool Valid(JsonObject? frame)
    {
        if (frame is null) return false;
        try
        {
            if (frame["player"] is not null)
            {
                if (frame["player"] is not JsonObject player) return false;
                foreach (var axis in new[] { "x", "y" })
                    if (player[axis] is not JsonValue value || !value.TryGetValue<double>(out var number) || !double.IsFinite(number)) return false;
            }
            if (frame["entities"] is not JsonArray entities) return false;
            foreach (var entity in entities)
                if (entity is not JsonObject row || row["cfgId"] is not JsonValue cfg || !cfg.TryGetValue<int>(out _) || row["kind"] is not JsonValue kind || !kind.TryGetValue<string>(out _)) return false;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static int Ms(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    internal string Resolve(string file)
    {
        if (file.Length == 0 || file != Path.GetFileName(file) || !file.EndsWith(".jsonl", StringComparison.Ordinal) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ToolError("file must be a plain *.jsonl name from recording_list");
        var path = Path.Combine(directory, file);
        return File.Exists(path) ? path : throw new ToolError($"no recording named {file}");
    }

    static IEnumerable<(int Index, JsonObject Frame)> Frames(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var index = 0;
        while (reader.ReadLine() is { } line)
        {
            JsonObject? frame;
            try
            {
                frame = JsonNode.Parse(line) as JsonObject;
            }
            catch (JsonException)
            {
                continue;
            }
            if (!Valid(frame)) continue;
            if (frame?["t"] is not JsonValue time || !time.TryGetValue<long>(out _) || frame["area"] is not JsonValue area || !area.TryGetValue<int>(out _) || frame["entities"] is not JsonArray) continue;
            yield return (index++, frame);
        }
    }
}
