namespace EmberHUD.Host.Plugins;

internal sealed class DrawStats(long intervalMs = 10000, int warnVertices = 50000)
{
    readonly Dictionary<string, (long Total, int Max, int Frames)> _plugins = [];
    long? _since;

    public void Add(string plugin, int vertices)
    {
        _plugins.TryGetValue(plugin, out var seen);
        _plugins[plugin] = (seen.Total + vertices, Math.Max(seen.Max, vertices), seen.Frames + 1);
    }

    public string? Report(long nowMs, int projectiles)
    {
        _since ??= nowMs;
        if (nowMs - _since < intervalMs) return null;
        _since = nowMs;
        var heavy = _plugins.Where(plugin => plugin.Value.Max >= warnVertices).OrderByDescending(plugin => plugin.Value.Max).ToList();
        _plugins.Clear();
        if (heavy.Count == 0) return null;
        return $"heavy draw - {string.Join(", ", heavy.Select(plugin => $"{plugin.Key} max {plugin.Value.Max} avg {plugin.Value.Total / plugin.Value.Frames}"))} verts/frame, {projectiles} projectiles";
    }
}
