using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using EmberHUD;

namespace Mcp;

internal static class UeTools
{
    const int ScanBudgetMs = 5000;
    const int MaxContainer = 0x4000;
    const int MaxNodes = 300;

    public static Tool[] Create() =>
    [
        new("ue_find", "Find UObjects by class (inheritance aware) and / or name substring. Default__ objects are skipped unless includeDefaults. Scans GObjects with a 5 second budget; timedOut says it stopped early.",
            Schema.Of([], ("class", "string", "class name, matches subclasses too"), ("name", "string", "case-insensitive substring of the object name"), ("includeDefaults", "boolean", "include Default__ objects, default false"), ("limit", "integer", "1 to 1000, default 100")), Find),
        new("ue_class", "A class or script struct with its super chain, size and properties (own + inherited, by offset). Pass a name (looked up among Class / ScriptStruct objects) or a hex address.",
            Schema.Of(["class"], ("class", "string", "class name, or a hex address starting 0x")), Class),
        new("ue_object", "Read a UObject's fields formatted by their reflected types. depth 2 or more follows object pointers and expands structs (at most 300 nodes, cycles are not followed).",
            Schema.Of(["address"], ("address", "string", "hex address of the UObject"), ("depth", "integer", "1 to 4, default 1")), Object),
        new("ue_name", "Resolve an FName index through FNamePool.", Schema.Of(["index"], ("index", "integer", "FName index")), Name),
    ];

    static void RequireAttached()
    {
        if (!Memory.Attached) throw new ToolError("memory is not attached - run EmberHUD live with the game open");
    }

    static JsonObject Find(Args args)
    {
        RequireAttached();
        var className = args.OptStr("class");
        var name = args.OptStr("name");
        var includeDefaults = args.Bool("includeDefaults", false);
        var limit = args.Int("limit", 100, 1, 1000);
        var objects = Ue.Objects();
        if (objects.Count == 0) throw new ToolError("no UObjects readable - GObjects is not resolved yet");
        var clock = Stopwatch.StartNew();
        var rows = new JsonArray();
        var scanned = 0;
        var timedOut = false;
        foreach (var obj in objects)
        {
            if (clock.ElapsedMilliseconds > ScanBudgetMs)
            {
                timedOut = true;
                break;
            }
            scanned++;
            if (!includeDefaults && obj.IsDefault) continue;
            if (className is not null && !obj.IsA(className)) continue;
            if (name is not null && !obj.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add(Describe(obj));
            if (rows.Count >= limit) break;
        }
        return new JsonObject { ["scanned"] = scanned, ["total"] = objects.Count, ["timedOut"] = timedOut, ["objects"] = rows };
    }

    static JsonObject Describe(UObj obj) => new()
    {
        ["address"] = Hex.Of(obj.Address),
        ["index"] = obj.Index,
        ["class"] = obj.Class?.Name,
        ["classAddress"] = Hex.Of(obj.ClassAddress),
        ["name"] = obj.Name,
        ["fullName"] = obj.FullName,
    };

    static JsonObject Class(Args args)
    {
        RequireAttached();
        var text = args.Str("class");
        var info = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? Ue.Struct(args.Addr("class")) : ByName(text);
        if (info is null) throw new ToolError($"class {text} not found");
        var chain = new JsonArray();
        for (var super = info.Super; super is not null; super = super.Super) chain.Add(super.Name);
        return new JsonObject
        {
            ["address"] = Hex.Of(info.Address),
            ["name"] = info.Name,
            ["size"] = info.Size,
            ["super"] = chain,
            ["properties"] = new JsonArray(info.Properties.Select(prop => (JsonNode)Prop(prop)).ToArray()),
        };
    }

    static UStructInfo? ByName(string name) =>
        Ue.Objects().Where(obj => obj.Name == name && (obj.IsA("Class") || obj.Class?.Name == "ScriptStruct")).Select(obj => Ue.Struct(obj.Address)).FirstOrDefault(info => info is not null);

    static JsonObject Prop(UProp prop)
    {
        var result = new JsonObject { ["name"] = prop.Name, ["type"] = prop.Type, ["offset"] = Hex.Of(prop.Offset), ["size"] = prop.Size, ["arrayDim"] = prop.ArrayDim };
        if (prop.BoolMask != 0) result["boolMask"] = Hex.Of(prop.BoolMask);
        if (prop.Struct != 0) result["struct"] = Ue.Struct(prop.Struct)?.Name ?? Hex.Of(prop.Struct);
        if (prop.Element is { } element) result["element"] = Prop(element);
        return result;
    }

    static JsonObject Object(Args args)
    {
        RequireAttached();
        var at = args.Addr("address");
        var depth = args.Int("depth", 1, 1, 4);
        if (!Ue.TryObject(at, out var obj)) throw new ToolError($"{Hex.Of(at)} is not a UObject");
        return new Walker().Object(obj, depth);
    }

    static JsonObject Name(Args args)
    {
        RequireAttached();
        var index = args.RequiredInt("index", 0, int.MaxValue);
        return new JsonObject { ["index"] = index, ["name"] = Ue.Name(index) ?? throw new ToolError($"no name at index {index}") };
    }

    sealed class Walker
    {
        readonly HashSet<nint> _seen = [];
        int _nodes;

        public JsonObject Object(UObj obj, int depth)
        {
            _seen.Add(obj.Address);
            var result = new JsonObject { ["address"] = Hex.Of(obj.Address), ["name"] = obj.FullName };
            if (obj.Class is not { } info)
            {
                result["error"] = "class unreadable";
                return result;
            }
            var size = Math.Min(info.Size, MaxContainer);
            var bytes = new byte[Math.Max(size, 0)];
            if (bytes.Length == 0 || !Memory.Read(obj.Address, bytes))
            {
                result["error"] = "memory unreadable";
                return result;
            }
            result["fields"] = Fields(bytes, info, depth);
            return result;
        }

        JsonArray Fields(byte[] bytes, UStructInfo info, int depth)
        {
            var fields = new JsonArray();
            foreach (var prop in info.Properties)
            {
                if (++_nodes > MaxNodes)
                {
                    fields.Add(new JsonObject { ["truncated"] = true });
                    break;
                }
                var field = new JsonObject { ["name"] = prop.Name, ["type"] = prop.Type, ["offset"] = Hex.Of(prop.Offset), ["value"] = Ue.Format(bytes, prop) };
                Expand(field, bytes, prop, depth);
                fields.Add(field);
            }
            return fields;
        }

        void Expand(JsonObject field, byte[] bytes, UProp prop, int depth)
        {
            if (prop.Type == "ObjectProperty" && prop.Offset >= 0 && prop.Offset <= bytes.Length - 8)
            {
                var pointer = (nint)BitConverter.ToInt64(bytes, prop.Offset);
                if (pointer == 0) return;
                field["address"] = Hex.Of(pointer);
                if (depth > 1 && _seen.Add(pointer) && Ue.TryObject(pointer, out var target)) field["object"] = Object(target, depth - 1);
            }
            else if (depth > 1 && prop.Type == "StructProperty" && prop.Struct != 0 && Ue.Struct(prop.Struct) is { } inner && prop.Size > 0 && prop.Offset >= 0 && prop.Offset <= bytes.Length && prop.Size <= bytes.Length - prop.Offset)
                field["fields"] = Fields(bytes[prop.Offset..(prop.Offset + prop.Size)], inner, depth - 1);
        }
    }
}
