# Minimap - to do

From the 2026-10-11 code review (`E:\EmberHUD\docs\superpowers\reviews\2026-10-11-code-review.md`).

- L9 - `Settings.cs` "Map" section uses raw `ImGui.CollapsingHeader(..., DefaultOpen)` - switch to `Controls.Category` (header open state resets once)
- L20 - `Main.cs` marker colour is `Color.FromArgb(0xF9, 0xDD, 0x75)` - use `GameColors.MinimapMarker` (same value, 0 users). Host `Icons/IconSheet.cs` has the same literal, fix both together
- L21 - `Settings.IconFor` / `FallbackShape` are render lookups that read no setting - move them plus their 3 tests to `Main.cs`. `ViewTab` / `MarkerTab` aren't tabs -> rename to `ViewSettings` / `MarkerSettings` (field names stay, so the JSON does too)
- L22 - `Tests/SettingsTests.cs` and `Tests/RenderTests.cs` hand-roll the vertex-count probe - use `TestFrame.Vertices(draw, inWindow)` from `EmberHUD.Testing` (landed 2026-10-13)
- L23 - tests restate private numbers - `MainTests` `ZoomRangeGoesFromAThousandthToFour` asserts `0.002f` (name and value disagree), `SettingsTests` `EveryMarkerHasADarkAccentByDefault` only checks `RareMonster` while `defaults.json` gives Portal / Altar / Elevator a white accent, `SettingsTests` uses hex where `GameColors.MinimapUnexplored` / `MinimapOutline` exist. Delete pure "default equals literal" tests, fix the two names, assert the relation instead
