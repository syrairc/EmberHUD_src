using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using EmberHUD;
using EmberHUD.Testing;

namespace MouseLab;

public sealed class MainTests
{
    const int Columns = 7;
    const int Rows = 5;

    static readonly Hotkey[] TakenByOthers = [new(Key.F2), new(Key.F3), new(Key.F4), new(Key.F5), new(Key.F6), new(Key.F7), new(Key.F8), new(Key.F9), new(Key.F10), new(Key.F11), new(Key.F12, KeyModifiers.Ctrl)];

    static BagCell Cell(int slot, float originX = 0) => new(slot, new Vector2(originX + slot % Columns * 50, slot / Columns * 50), new Vector2(originX + slot % Columns * 50 + 40, slot / Columns * 50 + 40));

    static GameState StashOpen() => GameState.Empty with
    {
        ChatOpen = false,
        ItemMenu = new ItemMenu(null, new ScreenRect(new Vector2(500, 500), new Vector2(560, 530)), new ScreenRect(new Vector2(500, 540), new Vector2(560, 570)), null, null, null, null),
        Stash = new StashView(0, Vector2.Zero, new Vector2(400, 300), Enumerable.Range(0, Columns * Rows).Select(slot => Cell(slot)).ToArray()),
        Bag = new BagView(InventoryBag.Equipment, Vector2.Zero, new Vector2(400, 300), [Cell(3, 1000), Cell(4, 1000)]),
    };

    static string NewDirectory() => Path.Combine(Path.GetTempPath(), "mouselab-test-" + Guid.NewGuid().ToString("N"));

    static Main Ready(out List<SentInput> sent, string directory, Func<int, CancellationToken, Task>? delay = null)
    {
        sent = TestSend.Record();
        TestSend.Instant();
        TestGame.Set(StashOpen());
        var plugin = new Main { OutputDirectory = directory, Delay = delay ?? ((_, _) => Task.CompletedTask) };
        plugin.Initialize();
        return plugin;
    }

    static Task UntilCancelled(int ms, CancellationToken cancel) => ms == 150 ? Task.Delay(Timeout.Infinite, cancel) : Task.CompletedTask;

    static void Finish(Main plugin) => Check.That(plugin.Finished.Wait(TimeSpan.FromSeconds(30)), "the run finished");

    static void WaitForTheFirstMove(List<SentInput> sent)
    {
        var give = Environment.TickCount64 + 10000;
        while (Environment.TickCount64 < give)
        {
            lock (sent) if (sent.Count > 1) return;
            Thread.Sleep(1);
        }
        throw new InvalidOperationException("no move was sent");
    }

    static void With(Action<string> body)
    {
        var directory = NewDirectory();
        try
        {
            body(directory);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void RegistersRunAndRunAllHotkeys()
    {
        var plugin = new Main();
        plugin.Initialize();
        Check.That(plugin.Hotkeys.Select(binding => binding.Id).SequenceEqual(["Run", "RunAll"]), string.Join(",", plugin.Hotkeys.Select(binding => binding.Id)));
        Check.That(plugin.Hotkeys.All(binding => binding.Default.Key != Key.None), "both have a default");
        Check.That(plugin.Hotkeys.Select(binding => binding.Default).Distinct().Count() == 2, "different keys");
        foreach (var binding in plugin.Hotkeys) Check.That(!TakenByOthers.Contains(binding.Default), $"{binding.Id} {binding.Default} is taken by the host or another plugin");
    }

    [Test]
    public void RunIsRefusedWithAReasonWhenNoStashIsOpen()
    {
        With(directory =>
        {
            var plugin = Ready(out var sent, directory);
            TestGame.Set(GameState.Empty with { ChatOpen = false });
            plugin.Toggle(false);
            Check.That(!plugin.Running, "nothing started");
            Check.That(plugin.Note is { } note && note.Contains("stash", StringComparison.OrdinalIgnoreCase), plugin.Note);
            Check.Equal(0, sent.Count);
            Check.That(!Directory.Exists(directory), "nothing written");
        });
    }

    [Test]
    public void RunScoresTheSuiteAndWritesTheFiles()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            plugin.Toggle(false);
            Finish(plugin);
            var result = plugin.Results.Single();
            Check.Equal("Normal", result.Preset);
            Check.That(result.Refused is null && !result.Cancelled, "ran clean");
            Check.That(result.Moves.Count > 10, $"{result.Moves.Count} moves");
            Check.Equal(2, Directory.GetFiles(directory).Length, "json and csv");
        });
    }

    [Test]
    public void ARefusedRunWithEarlierMovesIsStillWritten()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            TestGame.Set(StashOpen() with { ItemMenu = null });
            plugin.Toggle(false);
            Finish(plugin);
            var result = plugin.Results.Single();
            Check.That(result.Refused is not null && result.Moves.Count > 0, "refused after some moves");
            Check.Equal(2, Directory.GetFiles(directory).Length, "json and csv");
            Check.That(File.ReadAllText(Directory.GetFiles(directory, "*.csv").Single()).Contains("refused"), "csv status");
        });
    }

    [Test]
    public void ARefusedRunWithNoMovesIsNotWritten()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            TestGame.Set(StashOpen() with { ItemMenu = null, Bag = null, Stash = new StashView(0, Vector2.Zero, new Vector2(400, 300), [Cell(0)]) });
            plugin.Toggle(false);
            Finish(plugin);
            Check.That(plugin.Results.Single().Refused is not null && plugin.Results.Single().Moves.Count == 0, "refused with nothing moved");
            Check.That(!Directory.Exists(directory), "nothing written");
        });
    }

    [Test]
    public void RunAllRunsTheFiveNamedPresetsInOrder()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            plugin.Toggle(true);
            Finish(plugin);
            Check.That(plugin.Results.Select(result => result.Preset).SequenceEqual(["VerySlow", "Slow", "Normal", "Fast", "VeryFast"]), string.Join(",", plugin.Results.Select(result => result.Preset)));
        });
    }

    [Test]
    public void SecondPressCancelsARunningRun()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory, UntilCancelled);
            plugin.Toggle(false);
            Check.That(plugin.Running, "running");
            plugin.Toggle(false);
            Finish(plugin);
            Check.That(!plugin.Running, "stopped");
            Check.That(plugin.Results.Single().Cancelled, "cancelled result kept");
            Check.Equal(2, Directory.GetFiles(directory).Length, "partial run still written");
        });
    }

    [Test]
    public void DisablingWhileIdleReleasesNothing()
    {
        With(directory =>
        {
            var plugin = Ready(out var sent, directory);
            Send.Down(Key.D1);
            plugin.OnDisable();
            plugin.Dispose();
            lock (sent) Check.That(!sent.Contains(new SentInput(Key.D1, null, false, 0, 0)), "another plugin's held key left alone");
        });
    }

    [Test]
    public void ASaveFailureOfAnyKindBecomesANote()
    {
        var plugin = new Main { OutputDirectory = "bad\0folder", Delay = (_, _) => Task.CompletedTask };
        plugin.Initialize();
        TestSend.Record();
        TestSend.Instant();
        TestGame.Set(StashOpen());
        plugin.Toggle(false);
        Finish(plugin);
        Check.That(plugin.Note is { } note && note.StartsWith("Not saved"), plugin.Note);
    }

    [Test]
    public void DisableCancelsAndReleases()
    {
        With(directory =>
        {
            var plugin = Ready(out var sent, directory, UntilCancelled);
            Send.Down(Key.D1);
            plugin.Toggle(false);
            WaitForTheFirstMove(sent);
            plugin.OnDisable();
            Finish(plugin);
            Check.That(plugin.Results.Single().Cancelled, "cancelled");
            lock (sent) Check.That(sent.Contains(new SentInput(Key.D1, null, false, 0, 0)), "held key released");
        });
    }

    [Test]
    public void PanelDrawsAfterARun()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            Check.Equal(0, TestFrame.Vertices(plugin.Render), "nothing to show before a run");
            plugin.Toggle(true);
            Finish(plugin);
            Check.That(TestFrame.Vertices(plugin.Render) > 0, "panel draws");
            plugin.Settings.ShowTrail = false;
            Check.That(TestFrame.Vertices(plugin.Render) > 0, "panel draws without the trail");
        });
    }

    [Test]
    public void PanelShowsTheRefusalNote()
    {
        With(directory =>
        {
            var plugin = Ready(out _, directory);
            TestGame.Set(GameState.Empty with { ChatOpen = false });
            plugin.Toggle(false);
            Check.That(TestFrame.Vertices(plugin.Render) > 0, "note drawn");
        });
    }

    [Test]
    public void SettingsPageDraws()
    {
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings page draws");
    }

    [Test]
    public void TrailSegmentsDropRepeatedPointsAndCarrySpeed()
    {
        var trail = new PathSample[] { new(0, 0, 0), new(4, 0, 0), new(8, 30, 40), new(12, 30, 40), new(20, 60, 80) };
        var segments = Panel.Segments(trail);
        Check.Equal(2, segments.Length);
        Check.That(segments[0].From == new Vector2(0, 0) && segments[0].To == new Vector2(30, 40), "first segment");
        Check.That(Math.Abs(segments[0].Speed - 50.0 / 8) < 1e-9, $"speed {segments[0].Speed}");
    }

    [Test]
    public void ThePresetSettingMapsToAHumanizerPreset()
    {
        Check.Equal(HumanizerPreset.Fast, Main.Humanizer(LabPreset.Fast));
        Check.Equal(HumanizerPreset.VerySlow, Main.Humanizer(LabPreset.VerySlow));
    }
}
