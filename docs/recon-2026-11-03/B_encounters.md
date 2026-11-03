# B. Story encounter changes (recon at 43d54db6; verified against code, not docs)

Scope: 1a+ slots 2/3/4 (per side 1/2/2), 1b swarm waves, 2b+ traits Armoured / Catcher / Anti-air, 7d hazards vs launched or thrown mobs, bot measurement. Rulings in DECISIONS_gameplay_review.md are not reopened. Nothing was run; "inferred" marks what I did not trace.

## 0. Finding that bears on the plan

Raising slots probably will not raise pressure much, because the limiter is the supply of melee mobs, not the slot rules.
- Reward manifests give Levels 2-4 only 8-10 mobs each, in 2-3 waves of 2-5, and about half are ranged. Level 2: room1 `laser_archer,chrono_slasher,laser_archer`, room2 `slasher,archer,slasher`, room3 `archer,slasher` (resources/Content/reward_manifests/level_02_rewards.tres). A Hard wave holds 1-3 melee mobs, so 4 slots cannot fill.
- A mob competes for a slot only if `IsMeleeSlotCandidate`: Chase state and a next attack classed MeleeSlot (scripts/Enemies/EnemyController.Engagement.cs:142-148). Ranged mobs never count.
- A holder keeps its slot through cooldown, stun, grab and thrown flight (Engagement.cs:25-32), so "holders" is roughly the living melee count. Bot data matches: holders mean 0.72 Normal / 1.07 Hard with 2 / 3 slots (BOT_TABLES_43d54db6.md:515,521).
- Rest frames (Hard 20, Normal 45) and the stale release (120) cannot bind when fewer waiters than slots exist. `frames_gated_melee_over_slots` and `holders_over_slots` are 0 (BOT_TABLES:568), so the rules are not over-gating.
- Inferred: slot rules limit pressure only inside big crowds. Prove with the two counters in section 6.

Swarm waves (1b) are therefore the effective lever for 1a+. A 10-mob fodder wave is the first thing that can fill 4 Hard slots.

## 1. Slot totals 2 / 3 / 4, per side 1 / 2 / 2

Current: `MeleeSlotsTotal` 1/2/3 (scripts/Combat/StoryEngagementRules.cs:59-63), `MeleeSlotsPerSide` 1/1/2 (:69-72).

Change (rulebook, one file): Total Easy 2 / Normal 3 / Hard 4; PerSide Easy 1 / Normal 2 / Hard 2.
- Easy 2 total with 1 per side needs one attacker on each side; all mobs on one side still gives 1.
- Normal 3 / 2 is lopsided by design (2+1).
- Hard 4 / 2 has zero slack (`2*perSide >= total` holds exactly; StoryEngagementRulesTests:44).
- Ranged permits (1/2/3, :79-83) not in the ruling; leave. Easy already runs 3711 frames per 135 runs over permits (BOT_TABLES:512).

Is the ring gap a real behaviour issue? Partly.
- The front waiter median is 177 px Normal / 220 Hard against rank-0 gaps of 170 / 150 (BOT_TABLES:519,525), so the front of the ring is where the rule puts it.
- The 309 px Normal figure is the median over all ringing waiters. Rank n stands at 170 + 64n (StoryEngagementRules.cs:220-221): five waiters on one side reach 426 px.
- Members walk in from lock-wide spawn points. An engaged lock wakes every member into Chase at once over areas 1280-3200 px wide (EncounterLock.cs:445-470); approach speed is min(0.8 x own speed, 0.9 x hero speed) (:228-233).
- Far-waiter samples (>600 px) average 952 frames per Normal run, 259 on Hard (BOT_TABLES:519,525).
- Causes (inferred, not traced per mob): spawn distance, rank stacking, and ledge or wall blocked steps (Engagement.cs:387-394).
- Open risk: more slots do not pull waiters closer. Re-measure `mob_engagement` after. Do not redesign waiters (user declined pokes).

Per-file changes:
- StoryEngagementRules.cs: both tables and the doc comments that call 1/2/3 "the user's ruling".
- StoryEngagementDirector.cs: none (it reads the rules, :107-122). PlaytestTelemetry.cs:1233 reads `MeleeSlotsTotal`: none.
- Tests pinning old numbers: StoryEngagementRulesTests:28-35 (6 asserts) and PlaytestEncounterTelemetryTests:435 (`slot_total` 2 becomes 3). EncounterLockTests:751 and EncounterMemberBehaviourTests:349 are symbolic. StoryEngagementDirectorTests:139-143 are symbolic but their harnesses assume up to 3 holders; check Hard with 4.
- Docs: AGENTS.md (the turn-taking bullet and the turn-taking list "Easy 1 / Normal 2 / Hard 3", several places), CLAUDE.md crowd text, GU_F.md, GAMEPLAY_UPDATE_2026-10-12_PLAN, ledger USER-ENEMY-TURN-TAKING marked amended.
- Test delta: +2 new (a Hard 4-holder grant at 2 per side, a Normal 3-slot per-side case), 4 in place.

## 2. Swarm waves (1b)

### 2.1 Mechanics, verified
- A lock wave is a manifest row `level_NN.xxx:enemyA,enemyB,...` (LevelRewardManifest.cs:45-57, `ParseWaves` :94-110) and an `EncounterLockWave` with the same RowID and enemy IDs in order (EncounterLockSpec.cs:26-50). EncounterLockContentTests (:27, :188-202) pins the match.
- The lock spawns through `EnemyFactory.Spawn` into the shared `standard_enemy` pool (EncounterLock.cs:465, EnemyFactory.cs:44-50).
- Triggers already available: `OnBuild`, `OnEngage`, `WhenAliveAtMost(n)`, `AfterPreviousCleared`.
- Spawn tears are un-pooled nodes, one per spawn, 0.5 s (EncounterSpawnTear.cs:14; EncounterLock.cs:469). Ten at once is fine; stagger if it reads as noise.
- Difficulty prefix: Easy x0.7 floor, Normal x1.0, Hard x1.25, never above the authored length (StoryDifficultyTuning.cs:27-32, 63-71). Author the table at the Hard truth (10): Easy 7, Normal 10, Hard 10.

### 2.2 The mob (proposal, `chrono_swarmling`, name is the user's call)
- Tier Standard, so it uses `StandardEnemy.tscn` and the `standard_enemy` pool and stays grabbable (`IsGrabbable` requires Standard, EnemyController.cs:150-155). That makes it a good thrown projectile and hazard victim.
- Stats: MaxHP 8-12 (the Story basic floor is 6 per hit against standards, so hit 1 plus hit 2 kills), MoveSpeed ~5.2, Weight 0.6, StunResistance 0, AttackRange 1.2, AttackDamage 5, AttackCooldown 2.5, telegraph 12 / active 6 / recovery 14, MeleeStrike primary, ChronalDustDrop 1.
- For comparison: chrono_slasher 40 HP dmg 10, hologram_drone 30 HP (flying). Keep it no faster than the slasher; 17 roster mobs already outrun Lincoln and waiters cap at 0.9 x hero speed.

### 2.3 Files per mob
- resources/Enemies/<id>.tres (a scalar-melee primary works with no new ability resource; or add resources/Enemies/Abilities/<id>/).
- resources/Content/content_manifest.csv Enemy row (as line 89 for chrono_slasher); localization/en.csv `enemy_<id>_name`; run `--import` and commit the regenerated en.en.translation.
- Sprites: the enemy pipeline is tools/build_enemy_sprite_resources.py (6 rows: idle, patrol, attack, elite_attack, hitstun, death) from art staged outside the repo. The retro v2 pipeline (tools/retro_sprites) covers only the playable roster. Placeholder plan: reuse chrono_slasher_frames.tres with a distinct `PlaceholderTint` and a smaller scale. `EnemyData` has no scale export, so add a small one (`RetroSpriteScaleNormalizer` scales to the sheet's idle height). Real art waits for Package 10.
- The grab pass needs a `hitstun` animation (ShowHitstunFrame, EnemyController.cs:216-224); the reused atlas has it.
- Roster tests: EnemyRosterContentTests asserts at least 122 resources (:132) and standard dust 1-2 (:175), so author ChronalDustDrop 1.

### 2.4 Dust: does a kill still pay, and how does a 10-mob wave split it?
- Kill dust is issued per kill event from the next unissued source for that enemy ID in the ledger queue, whatever killed it (StoryDropSystem.cs:90; LevelRewardDirectory.cs:253-267). A hazard or wall-impact kill pays like any kill.
- Weights: standard 1, elite 5 (RewardAllocator.cs:46-50; LevelRewardDirectory.cs:230-240). Most pools are 15 (L5 55, L12 25, L13-15 35, L1 25).
- Naive weight 1: Level 2 has 8 mobs; adding 10 fodder gives 18 sources over 15 dust. Fifteen get 1, three get 0, and every existing mob falls from about 2 to 1, with up to 10 dust pickups per swarm.
- Recommended: fodder weight 0. `Allocate` keeps weight-0 sources at 0 and leaves them out of the weighted pass (RewardAllocator.cs:61-91), so the pool, the level total and the other mobs' shares are unchanged, and a zero award spawns no pickup (StoryDropSystem.cs:91). Implement as a branch in `WeightForEnemy` (LevelRewardDirectory.cs:230) keyed on an `EnemyData` flag.
- Alternative: a `ScriptedRequiredSources` row `level_NN.swarm1:3` carved out of the pool (LevelRewardManifest.cs:59-65), paid on wave clear. The user should pick; I recommend weight 0 first.
- Unchecked: that DustEconomyTests sums by pool and not by source count.

### 2.5 Where it suits
- Suits: ground areas at least 1600 px wide with no floor hazards. Themes: Orleans siege line (L2), Bastille courtyard (L4, rioters are already crowd mobs), Titanic E-deck (L5), Hakata beach (L9), Gettysburg line (L11), Florence workshop (L1).
- Avoid: L12 / L13 low gravity, L14 corridor grids, L10 audience stillness, boss-adjacent locks, Level 0.
- Level 2's controller comment assumes "3 or fewer live standards" against a warm pool of 10 (Level02Controller.cs ~488-492); a swarm of 10 plus 2-3 others means about 13 live.

### 2.6 Level 1 special case
- Florence does not extend StoryLevelControllerBase. It builds four standalone locks (Level01Controller.cs:960-1015) from public static tables (`Room3Spawns` :918-926); the print-shop lock also needs the gear puzzle.
- Level 1 uses resources/Pools/florence_pool_config.tres (standard_enemy warm 12, max 40), not a level_NN config; the level list in ScenePoolConfigTests covers levels 2-15.
- Manifest `level_01.room3@7` already holds 7 mobs. Add a row `level_01.swarm1@N`, an `EncounterLockWave` (in the Piazza or workshop lock) and a table in Level01Controller.
- The workshop (spike strip 8990-9090, steam geyser, crumbling scaffolds) is the natural first 7d showcase.

### 2.7 Pool budgets and performance
- Every level config: standard_enemy WarmUp 10-18, MaxCapacity 40, OverflowPolicy 1 = RecycleOldest (PoolManager.cs:12-16). Past 40 live standards the oldest live mob is recycled; a swarm of 10 plus others is far below.
- A swarm above the warm count instantiates full CharacterBody scenes at runtime (hitch). Raise standard_enemy WarmUp by about 6 on swarm levels.
- Test cap: `MaxWarmCombatants = 30` for standard + elite warm (ScenePoolConfigTests.cs:47). L14 is 18 + 5, L15 18 + 6, so +6 reaches 29 / 30 and +8 fails. Raise the cap or limit the bump.
- The "60 AnimatedSprite2D" budget is a warm-count proxy only; there is no live counter. About 13 mobs at one AnimatedSprite2D each stays well under 60. Freeze and rewind sweeps are O(n) over the Enemies group.

### 2.8 Tests
- In place: the 14 LevelNNContentTests pin authored encounter counts; EncounterLockContentTests, RewardManifestTests, ScenePoolConfigTests, EnemyRosterContentTests sweep rows.
- New: SwarmWaveContentTests (at most 2 swarm rows a level, 6-10 mobs, weight 0, manifest match), a fodder roster test, a dust-unchanged pin, a pure rule case. About +12 new, about 20 in place.

## 3. Traits (2b+)

Plumbing: a pure `EnemyTraitRules` in scripts/Combat/ and an `EnemyData.Trait` enum export (None, Armoured, Catcher, AntiAir). Traits sit on Standard-tier mobs only: the six unplaced elites stay unplaced, and elites are grab-immune, which would leave Armoured with no answer.

### Armoured (reduced damage until thrown)
- Hook: `ResolveIntakeScale` (EnemyController.cs ~2990-3046) scales player hits beside S4 and Punish. The throw never passes it: `ResolveStoryThrow` calls `victim.TakeDamage(int)` (PlayerController.cs:3408), which goes to the private `TakeDamage(damage, null)` (EnemyController.cs:2006, 2026-2052). Bowling (:314) and the wall impact (:915) use that same path, so they stay unreduced.
- Do not reuse `FrontalDamageReduction` (:2033, :2055-2060): it sits in the private path and would reduce the throw as well.
- Strip on `BeginHeld` or `LaunchThrown` (:179, :245) by clearing an `_armourIntact` flag. Ultimate (`ignoreDefenses`) bypasses it like the shield.
- Stun and knockback still apply, so a string then grab then throw is the intended answer.
- Tell with existing presentation: keep the gold hyper-armour glow (`RefreshArmorPresentation`) on while intact; it drops on the strip.
- Carrier: `shock_shield_legionnaire` (Standard, HP 55, `FrontalDamageReduction` 0.5, Level 6 x6) is the fit. Convert or keep both.
- Kit risk: every hero has the Block+Attack grab chord, but a player can switch the chord off (`DirectOrigin`, PlayerController.cs:3331-3336) and leave the direct bind unbound. Keep the reduction soft (x0.5-0.6), never immunity.

### Catcher (counters a basic string, loses to a throw or Special)
- Hook: in `OnHurtboxHit` (:2742-2870) on a Basic-class string hit (`IsBasicStringHitbox`, :2826-2827) the Catcher takes damage but skips stun and knockback on hit 1-2, then commits a counter swing on the second string hit in a chain. Reuse the Armored Recovery machinery that already commits an immediate counter (`BeginArmoredRecovery` :1877, `_armoredAnswerPending`) via a faster standard-poise trip fed only by basic string hits (`ChargesStandardPoise`, StoryEnemyCombatRules.cs:234; budget :188; recovery :202).
- Specials and Ultimates stun normally, because the skip is keyed to basic string hits; a grab ends any stun cleanly (`BeginHeld` :186-194).
- The answer is a retaliation: `BeginAttack` and `CommitArmoredAnswer` are never gated by the director (Engagement.cs:11-22).
- Tell: a Guard-Crush diamond glyph on a 12-frame telegraph (`TelegraphGlyph` shapes, EnemyAbilityExecutor.cs:854-857; tint plus shape, so not colour-only), after a braced pose (hitstun frame 0).
- Carriers: `cyber_guard` (Standard, HP 45, Level 1 x6, range 2.0); `plasma_spear_ward` (L8).
- Risk: it punishes every hero's default string. Keep the counter blockable and short, with a 2-3 s cooldown.

### Anti-air (punishes jump-ins, weak to grounded pressure)
- Hook: a free reactive action beside `TryReactivePhaseSkip` (never gated by the director). Condition: hero Airborne and descending within reach, cooldown up. Fires a rising MeleeStrike.
- Weak to grounded pressure: no special armour on the ground, a long recovery (about 40 frames) after a missed anti-air, StunResistance 0.
- No-required-hero: every kit has grounded basics and the Down-Air; movement abilities are punished only if they end in reach. Add a content check that no level forces a jump into an anti-air mob's cover.
- Tell: an upward stance on the `attack` row plus a diamond glyph, shown only when the hero is airborne in range.
- Carriers: `plasma_spear_ward` (L8, range 7.5, Standard) or `holo_page` (L10). `laser_archer` and other ranged mobs are poor fits.
- Most new code of the three and the likeliest to feel unfair to flying movement abilities.

### Rules touched
No change to EnemyStaggerRules for Armoured; Catcher adds one rule and constants to StoryEnemyCombatRules; Anti-air adds none. Add a content test that no required objective (breakable, gear plate) sits behind a trait mob that only a throw can beat.

Tests: EnemyTraitRulesTests (pure, about 8), EnemyControllerTraitTests (armoured reduction, strip on throw, bowling / wall / hazard unreduced, catcher answer, Special and grab counters, anti-air fire and recovery, about 12), a content sweep (about 3). About +23.

## 4. Hazards hit launched or thrown mobs (7d)

### 4.1 Current state: no Story hazard touches a mob
`EnemyController` has no hazard code (grep finds only comments). Every damaging area is hero-only by mask and by type check.

| Hazard | Collision / targeting | Source |
|---|---|---|
| StoryCyclicHazard (L2, L3, L7, L10, L11, L14 x14) | Area2D, `GetOverlappingBodies`, `body is PlayerController`, once per cycle (`_hitThisCycle`), blockable | StoryCyclicHazard.cs:44-70 |
| Spikes (`BuildHazardSpikes`) | Trigger layer, mask Player, `BodyEntered`, Player-only | StoryLevelControllerBase.cs:1061-1090 |
| SearchlightZone (L4, L9) | mask Player, strike plus meter drain | SearchlightZone.cs:54-55, 153 |
| StageHazard (L1 geyser etc.) | `DamageZone` overlaps, DPS accumulator, hero only | StageHazard.cs:70-90 |
| RisingWaterZone (L5 flood, drown) | mask Player, periodic drown tick | RisingWaterZone.cs:62-63, 126 |
| FallingDebris (Collapse Tremor) | hero only; tremor "never touches enemies" | FallingDebris.cs:139 |
| ChronalExtractor idle discharge | `_hazardArea` overlaps, Player-only, drains meter | ChronalExtractor.cs:150-175 |
| Escape fronts (L6 lava, L15 firestorm) | `EscapeSequenceController.CatchPlayer` | EscapeSequenceController.cs:140-150 |
| L7 sea haul-out, rift snap | Player-only | Level07Controller.cs:675-692; ChronalRiftZone.cs:64 |
| StoryKillBoundary | mask Player; none authored (VERIFY-STORY-PITS) | StoryKillBoundary.cs:41-56 |

Trigger Areas mask Player (layer 1); mobs are on the Enemy layer (2), so a mob never overlaps them. A mask change is needed and `CollisionLayerTests` changes with it.

### 4.2 Detecting "launched or thrown"
Existing runtime-only latches:
- `_thrownFlight` (`IsThrownFlight`, EnemyController.cs:83, 161, 245-256): set by `LaunchThrown`.
- `_wallBounceArmed` (:139, `IsWallBounceArmed` :816): armed by every player launch (`ArmsWallBounce`, :864-868) and every throw (:261-264); cleared at touchdown, stun end, death. The closest existing statement of "launched or thrown".
- `_finisherFlight` (`IsInFinisherFlight` :807): string finisher on Standard / Elite.
Proposed predicate: `IsHazardVulnerable => (_thrownFlight || _wallBounceArmed || _finisherFlight) && !IsEncounterDormant && CurrentState != Dead`. A walking, merely stunned or Defy-staggered mob is not vulnerable. Edge: a finisher slide stays flagged until the stun ends or the post-stun cap (`FinisherFlightMaxPostStunFrames`); decide if a floor slide counts (recommend yes).

### 4.3 Damage path and rules
- Use the wall-impact precedent: the private `TakeDamage(int, Vector2?, bool ignoreDefenses)` (EnemyController.cs:2026) with no intake scale, stun, hitstop, poise, meter, Rally or D1a (`TryWallBounce` :910-918).
- Add a public `ApplyHazardImpact(damage, ...)` that checks `IsHazardVulnerable`, applies once per launch per hazard source (a set cleared by `ResetLaunchCarry`), and ignores defences (an Armoured mob loses its reduction to hazards).
- Numbers (proposal): standards take about 30 or die; fodder always die; elites take about 25 % of MaxHP and never die to one hazard. Bosses never qualify (`BossController` has no launch latches). Dormant lock members refuse (they refuse all hits, `RefuseEncounterHit`, and cannot be launched).
- Kill credit, dust, meter: `Die()` raises `EnemyKilled` with no attacker (:2091) and `RaiseDied` counts for the lock (:2106); kill dust pays from the ledger by enemy ID (2.4). So a hazard kill pays dust and clears the lock with no new code. No meter, Momentum, Rally or Punish accrues because nothing is attributed; recommend leaving it that way.
- Rewind: enemies default to `PreserveCurrentState` (EnemyController.cs:45, 2699), so a hazard kill survives a death rewind and its dust source stays issued, as for any kill. `StoryCyclicHazard` restores its cycle and `_hitThisCycle` on rewind.
- Freeze: world-sourced hits are not stopped by the player-hit freeze gates (`Hitbox.OnAreaEntered`, `Hurtbox.TakeHit`); add an `IsWorldHeld` / Time Freeze check so held mobs are not hit.
- Physics: wrap kills in `PhysicsCallbackGuard.Enter()` like the hero paths (StoryLevelControllerBase.cs:1086); StoryDropSystem already defers its spawn (:31-34).
- Per hazard: contact types (spikes, cyclic) hit on overlap; extractor discharge on its pulse; flood and lava ticks only while the mob is flagged at tick time. Searchlight and tremor debris stay hero-only. Pits: none authored, and the mob fall net returns a member (240 px), so no kill.
- Bot telemetry: `damage_by_source` is hero damage only, so add `hazard_kills`, `thrown_kills` and mob hazard damage.

Per file: EnemyController.cs (+ partial EnemyController.Hazard.cs), the six hazard scripts above, `BuildHazardSpikes`, masks gain the Enemy bit only (no new layer).

Tests: HazardMobRulesTests (pure, 6), one real-frames case per hazard type (8), vulnerability matrix (walking, stunned, finisher flight, thrown, elite, dormant, held under freeze, 6), dust on a hazard kill and rewind (3). About +23 new, 3-4 in place (CollisionLayerTests).

## 5. Estimated test delta
Slots +2 (4 in place); swarm +12 (about 20 in place); traits +23; hazards +23. Total about +60 new and 25-30 changed in place, so roughly 4090 against 4028. Watch: the 122 resource floor, `MaxWarmCombatants` 30, the AGENTS.md `[TestCase]` count.

## 6. Bots and telemetry
- Targeting is nearest-member with a +2000 penalty after `LockTargetDeprioritizeFrames`, and no 620 px cap or line of sight for woken lock members (PlaytestBots.cs:287-288, 862, 921-954). A 10-mob swarm suits nearest-first, but the deprioritise timer can ping-pong while fodder keep arriving. The stuck-lock watchdog (`LockStallFrames` 3600) resets on any member hurt, so it will not misfire.
- The low-HP retreat (:1020) and block brace were tuned on 2-5 mob waves; swarm damage per second will show bot weakness before content weakness.
- Add to PlaytestEncounterStats / PlaytestTelemetry: (a) `slot_idle_with_waiters_frames` (a free slot and a ringing candidate: the direct test of whether slot rules limit pressure); (b) `melee_candidates` mean per engaged frame (the supply measure); (c) per-swarm records (wave flag): clear time, peak living, hero damage taken, mean holders and attackers within 250 px during it; (d) `hazard_kills`, `thrown_kills`, grabs, throws, armour strips; (e) trait counters (catcher counters landed, anti-air punishes, armoured damage prevented).
- compare_runs.py: extend `crowd_rows` (:516) with (a)-(c) and add a swarm table; keep the front / all gap split. Rerun the same command set under a new tag prefix. Before / after batches are not comparable, as with every earlier update.
- Bots never press Grab, so Armoured, throws and thrown-hazard kills will not appear in `play` runs unless `play` learns a throw (low priority); record that 7d is invisible to bots otherwise.

## 7. Risks
- Stacked unjudged pressure: slots up, swarms up, Time Freeze off, Hard unchanged. A 10-mob swarm on Hard with 4 slots is the largest single step.
- Weight-0 fodder may feel unrewarding; weight 1 thins every mob's dust.
- Pool warm against the 30 cap; recycle-oldest at 40.
- Traits lean on the grab, and the chord can be switched off.
- Hazard mask changes touch many Trigger areas and tests; walking mobs are filtered only by the predicate, so keep a regression test per hazard.

## 8. Open questions with recommended answers
1. Fodder dust: weight 0 (recommended) or a scripted wave-clear share?
2. Fodder look: reuse an atlas with tint and scale until Package 10 (recommended)?
3. Swarm placement: one wave per level, two only on the largest locks, none on L0 / L12-14 (recommended)?
4. Ranged permits: leave 1/2/3 (recommended)?
5. Armoured: strip on a completed throw (recommended), x0.5 on Basic and Special, Ultimate unreduced?
6. Catcher counter: blockable (recommended) or Guard-Crush?
7. Anti-air: trigger on jump-ins only, not on Rolling or movement-ability states (recommended)?
8. Hazard numbers: standards 30 or die, elites 25 % MaxHP, no boss, no meter or Momentum credit (recommended)?
9. Does a floor slide after a finisher count as launched (recommend yes until the carry ends)?
10. Defy-blast pushes: not hazard-vulnerable (recommended).
