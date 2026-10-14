# GU-FIX-LEVEL handoff: per-level and HUD fixes (gameplay update 2026-10-12, Phase C fix round)

Workstream FIX-LEVEL of the Phase C fix round of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md`. It fixes the Phase B review findings on Levels 6, 7, 10 and 15 and the Story HUD's GO prompt (`D:/Projects/absolum_research/update_2026-10-12/phaseB_review_verified.json`, key `confirmed`, items 2, 3/14, 4, 8, 9/16, 10, 15 and 18). Story only. Branch `worktree-wf_c57a521f-e47-3`, based on `c4885a9` (main with Phase B merged).

**Nobody has played or looked at any of this.** The evidence is headless: the suites below (each new or rewritten case was checked against a temporary revert of its fix), one full suite run and one `play` bot run of Level 15.

## 1. Shipped, per item

### Items 3 and 14 (duplicates, medium): the Level 6 lava escape never completed for a walking hero

**Root cause.** The escape template's `FinishTrigger` is 96 px wide and centred on `FinishX`. A hero walking east meets it with its pivot about 68 px short of the line (Level 6: at about 8812 against 8880), `NotifyPlayerReachedFinish` refuses, and `BodyEntered` never fires again on the way through. The controller's other completion path only looks at bodies overlapping the damage front, which parks at `EndX` (8800), short of the line. GU-L12 had fixed the same thing in Level 15 only, with a level-side `FrontAdvanced` hook.

**Fix, once, in the controller.** `EscapeSequenceController.AdvanceFront` now completes the run on any running tick (the front parked at `EndX` included) on which its runner stands at or past `FinishX`:
- new `public PlayerController Runner { get; set; }` — the hero the line is judged against; unset, the scene's first `StoryPlayer` runs (`ResolveRunner`, the `EncounterLock.Hero` pattern; `GetFirstNodeInGroup`, so no per-tick collection allocation);
- Level 6 (`OnLevelReady`) and Level 15 (`WireFirestormFinishLine`) set `Runner = Player`;
- Level 15's GU-L12 `FrontAdvanced → NotifyPlayerReachedFinish` hook is **removed** (the shared rule replaces it);
- both levels keep the `FinishTrigger` and its `BodyEntered` wiring as the visible line (harmless: it can only complete a hero already past the line).

### Items 9 and 16 (duplicates): the head start was floored inside the lock, on both levels

**Root cause.** Both escapes computed the front's start as `clamp(heroX − headStart, EscapeStartX, EscapeEndX)`, and each `EscapeStartX` lies inside the lock whose clear starts the escape (Level 15: 6500 against the hall lock's 6420; Level 6: 5700 against the Ash Road's 5660). A hero who took the last kill against the entry barrier got a front at the floor, whose zone (the 320 px west of `FrontX`) covered its body: the first tick caught it (22) and knocked it west, behind the front. Level 15's `BeginFirestorm(float)` re-clamped to the same floor.

**Fix.** Only the end clamps now:
- `Level15Controller.FirestormStartBehind(heroX) = Mathf.Min(heroX − FirestormHeadStartPixels, EscapeEndX)` and `BeginFirestorm(float)` sets `StartX = Mathf.Min(frontStartX, EscapeEndX)` (the parameterless `BeginFirestorm()` still passes `EscapeStartX`);
- `Level06Controller.EscapeStartFor(heroX) = Mathf.Min(heroX − EscapeHeadStartPixels, EscapeEndX)`, which also fixes the rewind restart that uses it.

A front west of the authored start is harmless: only the hero is east of it, it only moves east, and a rewind restores the front from the escape's own state. The doc comments that promised "never west of the authored start" / "every hero still outruns it" are corrected.

### Item 15 (low): a death during Level 15's firestorm stopped it for the rest of the attempt

**Root cause.** The `Firestorm` node used the default `RestoreCheckpointState`. Since GU-L12 the front starts on the hall lock's clear, after the Middle anchor and before the PreBoss anchor (past the finish), so no checkpoint can capture it running: every death rewind parked it at the Middle anchor's capture (6500, stopped), `FirestormTriggered` stayed latched and nothing restarted it (GU-L12's `VERIFY-L15-ESCAPE-REWIND-HALT`, which was named in `GU_L12.md` but never landed in the ledger).

**Fix (the verifiers' corrected fix, matching Level 6).**
- `scenes/campaign/Level_15_Alexandria.tscn`: `Firestorm` `RewindPolicy = 1` (`ResetToInitialState`).
- `Level15Controller.FirestormSurvived` (public, read-only): latched by the escape's `EscapeCompleted` (`OnFirestormCompleted`) and by `SettleFirestormForResume` (a resume past the run).
- `Level15Controller.OnStoryRewind(Vector2 landing)`, subscribed to `EventBus.OnRewindTriggered` in `BindFirestormRewind` (called from `OnLevelReady`) and unsubscribed in `_ExitTree` (`_rewindBound`). The escape's own handler (subscribed in its `_Ready`, so first) has already reset the front to `StartX` and stopped it. The level's handler then:
  - survived: `CompleteEscape()` again (the reset cleared `IsCompleted`), so a death in the rotunda or the boss fight never restarts the fire;
  - otherwise: `start = Mathf.Min(StartX, FirestormStartBehind(landing.X))`; if it moved, `StartX = start; ResetSequence()`; then `Begin()`.

**The same dead read was in Level 6** (the verifiers flagged it). `Level06Controller.OnStoryRewind` guarded on `_escape.IsCompleted`, which the escape's `ResetToInitialState` reset has always cleared by then, so a death after the run was won (say in the caldera) restarted the lava behind the hero. Level 6 now has the same latch: `Level06Controller.EscapeSurvived` (public, read-only), set in `OnEscapeCompleted` and in `MarkWavesClearedThrough(Checkpoint2)`; a survived run is re-completed on a rewind instead of restarted.

### Item 10 (low): Level 15's finish-line assertion could not catch a regression

`Level15ContentTests.TheFirestormStartsOnTheHallLocksClearBehindTheHeroNotBefore` teleported the hero into the `FinishTrigger` past the line, so the trigger's own `BodyEntered` completed the run and the fix it claimed to pin could be deleted unnoticed. Rewritten in place (the verifiers' version): the hero is placed inside the trigger short of the line (`FinishX − 30`) and the run must stay running and incomplete; then clear of the trigger's east edge (`FinishX + 100`, past 9148), where no `BodyEntered` can fire; only the escape's own finish rule can complete it there. It also asserts `FirestormSurvived`.

### Item 2 (medium): Level 7's Middle anchor could not be reached before the boarding lock engaged

**Root cause.** GU-L4 moved the Middle anchor to (5700, 880), on the Bonnet's deck under the **solid** receiving yard (5550–5950, top 692) where rope C lands the hero. The yard's west lip overhangs the water, so the only ordinary way down is its east end (5950), past the boarding lock's engage line (5780 + 20 + 24 = 5824). The lock engaged on the yard, its entry barrier (5760–5780) shut between the hero and the anchor, and `CheckpointTrigger.IsHeldByEncounterLock` refused any strike from inside: the boarding fight had no checkpoint.

**Fix (the verifiers' preferred option).** The anchor stands on the yard:
- `Level07Controller.CheckpointPlacements[1]` = `(Checkpoint1, (5650, ReceivingYardY − 20))`, i.e. (5650, 672); its strike surface is x 5620–5680, y 552–672, and its respawn (5650, 622) lands on the yard;
- new `Level07Controller.ReceivingYardEastX = 5950`; the yard's `BuildPlatform` is now derived from `ReceivingYardWestX`/`ReceivingYardEastX` (the same geometry);
- `SolidDeckSpans` gains `(SwingGapEndX, ReceivingYardEastX, ReceivingYardY)` — the yard's run over the hull, leaving out its 50 px lip over the water, so `TheRopeSwingSpansAChannelNoCharacterCanJump` (no span over the channel) still holds.

Nothing else moved: the lock, the waves and the PreBoss anchor are unchanged.

**All levels checked.** The new suite `AnchorApproachContentTests` runs the rule over every campaign scene (0–15): every live Middle and PreBoss anchor stands outside every lock area and is reachable without crossing the next lock's engage line (method in §3, decision 7). With the fix, **every level passes; no level deliberately differs.** Its second case pins that the rule refuses Level 7's old anchor (5700, 880) and passes the shipped one, so the rule is not vacuous.

### Item 4 (low): Level 7's boarding-channel seam overwrote the engaged boarding lock's objective

**Root cause.** The room-3 pair's east trigger (6040–6120, repeatable) stands about 200 px inside the boarding lock (5780–8480). Its `onEntered` (`EnterBoardingChannel`) posted "board the flagship" through `SetObjective`, which replaced the lock's "Defeat every enemy here…" for the rest of the fight.

**Audit of the other levels.** The same family exists wherever a room trigger's callback can fire while a lock with an `ObjectiveKey` is engaged:
- Level 7: the channel seam, on every run.
- Levels 12, 13, 14 and 15: their repeatable forward triggers sit just inside an area's entry (12: vent field 3040–3120 in 3020–5860 and relay spire 6240–6320 in 6220–7540; 13: drift 3240–3320 in 3220–5100; 14: containment wing 2940–3020 in 2920–; 15: Stacks 3040–3120 in 3020–5900 and the Collapsing Hall 6440–6520 in 6420–7900). The first crossing fires before the engage line, but a hero fighting near the entry steps back over the trigger and re-fires it mid-fight.
- Level 3: its repeatable seam pairs, the same way.
- Not affected: every single-shot trigger placed at `LockMinX + 40` (Levels 2, 4, 6, 8, 10), which fires before the engage line and never again; triggers inside locks with no `ObjectiveKey` (Level 2's siege line keeps the level's tower objective by design).
- Not room triggers, and deliberately not held: the levels' own mid-fight warnings posted as objectives (Level 4's searchlight alarm, Level 9's lantern exposure, Level 6's rescue count). GU-L4 and GU-L8 wrote their `RestoreLevelObjective` overrides expecting those to post mid-fight.

**Fix, once, in the base (small).** `StoryLevelControllerBase.BuildRoomTransition` now runs a trigger's `onEntered` through `RunRoomEntry` (a depth counter, `StoryLevelControllerBase.EncounterLocks.cs`), and `SetObjective` still remembers the key but does not post it while `RoomEntryObjectiveDeferredToLock` holds: a room entry is running **and** `ActiveEncounterLock` has a non-empty `ObjectiveKey`. The lock's release (`RestoreLevelObjective`) then posts it: Level 7's override posts the flagship line; the default restore of Levels 12–15 re-posts the remembered room objective. Level 7's `EnterBoardingChannel` needed no change (it still latches `_flagshipSighted`).

### Item 8 (low): a Level 10 Middle resume left the objective on "cross the stage"

**Root cause.** GU-L8 moved the Middle anchor to 6040, east of the galleries' room trigger (5750–5830), the only live writer of `_galleriesReached`; a resume there never crosses it, and `MarkWavesClearedThrough(Checkpoint1)` set only `_stageReached`. So `RefreshObjective` (also the galleries lock's `RestoreLevelObjective`) posted `globe_objective_cross_stage` in the galleries and again after their clear.

**Fix.** `Level10Controller.MarkWavesClearedThrough` sets both `_stageReached` and `_galleriesReached` for `Checkpoint1` as well as `Checkpoint2`. A live run is unaffected (the anchor can only be struck after crossing the trigger).

### Item 18 (low): the GO prompt showed a bare "GO" for a lock with no direction

`StoryHUD.ShowEncounterGoPrompt` (`scripts/UI/StoryHUD.EncounterLock.cs`) now hides the prompt and returns when the direction is `EncounterGoDirection.None`, so Level 0's Combat Trial (whose clear ends the tutorial) shows no prompt over the completion line and results, and a prompt still up from an earlier lock goes with it. The guard is at the top of the method, so any future caller is covered.

## 2. Files changed

Inside my ownership:
- `scripts/Environment/EscapeSequenceController.cs` (`Runner`, `ResolveRunner`, the finish rule in `AdvanceFront`)
- `scripts/Environment/Level06Controller.cs`
- `scripts/Environment/Level07Controller.cs`
- `scripts/Environment/Level10Controller.cs`
- `scripts/Environment/Level15Controller.cs`
- `scenes/campaign/Level_15_Alexandria.tscn` (one line: `RewindPolicy = 1`)
- `scripts/UI/StoryHUD.EncounterLock.cs`
- `tests/ContentValidation/Level06ContentTests.cs`, `Level07ContentTests.cs`, `Level10ContentTests.cs`, `Level15ContentTests.cs`
- new `tests/ContentValidation/AnchorApproachContentTests.cs` (+ `.uid`)
- new `tests/Unit/StoryHudEncounterGoPromptTests.cs` (+ `.uid`)
- this handoff

Outside my ownership (minimal, the brief's "shared guard in StoryLevelControllerBase is acceptable if small"):
- `scripts/Environment/StoryLevelControllerBase.cs`: two lines — `BuildRoomTransition` subscribes `RunRoomEntry(onEntered, id)` instead of `onEntered(id)`, and `SetObjective` returns after remembering while `RoomEntryObjectiveDeferredToLock`.
- `scripts/Environment/StoryLevelControllerBase.EncounterLocks.cs`: a new region at the end (`_roomEntryDepth`, `RunRoomEntry`, `RoomEntryObjectiveDeferredToLock`).

No `en.csv` key, no save field, no component, no schema change. Nothing under `scripts/FighterSim/`.

## 3. Decisions (candidate `PROVISIONAL-*` rows)

1. **`PROVISIONAL-ESCAPE-FINISH-LINE`.** An escape is won on the first running tick its runner stands at or past `FinishX`, judged by the controller (`EscapeSequenceController.Runner`, else the scene's `StoryPlayer`); the finish trigger is the visible line only. A dead runner past the line also completes it (not excluded).
2. **`PROVISIONAL-ESCAPE-HEAD-START-ANYWHERE`** (amends GU-L4 decision 9 and GU-L12 decision 7). The head start (Level 6 240 px, Level 15 432 px) holds wherever the hero stands when the lock clears; only `EscapeEndX` clamps. A front may start west of the authored `EscapeStartX` (Level 15 down to about 6008, Level 6 down to about 5440).
3. **`PROVISIONAL-ESCAPE-REWIND-RESTART`** (closes `VERIFY-L15-ESCAPE-REWIND-HALT`). Both escapes reset on a death rewind (`ResetToInitialState`) and restart from `min(StartX, landing − headStart)`; once the run has been won (a level-side `EscapeSurvived` / `FirestormSurvived` latch, set on completion or a resume past the run) a rewind re-completes it instead, so a death after the escape never restarts it.
4. **`PROVISIONAL-L07-MIDDLE-ON-YARD`.** Level 7's Middle anchor at (5650, 672) on the receiving yard. Known limit: a hero whose rope-C release carries it past 5824 before it lands (a fast hero holding east through a double jump) engages the boarding lock first, and the anchor then waits for the clear, like any anchor a hero runs past. Moving the boarding lock's west edge past the yard (≈ 5970, the verifiers' option b, which also moves wave 3's 5900 deckhand) would close that too; not done (larger ripple, and the natural landing is on the yard's west half).
5. **`PROVISIONAL-ROOM-ENTRY-OBJECTIVE-DEFERRAL`.** While a lock with an `ObjectiveKey` is Engaged, a room trigger's `onEntered` cannot post an objective; the key is remembered and the lock's release posts it. Only room entries are held — a level's mid-fight warnings still post (decided against a blanket guard because GU-L4 and GU-L8 deliberately post the Level 4 alarm and the Level 9 exposure mid-fight).
6. **`PROVISIONAL-GO-PROMPT-NONE`.** A lock clear with `GoDirection.None` shows no GO prompt and hides a stale one.
7. **`PROVISIONAL-ANCHOR-APPROACH-RULE`** (the new content rule, `AnchorApproachContentTests`). For every live Middle and PreBoss anchor:
   - its footprint (x ± 30) lies outside every lock area;
   - the **next lock** is the one whose area starts at or east of the anchor's footprint; its engage line is `Area.X + 20 + EncounterLockRules.EngageInsetPixels` (the hero's half-width, as `CanEngage` reads it);
   - from the anchor, a search walks its standing surface (the nearest standable top within 60 px below it) both ways until a wall in the hero's 64 px body band, the surface's end or the engage line, and succeeds if the walked stretch has an **open column** (no solid anywhere over the hero's head; one-way platforms, moving bodies, lock barriers, doors, forcefields and breakables are never ceilings or walls) or reaches within 40 px of the previous lock's area (or the hero's spawn when none). Otherwise it moves to surfaces the hero could have come from: onward at the same height (± 2 px) across a seam; down from a higher surface whose edge (± the hero's half-width) is over the stretch, or through a one-way one; up from a lower surface within the roster's weakest double-jump rise beside either end, or through this surface if it is one-way;
   - rect collision shapes only; a shape rotated off the axes is a walkable ramp (its bounding box is a floor, never a wall or ceiling);
   - no ropes, swings, movement abilities, Down-Air through a floor or air drift — a checkpoint must be reachable by ordinary means.
   It is a coarse graph: it can pass an anchor a nav graph would refuse (an open column over a stretch nothing leads into), but it refuses the defect it was written for, which its second case pins.
8. **Level 10:** `Checkpoint1` counts the galleries as reached (the anchor stands in them).

## 4. Deviations from the brief

- Item 2: the anchor moved (the verifiers' preferred option), not the lock.
- Item 4: fixed with a shared base guard (allowed by the brief), scoped to room-trigger callbacks rather than every `SetObjective` (decision 5). No Level 7 code change was needed for it.
- Items 9/16: fixed in Level 6 as well as Level 15 (item 16 covers both levels).
- Item 15: Level 6's same dead `IsCompleted` read was fixed too (it is in my ownership and the verifiers flagged it).
- Item 18's case lives in a new suite (`StoryHudEncounterGoPromptTests`) rather than `EncounterLockTests`, to stay clear of the lock-framework workstream's edits; it drives the HUD through `EventBus.RaiseEncounterLockChanged`, its only input.

## 5. Tests

New cases (+11):
- `Level06ContentTests` 17 → 20:
  - `ClearingTheRoadAgainstItsEntryBarrierStillStartsTheFrontBehindTheHero` (real frames; items 9/16)
  - `AHeroWalkingPastTheFinishLineCompletesTheLavaRun` (real frames, real input; items 3/14; also pins that the trigger meets the hero short of the line)
  - `ADeathRewindRestartsTheLavaBehindTheLandingButNeverOnceTheRunIsWon` (the restart behind the landing and the survived latch)
- `Level07ContentTests` 24 → 26:
  - `TheMiddleAnchorIsStruckFromTheRopeLandingBeforeTheBoardingLockEngages` (real frames, real input and a real swing from the yard; item 2)
  - `TheBoardingChannelSeamNeverPostsOverTheEngagedLocksObjective` (real frames; item 4)
- `Level10ContentTests` 24 → 25: `AMiddleResumeReadsTheClimbObjectiveInTheGalleriesAndAfterTheirClear` (real frames; item 8)
- `Level15ContentTests` 24 → 26:
  - `ClearingTheHallAgainstItsEntryBarrierStillStartsTheFirestormBehindTheHero` (real frames; items 9/16)
  - `ADeathRewindMidRunRestartsTheFirestormBehindTheLandingButNeverOnceItIsSurvived` (item 15)
- new suite `AnchorApproachContentTests` (all sixteen scenes) 2:
  - `EveryMiddleAndPreBossAnchorIsReachedBeforeTheNextLockEngages`
  - `TheRuleRefusesLevelSevensOldMiddleAnchorUnderTheReceivingYard`
- new suite `StoryHudEncounterGoPromptTests` 1: `ALockClearedWithNowhereToGoShowsNoPromptAndRemovesAStaleOne` (item 18)

Rewritten in place, no count change:
- `Level06ContentTests.TheLavaFrontStartsOnlyWhenTheAshRoadLockClearsAHeadStartBehindTheHero`: the `StartX >= EscapeStartX` assertion is gone, and the head-start pin now expects `EscapeStartFor(5600) == 5600 − 240` (it pinned the floor, 5700).
- `Level15ContentTests.TheFirestormStartsOnTheHallLocksClearBehindTheHeroNotBefore`: the finish-line half (item 10, above); it also asserts `FirestormSurvived`.

**Every fix was checked against a temporary revert**, in two passes on the touched suites:
- Pass A undid everything except the shared finish rule: the Level 6 latch (back to the `IsCompleted` guard), both head-start floors, Level 15's rewind policy and handler subscription, the Level 7 anchor (back to 5700/880), the base guard, the Level 10 flag and the HUD guard. Exactly 11 cases failed, each on its own assertion: both `AnchorApproachContentTests` cases (the generic sweep flagged Level 7's anchor at 5700), the three Level 6 head-start/latch cases (the rewritten head-start pin included), both Level 7 cases, the Level 10 case, both Level 15 head-start/rewind cases and the HUD case.
- Pass B undid only the shared finish rule: exactly the 4 expected cases failed — both walking/teleport finish cases (Level 6's walk, Level 15's rewritten finish half: the trigger's `BodyEntered` was still wired and still could not complete it) and the "won" step of both rewind cases.

Runs (lock held for each, `testhost` drained):
- the six touched suites: `Passed! - Failed: 0, Passed: 100, Total: 100`;
- **full suite: `Passed! - Failed: 0, Passed: 3191, Skipped: 0, Total: 3191` (1 m 21 s) = 3180 + 11.** The `[TestCase]` attribute count under `tests/` is 3191.

## 6. Measurements and investigations

- **Phase C bot batch, my levels** (the `guc*` reports): Level 6 stops at `stuck_lock:level_06_pompeii_lock_vault` on every run (the known winch bot gap: the bot never stands on the east pan); Level 7 at `stuck_lock:level_07_nassau_lock_quay` (a quay `chrono_slasher` holds the Ranger's 2950 platform, 88 px above the deck — it does not drop more than 64 px — while the bot idles below it, then jump-strikes the deckhand on the launch yard; a human jumps up to it; the stall watchdog's 200 px vertical threshold does not cover it); Level 10 mostly at `stuck_lock:level_10_globe_lock_galleries` (a slasher on the lower tier at 6700/872, 248 px above the yard floor, with the bot jumping into the tier's underside from directly below; the watchdog did not relocate it in 120 s, `reloc=0`, although the gap is over 200 px — worth a look by the lock-framework or bot workstream); Level 15 completes on Easy and dies to Game Over on Normal/Hard. None of these is a level content bug I could fix inside my ownership; they are recorded here for FIX-BOT and the lock framework. No guc run reached the Level 6 escape or the Level 7 boarding lock.
- **Bot run after the fix** (tag `fixlevel1`; `--level=15 --character=joan --bot=play --difficulty=easy --max-seconds=300`, from this worktree): `level_complete` in 111.7 s, all three locks cleared, 10 of 10 killed, one death (to the boss, in the rotunda). The hall cleared at about 39 s with the hero at x 7003. The firestorm started behind it, and the bot's `escape` task ran to about x 9258 at 46 s, then switched to the boss: the run completed past the line, and the bot no longer idles there. The front never caught the hero (110 HP throughout the run). The death at 91 s rewound in the boss arena and the fire did not restart.

## 7. Not delivered / open

- The Level 7 rope-overshoot limit (decision 4).
- `VERIFY-L15-ESCAPE-REWIND-HALT` is closed by this fix but was never written into the ledger; the closeout should record it as closed.
- The bot findings in §6 (Level 7's platform-posted quay slasher, Level 10's galleries slasher and the watchdog not relocating it).
- The anchor-approach rule is a coarse rectangle graph (decision 7); it does not model ropes, lifts in motion or polygon collision.

## 8. For the closeout

- AGENTS.md:
  - the Level 7 Middle anchor is (5650, 672) on the receiving yard;
  - the escape finish rule (`EscapeSequenceController.Runner`) and the two escapes' rewind restart and survived latches;
  - the room-entry objective guard in the base (encounter-lock bullets);
  - the new suites and the baseline.
- Ledger: `PROVISIONAL-ESCAPE-FINISH-LINE`, `PROVISIONAL-ESCAPE-HEAD-START-ANYWHERE`, `PROVISIONAL-ESCAPE-REWIND-RESTART` (closing `VERIFY-L15-ESCAPE-REWIND-HALT`), `PROVISIONAL-L07-MIDDLE-ON-YARD`, `PROVISIONAL-ROOM-ENTRY-OBJECTIVE-DEFERRAL`, `PROVISIONAL-GO-PROMPT-NONE`, `PROVISIONAL-ANCHOR-APPROACH-RULE`; amend GU-L4's and GU-L12's head-start rows and GU-L8's Level 10 Middle-anchor row.
- GU_E authoring guide additions:
  - a room trigger's objective never displaces an engaged lock's own; it waits for the clear (no level code needed);
  - an escape: set `EscapeSequenceController.Runner`, start the front on the lock's clear `headStart` behind the hero with only the end clamped, use `RewindPolicy = ResetToInitialState` with a level-side survived latch and an `OnRewindTriggered` restart behind the landing;
  - every Middle and PreBoss anchor must be reachable without crossing the next lock's engage line (`AnchorApproachContentTests`); an anchor under a solid overhang must be reached by walking from the west.
