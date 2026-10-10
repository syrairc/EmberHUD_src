using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TorchlightSDK.Host.Plugins;

internal static class JitCheck
{
    public static int Run(Assembly assembly, TextWriter report)
    {
        Type[] types;
        var failed = 0;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            foreach (var error in e.LoaderExceptions) report.WriteLine($"host: type load - {error?.Message}");
            failed = Math.Max(1, e.LoaderExceptions.Length);
            types = e.Types.OfType<Type>().ToArray();
        }

        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var prepared = 0;
        foreach (var type in types.Where(type => !type.ContainsGenericParameters))
            foreach (var method in type.GetMethods(declared).Concat<MethodBase>(type.GetConstructors(declared)))
            {
                if (method.IsAbstract || method.ContainsGenericParameters || method.Attributes.HasFlag(MethodAttributes.PinvokeImpl)
                    || method.MethodImplementationFlags.HasFlag(MethodImplAttributes.Runtime) || method.IsDefined(typeof(UnmanagedCallersOnlyAttribute), false))
                    continue;
                try
                {
                    RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    prepared++;
                }
                catch (Exception e)
                {
                    failed++;
                    report.WriteLine($"host: jit {type.FullName}.{method.Name} - {e.GetType().Name}: {e.Message}");
                }
            }
        report.WriteLine($"host: jit {prepared} methods, {failed} failed");
        return failed;
    }
}
