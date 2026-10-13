using System;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal static class RecordTools
{
    public static Tool[] Create(Recorder recorder, Recordings recordings) =>
    [
        new("record_start", "Start writing Game.State frames to Config\\Mcp\\recordings\\<time>-<label>.jsonl at the RecordRate setting. Optional filter (kind, rarity, cfgId, radius around the player) limits which entities are written. Stops itself at 64 MB.",
            Schema.Of([], ("label", "string", "short name for the file"), ("kind", "string", "EntityKind name"), ("rarity", "string", "Normal, Magic, Rare or Boss"), ("cfgId", "integer", "config id"), ("radius", "number", "world units around the player")),
            args => new JsonObject { ["file"] = recorder.Start(args.OptStr("label") ?? "", EntityFilter.From(args)) }, ReadOnly: false, Idempotent: false),
        new("record_stop", "Stop the running recording and report frames, bytes and the file.", Schema.Of([]), _ => recorder.Stop(), ReadOnly: false, Idempotent: false),
        new("record_status", "Whether a recording is running, its file, frames, bytes, elapsed time, and why the last one stopped.", Schema.Of([]), _ => recorder.Status()),
        new("recording_list", "Newest 50 recordings with size, frames, duration and which one is still being written.", Schema.Of([]), _ => recordings.List(recorder.CurrentFile)),
        new("recording_summary", "One pass over a recording: frames, duration, areas seen, player path length, and per entity kind the cfgIds with how many frames they appear in and first / last time (top 30 per kind).",
            Schema.Of(["file"], ("file", "string", "file name from recording_list")), args => recordings.Summary(args.Str("file"))),
        new("recording_range", "Raw frames by index, from..to inclusive, at most limit (1 to 50, default 10).",
            Schema.Of(["file", "from", "to"], ("file", "string", "file name"), ("from", "integer", "first frame index"), ("to", "integer", "last frame index"), ("limit", "integer", "1 to 50, default 10")),
            args =>
            {
                var file = args.Str("file");
                recordings.Resolve(file);
                return recordings.Range(file, args.RequiredInt("from", 0, int.MaxValue), args.RequiredInt("to", 0, int.MaxValue), args.Int("limit", 10, 1, 50));
            }),
        new("recording_search", "Frames where an entity with this cfgId and / or kind appears, with up to 20 matching rows per frame. limit 1 to 200, default 50.",
            Schema.Of(["file"], ("file", "string", "file name"), ("cfgId", "integer", "config id"), ("kind", "string", "EntityKind name"), ("limit", "integer", "1 to 200, default 50")),
            args => recordings.Search(args.Str("file"), args.OptInt("cfgId"), EntityFilter.From(args).Kind, args.Int("limit", 50, 1, 200))),
    ];
}
