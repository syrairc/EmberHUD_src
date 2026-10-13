using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using EmberHUD;
using EmberHUD.Testing;

namespace Minimap;

public sealed class SettingsTests
{
    static Entity Of(EntityKind kind, Rarity rarity = Rarity.Normal) => new(1, 1000, Vector3.Zero, kind, rarity);

    static MapIcon Icon(string texture, string name = "", int type = 1, int entity = 0) => new(type, texture, Vector2.Zero, 1, 0, entity) { Name = name };

    [Test]
    public void MonstersPickTheFixedRowOfTheirRarity()
    {
        var s = new Settings();
        Check.That(ReferenceEquals(s.Icons.NormalMonsters, s.Icons.FixedFor(Of(EntityKind.Monster, Rarity.Normal))));
        Check.That(ReferenceEquals(s.Icons.MagicMonsters, s.Icons.FixedFor(Of(EntityKind.Monster, Rarity.Magic))));
        Check.That(ReferenceEquals(s.Icons.RareMonsters, s.Icons.FixedFor(Of(EntityKind.Monster, Rarity.Rare))));
        Check.That(ReferenceEquals(s.Icons.Bosses, s.Icons.FixedFor(Of(EntityKind.Monster, Rarity.Boss))));
    }

    [Test]
    public void OtherKindsMapToTheirFixedRowOrNone()
    {
        var s = new Settings();
        Check.That(ReferenceEquals(s.Icons.Minions, s.Icons.FixedFor(Of(EntityKind.Ally))));
        Check.That(ReferenceEquals(s.Icons.OtherPlayers, s.Icons.FixedFor(Of(EntityKind.Player))));
        Check.That(ReferenceEquals(s.Icons.Npcs, s.Icons.FixedFor(Of(EntityKind.Npc))));
        Check.That(ReferenceEquals(s.Icons.MapObjects, s.Icons.FixedFor(Of(EntityKind.MapObject))));
        Check.That(ReferenceEquals(s.Icons.Destructibles, s.Icons.FixedFor(Of(EntityKind.Destructible))));
        foreach (var kind in new[] { EntityKind.Portal, EntityKind.Interactive, EntityKind.Coffin, EntityKind.Altar, EntityKind.Elevator })
            Check.That(s.Icons.FixedFor(Of(kind)) is null, kind.ToString());
    }

    [Test]
    public void FixedRowsComeInTheTableOrder() =>
        Check.Equal("Normal monsters,Magic monsters,Rare monsters,Bosses,Minions,NPCs,Other players,Map objects,Destructibles", string.Join(",", new Settings().Icons.Fixed.Select(row => row.Name)));

    [Test]
    public void OnlyMinionsAreHiddenByDefault() =>
        Check.Equal("Minions", string.Join(",", new Settings().Icons.Fixed.Where(row => !row.Show).Select(row => row.Name)));

    [Test]
    public void DiscoveryAddsEachTextureOnceAndSkipsEmptyOnes()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("UI_MiniMap_Portal", "Portal"), Icon(""), Icon("UI_MiniMap_Portal", "Portal"), Icon("UI_MiniMap_BOSS")]);
        Check.Equal(2, s.Icons.Found.Count);
        var portal = s.Icons.FoundFor("UI_MiniMap_Portal")!;
        Check.Equal("Portal", portal.Name);
        Check.Equal("game:UI_MiniMap_Portal", portal.Icon);
        Check.Equal("UI_MiniMap_BOSS", s.Icons.FoundFor("UI_MiniMap_BOSS")!.Name);
        Check.That(s.Icons.FoundFor("") is null);
    }

    [Test]
    public void AGameNameUpgradesOnlyRowsStillShowingTheirTexture()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("A"), Icon("B")]);
        s.Icons.FoundFor("B")!.Name = "My chest";
        s.Icons.Discover([Icon("A", "S15MonsterNormal"), Icon("B", "Chest")]);
        Check.Equal("S15MonsterNormal", s.Icons.FoundFor("A")!.Name);
        Check.Equal("My chest", s.Icons.FoundFor("B")!.Name);
    }

    [Test]
    public void AReplacedFoundListIsLookedUpAfresh()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("A")]);
        s.Icons.Found = [new IconStyle { Key = "B", Name = "B" }];
        Check.That(s.Icons.FoundFor("A") is null);
        Check.That(s.Icons.FoundFor("B") is not null);
    }

    [Test]
    public void NullOrKeylessFoundEntriesAreSkipped()
    {
        var s = new Settings();
        s.Icons.Found = [null!, new IconStyle { Key = null!, Name = "x" }, new IconStyle { Key = "A", Name = "A" }];
        Check.That(s.Icons.FoundFor("A") is not null);
        Check.That(s.Icons.FoundFor("B") is null);
        s.Icons.Discover([Icon("B")]);
        Check.That(s.Icons.FoundFor("B") is not null);
    }

    static int Drawn(Action draw)
    {
        var drawn = 0;
        TestFrame.Run(() =>
        {
            ImGui.Begin("minimap settings");
            var before = ImGui.GetWindowDrawList().VtxBuffer.Size;
            draw();
            drawn = ImGui.GetWindowDrawList().VtxBuffer.Size - before;
            ImGui.End();
        });
        return drawn;
    }

    static float Height(Action draw)
    {
        var height = 0f;
        TestFrame.Run(() =>
        {
            ImGui.Begin("minimap settings");
            var before = ImGui.GetCursorPosY();
            draw();
            height = ImGui.GetCursorPosY() - before;
            ImGui.End();
        });
        return height;
    }

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(Drawn(new Settings().Draw) > 0, "settings page draws");
    }

    [Test]
    public void FallbackShapesFollowTheIconTypeGroup()
    {
        foreach (var type in new[] { 2, 1040, 1050, 1060 }) Check.Equal(MarkerShape.Star, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 10, 11, 12, 14, 32, 33, 1000, 1022 }) Check.Equal(MarkerShape.Diamond, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 7, 8, 9, 1010, 2001, 2002 }) Check.Equal(MarkerShape.Hexagon, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 3, 1006, 1007, 1009, 1011 }) Check.Equal(MarkerShape.Square, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 15, 20, 24, 29 }) Check.Equal(MarkerShape.Triangle, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 1003, 1008, 1023, 1024 }) Check.Equal(MarkerShape.Sparkle, Settings.FallbackShape(type), $"type {type}");
        foreach (var type in new[] { 0, 1, 25, 1061, 99999, -5 }) Check.Equal(MarkerShape.Dot, Settings.FallbackShape(type), $"type {type}");
    }

    [Test]
    public void TheRegionLookIsTheDefaultWithTheGamesColours()
    {
        var map = new Settings().Map;
        var region = map.Region;

        Check.Equal("Region", map.DrawStyle.Value);
        Check.That(map.DrawStyle.Options.SequenceEqual(new[] { "Region", "Walls" }), string.Join(", ", map.DrawStyle.Options));
        Check.Equal(Color.FromArgb(0x1F, 0x5F, 0xB0, 0xB8).ToArgb(), region.Fill.Value.ToArgb());
        Check.Equal(Color.FromArgb(0xFF, 0x5F, 0xB0, 0xB8).ToArgb(), region.Outline.Value.ToArgb());
        Check.Equal(1.5f, region.OutlineWidth.Value);
        Check.Equal(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF).ToArgb(), region.ExploredFill.Value.ToArgb());
        Check.Equal(Color.FromArgb(0xFF, 0xC1, 0xBB, 0xB5).ToArgb(), region.ExploredOutline.Value.ToArgb());
        Check.Equal(2.5f, region.ExploredOutlineWidth.Value);
    }

    [Test]
    public void GameIconsDefaultToOnAtScaleOne()
    {
        var icons = new Settings().Icons;

        Check.That(icons.UseGameIcons.Value, "on by default, a missing icon folder falls back to shapes anyway");
        Check.Equal(1f, icons.GameIconScale.Value);
    }

    [Test]
    public void RenamingToBlankGoesBackToTheTextureName()
    {
        var row = new IconStyle { Key = "UI_MiniMap_S15guancai", Name = "Coffin" };
        Settings.Rename(row, "  Vigil coffin ");
        Check.Equal("Vigil coffin", row.Name);
        Settings.Rename(row, "   ");
        Check.Equal("UI_MiniMap_S15guancai", row.Name);
    }

    [Test]
    public void TheFilterMatchesNameOrTexture()
    {
        IconStyle[] rows = [new() { Key = "UI_MiniMap_S15guancai", Name = "Vigil coffin" }, new() { Key = "UI_MiniMap_Portal", Name = "Portal" }];
        Check.Equal("Vigil coffin", string.Join(",", Settings.Filtered(rows, "vigil").Select(r => r.Name)));
        Check.Equal("Vigil coffin", string.Join(",", Settings.Filtered(rows, "guancai").Select(r => r.Name)));
        Check.Equal(2, Settings.Filtered(rows, "").Count());
    }

    [Test]
    public void TheSettingsPageDrawsWithBalancedFrames()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("UI_MiniMap_Portal", "Portal")]);
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(700, 600));
            ImGui.Begin("settings");
            s.Draw();
            ImGui.End();
        });
    }

    [Test]
    public void TheMapAndIconsTabsDrawWithBalancedFrames()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("UI_MiniMap_Portal", "Portal"), Icon("UI_MiniMap_S15guancai", "Coffin")]);
        s.Icons.RareMonsters.Show = false;
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(700, 600));
            ImGui.Begin("settings");
            s.DrawMap();
            s.DrawIcons();
            ImGui.End();
        });
    }

    [Test]
    public void ARenamedFixedRowStillDrawsTheIconsTab()
    {
        var s = new Settings();
        s.Icons.NormalMonsters.Name = "Old label";
        TestFrame.Run(() =>
        {
            ImGui.SetNextWindowSize(new Vector2(700, 600));
            ImGui.Begin("settings");
            s.DrawIcons();
            ImGui.End();
        });
    }

    [Test]
    public void IconsKeepTheirSizeAtTheDefaultZoom() => Check.Equal(1f, new Settings().Icons.ZoomScale(GeneralTab.DefaultZoom));

    [Test]
    public void ZoomScaleFollowsTheFactorAsAPower()
    {
        var icons = new Settings().Icons;
        icons.ZoomScaleFactor.Value = 1;
        Check.That(MathF.Abs(icons.ZoomScale(GeneralTab.DefaultZoom * 1.5f) - 1.5f) < 1e-4f);
        icons.ZoomScaleFactor.Value = 0.5f;
        Check.That(MathF.Abs(icons.ZoomScale(GeneralTab.DefaultZoom * 1.69f) - 1.3f) < 1e-4f);
        icons.ZoomScaleFactor.Value = 0;
        Check.Equal(1f, icons.ZoomScale(GeneralTab.DefaultZoom * 3));
    }

    [Test]
    public void ZoomScaleIsClampedToMinAndMax()
    {
        var icons = new Settings().Icons;
        icons.ZoomScaleFactor.Value = 1;
        icons.ZoomScaleMin.Value = 0.5f;
        icons.ZoomScaleMax.Value = 2;
        Check.Equal(2f, icons.ZoomScale(GeneralTab.DefaultZoom * 10));
        Check.Equal(0.5f, icons.ZoomScale(GeneralTab.DefaultZoom / 10));
    }

    [Test]
    public void ZoomScaleIsOneWhenOffOrTheScaleIsBad()
    {
        var icons = new Settings().Icons;
        Check.Equal(1f, icons.ZoomScale(0));
        Check.Equal(1f, icons.ZoomScale(float.NaN));
        icons.ScaleWithZoom.Value = false;
        Check.Equal(1f, icons.ZoomScale(GeneralTab.DefaultZoom * 2));
    }
}
