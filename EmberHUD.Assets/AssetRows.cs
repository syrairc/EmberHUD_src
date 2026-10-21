using System.Globalization;
using EmberHUD.Launcher;

namespace EmberHUD.Assets;

internal sealed record AssetRow(IAssetJob Job, string Title, string Status, string ButtonLabel, bool CanExtract, string? Outcome);

internal sealed class AssetRows(IReadOnlyList<IAssetJob> jobs, JobRunner runner, byte[] key, Func<GameInstall, IReadOnlyList<string>>? pakList = null)
{
    const string NoGame = "set the game folder first";
    AssetStatus[] _statuses = [];
    GameInstall? _game;
    string _root = "";
    bool _readable;
    bool _stale = true;
    bool _wasBusy;
    IAssetJob? _lastRun;

    public bool Busy => runner.Busy;

    public bool LastSucceeded => runner.Failure is null && runner.Last is not null;

    public void Invalidate() => _stale = true;

    public void Cancel() => runner.Cancel();

    public bool Start(IAssetJob job, GameInstall game, string iconsRoot)
    {
        if (Paks(game) is not { } paks || !runner.TryStart(job, paks, iconsRoot, key)) return false;
        _lastRun = job;
        _stale = true;
        return true;
    }

    public IReadOnlyList<AssetRow> Rows(GameInstall? game, string iconsRoot)
    {
        var busy = runner.Busy;
        if (_stale || _wasBusy && !busy || !ReferenceEquals(game, _game) || iconsRoot != _root)
        {
            _game = game;
            _root = iconsRoot;
            _stale = false;
            var paks = game is null ? null : Paks(game);
            _readable = paks is not null;
            _statuses = paks is null ? [] : [.. jobs.Select(job => job.Status(iconsRoot, paks))];
        }
        _wasBusy = busy;
        return [.. jobs.Select((job, i) => Row(job, i, busy))];
    }

    IReadOnlyList<string>? Paks(GameInstall game)
    {
        try
        {
            return pakList is null ? game.Paks() : pakList(game);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    AssetRow Row(IAssetJob job, int index, bool busy)
    {
        if (!_readable) return new AssetRow(job, job.Title, NoGame, "Extract", false, null);
        var status = _statuses[index];
        var label = status.State == AssetState.NotExtracted ? "Extract" : "Re-extract";
        if (busy) return new AssetRow(job, job.Title, job == _lastRun ? runner.Progress : Describe(status), label, false, null);
        return new AssetRow(job, job.Title, Describe(status), label, true, job == _lastRun ? Outcome() : null);
    }

    string? Outcome() => runner.Failure is { } failure ? $"failed - {failure}" : runner.Last is { } last ? $"done - {last.Files} icons, {last.Missing} missing, {last.Failed} failed" : null;

    string Describe(AssetStatus status) => status.State switch
    {
        AssetState.NotExtracted => "none",
        AssetState.Current => $"{status.Files} icons, {(status.Bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB, game build {_game!.Build?.ToString() ?? "unknown"}",
        AssetState.OutOfDate => $"{status.Files} icons - out of date, extract again",
        _ => "unreadable - extract again",
    };
}
