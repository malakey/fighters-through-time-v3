# GR-CROWD handoff (gameplay review, rulings 1a, 1a+, 1b, 1b+, 4c, 4c+)

**Nobody has played any of this.** Every claim below is a headless suite or a hand check of a script on fabricated reports. No bot batch ran: the orchestrator owns those (plan §5). No level row exists yet, so no real level spawns a swarm.

## What shipped

**Slots (1a+).** `StoryEngagementRules.MeleeSlotsTotal` Easy 2 / Normal 3 / Hard 4; `MeleeSlotsPerSide` 1 / 2 / 2. The director reads them unchanged. Waiters are not redesigned and ranged permits stay 1 / 2 / 3.

**The fodder mob (1b, 1b+, Q4).** `resources/Enemies/chrono_swarmling.tres`, display name "Chrono Swarmling", key `enemy_chrono_swarmling_name`, `content_manifest.csv` row after `unbound_eraser`. Standard tier, so it uses the `standard_enemy` pool and is grabbable.
- It has **no ability resource**: §1.11's "12 / 6 / 14 scalar melee" is the legacy scalar path `chrono_slasher` also uses. It is a melee-slot candidate through the synthesized MeleeStrike.
- New `EnemyData` exports: `DustWeightZero`, `SpriteScaleFactor` (default 1), `TintAuthoredFrames` (see deviations).
- `LevelRewardDirectory.WeightForEnemy` returns 0 for `DustWeightZero`. `RewardAllocator` is unchanged. Its sources exist in the ledger and pay 0, so a kill spawns no pickup.
- `RetroSpriteScaleNormalizer.FigureScale` multiplies the sprite's scale and keeps the feet on the origin. `EnemyController.ApplyPresentation` sets it on every `ApplyData`, so a pooled body handed back as another mob returns to 1.

**Rules for the level owners.** `FTT.Environment.SwarmWaveRules` (pure): `RowMinMobs` 6, `RowMaxMobs` 10, `RowDefaultMobs` 8, `RowLargeLockMobs` 10, `LargeLockWidthPixels` 2800, `TriggerAliveAtMost` 2, `MaxRowsPerLevel` 2, `WarmUpBonus` 6, `MinimumWarmOnSwarmLevel` 16; `IsLevelExcluded`, `RowMobsForLockWidth`, `IsSwarmRow`, `ValidateRows`, `ValidateWaveTrigger`, `ValidateWarmUp`.

**What a level owner authors.** All four, or `SwarmWaveContentTests` fails:
1. A manifest row whose whole table is `chrono_swarmling`, 6–10 entries, authored at the Hard truth (`RowMobsForLockWidth`). Keep the per-row `@n` convention the other rows use.
2. An `EncounterLockWave` with the same `RowID` and table, `Trigger = EncounterWaveTrigger.WhenAliveAtMost`, `AliveAtMost = SwarmWaveRules.TriggerAliveAtMost`.
3. The pool config's `standard_enemy` warm count raised by `WarmUpBonus`, at least 16 and within the existing 30-combatant cap.
4. No row on Levels 0, 12, 13 or 14, and at most two on a level. The sweep does not check the "largest locks" half of 1b++: that is the owner's call (candidates L9 beach, L11 line).

A row is read as a swarm from its data, not its name, so `level_NN.swarm1` is a convention only.

**Runner options.** `--slot-table=legacy|current` and `--swarm=on|off` (also `1` / `0`); a bad value exits 2 like `--story-meter-scale`. Neither is in the run name, so an A/B uses two tags; the report carries `slot_table` and `swarm_waves`.
- `PlaytestOptions.ApplyCrowdSeams()` sets `StoryEngagementRules.SlotTableOverride` (the old numbers live in `PlaytestSlotTables.Legacy`, under Diagnostics) and `EncounterLock.SwarmWavesOffForDiagnostics`.
- Off marks a swarm wave spawned with 0 planned when its lock arms, so the lock plays as if the row were not authored. Later waves do not wait on it. Dust is unaffected, because fodder pays 0.

**Telemetry.**
- `mob_engagement`: `slot_idle_with_waiters_frames`, `melee_candidates_max`, `melee_candidates_mean`, `sums.melee_candidates`. An idle frame is an engaged frame with holders below the total and at least one `IsMeleeSlotCandidate` on the hero. A per-side cap or a waiter's rest can be what holds that slot.
- New top-level `swarms` block (`SwarmTracker`, one record per wave per lock instance): `count`, `cleared`, `log` rows with `lock`, `row`, `planned`, `spawn_frame`, `clear_frame`, `frames_to_clear`, `peak_living`, `hero_damage`, `frames`, `holders_sum`, `attacking_near_sum`, `melee_candidates_sum`.
- `PLAYTEST_RESULT` `encounters` gains `slot_idle_f`, `melee_cand_mean`, `swarms`; `run_playtests.ps1`'s CSV gains the same three columns.
- Reader seams: `EncounterLock.IsSwarmWave`, `ReadSpawnedSwarmWaves`, `SwarmWaveReading`; `PlaytestWorld.IsMeleeSlotCandidate`.

**Target curve (4c, 4c+).** `tools/playtest/target_curve.json` (Easy 0.90; Normal `0.75 − (L−1)·0.25/14`, written to four places; Hard null; gap notes on Levels 6, 7, 10). `compare_runs.py --curve <tags>` prints only the curve: `play` bot, 80 % Wilson interval, `low` or `high` only when the interval misses the target, Hard `-`, Levels 1–4 as their own table. Without `--curve`, the `crowd` table gains `table`, `cand_mean`, `slot_idle%`, `slot_idle_f`, and a new `swarms` table follows it. The script ran on fabricated reports in a scratch folder and both modes printed what they should; it has not read a real batch.

## Provisional numbers (the user ruled none of these)

Under `PROVISIONAL-FODDER-STATS`:
- From §1.11 (plan's choice): HP 10, MoveSpeed 5.2, Weight 0.6, AttackDamage 5, AttackCooldown 2.5, 12 / 6 / 14, ChronalDustDrop 1, rows 8 / 10 at 2800 px, trigger 2, warm +6.
- From dossier B: AttackRange 1.2, StunResistance 0.
- **Mine:** `PlaceholderTint` (0.62, 0.86, 1), `SpriteScaleFactor` 0.75, AttackKnockback 1.5, and AggroRadius 480 / DeAggroRadius 720 / JumpForce 10 / reaction delay 30–45 copied from `chrono_slasher`. The hurtbox is the standard scene's, so a smaller sprite keeps the full-size hit area.

## Test delta

**Declared +17; actual +16** (`[TestCase]` count 4028 → 4044): slots 2 (`StoryEngagementRulesTests`), swarm 10 (new `SwarmWaveContentTests`), telemetry 4 (`PlaytestEncounterTelemetryTests`). The plan's split was 2 / 12 / 3. The curve file's case went to the telemetry suite, and I did not add a filler case to reach the declared number.

Filtered run at this head, with the new key compiled locally: `Passed! - Failed: 0, Passed: 962, Skipped: 0, Total: 962`. No full run: the orchestrator owns those.

Changed in place, no count change (each pinned the old "one per side on Normal"; the intent moved to Easy, which now has that cap):
- `StoryEngagementRulesTests`: `TheSlotTotalsAreTheUsersRuling…`, `GrantsRespectTheTotalAndPerSideCaps…`, `TheHandOffTestFreesTheHoldersSide…`.
- `StoryEngagementDirectorTests`: `ThreeSlashersOnOneSide…`, `TheSlotRotatesRoundRobin…`, `TheEngageBlinkIsGranted…`, `TimeFreezeLeavesRoles…`, and `AWaiterCutOffByAWall…` (the wall also moves from 220 to 250, for Easy's 200 px rank 0) all run on Easy. `OnNormalOneMobAttacksFromEachSideAtOnce` became `EasyTakesOneMobFromEachSideAndNormalTakesThreeLopsided`, with a real-frames Normal half (3 holders, 2 on a side).
- `EncounterLockTests`: `AnEliteListedAfterAStandard…` has three members (the elite still outranks two standards that tick first); `ARevertedMemberLeaves…` expects 3 holders.
- `EncounterMemberBehaviourTests.AStruckWaiterRetaliates…`: both slashers stand east, because Easy's two slots would otherwise hold one each side.
- `PlaytestEncounterTelemetryTests.TheTelemetryReportsTheLockAndTheCrowdTheDirectorShapes`: `slot_total` 3 and three slashers on one side (planned, killed and seen become 3).
- `EnemyRosterContentTests` (30 enemies, 17 melee primaries), `EnemyRetroSpriteAssetTests` (30 enemies).

Seen failing first: 16 of 40 failed against stubs in the first filtered run, every new case that reads new behaviour plus the changed telemetry and table cases. The 11 other in-place pins went red once the table changed (13 failures of 358 in the next run, with the two roster counts).

The two sweeps pass today with no rows to read. To check they bite, I added a temporary eight-swarmling row to Level 2's manifest and reverted it: `EverySwarmLevelWarms…` failed with `Level 2 warms 10 standard_enemy, needs at least 16 | Level 2: swarm row 'level_02.swarm1' has no lock wave`, so it does load the real scene. Its check of a real wave's trigger has still not met a real wave; GR-PILOT's Level 2 is the first.

**Not run, read only:** a real fodder kill spawning no pickup. `StoryDropSystem.SpawnKillDust` returns on `amount <= 0`, and `AFodderRowAddsNoDust…` proves the ledger pays 0, but the kill itself needs a level with a row, because the ledger compiles from the real manifest.

Runner check, in a real process: `--slot-table=legacy --swarm=off` on Level 2 Easy for 20 s wrote `slot_table: legacy`, `swarm_waves: false`, `slot_total: 1`; `--swarm=maybe` exited 2.

## Hash, save and protocol effects

No Fighter hash moves: nothing under `scripts/FighterSim/` changed. No save field, schema change, protocol change or Klotho ID. Story crowd behaviour changes everywhere, because more mobs now commit at once.

## Deviations from the plan

1. **`EnemyData.TintAuthoredFrames` (added, not in the plan).** §1.11 asks for a tint on `chrono_slasher_frames`, but `EnemyController.ApplyPresentation` forced white on any mob with authored frames. The new flag keeps `PlaceholderTint` for a mob that borrows a sheet; every other mob reads as before.
2. **`scripts/Environment/SwarmWaveRules.cs` (added).** One home for the swarm numbers, so they are not repeated in tests or level code.
3. **`EncounterLock.cs` gained a small hunk** (the seam, `IsSwarmWave`, the reading, `Member.WaveIndex`). The ownership matrix does not name the file; `--swarm` needed it.
4. **The task line lists "abilities"; none was authored**, following §1.11's scalar melee. `EnemyRosterContentTests`' floor of 122 still holds.

## What did not ship

- No level rows, by design. `--swarm=on` and `off` read the same until the level owners add rows.
- No swarm-specific art: the look is a tinted, smaller slasher.
- `trace.py` has no swarm line; its `mob_engagement` header may show the new scalars.
- `slot_idle_with_waiters_frames` does not separate a per-side block from a rest block.

## What the docs owe (GR-DOCS)

- Ledger: `USER-ENEMY-TURN-TAKING` and `PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES` amended (2/3/4, per side 1/2/2); new `USER-SWARM-WAVES`, `PROVISIONAL-FODDER-STATS` (with the list above), `TARGET-COMPLETION-CURVE` (names the file, never the numbers).
- AGENTS.md: the turn-taking bullet ("Easy 1 / Normal 2 / Hard 3", per side), the `EnemyData` and roster counts (29 → 30 enemy resources), the `mob_engagement` and `swarms` report text.
- CLAUDE.md: runner options `--slot-table`, `--swarm`; `compare_runs.py --curve`; the new CSV columns; the crowd text.
- Design master and mirror: the turn-taking numbers.

## For the orchestrator

- `localization/en.en.translation` must be regenerated for `enemy_chrono_swarmling_name`. It is not committed here, so `EnemyRosterContentTests`' compiled-key sweep fails on this branch until you compile it.
- Expect a merge conflict in `StoryEngagementRulesTests`, `EnemyRosterContentTests` or the telemetry suite only if another workstream edited the same pins; the expected total after this merge is 4028 + the earlier merges + 16.
- GR-HAZ and GR-TRAITS may now add counters to `PlaytestTelemetry` and `compare_runs.py`. GR-TRAITS appends to `EnemyData.cs`; my hunks sit after `ItemDropChance` and after `PlaceholderTint`.
- Bot batches from before and after this are not comparable. Use `--slot-table=legacy --swarm=off` for the same-build control (`gra11lg`).
