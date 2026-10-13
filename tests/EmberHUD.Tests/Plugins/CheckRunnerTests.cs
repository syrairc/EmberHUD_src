using System.Runtime.Loader;
using EmberHUD.Host.Plugins;

namespace EmberHUD.Tests;

public sealed class CheckRunnerTests : IDisposable
{
    readonly PluginFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    (int Code, string Report) Check()
    {
        var report = new StringWriter();
        return (CheckRunner.Run(_fixture.Plugins, report), report.ToString());
    }

    [Fact]
    public void PassingTestsReachInternalMembers()
    {
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", "internal static int Twice(int x) => x * 2;"));
        _fixture.Write("Good", "Tests/MainTests.cs", """
            using EmberHUD.Testing;

            namespace Good;

            public sealed class MainTests
            {
                [Test]
                public void Doubles() => Check.Equal(4, Main.Twice(2));

                [Test]
                public static void StaticWorksToo() => Check.That(true);
            }
            """);
        var (code, report) = Check();
        Assert.Equal(0, code);
        Assert.Contains("PASS Good compile", report);
        Assert.Contains("PASS Good MainTests.Doubles", report);
        Assert.Contains("PASS Good MainTests.StaticWorksToo", report);
        Assert.EndsWith("check: 1 plugins, 2 tests, 0 failed" + Environment.NewLine, report);
    }

    [Fact]
    public void AFailingTestFailsTheRun()
    {
        _fixture.Write("Bad", "Main.cs", PluginFixture.Source("Bad", ""));
        _fixture.Write("Bad", "Tests/MainTests.cs", """
            using EmberHUD.Testing;

            namespace Bad;

            public sealed class MainTests
            {
                [Test]
                public void Fails() => Check.Equal(1, 2, "sum");
            }
            """);
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Bad MainTests.Fails - System.InvalidOperationException: sum - expected 1, got 2", report);
        Assert.Contains("check: 1 plugins, 1 tests, 1 failed", report);
    }

    [Fact]
    public void ACompileErrorInTheTestsFailsTheRun()
    {
        _fixture.Write("Broken", "Main.cs", PluginFixture.Source("Broken", ""));
        _fixture.Write("Broken", "Tests/MainTests.cs", "namespace Broken; public static class T { public static int X = \"no\"; }");
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Broken compile", report);
        Assert.Contains($"    {Path.Combine("Tests", "MainTests.cs")}(1,", report);
    }

    [Fact]
    public void EveryTestStartsFromAnEmptyGameState()
    {
        _fixture.Write("Fresh", "Main.cs", PluginFixture.Source("Fresh", ""));
        _fixture.Write("Fresh", "Tests/StateTests.cs", """
            using EmberHUD;
            using EmberHUD.Testing;

            namespace Fresh;

            public sealed class StateTests
            {
                [Test]
                public void First()
                {
                    Check.Equal(0, Game.State.AreaId);
                    TestGame.Set(GameState.Empty with { AreaId = 5 });
                }

                [Test]
                public void Second()
                {
                    Check.Equal(0, Game.State.AreaId);
                    TestGame.Set(GameState.Empty with { AreaId = 5 });
                }
            }
            """);
        var (code, report) = Check();
        Assert.True(code == 0, report);
    }

    [Fact]
    public void EveryTestStartsWithNoKeysHeld()
    {
        _fixture.Write("Keys", "Main.cs", PluginFixture.Source("Keys", ""));
        _fixture.Write("Keys", "Tests/KeyTests.cs", """
            using EmberHUD;
            using EmberHUD.Testing;

            namespace Keys;

            public sealed class KeyTests
            {
                [Test]
                public void First() => TestInput.Hold(Key.F8);

                [Test]
                public void Second() => Check.That(!Input.IsDown(Key.F8));
            }
            """);
        var (code, report) = Check();
        Assert.True(code == 0, report);
    }

    [Fact]
    public void ABadDefaultsJsonFieldFailsTheRun()
    {
        _fixture.Write("Defaults", "Main.cs", PluginFixture.Source("Defaults", "", "public RangeNode<float> X = new(10, 0, 100);"));
        _fixture.Write("Defaults", "defaults.json", """{ "X": "big" }""");
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Defaults defaults - X - ", report);
    }

    [Fact]
    public void ATestWithTheWrongShapeFails()
    {
        _fixture.Write("Shape", "Main.cs", PluginFixture.Source("Shape", ""));
        _fixture.Write("Shape", "Tests/ShapeTests.cs", """
            using EmberHUD.Testing;

            namespace Shape;

            public sealed class ShapeTests
            {
                [Test]
                public int Returns() => 1;
            }
            """);
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("takes no parameters and returns void", report);
    }

    [Fact]
    public void ACompileErrorFailsTheRunAndNamesThePlugin()
    {
        _fixture.Write("Broken", "Main.cs", "namespace Broken; public sealed class Main { int x = ; }");
        var (code, report) = Check();
        Assert.NotEqual(0, code);
        Assert.Contains("FAIL Broken compile", report);
    }

    [Fact]
    public void AThrowingTickFailsOnlyThatPlugin()
    {
        _fixture.Write("Boom", "Main.cs", PluginFixture.Source("Boom", "public override void Tick() => throw new InvalidOperationException(\"tick exploded\");"));
        _fixture.Write("Boom", "Tests/MainTests.cs", """
            using EmberHUD.Testing;

            namespace Boom;

            public sealed class MainTests
            {
                [Test]
                public void Ticks() => new Main().Tick();
            }
            """);
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", ""));
        _fixture.Write("Good", "Tests/MainTests.cs", """
            using EmberHUD.Testing;

            namespace Good;

            public sealed class MainTests
            {
                [Test]
                public void Fine() => Check.That(true);
            }
            """);
        var (code, report) = Check();
        Assert.NotEqual(0, code);
        Assert.Contains("FAIL Boom MainTests.Ticks - System.InvalidOperationException: tick exploded", report);
        Assert.Contains("PASS Good MainTests.Fine", report);
        Assert.Contains("check: 2 plugins, 2 tests, 1 failed", report);
    }

    [Fact]
    public void OnlyTheGoodPluginPasses()
    {
        _fixture.Write("Good", "Main.cs", PluginFixture.Source("Good", ""));
        _fixture.Write("Good", "Tests/MainTests.cs", """
            using EmberHUD.Testing;

            namespace Good;

            public sealed class MainTests
            {
                [Test]
                public void Fine() => Check.That(true);
            }
            """);
        var (code, report) = Check();
        Assert.Equal(0, code);
        Assert.Contains("PASS Good MainTests.Fine", report);
    }

    [Fact]
    public void NoPluginsIsAPass()
    {
        var (code, report) = Check();
        Assert.Equal(0, code);
        Assert.Contains("host: jit ", report);
        Assert.Contains("check: 0 plugins, 0 tests, 0 failed", report);
    }

    [Fact]
    public void EveryTestStartsWithoutAMapView()
    {
        _fixture.Write("Maps", "Main.cs", PluginFixture.Source("Maps", ""));
        _fixture.Write("Maps", "Tests/MapTests.cs", """
            using System.Numerics;
            using EmberHUD;
            using EmberHUD.Testing;

            namespace Maps;

            public sealed class MapTests
            {
                [Test]
                public void A_Sets() { Check.That(Map.View is null); Map.View = new MapView(Vector2.Zero, 1f); }

                [Test]
                public void B_Sees() => Check.That(Map.View is null, "view leaked");
            }
            """);
        var (code, report) = Check();
        Assert.True(code == 0, report);
    }

    [Fact]
    public void TestsLogUnderThePluginName()
    {
        var seen = new List<string?>();
        var previous = Log.Sink;
        Log.Sink = (_, plugin, _) => seen.Add(plugin);
        try
        {
            _fixture.Write("Named", "Main.cs", PluginFixture.Source("Named", ""));
            _fixture.Write("Named", "Tests/LogTests.cs", """
                using EmberHUD;
                using EmberHUD.Testing;

                namespace Named;

                public sealed class LogTests
                {
                    [Test]
                    public void Logs() => Log.Info("hello");
                }
                """);
            var (code, report) = Check();
            Assert.True(code == 0, report);
            Assert.Equal(["Named"], seen);
            Assert.Null(Log.CurrentPlugin.Value);
        }
        finally
        {
            Log.Sink = previous;
        }
    }

    [Fact]
    public void NoPluginSubclassFailsToLoad()
    {
        _fixture.Write("Empty", "Main.cs", "namespace Empty; public sealed class Other { }");
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Empty load - found 0 Plugin subclasses, expected one", report);
    }

    [Fact]
    public void TwoPluginSubclassesFailToLoad()
    {
        _fixture.Write("Twin", "Main.cs", PluginFixture.Source("Twin", ""));
        _fixture.Write("Twin", "Second.cs", "using EmberHUD; namespace Twin; public sealed class Second : Plugin<Settings> { }");
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Twin load - found 2 Plugin subclasses, expected one", report);
    }

    static bool ContextAlive(string name) => AssemblyLoadContext.All.Any(context => context.Name == name);

    List<string> RunCapturingUnload(string plugin, int expectedCode)
    {
        var messages = new List<string>();
        var previous = Log.Sink;
        Log.Sink = (_, _, message) => messages.Add(message);
        try
        {
            var (code, report) = Check();
            Assert.True(code == expectedCode, report);
        }
        finally
        {
            Log.Sink = previous;
        }
        AssertUnloaded($"check-{plugin}");
        return messages;
    }

    [Fact]
    public void TheCheckContextIsUnloadedAfterAPassingPlugin()
    {
        _fixture.Write("Unload1", "Main.cs", PluginFixture.Source("Unload1", ""));
        _fixture.Write("Unload1", "Tests/UnloadTests.cs", """
            using System.Runtime.Loader;
            using EmberHUD;
            using EmberHUD.Testing;

            namespace Unload1;

            public sealed class UnloadTests
            {
                [Test]
                public void Watch() => AssemblyLoadContext.GetLoadContext(typeof(UnloadTests).Assembly)!.Unloading += _ => Log.Info("unloaded");
            }
            """);
        Assert.Contains("unloaded", RunCapturingUnload("Unload1", 0));
    }

    [Fact]
    public void TheCheckContextIsUnloadedAfterALoadFailure()
    {
        _fixture.Write("Unload2", "Main.cs", """
            using System;
            using System.Runtime.Loader;
            using EmberHUD;

            namespace Unload2;

            public sealed class Main : Plugin
            {
                public Main()
                {
                    AssemblyLoadContext.GetLoadContext(typeof(Main).Assembly)!.Unloading += _ => Log.Info("unloaded");
                    throw new InvalidOperationException("boom");
                }
            }
            """);
        Assert.Contains("unloaded", RunCapturingUnload("Unload2", 1));
    }

    static void AssertUnloaded(string contextName)
    {
        for (var i = 0; i < 20 && ContextAlive(contextName); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(ContextAlive(contextName), $"{contextName} still loaded");
    }

    [Fact]
    public void ATestWithParametersFails()
    {
        _fixture.Write("Args", "Main.cs", PluginFixture.Source("Args", ""));
        _fixture.Write("Args", "Tests/ArgTests.cs", """
            using EmberHUD.Testing;

            namespace Args;

            public sealed class ArgTests
            {
                [Test]
                public void WithArg(int x) { }
            }
            """);
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Args ArgTests.WithArg - System.InvalidOperationException: a [Test] method takes no parameters and returns void", report);
    }

    [Fact]
    public void AFailingPluginDoesNotStopTheNext()
    {
        _fixture.Write("Aaa", "Main.cs", "namespace Aaa; public sealed class Other { }");
        _fixture.Write("Bbb", "Main.cs", PluginFixture.Source("Bbb", ""));
        _fixture.Write("Bbb", "Tests/OkTests.cs", """
            using EmberHUD.Testing;

            namespace Bbb;

            public sealed class OkTests
            {
                [Test]
                public void Ok() => Check.That(true);
            }
            """);
        var (code, report) = Check();
        Assert.Equal(1, code);
        Assert.Contains("FAIL Aaa load", report);
        Assert.Contains("PASS Bbb OkTests.Ok", report);
        Assert.Contains("check: 2 plugins, 1 tests, 1 failed", report);
    }

    [Fact]
    public void AMissingPluginsFolderIsAPass()
    {
        var report = new StringWriter();
        Assert.Equal(0, CheckRunner.Run(Path.Combine(_fixture.Root, "nowhere"), report));
        Assert.Contains("check: 0 plugins, 0 tests, 0 failed", report.ToString());
    }
}
