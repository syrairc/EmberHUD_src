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
        var plugin = new Main();
        if (settings is not null) typeof(Plugin<Settings>).GetProperty("Settings")!.GetSetMethod(true)!.Invoke(plugin, [settings]);
        plugin.Tick();
        return plugin;
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
        Check.Equal("Instrument 104", Names.Option(new(1, TartarusOptionKind.Instrument, -1, 104), state));
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
}
