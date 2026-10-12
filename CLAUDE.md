# Aember - code

Plugin SDK for a Torchlight: Infinite overlay. `Aember.Api` is the public API plugins compile against (never obfuscated, nothing sensitive in it). `Aember` is the host exe (everything `internal`, obfuscated in Release). Plugins are C# source in `Plugins\<Name>\`, compiled by the host with Roslyn. Same shape as EpochSDK (`E:\EpochSDK\src`, read-only reference) - host / plugin / UI layers get copied over and renamed, the game layer (UE4 reading) is new.

Phase 1 scaffold and the phase 2 Minimap plugin run on a fake game, the phase 3 live reader reads the real game read-only. `Aember.exe --check` (headless, host JIT + every plugin's tests) is the only run the agent does, the user runs `Aember.exe` for the real game (no args = live, `--live` still accepted) and `Aember.exe --fake` for the overlay with synthetic players, monsters and terrain. Several live fields are stubbed until the probe (`..\HANDOFF.md`). Plan -> `..\docs\superpowers\plans\`, RE findings -> `..\docs\superpowers\research\`.

## Hard rules

- Read game memory only - never write to it, never inject. Input exceptions only if the user asks for one, same rules as EpochSDK's Input.Tap (SendInput, focused window, host rate limit)
- No live attach until the user OKs the "Live read plan" in `..\docs\superpowers\research\anti-cheat.md`. The user OK'd live reads - no args = live, so the agent never launches the exe without `--check` or `--fake`. Game calls are `OpenProcess` `0x1010` and `ReadProcessMemory` (plus `QueryFullProcessImageNameW`, `NtQueryInformationProcess`, `GetExitCodeProcess` on that one handle) - never `Process.MainModule` / `HasExited` (extra handles)
- Nothing sensitive in `Aember.Api` - offsets, layouts, class names and addresses stay in the host
- `TreatWarningsAsErrors` - 0 warnings
- Never launch `Aember.exe` or any windowed program - the user runs the overlay. `Aember.exe --check` is headless and fine
- Never run `AemberLauncher.exe` or `AemberSetup.exe` - the launcher starts the game, Setup is checked by eye by the user
- A running overlay locks the normal output - always build / test into a temp dir, never kill the user's process
- Host types that System.Text.Json reads / writes or that show up by name (enum `ToString` / `Parse`, reflection by name) carry `[Obfuscation(Exclude = true, ApplyToMembers = true)]` and get an `InlineData` in `ObfuscationTests` - Release renames everything else (`ObfuscationTests` lands with the release plan)
- Prose (docs, commit messages) never hard-wrapped

## Build and test

```bash
pwsh scripts/build.ps1
```

Builds the host into a temp dir and runs every test project under `tests\`. Single suite:

```bash
T=$(mktemp -d); dotnet build tests/Aember.Tests -o "$T" && "$T/Aember.Tests.exe"
```

`Aember.Setup.Tests` has real-pak tests gated on `TLI_GAME_DIR` / `TLI_ICON_ASSETS` (skipped when unset). `native\ooz\build.cmd` rebuilds `ooz.dll` (VS 2022 C++ tools), only needed when the ooz source changes.

Release: `pwsh scripts/release.ps1` - tests -> Release publish (framework-dependent, win-x64) -> plugins / fonts / themes / licences staged, plus the launcher, Setup and `ooz.dll` published alongside the host (the icon art is no longer staged - each user extracts icons with Setup from their own install) -> `Aember.exe --check` -> allow-list copy into `..\dist` (local only, never pushed, the user runs it from there - the wipe keeps `Config` / `Cache` / `Logs` / `imgui.ini` / `GameIcons` and refuses while anything runs from `dist`). `pwsh scripts/release.tests.ps1` tests it (stub dotnet, run by hand). Obfuscar on `Aember.dll` and the leak scan come with the full release plan - keep the Obfuscar `Mapping.txt` then to read a user's stack trace.

## Layout

| Path | What |
|---|---|
| `Aember.Api` | public API - plugin base, settings nodes, game state records, `Camera`, `Map` / `Terrain` / `WallExtractor`, raw reads (`Memory`) and the UE reflection walker (`Ue`, layout handed over by the host), drawing, textures, fonts, input, logging, widgets (`Aember.Ui`), plugin test helpers (`Aember.Testing`, incl. `TestMemory` / `TestUe`) |
| `Aember` | host - overlay, plugin compiler / loader, settings window, `--check` runner |
| `Aember\Reading` | game source seam (`IGameSource`), fake game, reader loop, player smoother, live reader (`LogicWorldReader`, `FightMapReader`, `MonsterRarityReader`, `MinimapReader`, `TerrainReader`, `LiveReader`, `LiveGameSource`) |
| `Aember\Memory` | `IReadBackend` + `ReadProcessMemory` backend, read extensions, `GameProcess` (attach, exe size check) |
| `Aember.Launcher` | `AemberLauncher.exe` - launcher.json, game detection, starts game (runas / RunAsInvoker), Aember and other programs |
| `Aember.Setup` | `AemberSetup.exe` - ImGui setup window, pak reader, game icon export into `GameIcons\` (links host overlay / theme / offsets sources) |
| `native\ooz` | powzix/ooz source (GPLv3), `build.cmd` (MSVC, /MT), built `ooz.dll` checked in |
| `Aember\Offsets` | `Offsets.*` (generated `Offsets.g.cs` + sealed `offsets.bin`), regenerate with `pwsh scripts/gen-offsets.ps1` |
| `offsets` | `layout.want` - every offset, limit and game class / parameter name (quoted strings) the live reader uses, never literals in host code |
| `tools\OffsetGen` | want file -> AES-GCM sealed table + `Offsets.g.cs` |
| `Plugins` | first-party plugin sources (`Sample`, `Minimap`, `Inspector`) |
| `tests` | xunit v3 suites |
| `scripts` | `build.ps1`, `gen-offsets.ps1`, `live-build.ps1`, `release.ps1`, `release.tests.ps1` |

Planned:

| Path | What |
|---|---|
| `scripts\release.ps1` | Obfuscar + leak scan (full release plan) |
| `tools` | external SDK dumper |
| `offsets\offsets.want` | fields from an SDK dump, once the dumper exists |

## Rules

Code
- Very minimal comments, keep code itself readable. Names carry the meaning. When using comments, use my writing style
- If we are hand rolling what we expect are common UI elements - build a re-usable helper for it instead, and make sure it's available to plugins too
- KISS/DRY/YAGNI: no function for something called once or twice; one source of truth; no constants for single-use values; no interfaces/generics/abstraction for a single implementation; no config knobs nobody asked for
- A plugin `Settings` class that overrides `Draw()` must write `Aember.Draw` to reach the drawing class - inside it the bare `Draw` is the method
- Plugin structure: `Main.cs` (render, loop, thread work), `Settings.cs` (POCO + settings page, big plugins split UI into `SettingsUI.cs`), `defaults.json`
- Offsets and game class names live in `offsets\*.want`, never in host code. Garbage reads are normal while an area loads - reject, skip, never throw

Testing
- TDD where it's code - readers tested against fake memory, plugins test themselves (`--check`), same as EpochSDK

Git
- Only subagents commit. Never add a Claude co-author line, footer or emoji
- When work is done, suggest one 1-2 line plain-language commit message, the user makes the final commit
