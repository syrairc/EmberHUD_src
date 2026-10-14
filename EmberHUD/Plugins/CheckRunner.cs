using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using EmberHUD.Testing;

namespace EmberHUD.Host.Plugins;

internal static class CheckRunner
{
    public static int Run(string pluginsDirectory, TextWriter report) => Run(typeof(CheckRunner).Assembly, pluginsDirectory, report);

    internal static int Run(Assembly host, string pluginsDirectory, TextWriter report)
    {
        var failed = Jit(host, report);
        int plugins = 0, tests = 0;
        foreach (var directory in PluginCompiler.Plugins(pluginsDirectory))
        {
            var name = Path.GetFileName(directory);
            plugins++;
            var context = new AssemblyLoadContext($"check-{name}", isCollectible: true);
            try
            {
                var compiled = PluginCompiler.Compile(name, PluginCompiler.Read(directory, tests: true));
                if (compiled.Assembly is null)
                {
                    failed++;
                    report.WriteLine($"FAIL {name} compile");
                    foreach (var line in compiled.Diagnostics) report.WriteLine($"    {line}");
                    continue;
                }
                report.WriteLine($"PASS {name} compile");
                using var stream = new MemoryStream(compiled.Assembly);
                var types = context.LoadFromStream(stream).GetTypes();
                Type main;
                try
                {
                    main = PluginCompiler.FindPluginType(types);
                }
                catch (InvalidOperationException ex)
                {
                    failed++;
                    report.WriteLine($"FAIL {name} load - {ex.Message}");
                    continue;
                }
                if (((Plugin)Activator.CreateInstance(main)!).SettingsType is { } settingsType)
                {
                    SettingsJson.Load(settingsType, PluginCompiler.ReadDefaults(directory), null, problem =>
                    {
                        failed++;
                        report.WriteLine($"FAIL {name} defaults - {problem}");
                    });
                }
                foreach (var method in types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Where(method => method.IsDefined(typeof(TestAttribute))))
                {
                    tests++;
                    var label = $"{name} {method.DeclaringType!.Name}.{method.Name}";
                    try
                    {
                        if (method.ReturnType != typeof(void) || method.GetParameters().Length > 0) throw new InvalidOperationException("a [Test] method takes no parameters and returns void");
                        Game.State = GameState.Empty;
                        Map.View = null;
                        TestInput.Hold();
                        TestMemory.Clear();
                        Log.CurrentPlugin.Value = name;
                        method.Invoke(method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType), null);
                        report.WriteLine($"PASS {label}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        report.WriteLine($"FAIL {label} - {PluginCompiler.Unwrap(ex)}");
                    }
                    finally
                    {
                        Log.CurrentPlugin.Value = null;
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                report.WriteLine($"FAIL {name} load - {ex}");
            }
            finally
            {
                context.Unload();
            }
        }
        report.WriteLine($"check: {plugins} plugins, {tests} tests, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    internal static int Jit(Assembly assembly, TextWriter report)
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
