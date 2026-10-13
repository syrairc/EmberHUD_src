using System.Linq;
using System.Numerics;
using EmberHUD;
using EmberHUD.Testing;

namespace Inspector;

public sealed class MainTests
{
    [Test]
    public void FrozenHoldsTheSnapshotUntilResume()
    {
        TestGame.Set(Samples.Rich);
        var main = new Main();
        main.Freeze();

        TestGame.Set(GameState.Empty with { AreaId = 99 });

        Check.That(main.Frozen, "not frozen");
        Check.Equal(7, main.Shown.AreaId);

        main.Resume();

        Check.That(!main.Frozen, "still frozen");
        Check.Equal(99, main.Shown.AreaId);
    }

    [Test]
    public void TheWindowDrawsClosedOpenFrozenAndWithoutAGame()
    {
        var main = new Main();
        TestFrame.Run(main.Render);
        Check.That(!main.Open, "rendering opened a closed window");

        main.Open = true;
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
        Check.That(main.Open && !main.Frozen, "open frame changed state");

        main.Freeze();
        TestFrame.Run(main.Render);
        Check.That(main.Open && main.Frozen, "frozen frame changed state");
        main.Resume();

        TestGame.Set(GameState.Empty);
        TestFrame.Run(main.Render);
        Check.That(main.Open && !main.Frozen, "empty game frame changed state");
    }

    [Test]
    public void HighlightRingsDrawThroughTheCameraAndTheMapView()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);
        main.Browser.Selected = Samples.Entities[0];

        TestFrame.Run(main.Render);
        Map.View = new MapView(new Vector2(100, 100), 2f);
        TestFrame.Run(main.Render);
        TestGame.Set(Samples.Rich with { Camera = null });
        TestFrame.Run(main.Render);

        Map.View = null;
        TestFrame.Run(main.Render);

        Check.Equal(Samples.Entities[0].Position, main.Browser.Highlights().Single().Position);
    }

    [Test]
    public void TheToggleHotkeyOpensAndClosesTheWindow()
    {
        var main = new Main();
        main.Initialize();
        TestInput.Hold();
        TestInput.Dispatch(main);
        Check.That(!main.Open, "no key, no toggle");
        TestInput.Hold(Key.F6);
        TestInput.Dispatch(main);
        Check.That(main.Open, "press opens");
        TestInput.Hold();
        TestInput.Hold(Key.F6);
        TestInput.Dispatch(main);
        Check.That(!main.Open, "second press closes");
    }

    [Test]
    public void RegistersTwoHotkeysWithTheOldDefaults()
    {
        var main = new Main();
        main.Initialize();
        Check.Equal("Toggle window,Pick mode", string.Join(",", main.Hotkeys.Select(binding => binding.Id)));
        Check.Equal("F6,F7", string.Join(",", main.Hotkeys.Select(binding => binding.Default.Key)));
    }

    [Test]
    public void DisposeClearsTheCaches()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);
        TestFrame.Run(main.Render);
        main.Browser.Selected = Samples.Entities[0];
        Walker.Clear();
        Walker.Read(typeof(Thrower), Walker.Members(typeof(Thrower)).Single(m => m.Name == "Bad"), new Thrower());
        Check.Equal(1, Walker.ErrorsLogged);

        main.Dispose();

        Check.That(main.Browser.Selected is null, "selection survived Dispose");
        Check.Equal(0, Walker.ErrorsLogged);
    }

    [Test]
    public void TheExplorerSitsBehindTheObjectsTab()
    {
        var main = new Main { Open = true };
        TestGame.Set(Samples.Rich);

        Check.That(main.Explorer is not null, "explorer exists");
        TestFrame.Run(main.Render);
    }
}
