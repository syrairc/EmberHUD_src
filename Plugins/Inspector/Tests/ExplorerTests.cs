using System;
using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class ExplorerTests
{
    static (Explorer Explorer, TestUe Ue, nint Hero) Setup()
    {
        var (ue, hero, _, _) = Samples.ActorWorld();
        return (new Explorer(new Snapshots(() => 0), Samples.NoPin) { Job = new(inline: true) }, ue, hero);
    }

    [Test]
    public void TheIndexFillsOnFirstDrawAndRefreshesOnAnAreaChange()
    {
        var (explorer, ue, _) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        var before = explorer.Index.Rows.Length;
        Check.That(before > 0, "rows after the first scan");

        ue.Object("Late", ue.Class("Thing"));
        Samples.Frame(() => explorer.Draw(1));
        Check.Equal(before, explorer.Index.Rows.Length);
        Samples.Frame(() => explorer.Draw(2));
        Samples.Frame(() => explorer.Draw(2));
        Check.Equal(before + 2, explorer.Index.Rows.Length);
    }

    [Test]
    public void AFaultedScanLeavesAnEmptyIndex()
    {
        var (explorer, _, _) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        Check.That(explorer.Index.Rows.Length > 0, "the first scan landed");
        explorer.Job.Start(() => throw new InvalidOperationException("boom"));

        Samples.Frame(() => explorer.Draw(1));

        Check.Equal(0, explorer.Index.Rows.Length);
    }

    [Test]
    public void SelectingAnObjectDrawsItAndABackAndForwardTrail()
    {
        var (explorer, _, hero) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        explorer.History.Push(hero);
        explorer.SelectedClass = explorer.Index.Rows.First(r => r.Name == "Hero").ClassAddress;

        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));

        Check.Equal(hero, explorer.History.Current);
    }

    [Test]
    public void AGoneObjectKeepsItsHistoryEntryAndDrawsWithoutReading()
    {
        var (explorer, _, hero) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        explorer.History.Push(hero);

        TestMemory.WriteBytes(hero, new byte[0x30]);
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));

        Check.Equal(hero, explorer.History.Current);
        Check.That(!Ue.TryObject(hero, out _), "the object is really gone");
    }

    [Test]
    public void SearchDrawsTheMatchingInstances()
    {
        var (explorer, _, _) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));

        explorer.Search = "hero";
        Samples.Frame(() => explorer.Draw(1));

        Check.Equal(1, explorer.Index.Search("hero").Length);
    }

    [Test]
    public void ManyInstancesDrawInOneFrame()
    {
        var (explorer, ue, _) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        var cls = explorer.Index.Rows.First(r => r.Name == "Hero").ClassAddress;
        for (var i = 0; i < 200; i++) ue.Object($"Thing{i}", cls);
        Samples.Frame(() => explorer.Draw(2));
        Samples.Frame(() => explorer.Draw(2));
        explorer.SelectedClass = cls;

        Samples.Frame(() => explorer.Draw(2));

        Check.That(explorer.Index.Rows.Length >= 200, $"the scan found {explorer.Index.Rows.Length} rows");
    }

    [Test]
    public void DetachedSaysNotAttached()
    {
        TestMemory.Clear();
        var explorer = new Explorer(new Snapshots(() => 0), Samples.NoPin);

        Samples.Frame(() => explorer.Draw(1));
    }

    [Test]
    public void HistoryWalksBackAndForwardAndDropsTheForwardTailOnPush()
    {
        var history = new History();
        history.Push(1);
        history.Push(2);
        history.Push(3);
        history.Push(3);

        Check.Equal((nint)3, history.Current);
        history.Back();
        Check.Equal((nint)2, history.Current);
        Check.That(history.CanForward, "can go forward");
        history.Push(9);
        Check.That(!history.CanForward, "forward tail dropped");
        history.Back();
        Check.Equal((nint)2, history.Current);
        history.Back();
        Check.Equal((nint)1, history.Current);
        Check.That(!history.CanBack, "at the start");
        history.Back();
        Check.Equal((nint)1, history.Current);
    }

    [Test]
    public void HistoryIsCapped()
    {
        var history = new History();
        for (var i = 1; i <= 500; i++) history.Push(i);

        var steps = 0;
        while (history.CanBack)
        {
            history.Back();
            steps++;
        }
        Check.That(steps < 250, $"history kept {steps} entries");
    }

    static (Explorer Explorer, ObjectRow Row, nint Hero, Action<long> SetClock) PositionSetup()
    {
        var (ue, hero, _, _) = Samples.ActorWorld();
        long now = 0;
        var explorer = new Explorer(new Snapshots(() => now), Samples.NoPin) { Job = new(inline: true) };
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        return (explorer, explorer.Index.Rows.Single(r => r.Name == "Hero"), hero, t => now = t);
    }

    [Test]
    public void TheActorPositionBytesFollowTheSnapshot()
    {
        var (explorer, row, _, _) = PositionSetup();
        Check.Equal(new Vector3(1, 2, 3), explorer.ActorPosition(row)!.Value);
    }

    [Test]
    public void TheActorPositionGoesWhenTheRootComponentIsZeroedAndFollowsAReplacement()
    {
        var (explorer, row, hero, setClock) = PositionSetup();
        Check.Equal(new Vector3(1, 2, 3), explorer.ActorPosition(row)!.Value);

        TestMemory.Write(hero + 0x30, (nint)0);
        setClock(1000);
        Check.That(explorer.ActorPosition(row) is null, "a zeroed root has no position");

        var (_, _, otherRoot, _) = Samples.ActorWorld();
        TestMemory.Write(hero + 0x30, otherRoot);
        TestMemory.Write(otherRoot + 0x30, new Vector3(7, 8, 9));
        setClock(2000);
        Check.Equal(new Vector3(7, 8, 9), explorer.ActorPosition(row)!.Value);
    }

    [Test]
    public void AZeroLocationIsRetriedNextFrame()
    {
        var (explorer, row, hero, setClock) = PositionSetup();
        TestMemory.Write(hero + 0x30, (nint)0);
        setClock(1000);
        Check.That(explorer.ActorPosition(row) is null, "no root yet");

        var (_, _, root, _) = Samples.ActorWorld();
        TestMemory.Write(hero + 0x30, root);
        setClock(2000);
        Check.Equal(new Vector3(1, 2, 3), explorer.ActorPosition(row)!.Value);
    }

    [Test]
    public void TheBreadcrumbIsBuiltOncePerObjectAndRebuiltWithTheIndex()
    {
        var (explorer, ue, _) = Setup();
        Samples.Frame(() => explorer.Draw(1));
        Samples.Frame(() => explorer.Draw(1));
        var cls = explorer.Index.Rows.Single(r => r.Name == "Hero").ClassAddress;
        var level = ue.Object("Level", cls);
        var inner = new UObj(ue.Object("Inner", cls, level));

        var first = explorer.Crumb(inner);

        Check.Equal("Inner,Level", string.Join(",", first.Select(part => part.Name)));
        Check.That(ReferenceEquals(first, explorer.Crumb(inner)), "same object, same trail");
        Samples.Frame(() => explorer.Draw(2));
        Samples.Frame(() => explorer.Draw(2));
        Check.That(!ReferenceEquals(first, explorer.Crumb(inner)), "a new index rebuilds the trail");
        var again = explorer.Crumb(inner);
        TestMemory.Write(inner.Address + 0x20, ue.Name("Renamed"));
        Check.That(!ReferenceEquals(again, explorer.Crumb(new UObj(inner.Address))), "a reused slot with another name rebuilds the trail");
    }
}
