# FIX-BOT handoff: the playtest bots and lock telemetry, Phase C fix round (gameplay update 2026-10-12)

Workstream FIX-BOT of the Phase C fix round of `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md`. Branch `worktree-wf_c57a521f-e47-4`, reset to `c4885a9` (main with Phase B merged; suite 3180) before any edit.

**Diagnostics only.** No game code, scene, resource, `en.csv` key, save field, Klotho component or protocol changed. Everything is under `scripts/Diagnostics/`, `tools/playtest/` and `tests/Unit/PlaytestEncounterTelemetryTests.cs`.

**Nobody has played anything.** The evidence is the suite below and headless bot runs from this worktree (tags `fxbot*`).

## 1. Review items

### #11 (high): the bot pinned against a breakable between it and a lock member

**Root cause, confirmed in the batch.** On Level 4 Normal and Hard (all 8 runs), the cell-B rioter presses against the far face of `CellLockB`. `StoryBot.Fight`'s approach used a raw walk-in whenever the target was on the same level and under 300 px away. The bot therefore pushed at the block's near face for the rest of the run: x 7552, 144 px short.

The same pin ended other runs `stuck_lock`:
- every Level 6 vault run (a member behind the basalt boulder on the west pan, the bot at 3852);
- every Level 11 wheatfield and seminary run (members behind cover);
- the Level 5 staircase runs (a member one stair step up).

**Fix** (`PlaytestBots.cs`, lock targets only). The walk-in is used only when `LockWalkInClear` holds:
- **A solid ray is clear.** `PlaytestWorld.ClearWalk` casts an Environment ray from the chest (−36) to the member's centre (−30). Unlike `LineOfSight`, it does not skip `DamageableEnvironmentObject`, props or barriers.
- **Both stand on one nav surface.** Two static segments are two only because an obstacle or a gap splits them. A temporary surface, or a lift under the hero, is exempt.
- **The last walk-in did not stall.** `WatchLockWalkIn`: a walk-in that leaves the hero still for `LockWalkInStallFrames` (30) hands the approach to the agent for `LockWalkInDetourFrames` (180). This catches a step or low prop the ray misses (Level 5's stairs).

When the walk-in is refused, the bot uses `Go` instead (action `approach_around` when close). Its goal is the member's own surface (`LockTargetGoal`: the nav segment under its feet, `PerchMaxBelow` 40). The goal is never the old stand point 60 px off the member: on Level 4 that point lay over cell A's other side, and the two goals made the bot oscillate. `NavAgent.Drive` then takes the Break edge (`StrikeRect` → `break_path`), a jump, or its push-hop.

### #12 and #17 (low): lock telemetry merged lock instances across a scene reload

**Root cause.** `LockTracker` keyed records by `LockID` only. An Act III Anchor Snap reloads the scene and the run continues. The reload rebuilds every lock as a new node under the same ID: Armed with fresh members on a slotless bot run (#12), or Cleared through `RestoreCleared` when a save baseline applies (#17). The new node then:
- overwrote the first node's counters;
- stacked its members' kills onto the first plan (killed > planned: the Phase C `gucn0` batch reads killed 435 against planned 372);
- relabelled a lock cleared live as "restored";
- booked a respawn outside the area as a leave.

**Fix** (`PlaytestEncounterStats.cs`, `PlaytestTelemetry.cs`).

`LockObservation.InstanceId` is filled from `encounterLock.GetInstanceId()`. A changed instance starts a new `LockRecord`:
- `Instance` is 1, 2, … and the record keeps its `InstanceId`;
- it starts with the hero outside, so a respawn is not a leave.

`InstanceId` 0 is read as the same instance, which keeps the old behaviour for hand-built observations.

`NoteMember(LockRecord, …)` routes each member to its own instance's record. The string overload routes to `LockTracker.Current(id)`.

New read surface: `LockCount` (distinct IDs), `Reloads`, `EngagedLocks`, `ClearedLocks` (distinct IDs with any instance seen Cleared, so a lock cleared live and then re-armed by a Snap still counts) and `Current(id)`.

Report shape:
- **`locks` block.** `count`, `engaged` and `cleared` are distinct lock IDs. New keys `instances` and `reloads`. Every `log` row gains `instance`, with one row per instance. `members_planned` / `members_killed` sum per instance.
- **`PLAYTEST_RESULT.encounters`.** `locks` and `locks_cleared` are distinct IDs. New key `lock_reloads`.
- **Events.** Lock events carry `#n` from the second instance on.
- **Tools.**
  - `compare_runs.py`'s `locks` table gains a `reloads` column; an older report has no key and reads 0.
  - `trace.py` labels instance lines `id#n`.
  - `run_playtests.ps1`'s CSV gains `lock_reloads`, with its help text.

Checked on the new runs: `fxbotn*` reads 12 reloads and killed 347 ≤ planned 354.

## 2. Bot gaps from the Phase C batch

Every stall below was confirmed in its batch trace first.

1. **Level 1, the Florence print shop** (all 12 runs: `lock_wait` at x ≈ 6600). The bot had no `RotatingGear` handling, and the gears' `InteractionArea`s are siblings, not children.

   **Fix.** `PlaytestWorld` scans `Gears`. `GearPlan()` groups an unsolved train per `PuzzleManager` and solves it from the live turns with the Godot-free `PlaytestPuzzleMath.GearPresses`. That function brute-forces 4^n combinations, with n ≤ `MaxGearTrain` = 6, and returns the fewest presses that align every gear: a press turns its gear and every gear it drives.

   `PickTask` considers the gears of a train **the engaged lock requires** (`EngagedLockRequires`, a generic "solve the lock's required puzzle" hook). `WorkTasks` presses them through `GoInteract` (`gear`, one press per 30 frames, re-planned after each). `TaskDone` reads the plan, and `TaskProgress` reads the turns.

   `PlaytestWorld.InteractionAreaOf` also finds a sibling area whose `TargetPath` resolves to the target, so the runner's `RouteInteract` reaches gears.

   **Result:** Level 1 Normal clears all four locks and completes. The print shop clears about 16 s after its engage, after three presses.
2. **Level 6, the vault** (all 12 runs). This was three bot problems, not the "missing counterweight" wording CLAUDE.md records:
   - **(a) The boulder pin.** The #11 pin above, against the basalt boulder (Normal and Hard).
   - **(b) The wedge never broke for Joan.** On Easy, `StrikeRect` jumped to a full apex before the down-air. From the apex, the 125 px box ends short of a target at the hero's own floor level (the wedge's top is 8 px under the gallery).

     **Fix (inside an engaged lock).** When the full jump's apex plus `DownAirReachBelowPixels` (125, the bot's reading of `PlayerController`'s down-air box) is short of the target, `_airStrikeShortHop` holds Jump for 2 frames and presses the down-air on frame 3.
   - **(c) The east pan was never a task.** After the pumice dropped, its body covers the east pan's centre, and `Consider` tested reachability at the plate's centre. Only Lincoln had ever broken the wedge in older batches, and he then stalled here.

     **Fix.** A plate of a puzzle the engaged lock requires counts as reachable when any of its `PlatePoint` stand points is (`Consider(…, reachedAt: true)`).

   **Result:** Level 6 completes on Easy and Normal for Joan, and on Normal for Lincoln.
3. **Members posted above the hero** (Level 9 Hard seawall, Level 7 quay, Level 10 galleries).

   **Fix.** `LockTargetPerch` applies to a lock member at least 60 px off the hero's level that stands on a nav surface the hero is not on, when the hero can reach that surface. The bot then `Go`es to a stand point on that surface (`surface.Clamp(member ± 60, 14)`, `PerchMaxBelow` 40) and skips the `AirStrike` from below.

   The old `standAt` (member ± 60 at its height, `maxBelow` 300) usually missed the member's ledge and "arrived" on the floor underneath.

   A lock member that stands on a surface is now struck only from the ground. A string thrown mid-jump on the way up dropped the hero back under the ledge. Flyers keep the old `AirStrike` (FIX-ENEMY's ground).

   **Result:**
   - Level 9 Hard: the seawall clears.
   - Level 10 Normal: the galleries clear for both heroes.
   - Level 7 Normal: the quay clears for both heroes.
4. **Level 11** (wheatfield, seminary, angle).
   - **The stalls** were the #11 pin against cover (an 810-top wall at 5475–5675), plus a 30 px step at the seminary. The #11 fix clears them: Level 11 Normal completes for both heroes.
   - **The known targeting weakness** showed up as array Alpha struck from the ground route below the gallery deck: about 65 s of jumping up-attacks through the deck.

     **Fix (inside an engaged lock).** `PrefersBeside` makes `StrikeRect` skip the jumping up-attack, and `StrikePoint` try the two stand points beside the target's base first, whenever one of them is reachable. Alpha now breaks at frame 4740 instead of 7710. "Bravo first" did not show up in any run here.
5. **Level 12, the curtain** (the Easy runs collapsed on the Timer at about 1630 s). This was four nav and bot problems:
   - **(a) Dodges ran off the mast head.** The extractor-discharge dodge (`evade_discharge`) and the mast-head vent dodge (`evade_hazard`) ran the hero off the highline launch, into the dead end under the curtain (8136–8540).

     **Fix (everywhere).** `RunsOffLedge` refuses a dodge with less than `DodgeLedgeRoomPixels` (64) of surface before a drop: the hazard dodge tries the other way, and otherwise braces (`brace_discharge` / `brace_hazard`, Block held).
   - **(b) No edge back onto the lift.** There was no edge from the floor onto the spire lift's bottom stop, which sits flush in the floor, so a fallen hero could never plan back up.

     **Fix (`PlaytestNav.AddEdgesTo`).** A Walk edge onto a moving-platform stop lying within a static segment at the same height (±12 px). The existing boarding wait keeps the hero off the lift's footprint until it is there.
   - **(c) Landing where the ferry used to be.** The highline ferry waits 1.1 s at a stop, while a low-gravity jump onto it flies about 3 s.

     **Fix.** `NavAgent.AirTargetX`: an aerial edge onto a platform stop steers to where the platform is now.
   - **(d) Riding the lift forever.** Lincoln rode the spire lift up and down, waiting on it for the ferry: a platform-to-platform jump needs both platforms there at once.

     **Fix.** `PlatformTransferPenaltySeconds` (6) on edges between two different platforms' stops, so the plan waits on the static mast head.

   **Result:** Level 12 Easy completes for Joan (four runs) and Lincoln. Normal now reaches the boss pad and ends on combat deaths.

**Also fixed (found on Level 6 after the vault).** `OutrunEscape` now goes on once the hero is past the escape's `FinishX` (+20). It adds one nav note (`escape <id>: still running with the hero past its finish …`), so the underlying game bug (§6) stays visible. Before this, the bot idled past the finish line "escaping" until the timeout.

## 3. Files

- `scripts/Diagnostics/PlaytestBots.cs`
  - The lock-target approach: `LockTargetPerch`, `LockTargetGoal`, `LockWalkInClear`, `WatchLockWalkIn`, `StandingSurface`, `EngagedLockRequires`, and the ground-only `inRange`.
  - Gear and plate tasks.
  - The short-hop down-air and `PrefersBeside`.
  - `RunsOffLedge` (internal, tested) and the braces.
  - The escape finish.
  - The class doc.
- `scripts/Diagnostics/PlaytestWorld.cs`: `ClearWalk`, `Gears`, `GearPlan`, `PuzzleOf`, the sibling `InteractionAreaOf`, and the Godot-free `PlaytestPuzzleMath`.
- `scripts/Diagnostics/PlaytestNav.cs`: the flush-stop boarding edge, `NavAgent.AirTargetX` (internal, tested) and the platform-transfer penalty.
- `scripts/Diagnostics/PlaytestEncounterStats.cs`: the per-instance `LockTracker` / `LockRecord` / `LockObservation`.
- `scripts/Diagnostics/PlaytestTelemetry.cs`: the instance ID, per-instance routing and the report keys.
- `tools/playtest/compare_runs.py`, `trace.py`, `run_playtests.ps1`.
- `tests/Unit/PlaytestEncounterTelemetryTests.cs`, and this handoff.
- **Outside my ownership:** none.

## 4. Tests

**`PlaytestEncounterTelemetryTests`: 6 → 15, +9 new cases, 0 rewritten.** The `BotLockScene` harness gained:
- a `BuildLock(spawns, breakables, puzzleIDs)` overload (the old `BuildLock(nearOnly)` now calls it);
- `AddSolid`, `AddBlock`, `AddGear` and `AddPlatform`.

| Case | Kind | Pins |
|---|---|---|
| `ALockRebuiltByASceneReloadIsItsOwnInstanceRecord` | pure | #12 and #17. Live, restored-Cleared and re-armed instances get separate records: no relabelling, plan / reverts / frames-to-clear kept, kills ≤ plan per instance, no leave on a respawn, distinct counts and `Reloads`. Instance 0 behaves as before. |
| `TheGearPlanIsTheFewestPressesThatAlignTheWholeTrain` | pure | `GearPresses`: Florence's train is 3,3. Also covers an aligned train, a lone gear, an unsolvable train, and malformed or oversized inputs. |
| `TheBotBreaksABlockBetweenItAndALockMemberInsteadOfPushingAtIt` | real frames | #11. A slasher behind a 96 × 480 breakable wall, with the bot on the near side 150 px away: the wall breaks (`break_path`) and the member dies. |
| `TheBotClimbsOntoTheLedgeALockMemberHoldsInsteadOfJumpingAtItFromBelow` | real frames | Gap 3. A slasher on a solid ledge 200 px up, with the hero under it: the hero stands on the ledge, with no `air_strike`. |
| `TheBotStandsOnALockPlateWhoseCentreAPropCovers` | real frames | Level 6 (c). A 2-unit prop on a 3-unit plate that a zero-member lock requires: the bot stands on it and the lock clears. |
| `TheBotHopsShortToDownAirABreakableJustBelowItsLedge` | real frames | Level 6 (b). The wedge geometry, required by a lock: it breaks and the lock clears. |
| `TheBotPlansAGearTrainAndPressesGearsThroughTheirSiblingAreas` | engine, synchronous | Gap 1. Sibling-area lookup, a `GearPlan` of 3,3, pressing by the plan through the areas, and a solved train's empty plan. |
| `TheNavBoardsAFlushLiftAndTracksAPlatformThatLeftItsStop` | engine | Gap 5 (b) Walk edge, (c) `AirTargetX` 7500 → 7620, and the `RunsOffLedge` helper (a narrow perch drops; the open floor does not). |
| `TheBotGoesOnPastAFinishLineWhoseEscapeNeverCompleted` | engine | Past `FinishX`, not `escape`, plus the nav note; behind it, `escape`. |

**Mutation check.** With the WIP committed, every fix was disabled at once:
- `lockTarget = false`;
- `reachedAt: false`;
- `_airStrikeShortHop = false`;
- the escape finish guard off;
- the boarding edge off;
- `AirTargetX` returning `LandX`;
- `InteractionAreaOf`'s sibling search off;
- `LockTracker` keyed by ID only.

Each of the 8 behaviour cases then failed on its own assertion:
- "A rebuilt lock must start a new instance record."
- "never broke the wall … actions idle,break,approach; hero x 4932"
- "never got onto the member's ledge … air_strike,approach"
- "never stood on the plate (lock_wait)"
- "never broke the wedge (wedge HP 30)"
- "sibling interaction area must be found"
- "No edge boards a lift stop flush with the floor"
- "A hero past the finish line has nothing left to outrun"

The plate case first passed under the mutation, because a hero still in the air reads every point as reachable (the bot's optimistic airborne rule, cached two seconds). The case now waits for the hero to land, as Level 6's hero does, and then failed as expected.

Not covered by a mutation:
- **`AirTargetX`**: its assertion follows the boarding-edge assertion in the same case. Reasoning: reverted, it returns 7500 against the asserted 7620.
- **The gear-solver case**: it tests new code.
- **No test of their own:**
  - the wiring of `RunsOffLedge` into the two dodges;
  - the platform-transfer penalty;
  - `PrefersBeside`.

  These rest on the Level 12 and Level 11 bot runs (§5) and are named here as untested.

**Runs** (each holding `D:/Projects/.ftt-gdunit-lock`):

| Run | Result |
|---|---|
| `PlaytestEncounterTelemetryTests` + `PlaytestCombatStatsTests` + `PlaytestBotStrikeRangeTests` | **22/22** |
| **Full suite** | **`Passed! - Failed: 0, Passed: 3189, Total: 3189`** (1 m 18 s) = 3180 + 9. The `[TestCase]` attribute count under `tests/` is 3189. |

`dotnet build`: 0 errors and the three pre-existing warnings (CS8632, CS0649, KLSG_ECS004). No `KLOTHO_DET002`.

## 5. Bot runs from this worktree (`play`, `--max-seconds=0` unless noted)

Before this workstream, the Phase C batch (`guc*`) ended `stuck_lock` on Levels 1, 4, 5, 6, 7, 9 (Hard), 10 and 11, and Level 12 Easy collapsed on the Timer. Results on the final build:

| Level | Hero / difficulty (tag) | End | Locks |
|---|---|---|---|
| **1** | joan normal (`fxbotfinal`) | **level_complete**, 217 s | 4/4 |
| **4** | joan normal (`fxbotfinal`) | collapse (Death: the Governor's Guard) | 3/3 |
| **11** | joan normal (`fxbotfinal`) | `stuck_lock:level_11_gettysburg_lock_angle`: a game bug, see §6 | 2/3 |
| 11 | lincoln normal (`fxbotc`) / joan normal (`fxbotn`, before `PrefersBeside`) | level_complete, 301 s / 343 s | 3/3 |
| 6 | joan normal (`fxbotfinal`) | level_complete, 172 s | 3/3 |
| 7 | joan normal (`fxbotfinal`) | level_complete, 182 s | 2/2 |
| 10 | joan normal (`fxbotfinal`) | collapse (Death: the boss) | 3/3 |
| 12 | joan normal (`fxbotfinal`) | collapse (Death): two deaths in the spire lock, then two to the Gravity Overseer on the pad | 3/3 |
| 12 | joan easy / lincoln easy (`fxbot9`) | level_complete, 314 s / 184 s | 3/3 |
| 1–15 | joan normal (`fxbotn`) | 0 `stuck_lock`; every lock cleared on every level; ends are completions (4, 6, 8, 11, 13) or combat deaths | |
| 1, 4, 5, 6, 11 | lincoln normal (`fxbotn`) | level_complete | all |
| 9, 11, 7, 4, 10 | joan hard (`fxboth`) | combat deaths (Hard's single rewind); no `stuck_lock`. The Level 9 seawall clears | |

Single samples: seed-0 runs on this machine are not frame-identical across launches (CLAUDE.md).

## 6. Not fixed and game bugs found

- **Level 11 Angle, `stuck_lock` (game bug, FIX-OBJECTIVE's ground).** Run `L11_joan_play_normal_fxbotfinal`:
  1. Array Alpha breaks at frame 4740, and the wheatfield lock clears at 4738.
  2. The hero dies in the Angle at frame 7575.
  3. The death rewind restores Alpha to 160 HP (the `puzzles` event at 7801).
  4. The Angle lock requires both arrays, and Alpha now stands behind the Angle's closed entry barrier, so the lock can never clear.

  This is the "lock-required breakable a rewind un-solves" defect FIX-OBJECTIVE is fixing. The bot does not work around it. `fxbotc` shows the same thing.
- **Level 6's lava run never completes (game bug).** The finish trigger (`Level06Controller.WireEscapeFinishLine`) is a 96 px area centred on `FinishX` 8880, and it calls `EscapeSequenceController.NotifyPlayerReachedFinish` only on `BodyEntered`. A hero who runs east enters it with its pivot about 60 px short of `FinishX`. The call is refused and never fires again.

  The front stops at `EndX` 8800, so it never catches a hero past the line either. `RunningEscape()` stays true, and the objective stays `pompeii_objective_outrun`. This is the defect GU-L12 fixed on Level 15 with a per-advance `NotifyPlayerReachedFinish` (`Level15Controller.WireFirestormFinishLine`); Level 6 lacks it.

  Seen in `L06_joan_play_normal_fxbot2` from about 95 s: the bot idled at x 8953 for the rest of the run. The bot now goes on, with a nav note.
- **Flyers out of reach** (Level 3's shock drones, Level 13's high Rift Phantoms at y ≈ −3400, Level 14's hologram drones): `stuck_lock` in the batch, FIX-ENEMY's ground. The bot still `AirStrike`s and `lock_hunt`s them, unchanged.
- **Level 7's boarding lock** (Normal and Hard) now ends on deaths. The bot hops between skiffs under deckhand fire from 400+ px, so the damage is booked as `env`. That is traversal under fire, not a stall.
- **Level 12 Normal** reaches the pad but dies. It took vent, extractor and brace damage on the mast head, plus Eraser and Void Enforcer combat in the spire lock.

## 7. Decisions (candidate `PROVISIONAL-BOT-ENCOUNTER-RULES` amendments, diagnostics only)

1. **What changed outside locks.** The lock-member approach, the plate and gear tasks, the short-hop down-air and `PrefersBeside` apply only to lock targets or inside an engaged lock; outside locks the old behaviour stands.

   Five changes apply everywhere, because the stalls they fix are not lock stalls:
   - the escape finish;
   - the ledge-safe dodges;
   - the flush-stop boarding edge;
   - the moving-platform landing tracking;
   - the platform-transfer penalty.
2. **Walk-in rule.** A clear Environment ray from chest (−36) to centre (−30), and the same nav surface (a temporary or platform surface exempt). A stall is 30 still frames (< 2 px, < 20 px/s, Idle / Running); after one, the agent drives for 180 frames.
3. **The member's surface.** A nav segment within 16 px of its feet (±16 px vertically, 24 px x-margin). A plan to it ends on it only (`PerchMaxBelow` 40). A member on no surface keeps the old 300 px.
4. **Perch.** Applies to a member at least 60 px off the hero's level on another reachable surface. The stand point is 60 px on the hero's side, inset 14 px. A lock member that stands on a surface is struck from the ground.
5. **Short hop.** Inside an engaged lock, when the full jump's apex plus 125 px of down-air is short of the target top + 8 px: Jump held 2 frames, the down-air on frame 3.
6. **`PrefersBeside`.** Inside an engaged lock, when a base stand point (±20 px from the box, 46 px below) is reachable, it is preferred, and the jumping up-attack is skipped.
7. **Gears.** Trains of at most 6 gears are searched (4^n). Only the engaged lock's required puzzles are worked, with one press per 30 frames.
8. **Plates.** For the engaged lock's required puzzles only, any reachable `PlatePoint` makes the plate a task.
9. **Dodges.** Less than 64 px of surface before a drop refuses a dodge in that direction; with no way out, the bot braces with Block.
10. **Escape.** Past `FinishX` + 20 px, the bot goes on, with one nav note per escape.
11. **Nav.**
    - The boarding Walk edge requires the stop within the floor's span ±4 px and its top within 12 px; its cost is 0.1.
    - The landing x follows the platform's offset from the planned stop.
    - A platform-to-platform edge costs +6 s.
12. **Lock telemetry.** One record per lock instance; `count` and `cleared` are distinct IDs; `reloads` = instances − IDs. `cleared` counts a lock any instance of which was seen Cleared (live or restored).

## 8. For the closeout

**CLAUDE.md, the bots section:**
- "How the bots move":
  - lock members are reached around solid obstacles and up onto reachable ledges;
  - an engaged lock's gear trains and plates are solved;
  - dodges never run off a ledge;
  - flush lift stops are boarded;
  - moving-platform landings track the platform.
- Retire the sentence that the Level 6 bot "does not stand on the east pan as the winch's missing counterweight". It now solves the winch; see §2.2.
- Note the Level 6 escape bug (§6) and the bot's "past the finish" note.
- "Reading a report": the `locks` block's `instances` / `reloads` / per-row `instance`, distinct `count` / `cleared`, the `#n` event labels, and the CSV's `lock_reloads`.
- Comparability: `cleared%` / `planned` / `killed` from reports before this fix over-count on Act III runs with Snaps.

**Ledger:**
- Amend `PROVISIONAL-BOT-ENCOUNTER-RULES` with §7.
- Add the Level 6 escape-finish bug as a `VERIFY-*` row: owner Unassigned; acceptance = the run completes for a hero who crosses the line in one pass, e.g. the Level 15 per-advance check.
- The Level 11 rewind-restores-Alpha case belongs with FIX-OBJECTIVE's row.

**AGENTS.md:** the baseline gains +9, all in `PlaytestEncounterTelemetryTests` (6 → 15).

**Design-master lines owed:** none. Diagnostics only.
