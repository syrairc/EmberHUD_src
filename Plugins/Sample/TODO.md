# Sample - to do

From the 2026-10-11 code review (`E:\EmberHUD\docs\superpowers\reviews\2026-10-11-code-review.md`).

- L22 - `Tests/SampleTests.cs` hand-rolls the vertex-count probe twice - use `TestFrame.Vertices(draw, inWindow)` from `EmberHUD.Testing` (landed 2026-10-13)
