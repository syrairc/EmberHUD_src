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
    static MapIcon Icon(string texture, string name = "", int type = 1, int entity = 0) => new(type, texture, Vector2.Zero, 1, 0, entity) { Name = name };

    [Test]
    public void GameMinimapSettingsDefaultOffWithNoOffset()
    {
        var general = new Settings().General;
        Check.That(!general.UseGameMinimap);
        Check.Equal(0f, general.GameOffsetX);
        Check.Equal(0f, general.GameOffsetY);
    }

    [Test]
    public void GameMinimapSettingsSurviveAJsonRoundTrip()
    {
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var general = new GeneralTab { UseGameMinimap = true, GameOffsetX = 3, GameOffsetY = -4 };

        var back = System.Text.Json.JsonSerializer.Deserialize<GeneralTab>(System.Text.Json.JsonSerializer.Serialize(general, options), options)!;

        Check.That(back.UseGameMinimap);
        Check.Equal(3f, back.GameOffsetX);
        Check.Equal(-4f, back.GameOffsetY);
    }

    [Test]
    public void MonstersPickTheFixedRowOfTheirRarity()
    {
        var s = new Settings();
        Check.That(ReferenceEquals(s.Icons.NormalMonsters, s.Icons.FixedFor(Scene.Entity(EntityKind.Monster, Vector3.Zero, Rarity.Normal))));
        Check.That(ReferenceEquals(s.Icons.MagicMonsters, s.Icons.FixedFor(Scene.Entity(EntityKind.Monster, Vector3.Zero, Rarity.Magic))));
        Check.That(ReferenceEquals(s.Icons.RareMonsters, s.Icons.FixedFor(Scene.Entity(EntityKind.Monster, Vector3.Zero, Rarity.Rare))));
        Check.That(ReferenceEquals(s.Icons.Bosses, s.Icons.FixedFor(Scene.Entity(EntityKind.Monster, Vector3.Zero, Rarity.Boss))));
    }

    [Test]
    public void FriendlyMonstersPickTheNpcRow()
    {
        var s = new Settings();
        var friendly = Scene.Entity(EntityKind.Monster, Vector3.Zero, Rarity.Normal) with { Friendly = true };
        Check.That(ReferenceEquals(s.Icons.Npcs, s.Icons.FixedFor(friendly)));
        Check.That(ReferenceEquals(s.Icons.NormalMonsters, s.Icons.FixedFor(Scene.Entity(EntityKind.Monster, Vector3.Zero))));
    }

    [Test]
    public void OtherKindsMapToTheirFixedRowOrNone()
    {
        var s = new Settings();
        Check.That(ReferenceEquals(s.Icons.Minions, s.Icons.FixedFor(Scene.Entity(EntityKind.Ally, Vector3.Zero))));
        Check.That(ReferenceEquals(s.Icons.OtherPlayers, s.Icons.FixedFor(Scene.Entity(EntityKind.Player, Vector3.Zero))));
        Check.That(ReferenceEquals(s.Icons.Npcs, s.Icons.FixedFor(Scene.Entity(EntityKind.Npc, Vector3.Zero))));
        Check.That(ReferenceEquals(s.Icons.MapObjects, s.Icons.FixedFor(Scene.Entity(EntityKind.MapObject, Vector3.Zero))));
        Check.That(ReferenceEquals(s.Icons.Destructibles, s.Icons.FixedFor(Scene.Entity(EntityKind.Destructible, Vector3.Zero))));
        foreach (var kind in new[] { EntityKind.Portal, EntityKind.Interactive, EntityKind.Coffin, EntityKind.Altar, EntityKind.Elevator })
            Check.That(s.Icons.FixedFor(Scene.Entity(kind, Vector3.Zero)) is null, kind.ToString());
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
        s.Icons.Discover([Icon("Tex1", "Portal"), Icon(""), Icon("Tex1", "Portal"), Icon("Tex2")]);
        Check.Equal(2, s.Icons.Found.Count);
        var portal = s.Icons.FoundFor("Tex1")!;
        Check.Equal("Tex1", portal.Name);
        Check.Equal("game:Tex1", portal.Icon);
        Check.Equal("Tex2", s.Icons.FoundFor("Tex2")!.Name);
        Check.That(s.Icons.FoundFor("") is null);
    }

    [Test]
    public void AnIconWithNoPresetIsNamedAfterItsTexture()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("A", "S15MonsterNormal")]);
        Check.Equal("A", s.Icons.FoundFor("A")!.Name);
    }

    [Test]
    public void APresetNameBeatsTheGamesInternalName()
    {
        var s = new Settings();
        s.Icons.Discover([Icon("UI_MiniMap_cangku", "wanjiacangku")]);
        Check.Equal("Player Stash / Stash", s.Icons.FoundFor("UI_MiniMap_cangku")!.Name);
        s.Icons.Found = [new IconStyle { Key = "UI_MiniMap_cangku", Name = "wanjiacangku" }];
        s.Icons.Discover([Icon("UI_MiniMap_cangku", "wanjiacangku")]);
        Check.Equal("Player Stash / Stash", s.Icons.FoundFor("UI_MiniMap_cangku")!.Name);
        s.Icons.FoundFor("UI_MiniMap_cangku")!.Name = "My stash";
        s.Icons.SeedPresets();
        Check.Equal("My stash", s.Icons.FoundFor("UI_MiniMap_cangku")!.Name);
    }

    [Test]
    public void SeedingUpgradesRowsSavedWithAnInternalName()
    {
        var s = new Settings();
        s.Icons.Found = [new IconStyle { Key = "UI_MiniMap_cangku", Name = "cangku" }, new IconStyle { Key = "UI_MiniMap_Skill", Name = "Skill vendor" }];
        s.Icons.SeedPresets();
        Check.Equal("Player Stash / Stash", s.Icons.FoundFor("UI_MiniMap_cangku")!.Name);
        Check.Equal("Skill vendor", s.Icons.FoundFor("UI_MiniMap_Skill")!.Name);
    }

    [Test]
    public void SeedingAddsEveryPresetOnceAndKeepsRenamedRows()
    {
        var s = new Settings();
        s.Icons.Found = [new IconStyle { Key = "UI_MiniMap_Portal", Name = "Mine", Show = false }];
        s.Icons.SeedPresets();
        var count = s.Icons.Found.Count;
        s.Icons.SeedPresets();
        Check.Equal(count, s.Icons.Found.Count);
        Check.Equal(IconPresets.All.Length, count);
        Check.Equal("Mine", s.Icons.FoundFor("UI_MiniMap_Portal")!.Name);
        Check.That(!s.Icons.FoundFor("UI_MiniMap_Portal")!.Show);
        Check.Equal("game:UI_MiniMap_BOSS", s.Icons.FoundFor("UI_MiniMap_BOSS")!.Icon);
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

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(TestFrame.Vertices(new Settings().Draw, inWindow: true) > 0, "settings page draws");
    }

    [Test]
    public void TheRegionLookUsesTheGamesMinimapPalette()
    {
        var map = new Settings().Map;
        var region = map.Region;

        Check.Equal(MapStyle.Region, map.DrawStyle);
        Check.That(Enum.GetNames<MapStyle>().SequenceEqual(new[] { "Region", "Walls" }), string.Join(", ", Enum.GetNames<MapStyle>()));
        Check.Equal(GameColors.MinimapUnexplored.ToArgb(), region.Outline.ToArgb());
        Check.Equal(GameColors.MinimapUnexplored.ToArgb() & 0xFFFFFF, region.Fill.ToArgb() & 0xFFFFFF, "the fill is the outline colour");
        Check.That(region.Fill.A < 0xFF, "and see-through");
        Check.Equal(GameColors.MinimapOutline.ToArgb(), region.ExploredOutline.ToArgb());
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
        var coffin = new IconStyle { Key = "UI_MiniMap_S15guancai", Name = "Vigil coffin" };

        Check.That(Settings.Matches(coffin, "vigil"), "name");
        Check.That(Settings.Matches(coffin, "guancai"), "texture");
        Check.That(Settings.Matches(coffin, ""), "empty filter");
        Check.That(!Settings.Matches(coffin, "portal"), "neither");
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
        icons.ZoomScaleFactor = 1;
        Check.That(MathF.Abs(icons.ZoomScale(GeneralTab.DefaultZoom * 1.5f) - 1.5f) < 1e-4f);
        icons.ZoomScaleFactor = 0.5f;
        Check.That(MathF.Abs(icons.ZoomScale(GeneralTab.DefaultZoom * 1.69f) - 1.3f) < 1e-4f);
        icons.ZoomScaleFactor = 0;
        Check.Equal(1f, icons.ZoomScale(GeneralTab.DefaultZoom * 3));
    }

    [Test]
    public void ZoomScaleIsClampedToMinAndMax()
    {
        var icons = new Settings().Icons;
        icons.ZoomScaleFactor = 1;
        icons.ZoomScaleMin = 0.5f;
        icons.ZoomScaleMax = 2;
        Check.Equal(2f, icons.ZoomScale(GeneralTab.DefaultZoom * 10));
        Check.Equal(0.5f, icons.ZoomScale(GeneralTab.DefaultZoom / 10));
    }

    [Test]
    public void ZoomScaleIsOneWhenOffOrTheScaleIsBad()
    {
        var icons = new Settings().Icons;
        Check.Equal(1f, icons.ZoomScale(0));
        Check.Equal(1f, icons.ZoomScale(float.NaN));
        icons.ScaleWithZoom = false;
        Check.Equal(1f, icons.ZoomScale(GeneralTab.DefaultZoom * 2));
    }
}
