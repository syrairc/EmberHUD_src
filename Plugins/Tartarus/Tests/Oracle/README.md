# Tartarus oracle data

Recorded from the reference simulator (clone `E:\EmberHUD\RE\tartarus-simulator-new`, commit `bc528594`, 2026-10-18, `js\engine.js` and `js\solver.js`, never edited) by the scripts in `E:\EmberHUD\RE\tartarus-sim-oracle\` (not committed, not shipped). The rules in plain words are in `E:\EmberHUD\docs\superpowers\research\tartarus-sim-rules.md`. Every file here is plain JSON, one scenario or frequency per line.

Re-run: `cd E:\EmberHUD\RE\tartarus-sim-oracle`, then `node record.js <this folder>` (about 3 min, writes every file but `strategy.json`, env `REF` = reference root, default `E:/EmberHUD/RE/tartarus-simulator-new`, `NOSOLVER=1` skips the solver boards), `node solver-only.js <this folder>` (about 8 min, `ROLLOUTS=1000`, rewrites `solver.json`), `node strategy.js <out.json> 300` for `strategy.json`. A scenario is kept only when 4 different seeds give the identical result board.

Changes of the 2026-10-18 re-record against the old reference (`4416defb`): the scripts take the reference root from `REF`; the reference has no `eventChance` any more, only `eventStartChance` / `eventMorningChance` / `eventWalkChance` / `eventActionChance` (72 / 5 / 9 / 4), so the baseline sets all four to 0 and the chance runs set the one they measure. New scenarios: second meals at every rarity, Instigate (1 event, 2 on a board with none, on a locked door or beside an inmate), Usurpation putting the next one on a free cell, Extra Portion, Filthy Claws, Blood-Stained Trap and its Hunt (`action.kind` `hunt`, `used.hunt`), Behind Cell Doors, vault cap 10 (`vault` setup), Opulence, the four event chances. Dropped: the 5 % event behind a re-sealed cell (events now turn up on any free locked cell), "Mammoth Rare may not eat a Wolf" and the Instigate-on-two-cells scenarios (rules changed), the Casket / Brand scenarios were re-pinned to a one-meal beast (Lion and Mammoth now have two). Not recorded: Replace after a Hunt - the reference `swap()` does not check the Hunt, only its move list (`canSwapNow`) does, so the API call would succeed; we refuse it through `Legal()`.

Files: `doors`, `consume`, `swap`, `office`, `events`, `markers`, `tools`, `objectives` each hold `{ "scenarios": [...], "frequencies": [...] }`. `solver.json` is described at the bottom.

## Board

```
{
  "position": 0-15,                  warden cell, 0 = the office
  "day": 1-maxDay, "maxDay": 20,
  "phase": "patrol" | "manage" | "office" | "event" | "ended",
  "cells": [16 x Cell],              always all 16, index = i
  "chromatics": 0,                   Chromatics vaulted (the reference calls them Rainbows)
  "tools": ["saw", ...],             tool ids in pick order
  optional (absent = default):
  "used": { "swap": 0-1, "devour": 0-1, "hunt": 1 },        today's flags, absent = all 0 (hunt only when set)
  "objective": { "id": "king"|"walk"|"consume"|"incident"|"doors", "progress": int },   absent = no objective
  "milestone": true,                 objective already paid
  "startEvent": "cagefight" | "ironlaw" | "tyranny" | "opulence",
  "chain": true,                     Chain reaction armed for the next patrol
  "ratway": true,                    Sewer Ratway move pending (phase "patrol", destinations = every cell but the office)
  "eventPool": ["chain", ...],       only when it differs from the default pool (see below)
  "prey": [ { "lion": cell, "prey": cell, "rounds": int, "next": day } ],
  "pendingEvent": { "kind": "execution", "cell": 4 },   phase "event"
  "pending": [ { "kind": "tool"|"gift", "options": ["saw","keys","rope"] | null, "count": 3 } ],   choices waiting, first is front
  "chaining": cell,                  a consume chain waits for the next pick, value = the eater's cell
  "destinations": [3, 5, 6]          today's doors; written in `before` boards of phase "patrol", in `after` only for "ratway"
}
```

Cell:

```
{ "i": 0-15,
  "open": bool,                      true = an unlocked cell holding an inmate; the office and locked cells are false
  "beast": "mammoth"|"lion"|"wolf"|"rat"|"none",   "none" for locked cells and the office
  "tier": 0-5 or -1,                 0 Normal 1 Magic 2 Rare 3 Legendary 4 Supreme 5 Rainbow (never on the board), -1 = nobody
  optional:
  "hidden": { "beast", "tier" },     locked cell only: who is really behind the door (the oracle's roll). Setup input; absent = not rolled yet
  "markers": {
    "tusk": true,                    open cell
    "scratch": true,                 locked cell, the occupant's rarity is known and a Wolf can eat through the door
    "nest": 3,                       locked cell, rat nest, value = the day it hatches
    "event": "chain",                special event kind waiting behind the door (locked cell) or beside the inmate (open cell); "any" in an `after` board = a kind was rolled, ignore which
    "seen": true,                    locked cell, the warden has walked past the door, so the event shows
    "known": 1 | 2,                  locked cell, 1 = rarity revealed (Crow Whispers, Hunt, scratch), 2 = beast and rarity (Warden's Scales)
    "atLeast": 2                     locked cell, after a Hunt: not below this tier
  } }
```

The office is always cell 0, never holds anyone and never has markers. A locked cell that is not listed with `hidden` has an occupant nobody has rolled; a scenario never opens one unless it is a frequency.

Event ids: `chain execution trickster appetite convict crow freedom usurp1 usurp2 usurp3 usurp4 ratway hunt portion`. Default pool (order matters for `eventPool`): `chain execution trickster appetite convict crow freedom usurp1 ratway hunt portion`.

Tool ids (31): `revenge muzzle saw keys rope fire ironbox skewer hook pestle scales tongs guillotine axe shackles collar forged dogtag cage blacklist shortchain maiden whip ravenous scapegoat stake coin crown maul claws trap`. Gift ids (Warden's Treasure): `ring horn wine brand`.

An `after` board lists only what the rules decide. Cells that were locked and stay locked keep their `hidden` and markers; a cell that is eaten, removed or leaves for the vault becomes a locked cell with no `hidden` and no markers (nothing new is rolled for it).

## Scenario

```
{ "name": string,
  "before": Board, "after": Board,
  "action": { "kind", "cell", "param", "id"?, "then"?: [action] },
  "random": false,
  optional: "params", "result", "forced", "branch" }
```

`action.kind`, with `cell` and `param` (-1 = unused), applied in order, then[] after the first only while the previous one was allowed:

| kind | meaning | cell | param | id |
|---|---|---|---|---|
| `patrol` | walk to a door | destination | | |
| `consume` | the warden's inmate eats | target cell (a locked cell for a scratched door) | eater cell if not the warden's cell | |
| `chain` | next meal of a waiting chain | target cell | | |
| `stopChain` | stop the waiting chain (the game's Cancel) | | | |
| `swap` | Replace the warden's inmate | | | |
| `hunt` | Blood-Stained Trap: the Lion in the warden's cell marks a prey | target cell | | |
| `office` | Instigate or Bestow | | | `instigate` / `bestow` |
| `event` | accept or skip the pending event | | 1 accept, 0 skip | |
| `endDay` | end the day (next morning is rolled, see below) | | | |
| `choose` | pick a pending tool or gift | | | tool or gift id |
| `listConsume` | read-only: which cells could the cell's inmate eat | eater cell (-1 = warden's) | | |

Expectation, all of them must hold:

- `result.ok` absent means the action was allowed and `after` is the state to expect. `result.ok = false` means the action must be refused and `after` equals `before`; with a `then` list the steps before the refused one stay applied and `after` is the board after those (one scenario: a consume, then a chain bite that is refused).
- `result.targets` (`listConsume`): the legal meal cells, ascending. `result.scratch`: the subset reached through a scratched locked door. `result.chainTargets`: the meals offered while a chain waits (the board then has `chaining`).
- `endDay` runs the morning of the next day. The morning's door draw is random, so `destinations` is never in `after`; everything else the morning does (prey rises, nests hatch, Tongs, Iron Box, Ravenous Chains, Coin of Fate) is.
- `after.pending` with `"options": null, "count": 3` means three distinct tools the player does not hold, drawn at random; check the count only.
- `random: true` marks a chance branch: `branch` names which way the roll went and every other draw has one possible result on that board. Make the roll go that way (`hit` / `miss`, `gain3` = 3 of 5 tries, `rises1`, `gone4` = cell 4 is the one removed, `hit6` / `hit9` = which cell the Casket opened) and compare. The odds of each branch are in the matching `frequencies` entry (same name plus `- how often`).
- `forced`: the draw the setup pinned for a Replace (`tier`, `beast`). `params` is the same pin, see next.

### params

Every scenario and frequency was recorded with the reference defaults plus the four event chances at 0, `startTools 0` and `drillDays 20`, plus the entries in its own `params` object (the reference's names):

| param | default | meaning |
|---|---|---|
| `eventStartChance` / `eventMorningChance` / `eventWalkChance` / `eventActionChance` | 0 here (reference 72 / 5 / 9 / 4) | % chance a seen event turns up on a random free locked cell at the start of a run / each morning / after the patrol's walk / after a Consume, Replace, Hunt or special event |
| `beastWeights` | [25,25,25,25] mammoth lion wolf rat | beast of every new inmate and of a consume's re-roll. One-hot = that draw is pinned to that beast |
| `tierWeights` | [40,38,12,8,2] | rarity behind a door (and for High-Risk Convict, tiers 3-4 only). One-hot = pinned |
| `swapWeights` | [52,32,9,6,1.5] | rarity of a Replace. One-hot = pinned |
| `chainStop` | true | a chain waits for the player's pick after every bite; false = single-meal bites are taken by themselves |
| `scratchStacksBase` | true | scratch bite gains 2 + 1, false = 2 |
| `nestTier` | 2 | tier of the hatched Rat |
| others used | | `swapsPerDay 1`, `doubleEatTier 0`, `ivoryCount 4`, `ivoryIncludesSwapped true`, `preyRounds 3`, `nestDays 2`, `executionSameTier false`, `executionRerolls false`, `patrolRange 6`, `destinations 3`, `reusableCells true`, `rewards true` |

## Frequency

```
{ "name", "measure": string, "before": Board | null, "action", "trials": 2000-6000, "outcomes": { key: count }, optional "params" }
```

`measure` tells what each key means (`tier:2`, `beast:lion`, `dest:5`, `count:3`, `rose:7`, `offer:saw`, `hit`/`miss`, `gain:3`, `event:none`, `kind:chain` ...). A trial builds `before`, applies `action` once with a fresh seed, and adds one to every key that trial produced (a trial can add several, e.g. one `tier:` and one `beast:`). `before: null` with `action.kind = "newRun"` is a draw at the start of a run (objective, starting pick of three tools) with no board. Compare shares, not counts: each key's share of `trials` within 3 standard errors (sqrt(p(1-p)/n)) of the recorded share.

## solver.json

```
{ "rollouts": 1000, "seeds": [1, 2], "params": {...}, "strategy": { "runs": 500, "mean": 2.14, "se": 0.06, "sd", "hist", "days": 20 }, "boards": [ {
  "name", "board": Board (with destinations when phase is patrol),
  "runs": [ { "seed", "rollouts", "best": action, "bestLabel", "margin", "options": [ { "label", "action", "mean", "se", "gapSe", "n", "policy" } ] }, x2 ],
  "bestAction": action, "bestValue": number, "agree": bool, "decisive": bool } ] }
```

`mean` is the average Chromatics vaulted by the end of the run when that option is played first and the reference strategy plays the rest, with everything hidden re-rolled each playout (locked cells, events behind unseen doors). `options` are sorted best first. `policy` marks the option the reference's own one-shot strategy would pick. `margin` = (best - second) / paired standard error of that difference. `agree` = the two seeds picked the same best option; `decisive` = they agree and both margins are at least 3, so only `decisive` boards may assert the best option; for the others assert that our best option is within the reference's `se`-scaled noise of its best (value gap under 3 paired standard errors, or under 0.05 Chromatics). Boards use the `params` in this file (the reference's four event chances). Two options can be symmetric (same species and rarity in two cells) and then only the value, not the cell, is comparable.

`strategy` is the reference strategy played start to finish on 500 seeded runs (seeds `S0`..`S499`, the reference's event chances, `startTools 0`, 20 days, rewards on): mean Chromatics, its standard error, `hist` = runs per Chromatic count. Recorded by `E:\EmberHUD\RE\tartarus-sim-oracle\solver-only.js` (not committed, rewrites only `solver.json`: the 36 boards of `g5.js` plus this block). Our `Strategy` is not the reference, its mean may sit a little under (`StrategyRunsAtLeastAsWellAsTheReferenceNumbers` allows a tolerance of about 2.7 standard errors).

## strategy.json

```
{ "reference": "bc52859", "note", "boards": [ { "name", "board": Board (with destinations when phase is patrol), "action": action, "label", "why" } ] }
```

The reference strategy's single decision on each board (`SOLVER.instant`, no playouts, profile `rewards`), with its label and reason as the reference words them. The first board is the live board of 2026-10-18 (day 6, warden on the Rare Rat in cell 6, doors 8, 9 and 12, reference pick cell 12), the second a Magic inmate with no meal (Replace). The rest are random scene-editor boards over every phase (patrol, manage, chain, office, event, tool and gift picks). Recorded by `E:\EmberHUD\RE\tartarus-sim-oracle\strategy.js` (`node strategy.js <out.json> 300`, env `REF` = reference root). `StrategyTests` replays each into a `Run` and asks our `Strategy.Choose` (Rng 1) for the same move. Gift picks score the Thumb Ring and the Brass Horn on 3 random trials in the reference too, so a near tie there can come out either way.
