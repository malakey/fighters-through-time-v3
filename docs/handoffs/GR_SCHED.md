# GR-SCHED handoff: the Legacy Unlock Schedule, one level earlier

Workstream GR-SCHED of the gameplay review package (plan `docs/GAMEPLAY_REVIEW_2026-11-03_PLAN.md` §1.5–6 and §2; user rulings 3b and 3b+, Q3). Branch `gr/sched`. **Nobody has played or looked at any of it**; every acceptance claim is a headless suite.

## What shipped

- **The table** (`FTT.Core.LegacyUnlockSchedule.MilestoneLevelFor`, the completion of that level grants the slot): Special 1 **0**, Movement **1**, Special 2 **2**, Ultimate **3**. Special 1 is playable from Level 1, Movement from 2, Special 2 from 3, and Level 4 is the first full-kit level. Level 0 stays basics-first: the hero plays it with no Special, and completing it restores Special 1 on its results beat (Level 0 already reaches `OnLevelComplete` and shows `LevelResultsPanel`, so `ShowResonanceRestoredBeat` needed no new UI).
- **`LegacyUnlockSchedule.NotGated = -1`** replaces the "milestone `<= 0` means ungated" sentinel. `MilestoneLevelFor` returns it for a slot the schedule does not gate. Every site now reads `< 0` (ungated) or `>= 0` (gated): `LegacyUnlockSchedule.TryGrantedSlotForLevel`, `IsUnlocked`, `IsUnlockedFor`, `PlayerController.IsAbilityUnlocked` (`PlayerController.cs:418`) and `MoveListScreen` (`:378`, `> 0` became `>= 0`). `LevelNumberOf` returns −1 for an unrecognized or retired (`level_04a_*`) ID, and that value is only ever compared with a gated slot's milestone (0 or more), so a stray ID cannot grant Special 1.
- **`GatedSlots` is in unlock order** (Special 1, Movement, Special 2, Ultimate). Its two iterating callers (`PlayerController.cs:400`, `PlaytestRunner.cs:344`) need no change; the bot's `MilestoneLevelFor(slot) < Level` test gives Level 1 S1, Level 2 S1+Movement, Level 3 +S2, Level 4 the full kit.
- **No save derivation.** Persisted keys (`special1` / `movement` / `special2` / `ultimate`) keep their meaning; `UnlockedSlotsFor` unions them with the completed-level history, so a save only ever gains a slot. Two cases pin it (see tests). **Plan correction 5 holds:** a hand-built `CompletedLevels` with no `level_00_tutorial` no longer restores Special 1 from history alone (the old table took it from Level 2). A real save carries the `special1` key, which survives. A fixture that hand-builds history and expects Special 1 must add `level_00_tutorial` or the key.
- **One tooltip**, `special_momentum` (`FirstUseTooltips.SpecialMomentum`, key `tooltip_first_use_special_momentum`), count 9 to 10 (`All`). Pure rule `FirstUseTooltips.SpecialMomentumDue(levelNumber, special1Unlocked)` is true on Level 1 once Special 1 is unlocked; `FirstUseTooltipPresenter._PhysicsProcess` offers it from the polled triggers (one hunk, six lines).
- Prose: `LegacyUnlockSchedule` header, `LevelResultsPanel` comment (`:342`).
- `localization/en.csv`: one block at the end, `# GR-SCHED`, one key: `tooltip_first_use_special_momentum`. **It needs `--import` to compile; I did not commit `en.en.translation`.** `StoryHudBoundsTests` (the tooltip fits the free band at three UI scales) and the tooltip key resolution read the compiled file; both passed here against a locally imported translation.

## Exact API names

`LegacyUnlockSchedule.NotGated`, `.MilestoneLevelFor`, `.GatedSlots`, `.TryGrantedSlotForLevel`, `.IsUnlocked`, `.IsUnlockedFor`, `.UnlockedSlotsFor`, `.UnlockedKeysFor` (the last two unchanged); `FirstUseTooltips.SpecialMomentum`, `.SpecialMomentumDue`, `.All` (10), `.KeyFor`.

## Tests

Declared +5; **actual +5**, no deletions (`[TestCase]` count 4028 to 4033).

New cases (all in place in existing suites; one `[TestSuite]` per file kept):
- `LegacyUnlockScheduleTests.TheUngatedSentinelIsNegativeSoLevelZeroCanBeAMilestone`
- `LegacyUnlockScheduleTests.SavedKeysSurviveTheShiftSoNoSaveEverLosesASlot`
- `LegacyUnlockScheduleTests.AHistoryWithNoLevelZeroReadsSpecialOneOnlyFromItsSavedKey`
- `LegacyUnlockScheduleTests.LevelZerosCompletionRestoresSpecialOneOnItsResultsBeat`
- `FirstUseTooltipsTests.TheSpecialMomentumTooltipIsDueOnLevelOneOnceSpecialOneIsRestored`

Changed in place (0 delta): `LegacyUnlockScheduleTests` (table, fresh-campaign and Level 3 full-kit cases, the controller gate order, the milestone grant now Level 1 / Movement, the Ultimate beat on Level 3 and renamed `TheUltimateBeatPlaysOnLevelThreesResultsLikeEveryOtherUnlock`, `LevelFourIsTheFirstFullKitLevel...`, `LevelIdsResolveTheirMilestone...`), `MoveListDormantTests` (Special 1's badge reads "Level 0" and clears on Level 0; Special 2 is the one still dormant after Level 1), `FirstUseTooltipsTests` (nine to ten), `ResonanceProgressionTests.ARealGatedNodeStaysDormantUntilTheSavesScheduleRestoresItsSlot` (Special 2 is restored by Level 2).

**Fail-first:** with the old sentinel logic restored (`<= 0` / `> 0`) and the new table kept, 10 of 21 cases in `LegacyUnlockScheduleTests`, `MoveListDormantTests` and `FirstUseTooltipsTests` failed, including each new one; with the new code all pass.

**Filtered runs (new code):** 33 suites (every suite that touches `CompletedLevels`, `UnlockedLegacyAbilities`, the gate, the Move List, the Level 0 flow, the HUD tooltip fit and the menu bounds) read `Failed: 0`, after the two fixes above (first run `Total: 326`, 2 failed; second run of 49 further suites incl. all Level 5 to 15 content suites `Passed: 559, Total: 559`; final re-run of the five edited suites `Passed: 55, Total: 55`). The plan's "about 25 files with fixtures that move" was in fact four suites; the other fixtures in the grep list pass unchanged because the new table is a superset on every history that includes Level 0 and a fresh save is gated the same.

## Provisional

- The tooltip's English copy (`"Special 1 is restored — each Special spends one Momentum pip, and landing basic hits refills them."`) is agent-written.
- **The tooltip shows only on a skipped-calibration slot**, like the other nine (`ShouldShow` requires `SkippedCalibration`, AGENTS' documented invariant). A Full-calibration player still gets no Special tooltip, as before (the Special lesson was deleted from Level 0 by Package 11). Whether Level 1 should teach Special 1 to every slot (an exemption in `ShouldShow`) is a question for the user; it would change the documented "a Full-Calibration slot never shows one" rule.
- Special 1 on Level 0's results screen reads "Dormant - restored after Level 0" in the Move List before then (the dossier accepted this).

## Hash and save effects

None. No component, protocol, schema or save field; `UnlockedLegacyAbilities` keys unchanged. No Fighter hash moves (nothing under `scripts/FighterSim/`).

## Deviations from the plan

None of substance. `LevelResultsPanel.cs` got a comment only. `FirstUseTooltipPresenter.cs` is shared with GR-TF (matrix row "SCHED / TF"): my hunk is the one `Offer` call in `_PhysicsProcess`, directly under the Defy line; GR-TF's `SceneHasTimeFreeze` and attach hunks are elsewhere in that file, so a textual conflict is unlikely.

## Not shipped

No Levels 1 to 4 tuning (ruling Q3); no bot batch (the orchestrator owns those; bot runs of Levels 1 to 4 now carry one more slot, so they are not comparable with before). The Level 1 bot read after this change is the orchestrator's (`D_progression` §2: Normal bots completed L1 7/9, L2 3/9, L3 8/9, L4 3/9 before).

## What the docs owe (GR-DOCS)

- Ledger: `USER-LEGACY-SCHEDULE-SHIFT` (S1 0, Movement 1, S2 2, Ultimate 3), **declared as a no-op for saves** (this corrects 3b+'s "will need a declared derivation"), `NotGated = -1`, plan correction 5 (fixtures with no `level_00_tutorial`).
- AGENTS: the Legacy Unlock paragraph ("Movement Ability after Level 1, Special 1 after Level 2, Special 2 after Level 3, the Ultimate cast after Level 4 — so Level 5 is the first full-kit level") becomes the new table with Level 4 first full-kit; "Level 5 first full-kit" elsewhere (the boss-arena and Level 4A bullets) the same; baseline arithmetic +5; the first-use tooltip count nine to ten and the `FirstUseTooltips` list.
- Design master and mirror: the Legacy Unlock table; `design-godot.md` "Level 0 lessons" if it names Level 5 as first full kit; contracts `STORY_PERSISTENCE` and `CAMPAIGN_VALIDATION` if they carry the old table.
- CLAUDE: the `movelist_dormant_until` and failure-signature text need nothing; the bot kit note ("Level 2 S1+Mov…") if present.

## Merge notes

`GatedSlots` order changed: any later test that indexes it positionally would move (none found). Suites merged later that build a Story hero from a hand-built `CompletedLevels` and expect a slot dormant (Special 1 after Level 1 or Level 2, Ultimate after Level 3) will fail and need the in-place edit above. GR-CAP's grid pins about dormant nodes (`GatedAbilityID` Special 1 / Special 2 nodes go live one level earlier, no data change) are unaffected. `Level01ContentTests` and `Level02ContentTests` passed unchanged.
