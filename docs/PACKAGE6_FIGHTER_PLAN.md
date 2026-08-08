# Package 6 — Ten-stage local Fighter Mode completion: implementation plan

**Status:** Authored 2026-08-08 from a four-way reconnaissance of the stage system, match flow,
CPU/rollback infrastructure, and content pipeline. Baseline at authoring time: 705 passing tests,
Package 5 closed, one production Fighter stage (Florence Workshop), nine catalog entries silently
aliasing `TestArena.tscn`.

**Authority order:** explicit user instruction > `design-godot.md` (§10 stages/hazards/orbs/CPU at
lines ~3085–3203, §11 match flow at ~3205–3273, respawn platform at ~1565–1571) > this plan >
existing code — EXCEPT numbers: an authored `.tres` resource beats prose everywhere it exists.
`FighterStageGeometry` is deliberately code-authored (see §2.2); its dossier values in §4 are locked
by this plan and pinned by tests.

**Read §9 (Deviations) before touching anything Fighter-side.** Append-only; every agent logs
material deviations there in the Package 5 format (`**AN: one-sentence claim.** reasoning + pinning test`).

---

## 1. Scope (from `IMPLEMENTATION_PLAN.md` Package 6)

### Stage production
1. Nine additional independently authored stage scenes — all ten catalog entries get distinct scene
   paths, platform layouts, spawn points, camera bounds, Orb points, and hazard anchors.
2. Ten era-specific deterministic hazard implementations with Off/Low/Medium/High frequency,
   warning/active/recovery phases, snapshots, hashes, and rollback tests.
3. Distinct placeholder parallax layers, palette, lighting treatment, preview image, music set,
   hazard VFX/SFX, and pool configuration per stage.
4. Validated boundaries, blast zone, one-way platforms, spawn safety, camera framing, and
   worst-case performance for every stage.

### Match and CPU completion
5. Respawn-platform presentation: five-second dissolve, input lock, drop, invulnerability.
6. KO presentation: hit-freeze, slow motion, spotlight, KO stamp, winner pose, audio/fanfare,
   results transitions — without altering deterministic match state.
7. Local controller-disconnect forced pause, reconnect assignment, safe menu-exit.
8. CPU Orb pursuit, hazard avoidance, off-stage recovery, jumps, movement ability, and Special 2
   behavior at all difficulty bands.
9. Every rule setting persists through rematch; every match can end by HP KO, stock exhaustion,
   bottom fall, timer decision, or true draw.
10. Rollback-readiness gate across all kits/stages: complete snapshots and hashes, delayed-input
    convergence, bounded history, worst-case seven-frame resimulation within the frame budget.

### Explicitly OUT of scope
- Production transport / Steam / LAN discovery / online UI (Package 7 — gated on item 10 passing).
- Production art, music, cinematic ultimate/KO art passes (Packages 8/10). Everything visual/audio
  here is contract-conformant placeholder.
- Training Mode, 4-player, replays, detailed stat sheets (deferred by the plan of record).
- Story Mode content. Nothing in this package may touch `scripts/Environment/Story*`,
  campaign scenes, or Story tuning resources.

---

## 2. Standing decisions (all agents — read before writing code)

1. **Stage naming reconciliation.** The catalog / `GlobalSaveData.InitialStageIDs` /
   `localization/en.csv` spelling wins: **`vesuvius_caldera`** and **`alexandria_chambers`**.
   The manifest rows `pompeii_caldera` → `vesuvius_caldera` (path `FighterStage_Vesuvius.tscn`) and
   `egypt_chambers` → `alexandria_chambers` (path `FighterStage_Alexandria.tscn`), and the AudioSet
   rows `audio_stage_pompeii`/`audio_stage_egypt` and VisualSet rows
   `visual_stage_pompeii`/`visual_stage_egypt` are renamed to the vesuvius/alexandria spelling.
   Rationale: `UnlockedStages` is persisted save data — renaming catalog IDs would orphan existing
   saves; the manifest has no other consumer of these spellings. A1 applies the rename and adds a
   manifest↔catalog StageID cross-check test (Package 4 §3.7 precedent) so drift cannot recur.
2. **Fixed-point geometry stays code-authored** in
   `scripts/FighterSim/FighterStageGeometry.cs` (nine new static properties + `ForStage` arms).
   It is deliberately outside snapshots and outside `.tres` (deterministic, engine-free). The
   catalog `.tres` remains routing/presentation data only. The §4 dossier tables are the canonical
   numbers; tests pin them.
3. **Determinism boundary for new match flow.** Countdown and the respawn platform are
   *deterministic simulation state* (new component fields + system logic at 60 Hz; they will be
   part of every snapshot and hash). KO cinematics (hit-freeze, slow-mo, spotlight, stamp, fanfare)
   are *presentation only*: `FighterSimulationDriver` paces or defers `Simulation.Advance` calls on
   its own presentation clock. **Never** `Engine.TimeScale`, never a sim-side "cinematic" state,
   never Godot state feeding back into `scripts/FighterSim/`.
4. **Hazard–block interaction follows the design** (§10 "Stage Hazard Timing"): a hazard damage
   tick is treated as a basic attack — an active block absorbs the tick at the cost of one block
   charge. This is a behavior change from the current `ApplyEnvironmentHit` path; A1 implements it
   once in the shared hazard code and pins it with a test. Ultimate-class bypass rules are unchanged.
5. **The CPU stays an input source, outside the snapshot** (like a human). Its RNG/schedule are not
   sim state. Its seed becomes *derived from the match seed* (replacing the hardcoded `2026` in
   `FighterSimulationDriver`) so a rematch with the same settings is reproducible. Any observation
   field added for the new behaviors must be filled in **both** `FighterCpuController.Observe` and
   `MirrorParadoxDecisionAdapter.Observe` (Story sentinels where a concept doesn't exist there) —
   `tests/unit/MirrorParadoxTests.cs` pins frame-identical parity and must stay green.
6. **MatchSettings persistence stays session-scoped.** "Persists through rematch" is satisfied by
   the autoload `GameManager.CurrentSession`; we are NOT adding MatchSettings to the save schema in
   this package (no schema bump). The rematch destination bug (hardcoded `TestArena.tscn`) is fixed
   to route through the catalog's `ScenePath` for `SelectedStageID`.
7. **Shared-file conflict policy.** Phase B stage agents do NOT touch:
   `resources/FighterStages/stage_catalog.tres`, `resources/Content/content_manifest.csv`,
   `resources/Pools/scene_pool_catalog.tres`, `tests/integration/SceneSmokeTests.cs`,
   `localization/en.csv`, `scripts/FighterSim/**`, `scripts/Core/**`, shared UI scripts, the
   Florence scene, or other stages' files. Catalog `ScenePath` wiring, pool-catalog rows, smoke
   rows, and manifest flips are applied at integration (§7). In Phase A, file ownership is split
   per §3; the two known overlaps (`FighterSimulationComponents.cs` for A1/A2,
   `FighterSimulationDriver.cs` for A2/A3) must be kept additive and localized; merge order
   A1→A2→A3→A4 with the orchestrator resolving unions.
8. **Native-crash hygiene (mandatory; violating it corrupts the whole suite and the crash never
   points at its cause):**
   - Never call `Dispose()` on a Godot `Resource`. `Free()` the node; leave resources alone.
   - Load authored `.tres` data through `FTT.Core.AuthoredResources.Load<T>()`, never
     `ResourceLoader.Load`/`GD.Load` (scenes, textures, audio, pooled objects must NOT go through it).
   - Never author an empty Script-typed array in a `.tres`/`.tscn` (`Array[ExtResource("N")]([])`)
     — omit the property instead. `ScriptTypedEmptyArrayTests` guards this.
   - Whoever sets `SceneTree.Paused` releases it in `_ExitTree`; tests restore it in `finally`.
   - On a large partial `Total:` with a negative exit code, read
     `<user data>/Godot/app_userdata/Fighters Through Time/logs/godot.log` first; never bisect by
     test class on that signature; never call a subset clean on fewer than 5 runs.
9. **Import discipline.** `localization/en.csv` and `content_manifest.csv` are compiled by Godot
   import. After editing either, run
   `--headless --import` and commit the regenerated `en.en.translation` /
   `content_manifest.1.translation`. Only A1 (manifest), A2 (en.csv), and C1 touch these files.
10. **Marker↔geometry conformance is mandatory and tested.** The pixel scene mirrors the
    fixed-point geometry through `WorldOrigin = (950, 700)`, `PixelsPerUnit = 62.5`, y inverted:
    `pixel = (950 + x·62.5, 700 − y·62.5)`. A1 ships a reusable conformance validator (given a
    stage scene root + its `FighterStageGeometry`, assert spawn markers, orb points, hazard
    anchors, platform collision rects, and wall/floor extents agree within an epsilon of one
    pixel); every Phase B per-stage test must call it. Nothing enforces this today — it is the
    single highest-risk drift point for nine new scenes.
11. **Hash baselines shift and that is expected.** A1/A2 add fields to Klotho components, which
    changes every state hash. No test pins a golden hash (they compare run-to-run), so this is
    safe — but any agent who finds a hash literal in a test reports it in §9 rather than updating
    it silently.
12. **Platform `SurfaceY` values must be integrator-reachable.** Landing uses exact `FP64`
    comparison against `SurfaceY`. Reuse the proven pattern (Florence lands cleanly at 2.4);
    every dossier platform in §4 gets an automated jump-landing test in A1. If a value proves
    unreachable, A1 adjusts by the minimum amount and logs the delta in §9. A stage with zero
    platforms silently reverts the floor to drop-through (`Platforms.Length > 0` is the solid-floor
    switch) — every stage has ≥2 platforms, so this cannot trigger, but do not "simplify" a stage
    to zero platforms.

---

## 3. Phase A — foundations (four parallel worktree agents; merge order A1→A2→A3→A4)

### A1 — Deterministic stage geometry + ten era hazards (owner: stages)

**Files owned:** `scripts/FighterSim/FighterStageGeometry.cs`, `scripts/FighterSim/FighterEntitySystems.cs`
(hazard system), `FighterSimulationComponents.cs` (`FighterHazardComponent` fields only),
`scripts/Environment/TestArenaController.cs`, `scripts/Environment/FighterStageData.cs` (schema
additions), new `scripts/Environment/StageAudioSet.cs`, `resources/Content/content_manifest.csv`
(naming rename per §2.1 only — no status flips), tests under `tests/Determinism/` and
`tests/ContentValidation/` named for stages/hazards, the new conformance validator.

Deliverables:
1. Nine `FighterStageGeometry` static entries + `ForStage` arms per the §4 dossiers, each with
   walls, ceiling, blast zone (−5), spawn distance 4, platforms, hazard anchors, orb anchors.
   Rewrite `FighterStageGeometryTests.UnknownStagesFallBackToTheDefaultGeometry` (it currently
   asserts `orleans_vanguard` falls back to Default — after this change only a genuinely unknown
   ID does) and pin every stage's values + a jump-landing test per platform + a two-simulation
   hash-identity run per stage.
2. Ten era-specific hazard implementations per §4.1: real warning/active/**recovery** phases
   (recovery uses the currently-dead `CooldownFrames` field), per-type behavior (movement,
   targeting, dwell, idle-trigger, pools), the block-absorbs-tick rule (§2.4), and per-fighter
   one-shot hit masks where specified. All randomness threads `match.RandomState0/1` in fixed
   system order. New `FighterHazardComponent` fields as needed (velocity, dwell counters, hit
   mask, sub-type/phase encoding) — `Pack = 4`, `int`/`FP64` only.
3. Per-hazard determinism tests: for each `HazardTypeID` 1–10, a scripted scenario asserting the
   distinctive behavior (debris crosses the stage, mortar fires at the telegraphed anchor after
   90 frames, Berlin only damages after 90 consecutive frames of dwell, Globe only targets an idle
   fighter, Gettysburg strikes the full telegraphed band, Paris drains Influence and deals 0
   damage, blocking absorbs a tick and costs one charge...) plus snapshot/restore convergence
   *through* the hazard's active window.
4. Fix `TestArenaController` to forward `stageID` into `FighterDriver.Initialize` so a
   TestArena-hosted match gets its stage's authored geometry (today it silently degrades to
   `Default`).
5. `FighterStageData` schema additions: `PreviewTexturePath` (placeholder preview image slot);
   keep `ProductionReady` and wire nothing else — the stage-select UI consumes both in C1.
6. New `StageAudioSet : Resource` (`[GlobalClass]`): `SetID`, ambient/combat/boss stream slots,
   crossfade metadata — the resource type the ten `AudioSet` manifest rows have been reserving.
   Contract test in the `PlaceholderAudioKitTests` style (stems loopable, `LoopEnd == MixRate`).
7. Manifest naming rename per §2.1 + the FighterStage manifest↔catalog StageID cross-check test.
   Re-run `--headless --import`, commit regenerated translation artifacts.
8. The marker↔geometry conformance validator (§2.10), applied to Florence as its first consumer —
   Florence's markers are believed conformant; if not, fix the *scene*, log in §9.

### A2 — Deterministic match flow + Fighter presentation core (owner: match)

**Files owned:** `FighterSimulationComponents.cs` (`FighterStateComponent`/`FighterMatchComponent`
fields), `FighterSimulationSystems.cs`, `FighterSimulationDriver.cs`, `scripts/Combat/FighterCamera.cs`,
`scripts/UI/TestArenaHUD.cs`, new `scenes/ui/LocalFighterPause.tscn` + `scenes/ui/MatchResults.tscn`
(+ scripts), `scripts/Core/InputManager.cs`, `scripts/Core/EventBus.cs` (new payloads),
`scripts/Core/SaveManager.cs` (`CharacterWins`/`CharacterLosses` writes), `scripts/UI/CharacterSelectScreen.cs`,
`localization/en.csv`, match-flow tests.

Deliverables:
1. **Countdown:** new pre-match `MatchState = 0` with a deterministic frame count (180 frames +
   30-frame GO window), input-gated in the sim; driver renders 3-2-1-GO from new localized keys.
   The sim enters `MatchState = 1` deterministically; `FighterSimulationDriver._PhysicsProcess`
   advances the sim during countdown (state must tick) but the input systems ignore gameplay
   buttons until live.
2. **Respawn platform (deterministic):** per `design-godot.md` ~1565–1571 — on stock loss (all
   match modes), the fighter respawns on a platform at `(0, +3.0)`: new respawn phase + frame
   fields on `FighterStateComponent`; 300 frames (5 s) dissolve with **all inputs locked**; any
   gameplay input after a short grace, or expiry, drops the fighter; 180 frames (3 s)
   invulnerability starts at the drop. Replaces the current instant ground teleport + flat 120
   i-frames. Roll i-frames stay distinct. Pinned by FSM/timing tests including "cannot act while
   on platform" and "drop starts the 3 s window".
3. **KO presentation (driver-side only):** on stock loss / match end, the driver runs a
   presentation sequence — hit-freeze, brief slow-motion (pacing `Advance` calls), camera
   focus/zoom on the KO via a new `FighterCamera` API, localized KO/DRAW stamp (`match_ko` /
   `match_draw` exist), winner-pose hold, placeholder fanfare via `AudioManager`, then the results
   transition. Timings from `design-godot.md` ~3253–3273. A new
   `EventBus.OnFighterPresentation`-style payload (mirroring `RewindPresentationPayload`)
   carries phases so Package 8 can restyle without touching flow. Deterministic state is
   provably untouched: a test advances an identical sim without the driver and compares hashes.
4. **Fighter pause + controller disconnect:** author `LocalFighterPause.tscn` (manifest row
   exists, `Planned`) attached to both Fighter scene controllers; `SceneTree.Paused` discipline
   per §2.8. Subscribe to `InputManager.PlayerDeviceDisconnected` → forced pause with a localized
   modal naming the disconnected player; reconnect flow reassigns the device (fix
   `RefreshConnectedDevices`'s unconditional `AutoAssignDevices()` clobbering manual assignments
   mid-match); safe exit to menu with confirmation. Keyboard fallback allowed.
5. **Results + rematch:** author `MatchResults.tscn` (manifest row exists) replacing the
   code-built panel in `TestArenaHUD.ShowResults`; per-end-condition treatment (KO stamp vs DRAW +
   both-idle); rematch resolves the destination through
   `FighterStageCatalog.Find(session.SelectedStageID).ScenePath` (fixes the hardcoded
   `TestArena.tscn`); Change Fighters / Main Menu / Return-to-Ship paths preserved; the raw
   `ui_cancel` instant-exit is replaced by the pause menu.
6. **Rules round-trip:** expose the Off/Low/Medium/High item/hazard frequency granularity in the
   select screen (today collapsed to a binary); test that every `MatchSettings` field survives
   select → match → rematch → match.
7. **Statistics:** write `CharacterWins`/`CharacterLosses` (existing save fields) alongside the
   totals; true draw still recorded as neither. Test.
8. All five end conditions get explicit test pins (bottom-fall stock loss, HP-KO end, and the
   HP-percentage tiebreak branch are currently unpinned).
9. New en.csv keys (countdown, disconnect modal, pause, respawn timer, winner pose etc.) +
   `--import` + committed translation.

### A3 — CPU completion (owner: cpu)

**Files owned:** `scripts/FighterSim/FighterCpuController.cs`, `CpuDecisionObservation.cs`,
`scripts/Enemies/MirrorParadoxController.cs` (adapter only), `scripts/Enemies/CpuFighterAI.cs`
(delete the dead stub + duplicate enum), one-line CPU construction change in
`FighterSimulationDriver.cs` (seed; coordinate with A2's edits — keep it minimal), CPU tests.

Deliverables:
1. **Observation expansion** (filled in both Observe paths per §2.5): self Y position + Y
   velocity + `RemainingJumps`; stage bounds (walls, blast zone) and platform summary via the
   geometry handed to the controller at construction; nearest-orb presence/position; active-hazard
   presence/position/half-width/phase. Story adapter supplies explicit "absent" sentinels and its
   own Y projection; `MirrorParadoxTests` parity stays green.
2. **Off-stage recovery:** when beyond the walls or falling below the floor line with no ground
   under it, steer toward stage center, spend jumps while falling (fix the held-latch so discrete
   re-presses are possible), aim the movement ability upward (`MoveY` is currently hardcoded 0 —
   support directional output), difficulty-scaled competence.
3. **Orb pursuit** (difficulty-scaled chance to path toward a live orb when safe) and **hazard
   avoidance** (during the 90-frame warning, move out of the telegraphed zone; scaled per band —
   even Easy's 30–45-frame reaction window fits).
4. **Special 2 at all bands** (Easy included, lower rate), fix the `else if` chain that starves
   `MovementAbility` behind the Dash roll, and lift the inline percentage literals into a
   per-difficulty tuning table (still deterministic constants — no resources; Fighter values stay
   normalized and code-owned).
5. **Seed derivation** from the match seed (plus player slot) instead of the literal `2026`.
6. **Behavior tests** (new, pure C# where possible): recovery gets back to stage on each band;
   orb pursued when spawned nearby; CPU exits a telegraphed hazard zone; Easy uses Special 2;
   determinism (same seed/state → identical frames) preserved; reaction windows unchanged.

### A4 — Rollback-readiness harness (owner: rollback)

**Files owned:** `scripts/FighterSim/FighterSimulation.cs` (accessors only if needed),
`scripts/Networking/RollbackProtocol.cs`, new `tests/Determinism/RollbackReadinessTests.cs`,
`docs/PERFORMANCE_BASELINE.md` (new rollback section).

Deliverables:
1. **Parameterized convergence harness:** delayed-input rollback (via `OnlineRollbackSession` over
   `InMemoryRollbackTransport` with latency) for all nine characters (nine mirrors + a
   representative cross-pair set) × every authored `FighterStageGeometry`, hazards and orbs High,
   long enough to cross hazard/orb spawns; assert no `InputArrivedTooLate` and per-tick hash
   equality. (At Phase A time only Default+Florence+A1's nine exist in the worktree merge order —
   the harness enumerates geometries dynamically so C1 re-runs it across all ten with zero edits.)
2. **Bounded-history proofs:** `CorrectRemoteInput` rejects beyond `RollbackHistoryTicks`;
   a depth-8 packet fires `InputArrivedTooLate` and depth-7 does not; `DesyncDetected` fires on an
   induced divergence (and the event now at least logs — a subscriber with diagnostics).
3. **Fix the unbounded `_correctedTicks` HashSet** in `OnlineRollbackSession` (prune below the
   rollback window) — a real leak over a 480 s match.
4. **Worst-case seven-frame resimulation timing:** a harness that forces depth-7 corrections every
   frame during a worst-case entity load (constructs + projectiles + zones + hazard + orbs live),
   measures per-correction cost (the existing `RollbackBudgetExceeded` stopwatch path), asserts
   against `RollbackBudgetMilliseconds = 8.0` with headroom slack for CI variance, and records
   snapshot size (`CaptureFullState().Length`). Results written into a new
   `docs/PERFORMANCE_BASELINE.md` rollback section (C1 refreshes it on final hardware/state).

---

## 4. Per-stage dossiers (locked numbers)

Shared: `BottomBlastZone = −5`, `SpawnDistance = 4`, `Ceiling = 9`. Platforms are
`(centerX, surfaceY, halfWidth)` one-way pass-throughs over a solid floor at y = 0. Orb anchors sit
0.5 above their platform or floor. Pixel conversion per §2.10. Catalog colors
(`BackgroundColor`/`GroundColor`/`AccentColor`) already authored per era drive the placeholder
palette.

| StageID | Hazard ID | Walls | Platforms | HazardAnchorXs | OrbAnchors |
|---|---|---|---|---|---|
| `florence_workshop` (shipped) | 1 | ±9 | (−4, 2.4, 1.6), (4, 2.4, 1.6) | {−6, 0, 6} | (−4, 2.9), (0, 0.5), (4, 2.9) |
| `orleans_vanguard` | 2 | ±9 | (−3.5, 2.0, 1.4), (3.5, 2.0, 1.4), (0, 4.0, 1.4) | {−8, 8} | (−3.5, 2.5), (3.5, 2.5), (0, 4.5) |
| `chicago_exposition` | 3 | ±10 | (−6, 3.2, 1.5), (6, 3.2, 1.5) | {0} | (−6, 3.7), (6, 3.7), (0, 0.5) |
| `paris_bastille` | 4 | ±9 | (−4, 2.6, 2.0), (4, 2.6, 2.0) | {−6, 0, 6} | (−4, 3.1), (4, 3.1), (0, 0.5) |
| `vesuvius_caldera` | 5 | ±8 | (−4.5, 2.0, 1.1), (3.0, 3.5, 1.1) | {−6, −2, 2, 6} | (−4.5, 2.5), (3.0, 4.0), (0, 0.5) |
| `nassau_flagship` | 6 | ±9 | (−4, 2.8, 1.8), (4, 2.8, 1.8) | {−5, 0, 5} | (−4, 3.3), (4, 3.3), (0, 0.5) |
| `alexandria_chambers` | 7 | ±9 | (−4, 1.6, 1.3), (4, 1.6, 1.3) | {−6, 0, 6} | (−4, 2.1), (4, 2.1), (0, 0.5) |
| `berlin_wall` | 8 | ±10 | (−6.5, 3.6, 1.3), (6.5, 3.6, 1.3) | {−4, 0, 4} | (−6.5, 4.1), (6.5, 4.1), (0, 0.5) |
| `globe_theatre` | 9 | ±9 | (−5, 2.4, 1.4), (5, 2.4, 1.4), (0, 4.4, 1.2) | {−5, 0, 5} | (−5, 2.9), (5, 2.9), (0, 4.9) |
| `gettysburg_ridge` | 10 | ±10 | (−4.5, 1.8, 1.2), (4.5, 1.8, 1.2) | {−5, 0, 5} | (−4.5, 2.3), (4.5, 2.3), (0, 0.5) |

A1 may adjust any value by the minimum needed for integrator reachability or spawn safety, logging
the delta in §9; Phase B scenes are authored against the *merged* values in code, not this table,
if they diverge.

### 4.1 Hazard behavior specs (design authority: `design-godot.md` §10 table, lines ~3105–3119)

Standard phase frames unless overridden: warning 90 (1.5 s), recovery 60. Damage ticks every 30
frames while a fighter overlaps an active damaging region. Existing per-type damage numbers are
kept where they exist. All new movement/targeting state lives in `FighterHazardComponent`.

1. **Florence — Siege Mech Steam Pipe (shipped).** Keep current numbers (random 5–10 dmg, kb 3);
   gains the shared recovery phase and block-absorbs-tick rule like every type. Any other change
   is a §9 deviation.
2. **Orléans — Trebuchet Fire (rolling debris).** Spawns at a wall anchor (±8), 90-frame warning
   marker at the spawn edge, then rolls horizontally at 0.09 units/frame toward the far wall,
   ground band, halfWidth 0.8; 7 dmg kb 3, one hit per fighter per debris (hit mask); despawns at
   the far wall. Jumpable by design.
3. **Chicago — Tesla Coil Induction.** Static grid centered at 0, halfWidth 4, ground band;
   warning 90, active 360; 6 dmg + `StaticCharge` 30 f per tick.
4. **Paris — Neural-Dampening Beam.** Beam halfWidth 1, tall column; spawns at an anchor, sweeps
   horizontally at 0.05 units/frame, bouncing between walls for the 360-frame active window;
   0 damage; drains 5 Influence per 30-frame tick while overlapping (current drain rule kept).
5. **Vesuvius — Volcanic Rockfall.** Warning marker at a ground anchor 90 frames; rock falls from
   the ceiling at 0.15 units/frame; impact: 8 dmg kb 4 (one-shot), then leaves a ground pool
   (halfWidth 1) for 180 frames applying `TimeDilation` 120 f per tick, no damage.
6. **Nassau — British Navy Mortar.** Target marker at an anchor, 90-frame telegraph, then a
   30-frame explosion: 10 dmg, kb 5 with upward bias (launcher). One-shot per fighter.
7. **Alexandria — Shifting Sand Sinkhole.** Quicksand region halfWidth 2 at an anchor; warning 90,
   active 480; while a fighter is *grounded* inside: 2 dmg kb 1 + `TimeDilation` 60 f at
   intensity 1.0 per tick (the 50% slow). Airborne fighters unaffected.
8. **Berlin — Surveillance Searchlight.** Light column halfWidth 1.2, tall band, at an anchor;
   active 480. Per-fighter dwell counters: 90 *consecutive* overlapping frames triggers a drone
   laser hit on that fighter (8 dmg kb 3) and resets their counter; leaving the beam resets it.
9. **Globe — Audience Heckle.** Active window 600 frames. A fighter that is grounded with
   |velocity.x| below a small threshold for 120 consecutive frames is pelted from the nearest
   gallery anchor: 5 dmg kb 2, resets that fighter's idle counter. Punishes camping; a moving
   fighter is never hit.
10. **Gettysburg — Laser Artillery Strike.** Telegraph 120 frames (2 s red sight line) centered on
    an anchor, halfWidth 5, low horizontal band (ground to ~1.5 high); then a 30-frame strike:
    12 dmg kb 6, one-shot per fighter. The widest, hardest-hitting hazard.

---

## 5. Phase B — nine stage agents (parallel worktrees, purely additive)

One agent per stage: Orléans, Chicago, Paris, Vesuvius, Nassau, Alexandria, Berlin, Globe,
Gettysburg. Every agent delivers, for its stage `<era>`:

1. **Scene** `scenes/fighter/FighterStage_<Era>.tscn` satisfying `fighter_stage_contract.tres`:
   root `Node2D` in group `fighter_stage`, `FighterStageController` script with the exact catalog
   `StageID`; required node paths `Presentation`, `Geometry`, `Spawns/Player1|2`,
   `OrbSpawnPoints`, `HazardAnchors`, `Camera2D` (with `FighterCamera`, limits 0/0/1920/1080,
   zoom 1.0–1.4), `ContentContract` (`ContentTemplateMarker`, `ContentID = fighter_stage_<era>`,
   contract resource). Ground/walls `StaticBody2D` on collision layer 64, one-way platforms on
   layer 128 with `one_way_collision = true`, `collision_mask = 0` everywhere (the sim is
   authoritative). Every marker/collider placed by the §2.10 conversion from the **merged**
   `FighterStageGeometry` values.
2. **Presentation:** `BackdropTint` + a `ParallaxBackground` with ≥2 layers (placeholder SVGs +
   catalog era colors at distinct scroll factors), geometry visuals in `GroundColor` with
   `AccentColor` top lines, era-distinct props (ColorRect/Polygon2D level is fine), visible
   hazard-anchor dressing that matches the hazard identity (e.g. gallery boxes for Globe, towers
   for Berlin). Distinct at a glance from every other stage.
3. **Pool config** `resources/Pools/fighter_stage_configs/fighter_stage_<era>_pool_config.tres`
   with unique `ConfigID = fighter_stage_<era>_pools`, mirroring `test_arena_pool_config.tres`'s
   pool set/budgets (fighter_projectile, fighter_vfx, fighter_environment_vfx, chronal_orb,
   damage_numbers, fighter_construct).
4. **Audio set** `resources/Audio/stage_<era>_audio.tres` (`StageAudioSet` from A1) referencing
   the placeholder stems at the exact path the manifest AudioSet row reserves.
5. **Preview image** `assets/placeholders/stages/<era>_preview.svg` (distinct silhouette +
   era palette), path set in the stage's own test expectations (catalog wiring is C1's).
6. **Per-stage test file** `tests/ContentValidation/FighterStage<Era>Tests.cs`: scene loads and
   instantiates cleanly; contract validation via `ContentSceneContractValidator`; the A1
   marker↔geometry conformance validator passes; the pool config is valid/budgeted; the audio set
   meets the stem contract; a two-simulation hash-identity run on this stage's geometry with
   hazards High crossing at least one full hazard cycle.
7. **Deviations** appended to §9 under `### B<n> — <era>`.

Phase B agents must NOT touch the shared files listed in §2.7. New en.csv keys are not needed —
all four `stage_<era>_*` key families already exist and are compiled.

---

## 6. Phase C — closeout (C1, serial, after all Phase B merges)

1. `stage_catalog.tres`: set the nine `ScenePath`s, set `PreviewTexturePath` for all ten, flip
   `ProductionReady = true` on all ten.
2. `scene_pool_catalog.tres`: add ten rows — the nine new configs **plus Florence**, which today
   warms no pools at all (author `fighter_stage_florence_pool_config.tres` as part of this step).
   Extend `ScenePoolConfigTests.CatalogMapsEveryCurrentGameplaySceneToItsPoolBudget` with the ten
   (scene, ConfigID) rows.
3. `tests/integration/SceneSmokeTests.cs`: add all ten fighter scenes (Florence is missing today).
4. `content_manifest.csv`: flip the nine FighterStage rows to
   `Implemented/ReadyForReplacement/Valid`; flip the ten AudioSet stage rows to
   `Implemented/Placeholder/Valid` pointing at the authored `.tres`; update the ten VisualSet
   stage rows to the per-stage preview/parallax assets. Verify
   `ContentManifestTests.PlannedResourcesRemainVisibleUntilTheyAreAuthored` still has honest
   feeders (remaining Planned UIScreen/AudioSet/DialogueSet rows) — if the premise is genuinely
   gone, retire it with a note per its own doc comment; never invent a Planned row to feed it.
5. Stage-select polish: show the preview image; gate the `fighter_stage_prototype` tooltip line on
   `ProductionReady` (it currently brands every stage a prototype unconditionally).
6. New `tests/ContentValidation/FighterLocalizationTests.cs` family sweep through the compiled
   translation (`stage_*` ≥ 40, `fighter_*` ≥ 25, `match_*` ≥ 3, plus A2's new key families),
   mirroring `CampaignLocalizationTests`.
7. **Run the rollback-readiness gate** (A4 harness) across all ten geometries × nine kits with
   hazards/orbs High; record convergence, snapshot size, and worst-case depth-7 resimulation
   timings in `docs/PERFORMANCE_BASELINE.md`. The gate result is the Package 7 entry criterion —
   report it explicitly.
8. Full validation per §8, three consecutive full-suite runs, smoke-run all ten fighter scenes +
   MainMenu + TestArena + CharacterSelect, `--import` clean.
9. Ledgers in one commit: `AGENTS.md` (implemented boundary, new test baseline, gap bullets),
   `CLAUDE.md` (verified-commands baseline count), `IMPLEMENTATION_PLAN.md` (Package 6 checkboxes
   + status paragraph), root `IMPLEMENTATION_STATUS.md` §2/§21/§22, `docs/IMPLEMENTATION_STATUS.md`
   (milestone row 6, closure paragraph, validation record, remaining-packages item 3), and this
   plan's §9 closeout block including a "What Package 6 did NOT deliver" honesty list.

---

## 7. Sequencing and integration protocol

1. Plan committed to `main` first; all agents branch from it via worktrees.
2. **Phase A:** A1–A4 in parallel worktrees. Orchestrator merges in order A1→A2→A3→A4, resolving
   the two declared overlap files by union; after each merge, `dotnet build` + the merged agent's
   test set; after A4, one full suite run. Phase B does not start until Phase A is fully merged
   and green.
3. **Phase B:** nine stage agents in parallel worktrees off the post-A `main`. Purely additive —
   merges are conflict-free by construction except §9 appends (union). After all nine merge: build
   + full suite.
4. **Phase C:** C1 serial in the main checkout.
5. Each agent commits in its worktree with a descriptive `Package 6 <workstream>:` message and
   ends with its §9 deviation block already in the plan file.

## 8. Validation gates (every phase)

1. `dotnet build FightersThroughTime.csproj --nologo` — no new warnings.
2. `dotnet test FightersThroughTime.csproj --settings .runsettings` — read the **`Total:`**
   against the current baseline (705 at Package 6 start; each phase raises it). A green exit code
   alone is not evidence (CLAUDE.md failure signatures 1/2/4). Cold-cache first runs in a fresh
   worktree may hit the 300 s import timeout — re-run warm before investigating.
3. Headless import check after any scene/resource/CSV edit; `--import` (not `--quit`) after
   `en.csv`/manifest edits, with regenerated translation artifacts committed.
4. Headless smoke (`--quit-after 300`) for any authored scene.
5. Story/Fighter isolation: no Story progression value enters normalized Fighter loadouts; new
   Fighter state is fixed-point, snapshot-complete, hash-visible (A4 harness is the proof).

## 9. Deviations (append-only)

*(Every agent appends `### <workstream> — <subject> (date)` blocks here in the
Package 5 format: bold one-sentence claim, then reasoning and the pinning test. The orchestrator
appends integration blocks per phase and the closeout appends the final honesty list.)*

### A1 — Deterministic stage geometry + ten era hazards (2026-08-08)

**A1: every §4 dossier number shipped unchanged; no platform needed an integrator-reachability
adjustment.** Landing snaps `Position.y` to `SurfaceY` exactly on the crossing frame
(`FighterMovementSystem.TryLandOnPlatform`), so reachability is a question of *jump height*, not
representability: single jump is `13²/(2·30) ≈ 2.82` units and a double jump roughly doubles it,
which clears the tallest authored surface (Globe's 4.4). Pinned by
`FighterStageGeometryTests.EveryAuthoredPlatformIsReachableAndLandableByJumping`, which walks the
real fixed-point simulation to every one of the 21 authored platforms and asserts an exact landing —
not a tolerance comparison.

**A1: the plan's item 1 said "rewrite `UnknownStagesFallBackToTheDefaultGeometry`"; the whole
`FighterStageGeometryTests` file was rewritten instead.** The old file pinned Florence by hand and
had no mechanism for nine more stages. It is now dossier-table driven
(`EveryAuthoredStageMatchesItsLockedDossier`, `EveryAuthoredStageSharesTheCommonBoundsContract`,
`EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface`, plus per-stage jump-landing, hash-identity
and anchor-only runs), and the six original Florence behaviour tests are preserved verbatim. The
fallback test now asserts on genuinely unknown IDs *and* on the two retired manifest spellings
(`pompeii_caldera`, `egypt_chambers`) so a stale ID can never silently resolve to a real stage.

**A1: `FighterStageGeometry.AllAuthored` was added so downstream harnesses enumerate stages instead
of hardcoding a list.** Plan §3 A4 item 1 explicitly wants the rollback harness to pick up new
geometries "with zero edits"; a public ordered array is the cheapest way to give it that, and A1's
own per-stage tests use the same array so the two can never disagree about what "all stages" means.

**A1: the block-absorbs-a-hazard-tick rule (§2.4) turned out to be *already* satisfied by the shared
`ApplyFighterHit` path, so A1 made it explicit rather than implementing it.** `ApplyEnvironmentHit`
already routed through the basic-attack-class block branch, which charged 1 shield charge. That was
incidental — it fell out of the class-default `attackClass == SpecialAttackClass ? all : 1`
expression, so any later change to the special-shatter rule would have silently changed hazard
behaviour too. `ApplyEnvironmentHit` now passes `FighterDamageRules.HazardBlockChargeCost = 1`
explicitly, and `FighterHazardBehaviorTests.BlockingAbsorbsAHazardTickForOneShieldCharge` pins the
blocked/unblocked pair. **This is a downgrade of the plan's claim that it is "a behavior change";
it is not, and the plan's phrasing should not be read as evidence that hazards used to be
unblockable.**

**A1: one-shot hazard hit masks are consumed on first *overlap*, not on first *damage*.** A mortar
shell or artillery strike that a fighter blocked, rolled through with i-frames, or was hyper-armoured
against is spent for that fighter. The alternative — consuming only on a landed hit — lets a blocked
30-frame explosion re-attempt every frame and drain the whole shield in half a second, which is
strictly worse for the player and much harder to reason about. Pinned by the "exactly once" HP
assertions in the Orléans, Nassau and Gettysburg scenarios.

**A1: hazard `Velocity` is stored in units per *frame*, not per second, and is integrated directly
without `FixedDelta`.** Every other velocity in the sim is per-second. The §4.1 dossier speeds
(0.09, 0.05, 0.15) are authored per frame, and multiplying them by `FixedDelta` would have made the
debris take 50 minutes to cross the stage. The field is documented as per-frame on
`FighterHazardComponent`; the Orléans and Paris scenarios assert real travel distances so a unit
mix-up fails loudly.

**A1: the mortar's "upward bias" needed a new optional `verticalKnockbackScale` on
`ApplyFighterHit`.** The shared impulse writes `Velocity.y = force` — a 1:1 pulse — so there was no
way to express a launcher without duplicating the knockback maths in the hazard system. The
parameter defaults to `FP64.Zero` meaning "use 1", so every existing call site is byte-identical.
Nassau passes 2. Pinned by `NassauMortarTelegraphsThenLaunchesEachFighterUpwardExactlyOnce`
(`Velocity.y > |Velocity.x|` on the hit frame).

**A1: `CooldownFrames` now means *recovery length* (60), not the spawn interval.** The field was
dead; the plan asks recovery to use it. It previously held
`FighterSpawnIntervals.HazardFrames(frequency)`, which was never read. Nothing consumed the old
meaning. `EveryHazardRunsWarningActiveAndRecoveryBeforeDespawning` pins the full phase chain and the
value.

**A1: the two existing hazard tests stayed green with no assertion changes.**
`FighterSimulationTests.HazardAndOrbSpawnsAreDeterministicAndSnapshotSafe` (hazard count 1, phase 0
at tick 1805) and `SelectedStageHazardTypeIsPartOfDeterministicState` still hold: the first hazard
spawns on the 1800-frame boundary and the extra 60-frame recovery does not overlap the next spawn at
any supported frequency.

**A1: `TestArenaController` now forwards the stage ID, which means a TestArena-hosted match gets the
selected stage's *geometry* while still showing the TestArena *visuals* until Phase B lands the nine
scenes.** That mismatch is not new — the TestArena template already drew platforms the Default
geometry did not have — and it resolves itself when the closeout points each catalog `ScenePath` at
its own scene. Flagging it so nobody reports it as a regression during Phase B.

**A1: the marker↔geometry conformance validator treats a one-way platform body's *origin* as the
surface point, not the top edge of its collision rect.** Florence's `GearPlatformLeft` sits at pixel
(700, 550) = (−4, 2.4) with a 200×12 rect centred on the origin, so its rect top edge is 6 px above
the authored surface. Since `collision_mask = 0` everywhere and the simulation is authoritative,
the collider is presentation; the node origin is the meaningful anchor. Florence was found fully
conformant under this rule and **was not modified**. `FighterStageConformanceTests` proves the
validator rejects drifted markers and a wrong-stage geometry, so it cannot pass nine Phase B scenes
vacuously.

**A1: `StageAudioSet` carries `ClimaxStem`, not `BossStem`.** Fighter Mode has no boss; the third
layer is the last-stock/final-seconds climax. The placeholder kit's third stem file is still
`placeholder_stem_boss.tres` (Story-named, shared), which `StageAudioSetTests` loads into the climax
slot. Renaming the shared placeholder asset was out of A1's file ownership.

**A1: `content_manifest.1.translation` did not change despite the manifest rename, and that is
correct.** The manifest is imported with the `csv_translation` importer keyed on column 0
(`category`), which is heavily duplicated, so the compiled artifact never contained the per-row ids
that were renamed. `--headless --import` was run and regenerated the artifact byte-identically;
there is nothing to commit for it.

**A1: the GdUnit pipe name is shared across worktrees, so concurrent Phase A test runs corrupt each
other's results.** GdUnit4 launches its Godot child with
`--pipe-name gdunit4-FightersThroughTime`, derived from the assembly name — identical in every
worktree. When two agents run `dotnet test` at once the testhosts cross-connect and one or both
report a large partial `Total:` with `GodotRuntimeTestRunner ends with exit code: 100`, or fall back
to the 24 pure-C# tests with `Failed to connect: Connection timeout`. **This looks exactly like
CLAUDE.md failure signatures 1 and 2 but is neither** — there is no FATAL in `godot.log` and no
negative exit code, and the truncation point moves with the other agent's activity. A1's full-suite
numbers below were taken in a window with no other Godot process alive. The orchestrator should
serialize full-suite runs across Phase A/B worktrees, or this will keep producing phantom
regressions.
