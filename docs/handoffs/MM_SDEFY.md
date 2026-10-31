# MM-SDEFY handoff (meter and Defy update, Phase B1): the Story active Defy and the Level 0 lesson

Branch `mm/sdefy` (worktree `D:/Projects/ftt-wt/mm-sdefy`), cut from `9cba2b9a` (MM-RULES). Plan sections 2.4 to 2.7 (Story halves) and 2.14 (lesson); resolutions P5 to P11, P21 to P23, P30.

## 1. Shipped (exact names)

Commit 1 `d1b1e2df`, the passive Defy deleted:
- `PlayerController.ApplyDamage`: the lethal branch is gone. Deleted: `_defyProtectionAwaitControl`, `_defyProtectionFrames`, `_defySuppressHitEffects`, `_defyFiredThisHit` (and the `defied` / `defySuppressed` branches in `OnHurtboxHit` and `ApplyEnvironmentalRemainder`), `_storyDefyHistoryUsed`, `SetStoryDefyHistoryUsed`, `ClearDefyProtection` and its five calls, `DefyProtectionFramesRemaining`, `DefyProtectionAwaitingControl`, `DefySeal`, `_publishedDefySeal`, `PublishDefySealIfChanged` and its call, the `TickStoryDefense` await-control branch, `StoryDefenseRules.DefyProtectionFrames`, `StoryManager.StoryDefyHistoryUsed` / `DefyHistoryUsed` and their reset, save-write and save-read lines, and the `CharacterFactory` spent-Defy seeding.
- `PlayerController.IsDefyProtected` is now `CurrentState == Defying && 1 <= _defyFrame <= BasicComboRules.DefyInvulnerableFrames`. Every existing gate (`ResolveIncomingHit`, `ApplyPersistentDamage`, `ApplyEnvironmentalDamage`, `IsUltimateActivationAvoided`, `EnvironmentalBlock.cs:66`, `EinsteinRelativityRift.cs:309`) reads it unchanged.
- Kept for MM-SWEEP (plan 1.9): the dead getter `PlayerController.StoryDefyHistoryUsed` (returns false), `DefySealModel`, the `EventBus` seal members, `StoryAttemptState.DefyHistoryUsed` and `StorySaveData.StoryDefyHistoryUsed` (dead save data, no longer written by `StoryManager`).

Commit 2 `0d717d85`, the action (`scripts/Characters/PlayerController.Defy.cs`):
- `IsDefying`, `DefyFrame` (1 to 30, 0 idle), `DefyExecutionCount`, `CanDefyNow`, `DefyDisabledByScript` (set), `CashInEchoPool()`, internal `SeedDefyFrameForTest(int)`.
- `TryBeginDefy()` runs once a tick after the counters, before the tech-lock return and the state switch; `BufferDefyPress()` is called in the hitstop branch; `case CharacterState.Defying: ProcessDefying(dt)`; `TransitionTo` zeroes `_defyFrame` on any exit from Defying.
- Reads `CurrentInputFrame.IsPressed(GameplayButtons.Defy)` (the wire bit MM-RULES added). It does not read `Actions.Defy`, so it does not depend on MM-INPUT; MM-INPUT's sampling puts the action on that bit.

Commit 3 `ad657bd4`, the blast: `PlayerController.DefyBlast.cs` (`ExecuteDefyBlast`, `PlayerController.ApplyDefyBlast(Vector2 push, int staggerFrames, int hitstopFrames)`), `EnemyController.DefyBlast.cs` (`EnemyController.ApplyDefyBlast(Vector2 push, int staggerFrames, int hitstopFrames)`), `BossController.ApplyDefyBlast(Vector2 push)`.

Commit 4 `c973f8b2`, the lesson: `Level00Controller` (`BeginMeterAndDefyLesson`, `ProcessMeterAndDefyLesson`, `OnDefyExecuted`, a three-drone swarm, internal `CalibrationScript`), `TutorialCalibrationScript.RegisterDefyExecuted()` (replaces `RegisterDefyProc`; `SkipDefyLesson` removed), `en.csv` rows `tutorial_step_defy` / `tutorial_step_defy_spent` rewritten in place.

## 2. Decisions (one line each)

- `_defyFrame` counts the frame being executed and is incremented at the top of `ProcessDefying`, so between ticks it names the last executed frame: other bodies ticking after the hero in the same physics step still see frame 6 as invulnerable (P4).
- The press is read before the tech-lock early return and the state switch (legal from the tech lock, O9); it is not read in the hitstop branch except to buffer. The thaw tick is the first unfrozen tick (the branch returns on the tick the counter hits zero), so the buffered Defy is frame 1 on the tick after the freeze ends.
- Frame 31 transitions to Idle or Airborne and runs that state's processing the same tick, so the hero acts on tick 39 after a press with the blast freeze (38 frames after the press, P11); `StoryDefyTests.TheDefierIsFreeThirtyEightFramesAfterThePress` pins it.
- Release on begin: swing (`CancelActiveAttack`, `ResetComboChain`), casts (`InterruptActiveAbilities`), grab, block stance, stun, tumble, knockdown, daze, tech lock; `TransitionTo` cleans the roll, air dodge and ledge hang.
- P21 refusals, mapped: dead or `CurrentHP <= 0` or Respawning; already Defying; own Ultimate (`UsingUltimate`, any phase); `TimeFrozen`; the death-rewind sequence (`_rewindSuspended`); the Post-Landing Hold and a boss T01b suspension (`IsRecoveryWorldHeld`, `_worldSuspended`, and `ChronalRewindManager.IsWorldHeld(tree)`); Level 0's unlosable hold fight (new `DefyDisabledByScript`, set in `StartHoldFight`, cleared in `BreakTheBeam` and `_ExitTree`); below 50 Influence. A boss-intro freeze has no separate hero flag in Story; the intro freeze rides the hold query above. The tree pause needs no rule.
- The blast gathers targets from group `Enemies` (mobs and bosses, summons included) and, for the hero, group `MirrorParadox` clones; for a hostile clone's blast it gathers the non-hostile `StoryPlayer` bodies. Distance is body centre to body centre (the body's `CollisionShape2D`). The push sign is away from the Defier, the Defier's facing on an exact tie.
- `EnemyController.ApplyDefyBlast` zeroes the getup and Armored Recovery windows (P22), cancels the executor and the siphon tether, takes a fixed stun of `DefyBlastStaggerFrames / 60` s (ignores `StunResistance`), assigns the velocity (the stunned friction and the S1 drop-edge rule then act on it) and never touches `_staggerBudget`, standard poise or the confirm chain.
- `PlayerController.ApplyDefyBlast` (the clone, or the hero for a clone's blast) refuses while `IsDefyProtected`, rolling, post-rewind, tech-invulnerable or knocked down, and otherwise goes through `ApplyStun`, which cancels a swing, a cast (an A02 wind-up or active window included) and a grab; hyper-armour is not consulted because armour lives in `OnHurtboxHit`.
- Pose (P30): `ShowAttackPose("ultimate", 0)` for frames 1 to 6, `idle` after. Presentation hooks `PresentDefyStart` / `PresentDefyBlast` are called; MM-FX implements them.
- Level 0 button label: `DefyButtonLabel()` reads `InputMap` for `gameplay_defy` (the keyboard and the gamepad binding joined by " / ") and falls back to the word "Defy" while MM-INPUT has not merged. MM-TEXT may replace it with the localized prompt glyphs.
- Level 0 swarm: three `hologram_drone`s from `EnemyFactory.SpawnSummoned` 220 to 340 px from the hero, 120 frames after the lesson begins, respawned when all are down; `ScriptedHPFloor` is 1 for the step and restored on completion and `_ExitTree`; the bar is topped back up to 50 if it drops below the cost before the press.

## 3. Deviations

- The four commits follow the plan's order by concern, but the first three are not independently green: `PlayerController.cs` is a hub file whose hooks (`TryBeginDefy`, the buffer call, the switch case) landed in commit 1 with the deletions, and `IsDefyProtected` needs `_defyFrame` from commit 2. Only the branch tip is verified.
- `StoryAttemptState.cs:366` (legacy recovery sets `DefyHistoryUsed = true`) is untouched: it is not in my ownership list and `SaveEnvelopeTests` pins it; plan 1.7's "never written" holds for `StoryManager`, not for this migration path. MM-SWEEP should drop the line with the field.
- `tutorial_step_defy` / `tutorial_step_defy_spent` are rewritten in place by this workstream (the plan's 1.14 matrix gives them to MM-TEXT and MM-SDEFY); I added no new keys, so there is no `# MM-SDEFY block`.

## 4. Cross-edits to files I do not own

None beyond `scripts/Combat/StoryDefense.cs` (listed under my ownership) and `tests/Unit/TimeFreezeTests.cs` / `TutorialCalibrationTests.cs` (the `RegisterDefyProc` rename, named in the brief).

## 5. Tests (+12 against the plan's +12)

New: `StoryDefyTests` 11, `StoryDefyBlastTests` 9, `Level00FlowSceneTests.TheDefyLessonNeedsTheButtonAndTheSwarmCannotKillTheHero` (+1). Deleted: `DefySealStateTests` (4), `StoryDefenceOrderTests.AStoryDefyReleasesTheSurvivor...` and `...FirstSwingEndsTheDefyWindow...` (2), `StoryEnvironmentalDamageTests.ADefiedEnvironmentalHit...` and `DefyProtectionRejectsAHazardTick...` (2), `StoryMeterGainScaleTests.TheMeterAndDefyLessonsLethalHitIsDefied...` (1). Net 3778 + 12 = 3790 (`[TestCase]` count).

Rewritten in place: `StoryRallyTests.ALethalHitOnAFullMeterKillsBecauseThePassiveDefyIsGone` (was `DefyHistorySavesOneLethalHit...`), `StoryTumbleTests.ADefyMidTumbleClearsIt` (presses the button), `TutorialCalibrationV76Tests.TheDefyBeatNeedsTheButtonAndNoLethalHitIsDefiedAnyMore`, `TutorialCalibrationTests` (`RegisterDefyExecuted`, the skip cases removed), `TimeFreezeTests` (rename), `StoryMeterGainFactorTests.TheLevel0DefyLessonSetsTheBarToExactlyTheDefyCostWhateverTheGainFactor` (was `...FillsTheBarToExactlyOneHundred...`), `StoryMeterGainScaleTests.FlatGrantsDirectSetsAndSpends...` (a lethal hit on a full bar now kills), `StoryEnvironmentalDamageTests.ABlockedDischargeStillDrainsMeterAndDebrisDuringDefySpendsNothing` (the Defy startup via `SeedDefyFrameForTest(1)`, set after the stance), `FeelPassStoryTests` (the seal asserts removed).

Seen failing (two mutation runs on the committed tree, then reverted with `git checkout`): run 1 broke the spend (+1), `IsDefyProtected` (+1 frame), the end frame (-1), the buffer call, the push sign, the clone's invulnerability check, the getup reset, the dormant/drill skip, the boss cancel, the pool zeroing, the TimeFrozen refusal and the swarm's HP floor: 13 of 26 cases in `StoryDefyTests`, `StoryDefyBlastTests` and `Level00FlowScene` failed. Run 2 broke the meter refusal, the frame-1-to-6 gravity, a state refusal, the lockout arm, a Special-press refusal, the event raise, the radius (half) and the Defier's hitstop: 16 of 20 failed. Every new case failed in at least one of the runs (Level 0 and the buffer, mirror, boss, armour, dormant, pool and refusal cases in run 1; the others in run 2).

## 6. Commands run and totals

- Setup: `.godot` seeded with robocopy, `--headless --import` (exit 0), `dotnet build` (0 errors).
- Filtered run 1: 315 cases, 313 passed, 2 failed (both my test fixtures: the Level 0 case ran outside the Calibration phase, and the debris case raised the stance after the Defy seed; fixed).
- Filtered run 2 (Story*, Level00/13, Hud, FirstUse, CoreMechanic, LevelStartResource, Rewind, Tutorial, PlaytestCombat, EnemyController, BossController, EncounterLock, MirrorParadox, Suppression, Save*, TimeFreeze, UnusedTranslation, TranslationCsv, CampaignLocalization, Defen*, UltimateMeter, CharacterFactory, Tumble): `Failed: 0, Passed: 1161, Total: 1161`.
- Full run at `c973f8b2` (lock held): `Failed: 0, Passed: 3790, Total: 3790`, 2 m 13 s; the `[TestCase]` count is 3790 (3778 + 12).
- Smokes (`--headless --quit-after 300`, lock held): `Level_00_Tutorial.tscn` and `Level_13_ChronalVoid.tscn` both exit 0; the log shows only the existing "Invalid polygon data, triangulation failed" lines and the known "resources still in use at exit" note.

## 7. Not delivered / open

- The clone's own Defy decision (the CPU) is MM-CPU's; the clone only needs the `GameplayButtons.Defy` bit, which `TryBeginDefy` already honours.
- Nobody played the build: not the freeze, the pose, the push, the heal, the lesson swarm or the objective text.

## 8. For the docs

- Story Defy: legal from any state except the P21 list; 50 Influence unscaled at acceptance; frames 1 to 6 invulnerable with no velocity or gravity; blast and heal on frame 6; vulnerable recovery to frame 30; the hero is free 38 frames after the press (the 8-frame blast freeze pauses the Defier's clock). `D04` and `DEFENSIVE_EFFECTS.md` :237-257 superseded; `design-godot.md` Defy History text and the F13 seal; `TEMPORAL_STATE_CONTRACT` Time Freeze refusal list gains Defy.
- Level 0's Meter and Defy lesson is the button (the Sarah line / station line, plan 2.14, is MM-TEXT's).
- Ledger rows from the plan: `USER-DEFY-ACTIVE-BURST`, `USER-DEFY-FRAMES`, `USER-DEFY-ANY-TIME`, `USER-DEFY-VS-ULTIMATE`, `PROVISIONAL-DEFY-LEGALITY`, `PROVISIONAL-DEFY-FRAMES`, `PROVISIONAL-DEFY-BLAST`, `PROVISIONAL-DEFY-HEAL`, `VERIFY-DEFY-MUTUAL`, `VERIFY-DEFY-BLAST-RINGOUT`, `USER-DEFY-LEVEL0-LESSON`.
