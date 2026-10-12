using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aember;
using Hexa.NET.ImGui;

namespace Inspector;

internal sealed record Member(string Name, Type Type, Func<object, object?> Get);

internal sealed record Failed(string Exception);

internal static class Walker
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly Dictionary<Type, Member[]> _members = [];
    static readonly HashSet<(Type, string)> _logged = [];
    static readonly JsonSerializerOptions _json = new()
    {
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(), new ColorJson(), new SpriteJson() }
    };

    internal static int ErrorsLogged => _logged.Count;

    public static void Clear()
    {
        _members.Clear();
        _logged.Clear();
    }

    public static Member[] Members(Type type)
    {
        if (_members.TryGetValue(type, out var cached)) return cached;
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => new Member(p.Name, p.PropertyType, o => p.GetValue(o)));
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => new Member(f.Name, f.FieldType, f.GetValue));
        return _members[type] = [.. properties, .. fields];
    }

    public static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Sprite) || type == typeof(ImTextureRef);
    }

    public static bool IsFlat(Type type) => IsScalar(type) || typeof(IEnumerable).IsAssignableFrom(type);

    public static Type ElementType(Type type) =>
        type.IsArray ? type.GetElementType()! :
        type.GetInterfaces().Prepend(type).FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0] ?? typeof(object);

    public static int? Count(IEnumerable items) => items is ICollection collection ? collection.Count : null;

    public static string Format(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        nint pointer => $"0x{pointer:X}",
        float f => f.ToString("0.00", Invariant),
        double d => d.ToString("0.00", Invariant),
        Vector3 v => $"{v.X.ToString("0.0", Invariant)}, {v.Y.ToString("0.0", Invariant)}, {v.Z.ToString("0.0", Invariant)}",
        Vector2 v => $"{v.X.ToString("0.0", Invariant)}, {v.Y.ToString("0.0", Invariant)}",
        Quaternion q => $"{q.X.ToString("0.00", Invariant)}, {q.Y.ToString("0.00", Invariant)}, {q.Z.ToString("0.00", Invariant)}, {q.W.ToString("0.00", Invariant)}",
        Color c => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}",
        Sprite s => $"{s.Uv0.X.ToString("0.000", Invariant)}, {s.Uv0.Y.ToString("0.000", Invariant)} .. {s.Uv1.X.ToString("0.000", Invariant)}, {s.Uv1.Y.ToString("0.000", Invariant)}",
        ImTextureRef => "texture",
        Failed failed => $"<error: {failed.Exception}>",
        Enum e => e.ToString(),
        ICollection collection => $"{value.GetType().Name} [{collection.Count}]",
        IFormattable formattable => formattable.ToString(null, Invariant),
        _ => value.ToString() ?? ""
    };

    public static object? Read(Type owner, Member member, object target)
    {
        try
        {
            return member.Get(target);
        }
        catch (Exception exception)
        {
            var inner = exception is TargetInvocationException { InnerException: { } cause } ? cause : exception;
            if (_logged.Add((owner, member.Name))) Log.Warn($"Inspector: {owner.Name}.{member.Name} threw {inner.GetType().Name}");
            return new Failed(inner.GetType().Name);
        }
    }

    public static bool HasPosition(Type type) => Members(type).Any(IsPosition);

    public static Vector3? Position(object? row)
    {
        if (row is null) return null;
        var type = row.GetType();
        return Members(type).FirstOrDefault(IsPosition) is { } member && Read(type, member, row) is Vector3 position ? position : null;
    }

    public static float? Distance(object? row, Vector3 from) =>
        Position(row) is { } p ? MathF.Sqrt((p.X - from.X) * (p.X - from.X) + (p.Y - from.Y) * (p.Y - from.Y)) : null;

    public static string Json(object row)
    {
        try
        {
            return JsonSerializer.Serialize(row, row.GetType(), _json);
        }
        catch (Exception exception)
        {
            return $"<error: {exception.GetType().Name}>";
        }
    }

    static bool IsPosition(Member member) => member.Name == "Position" && member.Type == typeof(Vector3);

    sealed class ColorJson : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue(Format(value));
    }

    sealed class SpriteJson : JsonConverter<Sprite>
    {
        public override Sprite Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, Sprite value, JsonSerializerOptions options) => writer.WriteStringValue(Format(value));
    }
}
