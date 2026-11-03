# D. Progression recon (rank dust, schedule shift, capstones, regen 9 s, target curve)

Repo at 43d54db6. Verified against code; the rulings in DECISIONS_gameplay_review.md are taken as given. Suggestions are marked **[suggestion]**.

## 1. Level rank that pays dust (3a, 3c)

### Current state
- Completion order, `StoryManager.OnLevelComplete` (StoryManager.cs:1634-1657): `CompleteLevelRun` (freezes time, rewinds, integrity, :497) -> `ApplyIntegrityTierBonus` (:1695) -> `CommitCompletionTransaction` (:2221, the only banking event, deposit via `DepositDustToActiveSave` :1610) -> `GrantLegacyUnlockMilestone` -> `RecordLevelResultToSave` (:1794) -> `AdvanceToNextLevel`.
- The F05 tier bonus is `floor(retainedBaseDust x 10/5/0 %)` (`RewardAllocator.TierBonus`, RewardAllocator.cs:149), added with `CollectDust` so it banks with the wallet; untimed Level 1 gets none. **The rank bonus must be applied AFTER the tier bonus**, or the tier percentage would compound on rank dust (F05 "no compounding").
- Base ledger 720 / 1,000; all-Restored maxima 787 / 1,094 (DustEconomyTests.cs:47-72, :242-266). Level 0 pays nothing (:268).
- DustEconomyTests.cs:289-316 asserts "the required route never funds the full grid" (787 < 975) and that 1,000 base is reached only after the finale. **Both stop being true** with +750 (787 + 750 = 1,537 > 1,350 even with the capstone).
- Results panel: `LevelResultsPanel.ShowResults` (LevelResultsPanel.cs:151-172) sums mob + extractor + boss + optional + carried + tierBonus - loss; lines are frozen by `CompleteLevelRun` before the attempt reset (`LastLevelDustLost/CarriedIn`, StoryManager.cs:507-508). Callers: StoryLevelControllerBase.cs:2127-2131, Level01Controller.cs:1334-1338. Level 0 uses the single-total overload (Level00Controller.cs:1482).
- Retired rating: `RatingByLevel` (SaveManager.cs:39) is documented "Nothing may start writing it again"; `LevelResultsStatsTests.TheRetiredChronalRatingLineIsGone` (:172); STORY_PERSISTENCE.md:59/115 call it dead data. **Do not reuse it** (old saves may hold old string values; the pin would need unpicking). Add new fields.
- N05 ending uses `IntegrityByLevel` only (StoryManager.cs:2408-2450); rank must not touch it.
- Inputs that exist: `LevelRewindsUsed` (:92); the Integrity gauge frozen at PreBoss (`_attempt.FinalGateIntegrity`, StoryAttemptState.cs:285, persisted). **No damage-taken tally exists.** `PlayerController.DamageTaken` (PlayerController.DamageObservation.cs:70) is raised once per positive HP loss with `Kind` Hit/Hazard/Status and `Damage`; observation-only, nobody subscribes in production. `LevelElapsedSeconds` is wall-clock `_Process` time (:178-181) and is **zeroed on every `BeginLevelRun`, including a resume** (:411), so it cannot be the speed input.
- Level 1 (Florence) never arms the Integrity clock (no `BeginIntegrityClock` in Level01Controller); `ParSecondsForCurrentLevel` = 0 means untimed (:534).

### Finding that tests the decisions
Round 5 item 6 calls the rank speed "a second speed system". The only persisted, resume-safe, par-based speed measure is the Integrity gauge, so a speed score read from it is the *same* clock banded differently. A genuinely separate clock needs a new persisted per-attempt counter (option B below). Both work; see Q3.

### Proposed formula (pure `FTT.Core.LevelRankRules`, engine-free, new file) **[suggestion; every number provisional]**
Inputs: `baseDamage` (HP lost to discrete Hit/Hazard observations; periodic ticks and Status excluded as CM-FIXA-PRES did; divided by `StoryDifficultyTuning.GetEnemyDamageMultiplier` 0.5/1.0/1.5 so equal *hits* score equally on every difficulty), `maxHP` (effective, `GetSelectedCharacterMaximumHP`, StoryManager.cs:1595), `integrityAtGate`, `timed`, `difficulty`.
- `bars = baseDamage / maxHP`: HP bars lost, so Mozart (85 HP) and Lincoln (130 HP) compare fairly (MaxHP 80-130, resources/Characters). Deaths need no term: a rewind already cost the bar; Collapse and Snap keep the same attempt and tally.
- `damageScore = 100 * clamp(1 - bars / BudgetBars, 0, 1)`; `BudgetBars` per level, default 1.5. Bot data: Normal bots lose roughly 1.5-11 bars a level, Easy 0.6-3 (BOT_TABLES lines 6-20); bots take more than humans, so the budget needs the human session.
- `speedScore = 100 * clamp((integrity - 50) / 40, 0, 1)` (90+ full, 50 zero; completed bot runs keep >= 66 %, AGENTS Integrity bullet). Untimed Level 1: weight 0.
- `points = 0.6 * damageScore + 0.4 * speedScore` (timed); damage only on Level 1.
- Letters and dust: S >= 90 -> 50; A >= 75 -> 35; B >= 55 -> 20; C >= 35 -> 8; D -> 0. Ceiling 15 x 50 = 750 (Levels 1-15; Level 0 excluded: scripted unlosable hold, empty manifest).
- Difficulty: letters are difficulty-normalised; dust factor **Easy x0.5 (S = 25), Normal and Hard x1.0**. Hard is unset in the curve and no bot finishes it, so this is unmeasurable today.
- Pays once: `RankDustPaidByLevel[levelID]`; on completion pay `max(0, new - paid)`. A level completes once per campaign (the portal advances; replay modes are ruled out, 5a), so the guard only covers debug select and edited saves.

### Where the tally lives; resume, quit, restart
- New additive `StoryAttemptState` fields (StoryAttemptState.cs:259): `RankBaseDamage` (float), `RankHits` (int); option B adds `RankClockSeconds`. Written by the existing per-second durable snapshot (`TickDurableSnapshot`, StoryManager.cs:194, :2467) and at checkpoints, so **a mid-level quit cannot launder a bad fight** (at most ~1 s of damage lost). A Collapse (hub extraction), Anchor Snap, checkpoint resume and Quit-to-Menu (`CommitOrderlyExit` flushes the attempt) keep the attempt and the tally.
- Restart Level mints a new attempt (`MintFreshAttempt`) and clears the wallet (H02), so tally and unbanked dust reset together: no farming. Smothered restart likewise.
- Subscriber: one small `LevelRankTracker` attached by `StorySceneBootstrapper` (covers Florence, which does not extend `StoryLevelControllerBase`) subscribes to the hero's `DamageTaken` and only adds ints to `StoryManager` (the event can fire inside a physics flush: no node writes). The "nothing subscribes" comment and `PlaytestCombatStatsTests`' no-subscriber pin need a one-line amendment.
- Option B (separate clock): `RankClockSeconds` accumulates physics ticks while no `IntegrityClockPause` scope is set, including the boss fight, against `authoredPar * 1.5 * DifficultyTimeMultiplier`. +1 field, +3 tests.

### Required changes per file
- New `scripts/Core/LevelRankRules.cs` and `LevelRankTracker.cs`. `StoryManager.cs`: `AddRankDamage`, `ApplyLevelRankBonus()` called in `OnLevelComplete` between :1640 and :1647 (CollectDust(bonus), `LastLevelRank`, `LastLevelRankBonusDust`, `_attempt.CompletionTransaction.RankBonusDust`, `RankByLevel` / `RankDustPaidByLevel` written in `RecordLevelResultToSave`).
- `SaveManager.cs` `StorySaveData`: `RankByLevel` (string), `RankDustPaidByLevel` (int), null-guards in `Normalize()` (:175-181 pattern). `StoryCompletionTransaction.RankBonusDust` (+Normalize). `RosterSwapV8.cs:101` comment lists per-level dictionaries (neither new one is hero-keyed).
- `LevelResultsPanel`: new overload with `rankBonusDust` + letter; total adds it; new `RankLine` ordered after `DustTierBonus` (Order() :290-292, `ResultRow.Attach` list :323). Update both callers. Level 0 unchanged.
- `localization/en.csv` (then `--import`, commit the regenerated translation): `results_rank,"Level rank: {0}"`, `results_rank_dust,"Rank bonus: {0}"`, `results_rank_detail,"Damage taken {0}% of your health bar, speed {1}%"` (quote values with commas).
- Docs: `docs/DUST_ECONOMY.md` and the contract (new section "Rank bonus, on top of the base totals"; maxima 1,537 / 1,844 including tier), ledger `USER-LEVEL-RANK-DUST`, `PROVISIONAL-LEVEL-RANK-RULES` (V7.6 ruling 2.A amended), design master F05 text, AGENTS, CLAUDE.

### Save implications
Schema stays v8: additive fields with initializers (precedent: the Momentum key, MM update). No derivation (old saves simply carry no rank). Round trip in SaveEnvelopeTests. `CurrentVersion` is shared; do not bump.

### Test impact (est. +30)
New: `LevelRankRulesTests` pure ~14 (thresholds, multiplier and HP normalisation, untimed, Easy factor, pay-once delta, 750 ceiling); `StoryLevelRankTests` ~8 (after-tier order, deposit, results freeze, resume keeps tally, restart resets, collapse and snap keep, quit keeps); `LevelRankTrackerTests` ~3; results panel ~3; save round trip ~2. In place: DustEconomyTests :289-316, `LevelResultsStatsTests`, `CompletionOnlyBankingTests`, `StoryLevelControllerBaseTests` results cases.

### Risks
Rewards players already doing well; Normal and Hard bots rarely finish, so first real data is the human session. `BudgetBars` is a guess. Boss damage is 41-57 % of Normal damage, so rank is largely "beat the boss cleanly". Integrity-based speed is skewed by extractor breaking (lowers drain) and ignores boss time. A strong player can fund the 1,350 grid by about Level 8-10 (ruling 3a++ accepts).

## 2. Legacy schedule shift (3b+)

### Semantics (verified)
`MilestoneLevelFor(slot)` is the level whose **completion** grants the slot (LegacyUnlockSchedule.cs:65-71): Movement 1, S1 2, S2 3, Ultimate 4, so today Movement is playable on Level 2, S1 on 3, S2 on 4, Ultimate on 5. The ruling (S1 on Level 1, Movement on 2 unchanged, S2 on 3, Ultimate on 4) therefore means milestones **S1 = 0, Movement = 1, S2 = 2, Ultimate = 3**. Levels 1, 2, 3 and 4 each gain exactly one slot; Level 5+ unchanged.

### Hard problem: milestone 0 collides with the "ungated" sentinel
`<= 0` means "not gated" in `IsUnlocked` (:121), `IsUnlockedFor` (:160), `TryGrantedSlotForLevel` (`levelNumber <= 0`, :105), `PlayerController.IsAbilityUnlocked` (PlayerController.cs:418) and `MoveListScreen` (:378). Introduce `NotGated = -1` and change those five sites to `< 0`. `LevelNumberOf("level_00_tutorial")` already returns 0 (:195-204). Level 0 reaches `OnLevelComplete` through `LevelManager.CompleteLevel` (Level00Controller.cs:1467; `level_00_tutorial` lands in `CompletedLevels`, StoryManager.cs:292) and shows `LevelResultsPanel` (:1479), so the "RESONANCE RESTORED" beat plays for Special 1 with no new UI. Reorder `GatedSlots` to unlock order; callers iterate it (PlayerController.cs:400, PlaytestRunner.cs:344).

### Save and derivation
**No derivation is required**, contradicting the decision's "will need a declared derivation": `UnlockedSlotsFor` unions the saved keys with the completed-level history (:140-152), and the new table is a strict superset of the old for every history, so no save can lose a slot; keys backfill idempotently on the next grant (StoryManager.cs:1770). A mid-level save gains the slot on next load, harmlessly. The v5-to-v6 backfill (SaveEnvelope.cs:479-485) already ran and is not re-run. Recommend a test pinning "old-table saves read as supersets" and a ledger line declaring the no-op.

### Other consumers
- Grid dormant gate (`GatedAbilityID`, 47 uses in resources/Resonance; `ResonanceProgression.IsAbilityUnlocked`): S1 and S2 scoped nodes go live one level earlier; no data change.
- Move List `movelist_dormant_until` = "Dormant - restored after Level {0}" (en.csv:992) prints the milestone; S1 would read "after Level 0" (acceptable) once the sentinel is fixed.
- HUD lock states come from `AbilitySlotLockPayload` and the table (PlayerController.cs:400-403): no change. No dialogue or hub readout names a restored power by level (grep of en.csv finds only `results_resonance_restored` and `movelist_dormant_until`).
- Level 0 stays basics-first (the Special lesson is deleted; hold fight scripted). Level 1 teaches S1 with nothing today: **[suggestion]** add a first-use tooltip `special_momentum` (FirstUseTooltips.cs:24-57; `FirstUseTooltipsTests` pins Count 9 at :36 and :48 -> 10) with key `tooltip_first_use_special_momentum`. A fresh attempt already starts at 2 pips (`MomentumStartPips`), so the first cast is available at once.
- Bots: `ApplyCampaignKitOnce` uses `MilestoneLevelFor(slot) < Level` (PlaytestRunner.cs:346); the new table gives Level 1 S1, Level 2 S1+Mov, Level 3 +S2, Level 4 full with no edit. Bot batches before and after are not comparable on Levels 1-4.
- Tests pinning the schedule: `LegacyUnlockScheduleTests` (8 cases: table, IDs, key round trip, gate, holodeck full kit, idempotent grant, Ultimate beat on Level 4's results -> Level 3's, "Level 5 first full kit" -> Level 4), `MoveListDormantTests`, `ResonanceLiveApplyTests`, `ResonanceProgressionTests`, `RetireLegacyLevelV8Tests`, `SaveEnvelopeTests`, `SaveSchemaV8MigrationTests`, `StoryResourceTimerPersistenceTests`, `Level01ContentTests`, `Level02ContentTests`, `Level04ContentTests`, `HubPerLineGatingTests`, `PlaytestEncounterTelemetryTests`, `CampaignRouteTests` comment (:230). About 25 files grep-hit, **+4 new, ~35 assertions edited**.
- Prose: LegacyUnlockSchedule.cs header (:12-22), LevelResultsPanel.cs:342, AGENTS (Legacy Unlock paragraph, "Level 5 first full-kit"), design master and contract tables.

### Levels 1-4 data (BOT_TABLES, current kit Mov/S1/S2)
Normal bot completion and deaths: L1 7/9 d2.78, L2 3/9 d3.11, L3 8/9 d1.78, L4 3/9 d3.33; boss share of damage 57/67/68/31 %. Each level gains one slot; Level 4 gains the Ultimate. Easy completes 9/9 on L1, 2, 5, so Easy trivialises further; Normal L2 and L4 are the weak spots. No data exists for the new kit: rerun Levels 1-4 after, under a separate tag.

## 3. Grid capstone (3a++++)

### Current state
Every grid has 9 nodes, Tier 1/2/3 x3, costs 50/75/200 = 975, Major iff Tier 3, Traversal in Tier 2 (ResonanceProgressionTests.cs:503-548; DustEconomyTests.cs:81-110; TubmanContentTests.cs:109). `Tier` is a plain int export (ResonanceNodeData.cs:45); `ValidateGrid` (ResonanceProgression.cs:147) checks no tier; the panel passes `node.Tier` to `ResonanceTierMark` (ResonanceGridPanel.cs:297). `LayoutPosition` is normalised (Joan uses y 0.06-0.95); chips sized by `ResonanceChipLayout`. Perk keys are read through `HasStoryPerk`, and `ResonanceReaderCoverageTests` fails on a key with no reader. `RosterSwapV8`'s embedded Pocahontas price table (975) is history and unaffected.

### Design
A tenth node per hero: `Type = Major`, **`Tier = 4`**, `UnlockCost = 375` (1,350 total), `PrerequisiteMode` All of two existing Majors, `GatedAbilityID` only if the effect is slot-bound, `LayoutPosition` at the bottom (existing nodes reach y 0.95, so re-flow).
Changes: nine `.tres` (node + `Nodes` array); test constants (`GridTotalCost` 975 -> 1350, 9 -> 10 nodes, tiers 3/3/3/1, "Major iff Tier >= 3", cost by tier); TubmanContentTests:109; ResonanceGridSceneTests (:106-108 positions 9 -> 10); `ResonanceGridFitTests` (ten chips per hero at UI 1.0 and 1.4, may force a layout pass); `ResonanceTierMark` tier 4 (reuse tier 3 art); about 20 en.csv keys; docs (975 appears in AGENTS, DUST_ECONOMY, the design master, contract A15). Respec and `CalculateSpentDust` sum node costs (no change); `GridProgress` is a node-ID list (no migration). Each effect must be a Story perk (ResonanceReaderCoverageTests scans FighterSim).

Ideas **[suggestion]**, all reusing existing primitives:
- Joan, *Banner of Orleans* (Unstoppable Crusade + Shield of Orleans): Righteous Smite's ground wave leaves a short radiant strip (lingering-zone primitive).
- Einstein, *Unified Field* (Event Horizon + Quantum Entanglement): Relativity Warp's arrival detonates his Rift (Rift collapse seam).
- Leonardo, *Codex Atlanticus* (Master Stroke + Clockwork Overdrive): synced turret bolts pierce; every construct gains one charge.
- Lincoln, *Emancipation* (Executive Order + Kinetic Splitting): Rail Charge leaves a ground wave behind (ground-wave primitive).
- Cleopatra, *Dynasty's Curse* (Asp's Bite + Quicksand Grip): a Venom kill spreads Venom to nearby mobs.
- Tesla, *World System* (Resonant Overdrive + Lorentz Attraction): the discharge chains to coils with charges left and refunds a pip.
- Shakespeare, *Curtain Call* (Macbeth's Curse + Midsummer Glide): Midsummer Gust leaves a lingering windbox that pushes mobs toward hazards (pairs with ruling 7d).
- Mozart, *Symphony No. 41* (Virtuoso Drift + Requiem Crescendo): different Specials within 6 s stack a damage lane.
- Tubman, *Freedom Line* (Safe Passage + North Star Ward): a Foresight counter refunds a pip and grants a Resonance Barrier.
Tests: 9 kit tests + 5 content or layout additions, about **+14 net**, ~15 edited in place.

### Risks
A 375 node at the bottom of a grid laid out for nine chips; the Fit tests at 1.4 are the gate. With rank plus capstone, strong players finish the grid around Levels 8-10.

## 4. Momentum regen 9 s, both modes (10a+)

### Current
`MomentumUnitsPerPip` 7200, `MomentumRegenFramesPerPip` 720, `MomentumRegenUnitsPerFrame` 10 (BasicComboRules.cs:817-831); gains 360/360/1440 (:833-837); sim `FighterMomentumRules.Regen` adds 10 per frame (FighterMomentumRules.cs:48); Story float `RegenMomentum` uses `60 / MomentumRegenFramesPerPip` (PlayerController.Momentum.cs:98-103); bots PlaytestWorld.cs:234.

### Exact solution (integer, deterministic)
7200 / 540 = 13.33 is the trap. **Change the unit, not the rate**: `MomentumUnitsPerPip = 5400`, `MomentumRegenFramesPerPip = 540`, `RegenUnitsPerFrame = 10` unchanged, gains `270 / 270 / 1080` (exactly 0.05, 0.05, 0.2 pip), `MaxUnits` 21,600, `StartUnits` 10,800. Cost, HUD (`units / UnitsPerPip`), CPU (`SpecialCostUnits`), Mirror (`pips * UnitsPerPip`) and `LingeringKitRules` events all derive from the constants. Story needs no code change (60/540 = 0.111 pip/s; the grid's `MomentumRegenMultiplier` 1.1 as before). Component 310 stays an int (max 21,600). Every Fighter hash moves; snapshot size and protocol unchanged (old and new builds would desync on LAN, de-scoped).

### Tests touched (literals found)
`MeterDefyRulesTests` (:19, :24, :54-56, :70), `FighterMomentumGaugeTests` (:64-66, :109, "a pip per 720" comments), `StoryMomentumGaugeTests` (:118-133, use the constant), `FighterLingeringKitRulesTests` (:138-157: 1440 / 360), `FighterHitConfirmTests` (:323), `FighterAirAttackTests` (:131), `LingeringKitRulesTests` (:80-84), `OwnerHitSeamTests` (:99-101 `/7200f`), `StoryHitConfirmTests` (:259, 318, 357, 365, 647), `StoryAirAttackTests` (:171), `FighterHudSceneTests` (:436, 467), `CpuVerbPolicyTests` (:141, 7199), `FighterLockoutRefusalRulesTests` (:290-293), `PlaytestCombatStatsTests` (:334, `1f/720f`), `RollbackReadinessTests` (:675, spam-script thresholds; more pips may need re-tuning), plus comments in CpuDecisionObservation.cs:73, FighterSimulationComponents.cs:771, FighterSimulationSystems.cs:2501, LingeringKitRules.cs:174, FighterHudModel.cs:307. About 16 files edited in place, **+2 new pins** (5400 = 540 x 10; gains stay 0.05 and 0.2 of a pip). Replace literals with the constants as you go.
Effects: regen 33 % faster, so more Specials a minute for players, the CPU and bots (bots 4.93 a minute now); rerun the rollback re-measure (docs/PERFORMANCE_BASELINE.md). Docs: "1 pip per 12 s" in AGENTS, CLAUDE, ledger, contract A15, design master; check `systems_card_momentum_*` copy in en.csv for a stated 12 s.

## 5. Target curve and flags (4c+)

- Numbers (ruling): Easy 0.90 on every level; Normal linear 0.75 (L1) to 0.50 (L15), `0.75 - (L-1) * 0.25/14`; Hard unset. Levels 6, 7 and 10 carry bot-gap notes (Pompeii vault and Nassau quay stalls and the Level 10 recess, BOT_TABLES stuck list).
- **One canonical home**: `tools/playtest/target_curve.json` (levels 1-15; `easy`, `normal`, `hard: null`, `bot_gap_notes`). The ledger row `TARGET-COMPLETION-CURVE` names the file and never copies the numbers.
- Script: reports already carry `level`, `difficulty`, `level_completed` (PlaytestRunner.cs:485-489; PlaytestTelemetry.cs:1781) and `reports()` yields them (compare_runs.py:95). Add `curve_rows(tag)` and a `--curve` flag: group by (level, difficulty); print n, completions, rate, target and a flag from an 80 % Wilson interval (cells are 9 runs, so raw gaps are noise): `LOW` if the interval's upper bound is under target, `HIGH` if its lower bound is over, else `ok`; `gap` note on L6, 7, 10; `-` for Hard. About 60 lines; the Python tooling has no tests.
- Data now (Normal completions of 9): L1 7, L2 3, L3 8, L4 3, L5 2, L6 4, L7 1, L8 7, L9 1, L10 0, L11 7, L12 0, L13 6, L14 0, L15 0 against targets 75 falling to 50 %. Expect LOW at L2, 4, 5, 7, 9, 10, 12, 14, 15; Easy LOW at L3 (6/9) and L6, L7 (gap notes). Observation only; no tuning (ruling).

## 6. Open questions with recommended answers
1. Which levels pay rank? **Levels 1-15** (Level 0 pays no dust).
2. Level 1 (untimed)? **Damage-only rank**, still able to reach S.
3. Speed source? **v1: Integrity at the PreBoss gate** (persisted, no new clock); option B only if the user wants a truly separate clock. The decision text is in mild tension with v1.
4. Difficulty fairness? **Letters difficulty-normalised, dust Easy x0.5**; confirm with the user (alternative: all pay 50).
5. Count status or periodic ticks? **No** (hits and hazard contacts only).
6. Where shown? **Results panel only** (`RankLine` plus bonus line); a hub ledger line is optional later.
7. Schedule derivation? **None needed; record a ledger no-op** (superset reads).
8. First-use tooltip for Special 1 on Level 1? **Yes, one key.**
9. Capstone shape? **Tier 4, Major type, 375, prerequisite two Majors.**
10. Regen units? **5400 per pip** (above).

## 7. Aggregate
Estimated test delta **about +50** (rank +30, schedule +4, capstone +14, regen +2, curve 0) with roughly 70 existing assertions edited, from the 4028 baseline. Every Fighter hash moves (regen). Ledger rows to add: `USER-LEVEL-RANK-DUST`, `PROVISIONAL-LEVEL-RANK-RULES`, `USER-LEGACY-SCHEDULE-SHIFT`, `USER-GRID-CAPSTONE`, `USER-MOMENTUM-REGEN-9S`, `TARGET-COMPLETION-CURVE`.