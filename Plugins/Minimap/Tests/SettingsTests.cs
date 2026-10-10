using System;
using System.Numerics;
using Hexa.NET.ImGui;
using TorchlightSDK;
using TorchlightSDK.Testing;

namespace Minimap;

public sealed class SettingsTests
{
    static Entity Of(EntityKind kind, Rarity rarity = Rarity.Normal) => new(1, 1000, Vector3.Zero, kind, rarity);

    [Test]
    public void MonstersPickTheStyleOfTheirRarity()
    {
        var s = new Settings();

        Check.That(ReferenceEquals(s.Markers.NormalMonster, s.MarkerFor(Of(EntityKind.Monster, Rarity.Normal))));
        Check.That(ReferenceEquals(s.Markers.MagicMonster, s.MarkerFor(Of(EntityKind.Monster, Rarity.Magic))));
        Check.That(ReferenceEquals(s.Markers.RareMonster, s.MarkerFor(Of(EntityKind.Monster, Rarity.Rare))));
        Check.That(ReferenceEquals(s.Markers.BossMonster, s.MarkerFor(Of(EntityKind.Monster, Rarity.Boss))));
    }

    [Test]
    public void EveryOtherKindHasItsOwnStyle()
    {
        var s = new Settings();

        Check.That(ReferenceEquals(s.Markers.Ally, s.MarkerFor(Of(EntityKind.Ally))));
        Check.That(ReferenceEquals(s.Markers.OtherPlayer, s.MarkerFor(Of(EntityKind.Player))));
        Check.That(ReferenceEquals(s.Markers.Portal, s.MarkerFor(Of(EntityKind.Portal))));
        Check.That(ReferenceEquals(s.Markers.Npc, s.MarkerFor(Of(EntityKind.Npc))));
        Check.That(ReferenceEquals(s.Markers.Interactive, s.MarkerFor(Of(EntityKind.Interactive))));
    }

    [Test]
    public void AHiddenKindIsNotDrawn()
    {
        var s = new Settings();
        s.Markers.MagicMonster.Show.Value = false;
        s.Markers.Portal.Show.Value = false;

        Check.That(s.MarkerFor(Of(EntityKind.Monster, Rarity.Magic)) is null);
        Check.That(s.MarkerFor(Of(EntityKind.Portal)) is null);
        Check.That(s.MarkerFor(Of(EntityKind.Monster, Rarity.Rare)) is not null);
    }

    [Test]
    public void ShapesFollowTheKind()
    {
        Check.Equal(MarkerShape.Diamond, Settings.ShapeFor(EntityKind.Portal));
        Check.Equal(MarkerShape.Square, Settings.ShapeFor(EntityKind.Npc));
        Check.Equal(MarkerShape.Triangle, Settings.ShapeFor(EntityKind.Interactive));
        Check.Equal(MarkerShape.Dot, Settings.ShapeFor(EntityKind.Monster));
        Check.Equal(MarkerShape.Dot, Settings.ShapeFor(EntityKind.Player));
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

    [Test]
    public void TheSettingsPageDraws()
    {
        Check.That(Drawn(new Settings().Draw) > 0, "settings page draws");
    }

    [Test]
    public void TheMarkersSectionDraws()
    {
        var page = Drawn(new Settings().Draw);
        var viewOnly = Drawn(() => SettingsDrawer.DrawNodes(new Settings().View));
        Check.That(page > 1.5f * viewOnly, $"markers drew (page {page}, view only {viewOnly})");
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
    public void GameIconSettingsDefaultToOnAtAReadableSize()
    {
        var view = new Settings().View;

        Check.That(view.UseGameIcons.Value, "on by default, a missing icon folder falls back to shapes anyway");
        Check.Equal(24f, view.IconSize.Value);
        Check.Equal(System.Drawing.Color.White.ToArgb(), view.IconTint.Value.ToArgb());
    }
}
