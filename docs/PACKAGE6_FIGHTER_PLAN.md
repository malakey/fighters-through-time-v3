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

### A2 — Deterministic match flow + Fighter presentation core (2026-08-08)

**The countdown length is a `FighterMatchRules` field defaulting to zero, and only the Godot driver
passes the production 180.** §3 A2 asks for a pre-match `MatchState = 0`; making 180 the
*simulation* default would have shifted the frame budget of roughly twenty existing tests spread
across `FighterSimulationTests`, the nine kit/ultimate suites, `FighterCpuControllerTests` (A3),
`RollbackProtocolTests` (A4) and `MirrorParadoxTests` (A3) — files this workstream does not own and
whose edits would collide at merge. `FighterMatchRules.PreMatchCountdownFrames` is still fully
deterministic state (it is written into `FighterMatchComponent.CountdownFramesRemaining` and enters
every snapshot and hash); headless scenarios opt out by default and
`FighterSimulationDriver.RulesFor` — the single production mapping — always passes
`FighterMatchFlowRules.CountdownFrames`. Pinned by
`FighterMatchFlowTests.ProductionRulesStartTheMatchInTheThreeSecondCountdown`,
`.GameplayInputIsIgnoredUntilTheCountdownEnds` and
`.MatchSettingsSurviveTheMappingIntoDeterministicRules` (which asserts the driver mapping carries
180). **Later agents: construct a countdown simulation with
`FighterMatchRules.Disabled.WithCountdown(FighterMatchFlowRules.CountdownFrames)`.**

**The frame on which the countdown resolves is the match clock's first tick.** The countdown system
runs in PreUpdate and flips `MatchState` to 1 there, so `FighterMatchSystem` (LateUpdate) decrements
the timer on that same frame. Gameplay input is still discarded on it — `ClearGameplayInput` runs
before the flip — so the fighters cannot act; only the clock moves. Exactly 180 frames are
input-locked. Pinned by `FighterMatchFlowTests.TheMatchClockDoesNotRunDuringTheCountdown`.

**The bottom blast zone was unreachable before this change, and fixing it required reordering the
movement system's tail.** On any stage whose base floor spans the full width — which is every
authored geometry — the ground snap `Position.y <= 0` ran *before* the blast-zone check, so a
fighter who had fallen past `BottomBlastZone` was teleported back up onto the floor the instant
their 30-frame drop-through window expired. From a standing drop-through the deepest reachable point
is about −5.17 at frame 31, i.e. only just past the −5 line, and the snap always won. The blast-zone
check now resolves first and `continue`s. This is what makes plan scope item 9's "bottom fall" end
condition real rather than nominal; it changes nothing on solid-floor stages, where the fighter
never gets below y = 0 at all. Pinned by `FighterMatchFlowTests.MatchEndsOnABottomBlastZoneFall` and
`.MatchEndsOnStockExhaustion`. **A1/C1 note:** stages whose authored floor does not span the full
width will now let fighters fall off the sides of the floor as designed; nothing else depends on the
old ordering.

**`FighterStateComponent` sits exactly on Klotho's 128-byte component budget, so the respawn phase
and its countdown share one field.** Two `int`s pushed the struct to 132 bytes and raised
`KLSG_ECS004`, which would have been a new build warning. `RespawnFramesRemaining > 0` is itself the
"on platform" phase — the drop always zeroes the counter in the same frame it reaches zero — so
`FighterMatchFlowRules.IsOnRespawnPlatform` reads the single field. Anyone adding a field to this
struct must first free space; `SpawnPosition` (16 bytes) is now written at spawn and never read and
is the obvious candidate.

**Spawn invulnerability is expressed through the existing `InvulnerabilityFrames` field rather than
a new damage-rule branch.** While the platform holds a fighter the movement system re-asserts
`InvulnerabilityFrames = RespawnFramesRemaining + 180` every frame, and the drop sets a clean 180.
That satisfies "invulnerable on the platform, 3 s counted from the drop" without editing
`FighterDamageRules` in `FighterEntitySystems.cs`, which A1 owns. Roll i-frames stay distinct
because the roll grant is guarded by `if (InvulnerabilityFrames < RollInvulnerabilityFrames)`.
Pinned by `.TheFighterIsInvulnerableForTheWholeDissolve`,
`.AnyInputAfterTheGraceWindowDropsTheFighterAndArmsTheThreeSecondWindow` and
`.RollInvulnerabilityStaysDistinctFromSpawnInvulnerability`.

**A 30-frame grace window was added to the respawn platform; the design does not name one.**
design-godot.md ~1569 says "any input" drops the fighter, which in practice means the still-held
input that scored the knockout drops them on frame one. §3 A2 anticipated this ("any gameplay input
after a short grace"). `FighterMatchFlowRules.RespawnPlatformGraceFrames = 30` (0.5 s). Pinned by
`.AFighterCannotActWhileTheRespawnPlatformHoldsThem`.

**The flow UI is attached by `FighterSimulationDriver`, not by the stage controllers.**
`LocalFighterPause.tscn`, `MatchResults.tscn` and the new `FighterPresentationOverlay` are added as
driver children in `AttachMatchFlowUI`. The driver is the one node both `TestArenaController` (A1's
file) and `FighterStageController` already create, so this needed no edit to either controller and
every Phase B stage scene inherits the whole flow for free with no per-stage wiring. Pinned by
`FighterMatchFlowContentTests.TheAuthoredPauseSceneInstantiatesAtItsReservedPath` /
`.TheAuthoredResultsSceneInstantiatesAtItsReservedPath`.

**`MatchCompleted` now fires at the *end* of the KO sequence instead of the instant the match
resolves.** The driver raises the results transition after hit-freeze → slow motion → stamp →
winner pose (~4.6 s). `TestArenaHUD` no longer subscribes to it — its code-built results panel and
its raw `ui_cancel` instant-exit are both gone — so the only current consumer is the driver's own
results screen. Any future subscriber must expect the delay.

**Overall win/loss stays player-one-centric while the new character tallies are per-character.**
`FighterMatchStatistics.Record` keeps the pre-Package-6 meaning of `TotalWins`/`TotalLosses` (the
local profile is player one) and adds `CharacterWins`/`CharacterLosses` for both fighters. A true tie
writes none of the four. Pinned by `FighterMatchStatisticsTests` (5 cases).

**`InputManager.RefreshConnectedDevices` no longer re-runs a full auto-assign.** The old code called
`AutoAssignDevices()` on every connect *and* disconnect, which cleared both maps and reshuffled both
fighters' controllers any time any pad was plugged in mid-match. `ApplyDeviceTopology` now drops only
assignments whose device vanished and fills only empty slots; `TryAssignFirstFreeDevice` backs the
reconnect flow. `SetConnectedJoypadsForTesting` is an `internal` seam (tests compile into the same
assembly) so the policy is exercisable without Godot's `Input` singleton. Pinned by
`LocalFighterPauseTests.ReconnectingAJoypadKeepsExistingAssignmentsInsteadOfReshufflingThem`,
`.ADisconnectOnlyClearsTheAffectedSlot` and `.RebindingTakesTheFirstUnclaimedConnectedDevice`.

**No hash literal was found in any test**, per §2.11 — every determinism suite compares run-to-run,
so the two new component fields shifted baselines harmlessly.

**Placeholder KO/fanfare audio resolves to nothing today.** `FighterSimulationDriver` looks for
`res://audio/sfx/combat/ko_stinger.ogg` and `res://audio/sfx/ui/victory_fanfare.ogg` and no-ops when
absent; `audio/sfx/` is empty in this repository. The wiring and the `EventBus` phase payload are in
place so Package 8 only has to drop the stems in.

**Test-environment note for the orchestrator.** Parallel Phase A worktrees share one GdUnit4 REST
port and one `app_userdata` log directory, so concurrent `dotnet test` runs abort each other with
`Starting GodotRuntimeExecutor failed` / `The server returned an unexpected status code` and a
truncated `Total:` (29 or 122 here) — always with **0 failures and no negative exit code**, which
distinguishes it from CLAUDE.md failure signatures 1/2/4. The log will contain another worktree's
absolute paths. Two clean serial runs were obtained at **742/742** (705 baseline + 37 A2 tests).

### A3 — CPU completion (2026-08-08)

**A3: the per-band rates were taken from `design-godot.md` §10's difficulty matrices rather than
from §3 A3's summary, so three of this plan's phrasings are implemented differently.** §2's authority
order puts the design document above this plan, and §10 specifies each band's kit precisely. The
three divergences, all pinned by `tests/Determinism/FighterCpuBehaviorTests.cs`:

1. **Special 2 on Easy is the off-stage recovery button, not a low-rate neutral option.** §3 A3 item 4
   says "Special 2 at all bands (Easy included, lower rate)"; §10 Easy says Special 2 "is only
   triggered when the AI is off-stage and below the main platform Y-coordinate" and that Easy is
   "restricted from using Special 1 (projectiles) or Special 2 ... in standard neutral play". Easy
   therefore has `SpecialTwoPercent = 0` and `RecoverySpecialTwoPercent = 55`. Pinned by
   `EasyUsesSpecialTwoOffStageAndNeverInNeutral`.
2. **Easy does not avoid hazards at all, and Normal reacts only once the hazard is damaging.** §3 A3
   item 3 says avoidance is "scaled per band — even Easy's 30–45-frame reaction window fits"; §10
   says Easy "does not react to stage hazard warning indicators. Will walk into active hazard zones
   and take damage freely" and Medium "reacts to active hazard zones (after the warning phase ends
   and damage begins) ... does not preemptively avoid warning indicators". Only Hard vacates during
   the 90-frame telegraph. Pinned by `HardVacatesATelegraphedHazardDuringTheWarningPhase`,
   `NormalIgnoresTheWarningAndOnlyLeavesOnceTheHazardIsDamaging`, and
   `EasyWalksIntoHazardsInBothPhases`. **If the orchestrator prefers the plan's reading, the only
   change needed is `CpuBandTuning.Easy.HazardAvoidPercent` / `Normal.AvoidsHazardWarning`** — the
   table is the single edit point.
3. **Easy commands a recovery but cannot physically complete one, and the test says so.** §3 A3 item 6
   asks for "recovery gets back to stage on each band". At the simulation's −30 u/s² gravity, a
   −5 blast zone, and a 13 u/s jump, a fighter that has crossed the floor plane is already past the
   blast zone before Easy's 30–45-frame reflex window elapses. §10 Easy in fact says "No active
   recovery attempts ... simply falls". The middle ground implemented: Easy *issues* toward-centre
   steering plus jumps and Special 2 (its `RecoveryJumpPercent` is 60), but only Normal and Hard are
   asserted to make it back. `OffStageRecoverySteersTowardCentreAndSpendsJumpsOnEveryBand` pins the
   commands on all three bands; `CommandedRecoveryClimbsBackOverTheFloorWithinTheNormalAndHardReflexWindows`
   pins the outcome on two. Note §10 Easy contradicts itself here — the same block grants Easy the
   double jump "unless trying to recover from a ledge" and the off-stage Special 2.

**A3: two band gaps outside the plan's list were closed while lifting the literals into
`CpuBandTuning`, because they are the same bug as the Special 2 one.** Special 1 was Hard-only in
every branch (§10 Medium: "Uses Special 1 (zoning/projectiles) when at medium-to-long distance"), and
the defensive block/roll branch was Hard-only (§10 shield rates: Easy 10%, Medium 40%, Hard 80%).
Normal now has `SpecialOneRangedPercent = 22` / `SpecialOneClosePercent = 12`, and all three bands
block at their design rate with the evasive roll still Hard-only. Pinned by
`NormalZonesWithSpecialOneAtRangeWhereItPreviouslyNeverCould` and
`BandTuningMatchesTheDesignDifficultyMatrices`.

**A3: the CPU's buttons are now pulsed (2-frame hold, 1-frame release gap) instead of latched, which
is a behaviour change for every existing CPU including the Story Mirror Paradox.** The old controller
held the chosen `GameplayButtons` until the next *different* decision, and the simulation reads Jump,
Roll, Dash, Ultimate, and every attack from the `Pressed` edge — so a CPU that kept choosing "attack"
produced exactly one attack for the whole match, and a double jump was impossible by construction.
`EdgeButtons` are now scheduled through an explicit press → hold → release → gap cycle.
`RepeatedIdenticalDecisionsStillProduceRepeatedPressedEdges` and
`EveryEdgeButtonIsReleasedBeforeItIsPressedAgain` pin it; the Level 13 clone gets meaningfully more
aggressive as a side effect, which `MirrorParadoxTests` parity still covers because both sides of the
comparison changed identically.

**A3: orbs, hazards, and the match-live gate reach the CPU through a new `ICpuWorldObserver` handed in
at construction, not through the driver's per-frame `Sample` call site.** §3 A3 limits this workstream
to "one-line CPU construction change in `FighterSimulationDriver.cs`", and the CPU cannot see orbs or
hazards from fighter components alone. `FighterSimulationWorldObserver` wraps `FighterSimulation` and
is constructed in the same block as the controller, so the driver's `_PhysicsProcess` is untouched and
A2 owns it cleanly. The interface also lets the behaviour tests script an orb or a telegraphed hazard
without standing up a simulation. `TheWorldObserverFillsTheOrbAndHazardBlocksAndTheLiveGate` pins it.

**A3: `SuppressGameplayInput` is phrased as "not live" rather than as a `MatchState` value, so the
default reads as live.** A2 introduces `MatchState = 0` for the countdown and a respawn-platform
phase; a raw `MatchState` field would have made every hand-built observation (and the Story adapter)
read as "state 0 = countdown" and emit nothing. The world observer sets it from
`GetMatchState().MatchState != 1`, so any future non-live state suppresses the pad automatically.
Pinned by `ANonLiveMatchStateSuppressesEveryGameplayButtonAndAllMovement`.

**A3: the CPU seed is `WorldSeed * 397 + 1` (player slot), which today is a constant because the
driver never passes a match seed.** `FighterSimulation`'s `seed` parameter still defaults to `2026`
and `FighterSimulationDriver` does not supply one, so the derivation currently reproduces one fixed
stream — but it now tracks the match seed rather than a literal, so whoever wires a real per-match
seed (A2's rules round-trip is the natural place) gets varied CPUs with no further change here.

**A3: `scripts/Enemies/CpuFighterAI.cs` and its duplicate `FTT.Enemies.CpuDifficulty` enum are
deleted.** The class was a no-op stub — `DecideAction()` computed a distance and discarded it — and its
enum forced the fully-qualified `FTT.Core.CpuDifficulty` workaround in `MirrorParadoxDecisionAdapter`,
which is also removed. `IMPLEMENTATION_STATUS.md` line ~521 still lists it in an Enemies file
inventory; C1 should drop it there.

**A3: no hash literal was found in any test touched by this workstream** (plan §2.11 reporting duty).

**A3: parallel Phase A worktrees cannot run `dotnet test` at the same time — the resulting partial
`Total:` is contention, not a regression, and it is a sixth failure signature.** GdUnit4 v6.2's
runner transport is a *named pipe* whose name derives from the project, not the worktree, and Godot's
`app_userdata/Fighters Through Time` log/user directory is likewise shared across every worktree. A
run that loses the race reports `Failed to connect: Connection timeout` with `Total: 46` (only the
non-Godot tests), or aborts mid-suite with a large partial total; the tell is
`Exception All pipe instances are busy` in `godot.log`, or another agent's worktree path appearing in
the C# backtraces there. Re-running in a clear window gives the true result — every run in this
worktree that actually launched Godot reported the full total, with no in-between values. Worth a
CLAUDE.md failure-signature row at closeout.

Post-merge re-verification for the orchestrator:

- **A1's hazards.** The evasion tests use scripted observations, not real hazards, so they stay green
  regardless — but once A1's ten era hazards land, re-run
  `--filter "FullyQualifiedName~FighterCpuBehavior"` and spot-check that moving hazards (Orléans
  debris, Paris beam, Vesuvius rockfall) are handled. The controller deliberately re-reads the hazard
  every frame and never caches a position, and `TryGetRelevantHazard` selects by current region
  distance with warning phase winning ties, so movement is expected to Just Work.
- **A1's nine geometries.** `FighterStageGeometry` is a constructor parameter; new stages need no CPU
  edit. Worth confirming the recovery branch never fires on a solid-floor stage (it cannot: the
  movement system clamps X to the walls and pins Y at 0 when `Platforms.Length > 0`).
- **A2's driver refactor.** The only overlap is the `if (session.FighterOpponentType == ...Cpu)` block
  in `Initialize`; take A3's version whole. If A2 moves CPU construction before the `Simulation`
  assignment, `Simulation.GetMatchState()` and `new FighterSimulationWorldObserver(Simulation)` must
  move with it.
- **A2's countdown/respawn phases.** Confirm the driver still reaches `_cpuController.Sample` during
  those phases (the CPU will emit an inert pad on its own) or continues to early-return; either is
  correct, but only the former exercises `SuppressGameplayInput`.

### A4 — Rollback-readiness harness (2026-08-08)

**GdUnit4 silently refuses to execute a plain C# `[TestSuite]` that shares a source file with a
`[RequireGodotRuntime]` suite, so the gate is split across two files.** The first draft put both
suites in `tests/Determinism/RollbackReadinessTests.cs`. The adapter *discovered* all 22 pure-C#
cases (`Discover: TestSuite ... with 22 TestCases found`) and then ran none of them: the full-suite
`Total:` moved 705 → 706, i.e. only the one Godot-runtime case was added. Running the pure suite
alone by exact filter executed all 22 and passed. This is the most dangerous failure mode in the
repository's test story — a green run that silently drops a whole suite — and nothing in CLAUDE.md's
failure-signature table covers it, because the `Total:` looks plausible rather than collapsed.
Splitting the Godot-runtime suite into its own
`tests/Determinism/RollbackReadinessKitShapeTests.cs` fixed it immediately (705 → 728). **Rule for
later agents: one `[TestSuite]` class per file, and always check `Total:` against the exact expected
delta, not just against "bigger than before".** Plan §3 A4 named a single new test file; this is the
one file-layout deviation.

**Authored stage geometries are enumerated by reflection over `FighterStageGeometry`'s public static
members, not by a hardcoded list.** Plan §3 A4.1 requires the harness to pick up A1's nine new
geometries with zero edits, and C1 to re-run it across all ten. `RollbackReadinessTests.AuthoredGeometries()`
reflects over public static properties *and* fields of type `FighterStageGeometry`, de-duplicates by
instance, and orders by member name so the sweep is reproducible. In this worktree that yields two
(Default + Florence); after A1 merges it yields eleven with no change to the test. The 15 matchup
`[TestCase]` rows are fixed, so the suite's contribution to `Total:` stays at 22 regardless of how
many stages exist — only its runtime grows.

**The nine per-character loadouts are pure-C# *shapes*, not the authored kits, and a separate
Godot-runtime test pins the mirror.** `FighterLoadoutFactory.FromCharacterData` needs Godot
`Resource` instances, which would have forced `[RequireGodotRuntime]` on the whole gate. Instead
`RollbackHarnessKits.KitShape` builds a `FighterLoadout` per roster slot whose *structure* — special
1/2 execution types, persistent construct IDs, movement type — matches that character's authored
`.tres`, with deliberately synthetic tuning numbers chosen to maximize entity load. No canonical
balance number is restated. `RollbackReadinessKitShapeTests` loads all nine `CharacterData`
resources and fails if any of those six structural fields drifts, so a re-authored kit cannot
silently leave the rollback gate testing a stale shape.

**`OnlineRollbackSession` gained a `RollbackCorrectionMeasured` event so per-correction cost is
observable, not only budget breaches.** Plan §3 A4.4 says to measure through "the existing
`RollbackBudgetExceeded` stopwatch path", but that event only fires *above* 8 ms — and nothing ever
exceeds 8 ms, so it yields no samples at all. The same stopwatch now also raises
`RollbackCorrectionMeasured(depth, milliseconds)` for every applied correction;
`RollbackBudgetExceeded` is unchanged and still fires above budget. The new event is diagnostics
only, carries no simulation state, and is what produces the medians recorded in
`docs/PERFORMANCE_BASELINE.md`.

**`_correctedTicks` became a fixed 8-slot ring rather than a pruned `HashSet`.** Pruning a `HashSet`
each frame costs an allocation-free but O(n) sweep; a ring indexed by `tick % (MaximumRollbackFrames + 1)`
holding `tick + 1` (0 = empty) is O(1), allocation-free, and provably bounded, and de-duplication
stays exact because only ticks within seven frames of the current tick ever reach the recorder — a
tick exactly one window older is rejected upstream before it can collide. Pinned by
`CorrectedTickTrackingStaysBoundedAndStillDeduplicates` (3,000 ticks, a correction every frame,
`RetainedCorrectedTickCount` asserted `<= CorrectedTickCapacity` on every one, plus a duplicate-packet
no-op check).

**`DesyncDetected` now has a production consumer in `NetworkManager`.** `BeginRollback` subscribes,
`Disconnect` unsubscribes, and the handler increments `DesyncCount`, records `LastDesyncTick`, sets
`LastError`, and calls `GD.PushError`. The message text lives in a new engine-free
`RollbackDiagnostics.FormatDesync` so the wording is asserted without a Godot runtime. Full-state
resync remains Package 7; this only makes a desync visible instead of silent.

**Hazard/orb spawn cycles are crossed by a separate long run, not by the matrix sweep.** At High
frequency the first stage hazard spawns at frame 1,800 and the first orb at 660, so a run that
crosses both must exceed ~1,900 ticks. Doing that for 15 matchups × every geometry would dominate
the suite's runtime, so the matrix runs 420 ticks per (matchup, stage) and a dedicated
`LongRunConvergenceCrossesHazardAndOrbSpawnCycles` runs 2,200 ticks per stage for one construct-heavy
pair and asserts a hazard and an orb actually appeared. **Post-merge note for A2:** 2,200 was chosen
with ~300 frames of headroom precisely because A2's pre-match countdown (`MatchState = 0` for
180 + 30 frames) delays the spawn counters — `FighterHazardSystem`/`FighterOrbSystem` both return
early unless `MatchState == 1`, so their countdowns do not start until the match goes live. If A2's
countdown ends up longer than ~300 frames, raise the 2,200 constant.

**Post-merge re-checks the orchestrator should run.** (1) A1 adds nine geometries: the matrix test's
runtime grows ~5.5x — confirm the suite still finishes well inside the 300 s
`TestSessionTimeout`, and confirm `AuthoredGeometries()` really returns eleven (the test asserts
`> 1`, not a fixed count, by design). (2) A1's per-type hazard behaviours and A2's new component
fields change every state hash; this suite compares run-to-run and pins no hash literal, so it
should stay green — if it does not, the divergence is real. (3) A2's countdown means the sim is not
live at tick 0; hash-equality assertions are unaffected, but see the 2,200-tick note above.
(4) `docs/PERFORMANCE_BASELINE.md`'s rollback section is measured on Florence only and must be
refreshed by C1 across all ten stages.

**Worktree environment note (not a code defect).** In this git worktree roughly every other
`dotnet test` invocation fails to launch the GdUnit Godot child —
`GodotRuntimeTestRunner ends with exit code: 100` / `Failed to connect: Connection timeout` — and
reports only the 46 pure-C# tests. This reproduces on a clean stash of `main` (705, then 24), so it
predates this workstream. Every Godot launch also rewrites eight tracked
`addons/gdUnit4/**/*.import` files. The reliable recipe used here: run
`--headless --path <worktree> --quit`, then `git checkout -- addons assets localization resources`,
then `dotnet test`. Four full runs at **728 passed / 0 failed / Total 728** were obtained this way,
two of them consecutive. Do not mistake the 46-test result for a regression — the `Total:` is
unmistakably wrong, unlike the silent-suite-drop failure described in the first block.

### ORCHESTRATOR — Phase A integration (2026-08-08)

**INTEGRATION-A: merge order A1→A2→A3→A4 executed as declared; the two predicted overlaps resolved
cleanly.** `FighterSimulationComponents.cs` auto-merged (A1's `FighterHazardComponent` fields vs
A2's `FighterStateComponent`/`FighterMatchComponent` fields are disjoint regions);
`FighterSimulationDriver.cs` auto-merged with A3's CPU construction (match-seed derivation,
geometry, `FighterSimulationWorldObserver`) intact inside A2's restructured `Initialize` +
`AttachMatchFlowUI` flow — verified by inspection. The only conflicts were `docs/PACKAGE6_FIGHTER_PLAN.md`
§9 append blocks, union-resolved per §7.

**INTEGRATION-A: validation.** `dotnet build` clean (pre-existing vendored `CS8632` only);
`--headless --import` clean with no tracked-file churn; full suite **820 passed / 0 failed /
Total 820** across two consecutive serial runs (705 baseline + 32 A1 + 37 A2 + 23 A3 + 23 A4 — the
exact sum, no cross-workstream loss); TestArena and FighterStage_Florence headless smokes exit 0.

**INTEGRATION-A: the worktree GdUnit collision signature is confirmed by three independent agents
and is now a standing hazard for Phase B.** GdUnit4's runner pipe is named for the assembly
(`gdunit4-FightersThroughTime`), identical in every worktree; concurrent `dotnet test` runs
cross-connect and truncate each other (partial `Total:` with 0 failures and exit-code 100 /
`Failed to connect: Connection timeout` — NOT CLAUDE.md signatures 1/2/4; the log shows another
worktree's paths). Phase B agents are instructed to treat that signature as contention, retry, and
report numbers only from runs whose `Total:` matches the expected delta. C1 folds this into
CLAUDE.md's failure-signature table.

**INTEGRATION-A: A4's one-`[TestSuite]`-per-file rule is promoted to a Phase B instruction** (a
second suite in the same file is discovered but silently not executed — `Total:` moves by a
plausible-looking +1 instead of the real delta).

### B — Alexandria (2026-08-08)

**B-alexandria: the scene was authored against the merged `FighterStageGeometry.Alexandria` and
needed no deviation from it — walls ±9, sarcophagi at (−4, 1.6, 1.3) and (4, 1.6, 1.3), sinkhole
anchors {−6, 0, 6}, orb anchors (−4, 2.1), (4, 2.1), (0, 0.5).** A1 shipped the §4 dossier row
unchanged, so the pixel mirror is exact: platform bodies at (700, 600) and (1200, 600) with a
162.5×12 rect, orb markers at (700, 568.75), (1200, 568.75), (950, 668.75), hazard markers at
(575, 700), (950, 700), (1325, 700). `FighterStageAlexandriaTests.TheSceneMirrorsItsAuthoredFixedPointGeometry`
passed on the first run with no scene adjustment, which is the first independent confirmation that
A1's conformance validator works against a scene it did not help author.

**B-alexandria: `ParallaxBackground`/`ParallaxLayer` are marked obsolete in Godot 4.7 in favour of
`Parallax2D`, and the plan's §5.2 wording was followed anyway.** Referencing the deprecated types
from C# raises `CS0618`, which would have broken the §8 "no new warnings" gate, so the two
references in the per-stage test are wrapped in an explicit
`#pragma warning disable CS0618` with the reason inline. Rationale for not silently switching to
`Parallax2D`: nine Phase B stages are being authored in parallel against the same sentence, and C1
inherits nine scenes plus the ten VisualSet manifest rows — a uniform node type is worth more than
avoiding a suppressed warning, and the migration is one sweep in the Package 8 presentation pass.
**Any sibling stage agent that chose `Parallax2D` instead should be reconciled at integration; the
scenes should not ship split between the two.**

**B-alexandria: GdUnit4's `OverrideFailureMessage` throws `ArgumentException` on an empty string,
even when the assertion passes.** `AssertThat(errors.Count).OverrideFailureMessage(string.Join("; ",
errors)).IsEqual(0)` — the natural way to surface a validator's messages — fails the *passing* case,
because the override is evaluated eagerly. Every such message needs a non-empty constant prefix.
Flagged because the same idiom appears in the sibling per-stage suites and the failure mode looks
like a content bug rather than a harness bug.

**B-alexandria: the ground and wall colliders are dimensionally identical to Florence's, which is
correct rather than copy-paste drift.** Both stages author walls at ±9, so the ground rect is the
same 1125 px span and the wall bodies sit on the same x planes (387.5 / 1512.5). The wall rect keeps
Florence's 24×562 shape; only the wall's x plane is contract-relevant (`collision_mask = 0`
everywhere, the simulation is authoritative), so the half-pixel difference from the exact 562.5
floor-to-ceiling span is presentation, not geometry.

**B-alexandria: `--headless --import` rewrote the line endings of roughly forty unrelated tracked
`.import` files with zero content change, and those were reverted.** `git diff` reports them empty
while `git status` shows them modified (LF→CRLF). They are not part of this stage's change; only the
three new `assets/placeholders/stages/*.svg.import` artifacts are committed. Phase B siblings and C1
should expect the same churn and discard it rather than committing forty unrelated files.

**B-alexandria: the hash-identity run asserts it actually reached an active sinkhole, not merely
that it ran 2500 ticks.** Hazard type 7 is warning 90 + active 480 + recovery 60 and the first
High-frequency spawn is on the 1800-frame boundary, so a full cycle closes at 2430. The test tracks
`hazard.Phase == 1` across the run and fails if the active window was never observed — otherwise a
future change to the spawn interval would quietly turn a "full hazard cycle" test into a
2500-frame idle run that still passes.

**B-alexandria: validation.** `dotnet build` clean (only the pre-existing vendored `CS8632`);
`FighterStageAlexandriaTests` 8/8; full suite **828 passed / 0 failed / Total 828** (820 Phase A
baseline + 8), no contention signature on that run; `--headless --import` clean; a 300-frame
headless smoke of `res://scenes/fighter/FighterStage_Alexandria.tscn` exits 0 with an empty error
log.

### B — Globe (2026-08-08)

**B-globe: every merged `FighterStageGeometry.Globe` value is mirrored in the scene unchanged; no
geometry, hazard or catalog number was touched.** Walls ±9, the two gallery platforms
(∓5, 2.4, 1.4), the tiring-house balcony (0, 4.4, 1.2), hazard anchors {−5, 0, 5} and orb anchors
(∓5, 2.9) / (0, 4.9) convert to pixels exactly through §2.10, and
`FighterStageGlobeTests.GlobeSceneMirrorsItsAuthoredFixedPointGeometry` runs A1's shared conformance
validator against the instantiated scene. The A1 rule that a one-way platform body's *origin* is the
surface point (not the top edge of its 12 px collision rect) is what the three platform bodies are
authored to.

**B-globe: the three `HazardAnchors` markers sit at gallery/balcony height, not on the floor plane
Florence uses.** A1's validator compares hazard anchors on the X axis only
(`ValidateMarkerSet(..., compareYAxis: false)`), and the Globe hazard is thrown *from* the galleries
— a heckler marker at y = 0 would sit in the groundlings' pit, under the very platform the fruit
comes from. The markers are at (637.5, 520), (950, 395) and (1262.5, 520): just above each gallery
deck and the balcony, inside the authored gallery-box dressing. Nothing reads these markers at
runtime (they are documentation plus the conformance anchor set), so this is a readability choice,
not a behaviour change. Flagging it because sibling stages will most likely keep the Florence floor
convention and a reviewer diffing stages should not read this as drift.

**B-globe: `BackdropTint` is deliberately semi-transparent (alpha 0.55) rather than opaque like
Florence's.** `ParallaxBackground` is a `CanvasLayer` and its default layer is −100, so *any* opaque
layer-0 ColorRect painted behind the stage hides the parallax completely. Florence has no parallax,
so its opaque tint costs nothing; a stage that must ship "≥2 parallax layers" cannot have both an
opaque backdrop and a visible parallax. The tint is therefore what its name says — a purple era wash
the night sky and gallery tiers read through — and the viewport clear colour is the opaque base.
Phase B siblings hitting the same conflict should either do this or push the tint into its own
CanvasLayer below −100.

**B-globe: the per-stage test reaches `ParallaxBackground`/`ParallaxLayer` through the untyped
`Node` API instead of the C# bindings.** Both bindings carry `[Obsolete]` in Godot 4.7 (superseded
by `Parallax2D`), so `GetNodeOrNull<ParallaxBackground>` / `child is ParallaxLayer` add two `CS0618`
warnings to a build the gate requires to be warning-free. The scene still authors real
`ParallaxBackground`/`ParallaxLayer` nodes exactly as §5 item 2 specifies; only the test's type
references changed (`IsClass("ParallaxBackground")` and `child.Get("motion_scale").AsVector2().X`).
**If C1 or a later package migrates the stages to `Parallax2D`, this is the reason the plan's
wording and the engine's advice disagree — it is not an oversight.**

**B-globe: the wall collider is 562.5 px tall, where Florence rounds to 562.** Globe shares
Florence's ±9 walls and ceiling 9, so the exact floor-to-ceiling span is (9 − 0) · 62.5 = 562.5. The
conformance validator only checks wall X and layer, so both are conformant; the unrounded value was
used because there was no reason to introduce a half-pixel error.

**B-globe: the determinism run asserts the *behaviour* that makes the run meaningful, not just hash
equality.** Audience Heckle has no damaging region at all — it fires from per-fighter idle counters
stored on `FighterHazardComponent` — so a two-simulation hash comparison that never reaches the
active window would pass vacuously. `GlobeRunsIdenticallyAcrossTwoSimulationsThroughAFullHeckleCycle`
runs 2650 ticks (first spawn at 1800, warning 90, active 600, recovery 60) with hazards and orbs at
High, drives player one on a 40-frame pacing reversal so its idle counter can never reach 120, feeds
player two nothing at all, and asserts: warning/active/recovery phases all observed, the entity
despawned, the spawn landed on an authored anchor, `ActiveFrames == 600` and `Damage == 5`, the pacer
finished on full HP, and the camper lost a non-zero multiple of 5 — identical in both simulations.

**B-globe: the audio set puts `placeholder_stem_boss.tres` in the `ClimaxStem` slot, per A1's
deviation.** Fighter Mode has no boss; the shared placeholder kit's third stem is simply
Story-named. No asset was renamed (out of Phase B file ownership).

**B-globe: validation.** `dotnet build` clean (only the pre-existing vendored `CS8632`);
`FighterStageGlobeTests` 9 passed / 0 failed; `--headless --import` clean with no tracked-file
content churn (the `.import` sidecars Godot rewrote differed only in line endings and were restored);
`res://scenes/fighter/FighterStage_Globe.tscn` headless `--quit-after 300` smoke exits 0 with no
script errors. Full-suite numbers are in the commit message; per INTEGRATION-A, any run whose
`Total:` did not match 820 + 9 was treated as worktree pipe contention and retried.
