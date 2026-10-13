using System;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal static class WatchTools
{
    public static Tool[] Create(Watches watches) =>
    [
        new("watch_start", "Sample a memory range every frame. Give address + length (max 4096), or object (UObject address) + property (reflected name). At most 16 watches. Returns the id.",
            Schema.Of([], ("address", "string", "hex address"), ("length", "integer", "bytes, 1 to 4096"), ("object", "string", "hex address of a UObject"), ("property", "string", "reflected property name on that object"), ("label", "string", "your own name for the watch")),
            args => Start(args, watches), ReadOnly: false, Idempotent: false),
        new("watch_read", "Bytes that changed since the previous watch_read, as runs of offset / was / now (hex), plus the current bytes, how many frames were sampled and how many of them changed. Reading moves the baseline forward. Changes that flip back between two reads are still reported.",
            Schema.Of(["id"], ("id", "integer", "watch id")), args => watches.Read(args.RequiredInt("id", 1, int.MaxValue)), Idempotent: false),
        new("watch_stop", "Stop one watch by id, or all of them.", Schema.Of([], ("id", "integer", "watch id"), ("all", "boolean", "stop every watch")), args => Stop(args, watches), ReadOnly: false, Idempotent: false),
    ];

    static JsonObject Start(Args args, Watches watches)
    {
        var label = args.OptStr("label") ?? "";
        nint at;
        int length;
        if (args.OptStr("property") is { } property)
        {
            var objectAt = args.Addr("object");
            if (!Ue.TryObject(objectAt, out var obj)) throw new ToolError($"{Hex.Of(objectAt)} is not a UObject");
            var prop = obj.Class?.Property(property) ?? throw new ToolError($"property {property} not found on {obj.Class?.Name ?? "the object"}");
            if (prop.Offset < 0) throw new ToolError($"property {property} has offset {prop.Offset}, expected 0 or more");
            var bytes = (long)Math.Max(1, prop.Size) * Math.Max(1, prop.ArrayDim);
            if (bytes > Watches.MaxLength) throw new ToolError($"property {property} is {bytes} bytes, watches are limited to 1..{Watches.MaxLength}");
            at = objectAt + prop.Offset;
            length = (int)bytes;
            if (label.Length == 0) label = property;
        }
        else
        {
            at = args.Addr("address");
            length = args.RequiredInt("length", 1, Watches.MaxLength);
        }
        var id = watches.Start(label, at, length);
        return new JsonObject { ["id"] = id, ["address"] = Hex.Of(at), ["length"] = length, ["label"] = label };
    }

    static JsonObject Stop(Args args, Watches watches)
    {
        if (args.Bool("all", false))
        {
            watches.StopAll();
            return new JsonObject { ["stopped"] = "all" };
        }
        var id = args.OptInt("id") ?? throw new ToolError("give id or all");
        return watches.Stop(id) ? new JsonObject { ["stopped"] = id } : throw new ToolError($"no watch with id {id}");
    }
}
