# TorchlightSDK - code

Plugin SDK for a Torchlight: Infinite overlay. `TorchlightSDK.Api` is the public API plugins compile against (never obfuscated, nothing sensitive in it). `TorchlightSDK` is the host exe (everything `internal`, obfuscated in Release). Plugins are C# source in `Plugins\<Name>\`, compiled by the host with Roslyn. Same shape as EpochSDK (`E:\EpochSDK\src`, read-only reference) - host / plugin / UI layers get copied over and renamed, the game layer (UE4 reading) is new.

Phase 1 scaffold and the phase 2 Minimap plugin are built and run on a fake game - `TorchlightSDK.exe --check` (headless, host JIT + every plugin's tests) is the only run the agent does, the user runs `TorchlightSDK.exe --fake` for the overlay with synthetic players, monsters and terrain. No live reader yet. Plan -> `..\docs\superpowers\plans\`, RE findings -> `..\docs\superpowers\research\`.

## Hard rules

- Read game memory only - never write to it, never inject. Input exceptions only if the user asks for one, same rules as EpochSDK's Input.Tap (SendInput, focused window, host rate limit)
- No live attach until the anti-cheat check is written up and the user OKs it - see `..\docs\superpowers\research\anti-cheat.md`
- Nothing sensitive in `TorchlightSDK.Api` - offsets, layouts, class names and addresses stay in the host
- `TreatWarningsAsErrors` - 0 warnings
- Never launch `TorchlightSDK.exe` or any windowed program - the user runs the overlay. `TorchlightSDK.exe --check` is headless and fine
- A running overlay locks the normal output - always build / test into a temp dir, never kill the user's process
- Host types that System.Text.Json reads / writes or that show up by name (enum `ToString` / `Parse`, reflection by name) carry `[Obfuscation(Exclude = true, ApplyToMembers = true)]` and get an `InlineData` in `ObfuscationTests` - Release renames everything else (`ObfuscationTests` lands with the release plan)
- Prose (docs, commit messages) never hard-wrapped

## Build and test

```bash
pwsh scripts/build.ps1
```

Builds the host into a temp dir and runs every test project under `tests\`. Single suite:

```bash
T=$(mktemp -d); dotnet build tests/TorchlightSDK.Tests -o "$T" && "$T/TorchlightSDK.Tests.exe"
```

Release (planned, release plan): `pwsh scripts/release.ps1` - tests -> Release publish -> Obfuscar on `TorchlightSDK.dll` -> leak scan -> `TorchlightSDK.exe --check` on the obfuscated build -> allow-list copy into `..\dist`. Keep the Obfuscar `Mapping.txt` it prints to read a user's stack trace from that build.

## Layout

| Path | What |
|---|---|
| `TorchlightSDK.Api` | public API - plugin base, settings nodes, game state records, `Map` / `Terrain` / `WallExtractor`, drawing, fonts, input, logging, widgets (`TorchlightSDK.Ui`), plugin test helpers (`TorchlightSDK.Testing`) |
| `TorchlightSDK` | host - overlay, plugin compiler / loader, settings window, `--check` runner |
| `TorchlightSDK\Reading` | game source seam (`IGameSource`), fake game, reader loop, player smoother - the live reader is the next plan |
| `Plugins` | first-party plugin sources (`Sample`, `Minimap`) |
| `tests` | xunit v3 suites |
| `scripts` | `build.ps1` |

Planned:

| Path | What |
|---|---|
| `scripts\release.ps1` | release plan |
| `tools` | OffsetGen, external SDK dumper |
| `offsets` | `offsets.want` + `layout.want`, OffsetGen encrypts them into the host |

## Rules

Code
- Very minimal comments, keep code itself readable. Names carry the meaning. When using comments, use my writing style
- If we are hand rolling what we expect are common UI elements - build a re-usable helper for it instead, and make sure it's available to plugins too
- KISS/DRY/YAGNI: no function for something called once or twice; one source of truth; no constants for single-use values; no interfaces/generics/abstraction for a single implementation; no config knobs nobody asked for
- A plugin `Settings` class that overrides `Draw()` must write `TorchlightSDK.Draw` to reach the drawing class - inside it the bare `Draw` is the method
- Plugin structure: `Main.cs` (render, loop, thread work), `Settings.cs` (POCO + settings page, big plugins split UI into `SettingsUI.cs`), `defaults.json`
- Once the live reader lands: offsets and game class names live in the want files, never in host code

Testing
- TDD where it's code - readers tested against fake memory, plugins test themselves (`--check`), same as EpochSDK

Git
- Only subagents commit. Never add a Claude co-author line, footer or emoji
- When work is done, suggest one 1-2 line plain-language commit message, the user makes the final commit
