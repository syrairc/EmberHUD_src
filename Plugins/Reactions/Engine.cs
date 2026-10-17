using System;
using System.Collections.Generic;
using EmberHUD;

namespace Reactions;

public sealed class Engine
{
    const int MinCooldownMs = 50;

    readonly Dictionary<Reaction, long> _readyAt = [];
    List<Reaction>? _list;
    long _globalReadyAt;

    public Reaction? Step(Settings settings, GameState state, long nowMs, Func<Key, bool> press)
    {
        if (!(state.Status.StartsWith("live") || state.Status == "fake game") || state.Paused != false || state.Ui.AnyFullScreen || state.Player is null || nowMs < _globalReadyAt) return null;
        if (!ReferenceEquals(_list, settings.Reactions))
        {
            _list = settings.Reactions;
            _readyAt.Clear();
        }
        foreach (var reaction in settings.Reactions)
        {
            if (!reaction.Enabled || reaction.Trigger.Key == Key.None) continue;
            if (_readyAt.TryGetValue(reaction, out var readyAt) && nowMs < readyAt) continue;
            if (!Conditions.AllMet(reaction, state) || !press(reaction.Trigger.Key)) continue;
            _readyAt[reaction] = nowMs + Math.Max(reaction.CooldownMs, MinCooldownMs);
            _globalReadyAt = nowMs + Math.Max(settings.GlobalCooldownMs, MinCooldownMs);
            return reaction;
        }
        return null;
    }
}
