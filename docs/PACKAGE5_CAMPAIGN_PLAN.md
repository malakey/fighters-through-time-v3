# Package 5 — Campaign levels 2–15: implementation plan

Status: authored 2026-08-07. Working plan for `IMPLEMENTATION_PLAN.md` Package 5. Shared reference
for the implementation passes; agents record deviations in §9 at the bottom.

**Phase A completed and merged 2026-08-07** (A2 `7507533`: 14 era-mechanic components + 11 templates
+ 17 tests; A1 `7898e4f`: `StoryLevelControllerBase`, credits/campaign-completion chain,
`IsCompleted` writers, post-campaign hub portal, `Level_10_Globe` route fix, 29 tests). All 46 new
tests and the 139-test ContentValidation sweep pass on merged main.

**Waves A/B/C are BLOCKED pending the finalizer-crash fix.** The full suite (463 tests after
Phase A) cannot complete on merged main: the GdUnit Godot child dies nondeterministically
(`FATAL: Condition "gchandle.is_released()"` in the GC finalizer path — CLAUDE.md signature 3's
family). A1's worktree bisect showed the pre-Phase-A 417-test suite is stable and adding ANY 418th
test enters the broken regime, so this is a suite-size threshold, not a content defect. A fix
exists on `claude/peaceful-wilbur-23eaa0` (dispose Godot collections / drain finalizers, authored
in a parallel session, unmerged, needs performance re-validation). Do not launch wave agents until
that fix is merged and three consecutive full-suite runs pass.

Authority order: explicit user instruction > `design-godot.md` > this plan > existing code — EXCEPT
numbers: `docs/DUST_ECONOMY.md` + `tests/ContentValidation/DustEconomyTests.cs` lock the per-level
encounter economy and OVERRIDE the design doc's conflicting prose (elite dust is 10 not 20,
extractors are 15 dust each, per-level counts come from §4's table). Resources beat prose.

## 1. Scope

Every level 2–15 ships the per-level gate from `IMPLEMENTATION_PLAN.md` Package 5: separate
authored scene at the exact `StoryManager.LevelScenePaths` path, room graph with camera confinement,
2–3 tested checkpoints with save/resume, encounters exactly matching the locked economy table, a
boss arena (Mirror Paradox arena for 13), at least one mechanically-implemented era identity,
entrance/boss/exit dialogue resources, completion overlay + auto-deposit + sequential unlock +
pause/failure/reload behavior, pool catalog wiring, and content/smoke/localization tests.
Level 15 additionally ships the restoration sequence, ending dialogue, credits, the
`IsCompleted` save flag, and post-credits/post-campaign hub behavior.

OUT of scope: production art/audio (placeholder stems/tints only), cinematic presentation
(Package 8), hub NPC portal gating (deferred — interim auto-activation stays), character-specific
dialogue variants (deferred by design §16), New Game+/level replay (deferred).

## 2. Standing decisions (all agents)

1. **Construction style = the Florence convention, upgraded.** Each level is a thin `.tscn`
   (controller script + scene-authored toolkit template instances) plus a controller extending the
   new `StoryLevelControllerBase` (§3 A1). Graybox geometry stays code-built like Florence — the
   authored-scene TileMap conversion remains the open Package 2 item and is NOT re-litigated here.
   Unlike Florence, rooms use the tested `RoomTransitionTrigger` + `StoryCameraConfiner` for real
   per-room camera bounds.
2. **Level 10 is `Level_10_Globe.tscn`.** The manifest row (`level_10_globe`) and pool config
   (`level_10_globe_pools`) win; A1 changes `StoryManager.LevelScenePaths[10]` from
   `Level_10_London.tscn` to `Level_10_Globe.tscn` (one line) and adds the route test that would
   have caught it.
3. **IDs and keys.** LevelID = manifest id (`level_02_orleans` …); checkpoints
   `{levelID}_checkpoint_{0,1,2}` (0 = entry anchor, 1 = midpoint, 2 = pre-boss); dialogue set
   `resources/Dialogue/level_NN_dialogue.tres`, `DialogueSetID = "dialogue_level_NN"`, sequence IDs
   `level_NN.entrance` / `level_NN.boss_intro` / `level_NN.exit` (+ `level_NN.preboss` on 5/12/15,
   `level_NN.postboss` on 8); line keys `dlg_lNN_{beat}_{n}`; title `"{era}_level_title"`,
   objectives `"{era}_objective_{name}"`, hub next-mission `campaign_level_{era}`.
4. **Encounters are budget-locked.** Author EXACTLY the S/E/B/X counts in §4 (authored counts at
   Normal; `ScaleEncounterCount` handles difficulty). Standards/elites come from the level's era
   roster (Package 4 `docs/PACKAGE4_ROSTER_PLAN.md` §4.1) plus Future Cultists
   (`chrono_slasher` standard / `tech_enforcer` elite) mixed in; Titanic (5) and Act III (13–15)
   are cultist-only. Where the table says E = 0, place NO elites. Extractors use
   `ChronalExtractorTemplate.tscn` (15 dust, locked). Boss summon dust is a known small variance
   above the model — do not compensate; C1 documents it in DUST_ECONOMY.md.
5. **Concurrency budgets:** simultaneous live enemies must fit the level's pool config warm counts
   (§4 S+E column context: warm counts are concurrency, totals arrive across waves). Projectile-heavy
   enemy mixes must respect `enemy_projectile` warm counts.
6. **Dialogue:** generic player lines (no character-specific writing). Beat template = Florence
   (entrance: player reacts + Sarah radio briefing; boss_intro: 2-line exchange; exit: siphon
   shattered → locals restored → resonance-fading motif → return). Levels 8/12/15 use the
   design-doc authored scripts (design-godot.md 3368–3399) converted to keys nearly verbatim.
   Every visible string gets a `localization/en.csv` key.
7. **Level agents do NOT touch:** `resources/Pools/scene_pool_catalog.tres`,
   `tests/integration/SceneSmokeTests.cs`, `resources/Content/content_manifest.csv`, StoryManager,
   shared controllers/toolkit code, or other levels' files. Catalog rows, smoke-test rows, and
   manifest flips are applied at wave integration (§7). This is the shared-file conflict policy.
8. **Known issue — do not chase:** full-scene `--quit-after` headless runs of Florence/Hub crash AT
   SHUTDOWN with `-1073741819` in `Godot.Collections.Array.Finalize()` (CLAUDE.md failure
   signature 3, fix in progress in a separate session). New levels may show the same exit-crash.
   Gate on: zero SCRIPT errors during the run, clean `--headless --quit` import, and the in-process
   SceneSmokeTests. Report the exit code but do not treat the shutdown fault alone as a failure.
9. **Rewind/Timeline Collapse:** levels get rewind services from `StorySceneBootstrapper.Attach`
   defaults. Enemies keep their Package 4 rewind policies. Nothing level-specific to build beyond
   checkpoint registration, but every level must survive: death → rewind, rewind exhaustion →
   Timeline Collapse → hub → restart-from-anchor (`StoryManager.RestartCollapsedLevel`) resuming at
   the saved checkpoint with pre-checkpoint waves marked cleared (Florence's
   `RestoreSavedCheckpoint` pattern, provided by the base class).

## 3. Phase A — shared foundation (two parallel agents)

### A1 — Level flow framework, completion chain, credits (owner: framework)

- **`scripts/Environment/StoryLevelControllerBase.cs`** extracted from `Level01Controller`:
  geometry builders (`BuildFloor/BuildPlatform/BuildWall/BuildHazardSpikes/BuildCheckpoint/
  BuildWaveTrigger/BuildDoor/BuildRoomDecoration`), LevelManager creation (node name MUST be
  `"LevelManager"`), player spawn + camera limits + `RoomTransitionTrigger`/`StoryCameraConfiner`
  helpers, `StorySceneBootstrapper` attach, dust tally via `OnEnemyKilled`, generic
  checkpoint-resume (`RestoreSavedCheckpoint` with a virtual `MarkWavesClearedThrough(checkpointID)`),
  entrance-dialogue suppression on resume, boss encounter wiring helper (BossEncounterController +
  HUD + intro/exit dialogue hooks), `ShowCompletionResults` (LevelManager.CompleteLevel →
  LevelResultsPanel → ReturnToHub), extractor placement helper, objective helpers. Florence and
  Tutorial are NOT retrofitted (working code stays untouched); the base is for levels 2–15.
- **StoryManager:** fix `Level_10_Globe.tscn` path (§2.2). No other changes.
- **Campaign route test** (new, `tests/integration/CampaignRouteTests.cs`): every
  `LevelScenePaths[i]` for authored levels resolves via `ResourceLoader.Exists`; manifest
  StoryLevel `resource_path` agrees with `StoryManager.GetLevelScenePath` for all 16; sequential
  advance 0→15 caps at 15. (Initially asserts agreement only for existing scenes + the path-match
  invariant for all rows.)
- **Credits + campaign completion:** `scenes/ui/Credits.tscn` + `scripts/UI/CreditsController.cs`
  (scrolling credits from a `credits_*` key list, skippable with confirm, `ProcessMode.Always`);
  campaign-completion chain for Level 15 to call: ending dialogue → credits → set
  `StorySaveData.IsCompleted = true` + save → `GameManager.LoadScene(MainMenu)`. MainMenu already
  renders `IsCompleted`; add the `save_campaign_complete` banner key if the existing rendering
  needs one. **Post-campaign hub:** `HubWorldController` — when the active save `IsCompleted`, the
  Temporal Portal shows a localized "timeline restored" state instead of loading level 15 again;
  Holodeck/Repository stay usable.
- **Hub next-mission label:** replace the two-case `switch` with the `campaign_level_{era}` key
  family; add all 14 keys to en.csv.
- Tests: base-class unit coverage where headless-testable (checkpoint resume marking, completion
  chain event order, credits skip, IsCompleted write, hub portal completed-state), route tests.

### A2 — Era mechanic components (owner: toolkit)

All new components in `scripts/Environment/`, template scenes in `scenes/templates/` following the
existing toolkit patterns (exported IDs, `PuzzleManager` condition wiring where sensible,
`IStoryRewindable` policies where stateful, layer/mask conventions from `CollisionLayers`), each
with `tests/unit/PuzzleEnvironmentToolkitTests.cs`-style cases in a NEW file
(`tests/unit/EraMechanicToolkitTests.cs`) plus template-instantiation coverage:

| Component | Serves | Behavior contract |
|---|---|---|
| `SearchlightZone` | 4 Paris, 9 Berlin | Sweeping detection area (configurable arc/path + period). While player inside: Paris mode drains Ultimate meter at `UltimateDrainPerSecond` (5%/s default); Berlin mode fires a damage strike after `ExposureGraceSeconds` (1.5 s default). Exposes `PlayerDetected` signal for alarm waves. |
| `RisingWaterZone` | 5 Titanic (+ Tidal Eraser arena hook) | Water line rises in authored steps on trigger/timer; below the line: −50% move multiplier + drowning damage tick after a grace period. Visual = translucent ColorRect; `WaterLevelChanged` signal. |
| `GravityFieldZone` | 12 Lunar, 13 Void | Sets `PlayerController` gravity scale while inside (new minimal `EnvironmentGravityScale` hook on PlayerController, default 1.0, Story-only — NEVER touches FighterSim). Void variant cycles scale between authored values on a telegraphed timer (`GravityShifted` signal + tint pulse). |
| `PendulumAnchor` | 7 Nassau, 10 Globe | AnimatableBody2D swinging on an authored arc with an attached `LedgeGrabPoint` — rope swinging reuses the tested LedgeHanging state; jump-off inherits platform velocity. No new player state. |
| `PathMovingPlatform` | 7, 12, 13 | AnimatableBody2D following authored points at constant speed with endpoint waits. |
| `TrapdoorPlatform` | 10 Globe | Platform whose collision toggles open/shut on timer or trigger, with warning shake before opening. |
| `EscapeSequenceController` | 6 Pompeii, 15 | Advancing hazard front (damage area moving at authored speed) + objective countdown; catching the player deals heavy damage + knockback forward (not instadeath — rewind stays meaningful); `EscapeCompleted`/`PlayerCaught` signals. |
| `MovementDampenerZone` | 8 Egypt (deep sand) | Flat −N% move multiplier while inside (reuses the rift zone's slow application WITHOUT the snap). |
| `SequenceLock` | 8 Egypt (hieroglyphs) | N `IInteractable` glyph children must be activated in authored order; wrong pick resets; completion sets a `PuzzleManager` condition. |
| `RescuableNPC` | 4 Paris, 6 Pompeii | Interactable placeholder NPC: on interact plays freed feedback, despawns, increments an objective counter / sets a condition. |
| `ShieldGeneratorTower` | 2 Orléans, 11 Gettysburg | `DamageableEnvironmentObject` subclass; while alive keeps linked `ForcefieldBarrier` nodes (solid StaticBody2D + visual) active; destruction drops them and can set a condition. |

Constraint: NO FighterSim changes; the only shared-runtime edit allowed is the
`PlayerController.EnvironmentGravityScale` hook + the move-multiplier application if
`MovementDampenerZone` needs one beyond the status system (prefer reusing
`StatusMoveMultiplier`-style application locally; do not touch the status enum).

## 4. Per-level dossiers (locked numbers + era identity)

Economy columns are LOCKED (S standards / E elites / B boss / X extractors). Era enemies from
Package 4; "cultist mix" = blend chrono_slasher (+tech_enforcer where E>0). Boss resources exist —
levels build arenas + `BossEncounterController` wiring only. Pool warm counts (concurrency caps)
are in `resources/Pools/level_pool_configs/`.

| Lvl | Scene / LevelID | S/E/B/X | Era roster (S; E) | Era identity (mechanics to use) | Boss + arena notes |
|---|---|---|---|---|---|
| 2 | `Level_02_Orleans.tscn` / `level_02_orleans` | 8/0/1/3 | laser_archer, cyber-mix; — | Siege assault: 2× `ShieldGeneratorTower` + forcefields gating progress; mortar `StoryCyclicHazard` lanes | siegemaster_duke; battlement arena, 2–3 stone platforms |
| 3 | `Level_03_Chicago.tscn` / `level_03_chicago` | 8/0/1/3 | voltaic_shock_drone, cultist mix; — | Beam-routing puzzle room (BeamEmitter/ConductiveCoil/BeamReceiver — exists) gating the fairground; coil-discharge cyclic hazards | chronal_inventor; flat metallic stage, two coil side platforms |
| 4 | `Level_04_Paris.tscn` / `level_04_paris` | 10/0/1/3 | chrono_rioter, cultist mix; — | 2–3 `SearchlightZone` (ultimate-drain mode) corridors; 2× `RescuableNPC` prisoners behind `DestructibleBlock` locks opening the courtyard | revolutionary_tribunal (summoner, NOT knockback-immune); two drawbridge walkways over a pit, room for adds |
| 5 | `Level_05_Titanic.tscn` / `level_05_titanic` | 12/1/1/4 | chrono_slasher; tech_enforcer (cultist-only) | Act I finale. `RisingWaterZone` escalating per room, listing-deck slopes (angled floors), `level_05.preboss` beat | tidal_eraser; arena floods in phase 2 via `RisingWaterZone` hook |
| 6 | `Level_06_Pompeii.tscn` / `level_06_pompeii` | 10/0/1/3 | shock_shield_legionnaire, cultist mix; — | `EscapeSequenceController` lava-front room; counterweight/weight puzzle (exists) clearing rockfall; 2× `RescuableNPC` civilians; ash `StoryCyclicHazard` geysers | vulcan_decimator; slanted rocky slopes, two narrow ledges |
| 7 | `Level_07_Nassau.tscn` / `level_07_nassau` | 10/1/1/3 | laser_pistol_deckhand; overcharged_cannon_master | Ship-to-ship: `PendulumAnchor` rope swings between deck segments, `PathMovingPlatform` boarding skiffs, mortar cyclic hazards | dread_admiral; burning deck, two wooden yard platforms |
| 8 | `Level_08_Egypt.tscn` / `level_08_egypt` | 10/0/1/3 | plasma_spear_ward, cultist mix; — | Surface dunes with `MovementDampenerZone` deep sand; tomb section with `SequenceLock` hieroglyph puzzle gating the palace; `level_08.postboss` = the authored Cleopatra script (design 3368–3374) | jackal_priest (teleporter); sandy floor, two sarcophagi platforms |
| 9 | `Level_09_Berlin.tscn` / `level_09_berlin` | 12/1/1/3 | infrared_border_sentry; neural_mech_walker | `SearchlightZone` (strike mode, 1.5 s grace) stealth corridors; guard-tower vertical climbs; snow-tinted graybox | iron_chancellor; flat snowy street, two tower balconies |
| 10 | `Level_10_Globe.tscn` / `level_10_globe` | 10/0/1/3 | holo_page, cultist mix; — | Stage machinery: `TrapdoorPlatform` stage floor, `PendulumAnchor` between gallery tiers, idle-punish audience hazard (cyclic hazard with reset-on-move trigger zone) | tragedy_king (illusion summoner); open wooden stage, two gallery balconies |
| 11 | `Level_11_Gettysburg.tscn` / `level_11_gettysburg` | 12/1/1/3 | laser_rifle_infantry; cyber_cavalry_commander | Linear battlefield assault: artillery-line cyclic hazards (2 s telegraph) + cover geometry; 2× `ShieldGeneratorTower` arrays to destroy | siege_cannon (15 m ranges — WIDE arena); dirt path, rail-fence platforms |
| 12 | `Level_12_Lunar.tscn` / `level_12_lunar` | 14/2/1/4 | vacuum_digger; void_enforcer | Act II finale. Level-wide `GravityFieldZone` low gravity; vacuum vents as cyclic hazards; high-altitude platforming with `PathMovingPlatform`; `level_12.preboss` = the authored Sarah script (design 3376–3383) | gravity_overseer (3 phases); orbital pad arena |
| 13 | `Level_13_ChronalVoid.tscn` / `level_13_chronal_void` | 8/1/0/2 | rift_phantom + cultist mix; chrono_guard_elite | Era-mashup floating platforms; cycling `GravityFieldZone` shifts (telegraphed); `ChronalRiftZone` pockets. NO standard boss row — the encounter is `MirrorParadoxEncounterController` (dust 50 comes from it; economy table's B=0 EXCLUDES the mirror's 50, matching the locked 52-full/36-expected row — verify against DustEconomyTests before changing anything) | mirror_paradox; symmetric Fighter-like arena, flat + two platforms |
| 14 | `Level_14_NeoEarth.tscn` / `level_14_neo_earth` | 14/2/1/3 | chrono_slasher (+hologram_drone); tech_enforcer | Apex lab: laser-grid corridors (cyclic hazards in authored patterns), anti-grav containment `GravityFieldZone` pockets, metallic graybox | archive_prime (3 phases); security-core arena whose laser grid hazards sync to boss phase via `BossPhaseChanged` |
| 15 | `Level_15_Alexandria.tscn` / `level_15_alexandria` | 12/2/1/3 | chrono_slasher; tech_enforcer, chrono_guard_elite | Burning library assault; `EscapeSequenceController` reversed as advancing-restoration beat if useful; post-boss **Temporal Core insertion** interactable at the Prime Anchor → `level_15.ending` (authored script, design 3389–3399) → credits → `IsCompleted` → MainMenu (A1 chain). No standard results/hub-return flow | apex_eraser (3 phases, 3000 HP); grand library arena |

Checkpoint counts: every level exactly 3 IDs (`_0` entry anchor at spawn, `_1` midpoint, `_2`
pre-boss) matching the Florence precedent.

Wave A = levels 2–5. Wave B = levels 6–12. Wave C = levels 13–15.

## 5. Per-level deliverable checklist (every level agent)

1. Scene at the exact `StoryManager` path; root controller extending `StoryLevelControllerBase`;
   scene-authored toolkit instances where templates exist (checkpoints may stay code-built via the
   base helper). Level dimensions ~2–4 rooms, 8000–14000 px wide, Florence-scale.
2. Rooms with `RoomTransitionTrigger` + camera confinement; one-way platforms and at least one
   vertical section; bottomless pits only where the design implies them.
3. Encounters: exact S/E/X counts (§4), spawned via wave/proximity triggers using
   `EnemyFactory.Spawn` with era IDs; concurrency within pool warm caps; checkpoint-resume marks
   earlier waves cleared.
4. Era identity mechanics per §4 using Phase A components + existing toolkit; at least one puzzle
   OR bespoke traversal/hazard sequence, era-flavored.
5. X extractors placed (`ChronalExtractorTemplate`), guarding meaningful side paths.
6. Boss arena room + `BossEncounterController` (or Mirror equivalent) with intro/defeat dialogue
   wiring; arena geometry per §4 notes; boss row's ranges must fit the arena width.
7. Dialogue resource with the §2.3 sequences; en.csv keys for every line, title, objectives, room
   labels; use authored design scripts where they exist (8/12/15).
8. Content test file `tests/ContentValidation/LevelNNContentTests.cs` (unique per level): scene
   loads + instantiates headlessly, controller LevelID/checkpoint IDs correct, dialogue set
   resolves with all line keys present in en.csv, encounter authored counts match the locked
   economy table row (assert against your own authored spawn table), extractor count matches.
9. Validation: build; headless `--quit` import; `--filter` run of your test class + DustEconomy +
   ContentValidation; full-scene headless run reported per §2.8. Commit in your worktree.

## 6. Phase C — closeout (C1, after Wave C merges)

Catalog rows + smoke-test rows + manifest flips are applied per-wave at integration (§7); C1 then:
final manifest audit (StoryLevel rows Implemented/ReadyForReplacement/Valid; DialogueSet rows
flipped for 02–15; `UIScreen,credits` + `level_results` flipped), campaign route test extended to
assert all 16 scenes resolve, localization sweep (all `dlg_l*`, titles, objectives resolve; no
duplicate keys), DUST_ECONOMY.md addendum (boss-summon variance note, extractor placements now
authored), full-campaign sequential advance test, `AGENTS.md`/`CLAUDE.md`/`IMPLEMENTATION_PLAN.md`/
both `IMPLEMENTATION_STATUS.md` updates, full validation (build, suite ×3 consecutive, import,
smoke sweep of all 14 new scenes reporting script errors vs shutdown-crash separately).

## 7. Sequencing and integration protocol

1. Phase A: A1 + A2 in parallel worktrees; orchestrator merges, runs full suite.
2. Wave A: 4 level agents (2,3,4,5) in parallel worktrees. Orchestrator merges (en.csv unions),
   then applies the wave's integration block in ONE commit: 4 catalog entries, 4 SceneSmokeTests
   rows, manifest flips for the wave, hub label keys already present from A1. Full suite + import.
3. Wave B: 7 level agents (6–12), same protocol.
4. Wave C: 3 level agents (13,14,15), same protocol. 15's agent coordinates with the A1 completion
   chain (already merged).
5. C1 closeout, then three consecutive full-suite runs and the Package 5 close commit.

Wave gating exists to validate conventions early — Wave A's merge must be green before Wave B
launches. Lessons learned in a wave get appended to §9 and fed to the next wave's prompts.

## 8. Validation gates (every phase)

1. `dotnet build FightersThroughTime.csproj --nologo` — only the vendored CS8632 warning.
2. `dotnet test FightersThroughTime.csproj --settings .runsettings` — verify `Total:` against the
   current baseline (417 at plan time + additions; a ~24 total = launch failure; a large partial
   total + negative exit = corruption signature; a shutdown-only fault after a clean scene run =
   known signature 3).
3. Headless import `--quit` after any scene/resource change.
4. DustEconomyTests must never change (the model is locked; content conforms to it).
5. Story/Fighter isolation: no level code touches `scripts/FighterSim/` or Fighter loadouts.

## 9. Deviations

**A2: two PlayerController hooks, not one, both routed through a shared registry.**
§3 A2 allows `EnvironmentGravityScale` plus "the move-multiplier application if
`MovementDampenerZone` needs one". Both were needed and both landed as one-line Story-only
properties on `PlayerController`: `EnvironmentGravityScale` (multiplied into `ApplyGravity`'s
`effectiveGravity`) and `EnvironmentMoveMultiplier` (multiplied into `EffectiveMoveSpeed`, which
already feeds every ground/air speed site). The status enum is untouched — deliberately: terrain
slow must stack with, and never evict, a real `TimeDilation`/`Root` application under the
newest-status-replacement rule, so reusing `StatusController.ApplyStatus` the way `ChronalRiftZone`
does was rejected for zones 2 and 8. Zones never write the hooks directly; they register with the
new `scripts/Environment/EnvironmentPlayerModifiers.cs`, which publishes the product of all live
sources per player, so two overlapping zones cannot cancel each other's restore on exit.
`tests/Unit/EraMechanicToolkitTests.cs` scans `scripts/FighterSim/` and fails if either hook name
appears there.

**A2: `PendulumAnchor` owns both halves of the rope-swing hand-off.** `PlayerController` pins the
player to `LedgeGrabPoint.HangPosition` once at grab time and zeroes velocity; the ledge jump-off
writes only `Velocity.Y`. Rather than change that tested shared path, the anchor re-pins its
occupant to the moving hang anchor every physics frame (`CarryOccupant`, default true) and, on
release, adds its own measured tangential velocity scaled by the exported `ReleaseLaunchAssist`
(default 1.0, capped by `MaxLaunchSpeed`). Set the assist to 0 for a dead-stop anchor. No new
player state; `CharacterState.LedgeHanging` is reused as specified.

**A2: `PathMovingPlatform` runs with `sync_to_physics = false`.** With it enabled, direct `Position`
writes do not land until a physics step, which broke deterministic advancement. Manual movement
inside `_PhysicsProcess` still gives Godot the transform delta it uses to estimate the body's linear
velocity for carrying a `CharacterBody2D`, and this matches the existing `RotatingPlatform`
convention. `ConstantLinearVelocity` is deliberately not set (it would double the carry); the public
`PlatformVelocity` is exposed for level scripts instead.

**A2: `EndpointWaitSeconds` applies at every waypoint,** not only the two path extremes — each
waypoint terminates a segment, and per-waypoint waits are what the Level 7/12/13 uses want.

**A2: two supporting classes ship alongside the eleven components.** `SequenceGlyph` (required by
the spec) and `ForcefieldBarrier` (required by the spec) have no template files of their own; they
are authored as children inside `SequenceLockTemplate.tscn` and `ShieldGeneratorTowerTemplate.tscn`,
keeping the new template count at exactly eleven.

**A2: `GravityFieldZoneTemplate.tscn` ships in static mode.** `CycleScales` is omitted from the
template so the C# default (empty, static `GravityScale = 0.35`) applies; level agents author the
cycling variant in the inspector. Cycling is covered by unit tests, not by the template.

**A2: `docs/PACKAGE5_CAMPAIGN_PLAN.md` was untracked at branch time.** The plan existed only in the
shared checkout's working tree, not in any commit, so this branch adds it as a tracked file. If the
orchestrator's checkout still has it untracked, the merge will need `git checkout` of that path (or
a pre-merge `git add`) before it will fast-forward.

### A1 — Level flow framework, completion chain, credits

- **A1: `save_campaign_complete` was not needed.** `MainMenu.SaveSlotSummary` already renders
  `IsCompleted` through the existing `save_completed` key ("Campaign Complete", en.csv line 320).
  No new banner key was added; the A1 tests assert `save_completed` resolves instead.
- **A1: the level-10 pool config file keeps its numeric name.** Plan §2.2 implies a
  `level_10_globe_pools.tres`; the file on disk is `level_10_pool_config.tres` with
  `ConfigID = "level_10_globe_pools"` (all fourteen configs use `level_NN_pool_config.tres`).
  `CampaignRouteTests` asserts the **ConfigID**, not a filename, so the Globe/London tripwire
  still fires without renaming a Package 4 resource.
- **A1: hub label key for level 10 is `campaign_level_globe`.** The `CampaignLevel` enum member is
  still `London`; the key follows the manifest/pool "globe" naming, not the enum name.
- **A1: the base class owns the camera, and it is a `StoryCameraConfiner`, not a plain `Camera2D`.**
  Florence builds a bare `Camera2D`. Because rooms are built before the player spawns,
  `BuildRoomTransition` records its triggers and the base back-fills `CameraPath` right after the
  confiner is created (`LinkRoomCameras`). Level agents therefore do **not** set `CameraPath`.
- **A1: generic door label key.** `BuildDoor` defaults to a new `level_door_locked` key rather than
  Florence's `florence_door_locked`; pass your own era key when the door deserves flavour text.
- **A1: portal gating was extracted to a pure function.** `HubWorldController.ResolvePortalAction`
  (+ `HubPortalAction`) so the completed-campaign rule is testable without instantiating the hub
  scene. `_Input` is the only caller.
- **A1: the campaign-completion entry point is a Node, not a static call.** The chain has to wait on
  an EventBus dialogue callback, so `CampaignCompletionSequence : Node` owns the subscription
  lifetime. Level 15 calls the static factory `CampaignCompletionSequence.Begin(host, dialogue,
  endingDialogueID, returnToMainMenu)`. The save write goes through the new
  `SaveManager.MarkCampaignCompleted()`; nothing outside SaveManager touches `IsCompleted`.
- **A1: `Callable.From(...).CallDeferred()` instead of `CallDeferred(MethodName.X)`** in the base
  class. Subclasses may be constructed in code (tests, future tooling) where a bound script method
  name is not guaranteed.
- **A1: test-suite baseline moves 417 → 446** (+29: 7 `CampaignRouteTests`, 13
  `CampaignCompletionTests`, 9 `StoryLevelControllerBaseTests`). `AGENTS.md`'s baseline sentence is
  left for C1 to update in one place rather than being edited by every Package 5 worktree.
- **A1: GdUnit runs are unreliable while sibling worktree agents are also running `dotnet test`.**
  Concurrent runs produce `GodotRuntimeTestRunner ends with exit code: -1` /
  `The server returned an unexpected status code` / `Connection timeout`, nondeterministic partial
  totals, and even `ResourceLoader.Load` returning null in unrelated suites — the same shapes
  CLAUDE.md attributes to heap corruption. Before treating a partial total as a real regression,
  check for other `Godot_*.exe` processes and re-run. Force-killing a hung Godot mid-run can also
  leave `.godot/imported/` inconsistent; re-run `--headless --import` before believing the next
  failure.
