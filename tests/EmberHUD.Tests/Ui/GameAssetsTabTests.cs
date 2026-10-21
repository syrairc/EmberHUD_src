using EmberHUD.Assets;
using EmberHUD.Host.Ui;
using EmberHUD.Launcher;
using EmberHUD.Testing;

namespace EmberHUD.Tests;

public sealed class GameAssetsTabTests : IDisposable
{
    sealed class FakeJob(Func<CancellationToken, AssetResult> run) : IAssetJob
    {
        public AssetType Type => AssetType.Items;
        public string Title => "Items";
        public string Folder => "Items";
        public AssetStatus Status(string iconsRoot, IReadOnlyList<string> paks) => new(AssetState.NotExtracted, 0, 0);
        public AssetResult Run(IReadOnlyList<string> paks, string iconsRoot, byte[] key, Action<string> progress, CancellationToken cancel) => run(cancel);
    }

    static readonly GameInstall Game = new(@"C:\Game", 5975312);

    readonly TempDir _temp = new();
    readonly JobRunner _runner = new();
    int _rescans;

    public void Dispose() => _temp.Dispose();

    GameAssetsTab Tab(FakeJob job, GameInstall? game, out AssetRows rows, string? icons = null, Func<GameInstall?>? find = null)
    {
        rows = new AssetRows([job], _runner, []);
        return new GameAssetsTab(rows, icons ?? Path.Combine(_temp.Path, "GameIcons"), find ?? (() => game), () => _rescans++);
    }

    void WaitIdle() => SpinWait.SpinUntil(() => !_runner.Busy, TimeSpan.FromSeconds(10));

    [Fact]
    public void NoGame_HintsAndDisablesExtract()
    {
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), null, out _);
        var row = Assert.Single(tab.Update(0));
        Assert.Equal("set the game folder first", row.Status);
        Assert.False(row.CanExtract);
        Assert.False(tab.Extract(row));
        Assert.False(_runner.Busy);
    }

    [Fact]
    public void Status_NotExtractedIsNone_AndExtractCreatesTheIconsFolder()
    {
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), Game, out _);
        var row = Assert.Single(tab.Update(0));
        Assert.Equal("none", row.Status);
        Assert.Equal("Extract", row.ButtonLabel);
        Assert.True(tab.Extract(row));
        WaitIdle();
        Assert.True(Directory.Exists(Path.Combine(_temp.Path, "GameIcons")));
    }

    [Fact]
    public void ASecondExtractWhileBusyDoesNothing()
    {
        var release = new ManualResetEventSlim();
        var starts = 0;
        var tab = Tab(new FakeJob(cancel =>
        {
            Interlocked.Increment(ref starts);
            release.Wait(TimeSpan.FromSeconds(10), cancel);
            return new AssetResult(1, 0, 0);
        }), Game, out _);
        var row = tab.Update(0)[0];
        Assert.True(tab.Extract(row));
        Assert.False(tab.Extract(row));
        Assert.False(tab.Update(1)[0].CanExtract);
        release.Set();
        WaitIdle();
        Assert.Equal(1, starts);
    }

    [Fact]
    public void FinishingARun_RescansExactlyOnce()
    {
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), Game, out _);
        Assert.True(tab.Extract(tab.Update(0)[0]));
        WaitIdle();
        tab.Update(1);
        tab.Update(2);
        tab.Update(3);
        Assert.Equal(1, _rescans);
    }

    [Fact]
    public void AFailedRun_DoesNotRescan_AndShowsTheMessage()
    {
        var tab = Tab(new FakeJob(_ => throw new InvalidDataException("bad key")), Game, out _);
        Assert.True(tab.Extract(tab.Update(0)[0]));
        WaitIdle();
        var row = tab.Update(1)[0];
        tab.Update(2);
        Assert.Equal(0, _rescans);
        Assert.Equal("failed - bad key", row.Outcome);
    }

    [Fact]
    public void ARunFinishingWhileTheTabIsNeverDrawn_RescansOnceFromPoll()
    {
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), Game, out _);
        Assert.True(tab.Extract(tab.Update(0)[0]));
        WaitIdle();
        tab.Poll();
        tab.Poll();
        Assert.Equal(1, _rescans);
    }

    [Fact]
    public void AFailedRunPolled_DoesNotRescan()
    {
        var tab = Tab(new FakeJob(_ => throw new InvalidDataException("bad")), Game, out _);
        Assert.True(tab.Extract(tab.Update(0)[0]));
        WaitIdle();
        tab.Poll();
        Assert.Equal(0, _rescans);
    }

    [Fact]
    public void AThrowingGameLookup_IsTheNoGameHint()
    {
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), null, out _, find: () => throw new ArgumentException("bad path"));
        var row = Assert.Single(tab.Update(0));
        Assert.Equal("set the game folder first", row.Status);
    }

    [Fact]
    public void AnUnusableIconsFolder_MakesExtractReturnFalse()
    {
        var blocker = Path.Combine(_temp.Path, "file");
        File.WriteAllText(blocker, "x");
        var tab = Tab(new FakeJob(_ => new AssetResult(1, 0, 0)), Game, out _, icons: Path.Combine(blocker, "GameIcons"));
        Assert.False(tab.Extract(tab.Update(0)[0]));
        Assert.False(_runner.Busy);
    }
}
