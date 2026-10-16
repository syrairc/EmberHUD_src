using System.Reflection;

namespace EmberHUD.Tests;

static class ObfuscationCheck
{
    public static List<Type> Unmarked(Type root, Assembly scope)
    {
        var reached = new HashSet<Type>();
        Walk(root, scope, reached);
        return [.. reached.Where(type => !IsMarked(type))];
    }

    static void Walk(Type type, Assembly scope, HashSet<Type> reached)
    {
        if (type.HasElementType)
        {
            Walk(type.GetElementType()!, scope, reached);
            return;
        }
        foreach (var argument in type.GenericTypeArguments) Walk(argument, scope, reached);
        if (type.Assembly != scope || !reached.Add(type)) return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) Walk(property.PropertyType, scope, reached);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Walk(field.FieldType, scope, reached);
    }

    static bool IsMarked(Type type)
    {
        for (var declaring = type; declaring is not null; declaring = declaring.DeclaringType)
            if (declaring.GetCustomAttribute<ObfuscationAttribute>() is { } attribute)
                return attribute.Exclude && attribute.ApplyToMembers;
        return false;
    }
}
