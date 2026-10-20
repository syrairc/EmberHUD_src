namespace EmberHUD.Api.Tests;

public class DialogsTests : IDisposable
{
    public DialogsTests() => Dialogs.Reset();

    public void Dispose() => Dialogs.Reset();

    static string[] Taken(DialogRequest request)
    {
        Assert.True(request.TryTake(out var paths));
        return paths;
    }

    [Fact]
    public void StartingADialogDoesNotWaitForTheUser()
    {
        using var release = new ManualResetEventSlim();
        Dialogs.Runner = _ => { release.Wait(); return ["C:\\a.txt"]; };

        var request = Dialogs.OpenFile("Pick");

        Assert.False(request.Done);
        Assert.False(request.TryTake(out _));
        Assert.True(Dialogs.Busy);
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));
        Assert.Equal(["C:\\a.txt"], Taken(request));
        Assert.False(Dialogs.Busy);
    }

    [Fact]
    public void AResultIsTakenOnce()
    {
        Dialogs.Runner = _ => ["C:\\a.txt"];
        var request = Dialogs.OpenFile("Pick");
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));

        Assert.Equal("C:\\a.txt", request.Path);
        Assert.Single(Taken(request));
        Assert.False(request.TryTake(out _));
    }

    [Fact]
    public void CancellingGivesNoPaths()
    {
        Dialogs.Runner = _ => [];
        var request = Dialogs.PickFolder("Pick");
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));

        Assert.Empty(Taken(request));
        Assert.Null(request.Path);
    }

    [Fact]
    public void AThrowingDialogCountsAsCancelled()
    {
        Dialogs.Runner = _ => throw new InvalidOperationException("com failed");
        var request = Dialogs.OpenFile("Pick");
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));

        Assert.Empty(Taken(request));
        Assert.False(Dialogs.Busy);
    }

    [Fact]
    public void ASecondDialogWhileBusyIsCancelledAtOnce()
    {
        using var release = new ManualResetEventSlim();
        Dialogs.Runner = _ => { release.Wait(); return ["C:\\a.txt"]; };
        var first = Dialogs.OpenFile("One");

        var second = Dialogs.SaveFile("Two");

        Assert.True(second.Done);
        Assert.Empty(Taken(second));
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => first.Done, 5000));
    }

    [Fact]
    public void TheRunnerGetsTheKindAndOptions()
    {
        DialogSpec? seen = null;
        Dialogs.Runner = spec => { seen = spec; return []; };

        var request = Dialogs.OpenFiles("Pick", "C:\\start", new DialogFilter("Price files", "*.gz;*.json"));
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));

        Assert.Equal(DialogKind.OpenFiles, seen!.Kind);
        Assert.Equal("Pick", seen.Title);
        Assert.Equal("C:\\start", seen.StartFolder);
        Assert.Equal("*.gz;*.json", Assert.Single(seen.Filters).Pattern);
    }

    [Fact]
    public void ADialogRunsOffTheCallingThread()
    {
        int? runner = null;
        Dialogs.Runner = _ => { runner = Environment.CurrentManagedThreadId; return []; };

        var request = Dialogs.PickFolder("Pick");
        Assert.True(SpinWait.SpinUntil(() => request.Done, 5000));

        Assert.NotEqual(Environment.CurrentManagedThreadId, runner);
    }
}

public class GameColorsTests
{
    [Fact]
    public void RarityColorsFollowTheGameTiers()
    {
        Assert.Equal(GameColors.LegendaryName, GameColors.Rarity(100));
        Assert.Equal(GameColors.RareName, GameColors.Rarity(2));
        Assert.Equal(GameColors.NormalName, GameColors.Rarity(1));
        Assert.Equal(GameColors.NormalName, GameColors.Rarity(7));
        Assert.NotEqual(GameColors.PurpleName, GameColors.PinkName);
    }
}
