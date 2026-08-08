# Package 6 — Ten-stage local Fighter Mode completion: implementation plan

**Status:** Authored 2026-08-08 from a four-way reconnaissance of the stage system, match flow,
CPU/rollback infrastructure, and content pipeline. Baseline at authoring time: 705 passing tests,
Package 5 closed, one production Fighter stage (Florence Workshop), nine catalog entries silently
aliasing `TestArena.tscn`.

**Complete 2026-08-08.** All ten stages authored and routed; match flow, CPU and the rollback-readiness
gate closed. Final baseline **907 passing tests** across three consecutive runs. Read the C1 closeout
block at the end of §9 — including its "What Package 6 did NOT deliver" list — before treating any
part of Fighter Mode as finished.

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

### B — Gettysburg (2026-08-08)

**B-gettysburg: every merged `FighterStageGeometry.Gettysburg` value was authored into the scene
unchanged; no geometry, marker or anchor needed an adjustment.** Walls ±10 → x 325/1575, platforms
(−4.5, 1.8, 1.2) and (4.5, 1.8, 1.2) → body origins (668.75, 587.5) and (1231.25, 587.5) with
150 px collision rects, hazard anchors {−5, 0, 5} → x {637.5, 950, 1262.5}, orb anchors →
(668.75, 556.25), (1231.25, 556.25), (950, 668.75), spawns at (700, 700)/(1200, 700), ground rect
1250 px wide with its top edge exactly on the floor plane. Pinned by
`FighterStageGettysburgTests.TheStageSceneMirrorsItsAuthoredFixedPointGeometry`, which runs A1's
conformance validator — including the §9 A1 rule that a one-way platform body's **origin**, not its
collision-rect top edge, is the surface point.

**B-gettysburg: `ParallaxBackground`/`ParallaxLayer` are `[Obsolete]` in Godot 4.7 in favour of
`Parallax2D`, so naming them in C# adds `CS0618` warnings to a gate (§8.1) that allows none.** The
authored scene still uses the node types §5 item 2 specifies — they load and render fine, and
switching one stage to `Parallax2D` would have made this the odd scene out of nine for C1's sweeps.
The per-stage test therefore reaches them untyped: `GetNodeOrNull<CanvasLayer>` plus
`IsClass("ParallaxBackground")`, and `child.Get("motion_scale").AsVector2().X` for the scroll
factors. Build is back to the single pre-existing vendored `CS8632`. **Sibling Phase B agents who
referenced the typed API will each have contributed two `CS0618` warnings; C1 should either apply
this pattern across the nine suites or accept the warnings deliberately, not discover them at
integration.**

**B-gettysburg: `Presentation/BackdropTint` is translucent (alpha 0.38), where Florence's is
opaque.** `ParallaxBackground` is a `CanvasLayer`; at `layer = -100` it draws behind *everything* in
canvas layer 0, so an opaque `BackdropTint` at any `z_index` would have completely hidden the two
parallax layers this stage is required to have. Florence has no parallax, which is why its opaque
backdrop works. The node keeps the Florence-consistent path and now behaves as its name says — an
atmospheric tint over the backdrop rather than the backdrop itself. Any Phase B stage that authored
both an opaque `BackdropTint` and a negative-layer `ParallaxBackground` has an invisible parallax
and will still pass a node-existence test; worth a C1 spot-check.

**B-gettysburg: two extra placeholder SVGs were authored beyond the one preview §5 item 5 names.**
`assets/placeholders/stages/gettysburg_parallax_far.svg` and `gettysburg_parallax_near.svg` back the
two required parallax layers (§5 item 2 asks for "placeholder SVGs" but only itemizes the preview).
Reusing the shared `assets/placeholders/parallax_far.svg` was rejected: it is a purple/cyan night
sky that reads as neither Gettysburg nor the catalog palette, and every stage sharing it would
defeat "distinct at a glance". The far layer scrolls at 0.2 and the near at 0.55; the per-stage test
asserts ≥2 layers, distinct scroll factors, and that each layer actually carries a texture.

**B-gettysburg: the sight-line dressing is drawn stage-wide rather than as three separate
half-width-5 bands.** The hazard's authored `HalfExtents.x` is 5 units (625 px across), so three
bands centred on x {−5, 0, 5} would overlap into one full-width smear and read as a single flat
rectangle. The scene instead draws one full-width band at the hazard's true height (ground up to
1.5 units = y 606.25, the low horizontal band of §4.1 item 10), plus a 250 px scorch mark and a red
ranging stake at each of the three anchors so the individual aim points stay legible. This is
presentation only; the simulation's band is unchanged.

**B-gettysburg: the wall collider is 562.5 px tall, not Florence's rounded 562.** The ceiling is at
9 units, so the exact span is 562.5; the conformance validator checks wall X and layer only, so
either value passes. Using the exact number avoids a half-pixel lie in a scene whose whole purpose
is to mirror the fixed-point geometry.

**B-gettysburg: the hash-identity run needs a 2100-frame window, not the ~700 the A1 sweep uses.**
The first hazard spawns on the 1800-frame boundary and Gettysburg is the only 120-frame telegraph in
the catalog, so a full cycle does not close until 1800 + 120 + 30 + 60 = 2010. The test also asserts
it actually observed all three phases (warning, active, recovery) — a hash-identity run that never
reached the hazard would agree trivially and prove nothing.

**B-gettysburg: the suite ships 9 test cases against §5 item 6's six required coverage areas.** The
three additions are the scene's catalog identity (`StageID` resolving to the same geometry the
markers mirror), the collision contract (fences on layer 128 with `one_way_collision`, ground/walls
on 64, `collision_mask = 0` everywhere), and the presentation contract. The conformance validator
covers marker positions but not collision *layers*, and a fence authored on layer 64 would mirror
its geometry perfectly while silently becoming a solid floor.

**B-gettysburg: the GdUnit worktree pipe collision predicted by INTEGRATION-A cost two full test
attempts and is trivially confirmable.** Both showed `GodotRuntimeTestRunner ends with exit code:
100` / `Failed to connect: Connection timeout` / `No test matches the given testcase filter` — the
last line being the most misleading, since the filter was correct and the assembly did contain the
suite. `Get-Process testhost` showed five sibling runners alive at the time. Waiting for that count
to reach zero and re-running produced 9/9 immediately, and the full suite at **829 passed / 0 failed
/ Total 829** — exactly 820 + 9. Polling for a clear window is cheaper and far more reliable than
retrying blind.

### B — Nassau (2026-08-08)

**B-nassau: every merged `FighterStageGeometry.Nassau` value was authored into the scene unchanged
and the conformance validator passed on the first run — no scene or geometry adjustment was
needed.** Walls ±9, platforms (−4, 2.8, 1.8) and (4, 2.8, 1.8), hazard anchors {−5, 0, 5}, orb
anchors (−4, 3.3), (4, 3.3), (0, 0.5), spawn distance 4. Pinned by
`FighterStageNassauTests.TheSceneMirrorsItsAuthoredFixedPointGeometry`.

**B-nassau: Nassau's bounds are numerically identical to Florence's, so the ground and wall
colliders are byte-identical to Florence's — that is agreement, not copy-paste drift.** Both stages
author walls at ±9 and a ceiling at 9, which forces the same 1125×48 ground rect at (950, 700) and
the same 24×562 wall rects at x 387.5 / 1512.5. Only the platforms differ (Nassau's yards are
higher and wider: surface y 525 px, half-width 112.5 px against Florence's 550 / 100). The
conformance validator is checked against `FighterStageGeometry.Nassau` specifically, and A1's
`ValidatorRejectsAStageCheckedAgainstTheWrongGeometry` proves a wrong-stage check fails, so the
shared numbers cannot hide a mis-authored stage.

**B-nassau: `BackdropTint` is a translucent wash (alpha 0.42) over the parallax, not Florence's
opaque fill.** Florence has no `ParallaxBackground`, so its tint could be the backdrop itself. A
stage that layers a parallax behind an opaque full-screen ColorRect renders no parallax at all —
`ParallaxBackground` is a `CanvasLayer` and sits behind canvas layer 0 whatever its z-index. Full
screen coverage is instead guaranteed by an opaque `SeaBase` ColorRect parented directly to the
`ParallaxBackground` (a static, non-scrolling child), and the tint does what its name says. Stages
authored after this one should follow the same shape rather than copying Florence's opaque tint on
top of a parallax.

**B-nassau: the deck dressing (`Presentation/DeckProps`, `Presentation/MortarTargets`) sits at
negative z-index so it renders behind the fighters.** `FighterStageController._Ready` adds both
`PlayerController`s as children of the stage root at z 0; positive-z scenery would draw cannons and
targeting grids over the fighters. Nothing overlaps the ground visual, which starts at y = 700 while
all dressing ends there.

**B-nassau: the per-stage test checks the parallax structurally (`IsClass` +
`Get("motion_scale")`) instead of through the typed `ParallaxBackground`/`ParallaxLayer` C#
bindings.** Those bindings are `[Obsolete]` in Godot 4.7 (`Parallax2D` is the successor) and
referencing them adds two `CS0618` warnings to a build §8 requires to stay clean. The *scene* still
uses `ParallaxBackground` as §5 item 2 specifies — the node type is fully supported at runtime; only
the managed wrapper is deprecated. **The other eight Phase B stages will hit this the moment they
type `ParallaxBackground` in a test, and C1 should decide whether Package 6 migrates all ten stages
to `Parallax2D` or accepts the deprecated node for the placeholder pass.**

**B-nassau: the hash-identity run uses 12 stocks, not the production 3.** The first hazard spawns on
the 1800-frame High boundary, and `FighterHazardSystem.Update` returns immediately once
`MatchState != InProgress` — two fighters trading basics for those thirty seconds burn three stocks,
the match ends, and no mortar ever spawns. The first draft failed exactly that way with the
misleading message "the mortar never telegraphed". Dropping the attack inputs would have removed the
run's combat hash coverage instead, so the stock count moved and the test now asserts
`MatchState == InProgress` *before* the hazard assertions so the real cause is named if this ever
regresses. Stock count is not stage content; nothing in the shipped stage changed.

**B-nassau: flame colours are derived, not from the catalog triple.** The catalog authors
`BackgroundColor` (0.025, 0.08, 0.12), `GroundColor` (0.22, 0.12, 0.055) and `AccentColor`
(0.12, 0.72, 0.86) — a cold cyan accent that reads well as British Navy targeting optics but cannot
depict a burning deck. The accent drives every structural highlight (ground top line, yard top
lines, gunwales, all three target grids) as the plan intends; only the two deck fires and the
preview's blaze gradient use warm tones outside the triple. Replacing them is a texture swap, not a
code change.

**B-nassau: `--headless --import` in a fresh worktree rewrote 41 unrelated `.import` files and none
of that churn was committed.** Two causes, neither related to this stage: Godot rewrites the files
with CRLF endings (git reports them modified with an empty textual diff), and the eight
`addons/gdUnit4/**` entries additionally re-cased their `source_file` from `addons/gdunit4/` to
`addons/gdUnit4/` and re-hashed their `.godot/imported/` paths, which is a fresh-cache artifact of
the worktree. All 41 were restored; the only import artifact committed is the new
`assets/placeholders/stages/nassau_preview.svg.import`. Other Phase B agents should expect the same
and restore rather than commit, or the nine merges will conflict on files none of them touched.

**B-nassau: the preview SVG is a 480×270 thumbnail, not a full 1920×1080 plate.** §5 item 5 asks for
a "distinct silhouette + era palette" for the stage-select card; authoring it at card resolution
keeps it legible when C1 scales it down and keeps the placeholder small. The test asserts only that
it imports as a `Texture2D` with non-zero dimensions, so a production plate at any resolution
replaces it without touching the test.

**B-nassau: GdUnit worktree contention cost six of nine test invocations.** Confirming
INTEGRATION-A's standing hazard: `exit code: 100` / `Failed to connect: Connection timeout` /
`The server returned an unexpected status code`, each time with `No test matches the given testcase
filter` despite the filter being correct and the assembly built. No `godot.log` FATAL, no negative
exit code. Retrying with a short backoff eventually produced a clean run; only clean runs are
reported below.

### B — Orléans (2026-08-08)

**Every §4 dossier number for `orleans_vanguard` shipped unchanged and the scene mirrors the merged
`FighterStageGeometry.Orleans` exactly.** Walls ±9, platforms (−3.5, 2.0, 1.4), (3.5, 2.0, 1.4),
(0, 4.0, 1.4), hazard anchors {−8, 8} and orb anchors (−3.5, 2.5), (3.5, 2.5), (0, 4.5) convert to
pixels through §2.10 with no rounding residue — every value lands on a clean multiple of 0.25 px —
so `FighterStageConformance.Validate` returns an empty issue list with no epsilon slack consumed.
Pinned by `FighterStageOrleansTests.TheSceneMirrorsItsAuthoredFixedPointGeometry`.

**`BackdropTint` lives at `Presentation/Backdrop/BackdropTint` — one `CanvasLayer` deeper than
Florence's `Presentation/BackdropTint` — because a `ParallaxBackground` is itself a `CanvasLayer`.**
To sit behind the stage the parallax must be on a negative layer, and *any* layer-0 `ColorRect` then
draws in front of it regardless of `z_index`, hiding the whole backdrop. Florence has no parallax so
the conflict never arose there. The tint therefore sits on its own `CanvasLayer` at layer −2 with the
parallax at −1. The contract does not name `BackdropTint`, so this is a presentation-convention
divergence, not a contract violation; the per-stage test pins the new path. **The other eight Phase B
stages hit the same problem the moment they add a parallax — copying Florence's flat layout silently
produces an invisible parallax that no test catches.**

**`ParallaxBackground`/`ParallaxLayer` are deprecated in Godot 4.7.1 in favour of `Parallax2D`, and
were used anyway because §5 deliverable 2 names them explicitly.** The nodes still function; the cost
is a `CS0618` wherever C# references the types, scoped here with `#pragma warning disable CS0618`
around the one block in `FighterStageOrleansTests` that reads `MotionScale`. The `.tscn` itself
produces no warning. Nine stages doing this independently means nine identical suppressions — C1
should decide whether to migrate all ten stages to `Parallax2D` in one pass rather than leaving the
suppression scattered.

**A second placeholder SVG, `assets/placeholders/stages/orleans_parallax.svg`, was authored beyond
the single preview image §5 deliverable 5 asks for.** The only existing parallax placeholder,
`assets/placeholders/parallax_far.svg`, is a purple-sky asset already used as the generic prototype
backdrop; reusing it would have made Orléans read as "generic prototype stage" and failed the stated
"distinct at a glance from every other stage" bar. Both new SVGs are imported and their `.import`
sidecars are committed.

**The hazard-cycle assertion is written against observed behaviour, not the dossier's active window.**
At the authored 0.09 units/*frame* (A1's per-frame convention), the debris crosses the 18-unit stage
in roughly 178 frames, inside the 240-frame active window — so the despawn that ends the active phase
is the far-wall check in `AdvanceActiveHazard`, never the timer. The two-simulation run therefore
covers 2400 ticks (spawn on the 1800-frame High boundary, 90-frame warning, the full roll, 60-frame
recovery) and asserts both that the warning phase was observed and that the debris actually
translated more than one unit from its spawn anchor, so a future change that leaves the boulder
parked still fails.

**`AssertBase.OverrideFailureMessage("")` throws `ArgumentException` inside GdUnit4.** The idiom
`OverrideFailureMessage(string.Join("; ", errors))`, copied from an existing suite, fails the test on
the *success* path when the error list is empty. Every override message in this suite is prefixed
with a literal. Worth knowing before it costs another Phase B agent a run.

**No shared file was touched.** `stage_catalog.tres`, `content_manifest.csv`,
`scene_pool_catalog.tres`, `SceneSmokeTests.cs`, `localization/en.csv`, `scripts/FighterSim/**` and
`scripts/Core/**` are unmodified; the scene is not routed at runtime until C1 wires the catalog
`ScenePath`, and the per-stage test loads it directly by path.

### B — Berlin (2026-08-08)

**B-berlin: every merged `FighterStageGeometry.Berlin` value is mirrored in the scene unchanged; no
geometry, dossier or shared file was touched.** Walls ±10 → x 325/1575, the snowy street's collision
top edge at y = 700 and 1250 px wide, the two guard-tower balconies at (−6.5, 3.6) / (6.5, 3.6) →
body origins (543.75, 475) and (1356.25, 475) with 162.5 px collision rects, spawns at ∓4, orb
markers at (543.75, 443.75) / (1356.25, 443.75) / (950, 668.75), searchlight anchors at
x 700/950/1200. Pinned by `FighterStageBerlinTests.SceneMirrorsItsAuthoredFixedPointGeometry`
through A1's shared validator, including A1's rule that a one-way platform body's **origin** — not
its collision rect's top edge — is the surface point.

**B-berlin: the two-simulation hash-identity run uses movement-only inputs, and asserts the match is
still live at the end.** The obvious pattern (the `InputFor` helper in `FighterStageGeometryTests`,
which presses `BasicAttack` every 31 frames) KOs a fighter well inside the 2500 frames this test
needs, and `FighterHazardSystem.Update` returns immediately when `MatchState != 1`. The searchlight
then froze mid-active: the first version of this test saw warning and active but never recovery — a
real failure that would otherwise have been "fixed" by deleting the assertion. The Berlin input
schedule presses only move/jump/dash/roll, and
`AssertThat(first.GetMatchState().MatchState).IsEqual(1)` makes a match that ends early fail loudly
instead of turning the hazard assertions vacuous. **Any later stage whose hash run crosses a hazard
cycle needs the same treatment.**

**B-berlin: the run covers frames 0–2499, which is the searchlight's entire life.** Spawn on the
High-frequency 1800-frame boundary, 90-frame warning, the full 480-frame active window (the whole
dwell-counter sweep — the longest active window of any hazard except Globe), the 60-frame recovery,
and the despawn at 2430 with no successor before 3600. The test asserts all four transitions were
observed and that the column never leaves one of the three authored anchors.

**B-berlin: the beams deliberately do not reach the balconies, and that is the stage's identity, not
an authoring miss.** The hazard anchors are {−4, 0, 4} while the balconies sit at ±6.5, so a fighter
who commits to a guard tower is outside every searchlight column but also out of the fight and slow
to rotate back — the risk/reward the dossier's wide ±10 walls set up. Flagging it so nobody "fixes"
the anchors to sit under the platforms.

**B-berlin: a second placeholder SVG was authored beyond the required preview.**
`assets/placeholders/stages/berlin_parallax_skyline.svg` carries the mid parallax layer (a flat
East-Berlin concrete skyline with a transparent sky, so the shared `parallax_far.svg` reads through
it at a slower scroll factor). §5 item 5 only requires `berlin_preview.svg`, but a stage reusing the
shared far background at both scroll factors would not be "distinct at a glance", and C1's VisualSet
manifest row wants per-stage parallax assets anyway. Both SVGs are imported;
`PreviewImageIsAnImportedTexture` loads the preview as a `Texture2D` rather than only checking that
the file exists, because an un-imported SVG loads as null and would ship an empty stage-select tile.

**B-berlin: the scene has five `StaticBody2D`s, not four.** Three solid (street + two walls) plus the
two one-way balconies. Recorded because the per-stage collision-contract test asserts the count
exactly, and that count is the cheapest way to catch a body that lost its layer.

**B-berlin: the GdUnit worktree pipe contention predicted by INTEGRATION-A is real and was the
dominant cost of this stage.** Six consecutive `dotnet test --filter` invocations produced
`GodotRuntimeTestRunner ends with exit code: 100` / `Failed to connect: Connection timeout` with no
Godot process of mine alive and nothing in `godot.log`, while an unrelated filter
(`StageAudioSetTests`) ran clean 3/3 in the same window — proof the failure is the shared pipe, not
the content. Every number reported for this stage comes from a run whose `Total:` matched the
expected count.

### B — Chicago (2026-08-08)

**B-chicago: every merged `FighterStageGeometry.Chicago` value was authored into the scene
unchanged; nothing needed a conformance adjustment.** Walls ±10, platforms (−6, 3.2, 1.5) and
(6, 3.2, 1.5), the single hazard anchor at 0 and the three orb anchors all convert cleanly under
§2.10 (`pixel = (950 + x·62.5, 700 − y·62.5)`), including the half-pixel values the ±1.5 half-width
produces — the collision rect is 187.5 px wide and the wall bodies sit at exactly 325 / 1575.
Pinned by `FighterStageChicagoTests.TheChicagoSceneMirrorsItsAuthoredFixedPointGeometry`, which
calls A1's shared validator, plus `.EverySolidBodyIsPresentationOnlyWithNoCollisionMask` for the
layer/mask half of the contract that the validator does not cover for the ground and walls.

**B-chicago: the hash-identity run uses nine stocks, not the production three, and that is
load-bearing rather than cosmetic.** The plan asks for a two-simulation run "crossing a full hazard
cycle": Chicago's induction grid spawns on the High-frequency 1,800-frame boundary and then runs
warning 90 → active 360 → recovery 60, so the cycle only closes at frame 2,310. With three stocks
the scripted input pattern (basic attacks every 29 frames) exhausted a fighter's stocks first,
`FighterMatchSystem` set `MatchState = 2`, and `FighterHazardSystem` — which returns immediately
unless `MatchState == 1` — stopped advancing the hazard. The test would then have passed *vacuously*
on hash equality while never observing a hazard at all. Nine stocks keeps the match live through
frame 2,450, and the test now asserts liveness, that the grid actually spawned on the authored
central anchor, that all three phases were observed, and that the hazard despawned — so it cannot
regress back into a vacuous pass. **Later stage agents: assert `GetMatchState().MatchState == 1` at
the end of any long determinism run, or raise the stock count.**

**B-chicago: Godot 4.7 marks the `ParallaxBackground` / `ParallaxLayer` C# classes obsolete in
favour of `Parallax2D`, so the scene keeps them (plan §5 item 2) but the test reads them untyped.**
Referencing either type from C# raises `CS0618`, which would have added two new build warnings and
failed §8 gate 1 ("no new warnings"). `TheChicagoPresentationIsDressedForTheWorldsFair` therefore
fetches the node as a `CanvasLayer`, asserts `GetClass() == "ParallaxBackground"`, and reads each
layer's scroll factor through `child.Get("motion_scale")`. The nodes themselves work correctly —
the headless smoke of the scene exits 0. **This is a repo-wide decision C1 should make once:**
either all ten stages stay on the deprecated node (and Package 8 migrates them together), or the
plan's wording moves to `Parallax2D`. Nine Phase B agents authoring against §5 will all hit this.

**B-chicago: `AssertBase.OverrideFailureMessage("")` throws `ArgumentException`, so the
`AssertThat(errors.Count).OverrideFailureMessage(string.Join("; ", errors)).IsEqual(0)` idiom in
`ScenePoolConfigTests` fails on the *passing* path.** GdUnit4 evaluates the override message before
the assertion, and an empty error list joins to `""`. The first draft of the pool-config test copied
that idiom straight from `ScenePoolConfigTests` and failed with
`The value cannot be an empty string. (Parameter 'message')` rather than with any real budget
problem. The working form is the one `FighterStageConformanceTests` already uses:
`if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("")`. `ScenePoolConfigTests`
itself is safe only because its own messages always carry a non-empty path prefix — worth a sweep at
closeout, and worth knowing before eight sibling agents copy the same line.

**B-chicago: two era parallax SVGs were authored alongside the required preview, and the manifest's
`visual_stage_chicago` row still points at the shared `assets/placeholders/parallax_far.svg`.**
§5 item 2 asks for "placeholder SVGs ... at distinct scroll factors"; reusing the one shared far
backdrop with a tint would have left the nine stages distinguishable only by colour. The stage ships
`assets/placeholders/stages/chicago_parallax_far.svg` (the White City at night with the 1893 Ferris
Wheel) and `chicago_parallax_near.svg` (colonnade plus the electrical gantry), at motion scales 0.15
and 0.40. C1's §6 item 4 manifest pass should repoint `visual_stage_chicago` at these two plus
`chicago_preview.svg`; this workstream does not touch the manifest (§2.7).

**B-chicago: the stage's presentation dresses the *hazard's real dimensions*, not a decorative
guess.** `FighterHazardSpec.For(3)` authors half-extents (4, 2) centred on the anchor at y = 0, so
`InductionSpan` draws a 500 × 125 px field (±4 units wide, 2 units tall) with rails at its edges,
plus the coil-to-coil discharge arc between the two platform towers at ±6. A player reading the
stage sees where the grid will damage them.

**B-chicago: this worktree was branched from the pre-Phase-A `main` and had to merge `main`
(`1e1c674`) before any work could start.** `docs/PACKAGE6_FIGHTER_PLAN.md`, `FighterStageGeometry`'s
nine new entries, `FighterStageConformance` and `StageAudioSet` were all absent at branch time.
Flagging it because a Phase B agent that did not notice would have authored against the §4 table
instead of the merged code, with no validator to check itself against.

**B-chicago: validation.** `dotnet build` clean (pre-existing vendored `CS8632` only);
`--headless --import` regenerated only the three new SVG import artifacts, with no content change to
any tracked `.import`, to `en.csv` or to the manifest translation; the scene's `--quit-after 300`
headless smoke exits 0 with no script errors; `--filter "FullyQualifiedName~FighterStageChicago"`
reports **9 passed / 0 failed / Total 9**; the full suite reports **829 passed / 0 failed /
Total 829** — exactly the post-A baseline of 820 plus this workstream's 9, with no cross-workstream
loss.

**B-chicago: the GdUnit worktree contention has a second shape that a `Total:` check alone does not
catch, and Phase B agents need both.** With eight sibling testhosts alive, a *filtered* run loses
the pipe race and prints no `Total:` at all
(`GodotRuntimeTestRunner ends with exit code: 100` / `Failed to connect: Connection timeout` /
`No test matches the given testcase filter`) — obvious. A *full* run that loses the same race
instead prints `Passed! - Failed: 0, Passed: 73, Total: 73` in 12 s: a plausible-looking green
result carrying only the pure-C# tests (46 in A4's worktree, 73 now that A1–A4's engine-free suites
landed). Four consecutive attempts here returned 73 before the fifth returned the real 829. **Never
accept a full-suite number without comparing it to the exact expected total** — "Passed!" and a
non-trivial count are both worthless on their own. C1 should add the 73-variant to CLAUDE.md's
failure-signature table alongside the 24- and 46-test ones.

### B — Paris (2026-08-08)

**B-paris: every merged `FighterStageGeometry.Paris` value was authorable in pixels exactly; the
scene needed no geometry compromise and no §4 dossier number moved.** Walls ±9, platforms
(−4, 2.6, 2.0) and (4, 2.6, 2.0), hazard anchors {−6, 0, 6} and orb anchors (−4, 3.1), (4, 3.1),
(0, 0.5) all land on clean pixel coordinates through `pixel = (950 + x·62.5, 700 − y·62.5)` — the
drawbridge surfaces sit at y = 537.5 px with 250 px-wide colliders, the searchlight emitters at
x = 575/950/1325. `FighterStageParisTests.SceneMirrorsItsAuthoredFixedPointGeometry` runs A1's
shared conformance validator, and a second test pins the collision hygiene the validator does not
cover (every `Geometry` child is a `StaticBody2D` with `collision_mask = 0`, three on layer 64 and
exactly `Platforms.Length` on layer 128).

**B-paris: the hash-identity run needs 25 stocks, not the default 3, and that is a property of the
hazard system rather than of Paris.** `FighterHazardSystem.Update` returns immediately when
`MatchState != 1`, so a match that ends *before* the hazard's active window leaves the hazard frozen
in its warning phase forever. The first hazard spawns on the 1800-frame High boundary; a scripted
2340-tick run with attacks in the input pattern burns three stocks by tick ~1828, which silently
produced a run where the beam existed for 541 frames and never once reached `ActivePhase`. Raising
the stock count keeps the match live across the full spawn → warning 90 → active 360 → recovery 60
cycle while keeping combat in the input stream. The test now also asserts
`MatchState == FighterMatchStates.InProgress` at the end with an explicit message, so the failure is
loud rather than a vacuously green hazard assertion. **The other eight Phase B agents should check
their own hash-identity runs for the same trap** — an assertion that only checks "no desync" passes
happily on a dead match with a frozen hazard.

**B-paris: the scene uses `ParallaxBackground`/`ParallaxLayer` as the plan specifies, but the test
reaches them by class name instead of by their C# types.** Both are `[Obsolete]` in the Godot 4.7.1
bindings ("Use the 'Parallax2D' node instead"), so a typed `GetNodeOrNull<ParallaxBackground>` emits
CS0618 and breaks §8's no-new-warnings gate. The node types are unchanged in the `.tscn` — only the
test avoids naming them, via `GetClass()` and `Get("motion_scale")`. A headless `--quit-after 300`
smoke of the scene exits 0 with no engine-side deprecation output, so the nodes are functional, not
stubs. **C1 should decide once whether all ten stages migrate to `Parallax2D`**; this agent did not
migrate unilaterally because the plan names `ParallaxBackground` and eight sibling agents author
against the same text.

**B-paris: `BackdropTint` is a translucent colour grade over the parallax, not the opaque backdrop
Florence uses.** A `ParallaxBackground` is a `CanvasLayer`, and a negative-layer CanvasLayer draws
behind *everything* in layer 0 — including a full-bleed opaque `ColorRect`. Keeping Florence's
opaque tint would have hidden the parallax entirely. The Paris tint is the catalog
`BackgroundColor` at alpha 0.42 over an opaque far-parallax SVG, which grades the backdrop toward
the era palette instead of erasing it. Any Phase B stage that pairs the two nodes has the same
ordering constraint.

**B-paris: `GdUnit4.Asserts.AssertBase.OverrideFailureMessage` throws
`ArgumentException("The value cannot be an empty string")` on an empty message.** The natural
`AssertThat(errors.Count).OverrideFailureMessage(string.Join("; ", errors)).IsEqual(0)` pattern
therefore *fails* precisely when the content is correct and the error list is empty. Use the
existing repository idiom instead —
`if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("")` — or guard the message.
Worth flagging because the resulting failure reads like a content bug.

**B-paris: the pool config is asserted equal to the Test Arena's warm-up and capacity totals rather
than merely "valid".** Plan §5 item 3 says "mirroring `test_arena_pool_config.tres`'s pool
set/budgets"; a validity-only check would pass a config that quietly halved a budget. The test
compares `GetWarmUpInstanceCount()` and `GetMaxCapacityCount()` against the Test Arena config
directly, so the two can only drift on purpose. Catalog wiring for `fighter_stage_paris_pools`
remains C1's.

**B-paris: the first `--headless --import` in a fresh worktree rewrites ~40 unrelated `.import`
files with line-ending-only changes.** `git diff` reports no content difference; the churn is CRLF
normalization on files Git would re-normalize anyway. They were reverted with `git checkout --`
before committing, so the Paris commit carries only its own three new `.svg.import` sidecars. Phase
B agents should read `git status` after `--import` rather than committing the sweep.

### B — Vesuvius (2026-08-08)

**B-vesuvius: every merged geometry number was authored unchanged; the scene needed no adjustment
and `FighterStageGeometry.Vesuvius` was not touched.** Walls ±8 → pixels 450/1450, ground rect
1000×48 with its top edge on y = 700, ledges at (668.75, 575) and (1137.5, 481.25) with 137.5 px
collision widths, spawns at 700/1200, three orb markers, four hazard markers at 575/825/1075/1325.
`FighterStageConformance.Validate` passes with an empty issue list.

**B-vesuvius: the "hazards High, one full cycle" hash-identity run needed a deep stock pool, and
the reason is a real constraint every Phase B stage agent will hit.** `FighterHazardSystem.Update`
returns immediately unless `match.MatchState == 1`, so a match that *ends* freezes the live hazard
wherever it stood. The first hazard spawns on the 1800-frame boundary and Vesuvius' cycle runs
warning 90 → active 240 (60 frames of fall, then the 180-frame time-dilation pool) → recovery 60,
finishing at ~2190. Scripted adversarial inputs over that window burn roughly one stock per 300
frames, so with the default three stocks both fighters were out at ~2100 and the hazard sat in the
active phase forever — the test's "saw recovery" assertion was unreachable and looked like a hazard
bug. The suite now constructs both simulations with `stocks: 20`. Measured phase transitions with
that pool (headless probe, seed 605): warning at t=1799, active at t=1888, pool sub-state at impact,
recovery at t=2129, despawn at t=2189. **A Phase B stage whose test runs adversarial inputs past
frame ~1800 on the default three stocks is measuring a dead match, not a hazard cycle.**

**B-vesuvius: the parallax tree is inspected by class name and property, not by typed reference.**
Godot 4.7 marks `ParallaxBackground`/`ParallaxLayer` `[Obsolete]` in favour of `Parallax2D`, so
`GetNodeOrNull<ParallaxBackground>` and `child is ParallaxLayer` each emit `CS0618` — two new build
warnings, which §8 gate 1 forbids. The plan (§5 item 2) specifies `ParallaxBackground` for all nine
stages and the node still works, so the *scene* uses it and the *test* reads
`GetClass() == "ParallaxBackground"` / `Get("motion_scale")` instead. The build stays at the single
pre-existing vendored `CS8632`. Flagging it because the other eight stage agents will hit the same
warning; C1 may want to decide whether Package 6 or a later package migrates all ten stages to
`Parallax2D`.

**B-vesuvius: the era slope is cosmetic and drawn *behind* the flat floor, deliberately.** The
dossier calls for "slanted rocky slope dressing" but the simulation floor is a flat solid plane at
y = 0 across the whole ±8 span. `Presentation/CalderaSlope` is a `Polygon2D` at `z_index = -8` so
the `Geometry/Ground` visual always draws over it; nothing in the scene implies a walkable slope.

**B-vesuvius: three SVGs were authored under a new `assets/placeholders/stages/` directory** —
`vesuvius_preview.svg` (the §5 item 5 preview), plus `vesuvius_sky.svg` and `vesuvius_ridge.svg`
for the two parallax layers at scroll factors (0.15, 0.06) and (0.45, 0.18). The manifest's
`visual_stage_vesuvius` row still points at the shared `parallax_far.svg`; repointing it is C1's
job per §2.7/§6 item 4.

**B-vesuvius: validation.** `dotnet build` clean (one pre-existing vendored `CS8632`);
`FighterStageVesuviusTests` 9 passed / 0 failed; full suite 829 passed / 0 failed / **Total 829**
(820 Phase A baseline + 9); `--headless --import` clean with no tracked-file churn beyond the three
new SVG `.import` sidecars; `FighterStage_Vesuvius.tscn` headless smoke `--quit-after 300` exits 0
with no script or resource errors. The GdUnit cross-worktree pipe collision predicted by
INTEGRATION-A was hit repeatedly during this workstream (`exit code: 100` /
`Failed to connect: Connection timeout` / `No test matches the given testcase filter` on a filter
that plainly matches); every number above is from a run whose `Total:` matched the expected delta.
A temporary headless probe scene was used to diagnose the stock-exhaustion issue above while the
pipe was contended; it was deleted before commit.

### ORCHESTRATOR — Phase B integration (2026-08-08)

**INTEGRATION-B: all nine stage branches merged; purely additive as designed.** The only conflicts
were this file's §9 appends (union-resolved). Each agent independently merged the post-Phase-A
`main` into its stale worktree base before starting, so every branch built against the merged
geometry and hazard code.

**INTEGRATION-B: validation.** `dotnet build` clean (pre-existing vendored `CS8632` only);
`--headless --import` clean (line-ending-only churn on the new SVG sidecars discarded); full suite
**901 passed / 0 failed / Total 901** across two consecutive serial runs — exactly the 820 Phase A
baseline + 81 per-stage tests (Alexandria 8, Nassau 10, the other seven 9 each). Every stage agent
also reported a clean scene smoke (`--quit-after 300` exit 0) from its worktree.

**INTEGRATION-B: items accumulated for C1, from the per-stage §9 blocks:**
1. `ParallaxBackground`/`ParallaxLayer` are `[Obsolete]` in Godot 4.7 (superseded by `Parallax2D`).
   All nine scenes use the node type §5 specified; all nine test suites deliberately read it
   untyped so the build carries no new `CS0618`. C1 decides once: keep for the placeholder pass
   (recommended — migration is presentation work that belongs with Package 8) and record the
   decision, or migrate all ten scenes now.
2. Backdrop-tint-over-parallax: a `ParallaxBackground` is a negative-layer `CanvasLayer`, so an
   opaque layer-0 `BackdropTint` hides it. Each agent solved this locally (translucent tint or an
   opaque base inside the parallax); C1 spot-checks all nine scenes for an actually-visible
   parallax.
3. `visual_stage_*` manifest rows still point at the shared `parallax_far.svg`; C1 §6.4 repoints
   them at the per-stage assets.
4. The vacuous-hazard-test trap (match ends → `FighterHazardSystem` freezes → phase assertions
   never run) was independently found and guarded by the stage agents; every merged suite asserts
   observed phases and/or match liveness.
5. New failure signatures for CLAUDE.md: the cross-worktree GdUnit pipe contention (partial
   `Total:` with 0 failures, exit code 100 / `Failed to connect: Connection timeout` /
   `All pipe instances are busy` / a sibling worktree's paths in godot.log; also masquerades as
   `No test matches the given testcase filter` and as a plausible-looking 12-second
   `Passed! Total: 73` pure-C#-only run), the one-`[TestSuite]`-per-file rule, and the
   `OverrideFailureMessage("")`-throws footgun. Draining `testhost` processes before running
   proved more reliable than blind retries.

### C1 — Closeout (2026-08-08)

**C1: what was wired.** All ten catalog entries now carry their own `ScenePath` and
`PreviewTexturePath` and are flagged `ProductionReady`; ten `scene_pool_catalog.tres` rows map each
`scenes/fighter/*.tscn` to its own pool config; ten rows entered `SceneSmokeTests`; 32
`content_manifest.csv` rows were flipped (nine FighterStage rows to
`Implemented/ReadyForReplacement/Valid`, ten AudioSet stage rows to `Implemented/Placeholder/Valid`,
ten VisualSet stage rows repointed from the shared `parallax_far.svg` to each stage's own preview
plate, and the `local_pause` / `match_results` UIScreen rows, whose scenes A2 authored but
deliberately left `Planned`). Three Florence artifacts that had never existed were authored:
`resources/Pools/fighter_stage_configs/fighter_stage_florence_pool_config.tres`,
`resources/Audio/stage_florence_audio.tres`, and
`assets/placeholders/stages/florence_preview.svg`.

**C1: Florence's FighterStage manifest row was flipped to `ReadyForReplacement` too, which §6 item 4
did not ask for.** The plan says "the nine". Leaving Florence at `Placeholder` while its nine
siblings read `ReadyForReplacement` would have been drift a reviewer reads as a miss: Florence is
exactly what `ReadyForReplacement` means — a contract-conformant placeholder that production art
swaps out by reassignment. No test pinned the old value.

**C1: `ContentManifestTests.PlannedResourcesRemainVisibleUntilTheyAreAuthored` still has honest
feeders and was not retired.** After this pass the remaining `Planned`/`Pending` rows are the
seventeen per-level AudioSet rows (`audio_tutorial`, `audio_hub`, `audio_level_01..15`), ten UIScreen
rows (`story_hud`, `dialogue_box`, `resonance_grid`, `network_select`, `story_pause`, `online_pause`,
`settings`, `story_loading`, `fighter_loading`, `network_error`) and three DialogueSet rows. Its own
doc comment says to retire it rather than invent a feeder if the premise ever goes; the premise has
not gone.

**C1: four of the nine Phase B scenes shipped with an invisible parallax, and the per-stage tests
could not see it.** INTEGRATION-B item 2 asked C1 to spot-check this, and the check found real
breakage rather than confirming the claim. Alexandria, Berlin, Chicago and Vesuvius each pair a
negative-layer `ParallaxBackground` with a **fully opaque** full-bleed `Presentation/BackdropTint`
`ColorRect` in canvas layer 0. A `CanvasLayer` at a negative layer draws behind *everything* in layer
0 regardless of `z_index`, so their two required parallax layers rendered to nothing. Every per-stage
suite still passed, because all of them assert node existence and scroll factors, not visibility.
The fix keeps each scene's node paths: the tint drops to alpha 0.45 (the pattern Paris, Gettysburg
and Globe already used) and an opaque full-bleed `BackdropBase` `ColorRect` becomes the
`ParallaxBackground`'s first child, so screen coverage no longer depends on the viewport clear colour
(Nassau's `SeaBase` pattern). Chicago's suite pinned the opaque colour literal and now asserts the
RGB triple plus `A < 1`. **New guard:**
`FighterStagePresentationTests.NoStagePairsAnOpaqueLayerZeroBackdropWithAParallaxBackground` sweeps
the catalog, tolerates Orléans' deeper-CanvasLayer solution, and refuses to pass vacuously (it
asserts at least nine stages actually carry a parallax). Orléans, Nassau, Paris, Gettysburg and Globe
were correct as authored.

**C1: `ParallaxBackground`/`ParallaxLayer` are KEPT for the placeholder pass; the `Parallax2D`
migration is Package 8's.** INTEGRATION-B item 1 asked for one decision, and five Phase B agents
independently asked for it. Rationale: the deprecated nodes function correctly (all ten scenes smoke
clean at `--quit-after 300`), all nine suites already read them untyped so the build carries no new
`CS0618`, a uniform node type across ten scenes is worth more than removing a suppression, and
swapping the node type is a presentation-layer change that belongs with the pass that replaces the
placeholder art on top of it. **Do not migrate one stage in isolation.** When Package 8 migrates,
migrate all ten together and update `FighterStagePresentationTests` and the nine per-stage suites in
the same change — `Parallax2D` is a `Node2D`, not a `CanvasLayer`, so the opaque-backdrop invariant
above changes shape (z-order within layer 0 becomes the ordering rule) and the guard must be
rewritten, not deleted.

**C1: the rollback-readiness gate PASSES, and the timing measurement now sweeps all ten stages
rather than Florence alone.** A4's harness enumerated *geometries* dynamically for the convergence
matrix but hardcoded `FighterStageGeometry.Florence` for the depth-7 timing test, so the recorded
numbers were one stage's. `WorstCaseDepthSevenResimulationFitsTheRollbackBudget` now loops
`FighterStageGeometry.AllAuthored` through an extracted `MeasureWorstCaseDepthSeven` helper that
asserts the per-stage budget and returns its statistics; the case count is unchanged (one case, ten
measurements), and a stage added later is measured with no edit. Measured on the C1 machine, 300
corrections per stage after a 1,900-tick warm-up, Tesla vs Mozart with items and hazards High:
worst median **0.058 ms** (`globe_theatre`), worst p95 **0.104 ms** (`orleans_vanguard`), worst max
0.357 ms (`nassau_flagship`), largest full-state snapshot **2,699 bytes** (`globe_theatre`), **zero**
budget breaches over 3,000 measured corrections against the 8.000 ms budget — roughly 140x headroom
on the worst stage. The convergence matrix (nine mirrors plus six cross-pairs against all eleven
authored geometries = 165 runs) and the long hazard/orb-crossing runs pass; the whole 23-case gate
finishes in about 13 s, comfortably inside the 300 s `TestSessionTimeout`. The per-stage table is in
`docs/PERFORMANCE_BASELINE.md`. **This is Package 7's entry criterion and it is met** — with the
standing caveat that it is one Windows Debug machine over an in-memory transport, so the platform and
real-transport gates remain open.

**C1: the spread across stages is under 0.012 ms of median, so hazard identity is not a cost driver
at this entity scale.** Worth recording because it means Package 7 does not need per-stage rollback
budgets; one budget covers the catalog.

**C1: stage select shows the preview plate and no longer brands production stages as prototypes.**
`CharacterSelectScreen` gained a `TextureRect` beside the stage `OptionButton`, refreshed on
`ItemSelected`, hidden when a stage has no importable `PreviewTexturePath` so an unauthored stage
degrades to the era colours rather than a blank hole. The `fighter_stage_prototype` tooltip line is
now appended only when `!stage.ProductionReady`; it used to be appended unconditionally, which
branded even the shipped Florence Workshop a prototype. No new visible copy was needed — the preview
carries the stage's existing `DisplayNameKey` as its tooltip. The preview texture deliberately loads
through `ResourceLoader.Load`, **not** `AuthoredResources.Load`: it is streamable art, and the
pinning cache is for immutable tuning data only.

**C1: `FighterLocalizationTests` checks nine families, not the three §6 item 6 lists.** `stage_*`
(≥40), `fighter_*` (≥25) and `match_*` (≥3) are the plan's; A2's new copy is split out into
`fighter_countdown_*` (≥2), `fighter_frequency_*` (≥4), `fighter_disconnect_*` (≥3),
`fighter_pause_*` (≥2), `fighter_state_*` (≥11) and `fighter_results_*` (≥3) because the `fighter_*`
umbrella is large enough to hide a whole feature's worth of deleted keys under its own floor. Every
assertion goes through `TranslationServer.Translate`, never CSV membership, per
`CampaignLocalizationTests`' reasoning. `en.csv` was not edited this pass, so
`en.en.translation` and `content_manifest.1.translation` regenerated byte-identically and there was
nothing to commit for them.

**C1: `--headless --import` rewrote 21 unrelated `.import` sidecars with line-ending-only changes,
exactly as five Phase B agents predicted.** `git diff` reports them empty. They were discarded; the
only import artifact committed is the new `florence_preview.svg.import`.

**C1: no test was found pinning a hash literal** (plan §2.11 reporting duty), and no Godot `Resource`
is disposed anywhere in the new code.

**C1: final validation.** `dotnet build` clean — the single pre-existing vendored `CS8632`, no new
warnings. `--headless --import` clean. Full suite **907 passed / 0 failed / Total 907** across
**three consecutive runs** (901 Phase B baseline + 6: one Fighter pool-config case, three
`FighterLocalizationTests` cases, two `FighterStagePresentationTests` cases — the exact expected
delta, no cross-suite loss). Headless `--quit-after 300` smoke of all ten Fighter stage scenes plus
`MainMenu.tscn`, `TestArena.tscn` and `CharacterSelect.tscn`: thirteen for thirteen exit 0 with zero
`ERROR`/`SCRIPT ERROR` lines. End-to-end stage routing verified: with the catalog wired,
`FighterStageCatalogTests`' `ResourceLoader.Exists` check now exercises ten real distinct paths, and
`FighterStagePresentationTests.EveryCatalogScenePathResolvesToASceneDeclaringThatStageID` additionally
proves each scene declares the `StageID` its catalog row claims — a copy-pasted scene that kept a
sibling's id would otherwise resolve to the wrong deterministic geometry at runtime while every
per-stage suite stayed green.

#### What Package 6 did NOT deliver

Recorded so the next package inherits an honest boundary rather than a checkbox.

1. **A human playthrough of all ten stages.** The plan's exit criterion "all ten stage choices produce
   a visibly and mechanically distinct local 1v1 match" is a judgement about what a player sees. The
   automated gate proves the scenes load, the geometry is distinct, the hazards behave distinctly, and
   nothing errors — it cannot discharge "visibly distinct". Nobody has played these stages. The same
   applies to the human-vs-human and human-vs-CPU flow criterion: every step is unit-tested, and no
   one has walked it with two controllers.
2. **Production art, animation, music, VFX, and per-stage lighting.** Everything visual and audible in
   these ten stages is placeholder SVG, `ColorRect`/`Polygon2D` dressing, and three shared placeholder
   stems. No `Light2D`, `CanvasModulate`, `LightOccluder2D`, normal maps, or shaders — the era
   "lighting treatment" in the plan's stage-production item 3 is unstarted. Hazard VFX and SFX are
   likewise unstarted.
3. **Cinematic KO art.** The KO *sequence* exists and is proven not to disturb deterministic state,
   but hit-freeze, slow motion, spotlight, stamp and winner pose are all driven with placeholder
   presentation. The two audio hooks (`res://audio/sfx/combat/ko_stinger.ogg`,
   `res://audio/sfx/ui/victory_fanfare.ogg`) resolve to nothing because `audio/sfx/` is empty; the
   driver no-ops.
4. **The `ParallaxBackground` → `Parallax2D` migration.** Decided above: kept deliberately for the
   placeholder pass. Ten scenes and ten test suites move together in Package 8, and the
   opaque-backdrop guard must be rewritten rather than deleted when they do.
5. **`MatchSettings` disk persistence.** Session-scoped by decision (§2.6). Rules survive
   select → match → rematch → match through `GameManager.CurrentSession` and are lost on quit. No save
   schema bump was taken.
6. **Anything Steam or online.** Steam Networking Sockets, relay, LAN discovery, handshake and rules
   negotiation, full-state resync, matchmaking UI, and production failure/forfeit flows are all
   Package 7 and untouched here. The rollback gate makes Package 7 *startable*; it does not make the
   networking path production netcode.
7. **A CS0618-free typed parallax access path.** All nine per-stage suites plus the new cross-stage
   suite reach the parallax through `GetClass()` and `Get("motion_scale")` to keep the build
   warning-free. That is a deliberate suppression-by-avoidance, not a clean typed API, and it goes
   away with the `Parallax2D` migration.
8. **Rendering-cost measurement.** `docs/PERFORMANCE_BASELINE.md`'s new numbers are *simulation*
   costs from the .NET test host. Draw batches, vertices, and frame time for ten dressed stages have
   not been measured on any target machine, and the Steam Deck has never been touched. Package 9.
9. **Florence's three budgeted Chronal Extractors** (`docs/DUST_ECONOMY.md` §6) remain unauthored —
   a Package 5 leftover this package did not pick up, since it is Story content.
