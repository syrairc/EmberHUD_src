using EmberHUD.Launcher;

namespace EmberHUD.Assets.Tests;

public class AssetRowsTests
{
    sealed class FakeJob(string title, AssetStatus status, Func<CancellationToken, AssetResult>? run = null) : IAssetJob
    {
        public AssetType Type => AssetType.Items;
        public string Title => title;
        public string Folder => title;
        public AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks) => Current ?? status;
        public AssetStatus? Current { get; set; }
        public AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel)
        {
            progress("working");
            return run?.Invoke(cancel) ?? new AssetResult(2, 1, 0);
        }
    }

    static readonly GameInstall Game = new(@"C:\Game", 5975312);

    static AssetStatus Status(AssetState state) => new(state, 3, 2621440);

    static AssetRows Rows(JobRunner runner, params IAssetJob[] jobs) => new(jobs, runner, []);

    static void WaitIdle(JobRunner runner) => SpinWait.SpinUntil(() => !runner.Busy, TimeSpan.FromSeconds(10));

    [Fact]
    public void OneRowPerJob_WithTheStatusTexts()
    {
        var rows = Rows(new JobRunner(),
            new FakeJob("A", Status(AssetState.NotExtracted)),
            new FakeJob("B", Status(AssetState.Current)),
            new FakeJob("C", Status(AssetState.OutOfDate)),
            new FakeJob("D", Status(AssetState.Unreadable))).Rows(Game, "x");
        Assert.Equal(["A", "B", "C", "D"], rows.Select(row => row.Title));
        Assert.Equal(["none", "3 icons, 2.5 MB, game build 5975312", "3 icons - out of date, extract again", "unreadable - extract again"], rows.Select(row => row.Status));
        Assert.Equal(["Extract", "Re-extract", "Re-extract", "Re-extract"], rows.Select(row => row.ButtonLabel));
        Assert.All(rows, row => Assert.True(row.CanExtract));
    }

    [Fact]
    public void UnknownBuild_SaysUnknown()
    {
        var row = Assert.Single(Rows(new JobRunner(), new FakeJob("B", Status(AssetState.Current))).Rows(Game with { Build = null }, "x"));
        Assert.Equal("3 icons, 2.5 MB, game build unknown", row.Status);
    }

    [Fact]
    public void NoGameFolder_HintsAndDisablesEveryRow()
    {
        var rows = Rows(new JobRunner(), new FakeJob("A", Status(AssetState.Current)), new FakeJob("B", Status(AssetState.NotExtracted))).Rows(null, "x");
        Assert.All(rows, row =>
        {
            Assert.Equal("set the game folder first", row.Status);
            Assert.False(row.CanExtract);
        });
    }

    [Fact]
    public void Busy_DisablesAllRows_ShowsProgressOnTheRunningOne_AndCancelStops()
    {
        var runner = new JobRunner();
        var started = new ManualResetEventSlim();
        var a = new FakeJob("A", Status(AssetState.NotExtracted), cancel =>
        {
            started.Set();
            cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            cancel.ThrowIfCancellationRequested();
            return new AssetResult(0, 0, 0);
        });
        var b = new FakeJob("B", Status(AssetState.Current));
        var assets = Rows(runner, a, b);
        Assert.False(assets.Busy);
        Assert.True(assets.Start(a, Game, "x"));
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(assets.Busy);
        var rows = assets.Rows(Game, "x");
        Assert.All(rows, row => Assert.False(row.CanExtract));
        Assert.Equal("working", rows[0].Status);
        Assert.Equal("3 icons, 2.5 MB, game build 5975312", rows[1].Status);
        Assert.False(assets.Start(b, Game, "x"));
        assets.Cancel();
        WaitIdle(runner);
        Assert.False(assets.Busy);
        rows = assets.Rows(Game, "x");
        Assert.All(rows, row => Assert.True(row.CanExtract));
        Assert.Equal("failed - cancelled", rows[0].Outcome);
        Assert.Null(rows[1].Outcome);
    }

    [Fact]
    public void AfterARun_TheRunJobShowsTheResult_AndRefreshesItsStatus()
    {
        var runner = new JobRunner();
        var a = new FakeJob("A", Status(AssetState.Current));
        var assets = Rows(runner, a, new FakeJob("B", Status(AssetState.Current)));
        assets.Rows(Game, "x");
        assets.Start(a, Game, "x");
        WaitIdle(runner);
        var rows = assets.Rows(Game, "x");
        Assert.Equal("done - 2 icons, 1 missing, 0 failed", rows[0].Outcome);
        Assert.Null(rows[1].Outcome);
    }

    [Fact]
    public void AJobThatFinishesBetweenTwoRowsCalls_StillRefreshesItsStatus()
    {
        var runner = new JobRunner();
        var a = new FakeJob("A", Status(AssetState.NotExtracted));
        var assets = Rows(runner, a);
        Assert.Equal("none", assets.Rows(Game, "x")[0].Status);
        a.Current = Status(AssetState.Current);
        assets.Start(a, Game, "x");
        WaitIdle(runner);
        Assert.Equal("3 icons, 2.5 MB, game build 5975312", assets.Rows(Game, "x")[0].Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaksThatCannotBeListed_CountAsNoGame(bool io)
    {
        var job = new FakeJob("A", Status(AssetState.Current));
        var assets = new AssetRows([job], new JobRunner(), [], _ => throw (io ? new IOException() : new UnauthorizedAccessException()));
        var row = Assert.Single(assets.Rows(Game, "x"));
        Assert.Equal("set the game folder first", row.Status);
        Assert.False(row.CanExtract);
        Assert.False(assets.Start(job, Game, "x"));
        Assert.False(assets.Busy);
    }
}
