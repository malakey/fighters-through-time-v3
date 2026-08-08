# Package 5 — Campaign levels 2–15: implementation plan

Status: authored 2026-08-07. Working plan for `IMPLEMENTATION_PLAN.md` Package 5. Shared reference
for the implementation passes; agents record deviations in §9 at the bottom.

**Phase A completed and merged 2026-08-07** (A2 `7507533`: 14 era-mechanic components + 11 templates
+ 17 tests; A1 `7898e4f`: `StoryLevelControllerBase`, credits/campaign-completion chain,
`IsCompleted` writers, post-campaign hub portal, `Level_10_Globe` route fix, 29 tests). All 46 new
tests and the 139-test ContentValidation sweep pass on merged main.

**Test gate UNBLOCKED 2026-08-08 (`74cc12d`); waves may launch.** The suite is now
**465/465, verified over three consecutive runs at ~12 s**. The earlier instability was three
stacked faults, none of them a suite-size threshold (that theory was wrong, as was the
"excluding the A1 test classes is green" bisect — a 2-run green is not evidence on this signature):
a leaked `SceneTree.Paused` froze GdUnit's own transport node (a real in-game bug: a scene change
during a pausing dialogue or an open pause menu loaded into a frozen tree), double-disposed
C#-scripted `RefCounted` resources tripped `mono_object_disposed_baseref`, and a verbose-stdout
race killed the runner. See §9 `FIX:` and CLAUDE.md failure signatures 2 and 4.

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
8. **Native-crash hygiene — these four rules are mandatory; violating them corrupts the whole
   suite for everyone, and the crash never points at its cause.** (Learned the hard way; see §9
   `FIX:` and CLAUDE.md signatures 2 and 4.)
   - **Never call `Dispose()` on a Godot `Resource`.** Reference counting owns them. `Free()` the
     node; leave its resources alone.
   - **Load authored `.tres` data through `FTT.Core.AuthoredResources.Load<T>()`**, never
     `ResourceLoader.Load`/`GD.Load` — this covers every `EnemyData`/`BossData`/`AbilityData`/
     dialogue resource a level pulls in. `AuthoredResourceLoadRuleTests` fails with file:line if
     you regress. Scenes, textures, audio, and pooled objects must NOT go through it.
   - **Whoever sets `SceneTree.Paused` releases it in `_ExitTree`**; tests restore it in `finally`.
   - **On a large partial `Total:`, read `<user data>/Godot/app_userdata/.../logs/godot.log`
     first** — the runner only prints an exit code. Do not bisect by test class on this signature,
     and never call a subset clean on fewer than 5 runs.
   The earlier Florence/Hub shutdown crash (old signature 3) is FIXED and merged; a new level
   showing that stack is a regression, not a known issue.
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
are in `resources/Pools/level_pool_configs/`. **Boss HP and range numbers live in §4.1, transcribed
from the authored `.tres` files; any boss number in the prose column below is flavour, not spec.**

| Lvl | Scene / LevelID | S/E/B/X | Era roster (S; E) | Era identity (mechanics to use) | Boss + arena notes |
|---|---|---|---|---|---|
| 2 | `Level_02_Orleans.tscn` / `level_02_orleans` | 8/0/1/3 | laser_archer, cyber-mix; — | Siege assault: 2× `ShieldGeneratorTower` + forcefields gating progress; mortar `StoryCyclicHazard` lanes | siegemaster_duke; battlement arena, 2–3 stone platforms |
| 3 | `Level_03_Chicago.tscn` / `level_03_chicago` | 8/0/1/3 | voltaic_shock_drone, cultist mix; — | Beam-routing puzzle room (BeamEmitter/ConductiveCoil/BeamReceiver — exists) gating the fairground; coil-discharge cyclic hazards | chronal_inventor; flat metallic stage, two coil side platforms |
| 4 | `Level_04_Paris.tscn` / `level_04_paris` | 10/0/1/3 | chrono_rioter, cultist mix; — | 2–3 `SearchlightZone` (ultimate-drain mode) corridors; 2× `RescuableNPC` prisoners behind `DestructibleBlock` locks opening the courtyard | revolutionary_tribunal (summoner, knockback-immune like the rest of the roster); two drawbridge walkways over a pit, room for adds |
| 5 | `Level_05_Titanic.tscn` / `level_05_titanic` | 12/1/1/4 | chrono_slasher; tech_enforcer (cultist-only) | Act I finale. `RisingWaterZone` escalating per room, listing-deck slopes (angled floors), `level_05.preboss` beat | tidal_eraser; arena floods in phase 2 via `RisingWaterZone` hook |
| 6 | `Level_06_Pompeii.tscn` / `level_06_pompeii` | 10/0/1/3 | shock_shield_legionnaire, cultist mix; — | `EscapeSequenceController` lava-front room; counterweight/weight puzzle (exists) clearing rockfall; 2× `RescuableNPC` civilians; ash `StoryCyclicHazard` geysers | vulcan_decimator; slanted rocky slopes, two narrow ledges |
| 7 | `Level_07_Nassau.tscn` / `level_07_nassau` | 10/1/1/3 | laser_pistol_deckhand; overcharged_cannon_master | Ship-to-ship: `PendulumAnchor` rope swings between deck segments, `PathMovingPlatform` boarding skiffs, mortar cyclic hazards | dread_admiral; burning deck, two wooden yard platforms |
| 8 | `Level_08_Egypt.tscn` / `level_08_egypt` | 10/0/1/3 | plasma_spear_ward, cultist mix; — | Surface dunes with `MovementDampenerZone` deep sand; tomb section with `SequenceLock` hieroglyph puzzle gating the palace; `level_08.postboss` = the authored Cleopatra script (design 3368–3374) | jackal_priest (teleporter); sandy floor, two sarcophagi platforms |
| 9 | `Level_09_Berlin.tscn` / `level_09_berlin` | 12/1/1/3 | infrared_border_sentry; neural_mech_walker | `SearchlightZone` (strike mode, 1.5 s grace) stealth corridors; guard-tower vertical climbs; snow-tinted graybox | iron_chancellor; flat snowy street, two tower balconies |
| 10 | `Level_10_Globe.tscn` / `level_10_globe` | 10/0/1/3 | holo_page, cultist mix; — | Stage machinery: `TrapdoorPlatform` stage floor, `PendulumAnchor` between gallery tiers, idle-punish audience hazard (cyclic hazard with reset-on-move trigger zone) | tragedy_king (illusion summoner); open wooden stage, two gallery balconies |
| 11 | `Level_11_Gettysburg.tscn` / `level_11_gettysburg` | 12/1/1/3 | laser_rifle_infantry; cyber_cavalry_commander | Linear battlefield assault: artillery-line cyclic hazards (2 s telegraph) + cover geometry; 2× `ShieldGeneratorTower` arrays to destroy | siege_cannon (12 m ranged band — the widest in the roster, WIDE arena); dirt path, rail-fence platforms |
| 12 | `Level_12_Lunar.tscn` / `level_12_lunar` | 14/2/1/4 | vacuum_digger; void_enforcer | Act II finale. Level-wide `GravityFieldZone` low gravity; vacuum vents as cyclic hazards; high-altitude platforming with `PathMovingPlatform`; `level_12.preboss` = the authored Sarah script (design 3376–3383) | gravity_overseer (3 phases); orbital pad arena |
| 13 | `Level_13_ChronalVoid.tscn` / `level_13_chronal_void` | 8/1/0/2 | rift_phantom + cultist mix; chrono_guard_elite | Era-mashup floating platforms; cycling `GravityFieldZone` shifts (telegraphed); `ChronalRiftZone` pockets. NO standard boss row — the encounter is `MirrorParadoxEncounterController` (dust 50 comes from it; economy table's B=0 EXCLUDES the mirror's 50, matching the locked 52-full/36-expected row — verify against DustEconomyTests before changing anything) | mirror_paradox; symmetric Fighter-like arena, flat + two platforms |
| 14 | `Level_14_NeoEarth.tscn` / `level_14_neo_earth` | 14/2/1/3 | chrono_slasher (+hologram_drone); tech_enforcer | Apex lab: laser-grid corridors (cyclic hazards in authored patterns), anti-grav containment `GravityFieldZone` pockets, metallic graybox | archive_prime (3 phases); security-core arena whose laser grid hazards sync to boss phase via `BossPhaseChanged` |
| 15 | `Level_15_Alexandria.tscn` / `level_15_alexandria` | 12/2/1/3 | chrono_slasher; tech_enforcer, chrono_guard_elite | Burning library assault; `EscapeSequenceController` reversed as advancing-restoration beat if useful; post-boss **Temporal Core insertion** interactable at the Prime Anchor → `level_15.ending` (authored script, design 3389–3399) → credits → `IsCompleted` → MainMenu (A1 chain). No standard results/hub-return flow | apex_eraser (3 phases, 1200 HP — the roster ceiling); grand library arena |

### 4.1 Authored boss stats — `resources/Bosses/*.tres` is authoritative

**Read the resource, not this table, and never edit a boss resource from a level branch.** These
numbers are transcribed from the Package 4 `.tres` files as of the Wave A integration and are
reproduced here only so wave agents stop re-deriving them: all four Wave A agents independently
discovered that the design doc's boss list (which seeded this plan's prose) was superseded by
Package 4 and logged the same finding. Act I HP is pinned by `BossRosterActITests` (band 500–700,
strictly ascending by level, **exactly one** phase threshold), so "fixing" a resource to match older
prose fails Package 4 content tests.

Arena sizing: `BossController.PixelsPerUnit` is **60**, so a ranged band of *N* units needs
*N* × 60 px of usable floor. Assert `RangedRangeThreshold * 60 < arenaWidth` in your content test
rather than hardcoding a width — re-tuning the boss then cannot silently outgrow the arena.

| Lvl | BossID | MaxHP | Melee / Ranged (units) | PhaseThresholds (phases) | AttackPattern | KnockbackImmune | Dust |
|---|---|---|---|---|---|---|---|
| 1 | `borgia_inquisitor` | 500 | 3.0 / 7.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 2 | `siegemaster_duke` | 540 | 3.5 / 9.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 3 | `chronal_inventor` | 560 | 2.5 / 10.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 4 | `revolutionary_tribunal` | 590 | 3.0 / 9.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 5 | `tidal_eraser` | 640 | 3.5 / 10.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 6 | `vulcan_decimator` | 660 | 4.0 / 9.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 7 | `dread_admiral` | 700 | 3.0 / 11.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 8 | `jackal_priest` | 700 | 3.0 / 8.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 9 | `iron_chancellor` | 780 | 3.5 / 9.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 10 | `tragedy_king` | 800 | 3.0 / 8.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 11 | `siege_cannon` | 880 | 3.0 / 12.0 | `[0.5]` (2) | `DistanceBased` | yes | 50 |
| 12 | `gravity_overseer` | 950 | 3.0 / 9.0 | `[0.66, 0.33]` (3) | `DistanceBased` | yes | 50 |
| 13 | `mirror_paradox` | 1000 | 2.0 / 5.0 | `[]` (1) | `Sequential` | **no** | 50 |
| 14 | `archive_prime` | 1050 | 3.0 / 10.0 | `[0.66, 0.33]` (3) | `DistanceBased` | yes | 50 |
| 15 | `apex_eraser` | 1200 | 3.0 / 10.0 | `[0.66, 0.33]` (3) | `DistanceBased` | yes | 50 |

Corrections this table supersedes, for anyone reading an older draft or an agent brief derived from
one: `siegemaster_duke` is 540/3.5 (not 650/4.0); `chronal_inventor` 560/2.5 (not 800/3.5);
`revolutionary_tribunal` 590 HP, one threshold, `DistanceBased`, knockback-**immune** (not 900,
`[0.66, 0.33]`, `Sequential`, vulnerable); `tidal_eraser` 640/3.5/10.0 (not 1050/5.0/12.0);
`siege_cannon`'s ranged band is 12 units, not 15; `apex_eraser` is 1200 HP, not 3000.

`mirror_paradox` is the outlier by design: it is driven by `MirrorParadoxController`, not
`BossController`, its `BossAbilities` export is deliberately **absent** (an empty script-typed array
in a `.tres` corrupts the .NET heap — §2.8), and its 50 dust is awarded by
`MirrorParadoxEncounterController`.

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

**FIX: the suite-breaking child-process death was three independent faults, only one of them
introduced by Phase A.** After A1/A2 merged, `dotnet test` stopped completing: the GdUnit Godot
child either hung to the 300 s `TestSessionTimeout` or died with a negative exit code, always
reporting a large partial `Total:` with 0 failures. Bisecting by test class pointed at A1's three
new suites, but that was a false lead — the 434-test subset that "passed" also died on run 2 of 3
once it was run enough times. Three separate faults were stacked:

1. **A leaked `SceneTree.Paused` (A1's fault, deterministic, caused the hang).**
   `CampaignCompletionTests.TheChainWaitsForTheEndingDialogueBeforeRollingCredits` starts
   `level_01.exit`, which is authored `PausesGameplay = true`, then completes it by raising
   `EventBus.RaiseDialogueComplete` directly — bypassing `DialogueManager.EndSequence()`, the only
   code that ever hands the pause back — and then frees the manager. `SceneTree.Paused` stayed true
   for the rest of the process. GdUnit4's transport back to the .NET test host is a plain `Node`
   (`GdUnitTcpClient` in `GdUnitTestRunner.tscn`) with the default *Inherit* process mode, so its
   `_process` stopped pumping and the harness never heard from the child again.
   *Fixed in production code, not the test:* `DialogueManager._ExitTree` (and now
   `PauseMenu._ExitTree`) release the pause they took. This was a real shipping bug — any scene
   change while a pausing dialogue or the pause menu was open would have loaded the next scene into
   a permanently frozen tree. `CampaignCompletionTests` now samples, clears, and asserts the pause
   flag, and a new case,
   `ADialogueManagerTornDownMidSequenceHandsBackTheGameplayPause`, pins the contract (suite total
   463 -> 464).

2. **A double-disposed script instance (pre-existing, caused `-1073741795`).** The crash is
   `CRASH_COND(gchandle.is_released())` in `mono_object_disposed_baseref` — Godot's assert that a
   C#-scripted `RefCounted` is not disposed twice. On this MinGW build that trap is `ud2`, i.e.
   exit `0xC000001D`. Two sources fed it:
   *Explicit `Dispose()` on Godot `Resource` objects* in `BossControllerTests`,
   `EnemyAbilityExecutorTests`, `ContentTemplateContractTests`, and `PoolManagerTests` (20 call
   sites). `FreeBoss` disposed `boss.Data` and the caller then disposed the same
   `EnemyAbilityData` instances that `BossData.BossAbilities` had already released. All removed —
   Godot's reference counting owns Resource lifetime.
   *Authored `.tres` graphs cycling through the resource cache.* Loading a scripted resource,
   reading it, and dropping the only reference destroys the whole graph — including every scripted
   sub-resource behind an `Array[ExtResource(script)]` export — and rebuilds it on the next call.
   The .NET finalizer thread reaps those wrappers at unpredictable times, racing the main thread
   that is destroying the same native objects. New `scripts/Core/AuthoredResources.cs` pins each
   authored data resource for the process lifetime; every production and test load site
   (about 100) now goes through it.
   Symptoms before the fix included unrelated `ResourceLoader.Load` returning null and
   `tesla_grid.tres` reading back with zeroed node values.

3. **A GdUnit4 stdout race (pre-existing, caused `-532462766`).** `.runsettings` passed Godot the
   verbose engine flag, so ~2,100 "Loading resource:" lines per run went through
   `GdUnit4.Core.Hooks.WindowsStdOutHook.ReadPipeOutput`, whose pipe-reader thread calls
   `System.Console.Write` while the hook swaps `Console.Out` at test boundaries. It threw
   `ArgumentNullException` on a background thread and took the process with it (`0xE0434352`). The
   flag is removed from `.runsettings`; errors and warnings still log.

Measured after all three: 464/464 across consecutive full runs. **Level agents must not
reintroduce any of these:** never call `Dispose()` on a Godot `Resource`; load authored `.tres`
data through `FTT.Core.AuthoredResources`; and if you set `SceneTree.Paused`, release it in
`_ExitTree` as well as on the normal path.

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

### Wave A — Level 2, Orléans 1429

- **L02: the boss resource does not match the dossier's prose numbers, and the resource wins.**
  §4 describes `siegemaster_duke` as "2 phases, 650 HP, melee 4.0 m / ranged 9.0 m". The authored
  `resources/Bosses/siegemaster_duke.tres` is **540 HP** with `MeleeRangeThreshold = 3.5` (ranged
  9.0 and the two phases do match). Nothing was changed — Package 4 owns that resource and
  `resources/**/*.tres` outranks prose. The arena was sized against the resource: 1,600 px of boss
  court, against 540 px for the 9.0 m band at `BossController`'s 60 px/unit.
- **L02: gate geometry is coupled to the template's fixed barrier offset, and that coupling is
  now pinned by a test.** `ShieldGeneratorTowerTemplate.tscn` hangs its `Barrier` child at local
  `(320, -48)` with a 320 px pane, and overriding a property on an instanced scene's child needs
  a fragile `index=` block. So instead each generator is authored exactly
  `Level02Controller.BarrierLocalOffsetX` west of the gate it seals, and
  `Level02ContentTests` asserts the scene positions against `GateAX`/`GateBX`. A 320 px pane also
  does not reach a ceiling, so each gate gets a solid arch above the barrier's span — without it
  both gates are simply jumped and the era mechanic is decorative. Later levels reusing this
  component (11 Gettysburg) inherit the same constraint.
- **L02: checkpoint resume force-destroys the generators rather than restoring barrier state.**
  `_checkpoint_2` sits past both gates, so a resumed run would find the route re-sealed *behind*
  the player and could not backtrack. `MarkWavesClearedThrough` calls
  `TakeEnvironmentDamage(MaxHP)` on the towers ahead of that checkpoint: the barriers drop through
  the same path the player's attacks take, the `TowerDestroyed` signal fires once, and the
  objective counter stays honest. No bespoke barrier-restore state was added.
- **L02: the base class does not confine the camera on a resumed run.** Room triggers sit behind a
  player restored at a mid-level checkpoint, so `StoryCameraConfiner` keeps whole-level bounds
  until the next room boundary. `Level02Controller.OnLevelReady` re-applies the containing room's
  `CameraBounds`. If every wave-A level needs this, it belongs in `StoryLevelControllerBase` at C1
  rather than being copy-pasted fourteen times.
- **L02: room camera bounds are floored at 1,920 px.** A confiner narrower than the reference
  viewport produces a useless clamp, so the 1,600 px boss court is confined to a 1,920 px window
  starting 320 px west of the room. The content test asserts the floor for every room.
- **L02: `--headless --quit` does NOT regenerate `localization/en.en.translation`.** Only
  `--import` (or the editor) reimports a changed `en.csv`. A level agent that adds keys, runs the
  import *check* and commits will ship a stale compiled translation — every new key then renders
  as its raw key at runtime, and the `TranslationServer.Translate(key) != key` assertion every
  roster/localization suite uses will fail. Run `--headless --import` **after** editing `en.csv`,
  and commit the regenerated `en.en.translation` with it.
- **L02: first full-suite run reported `Total: 24` with `Starting GodotRuntimeExecutor failed` /
  `Connection timeout` / `exit code: -1`, with no stray `Godot_*.exe` alive.** The immediate
  re-run was **475/475, 0 failed, 14 s**. This is the A1 concurrent-worktree shape above, not
  signature 1 (`GodotSharp` was present and the next run was full) — worth noting that it can
  present with a *clean* process table, so "no stray Godot" is not evidence against it.
- **L02: test-suite baseline moves 465 → 475** (+10 `Level02ContentTests`). Left for C1 to fold
  into `AGENTS.md` in one edit.
- **L02: note for later wave agents** — `tests/Unit/StoryLevelControllerBaseTests.cs` already
  contains an internal `FrameworkTestLevelController` that reports `LevelID = "level_02_orleans"`
  and registers the same three checkpoint IDs. It is a test-only stand-in for the A1 framework and
  does not collide with the real scene, but a grep for the level id returns it first.
### Wave A — Level 3, Chicago 1893

- **L03: the Chronal Inventor is 560 HP with a 2.5-unit melee band, not the dossier's 800 HP /
  3.5 m.** `resources/Bosses/chronal_inventor.tres` reads `MaxHP = 560`,
  `MeleeRangeThreshold = 2.5`, `RangedRangeThreshold = 10.0`, one phase threshold (two phases),
  `AttackPattern = DistanceBased`. Resources beat prose, so nothing in the boss resource was
  touched; the arena was sized to the resource instead. `BossController.PixelsPerUnit` is 60, so
  the 10-unit ranged band is 600 px and the Court of Honor room is 1,920 px wide.
  `Level03ContentTests` asserts the band fits rather than asserting an HP number.
- **L03: room transitions are authored as facing pairs.** `RoomTransitionTrigger.ActivateOnce`
  defaults true, so a single forward trigger per seam leaves the camera clamped to the last room
  forever once the player backtracks. Level 3 adds `BuildRoomTransitionPair`: two repeatable
  triggers 160 px apart straddling each of the first two seams (their 80 px bodies never overlap),
  giving real bidirectional confinement. The boss seam stays a single one-shot trigger on purpose —
  that clamp *is* the arena lock. This is level-local; the shared base was not changed.
- **L03: the beam chain is authored in the `.tscn`, not built in `BuildLevel`.**
  `PowerRoutingNode` resolves its `OutputPaths` during `_Ready`, so a code-built chain only wires
  up if nodes are added strictly downstream-first. Godot readies a packed scene's children
  bottom-up, so scene authoring gets the dependency order for free and keeps the level a thin
  scene per §2.1. Node order in the file is still downstream-first for readability.
- **L03: mis-routing is expressed with dead-end "crowd tap" routing nodes, not an inverted
  condition.** `BeamReceiver` can only set a `PuzzleManager` condition *true* when powered; there is
  no fail/inverted condition in the toolkit and adding one would mean editing shared code. Instead
  the wrong coil orientations feed bare `PowerRoutingNode` taps whose `PowerChanged` arms a
  `StoryCyclicHazard` over the spectator stands — the design's "route it away from the crowds"
  fantasy, and the read-plan-execute tell, with no toolkit change. Note the taps settle during
  their own `_Ready`, i.e. before the level root's `_Ready`, so `Level03Controller` syncs the
  initial hazard state explicitly right after subscribing; subscribing alone misses the first
  emission.
- **L03: the level is 1,600 px tall, not Florence's 1,080.** The Electricity Building climb is the
  required vertical section and needs the headroom. Ground-level rooms therefore confine to a
  bottom-anchored 1,080-tall camera window (`GroundRoomCameraTop`) while the climb room uses the
  full height.
- **L03: the Court of Honor door is 720 px tall, not the `BuildDoor` default 400.** The routing
  hall's gallery catwalk sits at y=960; a Florence-height door could be cleared from it, skipping
  the puzzle entirely.
- **L03: the content-test fixture clears the persisted puzzle flag on setup.**
  `PuzzleManager.PersistCompletionToSave` is true (required — the plan asks for completion to
  survive a checkpoint resume), which means one case solving the puzzle would open the door for
  every later case. `Level03Fixture` forces `SetPuzzleCompleted(level_03.beam_routing, false)` at
  construction and restores the original value in `Dispose`. Later level agents authoring a
  persisted puzzle need the same guard.
- **L03: resuming at `_checkpoint_2` force-opens the routing door regardless of the puzzle flag.**
  The pre-boss checkpoint stands past the gate, so a resume that lost the flag would reload the
  player behind a sealed door. `OnLevelReady` opens it when either the puzzle is complete or the
  resume checkpoint is the pre-boss one, and a test pins the behaviour.
- **L03: suite total 465 → 479** (+14 `Level03ContentTests`), verified with the full suite at
  479/479, 0 failed. `AGENTS.md`'s baseline sentence is left for C1 per the A1 precedent.
### Wave A — Level 4, Paris 1789

- **L04: the Wave A prompt's boss numbers do not match the authored resource; the resource wins and
  was not touched.** The prompt described `revolutionary_tribunal` as 900 HP, thresholds
  `[0.66, 0.33]`, `Sequential` pattern, and `IsKnockbackImmune = false`. On disk it is **590 HP, a
  single threshold `[0.5]`, `AttackPattern = 1` (`DistanceBased`), and `IsKnockbackImmune = true`**.
  Three of those are load-bearing: `BossRosterActITests` pins the Act I health band to 500-700
  strictly ascending by level and asserts **exactly one** phase threshold for every Act I boss, so
  editing the resource to match the prompt would fail Package 4 content tests. Plan §4 also says
  levels build arenas and wiring only. Level 4 therefore wires the resource as authored; if the
  larger, three-phase, knockback-vulnerable Tribunal is actually wanted, it is a Package 4 resource
  change with its own test updates, not a level edit.
- **L04: the boss arena's back wall is deliberately partial.** Florence seals its boss room with a
  full-height wall at the room's start offset. Copying that here would wall the player out of the
  cell block, because Paris's courtyard is entered by dropping into the pit rather than through a
  door at the room edge. The west wall spans y 240-700 only; the 700-1000 band stays open as the
  entry drop. The east wall is full height.
- **L04: the arena pit floor is a floor, not a hazard.** `revolutionary_tribunal` summons
  `chrono_rioter` pairs, and adds need a continuous surface to land on and path along, so the
  "central lower pit" under the two drawbridge walkways is a flat 1600 px floor 100 px below the
  approach.
- **L04: the base class does not confine the camera to the room the run starts in.**
  `BuildRoomTransition` only fires when the player crosses the trigger, so a checkpoint resume drops
  the player past the trigger behind them and keeps whole-level limits. `Level04Controller`
  `OnLevelReady` calls `trigger.ActivateRoom(Player)` for whichever authored room's `CameraBounds`
  contains the spawn X. Other level agents will hit this; it is a candidate for the base class at
  C1 rather than four copies.
- **L04: `PuzzleManager` never re-emits `PuzzleCompleted` for a completion it restored from the
  save**, so a resume at `level_04_paris_checkpoint_2` with the prisoners already freed would find
  the courtyard gate still solid and nothing left to open it. The controller checks
  `PrisonerPuzzle.IsCompleted` in `OnLevelReady` and opens the gate directly. Florence solves the
  same problem with a deferred `ApplySavedPuzzleState`; pinned here by
  `ResumingPastTheCellsWithTheGateAlreadyEarnedDoesNotSoftLock`.
- **L04: the cell locks are `DestructibleBlockTemplate` instances scaled `Vector2(1, 3)`.** The
  template's shape is a fixed 96x96 sub-resource, too short to seal a doorway; scaling the instance
  gives a 96x288 barrier that meets the cell's ceiling slab. Gating is physical — the prisoner's
  120 px interaction area sits ~200 px behind the lock, so it is unreachable until the lock breaks.
  No `RescuableNPC` "locked" flag was added, because `ApplyPresentation` re-enables the interaction
  area on every rewind and a controller-driven flag would fight it.
- **L04: `SearchlightZone.PlayerDetected` drives an HUD alarm objective, not an alarm wave.** The
  10-standard budget is locked, and the plan's alarm-wave suggestion cannot be honoured without
  breaking it.
- **L04: the content-test fixture always resumes at a checkpoint.** A fresh entry defers the
  entrance dialogue, which is authored `PausesGameplay = true`; if that deferred call lands it leaks
  `SceneTree.Paused` and hangs the session (§9 fault 1). The fixture also restores the pause flag in
  `Dispose` and calls `PoolManager.ReleaseActiveInGroup("Enemies")` before freeing the level,
  because pooled enemies parented to a freed level would otherwise stay in `pool.Active` as dead
  references. Any level agent instantiating a real level scene in the tree needs both.
- **L04: suite baseline 465 -> 476** (+11 `Level04ContentTests`), verified at 476/476, 0 failed.
  `AGENTS.md` is left for C1 per the A1 convention.
- **L04: a fresh worktree's `--headless --import` rewrites ~24 tracked `.import` files** with new
  cache hashes and a `gdunit4` -> `gdUnit4` source-path casing change. Unrelated to level content;
  left unstaged rather than committed.
### Wave A — L05 Titanic (Act I finale)

- **L05: `tidal_eraser.tres` disagrees with this plan's §4 prose and the agent brief; the resource
  won.** On disk the boss is `MaxHP = 640`, `MeleeRangeThreshold = 3.5`, `RangedRangeThreshold =
  10.0`; the brief said 1050 HP and 5.0 m / 12.0 m. Phase count (one threshold at 0.5, so two
  phases) and `AttackPattern = DistanceBased` do match. Per AGENTS.md the resource is authoritative,
  so nothing was edited. The stern arena is 2160 px, sized against the resource's real band
  (10.0 × 60 px/unit = 600 px) with headroom for the brief's wider 12 m reading, and
  `Level05ContentTests` asserts `RangedRangeThreshold * 60 < arenaWidth` rather than hardcoding a
  number — so re-tuning the boss cannot silently outgrow the arena.
- **L05: two non-overlapping `RisingWaterZone`s, not one.** `HullFlood` covers x 0–8800 and
  `ArenaFlood` covers 8800–11200, and their Areas deliberately do not meet.
  `EnvironmentPlayerModifiers` publishes the *product* of every live source (A2 deviation above), so
  two water zones over one player would stack to a 0.30x crawl and double the drowning ticks. Levels
  6/7 and anything else combining two zone components of the same family should assume the same
  constraint.
- **L05: one global water line is enough to model a listing ship.** Decks rise sternward, so a
  single line drowns the bow while the stern is still dry — no per-room water offsets needed. The
  list itself is stepped `BuildFloor` slabs joined by rotated ramp slabs
  (`BuildListingRamp`: `BuildFloor` + `RotationDegrees`, −12° to −16°); a fully rotated floor plan
  produced collision seams at the joins.
- **L05: the hull's timed escalation uses an owned `Timer`, not `RisingWaterZone.AutoAdvanceSeconds`.**
  The component latches `_autoTimer = AutoAdvanceSeconds` in `_Ready`, so a zone built with 0 has an
  already-expired timer: assigning `AutoAdvanceSeconds` mid-level fires a step on the very next
  physics frame. That would double-advance on top of the room's authored step. The **arena** flood
  deliberately does use `AutoAdvanceSeconds`, because there the instant first step *is* the phase-2
  beat. If a later level needs a genuinely deferred auto-advance, the component needs a public timer
  reset.
- **L05: checkpoint water restoration is structural, not just an authored table.**
  `MarkWavesClearedThrough` calls `RestoreFloodForCheckpoint`, which sets the authored per-checkpoint
  step (`_0`→0, `_1`→2, `_2`→3) and then *walks the step back down* while the registered respawn
  position is still at or below the water line, and resets the arena flood to disabled/step 0. The
  "never resume underwater" guarantee therefore survives someone re-authoring the deck heights
  without touching the table. Two tests pin it: every checkpoint resumes dry, and a level flooded
  past every authored step still restores to a dry `_2`.
- **L05: the flood zones are code-built rather than `RisingWaterZoneTemplate.tscn` instances.**
  The template ships a 1920-wide collision shape and visual; both zones need different widths, and
  overriding a `SubResource` shape on an instanced scene risks mutating a sub-resource shared
  between the two instantiations. Graybox-in-code is the sanctioned Florence convention (§2.1). The
  `.tscn` still carries authored template content: `StoryDropSystem` plus three
  `CyclicHazardTemplate` ruptured steam pipes.
- **L05: the level is 1400 px tall, not Florence's 1080.** Four stacked deck bands (1300 → 800) are
  what make the flood readable as a climb. `LevelBounds`/`StoryCameraConfiner` handle it with no
  changes; room triggers are authored at `triggerSize = (80, 1400)` so they still span the full
  height.

#### Tooling notes for later wave agents

- **A fresh worktree's first `dotnet test` fails with `GodotRuntimeTestRunner ends with exit code:
  100` / `Failed to connect: Connection timeout` and `No test matches the given testcase filter`.**
  The adapter writes `gdunit4_testadapter_v5/GdUnit4TestRunnerScene.cs` *after* the build, so the
  first run's assembly has no such type. It is not a code fault: just `dotnet build` again (the file
  now exists) and re-run. Worth doing right after the initial `--import`.
- **The GdUnit pipe name `gdunit4-FightersThroughTime` is not worktree-scoped.** Concurrent
  `dotnet test` runs from sibling worktrees collide on it and both report the connection timeout.
  A collided run also leaves **two** live processes — one `Godot_..._console.exe` and one
  `Godot_....exe` — that must be killed before the next attempt, or it fails the same way. Confirm
  with `Get-CimInstance Win32_Process -Filter "Name LIKE 'Godot%'"` and check `CreationDate` before
  killing: the processes may belong to another agent mid-run.
- **`--headless --quit` does not rebuild `localization/en.en.translation`; `--headless --import`
  does.** Confirmed independently on this level. Re-import after touching en.csv and commit the
  regenerated `.translation` binary. The `--import` pass also rewrites eight
  `addons/gdUnit4/**/*.png.import` files with a case-only `gdunit4`/`gdUnit4` source-path flip —
  unrelated churn, revert with `git checkout -- addons/gdUnit4`.

### Wave A integration

- **INTEGRATION-A: the merged-Wave-A failure (514 total, 14 failed) was one real shipping bug — an
  unloading level leaks its pooled enemies into `PoolManager`.** Every failure died the same way:
  `ObjectDisposedException: 'FTT.Enemies.EnemyController'` from `Godot.GodotObject.GetPtr`, thrown
  by `Node.IsInGroup` inside `PoolManager.ReleaseActiveInGroup`.
  Cause: `EnemyFactory.Spawn` parents pooled enemies to the level, and nothing handed them back
  when the level went away. `PoolManager` kept the freed nodes in `pool.Active` forever. **This is
  not a test artifact** — the same thing happens in game on every level → hub → level transition:
  the dead entries permanently consume the pool's capacity, `RecycleOldest` then tries to recycle a
  freed node and dequeues from an empty inactive queue, and any group sweep throws.
  Each level class passed alone because it was the only pool user in the process; merged, level 2
  and 3 poisoned the pool that levels 4 and 5 later swept, and their fixtures then threw *before*
  reaching `Level.Free()` — which is why seven leaked level controllers stayed subscribed to
  `EventBus` and turned `StoryLevelControllerBaseTests.KilledEnemiesTally…` into 8 × 15 = 120 dust.
  `AerialComboUsesIndependentStateAndLandingResetDoesNotLeakIntoGroundCombo` was collateral from the
  same leaked scenes. Both victims are now green with no test edits.
  Fixed in production code on both sides:
  1. `StoryLevelControllerBase._ExitTree` calls the new `PoolManager.ReleaseActiveUnder(this)`.
     `Level00Controller` and `Level01Controller` do NOT extend the base and spawn through the same
     pooled `EnemyFactory` wrappers, so both got the same one-liner.
  2. `PoolManager` gained `ReleaseActiveUnder(Node)`, `PurgeInvalidActive()`, validity guards in
     `ReleaseActiveInGroup`/`ClearPool`/`ClearAllPools`, a purge on the exhausted-pool path in
     `SpawnFromPool`, and a fallback when `RecycleOldest` releases a node the pool no longer owns.
     A pool now self-heals instead of throwing when something frees a node behind its back.
  **Ancestry, not group, on purpose.** `ReleaseActiveInGroup("Enemies")` is global: it would also
  reclaim a *live* scene's enemies, and it only covers the one group. `ReleaseActiveUnder(this)`
  releases exactly the pooled objects parented under the level — enemies, their projectiles, loot,
  VFX, constructs — and cannot reach across scenes. Only one level is live at a time today, but the
  hub and the GdUnit fixtures are not, and the group form is what threw in the first place.
  The Level 4/5 fixtures still call `ReleaseActiveInGroup("Enemies")` before freeing; that is now
  redundant (the base does it) but harmless, and it was left in place.
- **INTEGRATION-A: resume camera confinement is now in the base class.** L02 and L04 each hand-rolled
  it. `StoryLevelControllerBase.ApplyResumeCameraBounds()` runs automatically between `BindEvents`
  and `OnLevelReady`, and re-applies the `CameraBounds` of the authored room containing the spawn
  point whenever `ResumedMidLevel` is true. Overlapping room bounds resolve to the **last** authored
  match (L02 has a 320 px overlap between its battlement and boss rooms and relies on that).
  It sets camera bounds *only* — it deliberately does not call `RoomTransitionTrigger.ActivateRoom`,
  because that re-runs the room's `onEntered` handler: on Level 5 that would re-raise the hull flood
  and respawn a wave straight on top of `RestoreFloodForCheckpoint`. A level that needs more than the
  camera (Paris marks its starting room crossed and enables its encounter root) uses the new
  `FindRoomTriggerContaining(x)` helper and calls `ActivateRoom` itself.
  L02's private `ApplyResumeCameraBounds` and L04's `ActivateRoomContaining` are deleted; behaviour
  is unchanged in both.
- **INTEGRATION-A: `PuzzleManager` now re-emits `PuzzleCompleted` for a completion restored from the
  save.** It read `SaveManager.IsPuzzleCompleted` in `_Ready` and set `IsCompleted` silently, so any
  save-persisted gate stayed shut on resume with nothing left to open it — L03 force-opened its
  court door and L04 checked `IsCompleted` explicitly to work around it. The emission is
  **deferred** (`Callable.From(AnnounceRestoredCompletion).CallDeferred()`): a packed scene's
  children ready before its root, so a synchronous emit in `_Ready` would reach no subscriber.
  It also publishes a `PuzzleStatePayload` with reason `"restored"`.
  Consequence for every later level agent: **`PuzzleCompleted` handlers must be idempotent.** All
  three existing gate openers already were (`OpenDoor` null-checks, `CourtyardGateOpen` guard,
  `OpenWorkshopDoor` validity check), so Florence, L03 and L04 are unchanged and their per-level
  guards are now belt-and-braces rather than load-bearing. Puzzles with
  `PersistCompletionToSave = false` are unaffected.
- **INTEGRATION-A: shared-file wiring applied for levels 2–5** (§2.7 / §7): four
  `ScenePoolCatalogEntry` rows in `resources/Pools/scene_pool_catalog.tres` mapping each scene to its
  `level_NN_pool_config.tres`; four paths added to `SceneSmokeTests.RequiredPrototypeScenes`; the four
  `StoryLevel` and four `DialogueSet` manifest rows flipped to
  `Implemented,ReadyForReplacement,Valid`. `ScenePoolConfigTests.CatalogMapsEveryCurrentGameplaySceneToItsPoolBudget`
  was extended to assert each new scene resolves to the right `ConfigID` (a mis-pointed row would
  otherwise leave a level warming another level's budget, silently). StoryLevel row count stays 16
  and levels 6–15 stay `Planned`/`Pending`, so
  `ContentManifestTests.PlannedResourcesRemainVisibleUntilTheyAreAuthored` still has warnings to find.
- **INTEGRATION-A: §4.1 added — the authored boss stat table.** All four Wave A agents independently
  found and logged the same contradiction between §4's prose and `resources/Bosses/*.tres`. The
  table is now transcribed from the resources for all 15 bosses so Waves B and C do not each burn a
  cycle rediscovering it, and the three wrong numbers still embedded in §4's prose column
  (`siege_cannon` 15 m, `apex_eraser` 3000 HP, `revolutionary_tribunal` "NOT knockback-immune") are
  corrected. No boss resource was modified.
- **INTEGRATION-A: suite total stays 514, 0 failed, across three consecutive runs.** No tests were
  added, weakened, or deleted; the 14 failures were fixed entirely in production code. Import clean;
  all four Wave A scenes plus Florence smoke clean at `--quit-after 300`.

### Wave B — Level 9, Berlin 1961

- **L09: the boss resource matches §4.1 exactly; nothing was re-derived.**
  `resources/Bosses/iron_chancellor.tres` reads 780 HP, melee 3.5 / ranged 9.0, one threshold
  (`[0.5]`, two phases), `DistanceBased`, knockback-immune, 50 dust — the §4.1 row as written. The
  bunker street is 1,920 px against the 540 px the 9.0-unit band needs at 60 px/unit, and
  `Level09ContentTests` asserts `RangedRangeThreshold * PixelsPerUnit < arenaWidth` rather than a
  hardcoded width. The boss resource was not touched.
- **L09: the stealth contrast with Paris is in the mode, and it is load-bearing.** Level 4 runs
  `SearchlightZone` in `UltimateDrain`: exposure is a resource tax a player can simply eat, and the
  answer is "keep moving". Level 9 runs the same component in `DelayedStrike`: exposure is silent
  for the 1.5 s `ExposureGraceSeconds` the design quantifies, then a 26-damage drone strike with
  knockback lands and re-arms every 1.5 s. Two tests pin the difference from the Berlin side —
  `ABeamStrikesOnlyAfterItsGracePeriodAndNeverInstantly` proves the beam does *not* hit on entry,
  does not hit at 1.4 s, does hit at 1.6 s, and never touches the Ultimate meter.
- **L09: cover in a searchlight gauntlet is positional, not occlusion, and the level is authored
  around that.** `SearchlightZone` is an `Area2D` cone; a wall between the player and the housing
  does not stop detection, and adding raycast occlusion would mean editing shared Wave A code. So
  the three rubble cover slabs are authored to sit in the gaps *between* the swept cone footprints,
  and `Level09Controller.SweptFootprint` computes the conservative world span an arc-swept cone can
  reach (`halfWidth·cos a + depth·sin a` from the template's 120×520 cone).
  `EveryCheckpointAndCoverPocketSitsOutsideEverySweptBeam` asserts every pocket and every checkpoint
  respawn against those footprints, so re-tuning a sweep arc cannot silently swallow a safe pocket
  or drop a resume into a live beam. Any later level reusing this component inherits the same
  constraint.
- **L09: the three sweep periods (2.8 / 3.9 / 5.1 s) are asserted distinct.** A shared period would
  collapse the corridor into one timing window instead of three interleaved ones, which is the
  whole reason the plan asks for desynced sweeps; the test fails on a duplicate.
- **L09: surveillance feeds are `DestructibleBlockTemplate` instances wired in the controller, not a
  new toolkit component.** `DestructibleBlock` has no `PuzzleManagerPath`/`ConditionID` export the
  way `RescuableNPC` does, and adding one is a shared-code edit. Each relay's `Destroyed` signal is
  bound in `CollectAuthoredNodes` to a `FeedLink` record that names its beam, so cutting one relay
  darkens exactly one light and satisfies exactly one condition. Unscaled 96×96 blocks with
  `HitsToBreak = 2` — they are cables, not the doorway-sealing locks Paris needed.
- **L09: darkening a beam releases the player it was counting down on.**
  `SearchlightZone.Enabled = false` stops the sweep and the exposure tick but leaves an already
  tracked player in `_exposure`, frozen mid-countdown; if the beam were ever re-enabled the strike
  would land instantly. `DarkenSearchlight` therefore also calls `RemovePlayer(Player)`, and defers
  the `Monitoring` flip (`SetDeferred`) because it runs inside an `Area2D` hit callback. Pinned by
  `CuttingAFeedRelayPermanentlyDarkensTheBeamItPowers`.
- **L09: resuming at checkpoint 2 blows the relays rather than just opening the gate.** Checkpoint 2
  stands east of the radar gate the feed puzzle unseals, so a resume that lost the save flag would
  strand the player behind a sealed gate with the gauntlet still live behind it.
  `MarkWavesClearedThrough` sets a resume flag and `OnLevelReady` drives every surviving relay
  through `TakeEnvironmentDamage` — the same path the player's attacks take — so the counter, the
  puzzle conditions, the darkened beams, and the open gate all agree with a run that really cut
  them (the L02 generator precedent). The `PuzzleCompleted` handler is idempotent, as the Wave A
  integration requires.
- **L09: the level is 1,400 px tall, not Florence's 1,080.** The two guard-tower climbs need the
  headroom. The two flat rooms confine to a bottom-anchored 1,080 window
  (`GroundRoomCameraTop = LevelHeight - 1080`); the two climb rooms use the full height, and the
  content test asserts exactly two of each. Room 4 is widened to 1,920 px (level width 10,560)
  rather than Paris's 1,600 so no room confines the camera to less than the reference viewport.
- **L09: `--headless --import` on this fresh worktree rewrote ~40 tracked `.import` files with
  line-ending-only churn** (no content diff at all — `git diff` reported nothing but CRLF warnings).
  Reverted with `git checkout -- "*.import"`. The regenerated `localization/en.en.translation` *is*
  committed, per the L02 rule.
- **L09: suite total 514 → 529** (+15 `Level09ContentTests`), verified at 529/529, 0 failed, 15 s,
  with no sibling worktree contention. `AGENTS.md`'s baseline sentence is left for C1 per the A1
  convention. Import clean; `Level_09_Berlin.tscn` smoke at `--quit-after 300` exits 0 with no
  script errors, byte-identical output to the Level 4 baseline run.
### Wave B — Level 8, Alexandria 30 BC (Cleopatra's Palace)

- **L08: the boss resource agrees with §4.1 exactly — no contradiction this time.**
  `resources/Bosses/jackal_priest.tres` reads 700 HP, `MeleeRangeThreshold = 3.0`,
  `RangedRangeThreshold = 8.0`, one threshold `[0.5]`, `AttackPattern = 1` (`DistanceBased`),
  knockback-immune, 50 dust. Nothing was edited. Recorded only because every Wave A agent burned a
  cycle on this; §4.1 is now load-bearing and correct.
- **L08: the level is 1,800 px tall because the design's two zones are stacked, not adjacent.**
  Surface (dunes, y 700 floor) sits above the tomb network (y 1620 floor). Surface rooms confine to
  a top-anchored 1080 window, the boss chamber to a bottom-anchored one (`Position.Y = 720`), and
  the descent room to the full 1,800 so the burial-shaft fall stays on camera. `Level08ContentTests`
  asserts one room of each kind exists, so collapsing the level back to a single band fails.
- **L08: the sand drifts are scene-authored template instances, but the level localizes their label
  in code.** `MovementDampenerZoneTemplate.tscn` ships a `Label` with hardcoded English
  `"DEEP SAND"`, and overriding a child of an instanced scene from a `.tscn` needs the fragile
  `index=` block L02 flagged. `Level08Controller.CollectDeepSand` therefore sets
  `label.Text = Tr("egypt_deep_sand")` when it picks the drifts up. Shared toolkit content was not
  edited (§2.7); levels 6/9 reusing this component inherit the same one-liner. Fixing the template
  to carry a key plus a resolver is a C1-sized toolkit change, not a level edit.
- **L08: five sand drifts, none of them overlapping.** `EnvironmentPlayerModifiers` publishes the
  PRODUCT of every live source (A2 deviation), so two drifts over one player would compound to a
  0.25x crawl — the same constraint L05 hit with its two flood zones. A content test sorts the
  authored spans and fails on any overlap, so re-authoring the desert cannot reintroduce it.
- **L08: the hieroglyph lock is authored directly in the `.tscn` instead of instancing
  `SequenceLockTemplate.tscn`.** The template ships exactly three placeholder glyphs with fixed IDs
  and spacing; the puzzle needs four with era IDs (`level_08.glyph_scarab` …) and a deliberately
  scrambled wall order. Adding a fourth child and overriding three existing ones is strictly worse
  than authoring the four nodes, and L03 already set the precedent of authoring a puzzle graph as
  raw scripted nodes (`PuzzleManager`, `PowerRoutingNode`) alongside template instances. The node
  structure copies the template's glyph contract exactly (`Visual` Polygon2D + `InteractionArea` +
  `Prompt`), so a later template revision is a mechanical diff.
- **L08: the wall order is deliberately NOT the solution, and that is pinned by a test.** Mounted
  west-to-east the glyphs read Falcon, Scarab, Jackal, Ibis; the activation order is Scarab, Ibis,
  Falcon, Jackal, carved on a relief in a side alcove ~1,200 px west (which also holds the second
  extractor, so the detour pays twice). `TheHieroglyphSealOpensOnlyInTheCarvedOrderAndResetsOnAWrongPick`
  fails if anyone "tidies" the glyphs into solution order and turns the relief into decoration.
  The controller also labels each glyph in code (`egypt_glyph_*`) so the relief can be matched to
  the wall.
- **L08: `OnBossDefeated` is overridden wholesale rather than calling `base`, because the base class
  has no concept of a beat between defeat and exit.** `StoryLevelControllerBase.OnBossDefeated` calls
  `StartExitSequence()` directly, and Level 8 owes the authored Cleopatra scene
  (`level_08.postboss`, design-godot.md 3368–3374) first. The override reproduces the base's
  objective post and dust tally, then chains defeat → postboss → exit → results through
  `OnDialogueSequenceComplete`. Consequence: the base's private `_bossDefeated` flag stays false for
  this level, so **`IsBossDefeated` is not meaningful on Level 8** — the controller exposes its own
  `PostBossBeatPlayed` and gates objectives on a local flag. Levels 12 and 15 want the same shape
  (`preboss`/`ending` beats); if a third level needs it, C1 should make `StartExitSequence` virtual
  or give the base an optional post-boss dialogue hook rather than a third copy of this override.
- **L08: the post-boss beat falls through to the exit sequence if the dialogue cannot start.**
  A missing sequence must not strand the player in a finished arena with no results overlay, so
  `RunPostBossBeat` calls `StartExitSequence()` when `StartDialogue` returns false.
- **L08: the content fixture forces the persisted vault flag off on setup and restores it on
  dispose,** the guard L03 discovered. `level_08.hieroglyph_lock` is authored
  `PersistCompletionToSave = true` (the plan requires the seal to survive a resume), so one case
  solving it would open the vault for every later case in the same process.
- **L08: resuming at `_checkpoint_2` opens the vault even without the puzzle flag.** The pre-boss
  checkpoint stands past the door. `OnLevelReady` opens it when either the puzzle is complete or the
  resume checkpoint is the pre-boss one, and the opener is idempotent (`OpenDoor` nulls the ref), so
  `PuzzleManager`'s deferred restored-completion re-emission cannot double-fire anything.
- **L08: first full-suite run reported `Total: 24` with `Starting GodotRuntimeExecutor failed` /
  `Connection timeout` / `exit code: -1`.** The immediate re-run was **525/525, 0 failed, 15 s**.
  Same concurrent-worktree shape A1 and L02 logged; not signature 1 or 2.
- **L08: suite baseline 514 → 525** (+11 `Level08ContentTests`). Left for C1 to fold into
  `AGENTS.md` in one edit.
### Wave B — L06 Pompeii, 79 AD

- **L06: §4.1 held exactly; no boss-resource contradiction this time.**
  `resources/Bosses/vulcan_decimator.tres` reads `MaxHP = 660`, `MeleeRangeThreshold = 4.0`,
  `RangedRangeThreshold = 9.0`, one threshold at 0.5, `AttackPattern = DistanceBased`,
  `IsKnockbackImmune = true` — identical to the §4.1 row. The Wave A transcription did its job;
  nothing was edited. The caldera arena confines to 1,920 px against the resource's real 540 px
  band, and `Level06ContentTests` asserts `RangedRangeThreshold * 60 < arenaWidth` rather than a
  hardcoded number.
- **L06: the weight puzzle's missing unit is the player, because the toolkit has no way for a
  player to move a `WeightedObject`.** `MovableWeightTemplate.tscn` is a `RigidBody2D` on
  `PersistentObject` with `collision_mask = 192` (Environment | OneWayPlatform), so it never sees
  the player, and `PlayerController` has no rigid-body push (no `GetSlideCollision` /
  `ApplyCentralImpulse` path anywhere in it). The classic "carry the block to the plate" puzzle is
  therefore not expressible without editing shared toolkit code, which §2.7 forbids. So the vault
  winch is authored around what *is* expressible: a 3-unit basalt boulder has already fallen into
  the west pan, a 2-unit pumice block is released into the east pan by smashing a
  `DestructibleBlock` wedge (the same blow-up-the-lock verb Level 4 uses), and the east pan is then
  one unit light — the player has to stand on it and be the counterweight. `PressurePlate.PlayerWeight`
  already models exactly that, `RequireBothThresholds` makes "both pans loaded AND equal" the win
  condition, and the puzzle latches on completion so stepping back off cannot re-seal the road.
  Later levels reusing this family should assume the same constraint: weights arrive by gravity or
  by the player's own body, never by pushing.
- **L06: the escape template's finish line is repositioned and wired from code.**
  `EscapeSequenceControllerTemplate.tscn` parks `FinishTrigger` at its own default X and connects it
  to nothing — `NotifyPlayerReachedFinish` has no caller. Overriding an instanced scene's child
  position in the `.tscn` needs the fragile `index=` block (L02 deviation), so
  `Level06Controller.WireEscapeFinishLine` sets the position from the `EscapeFinishX` constant and
  binds `BodyEntered`. The root's own exports (StartX/EndX/FinishX/AdvanceSpeed/CatchDamage) stay
  authored in the scene, and the content test asserts the scene and the constants agree. A later
  level reusing this component needs the same two lines.
- **L06: the lava front is `ResetToInitialState` plus a level-owned restart, or Chronal Rewind
  switches the level's signature mechanic off.** With the default `RestoreCheckpointState` a rewind
  restores the front to its checkpoint snapshot and, because the only checkpoint before the run is
  west of it, leaves `IsRunning = false` — the rest of the corridor becomes a walk. The escape is
  authored `RewindPolicy = 1` so the front always snaps back to `StartX`, and
  `Level06Controller.OnStoryRewind` re-calls `Begin()` when the run was started and not yet
  finished. Ordering is load-bearing and free: the component subscribes to `OnRewindTriggered` in
  its own `_Ready` (a child, so before the level root's `OnLevelReady`), so the reset has always
  landed before the restart runs.
- **L06: `MarkWavesClearedThrough(checkpoint_2)` closes the escape rather than leaving it armed.**
  The pre-boss checkpoint stands east of the run, so a resume there must not find a live front
  behind it. The handler calls `ResetSequence()` (front parked at `StartX`) then `CompleteEscape()`,
  and sets `EscapeTriggered` so the room trigger cannot re-arm it.
  `ResumingPastTheAshRoadNeverDropsThePlayerIntoALiveLavaFront` pins all three.
- **L06: the pan's starting load is registered explicitly, not left to the first physics tick.**
  `Area2D` reports its authored initial overlaps on the first physics frame, and a GdUnit fixture
  that instantiates a level and reads it synchronously never reaches one — the west pan would read
  0 and the winch would look unsolvable. `OnLevelReady` calls `LeftPan.RegisterBody(BasaltBoulder)`;
  `PressurePlate` keys loads by instance id, so it is idempotent with the physics callback that
  follows. Any level authoring a body already resting inside a plate needs this.
- **L06: room camera bounds are floored at 1,920 px, with a deliberate overlap on the arena.**
  Following L02: the 1,600 px caldera court confines to a 1,920 px window starting 320 px west, and
  the content test asserts the floor for every room. The resulting 320 px overlap with the Ash Road
  is what makes `ApplyResumeCameraBounds` (last authored match wins) land a checkpoint-2 resume in
  the arena.
- **L06: the legionnaire's frontal shield is a geometry contract, not a comment.**
  `shock_shield_legionnaire` carries `FrontalDamageReduction = 0.5`, so every one of its six posts
  is authored under a drop-through platform and with a patrol that turns its back.
  `EveryLegionnairePostIsFlankableFromAbove` walks the authored spawn table and fails if a post
  loses its overhead platform or its patrol collapses to a point — otherwise a later layout edit
  would quietly turn the era enemy into a damage sponge.
- **L06: the fresh-worktree exit-100 artifact and the pipe collision are two different faults, and
  the orphans can be your own.** The first `dotnet test` failed with `exit code: 100` as documented.
  The rebuild-and-retry then failed *differently* — `Starting GodotRuntimeExecutor failed` /
  `Connection timeout` — and left two live Godot processes (one console, one not) spawned by the
  back-to-back runs, with no sibling agent involved (`Win32_Process` showed no `dotnet`/`testhost`
  alive). Killing both and re-running was immediately green. So the tooling note's "check for other
  `Godot_*.exe`" applies to your own previous attempt too, not only to concurrent worktrees.
- **L06: suite baseline 514 -> 528** (+14 `Level06ContentTests`), verified at 528/528, 0 failed on
  the first full run, in 15 s. Import clean; `Level_06_Pompeii.tscn` smoke clean at
  `--quit-after 300` with zero errors or warnings. `AGENTS.md` is left for C1 per the A1 convention.
### Wave B — L11 Gettysburg, 1863

- **L11: §4.1 held exactly; no boss-resource contradiction.** `resources/Bosses/siege_cannon.tres`
  reads `MaxHP = 880`, `MeleeRangeThreshold = 3.0`, `RangedRangeThreshold = 12.0`, one threshold at
  0.5, `AttackPattern = DistanceBased`, `IsKnockbackImmune = true`, 50 dust — the §4.1 row as
  written. Nothing was edited. The railcut is 2,560 px against the 720 px the 12-unit band needs at
  60 px/unit (the widest band in the roster), and `Level11ContentTests` asserts
  `RangedRangeThreshold * 60` against the room width rather than a hardcoded number, so re-tuning
  the boss cannot silently outgrow the arena.
- **L11: the shielding arrays are differentiated from Orléans by geometry class, and a test pins
  the difference.** Level 2 uses `ShieldGeneratorTower` as a *wall*: its panes straddle the walking
  surface (y 582..902) so breaking a generator is how the player passes, which is why it needs a
  solid arch above each 320 px pane. Level 11 instead stands both arrays on a raised earthwork
  gallery at `CrestTopY = 560`, so every pane seals the **Union assault lane** (y 240..560) and
  never the ground route the player walks. Progress is gated by the Confederate breastwork, a
  `BuildDoor` the Union engineers drop only when `ArraysDestroyed == 2` — an objective gate, not a
  barrier gate. `ThePanesSealTheRaisedUnionLaneAndNeverThePlayersOwnAdvance` fails if any pane
  reaches down into the player's advance band (`GroundY - PlayerAdvanceHeadroom`), so a later
  layout pass cannot quietly turn Level 11 back into a copy of the Orléans gate. The L02 arch
  constraint still applies in spirit and is honoured: an earthwork roof mass fills everything above
  each pane's top edge, and the gallery deck is flush with its bottom edge, so the lane is a real
  seal rather than a jumpable ornament. Two of the three extractors sit on the galleries beyond a
  pane, so sabotaging an array pays the player as well as the Union line.
- **L11: the gallery height is a derived constant, not a taste call.** Everything follows from the
  template's un-overridable `Barrier` offset of `(320, -48)` with a 48x320 pane:
  `ArrayY = CrestTopY - 112` (the tower's collision half-height), `BarrierCenterY = ArrayY - 48`,
  and the seal spans `BarrierCenterY ± 160`. `CrestTopY` was then lowered from an initial 620 to 560
  purely so the gallery underside clears the tallest cover on the ground line beneath it by more
  than a player's height — at 620 a player standing on a 140 px cannon wreck under the gallery had
  only ~168 px of headroom. A content test asserts that clearance, because the two numbers are
  coupled and nothing else would catch the regression.
- **L11: the cover/artillery fairness contract is a pure function shared by the level and the test.**
  `Level11Controller.UncoveredArtilleryGaps()` and `IsInsideBlastColumn(x)` live on the controller,
  and `EveryArtilleryGapHoldsCoverAndNoCoverStandsInsideABlastColumn` drives them: every gap between
  two consecutive shell columns must hold at least one authored cover piece, no cover piece (centre
  *or* either edge) may stand inside a column, and no checkpoint may respawn the player inside one.
  This is the design's advance/cover/advance beat expressed as an invariant — the L09 precedent of
  positional cover, computed rather than eyeballed. The one deliberate exception is the two climbs
  onto the galleries: their middle step sits squarely inside a column (x 4790 under the 4750 lane,
  x 6620 under the 6620 lane), which is the "cross exposed artillery ground to reach the objective"
  beat. The invariant is scoped to `CoverPositions`, not to every platform, so that stays authorable.
  A second test asserts the scene's ten `CyclicHazardTemplate` instances match
  `ArtilleryImpactPoints` position-for-position, so the table the invariant is checked against
  cannot drift from the geometry it describes.
- **L11: the artillery telegraph is proven by driving the real cycle, not by reading exports.**
  `StoryCyclicHazard._PhysicsProcess` is stepped at 1/60 s (the `EraMechanicToolkitTests` pattern)
  to prove the phase order is cooldown -> warning -> active and that the warning really lasts the
  design's ~2 s of cycle time, then `ApplyToPlayer` is called in each phase to prove the telegraph
  is a free read and the active phase is not. Asserting `WarningDuration >= 1.8` alone would pass
  on a component that fired straight out of cooldown.
- **L11: the checkpoint-2 resume force-destroys both arrays; the checkpoint-1 resume deliberately
  does not.** Checkpoint 2 stands east of the breastwork, so `MarkWavesClearedThrough` drives both
  arrays through `TakeEnvironmentDamage(MaxHP)` — the same path the player's attacks take — and
  opens the gate, keeping the panes, the objective counter, and the geometry in agreement (the L02
  generator precedent). Checkpoint 1 sits at x 6,250, inside room 2 and west of nothing that seals,
  so a midpoint resume leaves the arrays standing and the player can still walk back to gallery
  Alpha without crossing a room seam. Both halves are pinned by one test; the gate opener is
  idempotent (`OpenDoor` nulls the reference) as the Wave A integration requires.
- **L11: no bidirectional room-transition pairs were needed.** L03 added `BuildRoomTransitionPair`
  because backtracking across a seam left the camera clamped. Level 11 is authored so that nothing
  ever requires crossing a seam westward: both galleries, both climbs, and every extractor sit
  inside the room whose checkpoint precedes them. Single one-shot triggers are correct here, and
  the linearity the design asks for is what makes that true.
- **L11: the level's only elite inherits the L06 flankability contract.**
  `cyber_cavalry_commander` carries `FrontalDamageReduction = 0.4`, so its post at the Angle is
  authored under a 400 px drop-through platform that spans its whole patrol, and
  `TheCavalryCommandersPostIsFlankableFromAbove` fails if the platform is removed, the patrol
  collapses to a point, or the post drifts out from under the platform.
- **L11: the content fixture always resumes at a checkpoint (default `_checkpoint_0`),** following
  L04/L06. A fresh entry defers the entrance dialogue, which is authored `PausesGameplay = true`,
  and a landed deferred call leaks `SceneTree.Paused` and hangs the session (signature 4). The
  fixture samples and restores the pause flag anyway.
- **L11: the first full-suite run reported `Total: 24` with `GodotRuntimeTestRunner ends with exit
  code: 100` / `Starting GodotRuntimeExecutor failed` / `Connection timeout`, with an empty process
  table** — even though a filtered run of four suites had just passed 39/39 in the same worktree.
  A plain `dotnet build` plus an immediate re-run was **570/570, 0 failed, 16 s**. So the
  fresh-worktree exit-100 artifact is not strictly first-run-only: a filtered run does not
  necessarily "warm" the adapter for the unfiltered one.
- **L11: suite baseline 554 -> 570** (+16 `Level11ContentTests`), where 554 is Wave A's 514 plus the
  merged L06 (+14), L08 (+11), and L09 (+15). Import clean; `--headless --quit` clean;
  `Level_11_Gettysburg.tscn` smoke at `--quit-after 300` exits 0 with no output at all — no script
  errors and no warnings. `AGENTS.md`'s baseline sentence is left for C1 per the A1 convention.
- **L11: `--headless --import` rewrote 41 tracked `.import` files with line-ending-only churn**
  (`git diff` reported nothing but CRLF warnings), the same artifact L09 logged. Reverted with
  `git checkout -- "*.import"`. The regenerated `localization/en.en.translation` *is* committed,
  per the L02 rule.
