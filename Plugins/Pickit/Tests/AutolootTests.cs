using System.Collections.Generic;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Pickit;

public sealed class AutolootTests
{
    sealed class Rig
    {
        public readonly Autoloot Engine = new();
        public readonly Settings Settings = new();
        public readonly HashSet<Key> Held = [];
        public readonly List<string> Sent = [];
        public bool DownOk = true;
        public bool Allowed = true;

        public void Step(GameState state) => Engine.Step(Settings, state, Held.Contains, key => { Sent.Add($"down {key}"); return DownOk; }, key => { Sent.Add($"up {key}"); return true; }, Allowed);
    }

    static GameState Near(params GroundItem[] items) => new() { Player = new Player(Vector3.Zero, Vector2.UnitX), GroundItems = items };

    static GroundItem Item(float x, bool owned = true) => new(1, Vector2.Zero, Vector2.One, new Vector3(x, 0, 0), owned, [], []);

    static Rig NearOnly() => new() { Settings = { HoldKeyEnabled = false } };

    [Test]
    public void HoldKeyPressesPickup()
    {
        var rig = new Rig { Held = { Key.Space } };
        rig.Step(new GameState());
        Check.Equal("down A", string.Join(",", rig.Sent));
        Check.That(rig.Engine.Holding, "holding");
        rig.Step(new GameState());
        Check.Equal(1, rig.Sent.Count, "no repeat while held");
    }

    [Test]
    public void ReleasesWhenHoldKeyLetGo()
    {
        var rig = new Rig { Held = { Key.Space } };
        rig.Step(new GameState());
        rig.Held.Clear();
        rig.Held.Add(Key.A);
        rig.Step(new GameState());
        rig.Held.Clear();
        rig.Step(new GameState());
        Check.Equal("down A,up A", string.Join(",", rig.Sent));
        Check.That(!rig.Engine.Holding, "not holding");
    }

    [Test]
    public void NearItemPressesPickup()
    {
        var rig = NearOnly();
        rig.Step(Near(Item(499)));
        Check.Equal("down A", string.Join(",", rig.Sent));
    }

    [Test]
    public void NearIgnoresUnownedAndFarItems()
    {
        var rig = NearOnly();
        rig.Step(Near(Item(100, owned: false), Item(501)));
        Check.Equal(0, rig.Sent.Count);
    }

    [Test]
    public void NoPlayerNoNear()
    {
        var rig = NearOnly();
        rig.Step(new GameState { GroundItems = [Item(0)] });
        rig.Step(new GameState { Player = new Player(Vector3.Zero, Vector2.UnitX), GroundItems = null });
        Check.Equal(0, rig.Sent.Count);
    }

    [Test]
    public void PlayerHoldingPickupKeyIsLeftAlone()
    {
        var rig = new Rig { Held = { Key.Space, Key.A } };
        rig.Step(new GameState());
        Check.Equal(0, rig.Sent.Count);
        Check.That(!rig.Engine.Holding, "not holding");
    }

    [Test]
    public void DownRefusedRetriesNextTick()
    {
        var rig = new Rig { Held = { Key.Space }, DownOk = false };
        rig.Step(new GameState());
        Check.That(!rig.Engine.Holding, "refused");
        rig.DownOk = true;
        rig.Step(new GameState());
        Check.That(rig.Engine.Holding, "retried");
        Check.Equal(2, rig.Sent.Count);
    }

    [Test]
    public void SendNotAllowedSendsNothingAndResets()
    {
        var rig = new Rig { Held = { Key.Space } };
        rig.Step(new GameState());
        rig.Allowed = false;
        rig.Step(new GameState());
        Check.That(!rig.Engine.Holding, "reset");
        Check.Equal(1, rig.Sent.Count, "nothing new while not allowed");
        rig.Allowed = true;
        rig.Step(new GameState());
        Check.Equal(2, rig.Sent.Count, "down again");
        Check.Equal("down A", rig.Sent[1]);
    }

    [Test]
    public void BothTriggersOffNeverPresses()
    {
        var rig = new Rig { Held = { Key.Space }, Settings = { HoldKeyEnabled = false, NearEnabled = false } };
        rig.Step(Near(Item(0)));
        Check.Equal(0, rig.Sent.Count);
    }

    [Test]
    public void TriggerOffMidHoldReleasesSameTick()
    {
        var rig = new Rig { Held = { Key.Space } };
        rig.Step(new GameState());
        rig.Settings.HoldKeyEnabled = false;
        rig.Step(new GameState());
        Check.Equal("down A,up A", string.Join(",", rig.Sent));
    }
}
