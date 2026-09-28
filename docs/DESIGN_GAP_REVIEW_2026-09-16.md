# Implementation versus intended design — 2026-09-16

The project has substantial playable infrastructure and broad content coverage, but it does **not yet satisfy the complete intended design**. The largest gaps are durable resource restoration, reward-source/content bindings, boss encounter mechanics, puzzle recovery, production presentation, and measured gameplay validation. Passing unit/content tests currently overstates conformity in some areas: one reward test explicitly pins an obsolete enemy binding.

This is a review report, not an implementation change or a new design decision. Owners for the work below are **Unassigned**. Existing accepted deferrals remain deferrals.

## Review baseline and limits

- **Requested design source:** `D:\Projects\fighters-through-time-docs-3`, commit `646de5a1df6c2589346f5997aea95c385a566917` plus its modified `design-godot-v7.md`.
- **Game source:** commit `599d5b84e49af7ecf80f5e5fa10921dd6cb68a1f` plus the existing working-tree changes listed below. Findings describe that working tree, not an unmodified release.
- **Master GDD SHA-256:** `D2E306C6502CEA917441768E3E17A60E5016BFEC3CCBA739FDAB7789CD400536`. The repository's `design-godot.md` has the identical hash.
- Read the GDD's relevant requirements, adopted contracts, implementation orientation and deviation records; traced selected requirements through C# producers/consumers, scene/resource bindings and tests. Historical reports were leads, not proof. More specific adopted decisions govern older conflicting prose.
- **Validation performed:** `dotnet build --no-restore`: succeeded, 0 warnings/errors. `dotnet test FightersThroughTime.csproj --no-build --settings .runsettings --logger "trx;LogFileName=design-gap-review-2026-09-16.trx"`: **2,237 passed, 0 failed, 0 skipped**, reported duration 1m13s, Windows/.NET 10, Godot headless configuration. [Local test record](../TestResults/design-gap-review-2026-09-16.trx). The run emitted orphan-node warnings; these were not attributed or investigated as leaks in this review.
- No manual campaign playthrough, visual/audio acceptance, real Steam session, cross-platform replay or hardware performance certification was performed. Static findings below are distinguished from missing validation. This is a broad design-conformance review with targeted deep traces, not a line-by-line certification of every requirement or every encounter.
- Pre-existing modified files preserved: `AGENTS.md`, `design-godot.md`, `docs/design-contracts/TEMPORAL_STATE_CONTRACT.md`; `PlayerController.cs`, `ChronalRewindBuffer.cs`, `ChronalRewindManager.cs`, `PathMovingPlatform.cs`; and `ChronalRewindTests`, `DeathTriggeredRewindTests`, `LevelDeathRewindDiagnosticTests`, `MirrorParadoxTests`, `StoryCombatRulesTests`.

Priority means implementation order: **P1** affects promised mechanics, progression, resource fairness or encounter behavior; **P2** is incomplete content/presentation or an evidence gap; **Deferred** is accepted future scope. It is not a measured crash-severity ranking.

## What is present

| Area | Current implementation | Qualification |
|---|---|---|
| Campaign structure | Hub, Levels 0–15, nine per-hero 4A scenes, explicit 17-slot route and locked campaign hero | Content existence is not full route/balance acceptance |
| Roster/progression | Manifest-backed roster, all nine kits, nine grids, milestone ability unlocks | Individual kit mismatches remain below |
| Story systems | Integrity clock, checkpoint roles, attempt/recovery records, death rewind, Time Freeze, Act III anchors/Smothered and ending selection | Persistence and placement gaps remain |
| Local Fighter | Deterministic fixed-point 1v1, Stock/Time, CPU, ten stages, three Open stages, Sudden Death and match results | Native network delivery and platform acceptance are separate |
| Combat | Shared basic rules, block/shatter, Rally, Defy, Echo Step, grabs, defensive layers and Conductive state | A rule/helper existing does not prove every consumer uses it |
| UI/onboarding | Authored UI scenes, localization, settings, Move List/Systems Card, six standalone Calibration Drills | Visual usability and playtest acceptance remain partial |
| Presentation | Sprite/VFX libraries, generated raster art, lighting/audio frameworks and stem mixing | Not equivalent to final artist-produced assets or completed score |

Representative evidence: [campaign routing](../scripts/Core/StoryManager.cs), [Legacy base](../scripts/Environment/LegacyLevelControllerBase.cs), [content manifest](../resources/Content/content_manifest.csv), [Fighter driver](../scripts/FighterSim/FighterSimulationDriver.cs), and the current passing content/determinism/integration suites.

## P1 — confirmed implementation/content gaps

### GAP-01 — Ordinary loads do not preserve most combat resources

**Target:** F10 and T01 require current block charges, regen/shatter-lockout timers, ability/Echo Step cooldowns, outstanding Rally meter and Wardenclyffe recharge delay to survive ordinary checkpoint reconstruction. Loading must not replenish spent resources.

**Observed:** [StoryPlayerResourceTimers](../scripts/Core/StoryAttemptState.cs) declares these fields, but the production capture path in [StoryManager](../scripts/Core/StoryManager.cs), around line 962, publishes only `TimeFreezeCooldownSeconds`. The base restore path at [StoryLevelControllerBase.cs](../scripts/Environment/StoryLevelControllerBase.cs), around line 548, consumes Rally's saved value and restores HP/meter, but has no corresponding restoration of the other resource timers. A newly constructed player therefore does not inherit those current values. The Rally settlement reader also lacks its live-value publisher.

**Impact:** save/resume can alter combat readiness and lose uncredited meter, contrary to the attempt resource contract. Save-schema tests can pass while live capture/restore is absent.

**Acceptance:** round-trip a real player with partially depleted block, active lockout/cooldowns, pending Rally meter and recharge delay through save/reload; preserve remaining values and settle Rally once, including repeated reload and recovery paths.

### GAP-02 — All nine 4A Eraser rewards bind the wrong enemy

**Target:** F05 reserves 15 required-encounter dust per 4A, with finite source identities matching actual encounters.

**Observed:** every `resources/Content/reward_manifests/level_04a_*_rewards.tres` still lists `chrono_guard_elite` for `.eraser_debut`. [EraserDebutTrigger.cs](../scripts/Environment/EraserDebutTrigger.cs), lines 53/68/138, actually spawns `unbound_eraser`; no variant override was found. [LevelRewardDirectory.TryIssueEnemyAward](../scripts/Environment/LevelRewardDirectory.cs), around line 261, looks up a queue by the actual enemy ID, so this encounter cannot claim its reserved elite share.

**Test gap:** [RewardManifestTests.cs](../tests/ContentValidation/RewardManifestTests.cs), around line 248, explicitly compares the manifest with `PlaceholderEnemyID`, not the trigger's active default. The passing test entrenches the mismatch.

**Acceptance:** bind manifests and tests to the actual spawned enemy; kill and collect every required source in every 4A/difficulty and demonstrate the 15-dust total. Do not increase the pool to compensate.

### GAP-03 — Secret rewards are allocated but physically unavailable

**Target:** F05's complete collection route provides 1,000 base dust; optional pools include discoverable secrets.

**Observed:** [SecretCache.cs](../scripts/Environment/SecretCache.cs) and secret source allocation exist, but searches of production scripts/scenes found no cache construction or placement. The base only restores already-found caches. The sealed [Legacy BuildLevel](../scripts/Environment/LegacyLevelControllerBase.cs) builds no cache or Extractor. Its manifests allocate the entire 10-dust optional pool to the secret when no Extractor exists, as [LevelRewardDirectory](../scripts/Environment/LevelRewardDirectory.cs) explicitly does.

**Impact:** the intended thorough-run economy is unreachable; each 4A alone lacks its entire optional 10 dust, separately from GAP-02. Existing `VERIFY-SECRET-CACHE` remains valid.

**Acceptance:** place reachable, correctly identified sources; verify physical collection, overlap/idempotency, reload and complete-route totals rather than only allocator sums.

### GAP-04 — Summons can consume another encounter's finite reward entitlement

**Target:** F05/F10 distinguish authored finite reward sources from repeatable summons and bind persistence to stable source identity.

**Observed:** [StoryDropSystem](../scripts/Environment/StoryDropSystem.cs), around line 77, requests an award by `payload.EnemyID`. The directory takes the first unissued/unclaimed source for that enemy type. [EnemyAbilityExecutor.SummonMinions](../scripts/Enemies/EnemyAbilityExecutor.cs), around line 492, spawns the same enemy type through the ordinary factory without a distinct reward provenance argument.

**Impact:** when a matching mandatory source remains unissued, a summoned enemy can consume its entitlement. The total cap may still hold, but reward location/ownership is wrong. This is a conditional source trace, not a claim that every boss summon always pays.

**Acceptance:** carry stable encounter/reward identity from spawn to death, mark summons ineligible, and verify a summon dies before a matching mandatory enemy without stealing its reward.

### GAP-05 — Per-level sealing interaction and pre-seal resume are absent

**Target:** N01 requires boss defeat → regain control beside the physical pickup and anchor → deliberate Seal Timeline interaction → one completion transaction. `AwaitingSeal` must survive reload.

**Observed:** [StoryLevelControllerBase](../scripts/Environment/StoryLevelControllerBase.cs), lines 1282–1370, advances defeat dialogue into exit/results and completion. `StorySealReadiness` has schema/normalization but no production writer. [TemporalCoreAnchor](../scripts/Environment/TemporalCoreAnchor.cs) is finale-only and explicitly derives armed state; its documented pre-insertion reload repeats the fight.

**Impact:** the intended agency, pickup opportunity and pre-seal persistence are missing. Existing `DEFER-SEALING-ANCHOR` records this package deferral, but it remains a gap against the adopted N01 target. The older implementation's derived-state policy does not supersede N01.

**Acceptance:** implement the reusable interaction and durable pending state across boss levels, including exactly-once pending pickup/completion recovery and the finale.

### GAP-06 — Final-boss historical recovery does not suspend world combat

**Target:** T01b requires a 1.5-second combat suspension preserving all participants' timers, plus historical/current/authored-anchor position fallback.

**Observed:** [BossController](../scripts/Enemies/BossController.cs) implements boss history, capped healing and its own suspended state. It raises `OnBossHistoricalRecovery`, but the production search found no event subscriber. `HistoricalRecoveryAnchor` is declared and read, but no scene assignment/controller binding was found.

**Impact:** the player's and other actors' combat are not suspended by that event; the final authored fallback is unavailable. Boss-local recovery tests do not establish world-wide conformity.

**Acceptance:** coordinate suspension/release without timer catch-up; verify player, summons, projectiles, statuses and hazards throughout the beat; bind and exercise a safe anchor with both earlier destinations obstructed.

### GAP-07 — Several authored boss phase rules are still replaced by generic abilities

**Target:** GDD §6 makes its per-boss phase mechanic a minimum requirement, beyond faster movement or a newly enabled generic attack.

**Confirmed examples:**

- **Jackal Priest:** the target leaves a strikeable burst decoy on each P2 teleport, with a readable true-staff tell. [jackal_step.tres](../resources/Bosses/Abilities/jackal_priest/jackal_step.tres) invokes the generic [Teleport](../scripts/Enemies/EnemyAbilityExecutor.cs) position change. No decoy creation exists there or in the inspected Level 8/boss wiring.
- **Tragedy King:** the target requires scripted actors, cycling trapdoors and invulnerability until the actors finish. [curtain_call.tres](../resources/Bosses/Abilities/tragedy_king/curtain_call.tres) summons two ordinary `holo_page` enemies; [tragedy_king.tres](../resources/Bosses/tragedy_king.tres) enables all four abilities from phase 0. The inspected boss/Level 10 code does not implement the actor-completion immunity rule.
- **Chronal Inventor:** GDD P2 calls for two destroyable corner coils shielding the boss. The inspected Level 3 controller and shared boss executor contain no encounter binding for this dependency.

The common intro/name-card/free-telegraph mechanism **does exist** in [BossEncounterController](../scripts/Enemies/BossEncounterController.cs); it should not be listed as wholly missing. Titanic and the Bastion also have explicit phase-event handlers. This finding does not claim every boss phase is absent.

**Acceptance:** a per-boss phase requirement → code/scene → runtime test matrix, beginning with these confirmed gaps; demonstrate each rule in an actual encounter. Do not count a named ability resource as proof of its narrative mechanic.

### GAP-08 — Puzzle reset and source-ownership contracts are incomplete

**Target:** V01b requires a safe local Reset Puzzle interaction for losable/unusable arrangements; V01c requires designated props/actions, puzzle ownership and stale-contact rejection.

**Observed:** no player-facing reset station/prompt was found. [PuzzleManager.ResetPuzzle](../scripts/Environment/PuzzleManager.cs), around line 92, clears conditions and can clear persistent completion; it is not the prescribed unsolved-only, geometry-validated prop reconstruction transaction. [WeightedObject](../scripts/Environment/WeightedObject.cs) has weight but no owning puzzle/prop identity. [PressurePlate.RegisterBody](../scripts/Environment/PressurePlate.cs) accepts any `WeightedObject` or player, without a per-puzzle source allowlist or explicit player-occupancy opt-in.

**Impact:** recovery from bad arrangements and cross-puzzle eligibility are not guaranteed. Some unintended body types are already rejected, but that is only partial V01c coverage.

**Acceptance:** enumerate puzzles needing reset, author reachable controls and restoration volumes, preserve solved facts, reject cross-puzzle props, and test obstructed reset, duplicate colliders, lost props, freeze and reload.

### GAP-09 — Story environmental damage skips ordinary block

**Target:** D03c sends eligible external-hazard remainder through Basic-class one-charge block after finite defenses.

**Observed:** [PlayerController.ApplyEnvironmentalDamage](../scripts/Characters/PlayerController.cs), around line 2703, calls `ResolveProtectionLayers` and then `ApplyDamage`, without the ordinary block resolution used by hurtbox hits.

**Impact:** qualifying Story hazards damage HP where the design requires shield absorption; the Fighter implementation does not establish Story parity. Lethal kill boundaries remain a separate non-hit rule.

**Acceptance:** exercise grounded eligible block, finite-shield partial/full absorption, exhausted block, Defy and non-hit pits through the actual hazard entry points.

### GAP-10 — Two kit behaviors have missing consumers; a third is partially absent

| Behavior | Current gap/evidence | Acceptance |
|---|---|---|
| Tesla Conductive → Lorentz chain, F07 | `FighterConductiveRules.TryConsumeChain` is defined/tested but has no production call. The Story Lorentz path exists; the Fighter chain consumer is missing. See [FighterEntitySystems](../scripts/FighterSim/FighterEntitySystems.cs) and [TeslaAbilities](../scripts/Characters/Abilities/TeslaAbilities.cs). | Successful marked hits consume only that Tesla's mark, once per Pulse/target, when an eligible coil exists; unmarked/blocked hits do not chain; rollback reproduces it. |
| Pocahontas Spirit Strike movement | [Story ability](../scripts/Characters/Abilities/PocahontasAbilities.cs) sets diagonal-up caster velocity. The deterministic special is a melee execution without equivalent caster translation. [CpuRecoveryProfile](../scripts/FighterSim/CpuRecoveryProfile.cs) consequently maps no optional mobility Special. | Implement and verify the Fighter trajectory, then enable the validated CPU recovery mapping and both-edge/depth drills. |
| Cleopatra Royal Aegis decoy recipient | [CleopatraAbilities](../scripts/Characters/Abilities/CleopatraAbilities.cs), around line 411, explicitly states no decoy node exists. The hero shield is implemented; its required separate decoy recipient is not. | Author the decoy and independent shield lifetime/depletion/grant identity, without merging capacities with the hero. |

### GAP-11 — Globe audience logic continues advancing during Time Freeze

**Target:** F03 freezes hazards and mechanisms without catch-up.

**Observed:** [Level10Controller.TickAudience](../scripts/Environment/Level10Controller.cs), around line 593, advances idle, warning and active timers without checking Time Freeze. The level controller is not itself parked by [TimeFreezeController](../scripts/Environment/TimeFreezeController.cs)'s freezable/projectile/zone group sweeps. Freezing the hazard component does not stop its external controller from arming or changing phases.

**Acceptance:** freeze during each audience state and verify unchanged timers/target/phase, no queued strike, and continuation from the preserved state after thaw.

## P2 — partial coverage and missing acceptance evidence

### GAP-12 — Pars and recovery budgets are provisional

V01a/F11 require measured required-route medians and separately measured checkpoint/difficulty remaining-route budgets. Shared levels use hardcoded provisional pars; [StoryLevelControllerBase](../scripts/Environment/StoryLevelControllerBase.cs), around lines 124/833, derives recovery time from `0.90 / 0.45 / 0.00` fractions. Legacy variants have individual authored numbers, not measurement records.

The existing ledger's claim that **no `budgetVersion` field exists is stale**: [StoryRecoveryEvent.BudgetVersion](../scripts/Core/StoryAttemptState.cs) exists with default 1. That does not supply a versioned measured budget dataset.

**Acceptance:** record per-hero Normal route samples, choose the required median-based par, measure enabled recovery anchors separately on all difficulties with depleted legal resources, and associate the published budget version with actual measurements. Include reset/detour time assumptions.

### GAP-13 — Encounter-baseline records are not authoritative reconstruction inputs

F10 requires stable checkpoint encounter membership. `CheckpointRecord.BaselineEncounterIDs` is written by [StoryManager](../scripts/Core/StoryManager.cs), but no production restoration reader was found. Resume still invokes per-controller `MarkWavesClearedThrough`; the base's baseline IDs are convention-derived.

This is an incomplete persistence integration, **not proof that every existing checkpoint respawns enemies incorrectly**. Acceptance is an explicit scene-to-baseline map and before/after-checkpoint reload coverage, including nonstandard wave IDs and retained reward claims.

### GAP-14 — Nexus Ultimate isolation relies on placement

F04 permits a free puzzle Ultimate, never free combat damage/reward. The current source invokes the real Ultimate through [PlayerController.TryCastNexusUltimate](../scripts/Characters/PlayerController.cs) and relies on the cleared arena/designated placement. There is no general puzzle-only damage-origin fence in that cast path.

Treat this as an enforcement/robustness gap, not a reproduced exploit in all nine routes. Acceptance should place a hostile actor/construct near the authorized cast and prove it cannot receive combat benefit while the puzzle target still works.

### GAP-15 — Act-boundary hub dialogue is authored but untriggered

The Sarah/Okafor act beats exist in localization/dialogue content; production searches found no act-sequence caller. Hub progression therefore does not demonstrate the intended delivery merely because the lines exist. Wire progression/once-only selection and test first return, repeated visits, load and the final departure before the Act III hub lockout.

### GAP-16 — No Story lethal boundaries are placed

F16's [StoryKillBoundary](../scripts/Environment/StoryKillBoundary.cs), non-hit death path and `BuildKillBoundary` helper exist. No production helper calls or scene placements were found. Existing `VERIFY-STORY-PITS` correctly distinguishes working machinery from absent content.

This is a content/design disposition: identify which authored openings are meant to be lethal and bind them, or explicitly approve the non-lethal route design. Do not turn every platforming gap into a kill pit by inference.

### GAP-17 — Ten boss HP rows remain an unresolved target conflict

The first four bosses match the adopted V7.6 rows, and Mirror Paradox's base is 1,000. Other resource values remain:

| Boss | Current base HP | GDD table HP |
|---|---:|---:|
| Tidal Eraser | 640 | 1,050 |
| Vulcan Decimator | 660 | 1,200 |
| Dread Admiral | 700 | 1,350 |
| Jackal Priest | 700 | 1,500 |
| Iron Chancellor | 780 | 1,700 |
| Tragedy King | 800 | 1,850 |
| Siege Cannon | 880 | 2,000 |
| Gravity Overseer | 950 | 2,200 |
| Forge Sentinel | 1,050 | 2,500 |
| First Unbound | 1,200 | 3,000 |

Evidence: [boss resources](../resources/Bosses), GDD boss data table and existing `VERIFY-BOSS-HP`. In particular, the authored 850 → 640 transition is not an ascending curve. The adopted ledger calls for one explicit curve ruling and encounter revalidation; neither the larger numbers nor the shipped curve should silently become the answer.

### GAP-18 — Production presentation remains incomplete

The game has real presentation frameworks and generated sprite/VFX work. Remaining obligations include artist-produced release assets, polished environment/ability/boss tells, illustrated major-story sequences and the intended music/SFX coverage. The manifest still labels significant presentation content `Placeholder`.

Concrete example: every Legacy route resolves the same [level_04a_audio.tres](../resources/Audio/level_04a_audio.tres), with three placeholder stems and a one-second loop. This is an operational audio binding, not the P01 per-variant era-appropriate music/adaptation record. A reused composition is allowed, but its fit, transitions and coverage still need authoring and review.

Collapse/Anchor Snap presentation is also partial: mechanical controllers exist, while the full authored visual/audio beats are not established by the notice text. P01's representative Joan route with measured production effort and representative art/music remains an acceptance obligation, not something placeholder-only route tests establish.

### GAP-19 — Launch remote-play integration is missing

M01 calls for Local Versus plus Steam Remote Play Together on supported Steam builds, with the latter hidden when unavailable. [NetworkManager.SteamTransportAvailable](../scripts/Networking/NetworkManager.cs) is hard false; the current implementation supplies explanatory copy, not verified Steam integration/invitation delivery.

Local controller-disconnect hooks do exist in [InputManager](../scripts/Core/InputManager.cs); do not describe device-loss handling as wholly absent. Steam host/guest assignment, overlays, invitation cancellation, stream interruption/rejoin and rematch require real-session acceptance. Remote Play is streamed local play and needs its own validation, not rollback peer tests.

### GAP-20 — Gameplay and supported-platform validation is incomplete

- **V01 routes:** locked early-game kits, every eligible hero/difficulty, every Legacy variant, depleted recovery baselines, optional detours and reset arrangements need measured completion/reachability coverage. Scene existence and geometry assertions are narrower evidence.
- **F09:** a frame-advantage/escape matrix across relevant normals, projectiles, constructs, grabs, landing and defense states is still required. Random CPU choices do not prove legal escape windows.
- **V02:** controlled stall-vs-pressure trials and cause diagnosis are still pending. The contract does not authorize automatic cooldown reductions.
- **CPU recovery:** current Paris drills do not establish all deep-centre recoveries. The documented Lincoln reachability limitation needs a stage/kit balance disposition, not a promise that every tier must recover every position.
- **S01/platforms:** the passing rollback suite demonstrates its local harness coverage. It does not complete the field-level serialization/hash/reconstruction inventory, Windows/macOS/Linux equivalence, real transport behavior or Steam Deck/full-render frame and allocation budgets. Older performance tables must retain their measured revision and scope.
- **Visual/audio/accessibility:** readability, warnings, reduced effects, layout/focus and audio mix need live judgment on representative hardware. Automated load/string tests do not certify the experience.

## Explicit deferrals and architecture exceptions

These should remain visible in planning without being misreported as newly discovered launch defects:

| Item | Disposition |
|---|---|
| Native rollback online/LAN, matchmaking, reconnection/forfeit delivery | M01 explicitly schedules native delivery as the first post-launch Package 7 milestone. Fixed-point simulation, UDP and rollback infrastructure already exist; production flow is unfinished. |
| Fighter/Story movement differences | GDD explicitly records flat Fighter gravity/fixed-height jumps versus Story weighted gravity/short hop, and Fighter Down+Jump versus Story double-tap-Down drop-through. Crouch/skid/coyote/buffering parity also needs its dedicated simulation pass. |
| CPU state outside snapshots | `DEFER-CPU-SNAPSHOT`: CPU records/replays generated input instead of snapshotting its full decision state. Resolve the contract/architecture difference; do not resample RNG on rollback. |
| Fixed Fighter character enum | `DEFER-ROSTER-ENUM`: append-only protocol identity, not evidence the runtime roster conversion is missing everywhere. |
| Shared global/story schema counter | `DEFER-SAVE-VERSION-SPLIT`: coupled migration versioning was accepted for the package; lower priority than broken live resource capture. |
| Four-player/teams, advanced Training Mode, cosmetics, replays, online profiles | Outside initial scope; do not add them to the launch gap count. |

## Documentation corrections needed

1. **Serpent Nest is not currently the listed 10-damage/4-second deviation.** Its actual resource is [cleopatra/special_1.tres](../resources/Abilities/cleopatra/special_1.tres), with `BaseDamage = 6` and `DamageTickIntervalFrames = 60`. [SerpentNestNode](../scripts/Characters/Abilities/SerpentNestNode.cs) consumes those fields. The old ledger points at `special_2.tres`, which is now Sandstorm Vortex. Retire/correct that historical observation after the chosen runtime evidence standard, rather than retuning an already matching resource.
2. **The recovery version field exists.** Correct the ledger's field-absence claim while retaining the genuine measurement/versioned-dataset gap (GAP-12).
3. **Fighter invulnerability does drive glow.** [FighterSimulationDriver](../scripts/FighterSim/FighterSimulationDriver.cs), around line 634, reads `state.InvulnerabilityFrames` and calls `SetSpawnInvulnerability`. The orientation document's blanket contrary statement is stale. This does not certify visual priority/readability.
4. **The temporal contract mirrors disagree.** The master GDD and modified game contract describe five-second death history and the newer landing behavior; the external docs workspace's `TEMPORAL_STATE_CONTRACT.md` still contains the older 480-frame wording. Reconcile that mirror explicitly. Do not classify the existing five-second working-tree implementation as a regression merely by selecting the stale source.
5. **Implementation-status prose trails the build.** External P02/status language still describes numerous now-implemented features as unverified/pending, and the ledgers retain historical statements. Update statuses per feature with pinned evidence, not by globally changing all rows to complete.
6. **Tests need target validation, not just self-consistency.** GAP-02 is the concrete example: the assertion and resource agree with each other while both disagree with the live spawner. Add placed-source/consumer checks at integration boundaries.

## Recommended sequence

1. Fix live save capture/restore and reward identity/bindings (GAP-01–04); validate reload and actual physical collection before economy tuning.
2. Complete sealing persistence, world-wide boss recovery, missing boss phase rules and puzzle recovery/eligibility (GAP-05–08).
3. Close environmental block, kit-consumer and freeze-controller gaps (GAP-09–11), plus Nexus isolation.
4. Run route/recovery measurements and combat escape/stall studies; resolve the boss HP and deep-pit balance decisions using those results.
5. Complete production asset/music mappings and Steam launch delivery; conduct visual/audio/accessibility and supported-hardware acceptance.
6. Keep post-launch native networking and recorded architecture exceptions on their accepted roadmap. Refresh documentation and tests alongside each actual closure.

The strongest next milestone is **a complete, resource-correct, reload-safe campaign route with collectible authored rewards**, followed by measured balance and production acceptance. Adding more content before these integration gaps are closed would make completeness harder to establish.
