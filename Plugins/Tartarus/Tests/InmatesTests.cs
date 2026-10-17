using System.Collections.Generic;
using EmberHUD;
using EmberHUD.Testing;

namespace Tartarus;

public sealed class InmatesTests
{
    static readonly string[][] Titles =
    [
        ["Assault", "Arson", "Violent Offender", "War Criminal"],
        ["Poaching", "Abuse", "Calamity Bringer", "Heretic Leader"],
        ["Bone Eater", "Impalement", "Grave Robber", "Cannibal Criminal"],
        ["Fencing", "Fraud", "Robber", "Underworld Boss"],
    ];

    static readonly string[] Species = ["Mammoth", "Berserk Lion", "Alpha Wolf", "Colossal Rat"];

    static Dictionary<string, string> Names()
    {
        var names = new Dictionary<string, string>();
        for (var s = 0; s < 4; s++)
            for (var step = 1; step <= 4; step++)
                names[$"npc|name|{5027000 + s * 10 + step}"] = $"{Titles[s][step - 1]}: {Species[s]}";
        return names;
    }

    static InmateKind? Classify(int cfgId, Dictionary<string, string> names) => Inmates.Classify(cfgId, key => names.GetValueOrDefault(key));

    [Test]
    public void ClassifiesTheKnownRareNames()
    {
        var names = Names();
        Check.Equal(new InmateKind(InmateSpecies.Rat, 3), Classify(5027033, names));
        Check.Equal(new InmateKind(InmateSpecies.Wolf, 3), Classify(5027023, names));
        Check.Equal(new InmateKind(InmateSpecies.Mammoth, 3), Classify(5027003, names));
    }

    [Test]
    public void ClassifiesEveryStep()
    {
        var names = Names();
        for (var step = 1; step <= 4; step++) Check.Equal(new InmateKind(InmateSpecies.Mammoth, step), Classify(5027000 + step, names));
    }

    [Test]
    public void OtherPrefixWithTheSameNameClassifiesTheSame()
    {
        var names = Names();
        names["npc|name|5024003"] = "Grave Robber: Alpha Wolf";
        Check.Equal(new InmateKind(InmateSpecies.Wolf, 3), Classify(5024003, names));
    }

    [Test]
    public void UnknownTitleIsUnclassified()
    {
        var names = Names();
        names["npc|name|1"] = "Mystery Title: Alpha Wolf";
        Check.Equal(new InmateKind(InmateSpecies.Wolf, 0), Classify(1, names));
    }

    [Test]
    public void NonInmateNamesAreNull()
    {
        var names = Names();
        names["npc|name|2"] = "Miss Melancholy - Azure";
        names["npc|name|3"] = "Jailer of Tartarus: Viktor the Defector";
        Check.That(Classify(99, names) is null);
        Check.That(Classify(2, names) is null);
        Check.That(Classify(3, names) is null);
    }

    [Test]
    public void StepNamesAreTheGameLabels()
    {
        Check.Equal("Normal", Inmates.StepName(1));
        Check.Equal("Magic", Inmates.StepName(2));
        Check.Equal("Rare", Inmates.StepName(3));
        Check.Equal("Legendary", Inmates.StepName(4));
    }
}
