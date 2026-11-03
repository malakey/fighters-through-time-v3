# GR-RANK handoff: the level rank that pays dust

Gameplay review package, Phase A. Rulings 3a, 3a+, 3a++, 3a+5, 3c, 3c+; plan §1.9 and §1.10.

**Nobody has played it or looked at it.** Every claim below is a headless suite. The UI capture tool was not run, and its `results_level` targets still call the overload with no rank, so no capture shows the new lines.

## What shipped

Levels 1–15 rank on damage avoided and speed when they complete. The letter pays dust as a bonus on top of F05, after the tier bonus, and banks with the wallet. Level 1 has no clock and ranks on damage alone. Level 0 earns no rank. Only the campaign hero feeds the tally.

## API

| Name | What it is |
|---|---|
| `FTT.Core.LevelRankRules` (`scripts/Core/LevelRankRules.cs`, pure) | Every rank number. `IsRankedLevel`, `CountsTowardTally(kind, delivery)`, `BaseDamage`, `SpeedIntegrity(finalGate, last)`, `DamageScore`, `ParUse`, `SpeedScore`, `LetterFor`, `DustFor`, `DustOwed`, `Evaluate`, `MaximumDustPerLevel`, `SpeedPercentUntimed` (−1) |
| `FTT.Core.LevelRankLetter`, `FTT.Core.LevelRankResult` | The letter enum (`ToString()` is the saved form) and one evaluated rank (`Letter`, `Points`, `Dust`, `DamagePercent`, `SpeedPercent`) |
| `FTT.Core.LevelRankTracker` (`scripts/Core/LevelRankTracker.cs`) | Node named `LevelRankTracker`, attached by `StorySceneBootstrapper` under `includeRewind`, so every level including Florence and never the hub. Polls for the first `StoryPlayer` that is not `IsStoryHostile`, then subscribes to `DamageTaken`. Internal seams `BindHero()`, `BoundHero` |
| `StoryManager` (`scripts/Core/StoryManager.Rank.cs`, a new partial) | `AddRankDamage(int)`, `RankBaseDamage`, `RankHits`, `LastLevelRank` (`LevelRankResult?`), `LastLevelRankBonusDust`, private `ApplyLevelRankBonus(levelID)` |
| `StoryManager.cs` | Two hunks: the call in `OnLevelComplete` after `ApplyIntegrityTierBonus`, and `RankBonusDust` in `CommitCompletionTransaction` |
| `StoryAttemptState` | `RankBaseDamage` (float), `RankHits` (int), `StoryCompletionTransaction.RankBonusDust`, all clamped in `Normalize` |
| `StorySaveData` | `RankByLevel` (string), `RankDustPaidByLevel` (int), null-guarded in `Normalize` |
| `LevelResultsPanel` | New `ShowResults` overload taking `rankBonusDust` and `LevelRankResult? rank`. The total adds the bonus. Lines `RankLine`, `RankDustLine`, `RankDetailLine`, directly after `DustTierBonus`, hidden when `rank` is null |
| `en.csv` block `# GR-RANK` | `results_rank`, `results_rank_dust`, `results_rank_detail` |

The two callers (`StoryLevelControllerBase`, `Level01Controller`) pass the two new values. Level 0's single-total call is unchanged.

## How the tally behaves

- It counts discrete Hit and Hazard observations only. A `Status` kind and any `HitDelivery.Tick` are excluded.
- Each hit is divided by `GetEnemyDamageMultiplier` at the difficulty current when the hit lands.
- It counts nothing outside a live run, or during a scene load. The load guard matters because the outgoing scene's hero is still in the tree after the new attempt is minted.
- It lives on the attempt record, so a Collapse resume, an Anchor Snap and a quit keep it. A Restart mints a fresh record and resets it. Pinned by `StoryLevelRankTests.TheTallySurvivesACollapseASnapAndAQuitAndOnlyARestartResetsIt`.

## Provisional numbers (agent-chosen; the user ruled only the dust table and the sources)

`BudgetBars` 1.5; weights 0.6 / 0.4; speed band `SpeedZeroParUse` 1.125 and `SpeedBandParUse` 0.9; letter lines S 90, A 75, B 55, C 35. All from the plan §1.9, none measured.

Ruled, not provisional: dust Normal and Hard 50 / 35 / 20 / 8 / 0, Easy 25 / 18 / 10 / 4 / 0.

## Tests

Declared +30, actual **+30**; every new case is below.

| New suite | Cases | Host |
|---|---|---|
| `LevelRankRulesTests` | 14 | pure C# |
| `StoryLevelRankTests` | 8 | Godot |
| `LevelRankTrackerTests` | 3 | Godot |
| `LevelRankResultsPanelTests` | 3 | Godot |
| `LevelRankSaveTests` | 2 | pure C# |

Changed in place, no count change:
- `DustEconomyTests`: `TheLedgerFundsTheContractsUpgradePacingAndNeverTheFullGridOnTheRequiredRoute` is now `TheLedgerFundsTheContractsUpgradePacingAndTheRankBonusSitsOnTopOfIt`. It keeps the ledger facts (787 is under the grid on the ledger alone) and adds 750, 1,537 and 1,844.
- `CompletionOnlyBankingTests`: three expectations add the rank bonus.
- `StoryLevelControllerBaseTests.AfterACollapseAndTheAnchorResumeTheResultsAddUpToWhatTheLevelBanked`: adds the rank bonus and its line.

`LevelResultsStatsTests` needed no change, though the plan expected it might: its calls use overloads with no rank.

**Seen failing:**
- A stub run failed 26 of the 30.
- The 4 that passed then:
  - the bootstrapper attach, already wired;
  - the save round trip, whose fields already existed;
  - Level 0 paying nothing, which the stub also satisfied;
  - the same-share damage case, vacuous at 0 == 0, so a real value was added afterwards.
- Two mutations each failed their case and were reverted: rank applied before the tier bonus, and the `IsStoryHostile` check removed.
- The layout guard added to the first panel case (slab on screen and detail row fits, at UI scale 1.0 and 1.4) has **not** been seen failing on its own.

**Runs**, on the final code:
- The 38-suite filter: `Passed! - Failed: 0, Passed: 323, Skipped: 0, Total: 323`.
- The five new suites alone: `Total: 30`, all green.
- `dotnet build`: 0 errors, and `CS8632` the only warning (pre-existing).

`StoryLevelRankTests` borrows real slot 2 and restores the in-memory slot and the session, as `CompletionOnlyBankingTests` does, because `StoryManager` has no scratch-directory seam. Its completions therefore write that slot's file.

## Save and hash effects

Schema stays v8 and protocol stays v5, with no derivation. Old saves carry no rank. `RatingByLevel` stays dead and is never written. No Klotho component, and no Fighter hash moves: nothing under `scripts/FighterSim/` names any of this.

## Deviations from the plan and dossier

1. **Detail line wording.** `results_rank_detail` reads `"Damage taken: {0}% of your health bar, speed {1}"`, with the `%` inside `{1}`. Level 1 passes `—` there, so it never reads "speed —%". `ResultRow` splits it at the colon.
2. **Where the save fields are written.** `RankByLevel` and `RankDustPaidByLevel` are written in `ApplyLevelRankBonus`, not `RecordLevelResultToSave` as the dossier said, so the deposit's own save write carries the paid record with the dust.
3. **A new partial.** The `StoryManager` rank code is in `StoryManager.Rank.cs`, leaving two small hunks in the shared file.
4. **Three lines, not one.** The plan names `RankLine`; the three keys need three rows, so all three sit after `DustTierBonus`.
5. **Repeat completions.** `RankByLevel` holds the latest completion's letter, and `RankDustPaidByLevel` never falls. A level normally completes once per campaign, so this only shows on a debug replay or an edited save.
6. **One comment outside the named files.** The `DamageTaken` summary in `PlayerController.DamageObservation.cs` said nothing in the game subscribes, which the tracker makes false, so that sentence changed. No code changed there.

## Recorded limits

- **The completion record does not survive completion.** `SaveManager.SaveLevelCompletion` runs after `StoryManager.OnLevelComplete` and writes the next level's empty attempt over `save.AttemptState`. `CompletionTransaction.RankBonusDust` therefore lasts no longer than `TierBonusDust` does, which was already so. The durable record is the two `StorySaveData` fields.
- **`RetainedBaseDust` is not purely base dust.** It is read after both bonuses, as it already was after the tier bonus. Nothing reads it for gameplay.
- **A double completion on a slotless run pays twice** into a wallet that is never banked. The tier bonus has the same gap.

## What did not ship

- No hub ledger line (the dossier called it optional).
- No capture target showing the rank lines.
- `RosterSwapV8.cs:101`'s comment listing per-level dictionaries was left alone; it is outside this workstream and neither new field is hero-keyed.

## What the docs owe (GR-DOCS)

- Ledger rows `USER-LEVEL-RANK-DUST` and `PROVISIONAL-LEVEL-RANK-RULES`, and the amendment to V7.6 ruling 2.A.
- `docs/DUST_ECONOMY.md` and the F05 contract: a section "Rank bonus, on top of the base totals", maxima 1,537 / 1,844 including tier.
- Design master F05 text and its mirror.
- `STORY_PERSISTENCE.md`: the new fields.
- `AGENTS.md` and `CLAUDE.md`: the rank, the new baseline, and every statement that nothing in the game subscribes to `DamageTaken`.

## For the orchestrator

- **Compile the translation.** The three keys need `--import` in `gr-integ`; `en.en.translation` is not committed here. Until then `ScriptTranslationKeyTests` will fail on them.
- **GR-CAP.** When it raises the grid cost, `DustEconomyTests`' `ThoroughBase >= FullGridCost` and `thoroughThroughFourteen >= FullGridCost` become its to reword. The new `1537 >= FullGridCost` line still holds at 1,350.
- **Exact-deposit tests.** A clean run on Levels 1–15 now banks up to 50 more, so any later test asserting an exact deposit there must add `LastLevelRankBonusDust`.
