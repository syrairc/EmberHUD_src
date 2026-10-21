using EmberHUD.Assets;
using EmberHUD.Launcher;
using EmberHUD.Ui;
using Hexa.NET.ImGui;

namespace EmberHUD.Host.Ui;

internal sealed class GameAssetsTab(AssetRows assets, string iconsRoot, Func<GameInstall?> findGame, Action rescan)
{
    const long GameRefreshMilliseconds = 2000;

    GameInstall? _game;
    long _checkedAt = -GameRefreshMilliseconds;
    bool _running;

    string? _error;

    public IReadOnlyList<AssetRow> Update(long now)
    {
        if (now - _checkedAt >= GameRefreshMilliseconds)
        {
            _checkedAt = now;
            var found = FindGameOrNull();
            _game = Equals(found, _game) ? _game : found;
        }
        Poll();
        return assets.Rows(_game, iconsRoot);
    }

    public void Poll()
    {
        if (!_running || assets.Busy) return;
        _running = false;
        if (assets.LastSucceeded) rescan();
    }

    GameInstall? FindGameOrNull()
    {
        try
        {
            return findGame();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public bool Extract(AssetRow row)
    {
        if (_game is null || !row.CanExtract) return false;
        try
        {
            Directory.CreateDirectory(iconsRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _error = $"failed - {ex.Message}";
            return false;
        }
        _error = null;
        if (!assets.Start(row.Job, _game, iconsRoot)) return false;
        _running = true;
        return true;
    }

    public bool Draw()
    {
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted("Icons are pulled from your own Torchlight install into the GameIcons folder next to Ember.exe and never ship with EmberHUD. Sounds come later.");
        ImGui.PopTextWrapPos();
        foreach (var row in Update(Environment.TickCount64))
        {
            ImGui.PushID(row.Title);
            ImGui.BeginDisabled(!row.CanExtract);
            if (ImGui.Button(row.ButtonLabel)) Extract(row);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextUnformatted(row.Title);
            ImGui.SameLine();
            ImGui.TextUnformatted(row.Status);
            if (row.Outcome is not null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(Text.EscPct(row.Outcome));
            }
            ImGui.PopID();
        }
        if (_error is not null) ImGui.TextDisabled(Text.EscPct(_error));
        if (assets.Busy && ImGui.Button("Cancel")) assets.Cancel();
        return false;
    }
}
