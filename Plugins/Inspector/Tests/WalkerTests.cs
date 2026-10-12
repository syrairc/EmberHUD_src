using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Aember;
using Aember.Testing;

namespace Inspector;

public sealed class WalkerTests
{
    sealed class Indexed
    {
        public int this[int index] => index;
        public int Value = 1;
        public static int Shared = 2;
        public static int Total => 3;
    }

    sealed record Tinted(Vector3 Position, Color Tint, float Ratio);

    [Test]
    public void MembersListPublicInstanceStateAndSkipStaticsAndIndexers()
    {
        var entity = Walker.Members(typeof(Entity)).Select(m => m.Name).ToArray();
        Check.That(new[] { "Id", "CfgId", "Position", "Kind", "Rarity", "Address" }.All(entity.Contains), $"Entity members {string.Join(",", entity)}");
        Check.That(Walker.Members(typeof(GameState)).Any(m => m.Name == "Camera"), "GameState.Camera missing");
        Check.That(!Walker.Members(typeof(GameState)).Any(m => m.Name == "Empty"), "static Empty listed");
        Check.Equal("Value", string.Join(",", Walker.Members(typeof(Indexed)).Select(m => m.Name)));
    }

    [Test]
    public void ElementTypesComeFromArraysAndGenericSequences()
    {
        Check.Equal(typeof(Entity), Walker.ElementType(typeof(Entity[])));
        Check.Equal(typeof(KeyValuePair<string, int>), Walker.ElementType(typeof(Dictionary<string, int>)));
        Check.That(Walker.IsFlat(typeof(int[])) && Walker.IsFlat(typeof(string)) && !Walker.IsFlat(typeof(Player)), "IsFlat wrong");
    }

    [Test]
    public void FormatCoversEveryValueKind()
    {
        Check.Equal("1.26", Walker.Format(1.256f));
        Check.Equal("NaN", Walker.Format(float.NaN));
        Check.Equal("1.0, 2.1, 3.0", Walker.Format(new Vector3(1.04f, 2.06f, 3f)));
        Check.Equal("#FF001080", Walker.Format(Color.FromArgb(0x80, 255, 0, 16)));
        Check.Equal("Boss", Walker.Format(Rarity.Boss));
        Check.Equal("null", Walker.Format(null));
        Check.Equal("true", Walker.Format(true));
        Check.Equal("42", Walker.Format(42));
        Check.Equal("0x1A2B", Walker.Format((nint)0x1A2B));
        Check.Equal("Int32[] [3]", Walker.Format(new[] { 1, 2, 3 }));
        Check.Equal("<error: InvalidOperationException>", Walker.Format(new Failed("InvalidOperationException")));
    }

    [Test]
    public void PositionPickFindsPositionsAndNothingElse()
    {
        Check.Equal(Samples.Entities[0].Position, Walker.Position(Samples.Entities[0])!.Value);
        Check.Equal(Samples.Player.Position, Walker.Position(Samples.Player)!.Value);
        Check.That(Walker.Position(Samples.Icons[0]) is null, "MapIcon positions are 2D");
        Check.That(Walker.Position(null) is null, "null has no position");
        Check.That(Walker.HasPosition(typeof(Entity)) && !Walker.HasPosition(typeof(Camera)), "HasPosition wrong");
    }

    [Test]
    public void DistanceIsFlatAcrossTheMapPlane()
    {
        var entity = new Entity(1, 1, new Vector3(3, 4, 9), EntityKind.Monster, Rarity.Normal);
        Check.That(Walker.Distance(entity, Vector3.Zero) == 5f, "expected 5 - X / Y, Z ignored");
        Check.That(Walker.Distance(Samples.Camera, Vector3.Zero) is null, "no position, no distance");
    }

    [Test]
    public void JsonWritesColoursAsHexAndKeepsVectorFields()
    {
        var json = Walker.Json(new Tinted(new Vector3(1, 2, 3), Color.FromArgb(0x80, 255, 0, 0), 1.5f));
        Check.That(json.Contains("\"Tint\":\"#FF000080\""), json);
        Check.That(json.Contains("\"X\":1") && json.Contains("\"Z\":3"), json);
        Check.That(Walker.Json(new Tinted(Vector3.Zero, Color.Red, float.NaN)).Contains("NaN"), "NaN must serialise");
    }

    [Test]
    public void AThrowingGetterBecomesAMarkerAndLogsOnce()
    {
        Walker.Clear();
        var member = Walker.Members(typeof(Thrower)).Single(m => m.Name == "Bad");
        var first = Walker.Read(typeof(Thrower), member, new Thrower());
        var second = Walker.Read(typeof(Thrower), member, new Thrower());
        Check.That(first is Failed { Exception: "InvalidOperationException" }, "first read");
        Check.That(second is Failed, "second read");
        Check.Equal(1, Walker.ErrorsLogged);
        Check.Equal(1, Walker.Read(typeof(Thrower), Walker.Members(typeof(Thrower)).Single(m => m.Name == "Fine"), new Thrower()));
    }
}
