# Recon dossier C — Engine/architecture, movement, and the 2026-09-11 **Time Freeze** rework

**Scope:** master §4 (lines 522–988 of `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md`) plus the Time Freeze
specification that physically lives at master lines **2031–2041** (Section 3's Chronal Rewind block) and its contract docs.
Repo under inspection: `D:\Projects\Fighters Through Time - V3` @ `main` (31fed14, V7.4 pass, 2026-08-29). Read-only; nothing edited.

**Design sources read in full or in relevant part**

| Source | Relevance |
|---|---|
| `design-godot-v7.md` 2031–2041 | The Time Freeze spec itself (F03) — authoritative |
| `design-godot-v7.md` 522–988 | My assigned §4 range (scene list, SessionData/MatchSettings, CharacterState + transition table, movement defaults, pooling, persistent objects, ledge/drop-through) |
| `design-godot-v7.md` 296, 308–318, 380–392, 2189–2200, 2225–2240, 3256, 3396 | Legacy Unlock (Time Freeze at Level 0), Integrity clock pause rules, Level 0 calibration beat, input table (`TimeFreeze` R / Back-Select), difficulty table row, Story HUD rows |
| `docs/TEMPORAL_STATE_CONTRACT.md` | Full read. State matrix row "Time Freeze, Story only"; Echo Step exact-destination policy; Death Rewind landing/fallback; T01a hero-effect cleanup; T01b boss rewind |
| `docs/CHECKPOINT_RECOVERY.md` | Full read. F11 recovery budgets; "Time spent travelling during Time Freeze counts as live time" |
| `docs/DESIGN_BUILD_DEVIATIONS.md` | Full read. **VERIFY-01 — Time mechanics** is exactly this dossier's subject |
| `docs/STORY_PERSISTENCE.md` (11, 23, 31, 66, 85, 108), `docs/HUD_CONTRACT.md`, `docs/COMFORT_SETTINGS.md` (31, 41, 61, 93–128), `docs/CAMPAIGN_VALIDATION.md` (24, 70, 91, 102, 146) | Save fields, HUD radial rule, C01a/C01c comfort + remap rules, puzzle-progress-during-freeze prohibitions |

---

## 1. What the design now requires — Time Freeze (F03, 2026-09-11)

**Headline: Manual Rewind, the Stasis Anchor, the Stasis Echo, and the 12 s manual cooldown are RETIRED.**
They are replaced by a single Story-only escape ability. Death Rewind is untouched and stays a separate, death-only mechanic.
"There is no manual charge-spending route."

### 1.1 The exact contract

| Aspect | Requirement (F03 + temporal contract) |
|---|---|
| **Mode scope** | **Story Mode, single player, only.** Fighter Mode, CPU matches and Fighter training must NOT receive it. Available from Level 0 on all three difficulties, including at **zero** death-rewind charges. |
| **Input** | New dedicated action **`gameplay_time_freeze`**, single **press** (no hold, no scrub, no commit phase). Defaults **`R`** keyboard, **Back / Select** gamepad. **Migrate any customized `gameplay_rewind` binding onto the new action.** |
| **Eligibility** | Requires a living, controllable player. Unavailable during hitstun, daze, grabs, attack/ability execution, death, or scripted presentations. **Suppression does NOT lock it** (explicit carve-out — it is the universal escape tool). |
| **Duration** | **5.000 s** (300 physics ticks). Duration clock pauses in the pause menu. Ends early on death, scripted scene transition, or level exit — cleanly. |
| **Cost** | **No charges, no meter, no dust.** Never spends or refunds a death-rewind charge. |
| **Cooldown** | **45 s**, identical on all difficulties. **Starts when the freeze ENDS**, including an early end. Advances only during live Story play; pauses in menus/dialogue/death-rewind presentations. Cannot be refreshed by checkpoint activation, death rewind, Anchor Snap, or reloading the same level session. **Fresh level entry or full Restart Level starts Ready.** Remaining cooldown is **saved**. |
| **What freezes** | Everything on screen except the player, camera and UI: enemies **including bosses**, **both sides'** projectiles, hazards, moving platforms, world mechanisms. **Suspend adjacent-room simulation** so off-screen attacks cannot enter; a newly revealed/entered room is frozen **before** its actors can act. Ordinary room crossings preserve remaining duration. |
| **What the player may do** | **Run, jump, and the universal evasive roll.** Disabled for the whole freeze: basic attacks, grabs, specials, ultimate, **Echo Step**, and the character Movement Ability. Attacks attempted during the freeze are **discarded, never queued**. |
| **Escape-only guarantee** | Enemies and bosses are **invulnerable throughout**, including against already-live player projectiles, constructs, zones and DoT. No damage, stagger, **Rally reclaim**, meter generation, healing, pickups, **checkpoint activation**, **puzzle interaction/progress** (explicitly including at an adjacent-room boundary — `CAMPAIGN_VALIDATION.md` 102), or objective credit during the effect. |
| **Suspended timers** | Passive combat recovery (block-charge regen), other ability cooldowns, and status timers are suspended. Movement and the 5 s effect clock continue. "These rules override ordinary live-play regeneration." HUD radials must follow the frozen authoritative clock and **must not count down independently** (`HUD_CONTRACT.md` 16). |
| **Thaw** | All actors resume **preserved positions, velocities, attack phases and remaining timers**. **No catch-up ticks, no projectile clearing, no accumulated damage, no delayed input burst, no audio backlog** (`COMFORT_SETTINGS.md` 61/77/89). |
| **Physics during freeze** | Solid geometry stays solid; frozen hazards/projectiles cannot damage the player; **pits still apply their normal fall rules** (and under F16 Option B, Story pits are lethal). |
| **Integrity clock** | **Continues draining at the current normalized rate** — confirmed 2026-09-12. Never restore Integrity on activation, expiry or load. Existing pauses (menus, dialogue, death-rewind presentation, pre-boss lock) still apply. Keep this clock strictly separate from suspended actor timers and the 5 s effect clock. `CHECKPOINT_RECOVERY.md` 31 and `CAMPAIGN_VALIDATION.md` 24 both count freeze travel as live route time. |
| **Death Rewind interaction** | Unchanged and separate. Only a lethal event not prevented by Defy History spends a charge. Spending the final charge 1→0 completes a full rewind with normal HP restore and landing invulnerability; **zero charges is a valid living state**. `PathMovingPlatform` still reverses along its recorded path during a **death rewind**, but only **freezes in place** during Time Freeze. |
| **Save / reload** | Activation commits a **45 s reload cooldown** for any reload during that activation; background autosaves keep that conservative value while the live 5 s freeze continues (autosave is not an early thaw). **Explicit Save or exit during an active freeze ends the freeze and stores 45 s.** After thaw, snapshots store the true remaining cooldown. **Reload never resumes or renews the effect.** Checkpoint reconstruction: "Active Time Freeze never resumes; use its saved cooldown." |
| **HUD** | A **separate 32×32 px Time Freeze icon** + remaining-time label with three states: **Ready**, **active 5 s countdown**, **45 s cooldown countdown**, plus a brief **thaw warning**. Its readiness is **independent of the death-rewind count**; **never** attach its cooldown to the rewind counter. |
| **Audio/VFX** | Stop scene animations and action audio; keep player movement audio and a subdued time-stop cue. C01a Reduced Temporal Effects keeps the same 5 s / 45 s timing with calmer visuals. |
| **Tutorial (Level 0)** | Two separate beats: (a) the scripted **death rewind** demonstration (0.75 s hold, continuous playback, grounded landing) explaining the finite pool and that the last charge still saves you; (b) a **five-second escape drill** with **invulnerable enemies and a safe destination**. The drill provides a **ready freeze on every retry**, consumes **no** death-rewind charges, and teaches the long cooldown. **Outside the drill there is no recharge exemption.** |
| **Puzzle re-authoring** | "Reauthor required Echo gates with ordinary latched switches or an equivalent resource-free route; **never** replace them with a mandatory Time Freeze gate." Time Freeze may never be a required progression gate. |
| **Difficulty table** | One row, identical across Easy/Normal/Hard: `5 s; no charges; 45-second cooldown`. |
| **Remapping (C01c)** | Time Freeze gets an ordinary direct binding per device kind, labelled **Story only**; configurable globally but never usable in Fighter. Default row: `R / Back or Select, preserving migrated user overrides`. No shortcut chord. |

### 1.2 Consequential non-Time-Freeze deltas inside the same block (cross-check, likely owned by other workstreams)

* Death Rewind landing search is now **"oldest valid grounded sample"** with **full-shape clearance/support/kill-region validation**, a **re-check before release**, and an explicit fallback chain (eligible grounded history → last-known grounded → last activated physical checkpoint → implicit entrance), plus a configuration-error path that keeps the recovery pending rather than placing the player in a wall. The repo's `ChronalRewindBuffer.BuildPlaybackPath` currently selects the deepest **grounded** frame but performs **no clearance/support/kill-zone validation and no pre-release re-check**.
* **T01a hero cleanup on death rewind** (clear statuses/marks/buffs/shields/armor, cancel attack/cast/armed Echo Step/grab attachments, cancel pending healing, clear Rally pool and settle uncredited damage-taken meter once, preserve surviving constructs/zones/emitted player projectiles with their remaining lifetimes).
* **T01b First Unbound Phase-2 boss rewind** (180-frame position/HP history, ≤20% max-HP heal, 1.5 s suspended-combat presentation).
Both are big, self-contained systems; **flag for whoever owns Story combat/boss recon** — they are not in my 522–988 line range but they sit inside the same design block and share `ChronalRewindManager`.

---

## 2. What the repo does today

### 2.1 The manual-rewind / Stasis Echo stack (all to be retired)

| File | Current behaviour |
|---|---|
| `scripts/Environment/ChronalRewindManager.cs` (636 lines) | The single owner. Contains **both** the death rewind and the whole V7.2/V7.3 manual verb: `ManualHoldSeconds = 0.5f`, `ScrubFramesPerTick = 4`, `StasisEchoSeconds = 10f`, **`ManualRewindCooldownSeconds = 12f`**, `_manualCooldownRemaining`, `ManualRewindCooldownRemaining`, `IsManualRewindOffCooldown`, `StartManualRewindCooldown()`, `ScriptedFreeRewind`, `IsManualRewindFree` (Easy free), `TrackManualRewindHold()`, `CanBeginManualRewind()`, `BeginScrub()`, `AdvanceScrub()`, `ScrubPreviewPosition`, `CancelScrub()`, `CommitScrub()`, `_manualCommit`, `_spawnEchoOnComplete`, `_pendingEchoOrigin`, and the `IRewindScrubbable` fan-out (`BeginPlatformScrub` / `ApplyPlatformScrub` / `EndPlatformScrub` / `CancelPlatformScrub`). Also owns `FreezeWorldForRewind()` / `ResumeWorldAfterRewind()` / `ClearEnemyProjectiles()` / `FrozenSimulationGroups`, the collapse beat, and the checkpoint refresh. |
| `scripts/Environment/StasisEcho.cs` (135 lines) | `AnimatableBody2D`, `BodyLayer = Environment \| PersistentObject`, static `Current` singleton, 10 s life, one-way collision platform, one-projectile absorb `Area2D`, `Spawn(...)`, `AbsorbProjectile(...)`, `SetLifeSecondsForTesting`. |
| `scripts/Environment/PressurePlate.cs` L53–55 | `StasisEcho => PlayerWeight` in the weight switch. |
| `scripts/Environment/SearchlightZone.cs` L93, 112, 125 | Beam occlusion ray returns true only when the hit collider `is StasisEcho`. |
| `scripts/Environment/IInteractable.cs` L21–29 | `IRewindScrubbable { BeginRewindScrub / ApplyRewindScrub(int) / EndRewindScrub / CancelRewindScrub }` — used **only** by `PathMovingPlatform` and only for the scrub preview + death-rewind path reversal. |
| `scripts/Environment/PathMovingPlatform.cs` L15, 90+ | Implements `IStoryRewindable, IRewindScrubbable`. |
| `scripts/Core/InputManager.cs` L30–33, 47, 68 | `Actions.Rewind = "gameplay_rewind"`, listed in `RemappableActions`, label key `controls_action_rewind`. No `GameplayButtons` bit (correct — never reaches the sim). |
| `project.godot` L144–149 | `gameplay_rewind` = physical keycode **82 (`R`)** + joypad **button_index 4 (Back/Select)**. **Already exactly the Time Freeze defaults** — only the action *name* is wrong. |
| `scripts/UI/StoryHUD.cs` L38, 54, 56–58, 327–370, 510 | `RewindLabel` (`hud_rewinds`), `_lastRewinds`, and a code-built **`RewindCooldownPip`** Label appended next to the rewind counter, bound to `ChronalRewindManager.ManualRewindCooldownRemaining` via the `chronal_rewind_manager` group, text `hud_rewind_cooldown`. **This is exactly the "never attach its cooldown to the death-rewind counter" pattern the design now forbids.** |
| `scripts/Environment/TutorialCalibration.cs` | `TutorialCalibrationStep` enum contains `UseRewind` **then `UseManualRewind`**; `RegisterManualRewindComplete()`, `SkipManualRewindLesson()`. |
| `scripts/Environment/Level00Controller.cs` L208–262, 346 | `ProcessRewindDemo()` (90-frame delay, 3 attempts, `SkipRewindDemonstration` fallback), `OnRewindTriggered` → refund + set `ScriptedFreeRewind = true` + objective `tutorial_step_manual_rewind`; second rewind completes the manual lesson and clears the flag. |
| `scripts/Core/StoryManager.cs` L30, 47–57, 137, 188, 243–272, 448, 505–533, 629 | `ChronalRewindsRemaining`, `LevelRewindsUsed`, `SetRewinds`, `BeginLevelRun` refill, `OnRewindTriggered` tally. **No Time Freeze state at all.** |
| `scripts/Core/SaveManager.cs` `StorySaveData` | Schema **v5**. `CurrentLives` = rewind pool. Attempt-state fields: `ActivatedCheckpointIDs`, `FontUsesConsumed`, `DestroyedExtractorIDs`, `FoundSecretIDs`, `LevelIntegrityPercent`, `ViewedDialogueIDs`, `HasSeenCollapseBeat`. **No cooldown field of any kind.** |
| `localization/en.csv` | `hud_rewinds` (585), `tutorial_step_rewind` (605), **`tutorial_step_manual_rewind` (606)** — *"Hold R to scrub time backward - release to commit, Jump to cancel"*, `controls_action_rewind` (1084), `results_rewinds_used` (1109), **`hud_rewind_cooldown` (1146)** — *"Rewind ready in {0}s"*. Also `difficulty_*_description` (330–332) still claim Easy restores **full HP** (stale since V7.2: it is 70%). |
| Tests | `tests/unit/ManualRewindScrubTests.cs` (4 cases), `tests/unit/StasisEchoPhysicsTests.cs` (3 cases), `tests/unit/CollisionLayerTests.cs::TheStasisEchoBodyLayerReachesTheAuthoredPressurePlateMask`, `tests/unit/TutorialCalibrationTests.cs::TheDesignedStepOrderRunsAttackRallyBlockSpecialUltimateRewindManual` + `TheRewindStepsCompleteInSequenceWithNeverStrandFallbacks`, plus `ChronalRewindTests`, `ScriptedRewindTests`, `DeathTriggeredRewindTests`, `LevelDeathRewindDiagnosticTests`, `RewindCuePresentationTests`, `TimelineCollapseBeatTests`, `StoryDropsAndRewindTests`. |

### 2.2 The world-freeze surface that exists today

`ChronalRewindManager.FreezeWorldForRewind()` sweeps four groups — `"Enemies"`, `"persistent_construct"`, `"chronal_extractor"`, `MirrorParadoxController.MirrorGroup` — for `IStoryRewindSimulation.SetStoryRewindFrozen(bool)`, then **clears** every `enemy_projectile` from the pool.

Implementers of `IStoryRewindSimulation` (16 files): `EnemyController`, `BossController`, `BossEncounterController`, `EnemyProjectile`, `MirrorParadoxController(+Encounter)`, `DilationFieldZone`, `ChronalExtractor`, `PooledPlaceholder`, and the five construct nodes (`TeslaCoilNode`, `LeonardoTurretNode`, `SerpentNestNode`, `VineSnareNode`, `SonataPlatformNode`).

**Not frozen by anything today** — and all required by Time Freeze:
`StageHazard`, `StoryCyclicHazard`, `RisingWaterZone`, `SearchlightZone`, `ChronalRiftZone`, `GravityFieldZone`, `MovementDampenerZone`, `ForcefieldBarrier`, `ShieldGeneratorTower`, `CrumblingPlatform`, `TrapdoorPlatform`, `RotatingGear`, `RotatingPlatform`, `PendulumAnchor`, `PathMovingPlatform` (scrubs, does not freeze), `Counterweight`, `EscapeSequenceController`, `BeamEmitter`/`BeamReceiver`/`PowerRoutingNode`, `SequenceLock`, `RescuableNPC`, `StoryPickup`/`ChronalDustPickup`/`ChronalFeast`, **player-side projectiles/zones** (the sweep only touches enemy projectiles, and by *clearing* them). There is also **no room-scoped simulation gating** at all: `RoomTransitionTrigger` + `StoryCameraConfiner` retarget the camera and activate encounter roots, but nothing suspends an adjacent room's ticking.

`PlayerController.SetRewindSuspended(bool)` (L2129) suspends the *player* — the opposite of what Time Freeze needs (player must keep moving while the world stops).

---

## 3. Gap table

| # | Requirement | Repo status | Verdict |
|---|---|---|---|
| C-1 | `gameplay_time_freeze` action, single press, R / Back-Select | `gameplay_rewind` exists with **exactly those default events**; semantics are hold-0.5 s-then-scrub | **Partial (rename + semantics)** |
| C-2 | Migrate customized `gameplay_rewind` overrides to the new action | `InputBindingSet.Actions` is a `Dictionary<string, List<InputBindingEvent>>` keyed by raw action name in the encrypted **global** payload (schema v4). No key-rename migration exists | **Missing** |
| C-3 | 5 s freeze duration, pausing in the pause menu | Nothing | **Missing** |
| C-4 | 45 s cooldown, armed at thaw/early end, all difficulties, live-play only | 12 s cooldown armed at **commit** (`ManualRewindCooldownSeconds`), with a `ScriptedFreeRewind` exemption and an **Easy-is-free** charge rule | **Retired-code-to-replace** |
| C-5 | No charge / no meter cost; usable at 0 rewinds | Manual rewind **spends a rewind charge** on Normal/Hard (`CommitScrub` → `RemainingRewinds--`) | **Retired-code-to-remove** |
| C-6 | Freeze enemies + bosses and make them invulnerable | Freeze exists for enemies/bosses/constructs/extractors; **no invulnerability gate** anywhere | **Partial** |
| C-7 | Freeze **both sides'** projectiles, preserving them | Enemy projectiles are **released to the pool** (cleared), player projectiles untouched | **Partial/Wrong** |
| C-8 | Freeze hazards, moving platforms, mechanisms | ~25 environment classes have no freeze hook; `PathMovingPlatform` *scrubs* instead | **Missing (largest single item)** |
| C-9 | Suspend adjacent-room simulation; freeze a newly entered room before it acts | No room-scoped simulation gating exists | **Missing** |
| C-10 | Player may run/jump/roll; attack/grab/special/ultimate/Echo Step/movement-ability disabled and **discarded** | No such input mask; `SetRewindSuspended` suspends the player entirely | **Missing** |
| C-11 | No damage/stagger/Rally/meter/healing/pickup/checkpoint/puzzle/objective credit during freeze | Nothing | **Missing** |
| C-12 | Suspend block-charge regen, ability cooldowns, status timers; keep movement + effect clock | Nothing | **Missing** |
| C-13 | Thaw with preserved positions/velocities/attack phases/timers; no catch-up | The freeze flag pattern (`_rewindFrozen` early-return in `ChronalExtractor`) is the right shape and already no-catch-up; needs to be generalized | **Partial (pattern exists)** |
| C-14 | Integrity **keeps draining** during freeze | Integrity drain is extractor/`DrainTimelineIntegrityAmount`-driven and pauses via the rewind freeze sweep (`chronal_extractor` is in `FrozenSimulationGroups`). **Under Time Freeze it must NOT pause** | **Conflict to resolve** (coordinate with the V7.6 Integrity-timer workstream) |
| C-15 | Story-only; absent from Fighter/CPU/training | Manual rewind is already Story-only and carries no `GameplayButtons` bit — keep that property | **Implemented (preserve)** |
| C-16 | Separate HUD icon with Ready / 5 s / 45 s states + thaw warning; **never** on the rewind counter | `StoryHUD.RefreshRewindCooldownPip()` attaches the cooldown pip **directly to the rewind counter** | **Retired-code-to-remove + new surface** |
| C-17 | Save remaining cooldown; activation stores conservative 45 s; explicit Save/exit ends the freeze; reload never resumes | No field, no rule | **Missing** |
| C-18 | Fresh entry / Restart Level start Ready; checkpoint activation / death rewind / Snap / reload never refresh it | No rule | **Missing** |
| C-19 | Level 0 drill: 5 s escape, invulnerable enemies, safe destination, ready-on-retry, no charge cost, teaches cooldown | `TutorialCalibrationStep.UseManualRewind` teaches the **scrub** with `ScriptedFreeRewind` | **Retired-code-to-rewrite** |
| C-20 | Retire the Stasis Echo; reauthor required Echo gates with resource-free mechanisms | `StasisEcho` + plate weight + searchlight occlusion exist. **Good news: no authored level requires it.** Level 6's scale puzzle is solved by the *player* standing on a pan (`Level06Controller` docs L322–334); Levels 4/9 searchlights are timing/route puzzles with `SearchlightZone.Enabled` kill switches. **No re-authoring is needed — deletion only.** | **Retired-code-to-remove** |
| C-21 | Time Freeze never a mandatory progression gate | Nothing gates on it (it doesn't exist) | **Compliant by construction — add a guard test** |
| C-22 | `PathMovingPlatform` freezes in place during Time Freeze, still reverses during death rewind | Only the reversal path exists | **Partial** |
| C-23 | `CharacterState` gains **`Thrown`** (F23, append-only after `Grabbing`) | `PlayerController.CharacterState` ends at `Grabbing` — **`Thrown` is absent**; `Dashing` correctly reserved | **Missing** |
| C-24 | `MatchMode` = `Stock = 0`, `TimeLimit = 1`, **2 reserved** (legacy Hybrid decodes to timed Stock, not selectable); `TimeLimit = 0` means Off for Stock only (F21) | `GameManager.MatchMode` still has a live `Hybrid` member | **Partial (Fighter-rules workstream overlap)** |

---

## 4. Concrete change list

### 4.1 New code

**`scripts/Environment/TimeFreezeController.cs`** (new; `FTT.Environment`) — the ability owner. Suggested surface, mirroring `ChronalRewindManager`'s testability conventions:

```
public const float FreezeSeconds = 5f;          // 300 ticks
public const int   FreezeFrames  = 300;
public const float CooldownSeconds = 45f;       // all difficulties
public const string ControllerGroup = "time_freeze_controller";   // HUD resolves through this
public bool  IsFrozen { get; }
public float FreezeSecondsRemaining { get; }
public float CooldownRemaining { get; }
public bool  IsReady { get; }                   // cooldown 0 && !IsFrozen
public bool  DrillFreeFreeze { get; set; }      // Level 0 drill: ready on every retry
internal bool TryActivate();                    // eligibility gate, test seam
internal void EndFreeze(bool early);            // arms the cooldown
public static bool CanActivate(CharacterState state, bool isFrozen, float cooldownRemaining, bool alive);  // pure rule
```

* Tick in `_PhysicsProcess` on `ProcessMode.Inherit` so `SceneTree.Paused` freezes both the duration and cooldown clocks for free (the same trick `ChronalRewindManager` relies on). **Do not** set `ProcessMode.Always`.
* Register in `StorySceneBootstrapper.Attach(...)` under the same `includeRewind` flag (level-only, never the hub) — `scripts/Environment/StorySceneBootstrapper.cs` L66–72, plus a `TimeFreezeController` field on `StoryServices` (L13–16).
* Eligibility gate mirrors the existing `CanBeginManualRewind()` state list **minus** the charge/history checks, **plus** an alive check, **plus** exclusion during `ChronalRewindManager.IsRewinding` / `IsCollapseBeatActive` / active dialogue. Suppression must NOT be consulted.

**A world-freeze service.** Two viable shapes; recommend (b):

(a) Extend `IStoryRewindSimulation` to ~25 more environment classes. Honest but wide and error-prone.
(b) **New `IStoryTimeFreezable { void SetTimeFrozen(bool frozen); }` in `scripts/Environment/StoryRewindPolicy.cs`, plus a `TimeFreezeScope` sweep** over a small set of groups, with a **`ProcessMode.Disabled` fallback** applied to a room's `Actors`/`Hazards`/`Encounter` container subtrees for everything that has no explicit hook. `ProcessMode` writes on collision-bearing subtrees are illegal during a physics flush — they **must** go through `PhysicsSafeSetters.SetProcessModeSafe` (see AGENTS.md "Physics in/out signal callbacks"). Explicitly implement `IStoryTimeFreezable` (not `ProcessMode`) for anything that must keep a collision shape live but stop simulating: `PathMovingPlatform` (freeze in place), `RisingWaterZone`, `SearchlightZone`, `StageHazard`, `StoryCyclicHazard`, `CrumblingPlatform`, `TrapdoorPlatform`, `RotatingGear`/`RotatingPlatform`/`PendulumAnchor`, `ForcefieldBarrier`, `ShieldGeneratorTower`, `EscapeSequenceController`, `ChronalRiftZone`, `GravityFieldZone`, `MovementDampenerZone`.
* Reuse `FrozenSimulationGroups` membership for enemies/constructs/extractors, but with a **Time-Freeze-specific** variant that (i) **omits** `chronal_extractor` (Integrity must keep draining — C-14) and (ii) **does not** call `ClearEnemyProjectiles()`; instead freeze `enemy_projectile` and player-projectile pool members in place.
* **Enemy invulnerability:** add a freeze check at the shared hit-resolution chokepoint rather than per-enemy — `Hitbox.OnAreaEntered` / `DamageCalculator` / `EnemyController` intake. A single `TimeFreezeController.IsFrozen` gate that discards player-sourced hits (no damage, no stagger, no Rally echo, no meter) is the cheapest correct implementation and satisfies "attacks are discarded rather than queued".
* **Player input mask:** `PlayerController` gains a `TimeFrozen` flag that (i) allows Move/Jump/Down/Roll, (ii) rejects `BasicAttack`/`Special1`/`Special2`/`Ultimate`/`MovementAbility`/Grab/Echo Step **without buffering**, (iii) suspends block-charge regen, ability cooldown ticks and status timers. Do **not** reuse `SetRewindSuspended` — that suspends the player.

**Room scoping (C-9).** Minimum viable: on activation, freeze the current `RoomTransitionTrigger`'s bounds plus all others (i.e. freeze everything level-wide), and on a room crossing during an active freeze, freeze the destination's encounter root **before** `RoomTransitionTrigger.ActivateRoom()` runs its `onEntered` handler. Level-wide freezing trivially satisfies "suspend adjacent rooms"; the design's wording is a floor, not a cap.

### 4.2 Edits to existing files

| File | Action |
|---|---|
| `project.godot` | Rename the InputMap action `gameplay_rewind` → `gameplay_time_freeze`, **keeping both events unchanged** (physical keycode 82 = R; joypad button 4 = Back/Select). |
| `scripts/Core/InputManager.cs` | Replace `Actions.Rewind` with `Actions.TimeFreeze = "gameplay_time_freeze"`; update `RemappableActions` (L47) and `ActionLabelKey` (L68) → `controls_action_time_freeze`. Keep the "no `GameplayButtons` bit" comment (Story-only guarantee). |
| `scripts/Core/InputBindings.cs` / `SaveManager` global payload | Add a **v4→v5 global migration** that renames the `gameplay_rewind` key to `gameplay_time_freeze` inside `InputBindingSet.Actions` (C-2). Per `COMFORT_SETTINGS.md` 128, do **not** assume v4 already covers it — inspect the actual stored format. |
| `scripts/Environment/ChronalRewindManager.cs` | **Delete** the entire "Manual rewind" region: `ManualHoldSeconds`, `ScrubFramesPerTick`, `StasisEchoSeconds`, `ManualRewindCooldownSeconds`, `_manualCooldownRemaining`, `ManualRewindCooldownRemaining`, `IsManualRewindOffCooldown`, `StartManualRewindCooldown`, `IsManualRewindFree`, `TrackManualRewindHold`, `CanBeginManualRewind`, `BeginScrub`, `AdvanceScrub`, `ScrubPreviewPosition`, `CancelScrub`, `CommitScrub`, `_manualHoldSeconds`, `_isScrubbing`, `_scrubDepthFrames`, `_scrubOrigin`, `_manualCommit`, `_spawnEchoOnComplete`, `_pendingEchoOrigin`, `IsScrubbing`, `ScrubDepthFrames`, and the `_spawnEchoOnComplete` branch in `CompleteRewind` (L462–470). Simplify `CompleteRewind`'s HP branch (the `_manualCommit` "restores no HP" case disappears). Simplify `_PhysicsProcess` (L46–78) and the `_manualCooldownRemaining` tick. **Keep** `ScriptedFreeRewind`? — its only remaining purpose is the tutorial's cooldown exemption, which now belongs to Time Freeze; delete it from the rewind manager and put `DrillFreeFreeze` on the new controller. **Keep** the death-rewind path, collapse beat, checkpoint refresh, and the world-freeze helpers (refactor the freeze helpers out into a shared service both mechanics call). |
| `scripts/Environment/StasisEcho.cs` | **Delete the file** (and its `.uid`). |
| `scripts/Environment/PressurePlate.cs` | Remove the `StasisEcho => PlayerWeight` switch arm (L53–55) and its comment. |
| `scripts/Environment/SearchlightZone.cs` | Remove the Echo occlusion path (L93, 112, 125). **Decide explicitly** whether beams keep *any* occlusion rule; simplest conformant answer is to delete `IsBeamOccludedForPlayer`'s Echo branch and have it return false, since no authored level requires occlusion to progress (Level 4 corridors and Level 9 beams both have kill-switch/timing routes). Confirm with the Level 4/9 owner before deleting the public method. |
| `scripts/Environment/IInteractable.cs` | `IRewindScrubbable` survives **only** for the death rewind's platform reversal. Delete `CancelRewindScrub` (a scrub can no longer be cancelled — there is no scrub). Consider collapsing `Begin/Apply/End` into the death-rewind path directly. |
| `scripts/Environment/PathMovingPlatform.cs` | Drop `CancelRewindScrub`; add `IStoryTimeFreezable` (freeze in place, resume from the same path index, no catch-up). |
| `scripts/UI/StoryHUD.cs` | **Delete** `_rewindCooldownPip` and `RefreshRewindCooldownPip()` (L56–58, 337, 341–370) and the `_rewindManager` group lookup. **Add** a separate `TimeFreezeIndicator` (32×32 icon + label) bound to `TimeFreezeController` via its own group, with Ready / countdown / cooldown states and a thaw warning at ~1 s remaining. Keep the rewind counter's own behaviour (`hud_rewinds`, red pulse at 1, empty hourglass at 0 with no alarm). |
| `scripts/Environment/TutorialCalibration.cs` | Rename `TutorialCalibrationStep.UseManualRewind` → `UseTimeFreeze`; rename `RegisterManualRewindComplete` → `RegisterTimeFreezeComplete`, `SkipManualRewindLesson` → `SkipTimeFreezeLesson`. Keep `UseRewind` (scripted death-rewind demo) ahead of it. |
| `scripts/Environment/Level00Controller.cs` | Keep `ProcessRewindDemo()` and the refund. Replace the `ScriptedFreeRewind` handoff (L251, 258) with the Time Freeze drill: author an **invulnerable-enemy + safe-destination** drill room, set `TimeFreezeController.DrillFreeFreeze = true` for the duration (ready on every retry), advance on a completed freeze rather than on `OnRewindTriggered`, and clear the flag on completion. New objective key `tutorial_step_time_freeze`. |
| `scripts/Core/StoryManager.cs` | Add the persisted per-attempt `TimeFreezeCooldownRemaining` plumbing alongside the existing attempt registries (`WriteAttemptStateToSave` / `BeginLevelRun` restore path, L243–272 / 500–535). **Fresh entry and Restart Level → 0 (Ready); resume → restore the saved value.** Checkpoint activation, death rewind, and Collapse/Snap must leave it untouched. |
| `scripts/Core/SaveManager.cs` | `StorySaveData`: add `public float TimeFreezeCooldownSeconds;` (additive → schema **v6**, or land it inside the V7.x batch's single version bump). Follow the v5 precedent: field-initializer defaults, no migration step needed for older payloads. Apply the F03/`STORY_PERSISTENCE.md` 85 rule: while a freeze is live, **write 45** (never the live remainder); on explicit Save/exit, **end the freeze and write 45**. |
| `localization/en.csv` | **Add:** `controls_action_time_freeze`, `hud_time_freeze_ready`, `hud_time_freeze_active` (`{0}`), `hud_time_freeze_cooldown` (`{0}`), `hud_time_freeze_thaw`, `tutorial_step_time_freeze`. **Retire/replace:** `hud_rewind_cooldown` (1146), `tutorial_step_manual_rewind` (606), `controls_action_rewind` (1084). Retired keys go on the `UnusedTranslationKeyTests` recorded-orphan roster or get deleted outright. **Re-run `--headless --import` and commit the regenerated `localization/en.en.translation`** (CLAUDE.md's explicit rule — `ScriptTranslationKeyTests`/`SceneVisibleTextTests`/`CampaignLocalizationTests` all read the compiled resource). Separately, `difficulty_easy_description` (330) still says "full HP restoration" and has been wrong since V7.2 (70%). |
| `scripts/Characters/PlayerController.cs` | Append `Thrown` after `Grabbing` (F23, C-23) — append-only, never renumber, never reuse `Dashing`. Add the `TimeFrozen` input mask described above. |
| `scripts/Core/GameManager.cs` | (F21, shared) mark `MatchMode.Hybrid` reserved/not-selectable and pin `Stock = 0` / `TimeLimit = 1`; allow `TimeLimit = 0f` as "Off" for Stock. **Confirm ownership with the Fighter-match workstream before touching.** |
| `AGENTS.md` | Rewrite the Story-progression bullet ("Since V7.2 the manual rewind is a real verb on its own `gameplay_rewind` action (scrubbed depth, Stasis Echo)…" and the V7.3 12 s cooldown sentence), the autoload/input list, the rewind-buffer bullet, and the test baseline. |

### 4.3 Tests

**Delete outright**
* `tests/unit/ManualRewindScrubTests.cs` — all 4 cases (`TheBufferPeeksAtScrubDepth…`, `ThePathPlatformScrubsAlongItsOwnRecordedPath…`, `ACancelledScrubSnapsThePlatformBackToThePresent`, `TheManualRewindCooldownArmsOnCommitAndScriptedRewindsAreExempt`). **Note:** case 2 also pins the death-rewind platform reversal — salvage that assertion into `ChronalRewindTests` before deleting. **−4**
* `tests/unit/StasisEchoPhysicsTests.cs` — all 3 cases (plate weight, searchlight occlusion, lifetime expiry). **−3**
* `tests/unit/CollisionLayerTests.cs::TheStasisEchoBodyLayerReachesTheAuthoredPressurePlateMask`. **−1**

**Rewrite in place (no count change)**
* `TutorialCalibrationTests::TheDesignedStepOrderRunsAttackRallyBlockSpecialUltimateRewindManual` → `…RewindTimeFreeze`.
* `TutorialCalibrationTests::TheRewindStepsCompleteInSequenceWithNeverStrandFallbacks` → death-rewind demo then Time Freeze drill.
* `ChronalRewindTests::DifficultyRulesMatchDesign` — keep 5/3/1 and 70/50/30; add the "Time Freeze is 5 s / 45 s on every difficulty" pin.

**New suite `tests/unit/TimeFreezeTests.cs`** (one `[TestSuite]` per file — GdUnit4 silently drops a second suite sharing a file):
1. Constants pin: 5 s / 300 frames, 45 s, identical across all three difficulties.
2. Cooldown arms at **thaw**, not activation; an early end arms it too.
3. Cooldown ticks only in live play; a paused tree and an active freeze both stop it.
4. Checkpoint activation / death rewind / Collapse / reload never refresh the cooldown.
5. Fresh level entry and Restart Level start Ready; a mid-level resume restores the saved value.
6. Activation costs **no** rewind charge and is legal at `RemainingRewinds == 0`.
7. Eligibility matrix: refused in `Stunned`/`Dazed`/`Dead`/`Respawning`/`Attacking`/`UsingSpecial`/`UsingUltimate`/`UsingMovementAbility`/`Grabbing`; **allowed under Suppression**.
8. During freeze: Move/Jump/Roll accepted; attack/grab/special/ultimate/Echo Step/movement-ability **discarded, not queued** (verify no burst on thaw).
9. During freeze: enemies and bosses take zero damage from a live player projectile/zone/DoT; no Rally echo, no meter, no stagger.
10. During freeze: no pickup collection, no checkpoint activation, no puzzle condition change.
11. Suspended timers: block-charge regen, ability cooldowns, status durations all frozen; effect clock and movement continue.
12. Thaw preserves enemy position/velocity/attack phase with **no catch-up tick** and no projectile clear.
13. `PathMovingPlatform` freezes in place under Time Freeze and still reverses under death rewind.
14. **Integrity keeps draining** across the whole 5 s (the C-14 conflict, pinned).
15. Save behaviour: activation stores 45 s; an explicit Save during a freeze ends it and stores 45 s; reload never resumes.
16. Story-only guard: no Fighter path can reach the controller; `gameplay_time_freeze` carries no `GameplayButtons` bit.
17. Guard test: no authored level gates progression on Time Freeze (sweep `scripts/Environment/Level*Controller.cs` for controller references in puzzle/gate paths).
18. `SceneTree.Paused` is never written by the controller (CLAUDE.md failure signature 4).

**Also add**
* `SaveEnvelopeTests`: v5-loads-with-v6-defaults + v6 round trip for `TimeFreezeCooldownSeconds`.
* `StoryHUD`/HUD suite: the Time Freeze indicator is a **separate** node from the rewind counter and never renders a cooldown on the rewind counter.
* `PlayerController`/state test: `CharacterState.Thrown` exists, is appended after `Grabbing`, and `Dashing` keeps ordinal 2.

**Baseline impact:** −8 removed, roughly **+22 to +26** added → the 1638 baseline moves to about **1652–1656**. Reconcile the exact delta at merge; CLAUDE.md is explicit that a plausible-looking total is worthless without the exact expected number.

---

## 5. Rest of my §4 range — deltas with code impact

| Delta | Design | Repo | Verdict / size |
|---|---|---|---|
| `CharacterState` + `Thrown` (F23) | Append `Grabbing, Thrown` without renumbering; grab phase data (counterpart actor ID, phase timer, throw direction, release/damage-applied flags, 20-frame throw immunity) is rollback state alongside the enum | `Grabbing` present, **`Thrown` absent** | **Missing, S** (combat workstream overlap) |
| State transition table rewrite | ~20 rows restated as eligibility rules (grounded stance with usable charge + no shatter lockout; ledge climb via Jump at 0.9×; `Stunned`→`Idle`/`Airborne` by grounding, "never force an airborne fighter to Idle"; Echo Step phase from attack recovery; Nexus Ultimate authorization) | Mostly already the shipped V7.1–V7.4 behaviour; the doc is catching up to code | **Mostly implemented — doc-side. Verify the `Stunned`→grounded-selection and `Dazed`→grounded-selection rows.** S |
| `MatchMode` / `TimeLimit = 0` (F21) | `Stock = 0`, `TimeLimit = 1`, 2 reserved, Hybrid not selectable; `TimeLimit = 0` = Off for Stock only | `Hybrid` still a live member | **Partial, S — Fighter-match workstream owns it** |
| `MatchSettings.GetDefault()` | Stock/3/480/items on Medium/hazards on Medium | **Exact match** (`GameManager.cs` L55–67) | Implemented |
| Scene list naming | `HubWorld` = **Warden** Time-Ship; Level 14 = *The Unbound Bastion*; Level 15 = *The Meridian Founding* (**legacy scene IDs retained**); MainMenu gains **Calibration Drills** (F18) | Repo scene paths/IDs unchanged (correct); display names and hub naming are `en.csv` + narrative work | **Not mine — narrative/UI workstream.** Confirm they keep scene IDs. |
| Loading screen C01a | Reduced Temporal Effects substitutes a clean portal + smooth fade, keeps the 2 s minimum | `GameManager.LoadScene` picks Story portal / Fighter VS / plain with the 2 s minimum; no comfort preset | **Missing — presentation/comfort workstream** |
| Movement defaults (run 14 / decel 12 / air 4 / fast-fall 16 / roll 4-12-10 with 8 invuln / coyote 6 / jump buffer 6 / guard-break `Vector2(2.0, 1.0)` + 1 s daze) | Unchanged from V7.3 | Matches `UniversalMovementRules` per AGENTS.md | **No delta** |
| Pooling system (`IPoolable`, `ScenePoolConfig`, Grow/RecycleOldest/Reject, the six warm-up rows) | Unchanged | Implemented | **No delta** |
| Persistent objects (no body collision, impulse-free, bottom-anchored in the sim, basic swings damage constructs, overhead HP bars, deploy-limit oldest-eviction, 1 block charge, friendly-fire immunity, owner tint) | Unchanged; construct cadences (coil 5 HP/2.0 s, turret 6 HP/2.0 s ×4 bolts, nest 6 HP/1.0 s, snare 8 HP/1.0 s) | Matches the 2026-08-11 rebalance **except** the doc now says the serpent nest is **6 HP per 1.0 s** while AGENTS.md records the shipped nest at **10 HP per 4 s** | **Verify the four `.tres` against the doc — possible confirmed deviation. S** |
| Ledge rules (trump, 3 regrabs/airtime, 5 s hang, Jump climb 0.9×, Down + 30-frame lockout, rising capture) | Unchanged from V7.3 | Implemented (`FighterLedgeRules` + Story `LedgeGrabPoint`) | **No delta** |
| Drop-through (double-tap Down Story vs Down+Jump sim) | Divergence **still recorded as deliberate** (`FighterRuntimeComponent` full at 128 bytes) | Matches | **No delta — stays open** |
| Tesla fence Static Charge (F07) | Now a **Static Charge interrupt** *plus* a separate **1.5 s Conductive mark** | Repo applies Static Charge only | **Partial — character-kit workstream** |

---

## 6. Sizing and shared-file collisions

| Workstream chunk | Size | Notes |
|---|---|---|
| Retire manual rewind + Stasis Echo (delete code, plate/searchlight arms, tests, keys) | **S–M** | Almost pure deletion; **no authored level depends on the Echo**, so no puzzle re-authoring |
| Input action rename + binding migration | **S** | Default events are already correct; the risk is the global-payload key rename |
| `TimeFreezeController` + eligibility + input mask + HUD indicator | **M** | Straightforward once the freeze service exists |
| World-freeze service (hazards, platforms, mechanisms, both-side projectiles, enemy invulnerability, no-catch-up thaw, room scoping) | **L** | **The dominant cost.** ~25 environment classes, plus the hit-resolution gate, plus `PhysicsSafeSetters` discipline on every `ProcessMode` write |
| Cooldown persistence (StoryManager attempt state + save schema + F10 rules) | **M** | Touches the same attempt-state plumbing as the V7.3 batch |
| Level 0 drill re-authoring (invulnerable enemies, safe destination, ready-on-retry) | **M** | Needs new drill geometry in `Level00Controller`, plus dialogue/objective copy |
| Tests | **M** | ~24 new cases, 8 deletions, 3 rewrites |
| **Total** | **L** | Plausibly one full workstream, or two agents split as "retirement + input + HUD + persistence" / "world freeze + tutorial drill" |

**Shared files other workstreams will also touch — coordinate merges:**
`localization/en.csv` + `localization/en.en.translation` (every workstream), `scripts/UI/StoryHUD.cs` (Integrity clock, Rally band, status slots, ability lock states all land in the same node tree), `scripts/Core/StoryManager.cs` + `scripts/Core/SaveManager.cs` (Integrity timer, anchors, Legacy unlocks, N05 ending records all add fields to the same payload — **agree ONE schema-version bump**), `scripts/Core/InputManager.cs` + `project.godot` InputMap (C01c adds direct Ultimate/Grab/Echo Step binds in the same table), `scripts/Characters/PlayerController.cs` (`Thrown`, Suppression, environmental-damage chokepoint), `scripts/Environment/ChronalRewindManager.cs` (T01a hero cleanup + landing validation land here too — **highest collision risk**), `AGENTS.md`, and the test baseline number.

---

## 7. Risks and open questions for the user

1. **C-14 Integrity-vs-freeze conflict.** `chronal_extractor` is in `FrozenSimulationGroups` today, so extractor drain pauses during a death rewind (correct) — but a shared freeze service would wrongly pause it during Time Freeze. The two mechanics need **different** freeze sets. This must be settled with the V7.6 Integrity-timer workstream, since that rework replaces siphon-share drain with a wall-clock level timer entirely.
2. **`SearchlightZone` occlusion.** Deleting the Echo removes the only thing that ever occluded a beam. Confirm with the Level 4/9 owner whether the public `IsBeamOccludedForPlayer` API should survive (returning false) or be deleted.
3. **Serpent-nest cadence.** Master says 6 HP / 1.0 s; AGENTS.md records the shipped `.tres` at 10 HP / 4.0 s. Per `DESIGN_BUILD_DEVIATIONS.md`, that is a candidate **confirmed deviation**, not a silent normalization.
4. **`ScriptedFreeRewind`** currently serves two masters (tutorial charge exemption + cooldown exemption). Once the manual verb is gone, the scripted **death** rewind still needs the `RefundRewind()` path but no cooldown exemption. Confirm the tutorial's scripted death demo continues to refund rather than being made free-by-flag.
5. **Enemy invulnerability implementation point.** Gating at `Hitbox.OnAreaEntered` is the single chokepoint, but that handler runs inside a physics flush behind `PhysicsCallbackGuard`. Verify the freeze read is side-effect-free there.
6. `DESIGN_BUILD_DEVIATIONS.md` **VERIFY-01** should be closed against this dossier: the docs workspace had no build evidence, and this recon supplies it — the current build implements the **retired** V7.2/V7.3 manual-rewind design in full, so VERIFY-01 becomes a confirmed open deviation.
