# Gameplay feel batch — 2026-08-10 plan

User-directed gameplay-mechanics batch (David, 2026-08-10). Twelve changes to movement and
combat feel, applied to **both modes** (Story `PlayerController` + deterministic Fighter sim)
per the same-move-same-behavior pillar. This document is the working agreement for the
worktree agents implementing it; §9 is the append-only deviation log.

## 1. Authority order

1. Explicit decisions in §2 of this document (they encode the user's directives).
2. `AGENTS.md` / `CLAUDE.md` working agreements (determinism boundary, resources own numbers,
   localization, shared-rulebook rule, `.tres` load discipline, no `Dispose()` on Resources).
3. Existing code conventions at the edit site.

Where a §2 decision supersedes an earlier locked design decision, that is deliberate and
recorded in §2.13. Do not re-litigate; do record any *further* deviation in §9.

## 2. Standing decisions (locked)

### 2.1 Run speed −15%, longer accel and decel

- All nine `resources/Characters/*_data.tres` `MaxMoveSpeed` ×0.85, rounded to nearest 0.25:
  lincoln 5.5→4.75, tesla 7.0→6.0, leonardo 7.5→6.5, shakespeare 7.5→6.5, einstein 8.0→6.75,
  mozart 8.0→6.75, cleopatra 8.5→7.25, joan 9.0→7.75, pocahontas 9.0→7.75.
- `FighterLoadout.Default.MoveSpeed` 8→7 (`FighterSimulationComponents.cs:230`).
- `UniversalMovementRules.RunAccelerationFrames` 8→**14**. Add new
  `UniversalMovementRules.RunDecelerationFrames = 12`; grounded stop ramps (target speed zero
  or sign reversal against current velocity) use the decel constant in **both** modes: the sim
  `ApplyNormalMovement` step selection, and Story's five inline grounded stop sites
  (`ProcessIdle`, `DecelerateHorizontal`, `ProcessSkidding`, `ProcessCrouching`,
  `ProcessBlocking`). Air ramps (4 accel / 8 decel Story, 4 sim) unchanged.
- `FighterSimulationTests.GroundRunAcceleratesOverEightFrames` is rewritten for the new
  constants (14-frame ramp to `FP64` 7) and a companion decel assertion added.

### 2.2 Jump height −20%

- Height ∝ force², so `MaxJumpForce` ×√0.8≈0.894, rounded to nearest 0.5:
  cleopatra 14.5→13.0, einstein 14.0→12.5, joan 13.5→12.0, leonardo 13.0→11.5,
  lincoln 11.0→10.0, mozart 14.0→12.5, pocahontas 15.0→13.5, shakespeare 13.5→12.0,
  tesla 13.0→11.5. (Rounding lands −17%…−22% per character; "about 20%".)
- **2026-08-15 follow-up (user directive: heavies felt too low):** the roster spread was
  compressed from 10.0–13.5 to **11.5–13.0**, linear in force with the ordering preserved:
  lincoln 10.0→11.5, leonardo 11.5→12.0, tesla 11.5→12.0, joan 12.0→12.25,
  shakespeare 12.0→12.25, einstein 12.5 (unchanged), mozart 12.5 (unchanged),
  cleopatra 13.0→12.75, pocahontas 13.5→13.0. Only the two floatiest characters lost any
  height, so every platform reachable before is still reachable; the reachability suites
  bound on the lowest jumper, which rose. Weight is unchanged and still carries the
  heavy/light knockback identity.
- `FighterLoadout.Default.JumpSpeed` 13→11.5 (default-loadout apex 2.82→2.20 units;
  double-jump ceiling ≈4.4).
- **Stage geometry consequence (locked):** lower the Globe Theatre canopy platform
  `SurfaceY` 4.4→**4.0** — the only platform outside the new double-jump envelope with
  margin. Orléans 4.0 and Berlin 3.6 stay. The edit must land in all four places together:
  `FighterStageGeometry.cs`, the `FighterStageGeometryTests.Dossiers` table, the
  `scenes/fighter/FighterStage_Globe*.tscn` markers/colliders (conformance validator,
  62.5 px/unit, 1 px tolerance), and the stage's orb anchor (0.5 above supporting surface).
- Story gravity model is untouched (H-7 stays open; only the shared force numbers move).

### 2.3 Double jump for everyone

- `MaxJumpCount` 1→2 for joan, leonardo, lincoln, shakespeare, tesla (others already 2).
  No character goes above 2. `FighterLoadout.Default` already 2.
- `Level00Controller` tutorial double-jump gate and any Story reachability math must be
  re-verified against the *reduced* jump force (§2.2) — the gate teaches double jump, which
  every character now has, but the geometry was sized for old forces.

### 2.4 Block cancels hitstun

- While in hitstun (not daze), **grounded**, stocks remaining: holding Block exits hitstun
  into the normal block stance. Airborne victims cannot (grounded-stance rule stands).
  Daze (guard break) is NOT cancelable — the guard-break punish window stays.
- Sim: clear `HitstunFrames` in `FighterMovementSystem.Update` when
  `HitstunFrames > 0 && DazeFrames <= 0 && IsGrounded != 0 && (HeldButtons & Block)`,
  before the hitstun gate, so the same tick enters the stance through `IsBlockStance`.
- Story: `ProcessStunned` polls `IsHeld(Block) && IsOnFloor()` → clear `_stunTimer`,
  `TransitionTo(Blocking)`.
- **Supersedes** the 2026-08-09 "hits one and two hold the victim through the chain" design:
  a victim holding block now escapes the string after hit one. Parity tests pinning the
  hold-through-chain behavior are rewritten to pin the escape (and to pin that an *airborne*
  or dazed victim still cannot escape).

### 2.5 Finisher separation + low-HP knockback scaling

- **HP scaling (all hits, both modes):** effective knockback ×`(1 + missingHPFraction)` of
  the **victim after the hit's damage is applied** — linear 1.0× at full HP to 2.0× at 0 HP.
  Sim: single insertion in `FighterDamageRules.ApplyFighterHit` (fixed-point:
  `knockback * (max*2 - hp) / max`). Story: `DamageCalculator.CalculateKnockback` gains the
  victim-HP overload; `EnemyController.OnHurtboxHit` and `PlayerController.OnHurtboxHit`
  pass post-damage HP. Environmental/hazard hits included (they route through the same
  chokepoints).
- **Finisher must create separation:** raise `BasicComboRules.KnockbackMultipliers[2]` from
  3 to a value (start at 4.5; tune) such that a mirror-match finisher at full victim HP
  produces **≥ 2.5 units horizontal separation** (attack range 2 + margin) measured when the
  victim's hitstun ends — pinned by a new determinism test across all nine kits. Story hit-3
  knockback Y component retuned in step.

### 2.6 Damage rebalance: basics −50%, specials +100%

- Nine `.tres` `BasicAttackDamage` ×0.5: cleopatra 4, einstein 5, joan 6, leonardo 4.5,
  lincoln 7.5, mozart 4, pocahontas 4.5, shakespeare 5, tesla 4.5. (The sim's
  `RoundDamage` rounds x.5 up — a known ±0.5 sim/Story divergence that already exists.)
- All 18 **Special1/Special2** `AbilityData.BaseDamage` ×2 in `resources/Abilities/`.
  **Ultimates unchanged. Movement abilities unchanged** (Lincoln's Rail Charge 5 stays;
  Shakespeare's Tempest stays 0). `FighterLoadout.Default.BasicDamage` stays 10 (synthetic).
- All content-pin assertions updated to the new numbers (≈18 assertions across 15 files,
  plus `LoadoutFactoryUsesNormalizedCharacterResources` and
  `RollbackReadinessKitShapeTests`).
- Known consequences, accepted: Story encounters take ~2× longer on basics (worse on Hard's
  1.5× enemy HP); special-heavy play charges ultimate meter faster; Resonance "+8% basic
  damage" minors are relatively weaker. No compensating retune in this batch.

### 2.7 New inputs: `gameplay_up`

- New InputMap action `gameplay_up`: W key, joypad axis 1 = −1 (stick up), joypad button 11
  (dpad-up). Edits land together in: `project.godot [input]`, `InputManager.Actions`,
  `RemappableActions`, `ActionLabelKey` (+ `controls_action_up` en.csv key), and the
  vertical-axis synthesis.
- **Vertical synthesis change:** `vertical = (down ? 1 : 0) − (up ? 1 : 0)`. Jump is
  **removed** from the vertical axis. Consequence: Warp-style up-direction reads
  (`MoveY < −30`, `Vertical` in Story movement abilities) are now driven by the Up input
  instead of held Jump — this is an intended improvement (stick-up now works on gamepad).
  Tests that set `MoveY` directly are unaffected; anything simulating "hold jump to warp up"
  through the sampler is updated to hold Up.
- No wire/protocol change: `MoveY` is already serialized; packet stays 47 bytes, protocol
  v2, `Dash` bit stays inert.

### 2.8 Up-attack and down-air

- **Selection at swing start** (sim `StartSwing` / Story `CheckAttackInput`):
  - Up held (`MoveY < −30` / `Vertical < −0.25`) + BasicAttack → **up-attack**, available
    grounded AND airborne.
  - Else airborne + Down held + BasicAttack → **down-air**.
  - Else grounded + Down held + BasicAttack → **normal string** (explicit: grounded
    down-attack is just a standard attack).
- Both are **single strikes outside the three-hit chain**: no chain-hold, no buffering into
  the string, combo index resets. Landing cancels a down-air with no lag (aerial-string
  rule). Frame data lives in `BasicComboRules` (new shared arrays — one source, both modes):
  - Up-attack: startup 7 / active 8 / recovery 18. Hitbox above the fighter (sim: ±1.2
    horizontal, up to 2.4 above origin; Story mirrors with its pixel hitbox family).
  - Down-air: startup 6 / active 10 / recovery 16, aerial only. Hitbox below (±1.0
    horizontal, 2.0 below origin).
- Damage: 1.0 × (post-§2.6) `BasicAttackDamage`. Hitstun 30 frames.
- **Knockback: both launch the victim upward** (sim `verticalKnockbackScale` 2.5 with a
  small 0.3 horizontal component; Story equivalent). "Distance depends on damage taken" is
  delivered by the §2.5 HP scaling — no separate mechanism.
- Attack-variant state: `FighterRuntimeComponent.AttackFlags` free bits 8 (up-attack) and
  16 (down-air). **No new component fields for attacks.**
- CPU: does not use the new attacks in this batch (its combat table emits `MoveY = 0`);
  recorded as an accepted gap, revisit with a CPU pass.
- Control-legend strings (`test_arena_controls`, `tutorial_controls`) updated; run
  `--import` and commit `en.en.translation` (integrator only).

### 2.9 Fast-fall

- **Stateless rule, both modes:** while airborne, not in hitstun, Down held → vertical
  velocity is clamped down to at least `UniversalMovementRules.FastFallSpeed = 16` units/s
  downward, immediately (sim: `if (vy > −16) vy = −16`; Story: px equivalent 960 px/s,
  bypassing the 600 px/s terminal clamp for this case). Fast-fall also cancels the Warp
  float window (`FloatFrames = 0`). No snapshot field — derived from held input each tick.
- Interactions (locked): drop-through then hold Down = immediate fast drop (intended);
  down-air + fast-fall stack (intended — a falling downward strike); Pocahontas glide and
  other ability floats are NOT modified in this batch beyond the Warp float cancel.

### 2.10 Platform drop-through everywhere

- Sim already supports Down+Jump drop-through on every one-way platform; **add double-tap
  Down** as a second trigger in the sim (18-frame tap window, matching
  `StoryCombatRules.DownDoubleTapFrames`), closing audit M-18 in the sim→Story direction.
  Requires one new sim counter — reuse: encode the tap timer in the existing
  `FighterRuntimeComponent` spare int budget ONLY if §2.11's allocation leaves room;
  otherwise Down+Jump stays the sim's only trigger and M-18 stays open (record in §9).
- Story already drop-throughs on double-tap Down from grounded states; no change beyond
  verifying every campaign one-way platform is in the `OneWayPlatform` group (spot-check,
  no sweep).

### 2.11 Ledge grab (Fighter sim)

- Platforms only (the base floor spans wall to wall; side walls are solid). A fighter
  grabs when: airborne, `DropThroughFrames == 0`, not in hitstun/daze, regrab lockout
  expired, vertical velocity ≤ 2 (falling, or rising slowly near apex — covers "jumping up
  to it"), and within the capture box of a platform end: `|x − edgeX| ≤ 0.5` and
  `SurfaceY − 1.2 ≤ y ≤ SurfaceY`.
- Hanging: position pinned at (edgeX nudged 0.25 outside the platform, SurfaceY − 1.0),
  velocity zeroed, gravity off, jumps refilled on grab. Actions: Jump → climb jump
  (`JumpSpeed × 0.9` upward, exits hang); Down → release with a 30-frame regrab lockout;
  auto-release at 300 frames. Both fighters may hang the same edge (no occupancy — keeps
  determinism simple).
- **Snapshot allocation (locked):** the three free ints in `FighterRuntimeComponent` become
  `LedgeAnchor` (platform index ×2 + side, −1 = none), `LedgeStateFrames`,
  `LedgeRegrabLockoutFrames`. This exactly fills the 128-byte budget — §2.10's tap timer
  only fits if the implementer can pack it into `LedgeRegrabLockoutFrames`' upper bits or
  another existing field without breaking clarity; otherwise see §2.10 fallback.
- `CpuDecisionObservation` gains `IsLedgeHanging` — added **field-for-field in both**
  `FighterCpuController.Observe` and `MirrorParadoxDecisionAdapter.Observe` (Package 6 §2.5
  alignment rule). CPU escape: a hanging CPU jumps (its recovery already emits Jump); add a
  behaviour test proving a hanging CPU is off the ledge within 120 frames.
- Presentation: driver animation branch (hang pose from existing placeholder set) + new
  `fighter_state_ledge` key — bump `FighterLocalizationTests` family counters
  (`fighter_* 25→26`, `fighter_state_* 10→11`).
- Story: existing marker-based `LedgeGrabPoint` system stays; one change — allow capture
  while rising slowly (`Velocity.Y ≥ −150` px/s instead of `≥ 0`) to match "jumping up to
  it". Story ledge authoring coverage is NOT expanded in this batch.

### 2.12 Facing follows movement during attacks

- Sim: `lockFacing` becomes `blockStance` only (drop `attacking`) —
  `FighterSimulationSystems.cs:290`. Story: `ProcessAttacking` and `ProcessRecoveryHold`
  call `UpdateFacing(GetHorizontalInput())` each frame. Hitbox placement continues to read
  facing at active-start (existing code) — the fix is orientation, not mid-active hitbox
  migration. **Supersedes** the 2026-08-09 "facing committed for the whole string" decision.

### 2.13 Superseded prior decisions (record, do not re-litigate)

| Prior decision (2026-08-09 parity pass) | Superseded by |
|---|---|
| Facing committed for the whole basic string | §2.12 |
| Hits 1–2 hold the victim through the chain (hitstun table margins) | §2.4 (block escape) |
| Sim drop-through is Down+Jump only (M-18 deliberate divergence) | §2.10 (best-effort) |
| No fast-fall (design-godot.md 1019) | §2.9 |
| Sim has no ledge (M-16 deferred) | §2.11 (partial M-16 close: ledge only) |

## 3. Workstreams and file ownership

Two phases. Phase A: three parallel Opus worktree agents. Phase B: two parallel Opus
worktree agents after A merges. Phase C: serial closeout in the main checkout
(orchestrator). Merge order within A: **A1 → A3 → A2**.

**Shared-file conflict policy:** `FighterSimulationSystems.cs`, `BasicComboRules.cs`,
`UniversalMovementRules.cs`, `PlayerController.cs`, and `localization/en.csv` are
multi-agent files. Each agent edits ONLY the regions its dossier names, keeps edits
additive where possible, and ends its final commit message body with a `SHARED-REGIONS:`
list naming every shared file + function it touched. en.csv additions go under a
`# Gameplay feel <WS>` comment marker, appended at end of file. Only the orchestrator
regenerates/commits `en.en.translation`. Nobody edits `AGENTS.md`/`CLAUDE.md` except the
orchestrator at closeout.

| WS | Scope (§) | Owns exclusively | Shared regions |
|---|---|---|---|
| A1 movement-feel | 2.1, 2.2, 2.3, 2.9, 2.10(sim tap timer decision) | nine `resources/Characters/*_data.tres`, `FighterStageGeometry.cs`, `scenes/fighter/FighterStage_Globe*.tscn`, `FighterStageGeometryTests.cs`, `Level00Controller` gate re-verify | `UniversalMovementRules.cs` (constants), sim movement region (`ApplyNormalMovement`, gravity/jump/drop-through blocks), `PlayerController` movement region (ramps, `ProcessAirborne` fast-fall, jump), `FighterSimulationComponents.cs` (Default loadout), movement determinism tests |
| A2 combat-rebalance | 2.4, 2.5, 2.6, 2.12 | 18 `resources/Abilities/*_special*.tres`, `DamageCalculator.cs`, `FighterEntitySystems.cs` (`ApplyFighterHit`), content damage-pin tests, `FighterBasicStringParityTests.cs` rewrite, new finisher-separation test | `BasicComboRules.cs` (KnockbackMultipliers), sim hitstun gate + `IsBlockStance` + `lockFacing` (`FighterSimulationSystems.cs`), `PlayerController` (`ProcessStunned`, `ProcessAttacking` facing, `StartComboHit` knockback), `EnemyController.OnHurtboxHit` |
| A3 attacks-and-inputs | 2.7, 2.8 | `project.godot [input]`, `InputManager.cs`, warp-direction call-site updates, new attack tests, control-legend en.csv edits | `BasicComboRules.cs` (new frame arrays — append), sim attack region (`StartSwing`, `ProcessBasicAttackPhase`, `ApplyBasicSwing`), `PlayerController` attack region (`CheckAttackInput`, timelines, hitboxes), `PlayerInputFrame.cs` (no new bits — comment updates only) |
| B1 ledge-grab | 2.11 | sim ledge implementation, `CpuDecisionObservation` + both Observe sites, driver hang presentation, `fighter_state_ledge` + counter bumps, CPU ledge-escape test, Story rising-capture tweak | `FighterSimulationSystems.cs` (movement update), `FighterRuntimeComponent` (the three ints), `PlayerController.TryGrabLedge` |
| B2 campaign-reachability | fallout of 2.1–2.3 on Story content | `tests/ContentValidation/Level{07,12,13,14,15}ContentTests.cs` + the level scenes/controllers they check, character-select stat card sanity | none expected |
| C closeout | — | `AGENTS.md`, `CLAUDE.md` baseline, this doc §9, `en.en.translation`, final ledger | — |

**B2 ground rule:** prefer retuning level geometry (keep the test's intent) over loosening
a test, and never weaken a two-sided assertion into a one-sided one; if a level's design
intent can't survive the new movement numbers, record the conflict in §9 and pick the
minimal geometry change.

## 4. Validation gates

- Every agent: `dotnet build FightersThroughTime.csproj --nologo` clean (1 known vendored
  warning), then **filtered** `dotnet test --settings .runsettings --filter ...` for its own
  suites ONLY, and only after polling `Get-Process testhost,Godot*` to zero (cross-worktree
  pipe contention, CLAUDE.md signature 5; the main checkout may have the GUI editor open —
  a non-console Godot process there is benign). Never a full suite in a worktree.
- Scene/`.tscn`/`.tres` edits: headless import check in the agent's own worktree.
- Orchestrator (phase C): the only full-suite runs — three consecutive greens with the
  exact expected `Total:`, plus `--import` regeneration of `en.en.translation` once per
  merge wave.
- Baseline entering this batch: **1417** passing. Each agent's report must state its exact
  expected test delta (added − removed).

## 5. Agent operating rules

- Worktrees fork from `main` HEAD — if this plan file is missing in your worktree, merge
  main first.
- Load authored `.tres` through `FTT.Core.AuthoredResources.Load<T>()`; never `Dispose()`
  a Resource; never author an empty Script-typed array in a `.tres`; keep `.uid` sidecars.
- Determinism boundary: no float math, no Godot state, no unseeded randomness in
  `scripts/FighterSim/`. All new sim state must live in snapshotted components.
- Reserved and untouchable: `GameplayButtons.Dash` (1<<11), `UniversalMovementPhase.Dash`,
  `CharacterState.Dashing`. New input needs no new bits (§2.7).
- Commit early, commit often on your branch; final commit message ends with the
  `SHARED-REGIONS:` list.

## 9. Deviation log (append-only)

(Agents append dated entries here — one per deviation from §2/§3, with the reason.)

### 2026-08-10 — A1 movement-feel

1. **§2.10 sim double-tap Down: SKIPPED, M-18 stays open in the sim→Story
   direction.** `FighterRuntimeComponent` sits at 116 of Klotho's 128 bytes and
   §2.11 locks its three remaining ints to B1's ledge state. The only packing
   sites available were `AttackFlags`' upper bits (A3 owns that field this
   batch — bits 8/16 for the new attacks) and `LedgeRegrabLockoutFrames`, which
   does not exist yet in this worktree and belongs to B1. Neither is a clean,
   non-conflicting home for an 18-frame tap timer, so the sim keeps Down+Jump as
   its only drop-through trigger, per §2.10's stated fallback. Nothing was added
   to either component.

2. **§2.1 roll decel: the roll's startup/recovery ramp switched to the decel
   constant.** `ProcessUniversalMovement`'s `runStep` (roll startup and roll
   recovery) only ever moves velocity toward zero, so it now derives from
   `RunDecelerationFrames` with every other grounded stop site rather than the
   accel constant it historically borrowed. Behaviourally this only touches roll
   startup — recovery begins with velocity already zeroed by the travel phase —
   and it shortens the roll's total ground travel from 2.2 to 1.925 units purely
   through the −15% move speed, not through this choice.

3. **§2.2 stage geometry: no platform beyond the Globe canopy needed lowering.**
   The reachability walker (`EveryAuthoredPlatformIsReachableAndLandableByJumping`)
   passes on all ten stages at `JumpSpeed` 11.5. Measured double-jump ceiling is
   ≈4.21 units (apex 2.108 per jump, second jump spent the tick velocity turns
   negative), so Orléans' 4.0 centre platform clears by ≈0.21 and Berlin's 3.6 by
   ≈0.61. Globe's canopy went 4.4 → 4.0 with its orb anchor 4.9 → 4.5 in all four
   places (`FighterStageGeometry.cs`, the `Dossiers` table, the scene's
   `Geometry/Balcony` body at pixel y 425 → 450, and `OrbSpawnPoints/Balcony` at
   393.75 → 418.75).

4. **§2.3 tutorial gate: the double-jump stack was rebuilt, and it was already
   broken before this batch.** `Level00Controller`'s mobility stack asked for a
   188 px hop from the floor to the first platform and another 240 px to the
   gate platform. No character has ever cleared 240 px, and before §2.3 the five
   single-jump kits could not clear even the first hop — Lincoln reached 113 px
   on his only jump. Both hops are now 128 px (`BuildPlatform(1700, 780, 200)`,
   `BuildPlatform(1900, 650, 180)`, gate at `(1900, 570)`), sized against
   Lincoln's post-retune 93.75 px single / 187.5 px double jump: the second jump
   is required and ~46% of headroom remains. Note the gate cannot *force* a
   double jump for the whole roster — Pocahontas clears 219.7 px on one jump,
   more than Lincoln manages on two — so it teaches rather than gates on the
   input.

5. **Movement determinism tests re-tuned rather than weakened.** Three Florence
   platform cases (`FighterLandsOnAFlorencePlatformAfterAJump`,
   `DropThroughLeavesThePlatformAndLandsOnTheSolidBaseFloor`,
   `WalkingOffAPlatformEdgeRemovesGroundSupport`) climbed 2.4 units on one jump,
   which is now above the 2.108 single-jump apex; they share a new
   `RiseOntoPlatform` helper that spends the second jump, keeping every original
   assertion. `RollPassesThroughOpponentAndIgnoresHitsOnlyDuringInvulnerableFrames`
   now walks the pair into pushbox contact (0.8 units) before rolling, because
   the 1.925-unit roll no longer crosses the full 2-unit spawn gap.
   `FighterCpuBehaviorTests.SimulateRecovery` re-aligned its integrator literals
   to the new default loadout (jump 13 → 11.5, move 8 → 7) **and** its modelled
   `remainingJumps` 1 → 2, since §2.3 means no single-jump kit exists any more;
   with one jump the Normal band's 15–20-frame reaction delay no longer fits
   inside the reduced apex from that starting depth.

6. **Test delta +3, no removals.** Added `ReleasedStickDeceleratesOverTheAuthoredStopRamp`,
   `HoldingDownInTheAirFastFallsImmediatelyButNeverDuringHitstun`, and
   `EveryAuthoredKitCarriesASecondJump`; `GroundRunAcceleratesOverEightFrames`
   was rewritten in place as `GroundRunAcceleratesOverTheAuthoredRunRamp`.
   Expected suite total after A1 alone: **1420**. Levels 07/12/13/14/15 content
   suites fail in this worktree (7 cases) — B2's scope, deliberately untouched.

### A3 attacks-and-inputs (2026-08-10)

1. **Directional knockback reaches 2.5 / 0.3 through the existing
   `verticalKnockbackScale` parameter, not a new one.** `ApplyFighterHit` derives
   *both* impulse axes from a single magnitude (`Velocity.x = ±force`,
   `Velocity.y = force * verticalScale`), so a separate horizontal factor is not
   expressible without changing that signature — and `FighterEntitySystems.cs`
   (`ApplyFighterHit`) is A2's exclusive region. A3 therefore folds the 0.3
   horizontal factor into the magnitude it passes and sends
   `DirectionalAttackVerticalKnockback / DirectionalAttackHorizontalKnockback`
   (2.5 / 0.3) as the scale. The delivered impulse is exactly §2.8's numbers:
   0.3x base horizontal, 2.5x base vertical. Both constants live in
   `BasicComboRules`; Story authors the two components directly on its hitbox.

2. **A directional input out of the chain-hold window starts the variant rather
   than continuing the string.** §2.8 specifies selection "at swing start"; a
   chain-hold continuation *is* a swing start, so Up + BasicAttack out of the
   hold window abandons the string and opens an up-attack with the combo index
   reset. Recorded because the plan text does not say so explicitly. Buffered
   continuations out of recovery are unaffected — they carry no recorded
   direction and always continue the chain.

3. **The Story enemy stun floor was extended to the two new hitbox IDs** (the
   A3 dossier's recommendation). `EnemyController.OnHurtboxHit`'s `combo_`
   prefix test — an A2 shared region — now calls a new shared predicate
   `BasicComboRules.IsBasicStringHitbox`, so `up_attack` and `down_air` hold a
   high-`StunResistance` enemy exactly as a chain hit does. One expression
   changed; no other line of that method was touched.

4. **Selection is a pure static helper**, `BasicComboRules.SelectAttackVariant(
   upHeld, downHeld, airborne)`, consumed verbatim by both modes and pinned
   directly by `DirectionalAttackTests`. The sim supplies `MoveY < -30` /
   the Down button bit; Story supplies `Vertical < -0.25` (the threshold is
   `BasicComboRules.StoryUpInputThreshold`) / `IsHeld(Down)`.

5. **Story pixel hitboxes** mirror the simulation's world-unit reaches at the
   repository's 62.5 px/unit convention: `up_attack` 150x150 centred at
   (0, -75), `down_air` 125x125 centred at (0, +62.5). Both are
   facing-independent (X offset zero), unlike the facing-mirrored chain boxes.
   Neither has an authored `basic_{ground|air}_N` animation, so both
   deliberately run on the frame clock with the opener's placeholder sprite
   animation as presentation scaffolding.

6. **`gameplay_up` carries no `GameplayButtons` bit and no arrow-key alias.**
   §2.7's default list is exactly W / stick-up / dpad-up; `gameplay_down`'s
   arrow-key alias was not mirrored. The action reaches gameplay only through
   `MoveY`, which is why `PlayerInputFrame.SerializedSize` stays 12 and the
   protocol is untouched. It is remappable (the Settings controls tab is
   data-driven off `RemappableActions`) and needs no save-schema migration,
   because only actions that differ from `project.godot` are stored.

7. **`en.en.translation` was regenerated locally to run the filtered suites and
   then reverted**, per §3's rule that only the orchestrator commits it. The
   same `--import` pass rewrote ~170 `.import` sidecars and
   `resources/Audio/default_bus_layout.tres` with line-ending-only churn; all of
   that was reverted too.

8. **Interaction found at the A1 merge — a down-air can only be thrown from
   height.** §2.9's fast-fall clamps any airborne Down-held fighter straight to
   16 units/s downward, so "jump, then hold Down and attack" lands the fighter
   before the 6-frame startup can finish; the move has to be thrown at or after
   the apex. That *is* §2.9's intended "down-air + fast-fall stack" reading (a
   falling downward strike) and no production code changed for it, but it makes
   the down-air noticeably harder to land than the pre-merge behaviour implied,
   and it is worth a human look during the playtest. `DirectionalAttackTests`
   now throws every down-air from a `JumpToApex` helper that advances until the
   fighter stops rising, so the suite re-derives the timing instead of pinning
   the numbers A1 happened to land on.

### 2026-08-10 — A2 combat-rebalance (§2.4, §2.5, §2.6, §2.12)

1. **Finisher multiplier landed at 4.5x** (§2.5's suggested starting point, kept). Measured
   mirror-match separation at **full victim HP**, read when the victim's hitstun ends, over all
   nine authored kits: cleopatra 3.06, leonardo 3.31, tesla 3.39, pocahontas 3.46, mozart 3.52,
   einstein 3.66, shakespeare 3.82, joan 4.01, lincoln 4.64 units — every one clears the 2.5-unit
   requirement, the tightest (cleopatra, base knockback 2.0 into weight 0.7) by 0.56. Pinned by
   `tests/Determinism/FighterKnockbackScalingTests.TheFinisherSeparatesEveryKitBeyondMeleeRangeAtFullVictimHP`.
   The test's protocol whiffs hits one and two from outside the 2-unit melee range so the finisher
   is the *only* hit that lands — otherwise "full victim HP" is unreachable, since §2.5's HP scaling
   would already be helping by the third hit. The analytic floor of that protocol (both fighters at
   the 0.8 pushbox minimum when the finisher connects) is 2.92 for cleopatra, so the pin has margin
   against A1's reduced move speeds changing where the two fighters meet.

2. **Story finisher knockback Y scaled with the multiplier, not re-derived.** `PlayerController`
   hit three went from `(baseKB * 3, -6)` to `(baseKB * 4.5, -9)` — the same 1.5x the horizontal
   multiplier took, preserving the authored launch angle. §2.5 only said "retuned in step"; a full
   1:1 X:Y match with the sim would have needed a per-character Y (the sim's vertical component
   equals its horizontal one), which is a larger change than this batch calls for.

3. **`BossController.ApplyKnockback` deliberately left unscaled.** §2.5's Story sentence names
   `DamageCalculator.CalculateKnockback`, `EnemyController.OnHurtboxHit` and
   `PlayerController.OnHurtboxHit` only, and the boss path is already divergent (it *adds* to
   velocity rather than replacing it, uses a hardcoded weight of 2, and most bosses are
   `IsKnockbackImmune`). Scaling it would also have moved the `BossControllerTests` velocity pins,
   which are outside this workstream's file list. Recorded as an open inconsistency rather than
   silently normalized.

4. **`RollbackHarnessKits` damage fields left at their synthetic values.** The A2 dossier listed
   them, but `RollbackReadinessKitShapeTests` pins only the *shape* fields (execution types,
   construct IDs) against the authored resources — it never asserts damage — and the harness is
   explicitly a synthetic engine-free kit, the same category as `FighterLoadout.Default`, which
   §2.6 keeps at 10. Changing them would have moved nothing but the numbers themselves.

5. **`ProcessRecoveryHold` does not call `UpdateFacing` itself.** §2.12 names both it and
   `ProcessAttacking`; the hold window is reached *only* through `ProcessAttacking`, which now
   applies the facing update before dispatching, so a second call would be redundant. Noted at the
   method with a comment so a future direct caller knows to re-check.

6. **Environment/hazard hits inherit the HP scaling structurally, not by a second edit.**
   `FighterDamageRules.ApplyEnvironmentHit` already delegates to `ApplyFighterHit`, so the single
   insertion covers hazards exactly as §2.5 requires. No separate hazard pin was added.

7. **Observed flake, not caused by this workstream:**
   `EnemyControllerTests.SmallTargetShuffleInsideTheBandDoesNotRestartTheApproach` failed once in a
   full-namespace unit run and passed in isolation, on the pre-change commit *and* after. Its own
   comment records why — the approach halts on the engine's out-of-band `MoveAndSlide` process
   delta, so where the enemy stops depends on wall-clock timing, and the 25 px shuffle in the test
   sits within ~0.25 px of the 110% release line. Re-runs were green (611/611). Worth tightening
   independently of this batch.

### 2026-08-10 — B2 campaign-reachability (fallout of §2.1–2.3 on Story content)

**Every repair is geometry, not test text.** All seven failures were fixed by moving the level's own
numbers so the authored assertion — including both sides of every two-sided one — still holds with
margin. No helper's meaning was changed, no assertion was loosened, no `singleJumpOnly` conservatism
was traded for the new universal double jump, and the test delta is **0 added / 0 removed**. The
reference values the repairs are sized against, all re-derived from the retuned `.tres` files:
worst single-jump rise (Lincoln) **93.75 px** (was 113.4), best single rise (Pocahontas) 219.7,
best total rise 439.4, weakest single-jump *distance* (Lincoln) **174.0 px** (was 223.8), best full
jump distance 965.1 (was 1285.8), slowest ground speed (Lincoln) **285 px/s** (was 330).

1. **Two compensation shapes, chosen per level by whether the level owns a gravity mechanic.**
   Where a level authors its own gravity field (L12's regolith/pad, L14's containment pockets), the
   *field* absorbed the retune — every scale scaled **×0.8**, exactly the jump-height reduction — so
   not one authored rung, lift, curtain or shaft altitude had to move and every internal contrast
   ratio is preserved bit-for-bit. Where the level runs at Earth-normal gravity (L13's arena
   platform, L14's portal scaffold, L15's tiers), the *step* came down instead, each re-sized to the
   same percentage of the heaviest character's reach it was originally authored at. L07 has neither,
   so its rope reach moved. Recorded because "scale the field, not the geometry" is the cheaper and
   more faithful repair whenever a level has a field to scale, and later movement passes should
   reach for it first.

2. **L07 Nassau — `AnchorAmplitudeDegrees` 45 → 52; nothing else moved.** The chain failed on hops
   two and three (139.2 px against a budget that fell from 167.8 to 130.5). The span from the launch
   yard to the receiving yard is fixed at 1,950 px and the three ropes consume `3 × 680 × sin(amp)`
   of it, so the *only* levers are rope reach, channel width and rope count — pivot spacing is
   zero-sum, and evening the pivots out at 45° would have left every hop at 97% of budget. Amplitude
   is the one lever the level owns outright (the plan records rope length as fixed by
   `PendulumAnchorTemplate` and not overridable). At 52° the pivots stay exactly where they were
   (3950 / 4570 / 5190) and the four hops become 97.8 / 84.2 / 84.2 / 76.3 px — worst case 75% of
   budget, against 107% before. Both yard clearances still hold (first grab 3697.8 > 3600, last hang
   5473.7 < 5550), the arc sweep rises 509 → 567 px (test floor 400), and peak tangential speed
   rises 740 → 855 px/s, still under the scene's authored `MaxLaunchSpeed` 900. The uncrossable-
   channel claim got *stronger*, not weaker: 2,000 px is now 2.07× the best full jump where it was
   1.56×. The Globe contrast the orchestrator adjudicated on 2026-08-08 survives — Nassau moves
   further outside Globe's ≤34° cap, not closer to it. Scene: three `AmplitudeDegrees` 45 → 52; the
   `AnchorPivotY` doc comment's arc extreme corrected 654 → 622 px.

3. **L12 Lunar — `RegolithGravityScale` 0.45 → 0.36 and `PadGravityScale` 0.6 → 0.48.** The pad
   gantries were the hard failure (`padJump` 156.3 < the authored 170 px rise); the rung contract
   passed only by 4.2% (200 px rungs against a 208.3 px lunar reach), which is not a climb a player
   can actually make. Both are the same cause and take the same ×0.8. Every authored altitude stays:
   rungs now use 76.8% of the 260.4 px lunar reach (was 79.4% pre-batch — restored, not merely
   fixed), the Earth-side half of the two-sided assertion is *stronger* than before (200 px is now
   2.13× the heaviest Earth jump, was 1.76×), `padJump` is 195.3 against the 170 px gantry, the two
   lifts and the curtain slot all still out-reach the best multi-jump (spire 1500 > 1220.4, curtain
   1350 > 1220.4), and the pad/regolith contrast stays at exactly 4:3.

4. **L13 Chronal Void — `ArenaPlatformY` 1810 → 1825.** Earth-normal arena, no field to scale, so
   the 90 px rise came down to 75 px: 80% of the heaviest character's 93.75 px reach, against the
   79.4% it was authored at and the 90% the test caps it at. Mirror symmetry, offset, width and the
   unbroken-floor node name are untouched.

5. **L14 Neo-Earth — three separate repairs, all in the controller.**
   - **Pocket scales ×0.8** (0.34/0.30/0.26 → 0.272/0.24/0.208). All nine shaft rungs stay exactly
     where they are and both sides of the two-sided contract widen: alpha's 300 px rungs use 87% of
     its 344.7 px in-pocket reach *and* are 1.37× the best Earth-normal single jump (the floor the
     shaft must beat), beta 84.5% / 1.50×, gamma 75.4% / 1.55×; every shaft's total climb still
     out-reaches the best Earth multi-jump by more than 2×. The deepening 17:15:13 ratio between the
     three pockets is preserved exactly.
   - **`PortalScaffold` step 100 → 80 px** (rungs 1620/1540/1460/1380). This one is deliberately
     *outside* every pocket — that contrast is the test's whole point — so there is no field to
     scale and the step had to move. 85.3% of the heaviest reach, against 88.2% as authored. Still
     clear of all three pocket rects.
   - **`ReferenceWalkSpeed` 330 → 285**, and the beam phase offsets are now **derived** rather than
     authored. The constant is defined as the slowest ground speed in the roster and the test
     re-derives it, so it had to follow Lincoln. Rather than hand-editing sixteen literals I added
     `BeamPhaseStrideSeconds => BeamSpacing / ReferenceWalkSpeed` plus a `BeamPhase(i)` helper and
     expressed every beam as `BeamPhase(n)` — the corridor grids at stride 1, the arena lanes at
     stride 2. **Beam X positions did not move**: `BeamSpacing` stays 330 px because it is authored
     corridor geometry, and the invariant the puzzle actually depends on (phase stride ==
     spacing ÷ walk speed, so the safe window travels with the walker) is now structural instead of
     coincidental. The stride is no longer a round 1.0 s but 1.158 s, so the one test comment and
     the one doc comment that said "one reference-walk second" were corrected to say what the code
     does. Solvability re-checked by hand as well as by the suite: contact time rises 0.485 → 0.561 s
     against dark windows of 2.1–4.7 s.

6. **L15 Alexandria — gallery tier step 100 → 80 px** (`Tier1..5Y` 1320/1240/1160/1080/1000). No
   gravity mechanic in this level at all, by design, so the tiers themselves came down: 85.3% of the
   heaviest character's reach against 88.2% as authored. The scriptorium's vertical section is now
   400 px rather than 500, still 4.27× the heaviest single jump (the test's bar is 3×; it read 4.41×
   before), and the three extractors ride the tier constants automatically. One scene edit follows
   the balcony: `FireScriptorium` moved (5330, 900) → (5330, 1000), because the hazard is authored on
   the scriptorium balcony and would otherwise have been left floating 100 px above it. No test pins
   that position — it would have been a silent visual break.

7. **Nothing else in Story content proved movement-sensitive.** Swept the full `ContentValidation`
   namespace plus `FTT.Tests.Integration` and `FTT.Tests.Unit`; the tutorial gate (A1's), the hub,
   Florence, levels 2–11 and the character-select stat card all pass untouched. No non-movement
   failure was observed in any of the three namespaces, so nothing was left unfixed and nothing
   outside scope was touched.

8. **Test delta 0 (0 added, 0 removed).** The five level suites report exactly their authored case
   counts — L07 21, L12 19, L13 22, L14 25, L15 21 = **108/108**. Namespace runs:
   `ContentValidation` **558/558**, `FTT.Tests.Integration` **16/16**, `FTT.Tests.Unit` **615/615**,
   all 0 failed. Build clean (1 known vendored `CS8632`). `--headless --quit` clean, and all five
   edited levels smoke-run 240 frames with no script errors (the `N resources still in use at exit`
   line is the `AuthoredResources` cache pin and appears identically on an untouched control level).
   Expected suite total after B2 is unchanged from whatever A1+A2+A3 leave it at.

9. **One run hit CLAUDE.md failure signature 5** (`GodotRuntimeTestRunner ends with exit code: 100`,
   `Failed to connect: Connection timeout`, then `No test matches the given testcase filter` on a
   filter that plainly matched) while B1 was validating in its sibling worktree. Polling
   `Get-Process testhost,Godot*` to zero and re-running was clean. Not a regression; recorded because
   the narrow-filter variant reports *zero* matches rather than a partial total.

10. **`--import` churn reverted.** The worktree was cold, so the first `--quit` failed on unimported
    menu textures and needed a `--import` pass; that rewrote ~180 `.import` sidecars plus
    `localization/en.en.translation` and `resources/Audio/default_bus_layout.tres`. All of it was
    reverted — §3 leaves the compiled translation to the orchestrator, and B2 added no keys.

### 2026-08-10 — B1 ledge-grab (§2.11)

1. **Grabs are resolved last in the movement tick, so landing and the ground snap
   always win.** §2.11 says "after gravity/velocity, around the landing checks"
   without fixing the order. Running the capture *before* `TryLandOnPlatform`
   would have turned every landing within half a unit of a platform end into a
   hang; running it after both the landing and the `y <= 0` ground snap means only
   a fighter still airborne after the whole resolve is a candidate. `CanGrab`
   re-checks `IsGrounded == 0` so the ordering is stated in the rulebook too, not
   just implied by the call site.

2. **Consequence of the locked capture conditions: walking off a platform end
   grabs it.** The condition list in §2.11 is closed — airborne, no drop-through,
   no hitstun/daze, no lockout, `vy <= 2`, inside the capture box — and a fighter
   who walks off an edge satisfies every one of them (`vy == 0`, `y == SurfaceY`,
   `x` a hair past the edge). This is standard platform-fighter behaviour and was
   implemented as specified rather than adding an unlisted "must be falling from
   above" clause. Walking off and holding Down is the way out, and the 30-frame
   regrab lockout is exactly what makes that work. `FighterLedgeTests` uses this as
   its cheapest deterministic route onto a ledge. Flag it for the playtest: if it
   reads badly, the fix is one added condition, not a redesign.

3. **Two extra release paths §2.11 did not enumerate, both required for the state
   to be safe.** Being struck out of a hang (`HitstunFrames`/`DazeFrames`) drops
   the hang *without* zeroing velocity — the combat system has already written the
   knockback — and arms the lockout so the victim does not instantly re-catch the
   anchor it is still standing in. Losing a stock clears both the hang and the
   lockout in `ApplyStockLoss`, so the respawn platform cannot inherit either.
   Without the first, a hanging fighter was effectively immortal on the ledge: the
   hang short-circuit runs ahead of the hitstun branch and consumes the tick.

4. **`LedgeAnchor` is written to −1 at fighter creation.** Klotho zero-initializes
   components and `0` is a valid anchor (platform 0's left edge), so the "none"
   sentinel §2.11 locks has to be authored in `FighterWorldSystem` rather than
   inherited from the default. Anything that builds a `FighterRuntimeComponent` by
   hand — tests included — must do the same.

5. **The hang pose is the existing `ledge_hang` animation, not a borrowed one.**
   §2.11 said "hang pose from existing placeholder set" and the dossier suggested
   crouch or hitstun; all nine character `SpriteFrames` already carry a real
   `ledge_hang` clip (Story's `LedgeHanging` state uses it), so the Fighter driver
   reuses that. No new art and no compromise pose. `GetStateLabel` gained a
   matching `"Ledge Hang"` → `fighter_state_ledge` row.

6. **The CPU's ledge branch is checked ahead of off-stage recovery.** A hanging
   fighter is by definition already holding the stage edge, so recovery's
   drift-and-jump would spend the jump budget the grab just refilled. `Decide`
   answers `Jump` and nothing else while `IsLedgeHanging` is set.

7. **The component budget is now exactly full.** `FighterRuntimeComponent` moved
   from 116 to 128 of Klotho's 128 bytes with the three ints, as §2.11 predicted.
   There is no packing room left in it — the next piece of sim state needs its own
   component. §2.10's fallback (no double-tap Down in the sim) therefore stands;
   A1 already recorded it and nothing here reopens it.

8. **`FighterStageGeometry` gained pure helpers, not state.** `EdgeX`,
   `HangPosition` and `IsInCaptureBox` on `FighterStagePlatform`, plus
   `TryFindLedge` and `TryGetHangPosition` on the geometry. Geometry still never
   enters a snapshot: the anchor int is snapshotted and the position is re-derived
   from it every tick, which is what makes rollback and full-state restore work
   with no extra plumbing. `TryFindLedge` scans platform index ascending, left edge
   before right, so overlapping capture boxes resolve identically on every peer.

9. **`en.en.translation` was regenerated locally by the `--import` this worktree
   needed, but is deliberately not committed.** Per §3 only the orchestrator
   regenerates it. `FighterLocalizationTests` and `UnusedTranslationKeyTests`
   resolve through the *compiled* resource, so `fighter_state_ledge` will fail
   there in any checkout that has not reimported since this branch merged.

10. **Cold-import artefact, not a regression.** The first filtered run in this
    fresh worktree failed three resource-loading tests
    (`FighterSimulationTests.LoadoutFactoryUsesNormalizedCharacterResources`,
    `EveryAuthoredKitCarriesASecondJump`,
    `RollbackReadinessKitShapeTests.HarnessKitShapesMatchTheAuthoredCharacterResources`)
    with `AuthoredResources.Load` returning null. All three passed after
    `--headless --import` warmed the `.tres` cache. Worth knowing for the other
    Phase B worktree.
