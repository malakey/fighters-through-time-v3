# C. Level authoring: traversal stretches (7a), optional routes (7c), six boss arenas (14)

Recon at 43d54db6, read-only. Rulings in `DECISIONS_gameplay_review.md` are not reopened. "Verified" = read in code; "inferred" = reasoned; "unmeasured" = needs a test or bot run.

## 0. Findings that matter most

1. **Width does not have to grow.** Every lock is a full-height rect (`EncounterLockSpec.Span`, EncounterLockSpec.cs:156) and the rules allow 1,280-3,200 px (`MinAreaWidthPixels` = view, `MaxAreaWidthPixels` 3200, EncounterLockRules.cs:133,140; test EncounterLockContentTests.cs:149). Most areas are 2,100-3,100 px, i.e. at the maximum. **Shrinking an oversized lock to ~1,700-2,000 px frees a 1,000+ px stretch inside the existing level width, with no eastward shift** of anchors, fonts, secrets, rooms, boss arena or par. Insertion (shifting east) is only needed where the level needs more total length.
2. **The 7c dust route cannot be ability-gated.** F05's "thorough 1,000" needs every optional source collectable by every hero (DUST_ECONOMY.md:11,15,51; the secret must pass a worst-hero double-jump BFS, SecretCachePlacementTests.cs:22-36, gap <= 250 px at :68; the no-required-hero rule). So dust stays base-kit reachable; abilities give shortcuts (section 3).
3. **Real gravity inversion is not a small change** (section 5, Overseer): `GravityFieldZone.SetScale` clamps to >= 0 (GravityFieldZone.cs:104) and `StoryAirPhysics.StepVertical` has no upward speed cap (StoryAirPhysics.cs:217-232).
4. **Test pins are absolute coordinates.** `ArenaGeometryContentTests.cs:220` asserts floor coverage to 10540; `Level02ContentTests.cs:236` pins 8960; per-level tests pin lock constants. Any east shift edits those tests too.
5. `AnchorApproachContentTests` (AnchorApproachContentTests.cs:9-36) reasons with **no ropes and no movement abilities**. A rope-only stretch before a Middle/PreBoss anchor fails it. Put rope stretches after the anchor, or keep a rope-free path to each anchor.
6. Level 7 already has a 2,190 px unlocked rope stretch (gap 3590-5780): the model for "stretch", and a known bot-gap level.

## 1. Current state: lock extents (Normal, from constants; ~ = approximate)

| L | Width | Lock x-ranges | Locked % | Free gaps (anchors/fonts) |
|---|---|---|---|---|
| 1 | 11520 | 720-2330, 2350-4430, 5800-8630, 8900-10460 (Level01Controller.cs:870-884) | ~70 | 4430-5800 (1370); 8630-8900 (Middle 8840) |
| 2 | 10560 | 300-3200, 3220-6290, 6580-8690 (Level02Controller.cs:195-207) | 76 | 6290-6580 (Middle 6500); 8690-9300 (PreBoss 9060, font 9180) |
| 3 | 10240 | 700-3820, 3840-5780, 6115-~8300 (Level03Controller.cs:202-219) | ~85 | ~335 round Middle 6000 |
| 4 | 10240 | 300-3200, 3220-4760, 6120-8100 (Level04Controller.cs:212-226) | 63 | **4760-6120 (1360; prisoner puzzle, Middle 5000)** |
| 5 | 11200 | 380-3400, 3420-5880, 6160-7520, 7540-8820 (Level05Controller.cs:270-280) | ~72 | 5880-6160 (Middle 6100); 8820-9300 |
| 6 | 10560 | 300-2880, 2900-5390, 5660-8500 (Level06Controller.cs:363-374) | ~75 | 5390-5660 (rockfall); 8500-9150 |
| 7 | 10600 | 380-3590, 5780-8480 (Level07Controller.cs:406-415) | 56 | **3590-5780 (2190 ropes/skiffs, exists)** |
| 8 | 10240 | 300-3200, 3220-5460, 5720-7000, 7020-8310 (Level08Controller.cs:262-298) | ~75 | 5460-5720 (Middle 5640) |
| 9 | 10560 | 300-2860, 3150-6350, 6620-8560 (Level09Controller.cs:248-274) | ~73 | 2860-3150; 6350-6620 (Middle 6570) |
| 10 | 10240 | 300-2540, 2560-5740, 6080-8330 (Level10Controller.cs:348-376) | ~75 | 5740-6080 (Middle 6040) |
| 11 | 11520 | 300-3200, 3220-6090, 6400-8895 (Level11Controller.cs:332-357) | ~72 | 6090-6400 (Middle 6340) |
| 12 | 11600 | 320-3000, 3020-5860, 6220-7540 (Level12Controller.cs:384-401) | ~59 | 5860-6220; **7540-~9600 (~2000)** |
| 13 | 9600 | 320-3200, 3220-5100, 5500-6900 (Level13Controller.cs:410-428) | ~64 | 5100-5500; 6900-Mirror |
| 14 | 11800 | 320-2900, 2920-5782, 6640-9030 (Level14Controller.cs:462-478) | ~66 | 5782-6640 (860); 9030-boss |
| 15 | 11600 | 320-3000, 3020-5900, 6420-7900 (Level15Controller.cs:397-412) | ~61 | 5900-6420; 7900-~9600 (~1700) |

Level 0 has one standalone trial lock (2580-4780), a tutorial: leave it out of 7a.

Locks are mostly back-to-back (20 px apart) and span whole rooms; camera rooms (`RoomTransitionTrigger`) equal lock areas, so a stretch needs its own camera bounds. Big existing free spans: L4 4760-6120, L7 3590-5780, L12 7540-~9600, L15 7900-~9600, L14 5782-6640, L1 4430-5800.

## 2. 7a design: where a stretch goes

**Option A (recommended default): carve.** Shrink the widest lock (usually the first) to ~1,700-2,000 px and put the stretch in the freed span. No shifts. Per carve: move the lock `MinX/MaxX` constants and barriers; re-lay spawns outside the new area (`EveryMemberSpawnStandsInsideItsAreaOnAFloor...`, EncounterLockContentTests.cs:118); move the room trigger and camera bounds; lock IDs and baselines unchanged; wave tables identical (manifest rows unchanged, EncounterLockContentTests.cs:182).
**Option B: use an existing gap** (L4, L7, L12, L14, L15, L1): no carve, but L4's gap holds a puzzle.
**Option C: insert width.** East of the insertion point shift: platform/spawn/extractor literals, scene-authored nodes (L2: towers and mortar lanes at 5980-8380, Level_02_Orleans.tscn:29-80), anchors, font, secret, room bounds, `BossSpawnX`/`BossArenaWestX`/`BossArenaBounds`, the 10540 wall, `LevelWidth`, baselines; then retarget absolute pins in `ArenaGeometryContentTests`, `Level0NContentTests`, `FontHazardClearance` callers. About 40-60 literals per level. Use only for a stretch over ~1,300 px.
**Vertical stretches:** a lock rect is always full height (`Span`, EncounterLockSpec.cs:156), so vertical content inside a lock is not a stretch. Verticality belongs inside the carved span (stacked one-ways, a lift, a ledge-grab wall).

**Pieces (existing, no new verb):** double jump; one-way platforms (double tap Down drops); `TrapdoorPlatform`; `PathMovingPlatform` lifts; `CrumblingPlatform`; `PendulumAnchor` ropes (L7, L10 only); `StoryCyclicHazard` lanes, `StageHazard` spikes, `FallingDebris`, `SearchlightZone`; `MovementDampenerZone`; `GravityFieldZone`; `LedgeGrabPoint`; short hop and fast fall (optional skill only; unverified that bots use them).
**Rules to carry:** pits non-lethal (`VERIFY-STORY-PITS`; use a trench floor 100-150 px down with a walkable exit like the Tribunal pit, Level04Controller.cs:509-520); gaps within the worst hero. Worst hero (AGENTS.md figures, derived from `StoryAirPhysics`): Lincoln single jump rise ~133, double ~265 px, air distance ~201 / ~353 px at 228 px/s; best single rise Cleopatra ~214. Use **horizontal gaps <= 280 px and rises <= 240 px per hop**; wider only on the optional line.

### Worked example: Level 2, carve, no shift
Today: vanguard lock 300-3200, spawns at 1100/1750/2600 (Level02Controller.cs:156-159), platforms (:321-326), spikes 1600/890, secret (900,467) (:65), courtyard extractor (2400,580) (:174), room trigger (300,600).
1. Vanguard lock -> **300-2000** (`VanguardLockMaxX` 2000, exit barrier 2010). Move the third archer 2600 -> ~1900 (platform 1900/540 stays). Keep the secret at (900,467).
2. **Stretch "Rampart run" 2020-3200** (1180 px; the siege line entry barrier at 3200-3220 is untouched). New camera room (`BuildRoomTransition("orleans_room_rampart", ..., Rect2(2000,0,1220,1080))`).
   - Trench: floor 2020-3200 lowered to y 1020, walkable ramps at both ends (<= 40.6 degrees).
   - Step line at y 700: platforms 2150 (w200), 2430 (w180), 2700 (w200), 2980 (w220): gaps 130-200 px, single-jump clearable by all.
   - Piece 1: 2430 is a `CrumblingPlatform`. Piece 2: a `StoryCyclicHazard` mortar lane at x 2850 (copy MortarLane, Level_02_Orleans.tscn:39-86) crossable between volleys. Piece 3: a one-way drop-through to a lower trench-lip corridor. Piece 4: a 220 px wall step at 3150 with a `LedgeGrabPoint` (also double-jumpable, rise 220 <= 265).
3. **Optional route (7c)**, "Parapet line", y ~400, entered from the 2400/660 platform: (2300,520,w180), (2560,420,w180), (2820,420,w160), (3080,400,w220). Gaps 80-140 px, so base-kit; the 2820->3080 hop is 260 px; a shortcut is a 420 px skip for bursts. The courtyard extractor (or the secret) sits at the end. Base path for everyone: add a stepping platform.
4. Unmoved: Middle 6500, PreBoss 9060, font 9180, arena 9300, baselines, manifests.
5. Par 360 -> 390 (section 2.1). Tests to edit: `Level02ContentTests` (lock constants, spawn table, extractor position). Automatic: EncounterLockContentTests snapshots, SecretCachePlacementTests, ArenaGeometryContentTests (check the 64+4 px ceiling rule for new roofs). New: `Level02TraversalStretchTests` x3.

### 2.1 Par and the Integrity clock
Par is `ParSeconds` per controller (seeded rooms x 90 s; L2 360, Level02Controller.cs:56) x `ProvisionalParScale` 1.5 (TimelineIntegrityRules.cs:80) x difficulty multiplier (2.0 / 1.5 / 1.2). Route budgets are fractions of par (0.90 / 0.45 / 0, StoryLevelControllerBase.cs:124-127, 1153) so they scale on their own. Rule: `par_new = ceil30(par_old + 1.25 x slowest-hero median stretch seconds from a play-bot trace, Normal)`, minimum +30 per stretch; +90 if the stretch counts as a seeded room. F11 budgets above 100 throw (TimelineIntegrityRules.cs:140-157); fractions <= 0.9 cannot. The clock stays a fail state (ruled): check with a Lincoln Hard run that par did not tighten. `StoryLevelControllerBaseTests.cs:705` only pins par > 0.

## 3. 7c: optional route by ability type

Movement abilities (Story, 60 px/u; from KitMotionRules, KitReachRules and the `.tres`; cooldown 5 s for all):

| Hero | Ability | Reach | Notes |
|---|---|---|---|
| Lincoln | Rail Charge | 300 px horizontal, ground only, stops on contact | armored in travel, no vertical |
| Cleopatra | Desert Mirage | 240 px, 8-way, pushbox off | no invulnerability |
| Tubman | North Star Leap | 240 px 8-way (+30 px ledge snap) | |
| Einstein | Relativity Warp | 240 px fold, any direction, then 60-frame float | wall pass-through unmeasured |
| Shakespeare | Prospero's Flight | 240 px forward + 150 px rise | 20 frames |
| Tesla | Lightning Blink | 180 px (+30 long blink) | projectile pass-through only |
| Mozart | Sonata Drift | 180 px directional glissando + 120 px staff | refund once per airtime |
| Joan | Ascendant Wings | rise at 420 px/s, then dive 300 fwd / 540 down px/s (KitReachRules.cs:29-30) | rise height unmeasured |
| Leonardo | Daedalus | boost peak ~61 px, glide sinks 40 px/s for 3 s (LeonardoAbilities.cs:314) | longest horizontal, unmeasured |

Base kit: run 228 (Lincoln) to 372 (Joan) px/s, double jump rise 265+ px, Lincoln double-jump air distance 353 px. Groups: horizontal bursts 180-300 px (Lincoln ground-only, Cleopatra, Tubman, Einstein, Shakespeare, Tesla); rise/glide (Joan, Leonardo, Mozart, Shakespeare's 150 px). No ability passes solid walls (inferred).

**Is a route some heroes cannot reach acceptable?** Not for dust: the thorough 1,000 and the secret test need base-kit reach by every hero. Options:
- **A (recommended):** dust is the existing secret (or one extractor share) at the end of a longer base-kit line (gaps <= 280 px); abilities provide a shortcut skipping 1-2 hops, in two flavours per level (a horizontal gap for bursts, a vertical shelf for rise/glide) so every hero has one. No new dust; F05 untouched.
- **B:** add an ability-gated vault paying non-dust (lore datalog, a Font charge) on top of A. Needs a content decision; skip unless wanted.
- **C (reject):** hero-dependent dust. Breaks thorough and the secret BFS.
- **D (later):** credit the rank's speed score for taking a shortcut (rank is on top of F05).

**Level 1:** no movement ability until Level 2 (3b+ keeps Movement on 2), and Level 1's optional pool is Extractors only with no secret (DUST_ECONOMY.md:40). Level 1's route is a **skill route** (double jump, trapdoors, gears) holding one of the three Extractors, with no ability shortcut.
**Attaching:** `SecretCachePosition` (StoryLevelControllerBase.cs:1575) plus manifest `SecretSourceID` (level_02_rewards.tres:16): moving the cache to the route end is a one-line override and the existing placement rules apply (surface, 300 px from checkpoints, outside the arena, BFS). An extractor on the route is just an `ExtractorPlacements` entry; no manifest change.
**Testing reach per hero:** extend the SecretCachePlacement BFS with a per-hero mode: `StoryAirPhysics.MultiJumpRise/JumpDistance` with that hero's `MaxJumpForce`, `Weight`, `MaxMoveSpeed`; shortcut edges from the table. Assert every hero reaches the dust on base kit, and at least one hero per group takes each shortcut. Bots confirm with Joan and Lincoln; bots do not use movement abilities for nav (unverified).

## 4. Bot and nav impact
`PlaytestNav` builds the graph from collision shapes and plans walk/fall/jump/double-jump/drop-through/ride/rope edges with a frame-simulated flight (PlaytestNav.cs:850, 1134; gravity zones :358, 689). Trenches, one-ways, lifts and trapdoors are planned with no code change. Risks: (a) a jump onto a closed trapdoor from beneath (Level 10 bug); (b) ropes are fragile, so none on L6, L7, L10; (c) cyclic-lane crossing timing is untested; (d) negative gravity scale is unplanned; (e) lock-engage logic unchanged. L6/L7/L10 carry bot-gap notes in the 4c curve; read their stretches by trace (`--trace-every=3`, `--nav-dump=1`), not by completion %. Level 6 vault: ledger row only; keep any L6 stretch out of 5390-5660 (Level06Controller.cs:366-376).

## 5. Item 14: six boss arenas

Shared pattern (verified): `BossEncounterController.PhaseEntered` fires once per phase (BossEncounterController.cs:72, 205-209); levels subscribe after `BuildBossEncounter` (Level04Controller.cs:492-494; Level01Controller.cs:1094; Level03Controller.cs:462; Level10Controller.cs:780). It fires inside the killing hit's physics flush, so body/hurtbox creation or ProcessMode changes defer with `PhysicsCallbackGuard.IsInPhysicsCallback` + `CallDeferred` (Level01Controller.cs:1169-1180; Level03Controller.cs:489-501). Pre-build bodies disabled and enable on the phase (Tribunal lid, Level04Controller.cs:538-581). Make each change an idempotent public method (test seam).
Interactions: **arena leash** (new hazards stay inside `BossArenaBounds`; nothing standable below the fall net, `NothingABossCanStandOnLiesBelowItsArenaFallNet`). **PreBoss freeze:** the Integrity gauge is frozen from PreBoss, so hazards add no clock pressure. **Post-Landing Hold:** hazards freeze while `IsWorldHeld`; do not start one mid-hold. **Time Freeze:** nothing here reads it; `GravityFieldZone`, `StoryCyclicHazard` and platforms implement `IStoryTimeFreezable` and stay dormant when it is off. `BossPhaseRuleMatrixTests` passes on the stand-ins today (DESIGN_BUILD_DEVIATIONS.md:57); retire or retune them per the row.

| Boss (L) | Intended (design-godot.md:3136-3146) | Stand-in | Build (reuse) | Cost |
|---|---|---|---|---|
| Duke (L2) | boiler rupture: both arena edges become steam hazards, fight pushed to centre, mortars | `mortar_barrage` Projectile x4, 16 dmg | Two `StageHazard`/cyclic steam strips on the court edges (arena 8960-10540, BossArenaBounds :132), disabled until phase 1; platforms unchanged | S |
| Vulcan (L6) | floor cracks into lava vents on a visible cadence; debris at double rate | `lava_vent_field` (field 110 px, 4 s) | 4-5 `StoryCyclicHazard` vents on the flat base floor (Level06Controller.cs:640-679, slopes stay), `FallingDebris` rate x2 from phase 1 | M |
| Admiral (L7) | deck lists, loose cannonballs roll, gatling follows tilt | `loose_cannon_roll` x2 | Swap collision to a sloped (<= 8 degrees) deck or raise the existing listing ramp joints (Level07Controller.cs:620-642, `BuildRampBetweenJoints`); cannonballs as `RigidBody2D` on Environment (Level 6 boulder idiom, `Level06RockfallPhysicsTests`) with an Area2D hurt trigger. Do not rotate a StaticBody under a CharacterBody | M-L |
| Chancellor/General (L9) | fleet lanterns sweep the deck; a caught player takes a volley | `artillery_barrage` x3, 20 dmg | Reuse `SearchlightZone` in its delayed-strike mode (SearchlightZone.cs:8-17), 2-3 beams over the flagship arena (Level09Controller.cs:104), enabled on phase 1 | S-M |
| Siege Cannon (L11) | railcar relocates along its track after each volley; cover fences become destructible | `rail_warp` Teleport 280-460 px | Fences as `DestructibleBlock`s invulnerable until phase 1 (idiom Level06Controller.cs:476); relocation is boss-side (`RelocationRules` needs floor under the landing; the rail is built at Level11Controller.cs:565-596,633) | M |
| Overseer (L12) | P2 gravity inverted in marked zones; P3 vacuum vents drag toward the edges | `inverted_gravity_field` (slow field 130 px, 4 s) | see below | L |

**Overseer.** True inversion needs a negative scale (blocked at GravityFieldZone.cs:25,104), an upward speed cap in `StepVertical`, an "up" for `IsOnFloor`/jump/coyote/ledge/drop-through/animation, a nav flight model that handles it (PlaytestNav.cs:1134), and Fighter isolation. **Not recommended.** Recommended: phase-2 zones are cycling `GravityFieldZone`s (scale 0.15 <-> 2.5, 1 s telegraph, `CycleScales`; the arena is already tiled with fields, Level12Controller.cs:21,226,679). The hero handles it; `PlaytestNav.GravityScaleAt` reads the static scale only, so bots re-plan per frame. P3 drag: no lateral push exists (`EnvironmentPlayerModifiers` has only move multiplier and gravity scale, EnvironmentPlayerModifiers.cs:27-50), so it needs a new modifier or a cyclic hazard with knockback toward the edges; that is why this is L.

## 6. Ownership, tests, effort

**Shared files:** `StoryLevelControllerBase.cs` (2135 lines; do not edit per level: add one new partial `StoryLevelControllerBase.Stretches.cs` first, owned by the lead); `localization/en.csv` and compiled `en.en.translation` (per-level key blocks; one integrator runs `--import`); shared test classes (`ArenaGeometryContentTests`, `EncounterLockContentTests` `EntryFloorByLock`, `AnchorApproachContentTests`, `SecretCachePlacementTests`); reward manifests (unchanged); AGENTS.md and ledger (closeout).
**Parallel split (disjoint controller, scene and per-level test files):** Agent 1 L1-L3 + Duke; Agent 2 L4-L6 + Vulcan; Agent 3 L7-L9 + Admiral and Chancellor; Agent 4 L10-L12 + Siege Cannon and Overseer; Agent 5 L13-L15. A boss lives in its level's controller, so it goes with that level.
**Effort per level (sessions incl. tests; S=1, M=1.5-2, L=2-3):** L1 M, L2 S (worked), L3 M, L4 M, L5 L, L6 M-L, L7 S-M (+Admiral), L8 M, L9 M-L, L10 M-L, L11 M, L12 M-L, L13 M, L14 L, L15 M-L. About 30-35 sessions plus ~3 for integration and a bot batch.
**Test delta (estimate):** +3 per level (stretch geometry, par, optional route) = +45; shared `TraversalStretchContentTests` +5; `OptionalRouteReachTests` +15; six arenas +4 each = +24; in-place edits to lock/spawn sweeps. **About +90 to +110.** Keep pure-C# reach cases free of Godot calls (signature 7).

## 7. Risks
- 14 controllers with many literals; carve first to avoid shifts. A missed pin shows only in the full suite (check `Total:`).
- Every carve fails `EveryMemberSpawnStandsInside...` until spawns are re-laid.
- A stretch without its own `RoomTransitionTrigger` leaves the camera clamped to the lock room.
- Bots on L6/L7/L10 confound measurement; new hazards add `play` bot failure modes.
- Longer exposure and more hazards arrive with higher slots (2/3/4) and Time Freeze removed.
- Einstein and Tesla pass-through and Joan and Leonardo rise are unmeasured: write measurement tests first.

## 8. Open questions (recommended answers)
1. Carve or insert? Carve; insert only for stretches over ~1,300 px.
2. Ropes in new stretches? No.
3. Extra dust on the route? No (F05); optional non-dust item later.
4. Ability-gated reach? Never; shortcut only.
5. Overseer gravity: cycling 0.15/2.5 fields, no negative scale.
6. Level 1 route: skill route holding an Extractor, no secret.
7. Par: bot-measured +25%, up to 30, min +30 per stretch.
8. Order: helper partial, Level 2 as pilot, then fan out.