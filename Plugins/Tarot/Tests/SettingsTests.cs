using System.Text.Json;
using System.Text.Json.Serialization;
using EmberHUD;
using EmberHUD.Testing;

namespace Tarot;

public sealed class SettingsTests
{
    [Test]
    public void SettingsClampRollouts()
    {
        Check.Equal(20, new Settings { Rollouts = -5 }.ToOptions().Rollouts);
        Check.Equal(1000, new Settings { Rollouts = 99999 }.ToOptions().Rollouts);
        Check.Equal(150, new Settings().ToOptions().Rollouts);
    }

    [Test]
    public void ObjectivePicksTheWeights()
    {
        Check.That(new Settings().ToOptions().Weights.Values == Weights.Rarity.Values, "rarity default");
        Check.That(new Settings { Objective = Objective.Good }.ToOptions().Weights.Values == Weights.Good.Values, "good");
    }

    [Test]
    public void FlagsPassThrough()
    {
        var options = new Settings { DeathLosesLoot = false }.ToOptions();
        Check.That(!options.DeathLosesLoot, "off");
        options = new Settings().ToOptions();
        Check.That(options.DeathLosesLoot, "on");
    }

    [Test]
    public void SameSettingsGiveEqualOptions() => Check.That(new Settings().ToOptions() == new Settings().ToOptions(), "equal");

    [Test]
    public void NewKeysRoundTripThroughJson()
    {
        var options = new JsonSerializerOptions { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
        var json = JsonSerializer.Serialize(new Settings { Objective = Objective.Good, Rollouts = 400, ShowStats = false }, options);
        foreach (var key in new[] { "UseSolver", "Objective", "Rollouts", "DeathLosesLoot", "ShowStats" }) Check.That(json.Contains($"\"{key}\""), key);
        var back = JsonSerializer.Deserialize<Settings>("""{ "Objective": "Good", "Rollouts": 400, "ShowStats": false }""", options)!;
        Check.That(back.Objective == Objective.Good && back.Rollouts == 400 && !back.ShowStats && back.UseSolver, "loaded with defaults for the rest");
    }

    [Test]
    public void TheSettingsPageDraws() => Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings drew");
}
