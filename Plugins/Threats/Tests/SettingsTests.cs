using System;
using System.Linq;
using EmberHUD;
using EmberHUD.Testing;

namespace Threats;

public sealed class SettingsTests
{
    static readonly Rarity[] Rarities = [Rarity.Normal, Rarity.Magic, Rarity.Rare, Rarity.Boss];

    static readonly Action<Settings>[] TelegraphFlips = [s => s.TelegraphNormal = !s.TelegraphNormal, s => s.TelegraphMagic = !s.TelegraphMagic, s => s.TelegraphRare = !s.TelegraphRare, s => s.TelegraphBoss = !s.TelegraphBoss];

    static readonly Action<Settings>[] LineFlips = [s => s.LinesNormal = !s.LinesNormal, s => s.LinesMagic = !s.LinesMagic, s => s.LinesRare = !s.LinesRare, s => s.LinesBoss = !s.LinesBoss];

    [Test]
    public void AllowsFollowsTheRarityToggles()
    {
        for (var flip = 0; flip < Rarities.Length; flip++)
        {
            foreach (var telegraphs in new[] { true, false })
            {
                var s = new Settings();
                var before = Rarities.Select(r => (s.Allows(r, true), s.Allows(r, false))).ToArray();
                (telegraphs ? TelegraphFlips : LineFlips)[flip](s);
                for (var i = 0; i < Rarities.Length; i++)
                {
                    Check.Equal(before[i].Item1 != (telegraphs && i == flip), s.Allows(Rarities[i], true));
                    Check.Equal(before[i].Item2 != (!telegraphs && i == flip), s.Allows(Rarities[i], false));
                }
            }
        }
    }

    [Test]
    public void DefaultsOnlyShowRareAndBoss()
    {
        var s = new Settings();
        foreach (var telegraphs in new[] { true, false })
        {
            Check.That(!s.Allows(Rarity.Normal, telegraphs));
            Check.That(!s.Allows(Rarity.Magic, telegraphs));
            Check.That(s.Allows(Rarity.Rare, telegraphs));
            Check.That(s.Allows(Rarity.Boss, telegraphs));
        }
    }
}
