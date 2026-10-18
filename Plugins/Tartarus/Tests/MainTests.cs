using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class MainTests
{
    static readonly TartarusCell[] Cells = [new(0, true, 0, TartarusBeast.Unknown, false, false), new(5, true, 2, TartarusBeast.Lion, true, false)];

    static TartarusState State(TartarusChoice? choice) => new(1, 16, 5, 20, Cells, choice, [], 0);

    static TartarusChoice Doors() => new(1, TartarusChoiceKind.Doors, [new(1, TartarusOptionKind.Door, 5, 4), new(2, TartarusOptionKind.Door, 9, 8)], true);

    static Main Run(TartarusChoice? choice, Settings? settings = null)
    {
        TestGame.Set(new GameState { Tartarus = State(choice) });
        var plugin = new Main { Logger = { DataDirectory = Path.Combine(Path.GetTempPath(), "tartarus-" + Guid.NewGuid().ToString("N")) } };
        if (settings is not null) typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [settings]);
        plugin.Tick();
        return plugin;
    }

    static TartarusState RealBoard()
    {
        var cells = Enumerable.Range(0, 8).Select(index => index switch
        {
            1 => new TartarusCell(1, true, 3, TartarusBeast.Lion, false, false),
            2 => new TartarusCell(2, true, 3, TartarusBeast.Wolf, true, false),
            _ => new TartarusCell(index, false, -1, TartarusBeast.Unknown, false, false),
        }).ToArray();
        var choice = new TartarusChoice(1, TartarusChoiceKind.Operation, [new(1, TartarusOptionKind.Consume, 2, 0), new(2, TartarusOptionKind.End, -1, 0)], true);
        return new TartarusState(1, 8, 5, 20, cells, choice, [], 0);
    }

    [Test]
    public void TickThinksThenAdvisesARealBoardWithoutBlocking()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tartarus-" + Guid.NewGuid().ToString("N"));
        var plugin = new Main { Logger = { DataDirectory = directory } };
        try
        {
            TestGame.Set(new GameState { Tartarus = RealBoard() });
            typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [new Settings { Playouts = 30 }]);
            plugin.Tick();
            Check.That(plugin.Advice is not null);
            Check.That(SpinWait.SpinUntil(() =>
            {
                plugin.Tick();
                return plugin.Advice!.Summary.EndsWith("best");
            }, 30000), plugin.Advice!.Summary);
            TestFrame.Run(plugin.Render);
        }
        finally
        {
            plugin.Dispose();
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    [Test]
    public void GarbageStateGivesNoAdviceAndNoException()
    {
        var plugin = Run(null);
        foreach (var state in new TartarusState?[] { RealBoard() with { Instruments = null! }, RealBoard() with { Choice = RealBoard().Choice! with { Options = null! } }, RealBoard() with { Size = -1 }, null })
        {
            TestGame.Set(new GameState { Tartarus = state });
            plugin.Tick();
            TestFrame.Run(plugin.Render);
            Check.That(plugin.Advice is null || plugin.Advice.Options.All(option => !option.Glow));
        }
        plugin.Dispose();
    }

    [Test]
    public void TickAsksForTartarus()
    {
        TestGame.Set(GameState.Empty);
        Check.That(TestGame.TartarusAskedDuring(new Main().Tick));
    }

    [Test]
    public void NothingToDrawWithoutAChoice()
    {
        var plugin = Run(Doors());
        Check.That(plugin.Advice is not null, "advised");
        TestGame.Set(new GameState { Tartarus = State(null) });
        plugin.Tick();
        Check.That(plugin.Advice is null, "cleared");
    }

    [Test]
    public void EventChoiceIsNotAdvised()
    {
        var plugin = Run(new TartarusChoice(1, TartarusChoiceKind.Event, [new(1, TartarusOptionKind.Confirm, -1, 213)], true));
        Check.That(plugin.Advice is null);
        TestFrame.Run(plugin.Render);
    }

    [Test]
    public void NamesNumberCellsFromOne()
    {
        var state = State(null);
        Check.Equal("Cell 6 (4 steps) Lion T2", Names.Option(new(1, TartarusOptionKind.Door, 5, 4), state));
        Check.Equal("Cell 10 (8 steps)", Names.Option(new(2, TartarusOptionKind.Door, 9, 8), state));
        Check.Equal("Consume cell 6", Names.Option(new(3, TartarusOptionKind.Consume, 5, 0), state));
        Check.Equal("Replace", Names.Option(new(4, TartarusOptionKind.Replace, -1, 0), state));
        Check.Equal("Stop chain", Names.Option(new(5, TartarusOptionKind.StopChain, -1, 0), state));
    }

    [Test]
    public void NamesFallBackForUnknownIds()
    {
        var state = State(null);
        Check.Equal("Barbed Whip", Names.Option(new(1, TartarusOptionKind.Instrument, -1, 106), state));
        Check.Equal("Instrument 999", Names.Option(new(1, TartarusOptionKind.Instrument, -1, 999), state));
        Check.Equal("Brand of Original Sin", Names.Option(new(1, TartarusOptionKind.Reward, -1, 4), state));
        Check.Equal("Reward 9", Names.Option(new(1, TartarusOptionKind.Reward, -1, 9), state));
    }

    [Test]
    public void RendersDoorsAndInstrumentChoices()
    {
        TestFrame.Run(Run(Doors()).Render);
        var instruments = new TartarusChoice(2, TartarusChoiceKind.Instrument, [new(1, TartarusOptionKind.Instrument, -1, 106), new(2, TartarusOptionKind.Instrument, -1, 131)], true);
        Check.That(TestFrame.Vertices(Run(instruments).Render) > 0, "instrument panel drew");
        Check.Equal(0, TestFrame.Vertices(Run(instruments, new Settings { ShowPanel = false }).Render));
    }

    [Test]
    public void TickAlsoRunsTheInmateLogger()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tartarus-" + Guid.NewGuid().ToString("N"));
        var now = 1.0;
        var plugin = new Main { Logger = { DataDirectory = directory, Seconds = () => now } };
        try
        {
            TestGame.UseLoc(key => key == "npc|name|5027023" ? "Robber: Alpha Wolf" : null, null);
            var wolf = new Entity(11, 5027023, Vector3.Zero, EntityKind.Monster, Rarity.Rare) { Life = 0, MaxLife = 90 };
            TestGame.Set(new GameState { AreaId = 7, Entities = [wolf] });
            plugin.Tick();
            TestGame.Set(new GameState { AreaId = 7 });
            now = 3;
            plugin.Tick();
            Check.Equal(1, plugin.Logger.Store.Grid[(int)InmateSpecies.Wolf, 2]);
        }
        finally
        {
            TestGame.UseLoc(null, null);
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    [Test]
    public void DisposeSavesTheInmateStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tartarus-" + Guid.NewGuid().ToString("N"));
        var plugin = new Main { Logger = { DataDirectory = directory } };
        try
        {
            plugin.Logger.Store.Add(new(5027023, new(InmateSpecies.Wolf, 2), SightingEnd.Killed, 0, 90, 5, 3), "Prison", null, DateTime.Now);
            plugin.Dispose();
            Check.Equal(1, InmateStore.Load(directory).Grid[(int)InmateSpecies.Wolf, 1]);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    [Test]
    public void SettingsPageDrawsEveryTab()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tartarus-" + Guid.NewGuid().ToString("N"));
        var plugin = new Main { Logger = { DataDirectory = directory } };
        try
        {
            TestFrame.Run(plugin.DrawSettings);
            plugin.Logger.Store.Kills.Add(new(DateTime.Now, "Odd", 5027099, null, 0, "killed", 0, 10, 1, 1, 12));
            plugin.Logger.Store.AddRun(new(DateTime.Now, null, null, 7, [new(1, 2, 3)]));
            plugin.Logger.Minigame.Add(new(DateTime.Now, 1, 2, 3, 4, 2, 1, "door", 0));
            TestGame.Set(GameState.Empty);
            TestFrame.Run(plugin.DrawSettings);        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    [Test]
    public void SettingsFields()
    {
        string[] expected = ["ShowPanel", "Lock", "MoveKey", "PanelX", "PanelY", "HighlightCards", "ShowAssumed", "Playouts", "CloseMargin", "ChromaticCap", "EaterKeepsBeast", "SecondMealFromTier", "ReplaceIsExecute", "EventChance"];
        var actual = Array.ConvertAll(typeof(Settings).GetFields(), f => f.Name);
        Array.Sort(expected);
        Array.Sort(actual);
        Check.Equal(string.Join(",", expected), string.Join(",", actual));
    }
}
