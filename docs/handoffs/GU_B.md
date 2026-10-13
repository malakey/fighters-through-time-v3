# GU-B handoff: the closer Story camera (gameplay update 2026-10-12)

**Workstream:** GU-B, request 2 ("The viewport should be closer to the character and enemies … it should be a closer view on the player"), Story only. Plan: `docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md` §2.2, §4 GU-B. Recon: `camera.md`, `critic.md` §1.3, §2.A, §2.B.
**Branch:** `worktree-wf_21dd1c67-9be-2`, base `b8f5343` (the worktree was created at `d2e52e0` and fast-forwarded to the plan commit before any edit).
**Status:** complete. **Nobody has looked at it.** Every claim below comes from headless tests and smoke runs. Nobody has seen the closer view, the boss framing ease, the shake or the watcher on a screen.

## 1. Shipped

### `FTT.Environment.StoryCameraRules` (pure, the one home of every number)

| Member | Value | Notes |
|---|---|---|
| `ReferenceViewportWidth` / `ReferenceViewportHeight` | 1920 / 1080 | The pinned content scale; `ViewWorldSize`, `FramingZoom`, `BoundsFitZoom` and the confiner's `ViewSize` fallback read them. |
| `ViewZoom` | **1.5** | The Story zoom (Godot 4: above 1 is closer). |
| `ViewWorldSize` | (1280, 720) | `static readonly`, = reference / `ViewZoom`. For tests and GU-E's lock `CameraBounds` rule. |
| `FramingMarginPixels` | 120 | World px kept clear on each side of a framed area. |
| `ZoomDampingSeconds` | 0.6 | 99 % of a zoom gap closes in this time (the same `DampFactor` damper as the follow). |
| `BossFramingMinZoom` | 1.0 | The widest a boss or Mirror framing may pull: the pre-update view. |
| `FramingZoom(float areaWidth, float minZoom)` | `clamp(1920 / (areaWidth + 2 × 120), min(minZoom, ViewZoom), ViewZoom)`; `ViewZoom` for `areaWidth ≤ 0` | The plan's formula. |
| `BoundsFitZoom(float boundsWidth, float boundsHeight)` | `max(1920 / w, 1080 / h)`; 0 for degenerate bounds | The smallest zoom whose view still fits the confinement rect (the room floor, §3). |
| `FramedCentre(float followX, float spanStart, float spanEnd, float halfView, float heroLeash)` | `clamp(ConfineCenter(followX, spanStart − 120, spanEnd + 120, halfView), followX ± heroLeash)` | Where a framing pulls the view centre (§3, deviation 1). |

### `FTT.Environment.StoryCameraConfiner` (new partial `scripts/Environment/StoryCameraConfiner.Framing.cs`, plus `.uid`)

- **A parameterless constructor** sets `Zoom = (ViewZoom, ViewZoom)`. The zoom is therefore in place before `_Ready`'s first `SetBounds` and before `EnableFollow`'s cut. All four Story camera sites (`HubWorldController`, `Level00Controller`, `Level01Controller`, `StoryLevelControllerBase`) get it with no code of their own. `FighterCamera`, the Test Arena's plain `Camera2D` and the Holodeck drills' `FighterCamera` cannot.
- `public void RequestFraming(object owner, Rect2 area, float minZoom)`: a repeat request from the same owner replaces its earlier one.
- `public void ReleaseFraming(object owner)`
- `public bool IsFramingFor(object owner)`
- `public int FramingRequestCount` (live requests only)
- `public float TargetZoom`: `ViewZoom` with no live request. Otherwise it is the **smallest** live request's fit zoom (the widest view wins), floored at `BoundsFitZoom` of the current `Limit*` rect and capped at `ViewZoom`.
- `public float FramingWeight`: 0–1, the eased strength of the framing's horizontal pull.
- `public Vector2 VisibleWorldSize`: `ViewSize()` at the current zoom.
- `public Rect2 VisibleWorldRect`: centred on `RigCenter` while following, else on `GetScreenCenterPosition()`.
- `public static StoryCameraConfiner FindOn(Node host)`: searches direct children only, using `GetChildCount` / `GetChild`, so no collection is allocated. Callers cache the result.
- Private members:
  - `StepZoom(delta)`: damped toward `TargetZoom`; snaps within 0.0005.
  - `SnapZoomToTarget()`: a cut cuts the zoom too.
  - `FramedFollowX(followX, view, delta)`, `TryGetLiveFramingSpan`, `EaseFramingWeight` (settles within 0.001), `PruneFramingRequests`.
  - An owner that is a `GodotObject` and has been freed counts as released.

**Edits in `StoryCameraConfiner.cs`**, all in follow and zoom members; `SetBounds`, `Confine` and the `Limit*` handling are untouched:
- class doc (+2 lines)
- `SnapToTarget`: +1 line, `SnapZoomToTarget();`
- `StepFollow`: +3 lines at the top (`StepZoom(delta);`). The three-line X `FollowAxis` call is replaced by `float x = FramedFollowX(follow.X, view, delta);` plus a two-line comment. **With no framing live, `FramedFollowX` makes exactly the old `FollowAxis` call with the same arguments.**
- `ViewSize`: doc, and the fallback reads the rules' reference size.

### `FTT.Core.CameraShake`

- `_Process` now writes `_camera.Offset = ScreenStableOffset(_camera, new Vector2(offsetX, offsetY))`.
- `internal static Vector2 ScreenStableOffset(Camera2D camera, Vector2 worldOffset)`: divides by the camera's current `Zoom` (per axis) when it is a `StoryCameraConfiner`, and returns the offset unchanged otherwise.
- `Shake()`, `_shakeIntensity` and `_shakeDuration` are untouched and stay in world pixels, so `StoryEnemyCombatTests`' reflection pin holds.
- The Fighter shake is unchanged.

### `FTT.Enemies.BossEncounterController` (framing calls only)

- `RequestArenaFraming()` runs at the top of `Reveal(bool skipIntroBeat)`, right after `IsRevealed = true`. That one site covers all three reveal paths: the first viewing's intro beat, a seen intro's skip, and a dormant boss woken by a hit (`RevealWokenBoss`, deferred off a physics flush as before).
- It resolves `_player` from `StoryPlayer` if needed, caches `_framingCamera = StoryCameraConfiner.FindOn(_player)`, and calls `RequestFraming(this, ArenaBounds, StoryCameraRules.BossFramingMinZoom)`.
- A radius-only encounter (`!HasArena`) or one outside the tree frames nothing.
- `ReleaseArenaFraming()` runs in `OnBossDefeated` (after `HideBossBar`) and in `_ExitTree`, which is now a block: `UnbindEvents(); ReleaseArenaFraming();`.

### `FTT.Enemies.MirrorParadoxEncounterController` (framing calls only)

- The same pair as the boss controller. `RequestArenaFraming()` runs in `Reveal()` before `BeginEncounter`, and frames nothing when `ArenaBounds.Size.X ≤ 0`.
- `ReleaseArenaFraming()` runs in `OnBossDefeated` and in `_ExitTree`.
- `_ExitTree`'s doc now names the framing release among the registrations it drops.

### `FTT.Environment.Level00Controller` (watcher placement only)

- `public const float WatcherViewInsetPixels = 160f;` and `private const float WatcherFallbackX = 120f;`
- `public Vector2 EraserWatcherPosition()`: returns `(camera.VisibleWorldRect.Position.X + 160, TutorialFloorTopY)` using the hero's Story camera, or `(120, TutorialFloorTopY)` with no camera.
- `ShowEraserWatcher` uses it. The figure, tween and timing are unchanged.

## 2. Files changed

| File | Change |
|---|---|
| `scripts/Environment/StoryCameraRules.cs` | constants and three pure functions (§1) |
| `scripts/Environment/StoryCameraConfiner.cs` | four localized edits (§1) |
| `scripts/Environment/StoryCameraConfiner.Framing.cs` (+ `.uid`, `uid://dpl4nx4bsumu4`) | **new** partial |
| `scripts/Core/CameraShake.cs` | offset compensation |
| `scripts/Enemies/BossEncounterController.cs` | framing request and release only |
| `scripts/Enemies/MirrorParadoxEncounterController.cs` | framing request and release only |
| `scripts/Environment/Level00Controller.cs` | watcher placement only |
| `tests/Unit/StoryCameraRigTests.cs` | +10 cases, 2 extended in place |
| `docs/handoffs/GU_B.md` | this file |

**Outside my ownership:** none. The import churn (`*.import`, `resources/Audio/default_bus_layout.tres`) was discarded before committing.

**Merge note for GU-E (same class):**
- My edits to `StoryCameraConfiner.cs` sit at the top of `SnapToTarget`, at the top of `StepFollow`, and on the X follow statement in the middle of `StepFollow`. If GU-E also adds a step line at the top of `StepFollow`, keep both. Run `StepZoom` first, so the lock layer's confinement reads this frame's zoom.
- `TargetZoom`'s room floor reads the effective `LimitLeft/Right/Top/Bottom`. If the lock layer confines through `Limit*`, it composes for free. If it confines through a separate rect, point `BoundsFitZoom` at that effective rect instead (one line in `TargetZoom`).

## 3. Decisions (Provisional; each becomes a ledger row)

1. **`ViewZoom = 1.5`**, as the plan states.
   - Ledger row: `PROVISIONAL-STORY-CAMERA-ZOOM`.
   - Reason: "closer" with the hero at about 14 % of the screen's height (103 → 155 px idle), roughly the Fighter camera's closest framing.
2. **Several live framing owners: the widest view wins** (the smallest target zoom).
   - Reason: it is commutative, so request order never matters, and no owner's area is cut off by another's.
   - Effect: a repeat request replaces; a freed `GodotObject` owner counts as released.
3. **Room floor on the framing zoom.** A framing never widens the view past the camera's current confinement rect: `TargetZoom ≥ BoundsFitZoom(Limit* rect)`, capped at `ViewZoom`.
   - Reason: a view wider than its room makes the rig's centring and Godot's built-in limits disagree (recon §1.6).
   - Only Level 4 is affected: the arena (1580 px) would fit at 1.055, but the courtyard room is 1600 px, so the framing stops at **1.2** and the view is exactly the courtyard.
4. **A cut cuts the zoom.** `SnapToTarget` (load, `EnableFollow`, a teleport of more than 900 px) snaps the zoom to its target instead of easing across the cut.
   - Reason: it matches "no lead and no damping".
   - The horizontal pull is not cut. It keeps easing from wherever the cut leaves it.
5. **The framing also slides the view onto the arena.** This addition is deviation 1 (§4).
   - The pull's follow target is `FramedCentre`, leashed to `softHalf − deadHalf` (0.25 × the view's width) from the hero's follow point.
   - It eases in and out with the zoom (`FramingWeight`, `ZoomDampingSeconds`).
   - The `FollowAxis` step toward it runs with the dead zone and X damping but **no** soft-zone catch-up toward the framing point, so nothing snaps at the reveal.
   - The final X is clamped to the hero's soft zone (exactly the old rig's soft-zone constraint).
   - Only X is framed; Y follows the hero as before.
6. **Framing lasts from the reveal to the defeat or teardown**, whatever the hero does in between.
   - A death rewind or a walk that leaves the hero outside the arena keeps the wider, arena-pulled view (the hero is still kept in the soft zone).
   - Reason: the boss is engaged for that whole time and stays leashed to the arena.
   - Nothing un-reveals a boss today.
7. **Shake compensation divides by the camera's *current* zoom on both axes.** A shake during a framing ease therefore stays the same size on screen as well.
8. **The watcher sits at the visible left edge + 160 px, standing on the floor** (y 850 → `TutorialFloorTopY` 900).
   - The old y left the silhouette's feet 50 px above the floor.
   - Fallback x when no Story camera is found: the old 120.

## 4. Deviations from the plan

1. **The framing adds a horizontal pull onto the arena.** The plan specified only the zoom formula.
   - Why: a zoom that fits the area's width only frames the area if the view is also over it.
   - Measured on Level 2 (arena 9300–10540, the Duke at 10200): a hero who has just stepped in at x 9310 gets a settled view ending at about 10050 with zoom-only framing, so the Duke stays off-screen. With the pull the view ends at about 10346, and the hero sits 296 px from the centre, inside the 444 px soft zone.
   - With no request live the rig is unchanged.
   - Pinned by `AFramingSlidesTheViewOntoTheArenaWhileTheHeroStaysInTheSoftZone`.
   - If the closeout prefers zoom-only, revert one statement in `StepFollow` to `FollowAxis` and drop `FramedFollowX`.
2. **The room floor** (decision 3) is an addition to the plan's formula.
3. **The plan's "two owners" choice** is decided as widest-wins (decision 2), per the brief's either/or.

## 5. Tests

**New cases (+10), all in `StoryCameraRigTests` (7 → 17):**
- `TheFramingZoomFitsTheAreaAndItsMarginsBetweenTheFloorAndTheViewZoom`: pure; `FramingZoom` for the L2, L1 and Mirror widths, floors and degenerate input; `BoundsFitZoom`; `FramedCentre`.
- `EveryStoryCameraStartsAtTheViewZoomBeforeItsFirstBoundsAndCut`: the constructor zoom; the first cut confines a 1280 × 720 view; `VisibleWorldRect`.
- `AFramingRequestEasesTheZoomOutToFitItsAreaAndAReleaseEasesItBack` covers:
  - The ease never cuts, and the first frame lands strictly between the old and target zoom.
  - 99 % of the gap closes in 36 frames (0.6 s).
  - The view stays confined at every intermediate zoom.
  - The settled view holds the whole arena.
  - The release eases back to 1.5 and onto the hero.
  - The Level 4 room floor stops the framing at 1.2.
- `AFramingSlidesTheViewOntoTheArenaWhileTheHeroStaysInTheSoftZone`: the far-side boss is in view, and the hero stays in the soft zone on every frame, including after walking out of the arena.
- `TwoFramingOwnersHoldTheWidestViewUntilBothReleaseAndAFreedOwnerFramesNothing`
- `TheHubAndFlorenceCamerasCarryTheStoryZoom`: the real scenes.
- `LevelZerosCameraCarriesTheZoomAndTheWatcherStandsInsideTheView`: the real scene. With the hero at x 1300 the old spot (120) is off-screen; the watcher is at view left + 160 on the floor, and the whole figure is in view.
- `ABossRevealFramesItsArenaOnTheHerosCameraUntilTheDefeat`:
  - The first-viewing beat path frames the arena, and the defeat releases it.
  - The woken-by-a-hit path frames the arena, and freeing the encounter (`_ExitTree`) releases it.
  - The Integrity `BossIntro` pause is restored in `finally`.
- `TheMirrorRevealFramesItsArenaUntilTheDefeat`: the real `MirrorParadoxEncounterController`; the clone is killed with `ApplyDamage(1000)`.
- `TheShakeKeepsItsWorldIntensityAndDividesAStoryCamerasOffsetByItsZoom`:
  - `ScreenStableOffset` on a Story camera and on a `FighterCamera` at zoom 1.4 (left unchanged).
  - A live `_Process` step, whose written offset equals the reconstructed noise × intensity / 1.5 while `_shakeIntensity` stays 12.
  - Every `CameraShake` field it touches is restored in `finally`.

**Rewritten in place (no count change):**
- `TheRulesCarryTheDesignTable`: + `ViewZoom`, `ViewWorldSize`, `FramingMarginPixels`, `ZoomDampingSeconds`, `BossFramingMinZoom`.
- `TheCampaignLevelCameraIsTheRigChildOfTheHero`: + `Zoom == ViewZoom`, `TargetZoom`, no framing at load, `FindOn(Player) == Camera`.

No other test file changed. Every case that writes `CurrentSession.ActiveSaveSlot` or `SelectedCharacterID` restores it, restores `SceneTree.Paused`, and frees its grouped `StoryPlayer` in `finally`.

**Runs** (each held `D:/Projects/.ftt-gdunit-lock`, with orphans drained first):

| Run | Result |
|---|---|
| Targeted: `StoryCameraRigTests`, `StoryLevelControllerBaseTests`, `PuzzleEnvironmentToolkitTests`, `StoryEnemyCombatTests`, `CollapseTremorTests`, `BossArenaLeashTests`, `MirrorParadoxTests`, `Level00FlowSceneTests`, `Level13ContentTests` | `Passed: 151, Total: 151` (= 17 + 20 + 10 + 21 + 9 + 19 + 30 + 3 + 22, the `[TestCase]` counts of those nine files). There are no `BossEncounter*` suites; the boss reveal suites are `BossArenaLeashTests`, and `BossPhaseMechanicsTests` is in the full run. |
| Full suite, twice: once before and once after a final no-behaviour-change cleanup (a single read of `Zoom`, a field rename, doc) | `Passed!  - Failed: 0, Passed: 3031, Skipped: 0, Total: 3031, Duration: 1 m 6 s`, both runs (74 s and 78 s wall clock). Both logged the same seven GdUnit orphan-node warnings (3/290/13/41/41/328/1), which are documented as noise. `StoryCameraRigTests` run alone logged none (17/17). |

**Reconciliation:** 3021 + 10 = **3031**, which is also the `[TestCase]` attribute count under `tests/`.

**Smoke runs** (`--headless --quit-after 300`, exit 0, no script errors):
- `HubWorld.tscn`: clean.
- `Level_00_Tutorial.tscn`: 6 leaked ObjectDB instances and 3 resources in use, the pre-existing figures AGENTS.md records for Level 0.
- `Level_02_Orleans.tscn`: 2 / 1, the control-level figures.

## 6. Measurements

- **View at 1.5:** 1280 × 720 world px (21.3 × 12 units). The idle hero (103 world px for most heroes) draws 155 px tall on screen; the 40 × 64 hurtbox draws 60 × 96.
- **Framing zoom per arena.** Arena widths are from `camera.md` §2; L2, L4 and L13 were re-checked in code. View widths are in world px.

| Level | Arena width | Fit zoom | View width | Note |
|---|---|---|---|---|
| L1 Florence | 760 | 1.5 | 1280 | no change: the arena already fits |
| L2 | 1240 | 1.297 | 1480 | |
| L3 | 1220 | 1.315 | 1460 | |
| L4 | 1580 | 1.055 | 1600 | **1.2** by the room floor (courtyard 1600 wide) |
| L5 | 1580 | 1.055 | 1820 | |
| L6 | 1290 | 1.255 | 1530 | |
| L7 | 1230 | 1.306 | 1470 | |
| L8 | 1320 | 1.231 | 1560 | |
| L9 | 1740 | 1.0 | 1920 | floor; R4 is exactly 1920 wide |
| L10 | 1220 | 1.315 | 1460 | |
| L11 | 1500 | 1.103 | 1740 | |
| L12 | 1600 | 1.043 | 1840 | |
| L13 Mirror | 2380 | 1.0 | 1920 | floor |
| L14 | 1540 | 1.079 | 1780 | |
| L15 | 1650 | 1.016 | 1890 | |

- **Ease:** 0.6 s to close 99 % of the gap. From 1.5 to Level 2's 1.297 it settles exactly at about 0.78 s, which is inside the 1.5 s intro beat. The horizontal pull settles at about 0.9 s.
- **Shake:** on-screen amplitude = authored world intensity, as at the old 1.0 view. It was 1.5× before the compensation.

## 7. Not delivered / open

- **Nobody has looked at it.** Unreviewed:
  - sprite and backdrop softness at 1.27 screen px per texel;
  - world-text legibility;
  - whether the boss ease reads well;
  - whether vertical follow in 1080-tall rooms (new at 1.5) feels right.
- **Recorded, not fixed (plan §2.2):**
  - room-name labels at world y 30 are off-screen;
  - the high optional Extractors (L2 8560/260, L4 5700/228, L6 4980/280) are hidden from the floor;
  - L3's routing hall and the L4/L9 searchlight heads no longer fit one screen.
- **Off-screen threats:**
  - Ranged mobs (engage at 504–576 px, the L9 Lantern-Eye Archer at 810) can fire from beyond the 640 px half-view.
  - No off-screen threat indicator exists.
  - GU-F's `RangedHoldMaxPixels` (560) bounds holding, not aggro.
- **Level 5's Captain Smith beat** at (10820, 520) plays as the boss falls, and the framing release eases the view back in at the same moment. It was already often off-screen at 1.0.
- **The Mirror reveal** is a 700 px radius around the clone, so the clone is about 560 px off-screen for the first ~0.6 s of the ease.
- **Framing is horizontal only.** Arena heights are ignored. A tall arena's vertical extent is framed only by the zoom-out.
- **Framing persists until the defeat** even when the hero leaves the arena (decision 6).
- **Low-gravity levels** (L12/L13) show less above and below the hero at 1.5 (recon §4.4). Unchanged.

## 8. For the closeout

**Ledger rows to write:**
- `USER-STORY-CAMERA-ZOOM`: quote request 2 and the Story-only scope.
- `PROVISIONAL-STORY-CAMERA-ZOOM`: 1.5; the hub and Level 0 included; shake compensated; the 120 px margin, 0.6 s ease and 1.0 floor; widest-wins owners; the room floor; the horizontal pull and its leash; framing until defeat; the watcher at +160 on the floor.
- Amend `F8-LEAD-AXIS` and the F8 camera row (the zoom is now a rig input).

**AGENTS.md:**
- The "Boss arenas, enemy hit geometry, gates and the Story camera" bullet (Scene architecture): add the 1.5 zoom and the reveal-to-defeat arena framing. Its sentence "The rig drives the camera's position and never `Offset`, which `CameraShake` owns" stays true; add that `CameraShake` divides a Story camera's offset by its zoom.
- The scene-scoped systems table needs no row; the confiner is not new.
- Baseline 3021 → 3031 for this workstream's part.

**CLAUDE.md:** no change from GU-B.

**Design-master lines owed** (`design-godot.md`, line numbers at `b8f5343`):
- `:3130–3143` "Story Mode Camera2D Configuration": add rows for Zoom `1.5`, Boss framing (arena + 120 px a side, eased 0.6 s, no wider than 1.0, with the horizontal pull), and Zoom damping.
- `:3122` Story Mode Camera System prose: a closer fixed zoom.
- `:2399` Story-mode `CameraShake`: the offset is divided by the Story camera's zoom.
- `:3529` Boss Introduction: "the camera clamps" becomes "the camera eases out to frame the arena".
