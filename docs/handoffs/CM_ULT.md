# CM-ULT handoff: the Ultimate activation-strike telegraph (combat and movement update, 2026-10-16)

Workstream CM-ULT of Phase A of `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md`: decision 2.1, dossier 2.2.1. Branch `worktree-wf_408a1af8-cf8-3`, fast-forwarded to the plan commit `735296f` before any edit. Worktree `D:\Projects\Fighters Through Time - V3\.claude\worktrees\wf_408a1af8-cf8-3`.

Commits, in order:
- `bf31d8c` — `TelegraphGlyph` moved to `FTT.Combat`, with the one canonical set of class colours;
- `2150776` — the wind-up critical cue row;
- `f358633` — the telegraph in Fighter and on the Level 13 Mirror;
- `9bb496c` — the tests (+7);
- the commit that adds this handoff (the branch head).

**Presentation only.** No sim rule file, Klotho component, snapshot field, hash input, save field, protocol field or `en.csv` key changed. The telegraph is derived from state and never writes it; the hash test below proves it.

**Nobody has played or looked at it.** The evidence is the headless suite, three smoke runs and a mutation check. Pad-session checklist row 20 (§5) is the first human look.

## 1. Shipped

**The rule.** For exactly the A02 activation strike's wind-up (component 321 `PhaseWindup` in Fighter; `UltimateActivationPhase == 1` on the Mirror clone), the caster shows:
- a steady Unblockable-red tint, `TelegraphGlyph.UnblockableColor` = (1, 0.25, 0.2), the existing enemy Unblockable red;
- the Unblockable X glyph, at (0, −120) px from the caster's body origin (its feet), radius 14 px (`TelegraphGlyph.RadiusPixels`).

Both go on the first active frame. Neither pulses, so Reduced Temporal Effects (C01a) changes nothing. A critical-path cue plays once, on the wind-up's first frame.

**`FTT.Combat.TelegraphGlyph`** (`scripts/Combat/TelegraphGlyph.cs`, moved from `scripts/Enemies/` with its `.uid`, `uid://dwg5bvfco652` unchanged). The namespace moved from `FTT.Enemies` to `FTT.Combat`; the shapes, accents and drawing are unchanged. New public static readonly members, the one canonical statement of the class colours (values unchanged):
- `BasicColor` = (1, 0.95, 0.6)
- `GuardCrushColor` = (1, 0.6, 0.15)
- `UnblockableColor` = (1, 0.25, 0.2)

`EnemyAbilityExecutor` reads them; its private `GuardCrushTelegraph` / `UnblockableTelegraph` are deleted, and its public `BasicTelegraph` now forwards to `TelegraphGlyph.BasicColor`.

**`FTT.Combat.UltimateActivationTelegraph`** (new, `scripts/Combat/UltimateActivationTelegraph.cs`), the one statement shared by both modes:
- `GlyphNodeName` = `"UltimateTelegraphGlyph"` (the glyph child under the caster's body);
- `GlyphOffset` = (0, −120);
- `Color` → `TelegraphGlyph.UnblockableColor`;
- `FindGlyph(owner)`, `EnsureGlyph(owner)` (created hidden, once);
- `Apply(owner, glow, windup)`: idempotent and null-glow tolerant; a hide never creates the glyph;
- `IsGlyphShown(owner)`, a test/presentation read.

**`GlowPresentationController`** gained a dedicated telegraph tint flag:
- `SetUltimateTelegraph(bool)` and `HasUltimateTelegraph`;
- `EffectiveTint` = the tint override if active, else the telegraph red if set, else the base tint (then the hero-aura warm bias and the smother, unchanged).

So a `FlashHit` on the armoured caster rides on top of the red, and when the flash times out the sprite returns to red. Clearing the telegraph never cuts a flash short. Nothing else in the arbiter changed.

**Fighter** (`scripts/FighterSim/FighterSimulationDriver.UltimateTelegraph.cs`, a new partial of the driver):
- `SyncUltimateTelegraph(player, playerID, glow)` is called once at the end of `SyncPresentationFeedback`. It reads component 321 through `Simulation.TryGetFighterUltimateActivation` every sync and applies on change. Its cosmetic diff cache is `_presentedUltimateTelegraph`, like the Package 8 A3 caches.
- `SyncUltimateWindupCue(playerID, cues)` is called once in `SyncCoreMechanicCues`. It plays `CoreMechanicCueIDs.UltimateActivationWindup` on the edge into the wind-up (cache `_cueUltimateWindup`).

Both are read-only over sim state. A rollback correction or a rematch that leaves the wind-up hides the telegraph on the next sync, because the state is re-read every frame.

**Mirror** (`scripts/Characters/PlayerController.UltimateTelegraph.cs`, a new partial):
- `SyncUltimateTelegraph()` shows the telegraph only when `UsesFighterUltimateActivation && CurrentState == UsingUltimate && _ultimateActivationPhase == Windup`, so the hero's own Story Ultimate never draws it. It is called at every activation phase edge:
  - the end of `BeginUltimateActivationStrike` (show);
  - the wind-up → active transition in `ProcessUltimateActivationStrike` (hide);
  - `StartUltimateFromActivation` and `EndUltimateActivationStrike` (hide).
- `PlayUltimateWindupCue()` runs at acceptance, through `AudioManager.Instance?.CoreCues?.Play`.
- `IsShowingUltimateTelegraph` is a test/presentation read.
- **The latent defect is fixed.** A `TransitionTo` exit block (directly after `CurrentState = newState;`) calls `OnLeaveUltimateState()` whenever the controller leaves `UsingUltimate` by any edge: a stun, a death, a grab or the natural end. That resets `_ultimateActivationPhase` / `_ultimateActivationFrames` to 0 and hides the telegraph. Before this, a stunned or killed clone kept a stale wind-up phase.

**Cue.** `CoreMechanicCueIDs.UltimateActivationWindup` = `"ultimate_activation_windup"`, in `All` and `Critical`. Its row in `resources/Audio/core_mechanic_cues.tres` is:
- `Critical = true`, so it plays on `CriticalCues`;
- nominal `Bus = "Combat"`, which a Critical row ignores;
- `Stream = placeholder_sfx_hazard.tres`, a silent WAV (the 2026-08-10 audio directive);
- a `DesignNote` for the audio team.

The catalog now has 30 rows; `load_steps` went 42 → 43.

## 2. Files changed

| File | Change | Ownership |
|---|---|---|
| `scripts/Combat/TelegraphGlyph.cs` (+ `.uid`, moved from `scripts/Enemies/`) | namespace, three colour members, doc | owned |
| `scripts/Enemies/EnemyAbilityExecutor.cs` | colour reads, `BasicTelegraph` forwarder, **one `using FTT.Combat;` line** | declared "colour reads only": the `using` line is forced by the namespace move (the moved types are named at eight sites) |
| `scripts/Core/CoreMechanicCueCatalog.cs` | the new ID in `All` and `Critical` | owned |
| `resources/Audio/core_mechanic_cues.tres` | the new row | owned |
| `scripts/Combat/GlowPresentationController.cs` | the dedicated telegraph flag (`SetUltimateTelegraph`, `HasUltimateTelegraph`, one `EffectiveTint` term) | owned ("a dedicated telegraph flag, if chosen") |
| `scripts/Combat/UltimateActivationTelegraph.cs` (+ `.uid`) | new shared helper | new file |
| `scripts/FighterSim/FighterSimulationDriver.UltimateTelegraph.cs` (+ `.uid`) | new partial: the sync and the cue edge | new file (the declared "new glyph attach") |
| `scripts/FighterSim/FighterSimulationDriver.cs` | one call line at the end of `SyncPresentationFeedback`, one in `SyncCoreMechanicCues` | declared regions |
| `scripts/Characters/PlayerController.UltimateTelegraph.cs` (+ `.uid`) | new partial | declared |
| `scripts/Characters/PlayerController.cs` | call lines in `BeginUltimateActivationStrike`, `ProcessUltimateActivationStrike` (the wind-up → active edge), `StartUltimateFromActivation` and `EndUltimateActivationStrike`, and the `TransitionTo` exit block | declared regions |
| `tests/Determinism/FighterPresentationSyncTests.cs` | +3 cases and five private helpers, appended at the end | shared file (§3.1 map) |
| `tests/Unit/StoryUltimateActivationTests.cs` | +3 cases, inserted before the `Run` helper | owned |
| `tests/ContentValidation/CoreMechanicCueCatalogTests.cs` | +1 case, inserted before `IsInstanceValid` | owned |

Not touched: `CoreMechanicCuePresenter.cs` (not needed), every sim rule file, `en.csv`, `EnemyAbilityExecutorTests.cs` (see Deviations).

## 3. Decisions (each a `PROVISIONAL-ULT-TELEGRAPH-LOOK` item unless noted)

1. **The tint lives on a dedicated arbiter flag, not on the tint override.** The dossier's primary text re-asserts the red on the tint-override channel each frame while `!glow.HasTintOverride`. The ownership row allows "a dedicated telegraph flag, if chosen", and I chose it, because:
   - a hit flash then rides on top of the red, and the red returns the moment the flash ends, with no idle frame of base tint between the flash's expiry (the arbiter's `_Process`) and the next physics sync;
   - ending the telegraph never clears or cuts short a flash, which a re-asserted override cannot tell apart from its own red;
   - neither mode needs per-frame tint writes.
2. **The tint is steady, not a flash.** The ruling says "red flash"; the dossier says "steady", with "no pulse, so it is C01a-safe". I built it steady.
3. **Glyph geometry: (0, −120) px, radius 14** (the dossier's numbers). Between the head and the Fighter slot badge:
   - the sprite's top sits about −108 px, so the X's lower tips at −106 clear it;
   - the badge Label's 24 × 24 box spans y −150 … −126, so the X's upper tips at (±14, −134) reach 8 px into that box's bottom corners. The badge's ▲/● is centred in the box, so they should not touch the drawn shape, but nobody has looked.
   - In Story there is no badge, and the factory's name label sits lower (−84 … −66).
4. **Cue row stream and bus.** `placeholder_sfx_hazard.tres` (silent) because the hazard warning is the nearest existing analogue. Nominal bus `Combat`, ignored for a Critical row.
5. **Fighter: apply on change.** The driver applies the telegraph only when the derived wind-up flag changes (`_presentedUltimateTelegraph`), so no node lookup or `NodePath` allocation runs on ordinary frames. The flag itself is re-derived from component 321 every sync.
6. **Mirror: sync at the phase edges, not every frame.** The telegraph is a pure function of (`UsesFighterUltimateActivation`, `CurrentState`, `_ultimateActivationPhase`). The five call sites plus the `TransitionTo` exit block are every writer of those three that can change the result. The `CurrentState == UsingUltimate` term makes the exit hook's hide unconditional.
7. **The exit block sits directly after `CurrentState = newState;`**, before `StateTransitioned` fires, so an observer of the transition already sees phase 0. It covers every exit, including a grab, a throw and `Thrown`, not only the stun and death the dossier names.
8. **The clone's cue plays at acceptance** (`BeginUltimateActivationStrike`), which is the wind-up's first frame. The Fighter cue plays on the presentation-observed edge into the wind-up.

## 4. Deviations from the plan

- **The tint channel** (Decision 1). This is within the ownership row's option, but it is not the dossier's primary design text.
- **`EnemyAbilityExecutorTests` needed no in-place change.** The dossier lists `TheTelegraphGlyphShowsTheClassShape…` as "in place (namespace)". The file already imports `FTT.Combat` and `FTT.Enemies`, so it compiles and passes unchanged. Rewritten cases: none.
- **`EnemyAbilityExecutor.cs` gained a `using FTT.Combat;` line** beyond "colour reads only" (§2 table). Nothing else in the file changed.

## 5. Tests

**New: +7** (declared +7).

`FighterPresentationSyncTests` +3:
- `TheActivationWindupShowsTheTelegraphForExactlyItsWindupWithoutMovingTheHash` — two drivers (the glow arbiter on one, none on the other) run the same inputs. On every wind-up tick the hashes match; P1's glyph shows the Unblockable X in red at `GlyphOffset` on both drivers; `HasUltimateTelegraph` is set and `EffectiveTint` is red; P2 shows nothing. On the first active tick both channels are gone. The count of shown ticks equals 321's `PhaseFrames` at the first wind-up tick.
- `AHitFlashMidWindupDoesNotLeaveTheTelegraphCleared` — a `FlashHit` mid-wind-up wins while it runs. After it times out (the arbiter's `_Process(1.0)`), the tint and the sprite's `Modulate` are red again, and still red after the next tick.
- `TheWindupCuePlaysOnceOnTheCriticalPath` — `LastCueID` is the wind-up cue on the `CriticalCues` bus at the wind-up's first tick, and `CuesPlayed` does not move through the rest of the wind-up.

`StoryUltimateActivationTests` +3:
- `TheFighterVersionShowsTheUnblockableTelegraphForExactlyItsWindup` — the flagged clone shows the glyph and tint for exactly `ActivationWindupFrames` steps, drops both on phase 2, and plays the cue once on the Critical path.
- `AStunnedOrKilledCloneDropsTheTelegraphAndItsActivationPhase` — `ApplyStun` mid-wind-up gives phase 0 and no telegraph, and it stays that way for 10 more steps. `KillPlayerNonHit` mid-wind-up gives the same.
- `APlayersOwnStoryUltimateNeverCreatesATelegraphGlyph` — the unflagged hero's Ultimate casts, and over 240 steps no glyph node ever exists and the tint flag stays off.

`CoreMechanicCueCatalogTests` +1:
- `TheUltimateActivationWindupRowIsACriticalSilentCue` — the row exists, is Critical, resolves to `CriticalCues`, has a silent stream, is in `All` and `Critical`, and `Play` routes to `CriticalCues`.

The harnesses press the Ultimate once and step until component 321 enters the wind-up (at most 10 frames), so a chord-leniency delay upstream (CM-INPUT) cannot break them.

**Rewritten cases:** none.

**Shared test files touched:** `FighterPresentationSyncTests`. I added methods and private helpers at the end (`CreateTelegraphDriver`, `StepTelegraphDrivers`, `PressUltimateUntilWindup`, `ActivationPhase`, `TelegraphShown`). No existing case or helper changed, so CM-SIM's in-place rewrite of `AcceptedFighterSpecialHoldsTheAuthoredAbilityPose` and CM-HIT's later +1 union-merge cleanly.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, with leftovers drained only while holding it):

| Run | Result |
|---|---|
| `dotnet build FightersThroughTime.csproj --nologo` | succeeded; only the three expected warnings (CS8632, `KLSG_ECS004` on `FighterTuningComponent`, CS0649) |
| Filtered: `FighterPresentationSync`, `StoryUltimateActivation`, `CoreMechanicCueCatalog`, `EnemyAbilityExecutor`, `MirrorParadox` | `Passed: 77, Total: 77`. That is 11 + 6 + 6 + 20 + 31 = 74 cases in the five suites, plus three cases in other suites whose method names contain `MirrorParadox` (`ArenaGeometryContentTests`, `BossBarPhaseNotchTests`, `ScriptTypedEmptyArrayTests`). |
| Mutation check (three call lines disabled: the driver's telegraph sync, the driver's cue edge, the `TransitionTo` exit block) | `Failed: 4, Passed: 13, Total: 17`. Exactly the three Fighter cases and the stun/kill case failed. The files were restored and the build re-run before committing. |
| `--headless --import` (worktree seed) | done |
| `--headless --quit` | exit 0 |
| **Full suite** | **`Passed! - Failed: 0, Passed: 3235, Skipped: 0, Total: 3235`, 1 m 31 s.** 3228 + 7 = 3235 exactly. The `[TestCase]` attribute count under `tests/` is 3235. |

## 6. Measurements

- **Wind-up length.** It equals the authored `ActivationWindupFrames` (20 on Einstein) in both modes. Fighter shows it on 321's `PhaseFrames` ticks from the acceptance tick; the Mirror on `ActivationWindupFrames` steps from the press step.
- **Smoke runs** (`--headless --fixed-fps 60 … --quit-after 300`, holding the lock):

  | Scene | Exit | Leaked ObjectDB | Resources in use | Script errors |
  |---|---|---|---|---|
  | `res://scenes/arenas/TestArena.tscn` | 0 | 6 | 2 | none |
  | `res://scenes/campaign/Level_13_ChronalVoid.tscn` | 0 | 23 | 1 | none |
  | `res://scenes/campaign/Level_12_Lunar.tscn` (control) | 0 | 34 | 4 | none |

  Neither smoke reaches an activation wind-up in 300 frames (the arena's countdown is 180; the Mirror stands at Level 13's end), so the new paths only load there. The exit-time counts are the known nondeterministic noise (CLAUDE.md), and the control level shows more.

## 7. Not delivered / open

- **Nobody has seen it.** Whether the red plus the X reads on P2's red slot edge, or on the Mirror at the 1.5 Story zoom, is §5 row 20 (`PROVISIONAL-ULT-TELEGRAPH-LOOK`).
- **The cue is silence** until Package 10.
- **LAN.** The Fighter cue is an edge observed by presentation. Under a rollback correction across the wind-up's first tick it could play twice or not at all. LAN is de-scoped to Package 7; the telegraph itself is re-derived every sync and is not affected.
- **No HUD or off-screen element.** A caster outside the camera's view shows nothing. The Fighter camera frames both fighters, but the Story zoom does not always frame the Mirror clone (AGENTS.md: it starts off-screen for the reveal's first ~0.6 s).
- **The slot-badge corners** (Decision 3).

## 8. For the closeout

**Ledger (`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`).**
- **Close `DEFER-ULT-TELEGRAPH`.** Built: `FTT.Combat.UltimateActivationTelegraph`, the arbiter's `SetUltimateTelegraph` flag, the driver's component-321 sync and the Mirror's phase-edge sync, plus the `ultimate_activation_windup` Critical cue. Pinned by `FighterPresentationSyncTests` (+3), `StoryUltimateActivationTests` (+3) and `CoreMechanicCueCatalogTests` (+1); the hash case is the "presentation-only, no sim state touched" acceptance.
- **New `PROVISIONAL-ULT-TELEGRAPH-LOOK`**, owner Unassigned. Value: Decisions 1–4 and 8 (steady tint on a dedicated arbiter flag, (0, −120) px, radius 14, the `placeholder_sfx_hazard` stream, nominal `Combat` bus, the cue at the wind-up's first frame). Acceptance: §5 row 20 says the red plus X reads on both slots and on the Mirror, and the audio team replaces the silent stream.
- **Suggested new closed row `FIX-MIRROR-ULT-PHASE-EXIT`:** leaving `UsingUltimate` by a stun or death left the clone's activation phase set. It is fixed by the `TransitionTo` exit block and pinned by `AStunnedOrKilledCloneDropsTheTelegraphAndItsActivationPhase`. The plan names this defect in dossier 2.2.1 but not a row; the orchestrator may fold it into the `DEFER-ULT-TELEGRAPH` closure instead.

**Design master and mirror (`design-godot.md` line numbers at `e2c655a`, unchanged at `735296f`).** None of these is a `DOC-MASTER-UNBOUND-BINDING` line.
- **:1275** (A02). It already states the intent ("Its wind-up carries the red Ultimate-class telegraph and X glyph"). Optionally add "a steady red tint (no pulse) and the X, for exactly the wind-up, in Fighter and on the Mirror clone". Not strictly owed.
- **§8 "7. Core Mechanic Cues" (:3721–3740).** Add a bullet, for example after "Ultimate Ready Chime" (:3734): "**Ultimate Activation Wind-up:** a rising unblockable warning once on the first frame of the A02 activation strike's 20-frame wind-up, beside the red tint and the X glyph. Critical Cues path." The catalog test ties the Critical rows to `CoreMechanicCueIDs.Critical`, so this line is owed in the same closeout.
- **:4939** (the asset list's `Core Mechanic Cues (H04)` entry). Add "Ultimate activation wind-up" to the list.

**Contracts.** None owed. `COMFORT_SETTINGS.md` :24 (attack-class telegraphs keep their Unblockable glyphs) and :75 (attack-class warnings on the Critical Cues path) already cover it in both copies, and `MIRROR_PARADOX.md` :33 ("telegraphs still govern each use") is consistent.

**AGENTS.md.**
- **:287** (the A02 bullet). Replace "no telegraph is drawn (`DEFER-ULT-TELEGRAPH`)" with: the wind-up shows a steady Unblockable-red tint (the glow arbiter's `SetUltimateTelegraph` flag, under a hit flash) and the Unblockable X at (0, −120) for exactly the wind-up, in Fighter (`FighterSimulationDriver.UltimateTelegraph.cs`, from component 321) and on the Mirror clone (`PlayerController.UltimateTelegraph.cs`), through `FTT.Combat.UltimateActivationTelegraph`, plus the Critical `ultimate_activation_windup` cue; presentation only.
- **:564** (Package 13 "not delivered → Ultimates"). Drop "No red telegraph or X glyph on the activation wind-up (`DEFER-ULT-TELEGRAPH`);".
- **Optional.** The V7.3 remaining-story bullet's `TelegraphGlyph` is now `FTT.Combat.TelegraphGlyph` (`scripts/Combat/`), holding the three class colours. A row in the scene-scoped systems table for `UltimateActivationTelegraph` would also help.

**CLAUDE.md.** Optional: add `scripts/Combat/UltimateActivationTelegraph.cs` to the "Ultimate / activation strike" row of "Where to start".

**Baseline.** +7, so 3228 → 3235 in this worktree. The plan's Phase A running total is 3256 → **3263** at the CM-ULT merge.
