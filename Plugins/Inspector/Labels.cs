using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using EmberHUD;

namespace Inspector;

internal static class Labels
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly string[] FallbackNames = ["Id", "Name", "Kind"];

    static readonly Dictionary<Type, Func<object, int, string>> Registry = new()
    {
        [typeof(Entity)] = Of<Entity>((e, i) => $"{i}  {Join(e.Kind, e.Name)}  cfg {e.CfgId}  {At(e.Position)}"),
        [typeof(MapIcon)] = Of<MapIcon>((m, i) => $"{i}  {Join(m.Texture, m.Name)}  {At(m.Position)}"),
        [typeof(InventoryItem)] = Of<InventoryItem>((m, i) => $"{i}  {m.Bag} {m.Slot}  base {m.BaseId}  x{m.Count}"),
        [typeof(QuestTarget)] = Of<QuestTarget>((q, i) => $"{i}  {Join(q.Id, q.Name)}  {At(q.Position)}"),
        [typeof(Projectile)] = Of<Projectile>((p, i) => $"{i}  Projectile {p.Id}  owner {p.OwnerId} {p.OwnerRarity}  {At(p.Position)}"),
        [typeof(GroundHazard)] = Of<GroundHazard>((h, i) => $"{i}  {h.Kind} hazard {h.Id}  r {h.Radius.ToString("0", Invariant)}  {At(h.Centre)}"),
        [typeof(GroundItem)] = Of<GroundItem>((g, i) => $"{i}  Drop {g.DropId}  base {g.BaseId}  x{g.Count}  {At(g.Position)}"),
        [typeof(Buff)] = Of<Buff>((b, i) => $"{i}  {Join(b.Id, b.Name)}  x{b.Stacks}  {b.Remaining.ToString("0.0", Invariant)}s"),
        [typeof(Skill)] = Of<Skill>((s, i) => $"{i}  {Join(s.CfgId, s.Name)}  slot {s.Slot}  {(s.Ready ? "ready" : s.CooldownRemaining.ToString("0.0", Invariant) + "s")}"),
    };

    public static string Of(object? item, int index)
    {
        if (item is null) return "null";
        var type = item.GetType();
        if (Walker.IsScalar(type)) return Walker.Format(item);
        return Registry.TryGetValue(type, out var label) ? label(item, index) : Fallback(item, type, index);
    }

    static Func<object, int, string> Of<T>(Func<T, int, string> label) => (item, index) => label((T)item, index);

    static string Join(object first, string? second) => string.IsNullOrEmpty(second) ? $"{first}" : $"{first} {second}";

    static string At(Vector3 position) => $"{position.X.ToString("0", Invariant)}, {position.Y.ToString("0", Invariant)}";

    static string At(Vector2 position) => $"{position.X.ToString("0", Invariant)}, {position.Y.ToString("0", Invariant)}";

    static string Fallback(object item, Type type, int index)
    {
        var values = Walker.Members(type)
            .Where(m => FallbackNames.Contains(m.Name) && Walker.IsScalar(m.Type))
            .Select(m => Walker.Format(Walker.Read(type, m, item)));
        return string.Join("  ", values.Prepend(type.Name).Prepend(index.ToString(Invariant)));
    }
}
