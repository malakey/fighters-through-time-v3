# GU-FIX-OBJECTIVE handoff: lock-required objectives under the death rewind (Phase C fix round, 2026-10-13)

Workstream FIX-OBJECTIVE of the Phase C fix round of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md`. Review items (indexes into `D:/Projects/absolum_research/update_2026-10-12/phaseB_review_verified.json`, `confirmed`): **#0** (Florence print shop), **#1** (Chicago routing hall), **#5 / #13** (Gettysburg's Angle and array Alpha, duplicates), **#6** (Hakata Bay's Lantern Watch), **#7** (Hakata Bay's font). Branch `worktree-wf_c57a521f-e47-2`, based on `c4885a9` (main with Phase B merged).

**Nobody has played any of this.** The evidence is the suites below, a revert check, and one `play` bot run of Level 9.

## The systemic defect, once

A death rewind raises `EventBus.OnRewindTriggered`. Every `PuzzleManager`, `DamageableEnvironmentObject` and puzzle prop (`RotatingGear`, `ConductiveCoil`) defaults to `StoryRewindPolicy.RestoreCheckpointState` and restores the last checkpoint's snapshot. Phase B moved every Middle and PreBoss anchor out of the lock fights, and FIX-E holds a fracture past a lock until the lock clears. So the snapshot a rewind inside a lock restores always predates the solve.

Before Phase B nothing read a solved puzzle or a broken generator once its door had opened. Phase B's encounter locks read them live every tick (`EncounterLock.RequiredPuzzlesSolved` → `PuzzleManager.IsSolvedOrPersisted`, `RequiredBreakablesDestroyed` → `IsDestroyed`). Both the clear predicate and the hard cap need them. So a rewind could un-meet an objective the hero had met, and the barrier then held. The damage ranged from a stale objective with a hidden re-solve (Florence, Chicago) to a hard trap (Level 9's re-cut relay could never set its condition again; Level 11's Alpha came back behind the Angle's closed entry).

## 1. Shipped

### The framework rule: a required objective is one-way (`scripts/Environment/EncounterLock.cs`, conditions region)

The rule is stated once, in the region comment. It has two halves.

**The latch.**
- From its first tick until it clears, the lock reads every required puzzle and every required breakable each tick. That covers Armed or Engaged, held or not. A puzzle counts when it is solved live or persisted; a breakable counts when it is destroyed or freed.
- It latches each one met the first time it reads it met. A latched objective is never read again.
- So a death rewind, a revert to Armed or any later reset can never take back an objective the hero already met. The lock clears once its members are down. The hard cap's condition reads the same latched values.
- Latching from Armed matters. Level 11's Alpha is broken in the Wheatfield while the Angle is still Armed; the Angle latches it then, before any engage.
- New code:
  - `private readonly HashSet<string> _latchedPuzzles` and `HashSet<object> _latchedBreakables` (reference equality);
  - one line in `Tick`, `LatchRequiredObjectives();`, right after the first `PollMembers()` and before the containment check, so it runs held or not;
  - `RequiredPuzzlesSolved` / `RequiredBreakablesDestroyed` now latch;
  - `FindPuzzle(id)` caches the live manager, so an Armed lock does not walk the group every tick;
  - `IsBreakableDown`.

**The world half.**
- On the latch, the lock also switches the objective itself to `StoryRewindPolicy.PreserveCurrentState`: the puzzle's `PuzzleManager` and the breakable.
- A later rewind therefore cannot un-solve the puzzle. The save keeps its completion too: `RestoreCheckpointState` used to write `SetPuzzleCompleted(false)`.
- Nor can a later rewind rebuild the breakable. For example, Level 2's Gate A forcefield never comes back under an open, latched siege line.
- A puzzle's props are the level's to keep (below).

**Read surface** (side-effect free; the tick does the latching):
- `public bool RequiredObjectivesMet`
- `public bool IsRequiredPuzzleMet(string puzzleID)`
- `public bool IsRequiredBreakableMet(DamageableEnvironmentObject breakable)`

Each answers "latched, or met now". A Cleared lock, or an object the lock does not require, answers true. **A level derives any "solve the puzzle" objective from `IsRequiredPuzzleMet`, never from the puzzle alone**, so the objective and the barrier always agree.

**Persistence.** The latch lives on the instance and is never saved.
- Every fresh entry, Restart, Collapse resume and Anchor Snap reloads the scene (`StoryManager.LoadCurrentLevel`), so it starts empty.
- A lock rebuilt mid-attempt latches on its first tick: a solve the save carries (`IsSolvedOrPersisted`), or a breakable the resume broke (Level 11's `ForceArrayDestroyed`, Level 2's `ForceGateOpen`).
- A lock past the resume anchor is still built Cleared from the F10 baseline, as before.

`EncounterLockSpec.RequiredPuzzleIDs` / `RequiredBreakables` documents the rule.

### Level 1, Florence (#0): `Level01Controller`

- The two gears are now fields (`_gearA`, `_gearB`).
- `PuzzleCompleted` now calls `OnGearPuzzleSolved()`. It sets the puzzle and both gears to `PreserveCurrentState`, then calls `OpenWorkshopDoor()`. The solve is one-way, like the door it frees; this is the burned scaffolding's precedent.
- It also runs for a completion the save restores, through `PuzzleManager.AnnounceRestoredCompletion`.
- `RefreshObjective` now picks `florence_objective_gears` from `!_printShopLock.IsRequiredPuzzleMet(GearPuzzleID)`.

### Level 3, Chicago (#1): `Level03Controller`

- `OnBeamPuzzleCompleted` now calls `KeepBeamRoutedThroughRewinds()`, then `OpenCourtDoor(announce: true)`.
- `KeepBeamRoutedThroughRewinds()` sets the puzzle and every `ConductiveCoil` child of `BeamRoutingPuzzle` to `PreserveCurrentState`. So a rewind no longer re-aims the coils or re-arms crowd tap A.
- `RefreshApproachObjective`: while the door is open and the routing lock holds, it posts `chicago_objective_reroute` if the lock still needs the beam (`IsRequiredPuzzleMet`), else `encounter_lock_objective_clear`.

### Level 9, Hakata Bay (#6, #7): `Level09Controller`

- `CollectAuthoredNodes` sets `FeedPuzzle.RewindPolicy = PreserveCurrentState` at build. Its conditions mirror the lanterns, which never relight, so a cut is one-way from the moment it happens. That includes a **partial** cut, which the latch alone could not cover: the puzzle is not solved yet, so nothing is latched.
- `OnFeedRelayCut(relay)`:
  - sets the relay to `PreserveCurrentState`;
  - darkens the lantern without gating on the result (`DarkenSearchlight` returns false for an already-dark lantern, which used to skip `SetCondition` for good);
  - always sets the line's condition;
  - counts `FeedsCut` only when `SetCondition` reports a change ("lines, not cuts").
- `RestorationFontPosition` moved **8610 → 8640** (#7):
  - At 8610, a hero pressed against the storm-anchor ship's closed exit barrier (pivot 8540) stood 70 px from the font, inside its 90 px channel circle.
  - At 8640 that pivot is 100 px away.
  - The font is 112 px from the PreBoss anchor (8740, 1150); the rule needs ≥ 110.
  - Its channel circle ends at 8730, still more than 30 px short of the arena (8800); the placement rule needs x ≤ 8680.
  - It stands on room 3's floor end, room 4's start.

### Level 11, Gettysburg (#5, #13): no level change

The framework covers it.
- Both the Wheatfield and the Angle are built Armed at level build. Both latch Alpha the tick after it falls, and the first latch switches Alpha to Preserve.
- So no rewind stands Alpha back up behind the Angle's closed entry, on Hard (the Middle is inert), on Normal with the Middle never struck, or after a Middle resume.
- The Angle keeps both arrays as requirements: its clear means "the breastwork is open", which needs `_arraysDown == 2`.
- With Preserve, `_arraysDown` can no longer double-count a re-broken array.

### #7's rule, for every level: `FontHazardClearance.CheckLockBarriers`

**A font's channel circle never straddles a lock barrier.**
- No feet position on a barrier's far side may lie inside the font's `ChannelRangePixels` circle.
- A hero pressed against the far face stands `HeroHalfWidth` (20) past it.
- A font inside a barrier's x-span straddles it by definition.
- A barrier whose height misses the circle's is ignored.

`EncounterLockContentTests.NoRestorationFontsChannelCircleStraddlesALockBarrier` sweeps every campaign scene as built, plus Level 0 with its Combat Trial begun (the Tutorial's font and trial lock exist only then).

The boundary is strict (`reach < range`). Florence's font (10530, 10460–10480 barrier) and Level 13's (6970, 6900–6920) sit exactly one range from a pressed pivot, and they pass, for three reasons:
- the body's collision margin keeps the pivot a fraction past the face;
- the channel interrupts only beyond the range;
- the interaction box that starts a channel reaches only 75 px (55 + 20).

## 2. Files changed

Production:
- `scripts/Environment/EncounterLock.cs`:
  - class summary, 2 lines;
  - three fields after `_members`;
  - one line in `Tick`;
  - the Conditions region (`RequiredObjectivesMet`, `IsRequiredPuzzleMet`, `IsRequiredBreakableMet`, `LatchRequiredObjectives`, `RequiredPuzzlesSolved`, `IsPuzzleSolved`, `FindPuzzle`, `RequiredBreakablesDestroyed`, `IsBreakableDown`).
  - **`TickWatchdog` is untouched.**
- `scripts/Environment/EncounterLockSpec.cs`: docs only.
- `scripts/Environment/Level01Controller.cs`
- `scripts/Environment/Level03Controller.cs`
- `scripts/Environment/Level09Controller.cs`

Tests:
- new `tests/Unit/EncounterLockObjectiveLatchTests.cs` (+ `.uid`)
- new `tests/ContentValidation/EncounterLockObjectiveContentTests.cs` (+ `.uid`)
- `tests/ContentValidation/EncounterLockContentTests.cs`: one case, `LevelSnapshot.Fonts`, one `Collect` line, one doc bullet.
- `tests/ContentValidation/FontHazardClearance.cs`: `CheckLockBarriers` and a doc paragraph.

This handoff. No scene, `.tres`, `en.csv` key, save field, schema or Klotho change. Nothing under `scripts/FighterSim/`.

## 3. Decisions (candidate ledger rows)

**`PROVISIONAL-LOCK-OBJECTIVE-LATCH`** (amends `PROVISIONAL-ENCOUNTER-LOCK-RULES` decision 8 and the clear rule):
1. A required puzzle or breakable latches the first time its lock reads it met: from the lock's first tick to its clear, Armed or Engaged, held or not. A latched objective is never read again, and the hard cap reads the latch too.
2. The latch is per instance and never saved (every reload rebuilds the lock). A lock rebuilt mid-attempt latches a persisted solve or a resume-broken breakable on its first tick.
3. **The world half:** on the latch the lock switches the objective's `PuzzleManager` / `DamageableEnvironmentObject` to `PreserveCurrentState`. A lock-required objective is therefore one-way for the rest of the scene. Do not make a lock require a puzzle that must legitimately reset on a rewind; none does today.
4. Props stay the level's:
   - Florence keeps the gear puzzle and both gears from the solve.
   - Chicago keeps the beam puzzle and its three coils from the solve.
   - Hakata Bay keeps the feed puzzle from build and each relay from its cut (the lanterns never relight).
5. Level 9's `FeedsCut` counts lines whose condition newly flipped. It never counts a repeated cut.
6. A level's puzzle objective reads `EncounterLock.IsRequiredPuzzleMet`, so it agrees with the barrier. Florence's gears objective and Chicago's reroute objective do now.

**`PROVISIONAL-FONT-LOCK-BARRIER`** (amends `PROVISIONAL-FONT-PLACEMENT`):
7. No font's channel circle contains a feet position on any lock barrier's far side (a pressed hero stands 20 px past the far face), strictly: exactly one range away does not count.
8. Level 9's font moved 8610 → 8640.

## 4. Deviations from the brief

- **The framework does more than latch.** The brief asked for a latch plus per-level policy switches. I put the policy switch for the *objective itself* (manager, breakable) in the framework. That fixes Level 2's tower A and Level 11's arrays with no level edit, and no future lock can resurrect its own objective. The per-level edits cover only props (gears, coils, relays), which the framework cannot know.
- **Level 11 needed no level change.** The verifiers proposed authoring Preserve on both arrays, or dropping Alpha from the Angle. The framework does the former once either lock has seen the array broken. The Angle's two-array requirement and its content pin (`Level11ContentTests` asserts 2) are unchanged.
- **No objective re-post on `PuzzleReset`.** After these changes no reset can happen once a required puzzle is solved:
  - a rewind is skipped by Preserve, and `ResetPuzzle` has no production caller other than the rewind;
  - a reset before the solve leaves the lock's puzzle objective correct, because it reads the latch.

  So the defensive `PuzzleReset += RefreshObjective` subscriptions were not added.
- **#5's case (a), a Middle resume, did not reproduce in a scratch-save harness.** See §6. The real-frames Level 11 case uses the Hard / Middle-not-struck path (cases b and c), which does reproduce.

## 5. Tests

New (+10):
- `EncounterLockObjectiveLatchTests` (real frames, a stand-in puzzle and breakable far from the origin): **4**.
  - `ASolvedRequiredPuzzleSurvivesADeathRewindAndAResetCannotHoldTheLock`
  - `ADestroyedRequiredBreakableSurvivesADeathRewindAndARestoreCannotHoldTheLock`
  - `AnObjectiveMetWhileArmedOrBeforeARevertStaysMet`: the latch from Armed, and across a revert to Armed.
  - `ALockRebuiltMidAttemptLatchesASolveTheSaveCarries`: a scratch slot carrying the solve, and the save keeps it through a rewind.
- `EncounterLockObjectiveContentTests`: **5**.
  - `EveryLockRequiredObjectiveStaysMetOnceItsLockHasSeenItMet`: the content rule, synchronous, over all sixteen campaign scenes. For every lock that requires something:
    1. meet each objective;
    2. let the Armed lock read it once;
    3. apply each objective's own rewind response (`ApplyStoryRewind`) and assert it stays met;
    4. undo it outright (`ResetPuzzle`, the old restore policy) and assert `RequiredObjectivesMet`;
    5. put the hero inside and hand every member back to the pool until the lock clears.

    It covers 9 locks: L1 print shop, L2 siege line, L3 routing hall, L4 cells, L6 vault, L8 vault, L9 Lantern Watch, L11 Wheatfield and Angle. It asserts at least one objective exists, so it is never vacuous.
  - Four real-frames cases. Each meets the objective inside the engaged lock and holds the hero there for the whole rewind buffer (`ScriptedHPFloor = 1`), so the rewind lands inside. It then calls `KillPlayerNonHit()` and waits out the rewind and the Post-Landing Hold. It asserts the world still shows the objective met, then kills the members and asserts the lock clears and the objective is right:
    - `FlorencesPrintShopKeepsItsGearsThroughADeathRewindAndClears`: the gears stay aligned; `encounter_lock_objective_clear` while engaged, then `florence_objective_reach_boss`.
    - `ChicagosRoutingHallKeepsItsBeamThroughADeathRewindAndClears`: the coils stay aimed, crowd tap A stays dark, then `chicago_objective_reach_boss`.
    - `TheLanternWatchKeepsItsCutLinesThroughADeathRewindAndClears`: one line is cut before the death and stays cut and counted; the other two complete the puzzle.
    - `TheAngleClearsAfterADeathRewindWithAlphaStillBrokenBehindItsEntry`, on Hard: Alpha stays broken behind the closed entry.
- `EncounterLockContentTests` +1: `NoRestorationFontsChannelCircleStraddlesALockBarrier`.

Rewritten in place: none.

**Every behaviour fix was checked against a revert.** I temporarily disabled the behaviour while keeping the new API: no latch, no policy switch, Florence's and Chicago's prop switches removed, Level 9's old `OnFeedRelayCut` and font x restored. Then I rebuilt and ran the 10 new cases.
- **All 10 failed**, each for its own reason.
- The content sweep listed every one of the 9 locks with "a death rewind un-solves / rebuilds", "did not latch" and "is Engaged with its objectives met and every member gone".
- The font case named Level 9's font at 70 px.
- The first Level 11 version (a Middle resume) passed against the revert, which is how §6's harness artifact was found. It was replaced by the Hard case, which fails against the revert.

The fix was then restored from a backup.

Runs, every one holding the lock:
- the two new suites: 9/9;
- 19 suites (the five `EncounterLock*`, `Level00FlowSceneTests`, `Level01/02/03/04/06/08/09/11ContentTests`, `SecretCachePlacementTests`, `StoryHealingLoopTests`, `PuzzleEnvironmentToolkitTests`, `PuzzleResetPlacementTests`, `EncounterBaselineTests`): **234/234**;
- **full suite: `Passed! - Failed: 0, Passed: 3190, Skipped: 0, Total: 3190` (1 m 20 s) = 3180 + 10.** The `[TestCase]` count under `tests/` is 3190.

`dotnet build`: 0 errors, the 3 known warnings (CS8632, KLSG_ECS004, CS0649). No `KLOTHO_DET002`.

## 6. Investigations

- **#0, Florence: confirmed and fixed.** In the Phase C batch, all 12 Level 1 runs end `stuck_lock:level_01_florence_lock_print_shop`. That is the known bot gap: `PlaytestBots` never turns a `RotatingGear` (GU_L0.md). It is not this defect, and no batch run solved the gears.
- **#1, Chicago: confirmed and fixed.** No batch run reached the routing hall with a death in it. Easy Lincoln and Normal both stop at the Midway or Electricity locks; Hard collapses.
- **#5 / #13, Gettysburg: confirmed and fixed.**
  - The new Hard real-frames case reproduces the trap against the reverted code.
  - Case (a), "a Middle resume breaks Alpha with `_checkpointHP` still 160", **does not reproduce in a scratch-save harness.** The scratch save carries no attempt registry, so the Entry fracture (`SelfActivating`) is not restored activated. It self-activates on the first frames of the resume, raising `OnCheckpointReached`. That re-captures Alpha at 0, and it also **re-parks the scratch save at `checkpoint_0`** (`LastCheckpointID` checkpoint_1 → checkpoint_0, probed).
  - A real save restores the Entry as activated, so I believe case (a) stands in play as the verifiers describe. The framework covers it regardless: the Angle latches Alpha on its first tick.
  - **For the orchestrator (not fixed, outside scope):** any real save whose attempt registry lacks the Entry fracture would be re-parked at the Entry by a mid-level resume. I did not check whether one can exist (a migrated save, for example).
  - The batch's Level 11 `stuck_lock` runs had 0 deaths, so they are unrelated to the rewind:
    - Easy Lincoln s1 and Normal Joan: Wheatfield, one living member the bot never reaches;
    - Normal Lincoln s1: the Angle, the bot pinned `Running` at x 6775 for 120 s with the slasher at 7005;
    - Hard Joan and Lincoln: Seminary Ridge.

    They belong to nav / member reachability (FIX-ENEMY / GU-BOT).
- **#6, Hakata Bay: confirmed and fixed.** The partial-cut case, which the latch alone cannot cover, fails against the revert. Every batch run that reached the Lantern Watch cleared it with no death inside it.
- **#7, Hakata Bay's font: confirmed and fixed.** The all-level rule also found Florence's and Level 13's fonts exactly on the boundary (accepted, §1).
- **Audit of every lock (48 in Levels 0–15).** Only 9 require anything. All 9 are covered by the latch and its world half. The rest have no required objective:
  - Level 0's trial (built when the trial begins);
  - L1 streets, piazza and workshop;
  - L2 vanguard and battlement;
  - L3 midway and electricity;
  - L4 gatehouse and corridor;
  - L5 all four;
  - L6 forum and ash road;
  - L7 both;
  - L8 dunes, siege line and tomb;
  - L9 seawall and storm anchor;
  - L10 all three;
  - L11 Seminary Ridge;
  - L12–L15 all three each.

| Lock | Requires | Latch covers | Level change |
|---|---|---|---|
| L1 print shop | `florence_gear_puzzle` (gears inside) | yes | **yes**: the gears are kept from the solve, and the objective reads the latch |
| L2 siege line | tower A (inside) | yes; the framework keeps it broken | none (Gate A's forcefield no longer returns under the open, latched barrier) |
| L3 routing hall | `level_03.beam_routing` (coils inside) | yes | **yes**: the coils are kept from the solve, and the objective reads the latch |
| L4 cells | `level_04.free_prisoners` | yes; the framework keeps the manager solved | none, see below |
| L6 vault | `level_06.clear_rockfall` | yes; manager kept | none, see below |
| L8 vault | `level_08.hieroglyph_lock` | yes; manager kept | none, see below |
| L9 Lantern Watch | `level_09.cut_feeds` (relays inside) | yes | **yes**: the puzzle is kept from build, the relays from their cut, the condition is set even under a dark lantern, and the font moved |
| L11 Wheatfield | Alpha (inside) | yes; the framework keeps it broken | none |
| L11 Angle | Alpha (**outside**, behind its entry) and Bravo | yes, from Armed | none |

  - **L4 / L6 / L8 (not changed, recorded):**
    - Each door is freed for good by the solve (`OpenDoor`), and each objective reads the door, not the puzzle.
    - The latch keeps the lock clearable, and the framework keeps the manager solved (the Level 6 Reset Puzzle station keeps refusing).
    - A rewind after the solve still restores the props, as it did before Phase B: L4's prisoners back in their cells (re-freeing them overcounts `PrisonersFreed`, which is display only once solved), L6's pans, boulders, wedge and counterweight, and L8's glyph sequence.
    - This is cosmetic, with no barrier or objective effect.
    - A follow-up could keep each puzzle's props from its solve, the Florence and Chicago pattern.
  - **Not covered** (a Cleared-from-baseline lock never ticks, so it never switches policies): after a resume past a lock, a breakable the resume broke and no Armed lock requires keeps its default policy. A later rewind restores it behind the hero. Examples are Level 2's tower A on a Middle or PreBoss resume, and Level 11's arrays on a PreBoss resume. This is pre-existing and cosmetic: the way is open and the lock Cleared.

## 7. Bot check

One run from this worktree:

`--level=9 --character=joan --bot=play --difficulty=easy --tag=fixobj1 --max-seconds=420` → **`level_complete`**, 142.5 s, 3 / 3 locks cleared (Lantern Watch engaged 33.9 s, cleared 62.6 s), 1 death (at the boss, x 9754), 0 left alive.

The log holds only the slotless-run warnings and the known "resources still in use at exit".

## 8. GU_E authoring guide: rules to add (for the closeout)

- **A lock-required objective is one-way.** The lock latches a required puzzle or breakable the first time it sees it met, from Armed on, and switches the object to `PreserveCurrentState`. Never make a lock require a puzzle that must legitimately reset on a rewind (a timed or failable puzzle). Give the lock the puzzle that the failable one completes instead.
- **Keep the props, too.** Anything whose state a required puzzle's conditions read stays in `PreserveCurrentState` from the solve on: gears, coils, relays, levers, plates and weights. Set it in the `PuzzleCompleted` handler (that also runs for a completion the save restores). For an act that is one-way in itself, such as Level 9's relay cut under a lantern that never relights, set it at the act or at build.
- **Derive a puzzle objective from the lock.** Post "solve the puzzle" from `EncounterLock.IsRequiredPuzzleMet(id)`, never from the puzzle alone, so the objective and the barrier agree.
- **A required object may stand outside its lock** (Level 11's Alpha): the lock latches it while Armed.
- **Fonts and barriers.** No font's channel circle may straddle a lock barrier (`EncounterLockContentTests.NoRestorationFontsChannelCircleStraddlesALockBarrier`). Keep a font at least `ChannelRangePixels − 20` (70 px) from the barrier's **far** face, which is 50 px from the near face of a 20 px barrier. The ±30 px footprint rule alone is not enough.
- **Tests that land a rewind inside a lock:**
  - Hold the hero inside for the whole buffer: `ChronalRewindBuffer.DefaultCapacity` frames, with `ScriptedHPFloor = 1`.
  - Kill it with `KillPlayerNonHit()` after checking `RemainingRewinds > 0`; `RefundRewind()` if not.
  - Wait out `IsRewinding` and `IsPostLandingHoldActive`.
  - `EncounterLockObjectiveContentTests.LevelFixture` is the pattern.
  - A scratch save carries no attempt registry, so the Entry fracture self-activates on load, re-capturing every rewindable and re-parking the save at the entry. Do not build a "Middle resume" rewind scenario on a scratch save without seeding the registry.

## 9. Merge seams

- **`EncounterLock.cs`.** FIX-ENEMY edits `TickWatchdog` in parallel; **I did not touch it.** My edits:
  - three new private fields directly after `_members`;
  - one new line in `Tick` (`LatchRequiredObjectives();` after the first `PollMembers();`);
  - the whole `// === Conditions ===` region;
  - two lines of the class summary.

  If FIX-ENEMY reorders the top of `Tick`, keep `LatchRequiredObjectives()` before the containment and held checks.
- `EncounterLockSpec.cs`: docs on `RequiredPuzzleIDs` / `RequiredBreakables`.
- `Level01Controller.cs`: the gear fields, `BuildGearPuzzle`, the new `OnGearPuzzleSolved`, and one condition in `RefreshObjective`.
- `Level03Controller.cs`: `OnBeamPuzzleCompleted`, the new `KeepBeamRoutedThroughRewinds`, and `RefreshApproachObjective`.
- `Level09Controller.cs`: `RestorationFontPosition`, `CollectAuthoredNodes` (one statement), and `OnFeedRelayCut`.
- `EncounterLockContentTests.cs`: one case and its private helper before `// === Snapshots ===`, `LevelSnapshot.Fonts`, one line in `Collect`'s `RestorationFont` case, and one doc bullet.
- `FontHazardClearance.cs`: the new `CheckLockBarriers` before `Walk`, and a doc paragraph.
- `en.csv`: **no keys added.**
- `scripts/Diagnostics/PlaytestBots.cs` (GU-BOT's) still reads `Spec.RequiredBreakables` directly. It could read `IsRequiredBreakableMet` instead. That is harmless today, because a latched breakable now stays destroyed.

## 10. For the closeout

- **Ledger:**
  - write `PROVISIONAL-LOCK-OBJECTIVE-LATCH` and `PROVISIONAL-FONT-LOCK-BARRIER` (§3);
  - amend `PROVISIONAL-ENCOUNTER-LOCK-RULES`: decision 8's hard cap reads the latched values, and the clear rule's "every `RequiredPuzzleID` `IsSolvedOrPersisted`" now reads "latched once seen";
  - amend `PROVISIONAL-FONT-PLACEMENT`: Level 9's font is at 8640, and the barrier rule.
- **AGENTS.md:**
  - the encounter-lock rows and bullets: the one-way objective rule and the three read members;
  - the Level 9 font at 8640;
  - the baseline +10.
- **Design master:** the lock section's clear rule (plan §2.3 "Clear") owes "an objective, once met, stays met; a death rewind never un-meets it".
- **Open, recorded above:**
  - L4 / L6 / L8 props still revert cosmetically after a solve;
  - a resume-broken breakable that no Armed lock requires keeps its default policy;
  - the scratch-save Entry self-activation observation.
