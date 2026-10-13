# GU-BOT handoff: lock-aware Story playtest bots and the lock / crowd telemetry (gameplay update 2026-10-12, Phase B)

Workstream GU-BOT of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` (§3.2, §5 "GU-BOT"). Branch `worktree-wf_6f019357-a19-5`, reset to `7aaae7d` (main with Phase A and its review fixes merged; suite 3125) before any edit. Diagnostics only: nothing here is gameplay state, no framework file was edited, nothing under `scripts/FighterSim/` was touched, no `en.csv` key, save field, component or protocol changed.

**Nobody has played anything.** The evidence is the GdUnit suites below and headless bot runs of Level 2 (the only level with locks on this base).

## 1. Shipped

### The bots (`scripts/Diagnostics/PlaytestBots.cs`, `StoryBot`)

Decision order is now `WorkSealAnchor → EvadeHazards → OutrunEscape → CollectHealing → FightNearby → HuntLockMembers → WorkTasks → WorkRouteHints → Travel`, with `WatchEngagedLock` run first every decided frame.

- **Boss-like targeting for the engaged lock's members** (`SelectTarget`). A woken member of the lock the hero is Engaged in (`PlaytestWorld.IsActiveMemberOf`) has no engage-radius cap (`EngageRadius` 620 px does not apply), no line-of-sight filter, and is never put on the 1500-frame ignore list. A lock member the bot has not hurt for `TargetGiveUpFrames` (720) is **deprioritized** for 720 frames (`LockTargetDeprioritizedPenalty` = +2000 on its score) instead of given up, so reachable members are fought first and the last one is never dropped. The "more than 330 px overhead with no surface under it" filter still applies to everybody.
- **Dormant members are never targets**, nor escape blockers (`PlaytestWorld.IsDormantMember`): an Armed lock's members refuse every hit (GU-E's ward), and the old radius rule would have struck them through the open entry and then given up on them.
- **`HuntLockMembers`** (action `lock_hunt`): engaged with living members none of the fight rules picked (all far overhead, say), the bot goes to the nearest one — to the surface under it when there is one.
- **Travel** (`Travel`):
  - **engaged, nothing to fight or work** (a wave still to come, a breakable the tasks cannot reach): `HoldInEngagedLock` (action `lock_wait`) holds in the middle 40 % of the area rather than pushing at the closed exit;
  - **an Armed lock on the way** (`PlaytestWorld.ArmedLockOnTheWay(fromX, goalX)`: the nearest Armed lock whose area lies between the hero and the travel goal, or holds the hero): action `lock_enter`. The goal becomes `PlaytestWorld.EntryPointX` (the entry edge + hero half-width 20 + `EngageInsetPixels` 24 + `LockEntryMarginPixels` **96**), and the nav goal is any non-rope surface inside the area past the engage line (`Agent.Drive`'s `isGoal`), so an area up a climb or down a drop is still entered.
- **Checkpoints a lock holds are skipped** (FIX-E's `CheckpointTrigger.IsHeldByEncounterLock`, read through `PlaytestWorld.IsHeldByLock`): never picked as a task, and a held current task counts as done until the lock clears.
- **`OutrunEscape` yields to an engaged lock** (its barriers cannot be outrun; plan §5 has escapes start on the last clear anyway).
- **The stuck-lock watchdog** (`WatchEngagedLock`, `StoryBot.LockStallFrames` = **3600**, 60 s of the bot's decided frames; frames the bot did not decide — the hero dead, the tree paused — do not count). Progress is any of: living members' HP falls, the living or remaining count changes, a required breakable loses HP, or the bot's current task advances (`TaskProgress`). With no progress:
  - for 60 s while **no living member is reachable** (`MemberReachable`: its feet within 120 px of a reachable surface, or a reachable surface under it within the hero's jumping-strike reach), or
  - for **120 s** otherwise (a member reachable but never hurt, or no living member left: a wave, breakable or puzzle the bot cannot finish),
  
  the bot sets `StuckLockID` and adds a nav note (`lock <id>: no progress for N s, … no member reachable: <enemy>@x,y …`). At the 60 s mark with a reachable member it adds a one-off note instead. The runner ends the run **`stuck_lock:<lockID>`**.
- **`bypass` is redefined**: it fights only what the run requires — the members of the lock it stands in, bosses, and (as before) a blocker in an escape. It still never defends and still breaks only what blocks it. With locks it is a lock-regression probe: it must clear every lock to finish.

### The world's seams (`scripts/Diagnostics/PlaytestWorld.cs`)

A new region, "Encounter locks and the engagement director", is the only place under `scripts/Diagnostics/` that names the GU-E framework or the GU-F per-mob state. Every read is a plain getter; the director itself is never asked (asking prunes its registry), so the bots stay pure observers.

| Member | What |
|---|---|
| `EncounterLocks()` | every lock in group `story_encounter_lock` (base levels and standalone Level 0 / Florence locks), cached per physics frame |
| `EngagedLock()` | the Engaged lock, or null |
| `ArmedLockOnTheWay(fromX, goalX)`, `EntryPointX(lock, fromX)`, `LockEntryMarginPixels` (96) | the next lock to walk into |
| `IsDormantMember`, `IsActiveMemberOf(body, lock)`, `LockIdOf` | membership |
| `IsHeldByLock(checkpoint)` | FIX-E's checkpoint hold |
| `MemberRelocations(enemy)` | `EncounterRelocations` / `EncounterFallRecoveries` |
| `EngagementRoleOf`, `EngagesHero`, `IsRingingWaiter` (and not crossing), `RingSideOf`, `EngagementGapOf`, `InFlightClassOf`, `SpawnGenerationOf` | GU-F's per-mob state |

### The nav graph (`scripts/Diagnostics/PlaytestNav.cs`)

Confirmed: a lock barrier is a pre-built `StaticBody2D` whose one shape is disabled while open, so a closing barrier was already noticed by the 15-frame `_disabledWatch` validation and an opening one by `StillValid`. Both could lag up to a quarter of a second, long enough to plan a jump through a closing curtain. Added: every `EncounterLockBarrier` seen at build goes into `_barrierWatch` with its shape state, and `Update` checks the list **every frame**; a toggle rebuilds on the frame it lands (a deferred `SetShapeDisabledSafe` included). New read surface `BarrierRebuilds` (reported as `nav_barrier_rebuilds`) and `ClosedBarriersAtBuild`.

### The telemetry

New Godot-free trackers in **`scripts/Diagnostics/PlaytestEncounterStats.cs`**:

- `EngagementFrameSample`
- `MobEngagementTracker`: `NearPixels` 250, `GapBucketPixels` 8, `FarWaiterGapPixels` 600, `FarWaiterLogLimit` 60, `FarWaiterSampleFrames` 60
- `LockObservation`, `LockRecord`, `LockTracker`

`PlaytestTelemetry` feeds them.

**`mob_engagement` block.** Counts are over living `EnemyController` mobs; bosses never hold a slot. An *engaged frame* is one with at least one mob in the hero's rotation (`EngagementRole` ≠ None on the hero) or attacking near it, and means are over engaged frames.

| Key | What |
|---|---|
| `slot_total`, `ranged_permits` | the difficulty's E1/N2/H3 slots and permits |
| `engaged_frames` | |
| `attacking_near_max` / `_mean` | mobs in `EnemyState.Attacking` within 250 px (pivot to pivot) of the hero |
| `melee_attacking_near_max` / `_mean` | of those, the melee-slot class in flight (`EngagementInFlightClass`) |
| `frames_melee_over_slots` | engaged frames with more melee attackers near the hero than the slot total. Retaliation is never gated, so this is not always a fault |
| `slot_holders_max` / `_mean`, `frames_holders_over_slots` | the director's own invariant; should stay 0 |
| `slot_grants`, `slot_handoffs` | a slot taken from any other role; a Slot → Waiting change while still on the hero (rotation or stale give-up). A new pool generation is a new body |
| `waiter_gap_px_median`, `front_waiter_gap_px_median` | every ringing waiter's gap to its aim, and the nearest waiter per side |
| `waiter_gap_samples`, `waiter_gap_histogram`, `front_waiter_gap_histogram` | the histograms are 8 px buckets, so batches pool exactly |
| `far_waiter_samples`, `far_waiters` | ringing waiters more than 600 px off the hero, with up to 60 logged positions; see §6 |
| `ranged_in_flight_max` / `_mean`, `frames_ranged_over_permits` | ranged-permit attacks in flight |
| `attacking_near_histogram` | |
| `sums` | raw sums, so `compare_runs.py` pools exactly |

**`locks` block.**
- Totals: `count`, `engaged`, `cleared`, `restored_cleared`, `force_cleared`, `reverts`, `left_with_members_alive`, `members_planned`, `members_killed`, `stall_relocations`, `member_relocations`, `fall_recoveries`, `frames_to_clear_median`.
- A `log` row per lock: `id`, `state`, `engage_frame` (first), `clear_frame`, `frames_to_clear`, `engaged_seconds`, `planned`, `registered`, `killed`, `living`, `max_living`, `engages`, `reverts`, `left_with_members_alive`, `left_frames`, `stall_relocations`, `member_relocations`, `fall_recoveries`, `recycled`, `force_cleared`, `restored_cleared`.
- "Killed" means the member's enemy record reached 0 HP; a pool park with HP left is not a kill.
- **Left with members alive**: the living hero went from inside an uncleared lock's area to outside it while it held living members. A dead hero is not judged, so a death-rewind landing outside counts as one leave.

**`enemy_summary`.**
- `passed_while_alive` / `passed_then_hit_player` **keep their old meaning** (comparability).
- New: `left_lock_with_members_alive` (the lock meaning), `passed_alive_lock_members` / `passed_alive_outside_locks` (the old passes split), `lock_members_seen`, `lock_members_killed`.
- Each `enemies[]` row gains `lock` (the owning lock ID, or "").

**Swings.**
- `air_attack` (GU-A's single aerial strike) is recognised: `swings.by_hit` keys it `air_attack` (no `_air` suffix, since it is airborne by definition; `PlaytestCombatMath.SwingHitKey`).
- New `swings.by_variant` counts string / `air_attack` / `up_attack` / `down_air` (`PlaytestCombatMath.HitVariant`, reading the `BasicComboRules` hitbox ID constants).

**Trace and events.**
- Trace samples gain `enc` (the engaged lock's ID tail and its enemies left, e.g. `siege_line:3`), `atk` (mobs attacking within 250 px) and `slots` (slot holders), the last two from the previous frame's sample.
- Events gain `lock` (every state edge: `<id>:<state> living=N remaining=N`, with `(restored)` or `(forced)` where it applies) and `lock_left_alive`.

**The runner** (`PlaytestRunner.cs`).
- New end reason **`stuck_lock:<lockID>`**.
- The auto run length is now `PlaytestOptions.AutoMaxSeconds(par, difficulty)` = ceil(max(par × `TimelineIntegrityRules.ProvisionalParScale`, `AutoMaxSecondsParFloor` 300) × `TimelineIntegrityRules.DifficultyTimeMultiplier`) + 60. Level 2 on Normal: 360 s par → 870 s, was 600 s. The difficulty multiplier now comes from the rulebook instead of an inline copy.
- `PLAYTEST_RESULT` gains `encounters`: `locks`, `locks_cleared`, `lock_reverts`, `left_lock_alive`, `lock_relocations`, `locks_forced`, `atk_max`, `atk_mean`, `melee_max`, `melee_over_slots_f`, `handoffs`, `waiter_gap_med`, `ranged_max`, `slot_total`.
- The report gains `nav_barrier_rebuilds`.

### Tools

- **`tools/playtest/run_playtests.ps1`**
  - Help text: `bypass` redefined, `stuck_lock`, the par-scaled `-MaxSeconds 0` formula, the new CSV columns.
  - The CSV gains `locks`, `locks_cleared`, `lock_reverts`, `left_lock_alive`, `lock_relocations`, `atk_max`, `atk_mean`, `melee_max`, `melee_over_slots_f`, `slot_total`, `handoffs`, `waiter_gap_med`, `ranged_max`.
  - The table width is now 400.
- **`tools/playtest/compare_runs.py`**
  - The main table gains `left_lock_alive` and `passed_out`.
  - New `locks` table: locks/run, cleared %, stuck_lock runs, reverts, left_alive, planned, killed, relocations, stall relocations, forced, and the median frames to clear, excluding locks restored Cleared.
  - New `crowd` table: slots, engaged seconds, atk max/mean, melee max/mean, over_slots %, holders max and over, handoffs/run, grants/run, pooled waiter and front-waiter medians from the histograms, far_waiter %, ranged max/mean/over. All are raw sums pooled, never medians of medians.
  - `--swings` keys the air strike `air_attack`.
- **`tools/playtest/trace.py`**
  - The header prints `mob_engagement`, up to 8 far waiters, the `locks` totals and one line per lock.
  - A `stuck_lock` run prints the bot's lock nav notes.
  - Trace lines show `enc=` / `atk=` / `slots=` through the generic extras.

## 2. Files

- **New:**
  - `scripts/Diagnostics/PlaytestEncounterStats.cs` (+ `.uid`)
  - `tests/Unit/PlaytestEncounterTelemetryTests.cs` (+ `.uid`)
  - this handoff
- **Changed:**
  - `scripts/Diagnostics/PlaytestBots.cs`, `PlaytestCombatStats.cs`, `PlaytestNav.cs`, `PlaytestRunner.cs`, `PlaytestTelemetry.cs`, `PlaytestWorld.cs`
  - `tools/playtest/run_playtests.ps1`, `compare_runs.py`, `trace.py`
- **Outside my ownership:** none. No framework file, shared test file, scene, resource or `en.csv` was touched.

## 3. Decisions (Provisional; for a `PROVISIONAL-BOT-ENCOUNTER-RULES` ledger row, diagnostics only)

1. **Stall bounds 60 s / 120 s** (`LockStallFrames` 3600). 60 s with no progress and no reachable member is the plan's example. The 120 s bound (a reachable member never hurt, or no living member left) stops a lock waiting on a puzzle or a breakable from being called stuck at 60 s, and still ends a truly hopeless one before the run's timeout.
2. **Lock members are deprioritized, never given up on** (720 frames, +2000 score). The plan says "no 720-frame give-up for members". A plain "never" would pin the bot to an unreachable nearest member while a reachable one waits.
3. **Dormant members are not targets at all.** The lock ward refuses their hits, so the old radius rule would have struck them through an Armed lock's open entry until the give-up ignored them, and they would then be ignored after the engage. The bot walks in instead (`lock_enter`).
4. **The overhead filter (330 px, no surface within 360 px) still applies to lock members**, with `HuntLockMembers` as the fallback. Fighting a flyer from below is the existing `AirStrike`'s job, and it only engages within 330 px.
5. **Entry margin 96 px** past the engage line, and an `isGoal` of any surface in the area past the line.
6. **Hold in the middle 40 % of an engaged area** when there is nothing to fight or work.
7. **An engaged lock outranks an escape front** (`OutrunEscape` returns false).
8. **`passed_while_alive` keeps its old meaning; the lock meaning is a new key** (`left_lock_with_members_alive`). The plan allowed "or report both". Batches stay comparable, and on a fully locked level the old key now flags a mob no lock owns (`passed_alive_outside_locks`).
9. **Crowd definitions:**
   - near = within 250 px pivot to pivot (Euclidean);
   - engaged frame = a mob in the hero's rotation, or attacking near it;
   - hand-off = Slot → Waiting on the same body and target;
   - front waiter = the nearest ringing waiter per side.
10. **Far waiter = a ringing waiter more than 600 px from its aim**, logged once per mob per 60 frames, at most 60 rows.
11. **The auto run length applies the par scale before the 300 s floor** (`max(par × 1.5, 300)`), i.e. the floor applies to the clock's real par.
12. **The nav graph rebuilds on the frame a barrier toggles** (an every-frame check of a handful of barriers), not on the lock's event: the shape state is the ground truth, and a deferred toggle lands a frame after the event.

## 4. Deviations from the plan

- `passed_while_alive` was reported both ways (Decision 8) rather than redefined in place.
- The watchdog adds the 120 s bound (Decision 1) to the plan's "60 s with no member HP progress and no reachable member".
- `AutoMaxSeconds` lives on `PlaytestOptions` (a plain class, testable) rather than inline in `BeginRun`.
- Added beyond the brief:
  - the `far_waiters` log;
  - `swings.by_variant`;
  - the `nav_barrier_rebuilds` report key;
  - the raw `sums` and 8 px histograms, so batches pool exactly;
  - the escape-front guard.

## 5. Tests

**New suite `PlaytestEncounterTelemetryTests`** (`tests/Unit/`, `[RequireGodotRuntime]`): **+6 cases.**

- `TheCrowdTrackerCountsConcurrentAttackersHandOffsAndWaiterGaps` (pure): quiet frames uncounted; max, mean, over-slot, over-permit and holder counts; the histogram; grants vs hand-offs (a death is neither; a new generation is a new body); waiter and front-waiter medians and buckets; the far-waiter count and log window.
- `TheLockTrackerBooksTheEngageTheClearAndEveryLeaveWithMembersAlive` (pure): the first engage and clear frames, frames to clear, `max_living`. Leaves:
  - an Echo-Step-style leave counts once;
  - a dead hero is not judged, and its rewind landing outside counts;
  - leaving a cleared area is no leave.

  Members: killed only at 0 HP (a pool park is not a kill), and relocation high-water marks.
- `TheAirStrikeIsItsOwnSwingFamilyAndTheAutoRunLengthCarriesTheParScale` (pure): `HitVariant` and `SwingHitKey` for every basic hit ID; `AutoMaxSeconds` on all three difficulties, including the 300 s floor and the scale.
- `TheBotWalksIntoALockFightsItsMembersAtAnyDistanceAndReportsAStuckOne` (real frames). A Joan `play` bot on a flat floor beside a 4000–7000 lock with two slashers, and a Middle checkpoint behind the hero whose baseline lists the lock:
  - Armed: the dormant member 550 px away is not the target, the held checkpoint is not a task, and the action is `lock_enter`. The nav graph saw one closed barrier at build.
  - The bot walks in and engages the lock; the entry's closing causes a barrier rebuild, and the graph then holds 2 closed barriers.
  - With the near member dead, the far member (2400+ px, beyond 620) is the target.
  - A non-member slasher 150 px away is `play`'s target, but `bypass` still targets the far member.
  - The member frozen 650 px above the floor: the action is `lock_hunt`, and the watchdog sets `StuckLockID` within the single 60 s bound, with a "no member reachable" note.
  - After the clear the checkpoint is free, and the bot goes to strike it.
- `TheTelemetryReportsTheLockAndTheCrowdTheDirectorShapes` (real frames): a two-slasher lock engaged, reverted by a move outside (`left_with_members_alive` 1), re-engaged and cleared.
  - `locks`: count 1, cleared 1, reverts 1, planned 2, killed 2, engages 2.
  - `enemy_summary`: `lock_members_seen` 2 and killed 2.
  - Crowd: Normal's slot total; at least 1 grant and holder; no holder over the slot total; the second slasher sampled as a waiter.
  - `EncounterSummary()`.
- `AnAirStrikeIsRecordedAsItsOwnSwing` (real frames): a Joan jump-and-press records an `air_attack` swing with `Air` true.

**Rewritten:** none. **Mutation note:** none run. The bot case asserts each behaviour against its own control (the checkpoint is held, then free; `play` and `bypass` are compared on the same frame).

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`, `GODOT_BIN` set, after a warm-cache `--import`):

| Run | Result |
|---|---|
| Diagnostics: `PlaytestEncounterTelemetryTests` 6, `PlaytestCombatStatsTests` 6, `PlaytestBotStrikeRangeTests` 1, `InfluenceMeterSourceScanTests` 3 | 16/16 (several times) |
| Targeted, 20 suites: the four above plus `ResonanceReaderCoverageTests`, `ScriptTranslationKeyTests`, `UnusedTranslationKeyTests`, `EncounterLockContentTests`, `EncounterBaselineTests`, `EncounterLockTests`, `EncounterLockRulesTests`, `StoryEngagementDirectorTests`, `Level02ContentTests`, `EncounterCompositionTests`, `RewardManifestTests`, `DustEconomyTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `ArenaGeometryContentTests`, `PuzzleResetPlacementTests` | **165/165**, equal to their `[TestCase]` count |
| **Full suite** | **`Passed! - Failed: 0, Passed: 3131, Total: 3131`** = 3125 + 6. The `[TestCase]` attribute count under `tests/` is 3131. The final run after the last edit is in the structured result |

`dotnet build`: 0 errors, only the three pre-existing warnings (CS8632, CS0649, KLSG_ECS004); no `KLOTHO_DET002`.

## 6. Measurements

**Smoke.** `Level_02_Orleans.tscn --quit-after 300` exits 0 with no script error. It printed 6 leaked ObjectDB instances and 3 resources in use at exit; GU-E's run printed 2 / 1. CLAUDE.md records this as nondeterministic at-exit noise, and nothing under `scripts/Diagnostics/` is loaded by a level scene, so it is not this change.

**Bots on Level 2** (Joan, Normal, seed 0, `--max-seconds=420`, tag `gubot3`, final build; an earlier batch `gubot1` and a lone `play` run `gubot2` agree):

| Bot | End | s | Locks cleared | Members killed | Reverts / left alive | Deaths (boss) | atk max / mean | melee max | over-slot frames | holders max | hand-offs | waiter gap med / front | ranged max | nav barrier rebuilds |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| play | `left_level:collapse(Death)` | 180.5 | 3/3 | 8/8 | 0 / 0 | 4 (Duke 4) | 1 / 0.28 | 1 | 0 | 1 | 3 | 175 / 175 | 1 | 6 |
| spam | `left_level:collapse(Death)` | 151.0 | 3/3 | 8/8 | 0 / 0 | 4 (Duke) | 1 / 0.14 | 1 | 0 | 1 | 2 | 203 / 198 | 1 | 6 |
| basics | `left_level:collapse(Death)` | 174.7 | 3/3 | 8/8 | 0 / 0 | 4 (Duke) | 2 / 0.31 | 1 | 0 | 1 | 1 | 1230 / 1230 | 1 | 6 |
| bypass | `left_level:collapse(Death)` | 136.4 | 3/3 | 8/8 | 0 / 0 | 4 (Duke) | 1 / 0.13 | 1 | 0 | 1 | 2 | 203 / 198 | 1 | 6 |

- **Locks.** All four bots cleared all three locks, killing 8 of 8 members, with no stall relocation, fall-net catch, force-clear, revert or leave. No bot gave up on a mob, and no mob was passed alive.
  - The vanguard was engaged at frame 30 and cleared by about frame 590.
  - The siege line took about 33–35 s, most of it striking generator A after its soldiers were dead.
  - The battlement took 5–16 s.
- **The boss.** Every run then died four times to the Siegemaster Duke and collapsed. That is the same end GU-E (`gue_e1`) and GU-F (`guf`) recorded on this base before the bots knew about locks, and it is outside this workstream.
- **`bypass` now finishes the locks.** Before this change it skipped every non-boss mob (`bots-docs-design.md` (a)), so it could not have cleared one; this was not run to confirm it.
- **Turn-taking on Level 2 (Normal).**
  - At most 2 mobs attacked within 250 px, and at most 1 melee attacker, against 2 slots. No holder ever exceeded the slot total.
  - Ranged attacks in flight peaked at 1 against 2 permits.
  - The front-waiter median is 175–200 px, which is the 170 px Normal ring plus approach.
- **Far waiters.** The 1230 px waiter median in `basics` (and in the first `play` run) is the siege line's `OnEngage` wave. A slasher spawns near x 5350 while the hero engages near 3300, then walks in as a waiter at the capped waiter speed: the gap fell 1986 → 1494 → 1254 px over 2 s. It is not a stuck waiter. `far_waiters` makes the difference visible.
- **Run time.** On Normal, Level 2's auto run length is now 870 s (was 600 s).
- **Comparability.** Seed-0 runs on this machine are not frame-identical across launches (CLAUDE.md), so these are single samples.

## 7. Not delivered / open

- **Only Level 2 has locks on this base.** The lock code was exercised in real levels only there. Levels 0, 1 and 3–15 get their locks from the parallel level workstreams, and the orchestrator's Phase C batch is the first real test there. These paths have run only in the unit case, never in a real level:
  - `stuck_lock`;
  - `lock_hunt`;
  - `lock_wait`;
  - the 120 s bound.
- **Mover holds.** The bots do not model GU-E's mover-hold query (`EncounterLock.IsHolding`). A moving platform that a level holds at its near stop is simply a platform at a stop to the nav graph.
- **Puzzle progress** reaches the watchdog only through the bot's own task progress (coil turns, glyph progress, breakable HP); a pressure plate shows none. A puzzle lock the bot can never solve ends `stuck_lock` at 120 s, and one that solves slowly but steadily is not cut short.
- **The bots do not "wait their turn" themselves.** They fight whichever member is nearest, waiters included: a waiter backs off at at most 0.9 × the hero's run speed, so the bot catches it. The `play` bot's crowd numbers measure the director against an aggressive player, not a patient one.
- **The `atk` / `slots` trace cells** come from the previous frame's sample.
- **Joan only, one seed, one difficulty.** The plan's Phase C batch (all levels, Easy/Normal/Hard, two heroes, seeds 0–1) is the orchestrator's.

## 8. For the closeout

**CLAUDE.md** (the bot section, "Automated Story playtests"):
- `bypass`'s new definition, and the `stuck_lock:<id>` end reason with its 60 s / 120 s bounds.
- The `lock_enter` / `lock_hunt` / `lock_wait` actions.
- The checkpoint skip.
- The `-MaxSeconds 0` formula now carries `ProvisionalParScale`.
- "Reading a report": the `locks` and `mob_engagement` blocks; `enemy_summary`'s `left_lock_with_members_alive` beside the unchanged `passed_while_alive`; `swings.by_variant`; `air_attack` keyed without `_air`; the trace's `enc=` / `atk=` / `slots=`; `nav_barrier_rebuilds`; `PLAYTEST_RESULT.encounters` and the CSV columns; `compare_runs.py`'s `locks` and `crowd` tables.
- "How the bots move": barriers are watched every frame.
- A comparability note: bot batches from before and after the 2026-10-12 package are not comparable on run time, deaths, crowd numbers, passes or finisher numbers (locks, turn-taking, the −20 % speed, static knockback, the air strike and the par-scaled budget all landed together).

**AGENTS.md:** the repository-map line for `scripts/Diagnostics/` (lock-aware bots, the lock and crowd telemetry), and the baseline (+6, `PlaytestEncounterTelemetryTests`).

**Ledger:**
- New `PROVISIONAL-BOT-ENCOUNTER-RULES` (diagnostics only; Decisions 1–12).
- Amend `USER-BOT-COMBAT-TELEMETRY`: the new blocks, and `passed_while_alive`'s unchanged meaning.
- `USER-ENEMY-TURN-TAKING`'s acceptance now has its measure: `mob_engagement.melee_attacking_near_max` ≤ `slot_total` and `frames_holders_over_slots` = 0 on every difficulty, read with `compare_runs.py`'s crowd table.
- `VERIFY-PAR-SECONDS`: the bots' auto budget now includes the ×1.5 scale.

**Design-master lines owed:** none. The bots and telemetry are diagnostics and are not in `design-godot.md`.
