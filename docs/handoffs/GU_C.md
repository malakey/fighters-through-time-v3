# GU-C handoff: movement speed −20%, jump distance −20%, jump height unchanged (both modes)

**Workstream:** GU-C of the 2026-10-12 gameplay update (`docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` §2.4, §4). **User request 4, verbatim:** "I want to reduce the movement speed by about 20%, and the jump distance by the same. The jump height should remain the same." Scope ruling: **both modes**.
**Branch:** `worktree-wf_21dd1c67-9be-3`, from plan commit `b8f5343`. **Status:** complete.
**Nobody played the build.** Every claim below is a headless test, a real-frame measurement harness (`_PhysicsProcess` stepping with `--fixed-fps 60`, and the Fighter sim stepped tick by tick) or a bot nav-graph dump.

## 1. Shipped

- **The nine `resources/Characters/*_data.tres` `MaxMoveSpeed` × 0.8 exactly** (line 20 of each file; nothing else in them touched):

  | Hero | Before | After | Story run px/s |
  |---|---|---|---|
  | cleopatra | 7.25 | **5.8** | 435 → 348 |
  | einstein | 6.75 | **5.4** | 405 → 324 |
  | joan | 7.75 | **6.2** | 465 → 372 |
  | leonardo | 6.5 | **5.2** | 390 → 312 |
  | lincoln | 4.75 | **3.8** | 285 → 228 |
  | mozart | 6.75 | **5.4** | 405 → 324 |
  | shakespeare | 6.5 | **5.2** | 390 → 312 |
  | tesla | 6.0 | **4.8** | 360 → 288 |
  | tubman | 6.75 | **5.4** | 405 → 324 |

  `MaxJumpForce`, `AirControlMultiplier`, `MaxJumpCount`, `Weight` and the dead `Acceleration` / `GroundFriction` fields are unchanged. Both modes read the one value: Story through `PlayerController.EffectiveMoveSpeed`, the sim through `FighterLoadoutFactory.FromCharacterData` → `FighterTuningComponent.MoveSpeed`. In both modes the air speed cap is the run speed and no jump writes horizontal velocity, so jump distance falls by exactly a fifth and jump height and airtime do not move (measured, §6).
- **`FTT.Core.UniversalMovementRules.RollSpeedMultiplier` 1.5 → 1.875** (= 1.5 / 0.8), with a doc comment. It is read at all four roll sites — Story roll travel (`PlayerController.ProcessRolling`) and Story roll get-up (`PlayerController`'s knockdown get-up), sim roll travel (`FighterMovementSystem`, `FighterSimulationSystems.cs` `RollSpeedMultiplier` FP64) and sim roll get-up (`FighterVerbRules`) — so the roll's travel frames and the knockdown roll get-up keep their pre-cut distance in both modes. 1.875 is exact in binary, so the `FP64.FromDouble` conversion is exact.
- **`Level14Controller.ReferenceWalkSpeed` 285 → 228** (doc comment updated: "3.8 x 60 px/s", "285 -> 228 with the 2026-10-12 gameplay update's 20% movement cut"). `BeamPhaseStrideSeconds` = 330 / 228 = 1.447 s (was 1.158 s); the grid phases are derived at runtime, so no scene value moves.
- **Escape fronts × 0.8:** `scenes/campaign/Level_06_Pompeii.tscn` `EscapeRun.AdvanceSpeed` 280 → **224**; `scenes/campaign/Level_15_Alexandria.tscn` `Firestorm.AdvanceSpeed` 270 → **216**.
- **One geometry fix the audit proved (§6.3): Level 11's railcut top perch widened** — `Level11Controller.BuildRoom4Railcut`, `BuildPlatform(10250, 480, 220)` → `BuildPlatform(10250, 480, 340)` (centre and height unchanged; now x 10080–10420), with a comment.
- **`FighterLoadout.Default` stays `MoveSpeed` 7** (the hand-built test loadout); its comment now says so and that it is faster than every roster hero after the cut.
- **Absolute-speed kit motions are unchanged** (not edited, so they no longer scale with the run): Tesla's Lightning Blink (3.0 u, 900 px/s), Einstein's Relativity Warp fold (up to 4 u), Tubman's North Star Leap (4 u, 800 px/s), Lincoln's Rail Charge (5 u / 30 frames, 600 px/s), Cleopatra's sand rush (4 u, 960 px/s), Shakespeare's Prospero gust (4 u), Joan's Wing-Dive (5 u/s), Mozart's glissando (180 px), Leonardo's glide (380 px/s × `StoryGlideSpeedMultiplier`), the air dodge's 1.0 u shift, the rope release (≤ 900 px/s), knockback and every enemy and boss speed. Echo Step (the position 30 ticks back) shrinks with the run on its own. Jumps assisted by these shrink by less than a fifth.

## 2. Files changed

| File | Change |
|---|---|
| `resources/Characters/{cleopatra,einstein,joan,leonardo,lincoln,mozart,shakespeare,tesla,tubman}_data.tres` | `MaxMoveSpeed` × 0.8 (one line each) |
| `scripts/Core/UniversalMovementRules.cs` | `RollSpeedMultiplier` 1.875 + doc comment |
| `scripts/Environment/Level14Controller.cs` | `ReferenceWalkSpeed` 228 + doc comment |
| `scenes/campaign/Level_06_Pompeii.tscn` | lava front `AdvanceSpeed` 224 |
| `scenes/campaign/Level_15_Alexandria.tscn` | firestorm `AdvanceSpeed` 216 |
| `scripts/Environment/Level11Controller.cs` | railcut perch width 220 → 340 (+ comment). A "minimal geometry fix the audit proves necessary" (my ownership); **GU-L8 owns Level 11 in Phase B** — the edit is the one `BuildPlatform` line in `BuildRoom4Railcut`. |
| `scripts/FighterSim/FighterSimulationComponents.cs` | **outside my region**: comment only, in `FighterLoadout.Default` (the plan asks for stale speed comments to be updated; GU-A owns the `AttackFlags` doc elsewhere in this file). No code change. |
| `tests/ContentValidation/TubmanContentTests.cs` | pin 6.75 → 5.4 (+ comment) |
| `tests/Determinism/FighterMovementFeelTests.cs` | doc comment of the run-ramp case (it listed the old speeds); no code change |
| `tests/Unit/StoryMovementSpeedCutTests.cs` (+ `.uid`) | new suite, 3 cases |
| `tests/Determinism/FighterMovementSpeedCutTests.cs` (+ `.uid`) | new suite, 2 cases |
| `docs/handoffs/GU_C.md` | this file |

Not touched (owned elsewhere or not needed): `PlaytestNav` (`HeroPhysics.RunSpeed` reads `MaxMoveSpeed` from the data, so the bots follow automatically), `CpuRecoveryProfile` (built from the loadout), the Story camera look-ahead (GU-B), `CharacterSelectScreen` / `CalibrationDrillPicker` (their `fighter_stats_summary` line prints the new numbers as is).

## 3. Decisions (Provisional; each a ledger row candidate)

1. **Exact × 0.8** (one-decimal values) rather than the 2026-08-10 convention of rounding to 0.25, which would have cut 18.5–21%. The plan states this.
2. **The roll keeps its distance** (`RollSpeedMultiplier` 1.875, both modes), and so does the roll get-up, which reads the same constant in both modes. The plan states this. Side effect, measured: the 4-frame stop ramp after a roll's travel (Story) and after a roll get-up (both modes) scales with the run speed while the roll speed did not drop, so the trailing slide is slightly longer — Story roll total Lincoln 103.3 → 108.8 px, Joan 168.6 → 177.5 px; Story get-up slide Lincoln 17.8 → 23.3 px; sim get-up slide Lincoln 0.297 → 0.388 u. The sim roll zeroes its velocity at the end of travel, so its total is identical. Left as is (the alternative is a roll-specific stop ramp).
3. **`FighterLoadout.Default.MoveSpeed` stays 7** (plan). Every CPU, hazard and recovery suite that runs on it keeps its premise; it is now faster than every roster hero (the fastest is Joan, 6.2).
4. **Escape fronts × 0.8** (plan), so every hero keeps today's speed ratio against them (Lincoln 228 : 224 against 285 : 280). Level 6's 90 px head start was not scaled, so it is now relatively larger.
5. **Level 11's railcut perch 220 → 340 px wide** (my choice of fix). 250 px gaps to each side platform are the old 310 px gaps × 0.8; widening one platform symmetrically moves nothing else.
6. **Not fixed, judged not broken** (§6.3): Level 10's trapdoor gaps, Level 7's ropes and skiffs, the Level 12/13 low-gravity crossings, Level 0's mobility gates, Level 9's lanterns, the Level 5 flood and Florence's crumbling scaffolding.

## 4. Deviations from the plan

- **Two new suites (+5 cases)** pin the decision (the plan asked only for tests whose pinned speeds move). They name the pre-cut values once, as a record of the cut, and fail if a later retune moves a run speed or the roll without reconsidering the other.
- **The roll compensation's stated rationale does not hold as written, though the decision stands.** The plan (from the recon) says the 1.875 multiplier is what keeps Lincoln's Story roll-through of a standard mob (recon: 82 px needed) and his sim cross-up (recon: 1.2 u needed). Measured from pushbox contact with the multiplier left at 1.5 and the new speeds, both **still pass**: in Story the roll's recovery frames resolve a remaining pushbox overlap in the roll direction (`CombatantPushbox.ResolveStoryOverlaps(_rollDirection)`), and the hero and a `chrono_slasher` are only (36 + 38) / 2 = 37 px apart at contact (the recon used body widths), so Lincoln ended 46 px past the mob's centre (68.4 px of travel); in the sim the pushbox resolves by side once the roller is past the opponent's centre, 0.6 u from contact, and Lincoln ended 0.60 u past (1.045 u of travel). With 1.875 he ends 72 px / 0.71 u past. The compensation keeps the **distance** (and so every margin); it was not needed for the pass-through itself. Recorded so the closeout's ledger row states the right reason.
- **The Level 11 fix** is a geometry change the plan did not anticipate (§6.3).

## 5. Tests

**New: +5 cases in two new suites.**
- `tests/Unit/StoryMovementSpeedCutTests.cs` (`[RequireGodotRuntime]`, real `PlayerController`s built with `applyStoryProgression: false`), 3 cases:
  - `EveryRunSpeedIsItsPreCutValueTimesFourFifths` — the nine `MaxMoveSpeed` values equal the recorded pre-cut table × 0.8 (a hero added later is skipped), and `RollSpeedMultiplier × 0.8 == 1.5`.
  - `AJumpKeepsItsHeightAndAirtimeAndCoversAirtimeTimesTheRunSpeed` — Lincoln and Joan, single and double: a running jump's rise and airtime equal the same hero's standing jump (rise ±0.1 px, airtime exact), the single rise equals `StoryAirPhysics.SingleJumpRise`, and the distance is airtime × run speed (±1 px), 0.8 of the pre-cut distance.
  - `TheRollAndTheRollGetUpKeepTheirPreCutTravelAndLincolnStillRollsThroughAMob` — Lincoln and Joan: exactly `RollTravelFrames` (12) ticks at the pre-cut roll speed (4.75 × 60 × 1.5 = 427.5 px/s for Lincoln), covering the pre-cut travel (85.5 / 139.5 px); the 14-frame roll get-up covers the pre-cut 99.75 / 162.75 px; and Lincoln, rolling from pushbox contact with a `chrono_slasher`, ends on its far side with no overlap.
- `tests/Determinism/FighterMovementSpeedCutTests.cs` (`[RequireGodotRuntime]`, loadouts from the `.tres` through `FighterLoadoutFactory`), 2 cases:
  - `AJumpKeepsItsHeightAndAirtimeAndCoversAirtimeTimesTheRunSpeed` — Lincoln and Joan, single and double: running and standing jumps have identical rise (raw FP64) and airtime; the distance is airtime × `MoveSpeed` (±0.01 u).
  - `TheRollAndTheRollGetUpKeepTheirPreCutTravelAndLincolnStillCrossesUp` — Lincoln and Joan: the roll and the 14-frame roll get-up cover the pre-cut speed × 1.5 over their moving ticks (±0.005 u); Lincoln, walked into pushbox contact with the default opponent, rolls to its far side.
- Discrimination: with `RollSpeedMultiplier` temporarily back at 1.5, the Story and sim roll cases and the run-speed pin fail (3 of 5), as they should; the jump cases do not depend on it.

**Rewritten in place (no count change):** `TubmanContentTests.TubmanCarriesTheDesignStatLine` (`MaxMoveSpeed` 6.75 → 5.4). Comment only: `FighterMovementFeelTests` (the run-ramp case's doc comment listed the old speeds).

**No existing test broke on the slower speeds.** Targeted run (lock held): `FighterStageGeometryTests` 17, `CpuRecoveryMatrixTests` 11, `FighterKnockbackScalingTests` 3, `RollbackReadinessTests` 22, `FighterCpuBehaviorTests` 31, `FighterMovementFeelTests` 8, `FighterSimulationTests` 23, `Level07ContentTests` 21, `Level14ContentTests` 26, `Level06ContentTests` 14, `Level15ContentTests` 22, `StoryMovementFeelTests` 12, `ResonanceLiveApplyTests` 6, `TubmanContentTests` 4, `TutorialCalibrationContentTests` 4, `StoryAirPhysicsTests` 9, plus `Level11ContentTests` 16, `ArenaGeometryContentTests` 16, `SecretCachePlacementTests` 3, `Level10ContentTests` 21, `Level12ContentTests` 22, `Level13ContentTests` 22, `FighterKnockdownTests` 6, `StoryKnockdownTests` 4 — **`Passed: 343, Total: 343`**, equal to their `[TestCase]` count (after the Level 11 change). The two new suites: `Passed: 5, Total: 5`.

**Full suite (lock held, orphans drained, after the Level 11 change and the new suites):** `Passed!  - Failed: 0, Passed: 3026, Skipped: 0, Total: 3026, Duration: 1 m 9 s`. **3026 = 3021 + 5.** The `[TestCase]` attribute count under `tests/` is 3026. Build: 0 errors, the three pre-existing warnings only (`CS8632`, `CS0649`, `KLSG_ECS004`); no `KLOTHO_DET002`. Headless `--import` exit 0 with the edited `.tres` / `.tscn` in place.

Every Fighter hash moves for roster loadouts (the speeds, the roll); no suite pins an absolute hash.

## 6. Measurements

### 6.1 Jumps (height unchanged, distance × 0.8)

Real frames, before (`b8f5343`) → after, jump taken from full run speed with forward and Jump held; double jump = second press on the first tick after the rise stops; distance from take-off to landing at the same height; airtime includes the jump and landing ticks.

**Story** (px):

| Hero | Rise ×1 | Dist ×1 | Air ×1 | Rise ×2 | Dist ×2 | Air ×2 |
|---|---|---|---|---|---|---|
| cleopatra | 213.74 | 558.27 → 446.62 | 77 | 427.42 | 993.27 → 794.62 | 137 |
| einstein | 186.43 | 465.77 → 372.62 | 69 | 372.72 | 823.52 → 658.82 | 122 |
| joan | 172.96 | 503.78 → 403.03 | 65 | 345.72 | 899.03 → 719.23 | 116 |
| leonardo | 177.45 | 442.03 → 353.62 | 68 | 354.69 | 786.53 → 629.22 | 121 |
| **lincoln** | 132.60 | **251.77 → 201.42** | 53 | 265.13 | **441.77 → 353.42** | 93 |
| mozart | 201.72 | 499.53 → 399.63 | 74 | 403.26 | 891.03 → 712.83 | 132 |
| shakespeare | 185.83 | 455.03 → 364.02 | 70 | 371.51 | 806.03 → 644.82 | 124 |
| tesla | 181.29 | 420.02 → 336.02 | 70 | 362.41 | 744.02 → 595.22 | 124 |
| tubman | 186.43 | 465.77 → 372.62 | 69 | 372.72 | 823.52 → 658.82 | 122 |

Rises and airtimes identical before and after (to 0.01 px); every distance ratio is 0.8000.

**Fighter sim** (units):

| Hero | Run | Rise ×1 | Dist ×1 | Air ×1 | Rise ×2 | Dist ×2 | Air ×2 |
|---|---|---|---|---|---|---|---|
| cleopatra | 7.25 → 5.8 | 2.6542 | 6.525 → 5.22 | 54 | 5.3042 | 11.7208 → 9.3767 | 97 |
| einstein | 6.75 → 5.4 | 2.5375 | 5.85 → 4.68 | 52 | 5.0708 | 10.575 → 8.46 | 94 |
| joan | 7.75 → 6.2 | 2.45 | 6.7167 → 5.3733 | 52 | 4.8958 | 12.0125 → 9.61 | 93 |
| leonardo | 6.5 → 5.2 | 2.3375 | 5.4167 → 4.3333 | 50 | 4.6708 | 9.75 → 7.8 | 90 |
| **lincoln** | 4.75 → 3.8 | 2.1458 | **3.8 → 3.04** | 48 | 4.2875 | **6.8083 → 5.4467** | 86 |
| mozart | 6.75 → 5.4 | 2.5375 | 5.85 → 4.68 | 52 | 5.0708 | 10.575 → 8.46 | 94 |
| shakespeare | 6.5 → 5.2 | 2.45 | 5.6333 → 4.5067 | 52 | 4.8958 | 10.075 → 8.06 | 93 |
| tesla | 6.0 → 4.8 | 2.3375 | 5.0 → 4.0 | 50 | 4.6708 | 9.0 → 7.2 | 90 |
| tubman | 6.75 → 5.4 | 2.5375 | 5.85 → 4.68 | 52 | 5.0708 | 10.575 → 8.46 | 94 |

Paris's 5.0 u courtyard pit against Lincoln's 5.45 u double jump (plus the 0.5 u ledge capture) stays crossable; `FighterStageGeometryTests.EveryAuthoredPitIsEscapableByEveryCharacter` passes.

### 6.2 Roll and roll get-up (before → after)

| | Lincoln | Joan |
|---|---|---|
| Story roll travel (12 frames) | 85.50 → **85.50** px | 139.50 → **139.50** px |
| Story roll, total from a standstill to rest | 103.31 → 108.77 px | 168.56 → 177.47 px |
| Story roll get-up (14 frames) | 99.75 → **99.75** px | 162.75 → **162.75** px |
| Story get-up trailing slide | 17.81 → 23.27 px | 29.06 → 37.97 px |
| Sim roll (11 moving ticks; velocity zeroed at travel end) | 1.3063 → **1.3063** u | 2.1313 → **2.1313** u |
| Sim roll get-up (14 frames) | 1.6625 → **1.6625** u | 2.7125 → **2.7125** u |
| Sim get-up trailing slide | 0.2969 → 0.3879 u | 0.4844 → 0.6329 u |

All nine heroes' Story roll travel and sim roll totals are identical before and after (Story 85.5–139.5 px, sim 1.30625–2.13125 u, Lincoln to Joan). Without the compensation (cut speeds, multiplier 1.5) Lincoln's Story roll travel would be 68.4 px and his sim roll 1.045 u. Roll-through from pushbox contact, after: Story Lincoln ends 72 px past a `chrono_slasher`'s centre (46 px with the multiplier at 1.5); sim Lincoln ends 0.706 u past the default opponent (0.600 u at 1.5) — see §4.

### 6.3 Content audit for the slowest hero (Lincoln: run 228 px/s, single jump 201 px, double 353 px)

**Method.** The bots' own arc planner (`PlaytestNav`, which simulates every jump frame by frame through `StoryAirPhysics` at the hero's real run speed) dumped its graph for Lincoln on all sixteen levels before and after the cut (`PlaytestRunner.tscn -- --level=N --character=lincoln --bot=play --difficulty=normal --nav-dump=1 --max-seconds=1`, tags `gucbefore` / `gucafter`, lock held), and a script compared the segment-to-segment **reachability closure**. Jump edges were lost on fifteen levels (L01 12, L02 4, L03 4, L04 7, L05 17, L06 9, L07 14, L08 9, L09 6, L10 12, L11 14, L12 16, L13 15, L14 2, L15 6; L00 0), but **every lost edge had an alternative route except one**: Level 11's railcut top perch (`Platform_10250_480`, then x 10140–10360 at y 480), reachable before only by a double jump from either 690-high side platform across a 310 px gap. Lincoln's best arc to its height now carries 285 px (75 airborne ticks at 228 px/s; 356 px before), against about 272 px of body travel from edge to edge — a 13 px, frame-perfect margin for a human and none for the planner. **Fixed** by widening the perch to 340 px (gaps 250 px); the re-dump (tag `gucfix`) shows `Jump2` edges onto it from both side platforms. Nothing is placed on the perch; it is the arena's optional high ground against the Siege Cannon. Limits of the method: the dump is taken at frame 30 (moving platforms and skiffs at their frame-30 positions, trapdoors shut) and Level 0's Part 3 is built later in the level, so those were checked by hand:

- **Level 10 trapdoors** (220 px gaps): shut traps are floor; an open trap is a 220 px gap against Lincoln's 353 px double jump, and a fall is the designed detour through the cellar's climb back. The 0.75 s shake warning against stepping off from the trap's centre (≈ 110–130 px at 228 px/s, ≈ 0.6 s with the ramp) is tight but holds. Not broken.
- **Level 7** ropes and skiffs: `Level07ContentTests` 21/21 — the channel (2000 px) still beats the best double jump × 1.25 (Cleopatra 794.6 × 1.25 = 993); every rope hop (worst 97.8 px) is inside 0.75 × the weakest single jump (201.4 × 0.75 = 151, was 189); the skiff crossings (430 / 410 / 380 px) are still longer than the weakest single jump. The rope release is absolute (≤ 900 px/s), so rope hops shrink by only a few percent.
- **Levels 12 and 13** (low gravity): reachability closure unchanged; content suites pass.
- **Level 0** mobility gates: the double-jump gate's gated step (x 1810–1990, top 532) begins 10 px past its first step's end (x 1600–1800, top 772), so the 240 px climb needs well under 50 px of horizontal travel against roughly 250 px that a double jump's airtime carries at 228 px/s. The first step is a 128 px hop against Lincoln's 132.6 px single rise (unchanged); by estimate he spends about 13 ticks above 128 px, about 50 px of travel at the new speed (62 before), and a double jump makes it trivial. The roll gate only checks that the hero is inside the zone while `Rolling`. `TutorialCalibrationContentTests` 4/4.
- **Level 9** fleet lanterns: a cone is at most 240 px wide; crossing it with the 40 px body at 228 px/s takes 1.23 s against the 1.5 s exposure grace (0.98 s before).
- **Level 6 lava / Level 15 firestorm**: scaled with the heroes (above).
- **Level 14 grids**: `ReferenceWalkSpeed` 228; beam contact 160 / 228 = 0.70 s against dark windows of at least 2.1 s. `Level14ContentTests` 26/26.
- **Level 5 flood** (water × 0.55 stacks: Lincoln 125 px/s submerged; drowning 5 HP/s after 3.5 s): non-lethal pressure, exits are climbed; not changed. GU-L4 re-times the flood steps on lock clears in Phase B.
- **Florence's crumbling scaffolding** (0.8 s shake; a 200 px plank takes 0.88 s at 228): optional boss-arena platforms that burn in Phase 2 anyway. Not changed.

### 6.4 Other consequences (not changed here)

- More mobs now outrun the slowest heroes (enemy speeds are absolute). Faster than Lincoln at 3.8 u/s: hologram_drone, laser_rifle_infantry, plasma_spear_ward, tech_enforcer (4.0); chrono_guard_elite, neural_linked_knight, unbound_eraser, voltaic_shock_drone (4.2); plasma_sabre_captain (4.4); cyber_guard, holo_page (4.5); laser_pistol_deckhand (4.6); chrono_slasher (5.0); chrono_rioter, rift_phantom (5.2); cyber_cavalry_commander (5.5); chrono_chariot_raider (6.5); laser_archer and vacuum_digger tie at 3.8. Faster than Joan at 6.2: chrono_chariot_raider only. (Recon numbers; the plan puts enemy re-tuning out of scope.)
- Traversal takes 25% longer, so Timeline Integrity drains further per level (GU-E's `TimelineIntegrityRules.ProvisionalParScale` covers it until `VERIFY-PAR-SECONDS` is measured).
- Bot batches from before and after this change are not comparable (the planner's `RunSpeed` follows the data).
- The run animation's playback rate is not speed-scaled, so feet slide a little more (presentation; nobody has looked).

## 7. Not delivered / open

- Nobody has played or looked at the slower movement in either mode.
- No par re-measurement (GU-E's provisional scale and the Phase C bot batch).
- The longer trailing slide after a Story roll and after a roll get-up (§3 item 2) is accepted, not fixed.
- Level 5's flood and Florence's scaffolding were judged, not changed; the Level 10 trapdoor step-off margin is tight for Lincoln (about 0.6 s against 0.75 s) and unverified by play.
- Enemy speeds against the slower heroes were not re-tuned (out of scope).

## 8. For the closeout

**Ledger rows.** `USER-MOVEMENT-SPEED-CUT` (quote request 4 and the both-modes ruling; the nine values; distance −20%, height and airtime unchanged, measured). `PROVISIONAL-*` rows for: exact × 0.8 (not 0.25-rounded); the roll keeping its distance (`RollSpeedMultiplier` 1.875, both modes, the get-up included), **with the corrected rationale of §4** and the trailing-slide side effect; `FighterLoadout.Default` staying 7; escape fronts × 0.8 (224 / 216); `Level14Controller.ReferenceWalkSpeed` 228; the Level 11 perch widening (220 → 340). Amend `USER-MOVEMENT-FEEL` (the 6 / 4-frame ramps are unchanged and still land on their frame at the new speeds; its measured run-speed-derived figures are pre-cut) and `VERIFY-PAR-SECONDS` (traversal × 1.25).

**AGENTS.md.** The universal-movement bullet: the roll's travel speed is 1.875 × run speed (both modes) since 2026-10-12; the "Movement also includes …" bullet: run speeds were cut −15% on 2026-08-10 and again ×0.8 on 2026-10-12 (list the nine values; the `.tres` remain law); the Fighter flow / `FighterLoadout.Default` note if any; add `StoryMovementSpeedCutTests` / `FighterMovementSpeedCutTests` to the validation arithmetic (+5). **CLAUDE.md:** the bot section's comparability note (batches before and after 2026-10-12 differ in run speed as well).

**Design-master lines owed** (`design-godot.md` mirror line numbers; the master is `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`): **:828** the roll — "12 travel frames at `1.5x` run speed" → 1.875× (so the roll keeps its pre-cut distance); **:1576** the stat-table preamble — "run speeds are the −15% retune" → add the 2026-10-12 × 0.8 cut; **:1580–:1588** the `maxMoveSpeed` column (6.75 → 5.4, 7.75 → 6.2, 6.5 → 5.2, 4.75 → 3.8, 7.25 → 5.8, 6.0 → 4.8, 6.5 → 5.2, 6.75 → 5.4, 6.75 → 5.4). `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md` §2.1 (the × 0.85 map) and §9 B2 (`ReferenceWalkSpeed` 330 → 285) want supersession notes.
