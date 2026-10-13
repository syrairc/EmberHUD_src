using System.Reflection;
using System.Runtime.Loader;
using EmberHUD.Testing;

namespace EmberHUD.Host.Plugins;

internal static class CheckRunner
{
    public static int Run(string pluginsDirectory, TextWriter report) => Run(typeof(CheckRunner).Assembly, pluginsDirectory, report);

    internal static int Run(Assembly host, string pluginsDirectory, TextWriter report)
    {
        var failed = JitCheck.Run(host, report);
        int plugins = 0, tests = 0;
        foreach (var directory in PluginCache.Plugins(pluginsDirectory))
        {
            var name = Path.GetFileName(directory);
            plugins++;
            var context = new AssemblyLoadContext($"check-{name}", isCollectible: true);
            try
            {
                var compiled = PluginCompiler.Compile(name, PluginCache.Read(directory, tests: true));
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
                var main = types.Where(type => type.IsSubclassOf(typeof(Plugin)) && !type.IsAbstract).ToList();
                if (main.Count != 1)
                {
                    failed++;
                    report.WriteLine($"FAIL {name} load - found {main.Count} Plugin subclasses, expected one");
                    continue;
                }
                if (((Plugin)Activator.CreateInstance(main[0])!).SettingsType is { } settingsType)
                {
                    var defaults = Path.Combine(directory, "defaults.json");
                    SettingsJson.Load(settingsType, File.Exists(defaults) ? File.ReadAllText(defaults) : null, null, problem =>
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
                        report.WriteLine($"FAIL {label} - {(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex)}");
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
}
