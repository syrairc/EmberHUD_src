using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EmberHUD;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record RawPin(nint Address, string Label);

internal sealed class RawTree(Snapshots snapshots)
{
    const int MaxDepth = 8;
    const int MaxBlock = 0x4000;
    const int HexDefault = 0x200;
    const int HexStep = 0x100;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    readonly Dictionary<uint, int> _hexLength = [];
    readonly HashSet<nint> _path = [];
    PropertyView? _view;
    string _address = "";
    nint _goto;

    public object? PinRequest;

    public PropertyView View => _view ??= new PropertyView(snapshots, this);

    public bool OpenAll
    {
        get => View.OpenAll;
        set => View.OpenAll = value;
    }

    public void Node(string id, nint address, int depth)
    {
        ImGui.PushID(id);
        if (!Memory.Attached) ImGui.TextDisabled("not attached (--fake or no game)");
        else if (depth > MaxDepth) ImGui.TextDisabled("...");
        else if (!_path.Add(address)) ImGui.TextDisabled($"cycle 0x{address:X}");
        else
        {
            if (Ue.TryObject(address, out var obj) && obj.Class is { } cls) View.Object(address, obj, cls, depth, false);
            else
            {
                View.Header($"0x{address:X}", address);
                var key = ImGui.GetID("length");
                var length = _hexLength.GetValueOrDefault(key, HexDefault);
                var before = length;
                if (ImGui.SmallButton("+")) length = Math.Min(MaxBlock, length + HexStep);
                ImGui.SameLine();
                if (ImGui.SmallButton("-")) length = Math.Max(HexStep, length - HexStep);
                if (length != before) _hexLength[key] = length;
                Hex(address, length, depth);
            }
            _path.Remove(address);
        }
        ImGui.PopID();
    }

    public void Globals()
    {
        if (!Attached()) return;
        foreach (var (name, address) in Memory.Globals.OrderBy(pair => pair.Key))
        {
            if (OpenAll) ImGui.SetNextItemOpen(true);
            if (!ImGui.TreeNode($"{name}  0x{address:X}###{name}")) continue;
            Node(name, address, 1);
            ImGui.TreePop();
        }
    }

    public void Address()
    {
        if (!Attached()) return;
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##address", "0x...", ref _address, 32);
        ImGui.SameLine();
        if (ImGui.Button("Go") && long.TryParse(_address.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, Invariant, out var parsed)) _goto = (nint)parsed;
        if (_goto != 0) Node("goto", _goto, 0);
    }

    static bool Attached()
    {
        if (Memory.Attached) return true;
        ImGui.TextDisabled("not attached (--fake or no game)");
        return false;
    }

    internal void Hex(nint address, int length, int depth)
    {
        var bytes = snapshots.Get(address, length);
        if (bytes is null)
        {
            ImGui.TextDisabled("??");
            return;
        }
        for (var offset = 0; offset + 8 <= bytes.Length; offset += 8)
        {
            ImGui.PushID(offset);
            var row = bytes.AsSpan(offset, 8);
            var pointer = (nint)BitConverter.ToInt64(row);
            var target = PointerLabel(pointer);
            var text = $"{BitConverter.ToString(bytes, offset, 8).Replace('-', ' ')}   {BitConverter.ToInt32(row)} {BitConverter.ToInt32(row[4..])}   {BitConverter.ToSingle(row).ToString("0.###", Invariant)} {BitConverter.ToSingle(row[4..]).ToString("0.###", Invariant)}   {BitConverter.ToDouble(row).ToString("G6", Invariant)}";
            if (View.Line($"+{offset:X}", target is not null, target ?? "", "", text, target is null ? 0 : pointer, depth))
            {
                Node("target", pointer, depth + 1);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
    }

    // ponytail: PointerLabel and the object-property values call Ue.TryObject live every frame instead of going through Snapshots - a few hundred reads per open hex block, cache labels per refresh if it shows up
    string? PointerLabel(nint pointer)
    {
        if (pointer < 0x10000 || pointer > 0x7FFF_FFFF_0000) return null;
        if (Ue.TryObject(pointer, out var obj)) return $"-> {obj.Class?.Name ?? "?"} {obj.Name}";
        return snapshots.Get(pointer, 8) is null ? null : "->";
    }
}
