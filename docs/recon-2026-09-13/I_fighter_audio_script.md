# Recon dossier — Workstream I

**Scope:** design master §8 Sound, §9 Art/Animation, §10 Fighter Arena & Stage Design, §11 Post-Match & Pause, §12 Settings, §13 Localization, §14 QA, §15 Balance, §16 Campaign Dialogue & Script Flow, §17 Asset specs — master lines 3462–4236 of `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`.

**Sources read in full:** the section diff `I_audio_art_fighter_script.patch` (531 lines), master §10/§16 in place, and `docs/FIGHTER_MATCH_RULES.md`, `docs/CPU_COMBAT_POLICY.md`, `docs/CPU_RECOVERY.md`, `docs/PRODUCTION_SCOPE.md`, `docs/DESIGN_BUILD_DEVIATIONS.md`, plus `docs/COMFORT_SETTINGS.md` (C01a/b/c are cited from my sections and nowhere else).

**Repo baseline:** `D:/Projects/Fighters Through Time - V3` @ `31fed14` (V7.4 pass, 2026-08-29), suite baseline 1638.

---

## 0. Executive summary — items in priority order

| # | Item | Design ref | Repo status | Size |
|---|---|---|---|---|
| I-1 | **Three Open stages (Paris / Vesuvius / Nassau) with real floor pits + main-floor ledges** | §10 "Floor Segments, Pits & Ledges"; V7.6 ruling 2.D | **Absent.** `FighterStageGeometry` has no floor-segment concept; the floor is a hardcoded `y <= 0` plane spanning wall-to-wall | **XL** |
| I-2 | **F22 Sudden Death rewrite** (retain living HP, next-death-loses, respect hazard toggle, full round reset) | §11; `FIGHTER_MATCH_RULES.md#sudden-death--f22` | **Contradicts.** `EnterSuddenDeath` sets HP=1, forces hazards on, no meter/cooldown/status/projectile reset, no countdown | **L** |
| I-3 | **F21 mode consolidation** (Stock + Time only; Hybrid retired; Time = fewest stocks lost, timer required) | §11; `FIGHTER_MATCH_RULES.md` | **Partial.** `MatchMode.Hybrid` still selectable; Time compares attacker-credited KOs; no "Stocks lost" HUD/results label | **M** |
| I-4 | **F19 CPU recovery + grab/Echo Step verbs** | §10 CPU AI; `CPU_RECOVERY.md`, `CPU_COMBAT_POLICY.md` | **Contradicts / absent.** Easy has no-recovery band values inverted, all tiers use a blind `Special2` recovery button, no per-character profiles, no Grab/EchoStep in the action set | **L** |
| I-5 | **C01b one prioritized background audio treatment** | §8 Audio Snapshots | **Contradicts.** `AudioSnapshotMixer` stacks snapshots *additively* and boosts UI +2 dB on pause | **M** |
| I-6 | **F24 persistent Fighter ownership outline, separate from effect glow** | §9 shader/glow tables | **Contradicts.** `GlowPresentationController` arbitrates slot indicator inside the same single-outline priority stack | **M** |
| I-7 | **C01a Reduced Temporal Effects setting** | §12 Gameplay Settings | **Absent.** No toggle, no `reducedTemporalEffects` field | **M** |
| I-8 | **§16 narrative rewrite** — Unbound/Wardens renames, no mid-campaign power loss, Mystery Thread, Level 12/14/15 re-set, hold-to-skip rule change | §16 + V7.5/V7.6 | **Contradicts.** Every level's `exit_3`/`exit_4` pair is the retired power-fade fiction; 28 lines carry retired faction names | **XL** |
| I-9 | **D03c hazard/Aegis ordering + Paris beam non-HP drain** | §10 hazard "Shield and Block Compatibility" | **Partial.** Aegis-before-block exists; Aegis is wrongly spent on the 0-damage Paris meter drain | **S** |
| I-10 | **F23 animation priority restatement** | §9 Animation Priority | **Mostly compliant**, doc-level; needs guard audit | **S** |
| I-11 | **C01c direct bindings for Grab/Echo Step/Ultimate/Time Freeze** | §12 Controls | Absent (overlaps the input/comfort workstream) | **M** — flag overlap |
| I-12 | **§17 asset coverage (P01 24 boss slots + 9 Legacy music; Eraser, Collapse Tremor, Defy seal art)** | §17 | Absent; production-scope ledger work | **Doc/prod** |
| I-13 | **Stage catalog `Open`/`Sealed` label shown in stage select** | §10 | **Absent.** `FighterStageData` has no layout-archetype field | **S** |

**Sequencing rule that governs this whole workstream (V7.6 2.D):** the three Open stages *ship before any further Fighter balance pass*. Every Fighter number touched before that is provisional. So I-1 is the gate for I-4's validation, for `COMBAT_VALIDATION.md#v02` stall testing, and for the Medium/Hard pit-aware DI deferral in §10.

---

## 1. §10 — The three Open stages (I-1)

### 1.1 What the design requires

Master §10, "Floor Segments, Pits & Ledges (V7 pillar decision — Option A, resolves audit H-11)":

* `FighterStageGeometry` authors the main floor as **segments**, not one wall-to-wall slab.
* **Open stages** — exactly three, named: **Paris Bastille** ("central lower pit between the drawbridge walkways"), **Vesuvius Caldera** ("a collapsed shelf on the slope's downhill end"), **Nassau Flagship** ("the listing deck's stern simply ends over open water").
* The main-floor edges at those gaps are **true ledges** — grabbable, trump-able, part of the recovery game.
* Falling through a gap reaches the **bottom blast zone** and costs a stock.
* **Far side walls remain solid** — the floor opens *inside* the stage, never at the screen edge.
* **Sealed stages** — the remaining seven keep an unbroken floor. The **catalog labels each stage `Open` or `Sealed`, and stage select shows the label**.
* Every stage keeps 2–3 raised one-way platforms (unchanged).
* **Floor segments are strictly solid: drop inputs are ignored on the main floor.** Falling into a pit requires being knocked, walking, or falling in — never a mis-input drop.

**No world-unit coordinates are given anywhere in the master or its docs.** I grepped the whole master for `pit|floor segment|collapsed shelf|stern`; lines 31, 103, 283, 1220, 1224, 3780–3785, 3805 are all prose. `docs/PACKAGE6_FIGHTER_PLAN.md` §4 (the locked dossier table, repo lines 290–312) likewise authors no gaps. **So this workstream must author the numbers**, and they belong in a new locked dossier row set in `FighterStageGeometryTests` plus a §4 amendment in `PACKAGE6_FIGHTER_PLAN.md`.

### 1.2 What exists

`scripts/FighterSim/FighterStageGeometry.cs` (402 lines):

* `FighterStagePlatform(CenterX, SurfaceY, HalfWidth)` — one-way platforms only, with `Supports(x)`, `EdgeX(side)`, `HangPosition(side)`, `IsInCaptureBox(...)`.
* `FighterStageGeometry` fields: `StageID, LeftWall, RightWall, Ceiling, BottomBlastZone, SpawnDistance, Platforms[], HazardAnchorXs[], OrbAnchors[]`. **There is no floor field at all.**
* `TryFindLedge(position, out anchor)` iterates **platforms only**; anchor encoding is `platformIndex * 2 + side`.
* `TryGetHangPosition(anchor, ...)` rejects `anchor/2 >= Platforms.Length`.

`scripts/FighterSim/FighterSimulationSystems.cs` — the floor is implicit and global:

* line 468: `bool groundIsSolid = _geometry.Platforms.Length > 0;`
* line 469: `if ((groundIsSolid || fighter.DropThroughFrames <= 0) && fighter.Position.y <= FP64.Zero) { Position.y = 0; IsGrounded = 1; RemainingJumps = tuning.MaxJumpCount; }` — **a wall-to-wall floor snap at y = 0**.
* line 448: `if (IsGrounded != 0 && Position.y > FP64.Zero && !HasPlatformSupport(...)) IsGrounded = 0;` — walking off an edge is only checked **above** y = 0, so a floor-segment edge would never drop anybody.
* line 461: blast-zone check runs *before* the ground snap (deliberate, per the comment) — good, this already works the moment a fighter can get below y = 0.
* line 749 (`ResolveMovement`): `bool onSolidBaseFloor = groundIsSolid && Position.y <= FP64.Zero;` gates drop-through off the main floor — the "no drop-through on the floor" rule is already correct in shape, it just needs a per-x floor lookup.
* line 602 `HasPlatformSupport` and line 611 `TryLandOnPlatform` both iterate `Platforms` only.

Current Paris/Vesuvius/Nassau geometry (identical to the locked dossier in `tests/Determinism/FighterStageGeometryTests.cs` lines 44–55):

| Stage | Walls | Platforms (cx, surfaceY, halfWidth) | HazardAnchorXs | OrbAnchors |
|---|---|---|---|---|
| `paris_bastille` | ±9 | (−4, 2.6, 2.0), (4, 2.6, 2.0) | −6, 0, 6 | (−4, 3.1), (4, 3.1), (0, 0.5) |
| `vesuvius_caldera` | ±8 | (−4.5, 2.0, 1.1), (3.0, 3.5, 1.1) | −6, −2, 2, 6 | (−4.5, 2.5), (3.0, 4.0), (0, 0.5) |
| `nassau_flagship` | ±9 | (−4, 2.8, 1.8), (4, 2.8, 1.8) | −5, 0, 5 | (−4, 3.3), (4, 3.3), (0, 0.5) |

Shared: `Ceiling = 9`, `BottomBlastZone = −5`, `SpawnDistance = 4` (spawns at x = ∓4, y = 0).

Scenes: `scenes/fighter/FighterStage_Paris.tscn`, `_Vesuvius.tscn`, `_Nassau.tscn`. All three author a **single full-width** `Geometry/Ground` `StaticBody2D` on layer 64 at pixel (950, 700) with a `RectangleShape2D` of width 1125 px (Paris/Nassau, ±9 units) / 1000 px (Vesuvius, ±8 units), collision offset `(0, 24)`.

> **Notable:** `FighterStage_Paris.tscn` already paints a *fake* pit — `Presentation/CourtyardPit` (a `ColorRect` at pixel x 700→1200, y 640→700) plus `CourtyardStepLeft/Right` and `CourtyardRail`. In world units that is **x −4 → +4, y 0 → −0.96**, i.e. a painted trench spanning exactly the two spawn points at ±4. It is pure decoration over solid floor, and its span is incompatible with any real pit that must keep the spawns on solid ground — **it must be re-authored, not reused**.

Conformance: `tests/ContentValidation/FighterStageConformance.cs` `ValidateGroundAndWalls` (lines 158–185) demands exactly one `Geometry/Ground` `StaticBody2D`, one `RectangleShape2D`, top edge at world y = 0, and **width == `RightWall − LeftWall`**. A segmented floor fails this outright.

CPU: `FighterCpuController.IsOffStage` (line 422–427) = `x` outside the walls **or** `y < 0 && !grounded`. With a real pit this becomes reachable for the first time, but it still cannot tell "I am over a pit at y = +2 with nothing under me". `CpuDecisionObservation` (`scripts/FighterSim/CpuDecisionObservation.cs`) carries walls/ceiling/blast-zone plus a *nearest platform* summary only — no floor topology.

DI: `FighterCpuController.ApplyHitstunDefense` line 233–236 holds toward `(LeftWall+RightWall)/2`, with an explicit comment that pit-aware "hold up" DI is deliberately unimplemented "because the authored stages run solid floors". The V7.4 design rows carry the same deferral flag (master lines 3869, master §10 Medium row). **Both the comment and the doc deferral expire with this workstream.**

Respawn: `FighterMatchFlowRules.RespawnPlatformPosition = (0, +3)` is a **global constant** consumed by `FighterSimulationRules.ApplyStockLoss` (line 2462) and `ProcessRespawnPlatform` (line 581). On a Paris centre pit the respawning fighter drops straight back into the hole.

### 1.3 Proposed geometry (needs user sign-off — the design authors no numbers)

Design constraints I solved against: spawns at x = ∓4 must be on solid floor; walls stay solid; gaps must be survivable with the post-2026-08-10 double jump (double-jump ceiling ≈ 4.21 units, per the Globe balcony note in `FighterStageGeometry.cs` lines 292–296); ledge capture box is ±0.5 wide and 1.2 deep below the surface (`FighterLedgeRules.CaptureHalfWidth`/`CaptureDepth`, pinned in `FighterLedgeTests.TheCaptureBoxIsHalfAUnitWideAndReachesOnePointTwoBelowTheSurface`); blast zone is 5 units below the floor.

| Stage | Floor segments (world x) | Pit span | True main-floor ledges | Notes |
|---|---|---|---|---|
| **Paris Bastille** | `[−9, −2.5]`, `[2.5, 9]` | **−2.5 → +2.5** (5.0 wide, centred) | x = −2.5 (right end of left segment), x = +2.5 (left end of right segment) | Two ledges, matching "central lower pit between the drawbridge walkways". The one-way walkways at (∓4, 2.6, hw 2.0) span ∓6…∓2, so each walkway **overhangs the pit by 0.5** — reads as a drawbridge over the gap. Spawns ∓4 sit on solid floor under the walkways. |
| **Vesuvius Caldera** | `[−8, 5.5]` | **5.5 → 8** (2.5 wide, against the right wall) | x = +5.5 only | "Collapsed shelf on the slope's downhill end" — asymmetric, matching Vesuvius's already-asymmetric platforms. The right wall stays solid above the gap, so a knocked fighter is pinned between wall and ledge: one true ledge, one high-tension recovery. |
| **Nassau Flagship** | `[−9, 6]` | **6 → 9** (3.0 wide, against the right wall = the stern) | x = +6 only | "The listing deck's stern simply ends over open water." Cheapest of the three: every existing hazard anchor (−5, 0, 5) and every orb anchor stays over floor, so no anchor re-authoring. |

**Knock-on anchor changes required by the proposal:**

* **Paris** orb anchor `(0, 0.5)` sits over the new pit and would fail `EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface`. Move it to **`(−7, 0.5)`** (or keep three symmetric anchors by going to four: `(−7, 0.5)`, `(7, 0.5)`). Hazard anchors {−6, 0, 6} stay — the Neural-Dampening Beam is a tall sweeping column and conformance compares hazard anchors on the **X axis only** (`ValidateMarkerSet(..., compareYAxis: false)`).
* **Vesuvius** hazard anchor `+6` sits over the new gap; the Rockfall's ground residue pool (halfWidth 1, 180 f) would hang in mid-air. Move to **`+4.5`** → `{−6, −2, 2, 4.5}`. Orb anchors unchanged.
* **Nassau** — no anchor changes.
* All three: **respawn platform** must stop being a global constant (see below).

### 1.4 Concrete change list

**A. `scripts/FighterSim/FighterStageGeometry.cs`**

1. Add `readonly struct FighterStageFloorSegment(FP64 CenterX, FP64 HalfWidth)` with `Supports(x)`, `EdgeX(side)`, `HangPosition(side)`, `IsInCaptureBox(position, side)` — mirroring `FighterStagePlatform` but with a fixed `SurfaceY = 0`. (Alternative: reuse `FighterStagePlatform` with `SurfaceY = 0` and a separate array; fewer new types, and `HangPosition`/`IsInCaptureBox` come for free. Recommended.)
2. Add `public readonly FighterStagePlatform[] FloorSegments;` + a `NoFloorSegments` sentinel meaning "unbroken floor wall to wall" (all seven Sealed stages keep the empty array, so zero behaviour change for them).
3. Add `public bool HasFloorSupport(FP64 x)` → `FloorSegments.Length == 0 ? true : any segment Supports(x)`.
4. Add `public bool IsOpenStage => FloorSegments.Length > 0;`.
5. Add a per-stage **respawn anchor**: `public readonly FPVector2 RespawnPlatformPosition;` defaulting to `(0, 3)` and overridden for Paris (proposal: `(−4, 3)`, over the left walkway/floor). See E below.
6. Extend `TryFindLedge` to search floor segments **after** platforms, with the anchor encoding continued: `anchor = (Platforms.Length + segmentIndex) * 2 + side`. `TryGetHangPosition` resolves the same way. Search order must stay deterministic (platform index ascending, left edge before right, then segments).
   * **Constraint to respect:** `FighterRuntimeComponent` is *exactly full at 128 bytes* (AGENTS.md known-gap bullet). The extended encoding adds **no new snapshot field** — `LedgeAnchor` is already an `int`. Do not add sim state here.
7. Author the three Open geometries per §1.3 and leave the other seven untouched.

**B. `scripts/FighterSim/FighterSimulationSystems.cs` (`FighterMovementSystem`)**

8. Line 468 `groundIsSolid`: replace `Platforms.Length > 0` with `_geometry.HasFloorSupport(fighter.Position.x)` for the **snap** decision, and keep a separate `stageHasSolidFloorRule = _geometry.Platforms.Length > 0 || _geometry.IsOpenStage` for the legacy-arena drop-through behaviour.
9. Line 469: the snap must only fire when `HasFloorSupport(x)` is true — otherwise the fighter keeps falling toward the blast zone (which is already resolved first at line 461, so the ordering is already right).
10. Line 448 (`walked off the edge`): change the `Position.y > FP64.Zero` guard so a grounded fighter at y == 0 with `!HasFloorSupport(x)` also loses support. Today `HasPlatformSupport` compares `Position.y == platform.SurfaceY` with exact fixed-point equality; extend it to treat y == 0 as the floor plane and consult `HasFloorSupport`.
11. Line 401 / 749 (`onSolidBaseFloor` in `ResolveMovement`): pass the per-x result so the "**drop inputs are ignored on the main floor**" rule holds on floor segments and is *not* accidentally re-enabled over a pit (there is nothing to drop through there anyway — the branch must simply not fire).
12. `TryGrabLedge` (line 543) needs no change once `TryFindLedge` covers segments, but confirm `FighterLedgeRules.CanGrab` gating (falling or rising slowly, not in hitstun/daze/drop-through, regrab lockout, `LedgeGrabsThisAirtime < 3`) behaves at y ≈ 0: the capture band is `SurfaceY − 1.2 … SurfaceY`, i.e. **y −1.2 … 0** for a floor ledge, comfortably above the −5 blast zone.
13. `ResolveLedgeTrump` (line 507) needs no change — it compares anchors, which now include floor ledges. **Verify** the V7.3 trump rule reads sensibly when both fighters hang off the same *floor* ledge.

**C. `scripts/FighterSim/FighterSimulationRules.ApplyStockLoss` + `FighterMatchFlowRules`**

14. `FighterMatchFlowRules.RespawnPlatformPosition` (a `static readonly FPVector2` at `(0, 3)`) is consumed at `ApplyStockLoss` line 2462 and `ProcessRespawnPlatform` line 581. Both must read the **geometry's** anchor. `ApplyStockLoss` is `static` and has no geometry parameter — thread the value through (cheapest: pass `in FighterStageGeometry` or an `FPVector2 respawnPosition` argument; it is called from `FighterMovementSystem` which already holds `_geometry`, and from the damage chokepoint). **Check every call site**: `grep ApplyStockLoss` → `FighterSimulationSystems.cs:462` and the hit pipeline.
15. Verify the drop from the respawn platform lands on floor for all three Open stages; add a test.

**D. `scripts/FighterSim/FighterCpuController.cs` + `CpuDecisionObservation.cs`** — see §2.4 (F19) below; the pit work and the F19 work must land together or the CPU walks into every pit.

**E. Scenes — `scenes/fighter/FighterStage_{Paris,Vesuvius,Nassau}.tscn`**

16. Replace the single `Geometry/Ground` body with **one `StaticBody2D` per floor segment**, named `Geometry/GroundLeft` / `Geometry/GroundRight` (Paris) and `Geometry/Ground` (Vesuvius/Nassau, single shifted segment). Keep layer 64, keep the `(0, 24)` collision offset convention, keep the top edge at world y = 0 (pixel y 700).
    * Paris: left segment centre world x = (−9 + −2.5)/2 = **−5.75** → pixel `950 + (−5.75 × 62.5)` = **590.625**, width `6.5 × 62.5` = **406.25 px**. Right segment mirrored at pixel **1309.375**.
    * Vesuvius: segment `[−8, 5.5]`, centre **−1.25** → pixel **871.875**, width `13.5 × 62.5` = **843.75 px**.
    * Nassau: segment `[−9, 6]`, centre **−1.5** → pixel **856.25**, width `15 × 62.5` = **937.5 px**.
17. Delete/re-author Paris's decorative `Presentation/CourtyardPit` + `CourtyardStepLeft/Right` + `CourtyardRail` so the painted pit matches the real one (x −2.5…+2.5 = pixel 793.75…1106.25) and add a visible depth/edge treatment. Vesuvius and Nassau need new pit art (collapsed shelf / open water below the stern).
18. Add readable **ledge/edge cues** at each true ledge. The master's pit-presentation requirement is stated Story-side (master line 3070, "every lethal opening needs a readable edge/depth cue"); apply the same standard here.
19. Check `FighterCamera` framing and `StageLightingRig` placement against the new hole.

**F. Conformance validator — `tests/ContentValidation/FighterStageConformance.cs`**

20. Rewrite `ValidateGroundAndWalls` (lines 158–185): collect **all** layer-64 bodies under `Geometry` whose name is not `WallLeft`/`WallRight`, match each against a `FloorSegments` entry by centre + half-width (same matching loop shape as `ValidatePlatforms`), assert top edge y = 0 on each, and assert that the union of segments equals wall-to-wall **only when `FloorSegments` is empty**.
21. Add a negative test in `FighterStageConformanceTests` — a stage whose scene floor spans the pit must fail.

**G. Tests**

22. `tests/Determinism/FighterStageGeometryTests.cs`: extend `StageDossier` with a `FloorSegments` field; update the three Open rows; `EveryAuthoredStageSharesTheCommonBoundsContract` gains "segments lie inside the walls, never overlap, and always support both spawn points"; `EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface` must consult floor support. `EveryAuthoredPlatformIsReachableAndLandableByJumping` should gain a sibling "**every pit is escapable**" run: drop a fighter into each gap and prove a jump + ledge grab returns them (per character, ideally — `RollbackReadinessKitShapeTests` already enumerates all nine kits and is the pattern).
23. `tests/Determinism/FighterLedgeTests.cs`: new cases — floor-segment ledge capture, the trump rule on a floor ledge, the regrab budget on a floor ledge, `TheLegacyFlatArenaHasNoPlatformsAndThereforeNoLedges` must stay true.
24. `tests/Determinism/FighterMatchFlowTests.cs`: `MatchEndsOnABottomBlastZoneFall` (line 298) currently has to manufacture the fall; add real per-Open-stage "walked/knocked into the pit costs a stock" cases, and a respawn-platform-lands-on-floor case.
25. Per-stage suites `FighterStageParisTests` / `VesuviusTests` / `NassauTests`: the `SceneMirrorsItsAuthoredFixedPointGeometry` case in each will fail until the scene is re-authored — update, don't delete.
26. `FighterStageCatalogTests` / `FighterStageManifestTests`: add the Open/Sealed archetype assertion (I-13).

**H. Catalog label (I-13)**

27. `scripts/Environment/FighterStageData.cs`: add `[Export] public bool IsOpenStage;` (and optionally `LayoutArchetypeKey`). Set `true` on the three entries in `resources/FighterStages/stage_catalog.tres`; **assert in a test that the catalog flag matches `FighterStageGeometry.ForStage(id).IsOpenStage`** — a drift here is a silent lie in stage select.
28. New localization keys `stage_layout_open` / `stage_layout_sealed` in `localization/en.csv`; render the badge in `CharacterSelectScreen`'s stage phase and in `HolodeckConsolePanel`'s stage row. Existing per-stage `stage_*_layout` descriptions (en.csv lines 493, 497, 501) should be reworded to mention the pit.
29. Re-run `--headless --import` after `en.csv` edits (CLAUDE.md rule: `--quit` does **not** recompile `en.en.translation`).

**I. Docs**

30. `docs/PACKAGE6_FIGHTER_PLAN.md` §4 dossier table (lines 298–312) gains a FloorSegments column and a §9 deviation entry.
31. `AGENTS.md`: the "bottom-blast-zone pillar vs. the solid-floor stage dossiers (audit H-11)" open-by-design bullet closes; the Fighter-stage paragraph and the 2026-08-10 M-16/M-18 gap bullet need edits. **Shared file — coordinate.**

### 1.5 Risks

* **Rollback/hash surface.** Geometry never enters snapshots (by design, see the class doc), so segment data is safe. The *anchor encoding change* does touch `LedgeAnchor` semantics, which **is** snapshotted — a mid-match re-encode would desync. Land it as one atomic change and re-run `RollbackReadinessTests` across all ten stages.
* **The V7.6 2.D provisional-numbers rule** means every existing Fighter tuning test is now measuring a provisional value. Do not "fix" a failing balance assertion by retuning; re-verify after the pits land.
* Suite-total accounting: expect a meaningful `Total:` delta. Compare against the exact expected number (CLAUDE.md failure signature 6).

---

## 2. §10 — Hazards, Orbs, CPU AI

### 2.1 Hazard shield/block compatibility — D03c Option B (I-9)

**Design:** active stage-hazard damage first uses eligible **Temporal Aegis and HP barriers**. One Aegis stops one damage tick; HP barriers absorb to capacity. Any remainder keeps the Basic-class block rule (exactly 1 block charge). Full shield absorption consumes **no** block charge. Do **not** spend protection during zero-damage warnings, grant tick hitstop, stop the hazard, or replay absorbed damage later. "Pits, Integrity and **non-HP drains** remain separate."

**Exists:** `FighterDamageRules.ApplyFighterHit` (`scripts/FighterSim/FighterEntitySystems.cs:70`) resolves `AegisHits` at line 94 **before** the block branch at line 111, returns false, and applies nothing else — so D02b ordering, D03e (no hitstun/status from an absorbed hit) and "no block charge spent" are already correct. `ApplyEnvironmentHit` (line 398) pins `blockChargeCost` to 1 with an explicit design citation.

**Gaps:**
* **`FighterEntitySystems.cs:1464–1472` — the Paris Neural-Dampening Beam spends an Aegis charge on a pure meter drain** (`if (beamRuntime.AegisHits > 0) { AegisHits--; continue; }`). D03c explicitly excludes non-HP drains. **Change:** the beam must not consume Aegis; the drain should proceed (subject to the existing invulnerability/stocks gates).
* **HP barriers do not exist in the Fighter sim.** D01/D02a/D02c/D03a reference Henry's Bastion / Royal Aegis / Leaf Barrier / Wardenclyffe. Nothing named `Barrier` exists under `scripts/FighterSim/`. This is the defensive-effects workstream's problem; hazards just need to slot into whatever ordering it builds. **Flag as a cross-workstream dependency: do not build a hazard-local barrier.**
* Verify `ApplyPeriodicTick` never runs during the 90-frame warning phase (it is gated by hazard phase upstream — confirm and pin a test).

**Size:** S for the beam fix + test; the barrier ordering is blocked on another workstream.

### 2.2 Chronal Orbs — D02b / D02e / D03e

**Design (master §10 Orb list item 4):** Aegis is separate from the block shield (pickup does not restore charges, end shatter lockout, or change regen timing); projectile immunity resolves first, then Aegis, then HP barriers, then block; it covers **one distinct hit contact, not every hit of a multi-hit attack**; **one active Aegis, no time expiry**, a collected duplicate is consumed with no extra protection; the absorbed hit applies **no hitstun/stagger and no new statuses/marks**, existing statuses remain, and primary throws retain the D03d bypass.

**Exists:** `FighterEntitySystems.cs:1830` sets `runtime.AegisHits = 1` on pickup (so "one active, duplicate grants nothing" is satisfied by assignment rather than increment — correct behaviour, worth a pin). No expiry timer exists — correct. Aegis resolves before block and short-circuits everything — matches D03e. Multi-hit: each hit goes through `ApplyFighterHit` separately, so one Aegis absorbs one contact — correct, needs a pin.

**Gaps:** throws — confirm the D03d bypass (a legal primary throw must bypass Aegis **without consuming it**). `FighterGrabRules.cs` + the throw path in `FighterVerbRules` need checking against line 94's unconditional early return. **This is probably a real bug:** a throw currently hits `ApplyFighterHit` and would be eaten by Aegis, consuming it. **Size:** S.

Sudden Death orb suppression is already in place (`FighterEntitySystems.cs:1726–1728`, `FighterMatchFlowTests.SuddenDeathSpawnsAndAwardsNoOrbs`) and survives F22 unchanged.

### 2.3 CPU decision/reaction model

**Design changes:**
* The action set gains **Grab** and **EchoStep** (master §10 §1 "Utility AI Decision Engine").
* **Reaction Time:** "All difficulties use the stated reaction buffer, **including Hard at 4–8 frames; there is no maximum-difficulty exception** for new opponent reads. Current self-state may be checked for legality, while decisions use delayed opponent observations." F19 admission rolls use the **seeded match PRNG** and are **saved with opportunity IDs for deterministic resimulation**.

**Exists:** `FighterCpuController` already delivers every decision through the `_schedule` ring at `NextReactionDelay()` (30–45 / 15–20 / 4–8 — `GetReactionDelayBounds`, lines 362–376), so the "no Hard exception" rule is already honoured. The V7.4 hitstun defense deliberately bypasses the schedule (`ApplyHitstunDefense`, lines 200–238) — that is a *self-state* reflex, which the new wording explicitly permits ("current self-state may be checked for legality"). **Compliant; worth a comment citing the new sentence.**

**Gap:** the controller's RNG is a private xorshift `_randomState` seeded from the constructor seed and deliberately **outside** the rollback snapshot (class doc lines 19–24: "determinism comes from the seed plus the observation sequence"). `CPU_COMBAT_POLICY.md` requires admission rolls to use "the seeded **match** PRNG" and to "snapshot the admission result, opportunity/attack IDs and PRNG state for deterministic resimulation." Those two statements are in tension with the shipped architecture. **Recommendation:** keep the input-source model (it is what makes CPU frames recordable and replayable exactly like human pads — `FighterMatchSeedTests`, `FighterCpuBehaviorTests.TheExpandedTableStaysBitIdenticalForTheSameSeedAndObservationStream`), derive `_randomState` from the per-match seed (already done via `FighterMatchSeedTests`), and satisfy the doc's intent by recording opportunity IDs in the **produced input frame**, not in the sim snapshot. **Raise this as a design/build deviation candidate for `DESIGN_BUILD_DEVIATIONS.md`** rather than silently re-architecting.

### 2.4 F19 — CPU recovery (I-4, part 1)

**Design (`CPU_RECOVERY.md` + master §10 matrices):**
* Recovery = returning from an unsupported position over a **stage gap** to a reachable platform or legal ledge before the blast zone. **Detect it from authored floor segments, ledges, fighter position and velocity — not from camera bounds or a universal main-platform Y threshold.**
* Recovery planning **takes priority over optional offense and orb pursuit** while return is at risk.
* Read distances/durations/trajectories from the **same normalized Fighter resources and simulation** as humans; no duplicated tuning constants, no CPU-only jump resets/cooldown refunds/invulnerability/teleport reach.
* "Closed stages and ordinary supported traversal must not trigger emergency casts."
* **Easy:** steer toward the nearest plausible legal ledge/landing; remaining jumps; **at most one activation of the character's movement ability per offstage episode**; **Specials remain disabled on Easy** (this *replaces* the old no-recovery rule).
* **Medium:** estimate reachable landings; remaining jumps + **one** suitable recovery ability — normally the movement ability, or an **explicitly validated** mobility Special. No multi-ability or platform/refund chains. "**Special 2 is not a universal recovery move.**"
* **Hard:** compare feasible routes, plan longer legal sequences (jumps, movement abilities, validated mobility Specials/temporary platforms); re-evaluate after interruption; no fixed jump→movement→Special 2 script; no indefinite offstage stalls.
* An offstage **episode** ends on a stable landing or legal ledge capture, or KO. A jump refund or temporary staff-platform landing alone does **not** reset the one-activation limit.
* **Per-character profiles table** (all nine) naming the primary movement ability and its planning constraints — Einstein Relativity Warp (+ reduced-gravity float; "Relativity Rift is not a directional recovery Special"), Joan Ascendant Wings (3 s glide, no Wings Refresh in Fighter), Leonardo Ornithopter Flight ("a turret is not a stepping platform"), Tesla Lightning Blink ("Lorentz Pulse does not move Tesla back"), Shakespeare Prospero's Flight, Mozart Sonata Drift (apex staff platform 3 s; Hard may plan a valid staff landing + the existing half-cooldown refund), Cleopatra Desert Mirage ("Sandstorm Vortex is not a recovery launch"), Lincoln Rail Charge ("armor does not prevent pit KOs"), Pocahontas Breeze Glide + double-jump reset (Spirit Strike is a *candidate* mobility Special, **only after confirming aerial legality and the implemented trajectory**).

**Exists — and contradicts on every tier:**

`FighterCpuController.DecideRecovery` (lines 429–453) is a three-branch percentage ladder over `RecoveryJumpPercent` → `RecoveryMovementPercent` → `RecoverySpecialTwoPercent`, with band values (`CpuBandTuning`, lines 780–782 / 815–817 / 853–855):

| Tier | RecoveryJump% | RecoveryMovement% | RecoverySpecialTwo% | Design says |
|---|---|---|---|---|
| Easy | 60 | **0** | **55** | movement ability **allowed once per episode**; **Specials forbidden** |
| Normal | 90 | **0** | **85** | one suitable ability, movement ability is the **default**; Special 2 is *not* universal |
| Hard | 100 | 90 | 100 | route comparison, no fixed script |

So Easy and Medium are **exactly inverted** relative to F19: they use Special 2 heavily and never use the movement ability. `CpuBandTuning.Easy`'s doc comment even says "Special 2 exists only as the off-stage recovery button, exactly as the design states" — that citation is now stale. `FighterCpuBehaviorTests.EasyUsesSpecialTwoOffStageAndNeverInNeutral` and `OnlyHardChainsAnUpwardMovementAbilityIntoItsRecovery` pin the old rules and **must be rewritten**.

There is no episode concept, no per-character profile, no route feasibility check, no "recovery beats orb pursuit" ordering beyond the fact that `IsOffStage` is tested first in `Decide` (line 407) — which is correct in shape.

**Change list:**
1. `CpuDecisionObservation`: add floor topology — nearest floor-segment edge X, whether the fighter's current X has floor support, gap span, and (for DI) whether the launch trajectory heads over a gap. Keep the struct mode-neutral (Story's `MirrorParadoxDecisionAdapter` fills sentinels).
2. `IsOffStage` (line 422): replace `SelfPositionYRaw < 0 && IsGrounded == 0` with "unsupported over a gap" using the new fields; keep the outside-the-walls branch. **Must return false on Sealed stages in every ordinary situation** (`CPU_RECOVERY.md`: "Also check Closed stages for false recovery triggers").
3. Add an offstage **episode** latch (input-side state, not sim state — the controller already owns `_hitstunHoldBlockActive` etc. outside the snapshot) with a per-episode movement-ability activation counter for Easy/Medium.
4. Rewrite `DecideRecovery` into a tiered planner; delete `RecoverySpecialTwoPercent` for Easy/Medium (or set to 0 and gate behind a per-character "validated mobility Special" flag).
5. New per-character recovery profile table — **code-owned constants, not resources** (Fighter stays competitively normalized; `CpuBandTuning`'s class doc is the precedent). It must key off canonical ability IDs and read reach from the normalized loadout (`FighterLoadoutFactory`), never duplicate numbers.
6. Tests: rewrite the three recovery cases in `FighterCpuBehaviorTests`; add a per-character × per-Open-stage × per-difficulty matrix (the doc demands "all nine characters on every authored Open stage at all three CPU difficulties"), plus false-trigger checks on Sealed stages.

**Size:** L, and **hard-blocked on I-1** — there is nothing to recover from until pits exist.

### 2.5 F19 — CPU grabs and Echo Step (I-4, part 2)

**Design (`CPU_COMBAT_POLICY.md`):** Easy disabled; Medium 25% admission per eligible opportunity (blocking+grounded+in reach for grab; vulnerable whiff during attack recovery for Echo Step); Hard scores both tactically including the 30-meter Echo Step spend against Ultimate setup and an unused eligible Defy. Roll admission **once per opportunity** (not per 3-frame evaluation); opportunity begins when the delayed observation first satisfies the conditions, ends when they cease or another action commits; a failed roll does not reopen the same continuing opportunity. Echo Step admission keys to the CPU's **attack execution ID**. Grab uses the canonical 0.8-unit reach and 10/4/24 frames **sourced from shared rules**; the grabber drops block; never target airborne/rolling/invulnerable/hitstun/daze/shieldstun victims. Echo Step requires own attack recovery, ≥30 meter, 120-frame cooldown ready, all canonical state restrictions, 8-frame wind-up, exact position from 30 frames prior, **31 exact frame samples**, reject invalid destination before spending, recheck at completion with no refund. "Check the resolved historical destination against current geometry and perceived danger before selection, **including pit risk**."

**Exists:** `GameplayButtons` — need to confirm Grab/EchoStep bits. Grab is the Block+BasicAttack chord (`FighterGrabRules.cs` exists with the canonical rules); Echo Step is Block+Roll (`verb.EchoStepWindupFrames` in `FighterVerbComponent`). The CPU's action set (`Decide`, `DecideCombat`) contains neither, and `ApplyHitstunDefense` deliberately makes the Block hold **exclusive** precisely so a scheduled attack edge can't misread as the grab chord (lines 228–232). Adding CPU grabs must not regress that.

**Note the C01c interaction:** C01c adds a **direct** `gameplay_echo_step` action and direct Grab/Ultimate binds. `CPU_COMBAT_POLICY.md` says "generate ordinary player inputs through the shared action resolver, including chord priority; never call an ability directly". Once direct binds exist, the CPU should emit the *logical* verb request, not a synthesized chord. **Sequence I-11 before or with this.**

**Size:** M–L. Depends on the recovery work and on C01c.

### 2.6 Difficulty matrix deltas (small, mechanical)

| Row | Old | New | Repo |
|---|---|---|---|
| Easy Specials | "Restricted... Special 2 only when off-stage and below the main platform Y" | "Easy does **not** activate Special 1 or Special 2 in neutral **or recovery**" | `CpuBandTuning.Easy.RecoverySpecialTwoPercent = 55` → **0** |
| Easy Movement Abilities | "Ignored entirely" | "Used only for the simple offstage recovery plan" | `MovementAbilityPercent = 0` stays; `RecoveryMovementPercent = 0` → per-episode single activation |
| Easy Grab/Echo Step | — | **Disabled** | n/a (absent) — pin a test |
| Medium Ultimate | "immediately at 100%" | "at the first **legal** opportunity...; if both it and Echo Step are legal, **Ultimate takes priority**" | `UltimatePercent = 100`, `RequiresUltimateSetup = false` — compliant; add the priority rule with Echo Step |
| Medium Hitstun Defense prose | "loops work sometimes and visibly fail sometimes" | "A randomized CPU attempt rate does **not** prove any pressure sequence is escapable; validate with deterministic defender inputs under F09" | Numbers unchanged (45/40 — `CpuBandTuning.Normal`). **Doc-only**, but it invalidates using `FighterCpuHitstunDefenseTests` as escape-proof evidence |
| Medium DI pit deferral | deferred | still flagged deferred in the master, but the deferral's premise dies with I-1 | `FighterCpuController.cs:190–194` comment must be rewritten |
| Hard Movement Pathing | — | already says "aggressive run-ramp spacing and roll usage (the universal dash no longer exists), ... **ledge play at open-stage edges**" | New requirement once pits exist |
| Holodeck | — | unchanged (✅ applied 2026-08-24 marker retained) | `HolodeckConsolePanel` exists |
| Training Mode | deferred | "**Guided Calibration Drills remain launch scope under F18**" | Absent — F18 belongs to another workstream; flag |

---

## 3. §11 — Post-Match & Pause

### 3.1 F21 mode consolidation (I-3)

**Design (`FIGHTER_MATCH_RULES.md`):**
* Selectable modes are **Stock** and **Time**. **Retire Hybrid.**
* Stock: limited stocks (default 3, 1–5); timer default 8:00, configurable **including Off**; expiry compares remaining stocks, then remaining HP as a fraction of that fighter's max HP (unreclaimed Rally echo excluded); true tie → Sudden Death, "also... when both final stocks are lost in the same regulation frame".
* Time: **unlimited respawns**, stock count is not a gameplay rule; timer required positive, Off unavailable; winner = **fewest stocks lost**, no HP or attacker-credit tiebreak; equal totals (including 0–0) → Sudden Death.
* Victim-side counting only: every actual stock loss adds 1 regardless of cause (opponent, blast zone, hazard, self-KO, DoT, surviving projectile/construct). A Defy- or invulnerability-prevented attack is not a loss. Co-occurring lethal damage + blast-zone crossing = **one** increment. Increment atomically with the living→KO transition, before respawn; duplicate callbacks cannot repeat it. **Life/death state, counter, match phase and timer are rollback snapshot/hash state.** Resolve all KOs in a regulation tick before evaluating the winner; nothing after regulation can change totals.
* HUD: Time replaces finite stock icons with a localized **"Stocks lost: N"** label under each HP bar; both counters start at 0; the simple results view shows the final totals; **never** label it KOs scored / points / remaining lives.
* Enum: make `Stock = 0`, `TimeLimit = 1` explicit; **reserve 2 for legacy decoding**, never reuse it or list a third playable mode. Legacy Hybrid normalizes to Stock with the same valid positive timer and stock count; an invalid/Off/missing Hybrid timer becomes 480 s with the repaired value visible before launch; invalid stock count → 1–5 clamp, default 3. Idempotent normalization before display or next match; persist through the existing writer; never change a running match.
* Label the second choice **Time** while retaining the internal `TimeLimit` identifier.
* F21 also adds to Post-Match: "**Time also shows both final regulation totals labeled Stocks lost**; a tied regulation total can have a winner determined by Sudden Death."

**Exists:**
* `scripts/Core/GameManager.cs:12–16` — `enum MatchMode { Stock, TimeLimit, Hybrid }` (implicit values). `MatchSettings` at line 46; `GetDefault()` = Stock / 3 / 480 / items Medium / hazards Medium.
* `scripts/UI/CharacterSelectScreen.cs:620–622` offers all three including `fighter_mode_hybrid`; `scripts/UI/HolodeckConsolePanel.cs:99+` likewise.
* `localization/en.csv:277` `fighter_mode_hybrid,Stock + Time`.
* `scripts/Core/FighterAudioRules.cs:20` treats `Stock || Hybrid` as the stock-showing mode.
* `FighterSimulationSystems.cs:91` `UsesStocks = MatchMode == TimeLimit ? 0 : 1` — Time already has unlimited respawns. Good.
* `FighterMatchSystem.Update` line 2280–2289: `shouldEnd` switch has a `_ => !allAlive || timerExpired` default that Hybrid falls into; Time resolves with `match.PlayerOneKOs`/`PlayerTwoKOs`, which `TrackKnockouts` (line 2380) derives from each fighter's `runtime.KnockoutsSuffered` **and credits to the opponent**. Semantically `PlayerOneKOs == P2's stocks lost`, so the *winner* comes out right today — but the model, the naming, the HUD and the results screen are all attacker-credit, which F21 explicitly forbids.
* `FighterSimulationRules.ApplyStockLoss` (line 2429) is the single victim-side chokepoint and already increments `runtime.KnockoutsSuffered` for **every** cause. This is the right hook.
* `FighterSimulationComponents.cs:532+` — `MatchMode`, `SuddenDeathActive`, `PlayerOneKOs`, `PlayerTwoKOs`, `LastPlayerOneKnockoutsSuffered`… are on `FighterMatchComponent`, i.e. already snapshot/hash state. ✅
* `scripts/UI/FighterHUD.cs:152` reads `MatchSettings.StockCount` and builds pips unconditionally; `LitStockPips` etc. are test seams. No stocks-lost label exists.
* `scripts/UI/MatchResults.cs:44–61` shows only stamp + winner/draw line; no totals.
* `scripts/Core/SaveManager.cs:96` `SavedMatchSettings` stores `Mode` as an int — **the migration surface**. `FIGHTER_MATCH_RULES.md` warns: "Verify actual shipped serialization/schema versions before implementing a converter; do not assume all saved formats match this design example." The global payload is schema v4 (`GlobalSaveData.SaveVersion`).

**Change list:** rename the counters to victim-side `PlayerOneStocksLost`/`PlayerTwoStocksLost` (snapshot field rename — coordinate with rollback hashing); make the enum values explicit and reserve 2; remove Hybrid from both selectors + `FighterAudioRules`; add the normalizer in `GameManager.EnsureMatchSettingsLoaded` / `SavedMatchSettings` decode; force a positive timer when Time is selected (default 480, shown before launch); add `fighter_stocks_lost` (`"Stocks lost: {0}"`) to `en.csv`, swap the pip row for the label in Time mode in `FighterHUD`, add both totals to `MatchResults`; retire `fighter_mode_hybrid` into `UnusedTranslationKeyTests`' recorded-orphan roster (the established idiom — see `menu_lan_match`).

**Tests:** `tests/unit/MatchSettingsTests.cs`, `tests/Determinism/FighterMatchFlowTests.cs` (`ATimeLimitKnockoutRaisesTheKnockoutCounterWithoutTouchingStocks` at line 393 and `MatchSettingsSurviveTheMappingIntoDeterministicRules` at line 741 both need rewriting), `tests/unit/FighterHudSceneTests.cs`, `tests/unit/FighterMatchPresentationTests.cs`, `tests/unit/HolodeckConsoleTests.cs`, `tests/unit/CharacterSelectSceneTests.cs`, `tests/unit/SaveEnvelopeTests.cs` (migration).

**Size:** M (the sim already counts the right thing; it is naming, UI, settings and migration).

### 3.2 F22 Sudden Death (I-2)

**Design:** on a true regulation tie, start an untimed **next-death decider**.

| Transition state | Required | Repo (`EnterSuddenDeath`, `FighterSimulationSystems.cs:2309–2360`) |
|---|---|---|
| Living fighter HP | **Preserve exact current positive HP** (including unequal HP in a Time tie). No heal, no clamp to 1 | ❌ `fighter.CurrentHP = 1` for everyone |
| Dead/respawning fighter HP | **Normal Fighter respawn HP**, resolved once from committed end-of-regulation life state | ❌ absent; a 0-stock fighter is given a phantom stock and 1 HP |
| Position / motion | Authored **match spawn points**, zero velocity, release ledge/grab attachments | ⚠️ `fighter.Position = fighter.SpawnPosition`, velocity zeroed, `ClearHang` — grab release not handled |
| Action state | Cancel attacks, grabs, throws, hitstun, daze, hitstop, armed Echo Step; neutral with **empty input buffers**; no queued attack fires after restart | ⚠️ hitstun/daze/hitstop/wind-up cleared; grab/throw state (`GrabPhase`, `BeingHeld`, `ThrowImmunityFrames`) **not** cleared; input buffers not flushed |
| Meter and cooldowns | Meter **0**; Special + movement + Echo Step cooldowns **ready**; no pending Ultimate | ❌ none of this |
| Block | Refill baseline charges; clear shatter lockout, shieldstun, regen countdown | ⚠️ `ShieldStunFrames`/`BlockLockoutFrames` cleared; **`BlockCharges` not refilled**, `BlockRegenFrames` not cleared |
| Movement resources | Reset jumps and ledge regrab; **initialize a new Echo Step history generation at the spawn with one valid sample**; require 30 subsequent ticks before use; never teleport into regulation history | ❌ absent — this is the `TEMPORAL_STATE_CONTRACT.md` hook |
| Status / item effects | Clear both status slots, marks, Root/tethers, timed orb buffs and **Aegis** | ❌ absent (`StatusType`, `DamageStatusType`, `AegisHits`, speed/jump buff timers all survive) |
| Rally / Desperation | Clear echo pool and pending echo-meter accounting; ordinary rules at retained HP, **no Overtime multiplier** | ⚠️ `EchoPool`/`EchoDrainPerFrame` cleared, `OvertimeActive = 0` ✅ |
| Defy History | **Disabled throughout**, meter irrelevant; **preserve its spent flag**; HUD seal shows unavailable; do not consume a new use on entry | ⚠️ `DefyHistoryUsed = 1` — this *destroys* the spent flag rather than preserving it. Needs a separate `DefyDisabledByPhase` concept (**but `FighterVerbComponent` byte budget — see the 128-byte note**) |
| Projectiles / constructs | **Remove every** regulation projectile, zone, summon, construct and temporary ability platform, including orphans; clear pending damage/events | ❌ absent |
| Arena and hazards | Restore authored round-start geometry/mechanism state. **Respect the match hazard toggle** — Off stays Off. If On: first full warning after play resumes; twice-normal cadence by halving idle/recovery, warning and active durations retained; do **not** multiply again by regulation Overtime | ❌ `HazardsEnabled = 1` and `HazardFrequency = 3` are **forced on** even when the house rules disabled them (lines 2320–2327) |
| Orbs | Remove existing, disable spawning and collection | ✅ lines 2313–2319 + `FighterEntitySystems.cs:1726` |
| Respawn protection | Clear prior platforms/invulnerability and old attack/roll invulnerability; common ready countdown freezes both; **no new spawn-invulnerability once play resumes** | ⚠️ `RespawnFramesRemaining = 0`, `InvulnerabilityFrames = 0` ✅; no countdown phase exists |
| Match state | No timer / stock pool; one decisive life each; **freeze regulation stocks-lost totals for results**; reset CPU action plans and opportunity tracking without changing difficulty; continue the match PRNG | ⚠️ timer off ✅; totals not frozen/preserved; CPU not reset |
| Ending | After each active tick resolve all real deaths; one dead fighter loses; two in the same tick = Draw; damage/falls/hazards/self-KOs equivalent; a nonlethal hit / legal block / invulnerable contact does not finish | ❌ current rule is literally "the first KO ends it" at 1 HP — the **retired** V7.1 rule |
| Presentation | Common match-start ready countdown showing **"Sudden Death — next death loses"** and the retained HP bars; controls and sim clocks frozen during transition/countdown; resume together | ❌ only a `FighterPresentationPhase.SuddenDeathStamp` (`EventBus.cs:242`, `FighterSimulationDriver.cs:956`, `FighterOverlayModel.cs:177`, key `fighter_sudden_death_stamp,SUDDEN DEATH`) |

**Also required:** rollback commits the phase transition and match state **together**; `ROLLBACK_STATE_CONTRACT.md` (S01) includes phase/life generations, frozen totals, pending cleanup/HP grants, continuing PRNG state and result identity, restored atomically; discard late regulation events by **phase identity**; exactly-once result logging. The listed verification matrix is long (unequal 20-vs-70 HP, 0–0 Time tie, hazards Off/On, warning duration, no stacked acceleration, pending projectiles/constructs/statuses, full/used Defy, old respawn invulnerability, simultaneous deaths, rollback across entry).

**Change list:** effectively rewrite `EnterSuddenDeath` as a shared "round reset" routine (which the countdown path can also use), add a `SuddenDeathCountdownFrames` phase reusing `FighterMatchFlowRules.CountdownFrames`, add entity sweeps for projectiles/zones/constructs, plumb the frozen regulation totals into `FighterMatchResult` and `MatchResults`, and add the localized `fighter_sudden_death_banner` string.

**Watch the byte budget:** AGENTS.md records `FighterRuntimeComponent` as *exactly full at 128 bytes*. F22 needs at minimum a "Defy disabled by phase" bit and frozen-total storage; frozen totals belong on `FighterMatchComponent` (which has room), and the Defy disable can be derived from `match.SuddenDeathActive` at the read site rather than stored per fighter — **prefer derivation**.

**Tests:** `FighterMatchFlowTests` cases `TimerExpiryWithIdenticalStateEntersSuddenDeathNotADraw` (line 364), `SimultaneousFinalStockKOEntersSuddenDeath` (line 520), `SuddenDeathSpawnsAndAwardsNoOrbs` (line 627) all need rewriting plus ~10 new cases. **Size: L.**

### 3.3 Pause, forfeit, post-match

* **M01 (Remote Play Together = local play):** "either assigned fighter can use the existing local pause menu on the host"; pausing is disabled in **native** networked matches. `scripts/UI/LocalFighterPause.cs` (287 lines, extends `PauseMenuBase` which owns `SceneTree.Paused`) already allows any local player to pause. **No code change needed today** — LAN is de-scoped to Package 7 (`MainMenuSceneTests` pins the absent LanButton). **Doc-only** unless the Remote Play messaging surfaces in UI.
* **Forfeit mechanism** (3 s Pause hold, radial fill, concede) — **absent**; belongs to Package 7 with native online. Leave deferred; note it.
* **Post-Match F21 totals** — see §3.1.
* **Overtime V02** (master §11 + `COMBAT_VALIDATION.md#v02`): "Faster endings are an **intention, not a guarantee**"; **diagnose the cause, propose a targeted change for user selection; no automatic cooldown reduction**; run comparable-player tests *after the Open stages are playable*, with roles/characters/sides swapped, separate stage/mode/hazard/item groups and hazards-off coverage. **No code change; this is a validation gate that must precede any Fighter tuning lock.** The shipped Overtime (`FighterMatchSystem.OvertimeFrames = 3600`, `UpdateOvertimeFlags`, hazard cadence doubling) stays exactly as is.
* **Sudden Death spawn-protection note in §9's shader table:** "Fighter: 3 s after platform release; **no F22 Sudden Death spawn protection**" — matches the F22 row; the glow must follow **actual protection state**, not a visual timer (see I-6).

---

## 4. §12 — Settings

### 4.1 C01a Reduced Temporal Effects (I-7)

**Design:** one toggle in **Settings → Gameplay beside Screen Shake**, default **Off**, help text *"Reduces distortion, after-images and screen flashes. Keeps gameplay cues and timing unchanged."* Persist Boolean **`reducedTemporalEffects`** in the existing global settings payload through its versioned handling; a missing legacy value defaults Off without overwriting an explicit value or inferring from Screen Shake. **Apply it before the first loading/portal effect.** Covers Story, Fighter, training, menus/loading transitions and scripted/cinematic effects. Applies **live** — clear already-emitted decorative after-images and stop suppressed overlays on enable; disabling resumes only the current event's remaining presentation and must not replay old flashes. No bright auto-preview.

Reduced treatment table (7 rows): kill chromatic aberration / RGB split / lens warp / high-frequency scanlines (**including Relativity Rift, Death Rewind and portal/loading**); remove decorative ghost trails/after-images (**preserve the single fixed Echo Step destination indicator** and real decoys); replace full-screen KO/Ultimate/rewind/transition flashes with a restrained overlay or smooth fade plus the existing text/glyph/local impact cue (**never** a differently coloured full-screen flash); disable full-scene desaturation and pulsing vignette/flicker (**preserve readable crack lines, platform warnings, static danger areas**); steady glow/symbols with a smooth expiry fade for armor/status/spawn/Defy feedback (**keep the separate Fighter ownership outlines, both status slots, the spent Defy seal**); retain attack-class glyphs, timing and the Special-class projectile signature; clean portal shape/static image for loading and cinematics.

Hard invariants: gameplay timing, hitstop, slow motion, camera framing, warning durations and resource clocks unchanged; Defy's 60-tick protection and Time Freeze's 5 s / 45 s untouched; Screen Shake stays independent (0 still disables); **store and apply locally, outside match snapshots/hashes — under S01 it cannot influence hit eligibility, target selection, random draws or simulation results**; reconcile after rollback without regenerating suppressed effects; graphics-quality settings must respect the same preset.

**Exists:** `scripts/Core/SaveManager.cs:100–192` `GlobalSaveData` carries `DamageNumbersVisible`, `HudOpacity`, `ScreenShakeScale`, `UiScale` (+ `Normalize()` clamps). `scripts/UI/SettingsMenu.cs:146–151` binds `Gameplay/HapticToggle`, `DamageNumbersToggle`, `HudOpacitySlider`, `ScreenShakeSlider`, `UiScaleSlider` from the authored `scenes/ui/Settings.tscn`; labels at 178–183; load/save at 478–503. **No reduced-effects field, toggle or consumer exists.**

**Change list:** add `public bool ReducedTemporalEffects;` to `GlobalSaveData` (additive — the codebase's own precedent is `TotalDraws`, "Newtonsoft leaves it at its default on payloads written before it existed, so no schema bump"); add the toggle node to `scenes/ui/Settings.tscn` + wiring + `settings_reduced_temporal_effects` / `..._help` keys in `en.csv`; apply at boot **before the first loading screen** (`ViewportEnforcer` is the last autoload and already applies display settings — but the loading screen is built by `GameManager.LoadScene`, so the read must be available there); add a single read point (a static on `UIPalette` or a new `ComfortSettings` service) consumed by `RewindPresentationOverlay`, `GlowPresentationController`, `VfxEmitter`, `LoadingScreen`, `FighterSimulationDriver`'s KO presentation, `CameraShake`, and the `outline_glow.gdshader` pulse path.
**Tests:** new `ComfortSettingsTests` (persistence, missing-legacy default, live apply, no gameplay-timing change) + a determinism pin that a differing local preference produces identical hashes (the doc explicitly asks for this).
**Size:** M for the plumbing, and it fans out into every presentation surface — expect follow-up passes.

### 4.2 C01c input remapping (I-11 — overlap)

**Design:** direct binding slots for **every** gameplay action including **Ultimate, Grab, Echo Step and Story-only Time Freeze**; keep the fixed Ultimate/Grab/Echo Step **action-chord recipes as optional per-device shortcuts**; **no custom chord editor**; independent per-device overrides, explicit `Unbound`, shortcut flags, versioned migration. Explicitly: "**schema v4 is historical, not proof of support for the new format**"; "Add the direct Echo Step action (`gameplay_echo_step`) and its explicit logical intent; **do not reuse the reserved Dash bit**."

**Exists:** `scripts/Core/InputBindings.cs` + `InputBindingSet` in `GlobalSaveData` at schema v4, one slot per device kind, conflict detection, per-action/global reset; the Ultimate LB+RB chord is shown **read-only**. `tests/unit/InputBindingSchemaTests.cs` pins the v3→v4 migration.

**This is primarily the input/comfort workstream's item.** My stake: the §12 text is in my section, and CPU grab/Echo Step (§2.5) depends on a logical-verb request path. **Call out the overlap; do not implement it here.**

---

## 5. §8 — Sound

### 5.1 C01b one prioritized background treatment (I-5)

**Design:** bus hierarchy gains a split — separate **clear Critical Cues** from background **World SFX**, *below* the existing user SFX gain/mute; UI & Dialogue bypass background health/environment/time filters and ducking, **retaining authored voice character and user gain/mute with no automatic boosts**. Low-pass is selected for background Music/World SFX by **one prioritized profile**; never a second filter on a parent bus carrying protected cues.

Priority order (highest first): **Pause → Death Rewind/Collapse/Anchor Snap recovery → Ultimate → Defy/boss/scripted presentation → Time Freeze → Low Integrity/Collapse Tremor → Low health → underwater → Normal.** Apply one set of targets relative to user volumes, **without cumulative filtering/ducking**; re-select current requests on exit rather than blindly restoring Normal. "Ultimate over LowHealth uses the Ultimate treatment alone; a prior 12 dB rewind duck cannot make it 24 dB." Shared local play uses **one global background selection** (two low-health fighters cannot double the duck). Spatial reverb and musical arrangement cannot introduce another filter or duck; select/blend **one** current environmental reverb send. Do not restart the music clock/stems on a priority change. Derive from authoritative state, apply locally **outside gameplay snapshots/hashes**; on load/scene change/rollback reconcile without replaying profile-entry stingers or already-presented one-shots.

Snapshot rows restated: `GamePaused` — selected background low-pass; **UI/dialogue stays clear at the user's configured level, without automatic boost**. `LowHealth` — heartbeat panning and high-end muffling on background only **while this profile wins**. `UltimateCinematic` — background ducked **once** by 12 dB; the featured Ultimate sound stays clear at its authored level.

**Exists — and directly contradicts:** `scripts/Core/AudioSnapshotMixer.cs` class doc: *"Snapshots stack **additively**: Pause plus Ultimate ducks music by the sum. Filter cutoffs stack by **minimum**."* Definitions: `Pause(-8, -12, **+2**, 900, 1200, 0.20)`, `LowHealth(-2, 0, 0, 3200, ∞, 0.60)`, `Ultimate(-12, -12, 0, ∞, ∞, 0.15)`, `Rewind(-12, 0, 0, 1800, ∞, 0.30)`. The `AudioSnapshot` enum has only **four** members; there is no Defy/boss, Time Freeze, Low Integrity/Tremor, underwater or Anchor Snap profile. `AudioManager` applies/releases via `_active` set semantics. Bus layout (`resources/Audio/default_bus_layout.tres`): Master(limiter) → Music(LPF), SFX(LPF) → {Combat, Movement, Environmental}, UI. **No Critical Cues bus, no Dialogue bus.**

**Change list:**
1. Convert `AudioSnapshotMixer` from an additive stack to a **priority selector**: keep the request set, resolve the single highest-priority active profile, blend gain/filter/pitch targets to that profile alone, re-select on release.
2. Remove the Pause `+2 dB` UI boost (explicitly forbidden).
3. Extend the enum with `DefyOrScripted`, `TimeFreeze`, `LowIntegrityTremor`, `Underwater` (a priority row with no authored treatment "adds none" — so empty definitions are legal placeholders).
4. Bus layout: add a **CriticalCues** bus under SFX (sibling of Combat/Movement/Environmental, **not** carrying the background LPF) and a **Dialogue** bus under UI; move hazard/attack-class warnings, freeze/thaw, recovery/phase and timer-danger cues onto it. `AudioBuses` constants + `AudioBusLayoutTests` + `AudioPresentationBindingTests` follow.
5. Route the dialogue chirp (`placeholder_sfx_dialogue_chirp.tres`, `DialogueChirpPitchTests`) to Dialogue.
6. `AudioSnapshotMixerTests` (currently pins additive stacking and minimum-cutoff stacking) and `AudioSnapshotTriggerTests` must be **rewritten**, not extended.

**Size:** M. Contained inside `scripts/Core/` + the bus `.tres` + three test suites.

### 5.2 §8 renames (narrative-rename overlap)

BGM table rows: `Neo-Earth / Future` → **The Unbound Bastion (was Neo-Earth)** / "Cyberpunk Fortress Between Timelines"; `Hub World (Archive Time-Ship)` → **Hub World (Warden Time-Ship)** / "Warden Vessel"; `Library of Alexandria (Final Level)` → **The Meridian Founding** / "The Future's First Day / The Prime Anchor", key instruments drop *Lyre* and gain *Radiant Pads*. Footstep material row: `Metal/Future-Tech (Time-Ship, Neo-Earth)` → `(Time-Ship, Unbound Bastion)`. Philosophy paragraphs: "Apex Archive" → "the Unbound" (×2).

**Repo impact:** these are prose rows, but they name the **audio sets**: `resources/Audio/level_14_audio.tres`, `level_15_audio.tres`, `hub_audio.tres` — check for any display/description strings and any `StageAudioSet` ID that embeds the retired names. Also `tests/integration/StoryAudioDirectorTests.cs` maps level → set. **Low code risk, but the rename workstream owns the vocabulary.**

### 5.3 New §8 SFX families (new content)

* **Timeline Collapse Alarm** — narrowed: sounds **only** when an unprevented lethal event occurs with no death-rewind charge available, or Integrity reaches zero. "**Spending the final charge is not a collapse and does not sound the alarm.**" → a trigger-condition change wherever the collapse beat is raised (`ChronalRewindManager`, `TimelineCollapseBeatTests`). **Cross-workstream (Story time systems) — flag.**
* **Eraser (V7.6)** — rising null-tone on the Null Lance telegraph; hollow *whump* + a brief silencing of the player's resonance hum while Suppressed (hum returns on expiry); tightening electric drone for the Siphon Snare tether that snaps on break. **F14:** the Snare's rising cue spans its 45-frame telegraph; sustained drone begins **only after attachment**, ends on every break/cancel, and **pauses with the frozen channel**. Pair every audio cue with the range/glyph/tether **visual**; no audio-only warning, no duplicate break sound on reload.
* **Collapse Tremor (V7.6)** — low sub-bass bed while Timeline Integrity < 20%; stone-and-timber groaning per jolt; debris whistle and shatter; a slow **detune of the music bus**; all intensifying below 10%.

**Repo:** nothing named `Eraser` (as an elite), `NullLance`, `SiphonSnare`, `CollapseTremor` exists — only the *boss* `resources/Bosses/apex_eraser.tres` (which V7.5 renames to **The First Unbound**) and `tidal_eraser`. The placeholder audio kit is **deliberately digital silence** (AGENTS.md; `resources/Audio/README.md`) — new cues must ship silent under the same rule or the whole audio contract suite breaks. **Size:** S for the silent-placeholder `.tres` + routing; the enemy itself is another workstream.

---

## 6. §9 — Art, Animation & Shaders

### 6.1 F24 — ownership outline as its own layer (I-6)

**Design:** F24 **separates Fighter ownership outlines from status/armor/spawn glow and flash effects.** New shader uniforms: `_OwnerOutlineColor` (slot-derived), `_OwnerOutlineThickness` (float, 1.0, "independent of effect thickness/pulse"), `_OwnerOutlineEnabled` (mode/actor-derived; "status/armor expiry cannot disable it"). `_OutlineColor` is demoted to "**secondary** effect color/opacity — alpha 0 disables that effect only; the Fighter ownership edge remains. Story may use the effect as its outline."

Option A wording: "Fighter player-slot outlines are a **separate persistent layer**, P1 cyan / P2 red at the existing 1 px reference thickness. Status, armor and spawn effects use interior glow/flash, particles and the independent HUD icons; they **cannot recolor, pulse, hide or overwrite** the ownership edge. **Composite that edge after effects** and constrain bloom so it remains readable. Within the secondary effect layer only, priority is spawn protection → armor → control status → damage status; both occupied HUD slots still render. Recompute from authoritative state when a source clears."

Also: per-instance control must "keep visual state isolated per actor... **do not mutate a shared resource and recolor other fighters**"; ownership parameters "initialized from match slot identity and **restored on spawn/rollback independently of effect changes**"; "Parameter updates alone do not guarantee preserved batching or zero allocations; **verify against rendering budgets**." Lifetimes must "read **authoritative gameplay state**, not duplicate visual timers" — the Spawn Invincibility row now says "follows actual protection state (Fighter: 3 s after platform release); **no F22 Sudden Death spawn protection**". The complementary `PointLight2D` is driven by the **secondary** effect's intensity and "never changes the ownership color or obscures its edge."

**Exists:** `assets/shaders/outline_glow.gdshader` has exactly four uniforms — `outline_color`, `outline_thickness`, `glow_intensity`, `pulse_speed` — with a header comment noting canvas shaders have **no `instance uniform` support** (the reason the design's per-instance approach was worked around). `scripts/Combat/GlowPresentationController.cs` is "the **single owner** of a sprite's material/tint state (three channels: base tint, tint override, **arbitrated outline stack**)"; `SetSlotIndicator(playerIndex)` (line 212) **pushes the slot colour into that same stack**, so any status/armor/spawn source outranks and replaces it — precisely what F24 Option A forbids. `GlowStateStackTests` and `GlowPresentationControllerTests` pin the old priority-overwrite rule.

**Change list:** add the three owner uniforms + a second composite pass in the shader (owner edge composited **after** the effect edge); split `GlowPresentationController` into an ownership channel (set once from slot identity, re-applied on spawn/rollback, never popped) and the effect stack (priority spawn → armor → control status → damage status); re-point `SetSlotIndicator`; move spawn-invulnerability glow onto `FighterStateComponent.InvulnerabilityFrames` rather than a presentation timer (and make it **not** fire in Sudden Death); confirm material isolation per actor (`gl_compatibility` forbids `instance uniform`, so this means per-actor `ShaderMaterial` instances — **measure batching**, the design explicitly stops claiming it is free). Rewrite `GlowStateStackTests` / `GlowPresentationControllerTests` / `EnemyGlowRoutingTests` (Story may keep effect-as-outline, so the enemy path should be unaffected — verify).
**Size:** M. Shader + one controller + three suites. Visual sign-off required (AGENTS.md: "nobody has visually judged any of the Package 8 output").

### 6.2 F23 — animation priority is a presentation summary (I-10)

**Design:** "animation selection **follows the resolved gameplay state**. The ordering below is a presentation summary; **shared transition guards, defenses and the explicit cancel windows are authoritative. A higher display priority is never permission to interrupt a protected action.**" Priority 5 becomes "Resolved death/respawn presentation; **protected grab/throw phases** and effective stun/daze"; 4 "Legal block stance (**includes the explicitly gated grounded hitstun escape**)"; 3 "Resolved attacks/abilities (only their authored cancels and recovery windows)"; 2 "Resolved locomotion / **ledge hang**".

Interrupt rules restated: only a resolved hit **with effective hitstun** enters Stunned; blocked hits use shieldstun/shatter; invulnerability and armor apply before the stun transition; **protected throw animation and death/respawn are not unconditionally overwritten**; a lethal event follows the death resolver. Block cancel needs grounded + usable charges + no conflicting action or shatter lock; the Stunned→Blocking escape follows the existing Hit-2/non-string gate; **never cancel daze, shieldstun or a grab phase**; landing tech is a separate tumble/ground-contact input, **not a block stance**. Combo chain cancel follows the authored string timing and the 24-frame window; preserve the zero-lag aerial landing cancel and legal basic→Special/Ultimate cancels; **Block+Roll and Block+BasicAttack use the shared F23/Fighter input priority; no grab-phase Echo Step.**

**Exists:** the shipped rules already match on nearly every point — `BasicComboRules` owns the timing, `HitstunBlockCancelBlocked` gates hit 1, `ShieldStunFrames`/`BlockLockoutFrames` exist, landing tech reads the raw Block input independent of charges (`FighterVerbRules.TryLandingTech`), and `FighterSimulationDriver.SyncPlayer` maps sim state onto animations rather than running its own FSM. **Status: mostly compliant.** Work = an audit pass confirming grab/throw phases and death/respawn are never overwritten by the animation mapper, plus a doc note. **Size:** S.

### 6.3 AI-asset scope ruling (V7.6 2.G)

"The AI-generated assets are **development placeholders**. All shipped art is **replaced with artist-produced content before release**, under the same atlas contract, so Steam's AI-content disclosure does not apply to the release build and **no per-atlas consistency gate is needed for placeholder art**. If any generated asset survives to the release candidate, the disclosure becomes a release requirement for that asset."

**Repo:** the whole retro-pulp raster pipeline (`tools/build_retro_character_spriteframes.py`, `tools/generate_assets.gd`) is exactly this AI pipeline, and the manifest rows already sit at `Placeholder` (`CharacterPresentationTests` pins that). **No code change. Record the ruling in `AGENTS.md` / `docs/` so nobody builds a consistency gate.** Note the memory file `asset-generation-pipeline` should be updated too.

---

## 7. §13 / §14 / §15

* **§13 Localization** — unchanged in the diff. Repo has the three gates (`ScriptTranslationKeyTests`, `SceneVisibleTextTests`, `UnusedTranslationKeyTests`) resolving through the compiled `en.en.translation`. **Every en.csv edit in this workstream must be followed by `--headless --import` and the regenerated `en.en.translation` committed** (CLAUDE.md).
* **§14 QA — rewind coordinate buffer test spec rewritten:** "Verify the **oldest valid grounded landing**, current clearance/support **after platform reversal**, invalid-sample skipping and the validated last-grounded/checkpoint/**entrance** fallbacks in order. Include safe zero-distance recovery, no valid anchor, one committed charge spend, and interrupted load. **Echo Step separately requires exact activation-minus-30 history and obstruction checks before spending and after wind-up**; see `docs/TEMPORAL_STATE_CONTRACT.md`." Repo: `tests/unit/ChronalRewindTests.cs`, `DeathTriggeredRewindTests`, `LevelDeathRewindDiagnosticTests` pin the current V7 depth semantics. **The entrance fallback and the platform-reversal clearance check are new** — Story time-systems workstream owns them; my section only states the QA obligation. **Flag overlap.**
* **§15 Balance** — no diff hunk in my range, but the governing sequencing text lives in §10: **every Fighter number is provisional until the Open stages ship**, and `COMBAT_VALIDATION.md#v02` stall validation is a gate before any tuning lock. Practical effect: this workstream must **not** retune knockback, DI, tech or ledge numbers; it ships geometry and re-verification infrastructure.

---

## 8. §16 — Campaign Dialogue & Script Flow (I-8)

### 8.1 Mechanics changes

**Hold-to-skip, V7.6 (replaces the V7.3 completed-slot rule):** holding confirm/Interact for **0.75 s** fast-forwards **any** dialogue sequence or cutscene. **First viewings ask once:** if the sequence has never been seen on *any* save slot — a **global** seen-list in `GlobalSaveData`, `seenDialogueIDs` — the hold opens a confirmation *"Skip this scene? It won't replay."* and skips on confirm; a sequence seen on any slot skips with no prompt. **F10:** completed **or explicitly skipped** presentations add their IDs to the global seen set; interrupted unresolved sequences resume their required flow; **story slot effect IDs** ensure watched/skipped outcomes match and cannot repeat rewards or unlocks; a retryable pending global union does not gate slot progression.

**Exists:** `scripts/UI/DialogueManager.cs` — `HoldToSkipSeconds = 0.75f` (line 63), `CanHoldToSkip` (66), `SkipHoldProgress` (70), the eligibility test at **lines 301–303**:
```csharp
_skipEligible = save != null && save.IsCompleted
    && save.ViewedDialogueIDs != null
    && save.ViewedDialogueIDs.Contains(sequence.DialogueID);
```
and the per-slot write at 582–584. Key `dialogue_hold_skip`. Save schema v5 carries `ViewedDialogueIDs` **per story slot**.

**Change:** move the seen-list to `GlobalSaveData.SeenDialogueIDs` (additive field), drop the `IsCompleted` gate entirely, make `_skipEligible` always true, and add a `ConfirmModal` first-viewing prompt (the shared modal idiom already exists — `scripts/UI/ConfirmModal.cs`, `ConfirmModalTests`). Keep the per-slot `ViewedDialogueIDs` for effect/reward idempotency (that is exactly what F10's "story slot effect IDs" asks for) and write to **both** sets on completion *and* on explicit skip. New keys: `dialogue_skip_confirm` ("Skip this scene? It won't replay."). **Tests:** `tests/unit/DialoguePresentationTests.cs` has 2 V7.3 hold-to-skip cases that must be rewritten + ~3 new.
**Size:** M.

**Narrative-layer scope wording:** "≈50 short lines per character (**scaling with the current roster — never a hardcoded total**, per the V7.5 roster-agnostic mandate)"; "one hero line per campaign level, **the Legacy Level included**"; "the **First Strike** opens at *this* character's historic nexus point" (was "the Fracture"). §17 likewise: "Each playable character — **the current starting roster of 9** ... the roster is expected to grow, and narrative/spec text must stay roster-agnostic."

### 8.2 The Mystery Thread (new authoring rules)

* **Absence beats** — every historical level whose central figure is a playable roster legend carries a **1–2 line absence beat**: Florence/Da Vinci, Orléans/Joan, Chicago/Tesla, Alexandria/Cleopatra, the Globe/Shakespeare, Gettysburg/Lincoln. The era "holds its breath"; the player **stands in for history's missing hero**.
* **N03 swap rule (Option A)** — when the saved campaign hero **is** that level's central legend, replace the absence report with **brief recognition in the existing short home-era dialogue allocation**; the larger personal payoff stays in the Level 4A Legacy Level. Recognition applies before or after Legacy completion, grants no automatic mission progress or permanent return home. Other heroes retain that level's absence beat and outsider observation; all other missing-legend beats still fire. **Select by stable hero/level IDs, including matching visual/seen/skip variants, never localized names.**
* **Act-boundary reports** — Sarah/Okafor widen the pattern to legends missing from eras the campaign never visits; **authored as slots keyed to the current playable roster**, scaling as the roster grows.
* **The mistaken theory (Acts I–II)** — Sarah's working explanation is "displaced like you — lost in the rifts"; the Observation Deck searches the drifting shards. It is comforting, plausible, and **wrong**.
* **Knowledge boundary (hard rule)** — **no dialogue line, brief, or UI string states any Villain's-True-Plan fact before Level 13.** Level 12 reveals *where*; Level 14 reveals *why*.

The six authored **Absence** lines (master lines 163, 169, 175, 196, 207, 213) and their six N03 recognition one-liners are written out in §8.4 below.

**Repo capability gap:** `DialogueSequenceData` (`scripts/UI/DialogueSequenceData.cs`) has `DialogueID`, `SpeakerNameKeys`, `LineKeys`, `EmotionKeys`, `PausesGameplay`, `AutoAdvance`. There is **no hero-conditional variant selection** and **no runtime placeholder substitution**. Both are now required:
1. **Variant selection by hero ID** for the six N03 swaps (and the per-character nexus openings / one-hero-line-per-level families).
2. **`{CaptiveName1}` / `{CaptiveName2}` substitution** in Level 12's reveal, resolved from "the first two non-active heroes in the authored priority Leonardo (spoken **"Da Vinci"**), Cleopatra, Tesla", excluding the saved hero (N04).
**Size:** M for the two mechanisms + `DialogueSetData` authoring conventions; the tests live in `tests/ContentValidation/CampaignLocalizationTests.cs` and the per-level `LevelNNContentTests`.

### 8.3 Speaker renames (`localization/en.csv` lines 558–560, 963, 989)

| Key | Current | Required |
|---|---|---|
| `speaker_sarah` | `Commander Sarah` | unchanged (her faction changes, not her name) |
| `speaker_player` | `Traveler` | **unchanged as a label**, but §16 rule 1 says the hero's rows must "use their name and voice"; "Traveler" survives only as Sarah's affectionate nickname. → needs a per-character speaker resolution (new mechanism, same as §8.2 item 1) |
| `speaker_archive_prime` | `Archive Prime` | → **`The Forge Sentinel`** (V7.5 rename; master line 55) |
| `speaker_apex_eraser` | `The Apex Eraser` | → **`The First Unbound`** (master lines 55, 1630, 3020) |
| — | — | **New:** a speaker key for the Level 14 Extraction Hall voice = `The First Unbound` (reuse the renamed key) |
| — | — | **New (N03/Mystery Thread):** `speaker_apprentice`, `speaker_captain`, `speaker_engineer`, `speaker_guard`, `speaker_player_company` (the Globe "Player"), `speaker_union_officer` |
| `speaker_cleopatra` | `Cleopatra` | retained but **only reachable in the N03 hero-is-Cleopatra variant** — the Level 8 post-boss scene no longer has Cleopatra present in the default path |

**Keep the resource IDs stable.** The master is explicit that legacy scene IDs are retained in code (`Level_14_NeoEarth`, `Level_15_Alexandria`) — apply the same discipline to `DialogueID`s (`level_14.entrance` etc.) and to `speaker_*` key names where practical; rename the *value*, not the key, wherever the key is not itself misleading. (`speaker_apex_eraser` → keep the key, change the value to "The First Unbound"; that keeps `UnusedTranslationKeyTests` quiet and avoids touching 17 `.tres` files.)

### 8.4 Per-level rewrite list

Legend — **[AUTHORED]** = exact new English text is in the master; **[DERIVED]** = the master mandates the change (retired fiction / renamed faction) but authors no replacement, so the text below is a **proposal requiring sign-off**; **[NEW]** = a sequence that does not exist yet.

---

#### `hub_dialogue.tres` — `hub.sarah_briefing`
* `dlg_hub_sarah_1` **[DERIVED]** — "Chronal Repository" survives; nothing faction-named. **No change required**, but §16 item 4 mandates a **per-act Sarah + crew refresh**, so this set must grow to three act variants plus Medic Okafor's observation-deck lines reacting to the chosen character at each act boundary. **[NEW]** sequences: `hub.sarah_act1/2/3`, `hub.okafor_act1/2/3`, plus the act-boundary missing-legend reports. Text not authored in the master.

#### `level_00_dialogue.tres` — `level_00.intro` (6 lines → **7 lines**) **[AUTHORED]**
The master rewrites this scene wholesale and adds a line. New `SpeakerNameKeys` = sarah, player, sarah, player, sarah, **sarah**, sarah.

| Key | New English |
|---|---|
| `dlg_l00_intro_1` | "Welcome back to reality, traveler. Or rather, what's left of it. I am Sarah, commander of the Meridian's Wardens. Breathe — we caught you mid-transfer. Your body is still deciding when it is." |
| `dlg_l00_intro_2` | "...Where am I? Princeton? Orléans? The sky... it tore apart, and something pulled." |
| `dlg_l00_intro_3` | "An extraction beam. The ones who fired it call themselves the Unbound — a sect from my century that went to war over the right to touch the past, and lost. Losing made them desperate. Now they attack history itself: cracking open its pivotal moments and draining them, remaking time in their favor. You were a target. You are the one we reached in time." |
| `dlg_l00_intro_4` | "Then we must stop them. But what of the soldiers I saw? They looked like my people, but... wrong. Their eyes glowed, their weapons hummed with light." *(unchanged)* |
| `dlg_l00_intro_5` | "The Unbound's doing. Where their siphons deploy, they neural-link the local inhabitants and force them to defend the machines as 'Altered Mobs' — steam automatons overcharged with chronal energy, knights with energy shields. Alongside them you will face the Unbound's own infantry from the future. Destroy the siphons, and the link breaks." |
| `dlg_l00_intro_6` *(new content)* | "Time protects itself. When they tore your moment open, history armed the only anchor it had left — you. Everything your era is poured itself into its legend rather than let them take it. We call it Temporal Resonance." |
| `dlg_l00_intro_7` **[NEW KEY]** | "We couldn't stop the beam. We could hold it — a few seconds. That was enough for the fire to finish catching. You are the one they fired on and did not get. Now — we calibrate it. Step up to the bay console." |

Note `dlg_l00_intro_2` now names the hero's own nexus ("Princeton? Orléans?") — that is a **per-character** line under the §16 narrative layer, so it needs the hero-variant mechanism, not one shared string.
Also **[NEW]**: the per-character Level 0 nexus cold-open (one illustrated still + 2–3 lines each) precedes `level_00.intro`.

#### `level_01_dialogue.tres`
* `level_01.entrance` — `dlg_l01_entrance_2` **[AUTHORED]**: "Stay alert. **The Unbound have** deployed a siphon in Da Vinci's workshop. …" (rest unchanged).
* `level_01.boss_intro` — `dlg_l01_boss_1` **[DERIVED]**: "The **Archive** has promised Florence eternity" → "The **Unbound have** promised Florence eternity".
* `level_01.exit` — **rewritten, 5 lines → 4** **[AUTHORED]**. This is Mystery Thread beat #1 and the master adds an explicit prohibition: *"The retired V6/V7 power-fade exchange — 'my hands are glowing less' — must not be reintroduced here or anywhere: there is no mid-campaign power loss."*

| Key | New English | Speaker |
|---|---|---|
| `dlg_l01_exit_1` | "The siphon is shattered. The energy is dispersing." *(unchanged)* | player |
| `dlg_l01_exit_2` | "Outstanding. With the siphon gone, the neural-link is broken. The altered guards will wake with nothing but a headache, thinking it was a strange dream." | sarah |
| `dlg_l01_exit_3` | "And the master of this workshop? Half-built wonders, ink still warm on the drafting table... and no Da Vinci. Where is he?" | player |
| `dlg_l01_exit_4` | "...I don't know. The record says he never left this room in 1503. Yet the era stands — as if history is holding its breath for him. Come home. I need to look at the other eras." | sarah *(Hesitant)* |
| `dlg_l01_exit_5` | **RETIRE** — remove the key and the array slot (or repurpose). | — |

**N03 Leonardo variant [AUTHORED, master line 163]:** replace the absence report with **Apprentice: "Maestro! We need your help at the workshop."**

#### `level_02_dialogue.tres` (Orléans)
* `dlg_l02_entrance_2` **[DERIVED]**: "That is **Archive** hardware" → "That is **Unbound** hardware".
* `dlg_l02_boss_1` **[DERIVED]**: "The **Archive** bought me this siege" → "The **Unbound** bought me this siege".
* `dlg_l02_exit_3` / `dlg_l02_exit_4` **[DERIVED — MANDATORY RETIREMENT]**: the entire power-fade exchange ("The glow in my hands dimmed again…" / "Your resonance is borrowed from the damage…") must go. **Proposed replacement, carrying the Mystery Thread absence beat instead:**
  * `dlg_l02_exit_3` (player): "The banner is still standing in the mud where they planted it, Commander. Nobody picked it up. Where is the Maid of Orléans?"
  * `dlg_l02_exit_4` (sarah): "Not on the field. Not in the camp. Not anywhere in 1429 that I can read. That is two eras now, both waiting on someone who never arrived."
* **N03 Joan variant [AUTHORED, master line 169]:** **Captain: "Joan! The line needs you at the banner."**

#### `level_03_dialogue.tres` (Chicago)
* `dlg_l03_entrance_2` **[DERIVED]**: "That is **Archive** work, Traveler" → "That is **Unbound** work".
* `dlg_l03_exit_3` / `dlg_l03_exit_4` **[DERIVED — MANDATORY RETIREMENT]** ("The resonance is fading already" / "It always fades once the moment is anchored"). **Proposed:**
  * `dlg_l03_exit_3` (player): "The engineers were reading his notes aloud to each other, Sarah. Guessing. Tesla's own switch, and no Tesla."
  * `dlg_l03_exit_4` (sarah): "Three. Florence, Orléans, Chicago. Every one of them missing the one person the moment was built around. I am starting to think that is the pattern, not the coincidence."
* **N03 Tesla variant [AUTHORED, master line 175]:** **Engineer: "Mr. Tesla! Help us bring the lights back."**

#### `level_04_dialogue.tres` (Paris)
* `dlg_l04_entrance_2` **[DERIVED]**: "The **cult** wants this uprising too tired…" → "The **Unbound** want…".
* `dlg_l04_boss_1` **[DERIVED]**: "sedition against the **Archive**" → "against the **Unbound**".
* `dlg_l04_exit_3` / `dlg_l04_exit_4` **[DERIVED — MANDATORY RETIREMENT]**. No roster legend is central to 1789, so this level takes an **outsider observation** rather than an absence beat. **Proposed:**
  * `dlg_l04_exit_3` (player): "They took the gate themselves in the end. I only cleared the way."
  * `dlg_l04_exit_4` (sarah): "That is the job, and it is going to keep being the job. Come home — I have four more eras on the board and a question I do not like the shape of."
* **Also in-scope (design §10 hazard table + master line 283):** Paris is the campaign level whose "open pit rooms" are called out at master line 3165 as *audit H-11's Story-side sibling*. **Story-level geometry — different workstream. Flag.**

#### `level_05_dialogue.tres` (Titanic)
* `dlg_l05_entrance_2`, `dlg_l05_exit_4` **[DERIVED]**: "The **Archive** was never storing this haul aboard" → "The **Unbound** were never…". The Act I finale's cargo-manifest hook ("something the Unbound are ferrying through the rifts that needs **life support**", master line 263) is **already compatible** with V7.5 — it is the Mystery Thread's first hard clue. Keep it; verify the line says it.

#### `level_06_dialogue.tres` (Pompeii)
* `dlg_l06_entrance_2` **[DERIVED]**: "The **Archive** has driven thermal anchors" → "The **Unbound** have driven…".
* `dlg_l06_exit_3` / `dlg_l06_exit_4` **[DERIVED — MANDATORY RETIREMENT]** ("It went dim again. Further than last time…"). **Proposed:** replace with an outsider beat + an act-boundary missing-legend report slot (§16 item, roster-keyed).

#### `level_07_dialogue.tres` (Nassau)
* `dlg_l07_entrance_2` **[DERIVED]**: "The **Archive** has seeded the water" → "The **Unbound** have seeded…".
* `dlg_l07_exit_3` / `dlg_l07_exit_4` **[DERIVED — MANDATORY RETIREMENT]** ("The resonance is thinning again…" / "It always fades once the fracture closes…").
* **Note:** Level 7 is where the **Eraser** elite debuts under V7.6 ("Levels 7–15"), which may want a line. Cross-workstream.

#### `level_08_dialogue.tres` (Alexandria) — **the heaviest rewrite in Act II**
* `dlg_l08_entrance_2` **[DERIVED]**: "The **Archive** is buying Octavian a total victory" → "The **Unbound are** buying…".
* `level_08.postboss` — **fully rewritten, 4 lines → 4, speakers change from (cleopatra, player, cleopatra, player) to (player, sarah, player, sarah)** **[AUTHORED]**:

| Key | New English | Speaker |
|---|---|---|
| `dlg_l08_postboss_1` | "The Jackal Priest is beaten, and the guards breathe freely again... but these are the queen's own chambers. Where is Cleopatra?" | player |
| `dlg_l08_postboss_2` | "Nowhere. Her guards defended an empty throne with future fire. And the era holds its breath — like Florence. Like Orléans. That makes every one of them now. Every giant, missing from their own moment." | sarah (via radio) |
| `dlg_l08_postboss_3` | "Torn loose, as I was? Adrift somewhere between the timelines?" | player |
| `dlg_l08_postboss_4` | "That is my hope. We caught you mid-theft — perhaps the others tore free and are drifting where we can search. The Observation Deck is already scanning the shards. We will find them." | sarah |

 **N03 Cleopatra variant [AUTHORED]:** *"when Cleopatra is active, use brief recognition in the existing short dialogue allocation; the larger personal homecoming stays in 4A. Suppress current claims that Cleopatra is missing and adapt collective absence references to the other legends. Sarah still voices the mistaken 'lost in the rifts' theory in this scene, with no early Forge reveal."* Recognition line (master line 196): **Guard: "Your Majesty! Your people need you."**
* `dlg_l08_exit_3` / `dlg_l08_exit_4` **[DERIVED — MANDATORY RETIREMENT]** ("The queen felt her resonance go out. I felt mine dim with it." / "You will feel that at every anchor we mend from here…").

#### `level_09_dialogue.tres` (Berlin)
* `dlg_l09_exit_3` / `dlg_l09_exit_4` **[DERIVED — MANDATORY RETIREMENT]**. Also note V7.5's Time Rule "re-aims Berlin's brief" (master line 55) — the entrance brief may need a sabotage-as-extraction reframe. Cross-check with the narrative workstream.

#### `level_10_dialogue.tres` (Globe)
* `dlg_l10_entrance_2` **[DERIVED]**: "The **Archive** has seeded the galleries" → "The **Unbound have** seeded…".
* `dlg_l10_exit_3` / `dlg_l10_exit_4` **[DERIVED — MANDATORY RETIREMENT]**. Replace with the **Shakespeare absence beat**. **Proposed:**
  * `dlg_l10_exit_3` (player): "They held the curtain on an unfinished fifth act, Sarah. The prompt-book just... stops. Nobody has seen the author in days."
  * `dlg_l10_exit_4` (sarah): "Of course they haven't. Add him to the list. It is getting long enough that I have stopped calling it a coincidence on the log."
* **N03 Shakespeare variant [AUTHORED, master line 207]:** **Player (of the company): "Master Shakespeare! The company needs you."**

#### `level_11_dialogue.tres` (Gettysburg)
* `dlg_l11_entrance_2` **[DERIVED]**: "The **Archive** handed the Confederate batteries laser guidance" → "The **Unbound** handed…".
* `dlg_l11_exit_2` **[DERIVED]**: "Whatever the **Archive** wrote here is unwritten" → "the **Unbound**".
* `dlg_l11_exit_3` / `dlg_l11_exit_4` **[DERIVED — MANDATORY RETIREMENT]**. Replace with the **Lincoln absence beat**. **Proposed:**
  * `dlg_l11_exit_3` (player): "The dedication platform is built and empty, Sarah. The address unread. The officers have had no word from Washington in days."
  * `dlg_l11_exit_4` (sarah): "Six now. Six of history's heaviest, and not one of them standing where the record says they stood. We find the pattern or we lose the next one."
* **N03 Lincoln variant [AUTHORED, master line 213]:** **Union officer: "Mr. President! We feared you would never reach us."**

#### `level_12_dialogue.tres` (Lunar Landing) — **the Act II "where" reveal**
* `dlg_l12_entrance_2` **[DERIVED]**: "The **Archive** is riding the broadcast" → "The **Unbound are** riding…".
* `level_12.preboss` — **fully rewritten** **[AUTHORED]**; the old lines 2–4 are the retired end-of-campaign power-loss exposition and must go. New shape: player / sarah / *(stage direction: the player destroys the nexus siphon; the trace completes)* / sarah (Shocked) / player.

| Key | New English | Speaker |
|---|---|---|
| `dlg_l12_preboss_1` | "To think humanity would walk among the stars... and yet, the Unbound have built a fortress of metal here." | player |
| `dlg_l12_preboss_2` | "Their last siphon nexus. Break it, and their roads through the rifts begin to close. And my trace is nearly done — every drained era, every stolen scrap of resonance... it has all been flowing somewhere. This siphon will show me where." | sarah |
| `dlg_l12_preboss_3` | *(stage beat — the player destroys the nexus siphon; the trace completes.)* | narration / none |
| `dlg_l12_preboss_4` | "...Coordinates. A fortress in the space between timelines. And inside it — resonance signatures. Alive. It's them. **{CaptiveName1}**. **{CaptiveName2}**. The others we lost. They were never adrift. The Unbound have had them since the first strike — and their resonance is draining." | sarah (Shocked) |
| `dlg_l12_preboss_5` | "Then the eras were never the whole of it. Whatever they are building with the stolen years, it ends now. Set the portal, Commander." | player (Determined) |

 **N04 selection rule [AUTHORED]:** resolve `{CaptiveName1}`/`{CaptiveName2}` from the **first two non-active heroes in the authored priority Leonardo (spoken "Da Vinci"), Cleopatra, Tesla**; use the same pair for the two featured return shots in Level 15; **exclude the saved campaign hero from the whole captive roster**.
 **Knowledge boundary [AUTHORED]:** this scene reveals *where* and *that they are draining* — **never** the Forge, the deficit, or the rewrite.
* `dlg_l12_exit_2` / `dlg_l12_exit_4` **[DERIVED]**: retire "Neo-Earth" ("It is their home. Far future. Neo-Earth.") → "the Unbound Bastion". `dlg_l12_exit_3` **[MANDATORY RETIREMENT]** ("It went out completely on the pad, Sarah. The resonance…").

#### `level_13_dialogue.tres` (Chronal Void) — **the Mirror Paradox's motivation is retired fiction**
* `dlg_l13_entrance_2` **[DERIVED]**: "the **Archive** cut a corridor" → "the **Unbound** cut a corridor".
* `dlg_l13_boss_intro_1` **[MANDATORY REWRITE]**: "*I am every second of resonance the fractures took out of you*" is built entirely on the retired power-fade. The Mirror Paradox needs a new premise consistent with V7.5 (resonance is history's immune response, growing all game; nothing is being shed). **Proposed:** "You are the one that got away. They modelled you a thousand times to find out how — and I am the model that kept getting up." → and `dlg_l13_boss_intro_2` adjusts accordingly.
* `dlg_l13_exit_3` / `dlg_l13_exit_4` **[MANDATORY RETIREMENT]** ("for a second I could not tell whose it was… That was the price").
* `dlg_l13_exit_5` **[DERIVED]**: "Take me to **Neo-Earth**, Commander." → "Take me to the **Bastion**, Commander."
* **Knowledge boundary:** Level 13 is the first level where Villain's-True-Plan facts may appear at all. Keep the Forge/deficit/rewrite out until 14.

#### `level_14_dialogue.tres` (The Unbound Bastion) — **re-set + one new sequence**
* `level_14.entrance` **[DERIVED, heavy]** — `dlg_l14_entrance_2` currently says "That is **Neo-Earth**… The **Apex Archive's core laboratory**". Rewrite for the Bastion: a fortress **in the space between timelines**, not a future Earth. Security lattices and anti-gravity containment fields survive as authored hazards.
* `level_14.boss_intro` — speaker key value `speaker_archive_prime` → **"The Forge Sentinel"**; lines 1/2 keep their voice but must not leak Forge facts before the Extraction Hall beat *(order the Hall first — see below)*.
* **`level_14.extraction_hall` [NEW SEQUENCE, AUTHORED]** — triggers on entering the Extraction Hall, **before** the final approach to the Forge core. 5 lines + a stage beat; speakers player, sarah, **first_unbound**, player, first_unbound.

| Key | New English | Speaker |
|---|---|---|
| `dlg_l14_hall_1` | "By every saint and star... the hall goes on beyond sight. The legends — wired into the machines like lamps into a chandelier. And behind them... hundreds more." | player (Shocked) |
| `dlg_l14_hall_2` | "Extraction Cradles. This is where every stolen era went — they were never spending the energy, they were banking it. All of it feeds that engine at the core. Do NOT touch the cradles! Sever a thread while the machine is charged and the machine keeps them — forever. The only way to free them is to kill it." | sarah |
| `dlg_l14_hall_3` | "The Warden is half right. It is called the Anchor Forge, little legend, and it was calibrated for all of you. The others we caged before their fire could catch. Yours caught — history flinched, and hid itself inside you. Every era we burned since, we burned to fill the hole you left. The deficit in my ledger carries your name. Lie down in a cradle — and history keeps the rest." | **The First Unbound** (echoing through the Hall) |
| `dlg_l14_hall_4` | "My legacy is not yours to spend. I will break your Forge — and every thread in this hall goes home." | player (Determined) |
| `dlg_l14_hall_5` | "Then watch it fire." | The First Unbound |

  *(Stage beat: the player destroys the Forge's intake; the cradles cannot be touched; the firing sequence begins as the level ends — transition to Level 15.)*
* `level_14.exit` — `dlg_l14_exit_3` **[MANDATORY RETIREMENT]**; `dlg_l14_exit_4` **[DERIVED]** must be re-set ("Alexandria, the moment of the cataclysm, the Leader of the Apex Archive" → riding the Forge's **firing channel** to the **Meridian Founding**).

#### `level_15_dialogue.tres` (The Meridian Founding) — **fully re-set**
* `level_15.entrance` / `level_15.preboss` **[DERIVED, total rewrite]** — every line is Alexandria-specific ("the Library is burning", "the original overload", "the Prime Anchor is at the far end of the rotunda"). The new location is the **founding day of the Meridian**, reached by **riding the Forge's own firing channel** (master lines 270, 3452), with the Prime Anchor **half-seated** and The First Unbound holding it.
* `level_15.boss_intro` — speaker value → **The First Unbound**; lines 1/2 rewritten off "Alexandria burns twice".
* `level_15.postboss` **[AUTHORED — N01 timing rule]**: "defeat The First Unbound, then use **Interact** at the exposed half-seated Prime Anchor. That accepted **sealing action** commits final completion/rewards and triggers the shattering/ending; **there is no extra attack test.**" `dlg_l15_postboss_1/2` must describe sealing, not inserting a Temporal Core into Alexandria. **Repo impact:** `scripts/Environment/TemporalCoreAnchor.cs` and `Level15Controller.cs` implement the Core-insertion interaction — verify the verb matches (Interact, one accepted action, once-only commit).
* `level_15.ending` — **fully rewritten, 6 lines** **[AUTHORED]**:

| Key | New English | Speaker |
|---|---|---|
| `dlg_l15_ending_1` | "It's shattering! The Forge is drinking its own backlash — the cradles are opening! The resonance — all of it — it's going home!" | sarah (static crackling) |
| `dlg_l15_ending_2` | *Montage:* "Threads of light arc out of the collapsing Prime Anchor and across every era. In Renaissance Florence, Roman Pompeii, and Civil War Gettysburg, cybernetic gear, laser visors, and glowing energy cells dissolve from guards, soldiers, and citizens. Two named captive-return shots use the N04 filtered selection: Da Vinci wakes at his workbench mid-sketch; Cleopatra's court finds its queen enthroned; substitute Tesla returning to his workshop when either is the active hero. Show only the selected pair, followed by the remaining six captive roster legends and the hundreds behind them dissolving into golden light and reappearing each in their own moment. Exclude the active hero from every captive/group return image; that hero stays at the Meridian Founding until the final hero beat." | narration |
| `dlg_l15_ending_3` | "The resonance... it was never mine. It was history's — held in trust. Take it back." | player |
| `dlg_l15_ending_4` | "The timelines are sealed. The war is over — and the future you saved is the one you're standing in. Thank you, traveler. You will wake where they tried to take you from. You will remember the Time-Ship, the battle, and the friends you fought beside... but the rifts are closed forever. You are going home." | sarah |
| `dlg_l15_ending_5` | "History... is ours to write now." *(unchanged)* | player |
| `dlg_l15_ending_6` | "The player character disappears — last of all the legends. The Founding's sky clears; the Meridian's first day proceeds, untouched." | narration |

 **N05 ending-variant rule [AUTHORED]:** the equal average across Levels 2–15 and the selected 4A (excluding 0/1) picks the ending still using **unrounded PreBoss values**: at **≥ 50%** the day is pristine; below, it carries the Prime Anchor's **visible scar**. → a second `dlg_l15_ending_6` variant + selection logic. **Cross-workstream (Timeline Integrity / §3).**

### 8.5 §16 rollup

* **Lines requiring faction-rename edits:** 28 keys (exact list from `grep -i "archive|cult|chrono-resistance|neo-earth|apex"` over `en.csv` dlg rows) — `dlg_l00_intro_1/3/5`, `dlg_l01_boss_1`, `dlg_l01_entrance_2`, `dlg_l02_boss_1`, `dlg_l02_entrance_2`, `dlg_l03_entrance_2`, `dlg_l04_boss_1`, `dlg_l04_entrance_2`, `dlg_l05_entrance_2`, `dlg_l05_exit_4`, `dlg_l06_entrance_2`, `dlg_l07_entrance_2`, `dlg_l08_entrance_2`, `dlg_l08_postboss_2`, `dlg_l10_entrance_2`, `dlg_l11_entrance_2`, `dlg_l11_exit_2`, `dlg_l12_entrance_2`, `dlg_l12_exit_2`, `dlg_l12_exit_4`, `dlg_l12_preboss_1`, `dlg_l13_entrance_2`, `dlg_l13_exit_5`, `dlg_l14_entrance_2`, `dlg_l14_exit_4`, `dlg_l15_preboss_3`.
* **Lines requiring power-fade retirement:** 11 keys — `dlg_l01_exit_4`, `dlg_l03_exit_3`, `dlg_l09_exit_3`, `dlg_l08_postboss_4`, `dlg_l08_exit_3`, `dlg_l06_exit_4`, `dlg_l07_exit_3`, `dlg_l10_exit_3`, `dlg_l11_exit_3`, `dlg_l14_exit_3`, `dlg_l15_ending_3` — **plus their paired partner lines** (`*_exit_4` where the partner is the one flagged, and vice versa), giving **~22 keys** across 13 levels. The exit_3/exit_4 slot is the retired fiction's home in **every** level from 1 to 14.
* **Sequences fully rewritten:** `level_00.intro`, `level_01.exit`, `level_08.postboss`, `level_12.preboss`, `level_14.entrance`, `level_14.exit`, `level_15.entrance`, `level_15.preboss`, `level_15.boss_intro`, `level_15.postboss`, `level_15.ending`.
* **New sequences:** `level_14.extraction_hall`; six N03 recognition variants; per-character Level 0 nexus openings; per-level hero lines; per-act hub Sarah/Okafor refreshes + act-boundary reports.
* **New mechanisms:** hero-conditional sequence variants; `{CaptiveName}` substitution with N04 filtering; global `seenDialogueIDs`; first-viewing skip confirm.
* **Tests touched:** `tests/ContentValidation/CampaignLocalizationTests.cs` (asserts every dialogue line/speaker key resolves through the **compiled** translation — it will catch every missed key), `Level02ContentTests`…`Level15ContentTests` (each pins its dialogue set ID and line keys), `tests/unit/DialoguePresentationTests.cs`, `tests/unit/CampaignCompletionTests.cs`, `tests/unit/DialogueChirpPitchTests.cs` (per-speaker pitch — new speakers need entries).
* **Size: XL.** This is the single biggest item after the Open stages, and it is almost entirely shared-file work.

---

## 9. §17 — Asset generation specs

| Change | Requirement | Repo status |
|---|---|---|
| **Roster-agnostic wording** | "the current starting roster of 9 … the roster is expected to grow" | Prose only |
| **P01 boss coverage** | "cover **24 authored non-tutorial boss encounter slots: 15 shared and 9 per-character Legacy bosses**. This is encounter coverage, not 24 wholly original atlas sets: reuse compatible rigs, animations, materials and UI templates, **including the existing Mirror Paradox fighter-kit reuse**. Each encounter still needs a mapped portrait, legible attacks/phases and defeat presentation; author missing assets explicitly." | 15 `BossData` resources exist; the 9 Legacy bosses are absent. **Production-scope ledger item (`PRODUCTION_SCOPE.md`), not a code task.** |
| **P01 music coverage** | "all **24 authored non-tutorial campaign slots (15 + 9 Legacy)**, Level 0, the versus arenas and Time-Ship Hub need explicit music coverage. A distinct scene/encounter slot does **not** imply a new composition… Record exploration/combat/boss bindings for all nine variants in the production ledger, retaining three synchronous, mixable stems." Plus the §8 note: "These are coverage obligations, **not nine automatically commissioned new tracks**." | 27 `StageAudioSet` resources (17 Story + 10 Fighter) on silent placeholders. Nine Legacy sets absent. |
| **Collapse Tremor VFX** | New sub-list under Character Assets → VFX: chronal crack-line overlay for background and foreground (cold cyan-white emissive); shaders for edge-inward desaturation, vignette pulse, and a time-ghost after-image for props; per-era debris sprites (3–4 per era; `Telegraph` crack / `Fall` / `Shatter`, 3 frames each); a **cracked-state variant for `FractureEligible` platform tiles**. | Absent. Note the **C01a interaction**: reduced effects must disable the vignette pulse and after-images while **preserving readable crack lines**. |
| **Eraser (V7.6) enemy assets** | Standard elite set + a second `Elite Special Attack` row (Null Lance thrust / Siphon Snare cast), a `NullLance_Bolt` projectile sprite (3 frames, cold-white), a `SiphonTether` `Line2D` texture, and the player-side **aura-smother** shader state (gold → cold grey) shared by all nine characters. **F14:** a readable **3-unit cast-boundary decal** and tether/meter-drain glyph alongside the Basic block-class cue, distinct failed-attachment / tether-break feedback, and a **stationary channel pose**. "These are not control-status icons; the move applies no movement lock." | Absent. The aura-smother shader state is a **new channel on `outline_glow.gdshader`** and interacts with F24's channel split (I-6) — build them together. |
| **F13 Defy resonance-seal HUD art** | Under UI HUD Sprites: a shared asset set with **intact-outline, intact-filled, broken and barred** variants, readable at **20 × 20 px** and across the full UI-scale range; same artwork in Story and **both** Fighter panels, with localized state labels; "distinct silhouettes/fill must survive grayscale and opacity settings." | Absent — `HUD_CONTRACT.md` is the authority; overlaps the HUD workstream. |
| **AI pipeline ruling** | V7.6 2.G placeholder ruling (see §6.3). | Doc/record only. |

---

## 10. Shared files and workstream overlap

| File | Who else touches it | Coordination note |
|---|---|---|
| `localization/en.csv` | **Narrative-rename workstream**, HUD, settings, onboarding | I add: `stage_layout_open/sealed`, `fighter_stocks_lost`, `settings_reduced_temporal_effects(+_help)`, `dialogue_skip_confirm`, the new speaker keys, ~22 rewritten `dlg_*` values, and retire `fighter_mode_hybrid`. **Every editor must re-run `--headless --import` and commit the regenerated `en.en.translation`.** `UnusedTranslationKeyTests` fails on a **new** orphan — retired keys must be added to its recorded-orphan roster in the same change. |
| `resources/Dialogue/*.tres` (17 files) | **Narrative-rename workstream** | I own the §16 script content; they own the faction vocabulary. Recommend **one agent** does both — splitting them guarantees merge conflicts in every file. |
| `AGENTS.md` | Everyone | I close the H-11 open-by-design bullet, change the Fighter-stage paragraph, the CPU/hitstun-defense bullet, the audio-snapshot bullet, the glow-controller bullet, the dialogue hold-to-skip bullet, and the test baseline. **Serialize the edits.** |
| `scripts/Core/SaveManager.cs` (`GlobalSaveData`) | Settings/input (C01c), dialogue (global seen list), match settings (F21) | Three additive fields land here from my section alone: `ReducedTemporalEffects`, `SeenDialogueIDs`, plus whatever the F21 mode normalizer needs. Additive fields need no schema bump (the `TotalDraws` precedent), but C01c explicitly demands a **versioned migration** for the input format — **that one bumps, and everyone else must land on the same version.** |
| `scripts/FighterSim/FighterSimulationSystems.cs` | Combat rules, verb layer, time systems | I touch `FighterMovementSystem` (floor segments) **and** `FighterMatchSystem` (F21/F22) in the same file. Two big edits; sequence them. |
| `scripts/FighterSim/FighterCpuController.cs` | Mirror Paradox (Story, `MirrorParadoxDecisionAdapter`), F20 boss profile | `CpuDecisionObservation` is the shared contract — **any field I add must have a Story-side "absent" sentinel**, exactly as `HasStageBounds` does. `MIRROR_PARADOX.md`'s F20 profile depends on this table. |
| `assets/shaders/outline_glow.gdshader` + `GlowPresentationController` | Eraser aura-smother (enemy workstream), C01a reduced effects, `HUD_CONTRACT.md` | Three separate consumers want new channels. Land F24's split first; the others build on it. |
| `resources/Audio/default_bus_layout.tres` | Story audio director, dialogue | The Critical Cues / Dialogue bus split changes every routing call site. |
| `docs/PACKAGE6_FIGHTER_PLAN.md` §4/§9 | Fighter stage work only | Mine. |

---

## 11. Suggested execution order

1. **I-1 Open stages** (geometry → sim → scenes → conformance → tests) — the V7.6 2.D gate; nothing else Fighter-side should land first.
2. **I-4 CPU recovery** — immediately after, or the AI is suicidal on the new stages. Then I-4 part 2 (grab/Echo Step), after C01c if possible.
3. **I-3 F21** then **I-2 F22** — F22's transition contract references frozen F21 totals, so F21 first.
4. **I-5 C01b audio** and **I-6 F24 glow** — independent, parallelizable.
5. **I-7 C01a** — depends on I-6 for the "keep ownership outlines" row.
6. **I-8 §16 dialogue** — merge with the narrative-rename workstream into one agent; it is orthogonal to everything above and can run in parallel from day one.
7. **I-9, I-10, I-13** — small, fold into whichever pass is convenient.
8. **I-12 §17** — ledger/doc work, `PRODUCTION_SCOPE.md`.

**Never run `dotnet test` concurrently across checkouts** (CLAUDE.md failure signature 5), and check every `Total:` against the exact expected delta — several of these passes rewrite tests in place, so "bigger than before" proves nothing.
