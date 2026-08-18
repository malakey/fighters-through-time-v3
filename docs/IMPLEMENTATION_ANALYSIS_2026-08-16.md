# Implementation analysis — 2026-08-16

A deep review of the *implementation* of Fighters Through Time against `design-godot.md`, the accepted ADRs, the package plans, `docs/GAMEPLAY_FEEL_2026-08-10_PLAN.md`, and `docs/IMPLEMENTATION_AUDIT_2026-08-08.md`. Sibling document: `docs/DESIGN_ANALYSIS_2026-08-16.md` (design gaps and genre comparison). This one answers: **does the code do what the design/decisions say, what is missing, what is broken, and what should be done about it.**

Method: five parallel domain reviews (movement/basic combat in both modes; Story campaign/hub/rewind/saves; Fighter match flow/stages/CPU/networking; character kits/enemies/bosses; UI/audio/art/localization/core services), each citing `file:line` from the current tree, followed by direct re-reads of the highest-impact claims (the omnidirectional sim melee, the unreachable Story double-tap drop-through, and the boss `StaticCharge` divergence were all re-verified by hand). Everything already recorded in the deviation logs and `AGENTS.md` known gaps was treated as known and is referenced, not repeated, except where it is contradicted.

Baseline at review time: `AGENTS.md` records 1480 passing tests (2026-08-15). This review did not run the suite; it read code and tests.

---

## 0. Executive summary

The architecture is in better shape than most projects at this stage: the deterministic boundary is clean (no float/unseeded random in the sim), the save envelope is correct, pooling and physics-callback hygiene are disciplined, every screen is an authored themed scene, and localization is gated both ways. The 2026-08-10 audit fix round closed the Critical and most Highs.

What this review adds — things not currently tracked anywhere:

**Confirmed bugs (new):**
1. **Fighter-mode melee has no facing check** — every basic swing, construct swing and special hits *behind* the attacker (`FighterSimulationSystems.cs:1294-1295`, `:1253`, `:1418`; `FacingRight` is never read in `FighterCombatSystem`). §2.12 "facing follows movement" is cosmetic in the sim.
2. **Story double-tap drop-through is unreachable from Idle/Running/Crouching** — the Down-held branch transitions to `Crouching` before `CheckDropThrough()` runs (`PlayerController.cs:739-744`, `:774-777`), and `Crouching` only sees the edge frame. You cannot drop through a platform by standing on it and double-tapping Down.
3. **Story can block while airborne** (`CheckBlockInput` `:1880-1886` has no `IsOnFloor()` gate; reachable via aerial-swing recovery cancel), contradicting the grounded-stance contract in `AGENTS.md`.
4. **Holding Block at 0 charges is a self-inflicted stun in both modes** — neither `CheckBlockInput` (Story) nor `IsBlockStance` (`FighterSimulationSystems.cs:1031-1041`) checks `charges > 0`; the stance roots you, suppresses attacks, absorbs nothing.
5. **Rewind fallback can land at world origin** — `LevelManager._lastCheckpointPosition` defaults `Vector2.Zero`; a death before checkpoint 0 with no grounded frame in the buffer teleports the player to (0,0) (`ChronalRewindManager.cs:237-240`, `LevelManager.cs:12,47`).
6. **Room transitions are one-way** — `ActivateOnce = true` and never overridden (`StoryLevelControllerBase.cs:680-704`); walking back into a cleared room leaves the camera confined to the next room.
7. **Boss `StaticCharge` is a 35 % slow, not a daze** (`BossController.cs:555-557`) — the design and both other status implementations daze. Tesla's synergy reads differently against every boss.
8. **Per-ability boss cooldowns are dead data** — `EnemyAbilityData.CooldownSeconds` is authored on all 76 kits and read by nothing (`BossController.SelectAbilityIndex` filters only phase and range); `SummonMinions` has no live-minion cap.
9. **Mozart's Sonata Drift is broken in the sim** — `MovementSpeed = 0.0` in `mozart/movement.tres` falls through to a default 8 → an undirected upward pop (`FighterEntitySystems.cs:726,739-741`).
10. **`FindConflictingAction` ignores its `action` parameter** (`InputBindings.cs:174-188`) — rebinding a key to the key it already has reports a self-conflict; conflicts against `ui_*` actions and the ultimate chord are invisible.
11. **`OnCooldownComplete` carries no player index** (`EventBus.cs:280-281`) and `StoryHUD` consumes it unfiltered — the one cross-player leak in a 1v1 codebase.
12. **`PoolManager.RecycleOldest` degrades to `Grow` inside a physics callback** (`PoolManager.cs:161-166` + the deferred `Release` at `:210-213`) — the exact path projectile detonations take.
13. **`TestArenaHUD` F3 debug overlay is live in every build** on all ten production Fighter stages (`TestArenaHUD.cs:30`).
14. **No `export_presets.cfg` exists**; `project.godot` `config/features` says `Forward Plus` while rendering is `gl_compatibility`.

**Cross-mode divergences not previously recorded:** the sim uses one flat 2.0×1.6 hitbox for all three basics (Story authors 72×60/84×72/108×84 px per design), a fixed 45° launch on every hit (Story authors per-hit vertical), a 30-frame drop-through window (Story/design 15), a 0.9× ledge-climb impulse (Story 0.833×), sim-only "walk off a platform end auto-grabs", sim aerial specials with no 50 %-gravity window and no airborne-ultimate zero-gravity, sim persistent constructs with `±10` hardcoded wall clamps on stages authored at `±9`, sim block-break knockback *replaces* vs Story *adds*, meter accrual float vs int.

**Biggest incomplete areas (beyond the known Package 7/9/10 scope):** hub gating and NPC content, puzzles (one level uses `PuzzleManager`), Fighter settings persistence, cutscene surface (zero scaffolding), input glyphs, playtime tracking (persisted fields never written), the netcode's handshake/redundancy/resync/rift management (none exist — the "foundation" is a simulation, not yet a protocol), Story-side movement test coverage (none), and a `HubWorldController` test suite (none).

**Documentation drift** is material: `AGENTS.md` says in one place that all 27 perks are wired and in another that only Einstein's are (all 27 *are* wired); `docs/IMPLEMENTATION_STATUS.md` still says 300-frame/4× rewind and "bespoke perks remain"; root `IMPLEMENTATION_STATUS.md`'s "Top Priority Gaps" list is two packages stale; `AGENTS.md` claims every production screen is an authored `.tscn` while `MatchResults` and `LocalFighterPause` build their UI in code.

---

## 1. Confirmed bugs and defects (severity-ranked)

Severity: **H** = player-visible wrong behaviour in the core loop; **M** = wrong in a reachable but narrower path, or a correctness hazard; **L** = smell / latent.

### 1.1 Combat & movement

| Sev | Where | Defect | Fix shape |
|---|---|---|---|
| H | `FighterSimulationSystems.cs:1294-1295, :1253, :1418` | Sim basic swings, construct swings and special/ultimate intents gate only on `|Δx| ≤ AttackRange`; `FacingRight` is never read in `FighterCombatSystem`. Every Fighter attack hits behind. | Add `(target.x − attacker.x) * facingSign ≥ 0` (or a small back-margin) to the three sites; pin with a test in `FighterBasicStringParityTests`. |
| H | `PlayerController.cs:739-744, :774-777, :866-873, :1904-1913` | Story double-tap drop-through unreachable from Idle/Running/Crouching (Down-held branch returns before `CheckDropThrough`; Crouching only sees the edge). Only Blocking/Attacking arm the tap timer. | Call `CheckDropThrough()` *before* the crouch transition in Idle/Running and on entry to Crouching; add a Story input-path test (the existing test covers only the pure predicate). |
| H | `PlayerController.cs:1880-1886, :1095, :459` | Story block stance reachable airborne via `TryRecoveryCancel:1056`; `ProcessBlocking` never re-checks ground; `OnHurtboxHit` absorbs on state alone. | Gate `CheckBlockInput` on `IsOnFloor()`; drop out of Blocking when airborne. |
| H | `PlayerController.CheckBlockInput`, `FighterSimulationSystems.cs:1031-1041`, `FighterEntitySystems.cs:296-297` | Block stance enterable at 0 charges: roots, suppresses attacks/abilities, absorbs nothing. | Require `charges > 0` to enter/hold the stance (or implement the design's 5 s shatter lockout, which subsumes it). |
| M | `BossController.cs:529-534` | `ApplyKnockback` uses a hardcoded `2f` weight, `Velocity +=`, and skips the §2.5 missing-HP scale — the only combatant that does. Recorded as "open" but it is live math inconsistency. | Route through `DamageCalculator.CalculateKnockback(…, victimHP)` like `EnemyController`. |
| M | `FighterLedgeRules.cs:917-922, :891` | Jump-climb sets no regrab lockout and Grab refills jumps → infinite climb-regrab stall. `FighterStageGeometry.TryFindLedge:374-384` picks first index-order match with no side discrimination. | Apply the 30-frame lockout on climb too; cap regrabs; prefer nearest edge. |
| M | `FighterSimulationSystems.cs:1480` | Generic ultimate path zeroes `Influence` before `ApplyFighterHit` can refuse (Aegis/invuln). Vestigial since `TryCharacterUltimate` covers all nine, but live. | Consume meter after the hit resolves, or delete the generic path. |
| M | `PlayerController.cs:1920` | `TryDropThrough` uses `GetLastSlideCollision()`, which may be a wall — drop-through pressed while wall-sliding no-ops. | Use the floor collision. |
| M | `PlayerController.cs:1601, :1588` | `RemainingJumps` can go negative (grounded jump permitted at 0, decrement unconditional). | Clamp. |
| L | `PlayerController.cs:201, :1541-1546`, `CharacterData.cs:34` | Dead: `DirectionReversalPenalty` (design `:689`) never applied; `ApplyFriction` never called so `GroundFriction` on all nine `.tres` is consumed by nothing; `PlayerController.CurrentBlockCharges` never synced from `BlockSystem`. | Wire or delete; add a "every `CharacterData` field is read somewhere" content test. |
| L | `PlayerController.cs:1525, :1599, :1162, :644`; `FighterSimulationDriver.cs:16` | Four different pixels-per-unit constants (60 / 54 / 45 / 62.5). The 45/54 ledge ratio silently encodes 0.833× where the sim uses `ClimbJumpMultiplier = 0.9`. | One `StoryUnits.PixelsPerUnit` constant; express the climb ratio via the shared rule. |
| L | `PlayerController.ProcessCrouching:858` | Near-dead state: no jump/roll/block/special/movement-ability checks. | Add the transitions or document crouch as attack-only. |

### 1.2 Story campaign, hub, rewind, saves

| Sev | Where | Defect | Fix shape |
|---|---|---|---|
| H | `ChronalRewindManager.cs:237-240`, `LevelManager.cs:12,47`, `:225` | Rewind fallback = `_lastCheckpointPosition`, default (0,0); buffer wiped on `CompleteRewind`. Death after a rewind and before checkpoint 0 → teleport to origin. | Register the spawn point as an implicit checkpoint on level boot. Test the empty-buffer/no-checkpoint case. |
| H | `StoryLevelControllerBase.cs:680-704, :268, :375-390` | Room transitions one-way (`ActivateOnce` default true); backtracking leaves the camera confined to the wrong room. Resume and rewind also disagree on overlap resolution (last vs first match). | Make transitions bidirectional (or re-confine on every room-rect containment change); unify the resolver. |
| M | `LevelManager.cs:70` | `CheckpointTrigger` resolves `LevelManager` via `GetTree().CurrentScene.GetNodeOrNull("LevelManager")` — silently drops the respawn position (while still raising the event) whenever the level is not `CurrentScene`. | Resolve via exported reference or the bootstrapper. |
| M | `ChronalExtractor.cs:53-58, :63` | Discharges on *every hit taken* (design: idle cycle) — ~8 melee hits ≈ 160 self-damage for 15 dust. Dust raised as bare event, no pickup (design's Large tier never renders). Same for boss dust (`BossEncounterController.cs:125`). | Idle warning/discharge cycle with a safe window; spawn a physical pickup. |
| M | `HubWorldController.cs:21, :540, :62, :553` | `_portalReady` initialised true, never written (no NPC gate); `_Input` interacts with no player-state check (open the grid mid-attack/dead); auto-deposit on entry makes the terminal deposit a no-op. `_player` dereferenced unguarded at `:558,561,594,598,603`. | Implement the gate; check `Idle/Running`; null-guard. |
| M | `ChronalRiftZone.cs:45,51` | Applies a 3600 s `TimeDilation` as "until exit" and clears any `TimeDilation` on exit — under newest-replaces-oldest it also cures Venom/Root/RadiantBurn. | Model "in rift" as a zone flag, not a status. |
| M | `ResonanceProgression.cs:262-263` | `PercentValue` ternary returns the same value in both branches; `StatModifierIsPercent` is authored everywhere and read nowhere. `BlockDurability` (×4 grids) and `Armor` (×1) have no case in the resolver switch (`:103-128`) → 5 purchasable dead nodes. | Implement flat vs percent; add the two stat types or re-author the nodes. |
| M | `SaveManager.cs:578` | `SetPuzzleCompleted` mutates in memory without saving; only a later checkpoint/level save persists it. Puzzle completion is per-slot, so a Timeline-Collapse full-level restart re-enters with gates open. | Save on set (or mark dirty); decide the restart semantics. |
| M | `StoryManager` / `SaveManager.cs:527` / pause | Three loss rules for undeposited dust: collapse 20 %, quit 50 %, crash 0 % (last checkpoint's wallet). | Design decision; then one chokepoint. |
| L | `StoryLevelControllerBase.cs:795` | `AttributeExtractorDust` closure reads `extractor.DustReward` after destruction. | Capture the value. |
| L | `StatusController.cs:56, :126-133`; `EnemyController.cs:733` | `StaticCharge` daze outlives the status when overwritten by a newer one (`OnRemove` never clears the stun); `RestoreState` re-raises `StatusEffectApplied` on every rewind restore. | Clear stun on remove; restore silently. |

### 1.3 Fighter mode, CPU, netcode

| Sev | Where | Defect | Fix shape |
|---|---|---|---|
| H | `FighterStageGeometry.cs:97-347`, `FighterSimulationSystems.cs:364-397` | Bottom blast zone unreachable on all ten production stages (audit H-11, open). `MatchEndsOnABottomBlastZoneFall` passes only on legacy flat geometry. | Design decision (see design report §1.1); then floor segments or delete the end condition. Add a production-geometry reachability test either way. |
| H | `FighterCpuController.cs:508-518, :136` | Block decision gates on `TargetPressedButtons` (1-frame edge) but decisions run every 3rd tick → Hard's 80 % block rate lands ~27 %. Tests pin tuning constants, not behaviour. | Latch "attack seen within reaction window"; add an observed-rate behavioural test. |
| M | `FighterCpuController.cs:660-663, :139-142, :731-734` | Easy CPU recovers competently (`RecoveryJumpPercent 60`, `RecoverySpecialTwoPercent 55`) contrary to its design row; scheduled decisions can deliver out of order / overwrite in the 128-slot ring; `NextReactionDelay` is modulo-biased. | Record or fix; use a min-heap or per-tick list; rejection-sample. |
| M | `RollbackProtocol.cs:200-207, :177-180, :160-166` | UDP `Poll` catches only `WouldBlock`/`MessageSize` — a Windows `ConnectionReset` throws out of `Advance`; `Send` unguarded; `_acceptFirstPeer` latches the first 47-byte datagram *before* the session-ID check (one spoofed packet locks out the real peer); two allocations per packet at 60 Hz. | Catch/log; move latch after session validation; pool buffers. |
| M | `RollbackProtocol.cs:312-324, :239, :386, :395` | No input delay setting; no frame-advantage/rift management (7-frame hard cap ⇒ any RTT ≳110 ms or one hitch permanently diverges); one tick per datagram with no redundancy (one lost packet = silent desync); `AcknowledgedSequence` written and never read; `StateHash == 0` conflated with "no hash". | See §3.2 — this is Package 7 scope, listed here because the "foundation" label overstates it. |
| M | `NetworkManager.cs:78-83`; `FighterSimulationDriver.cs:114,151`; `FighterStageController.cs:54` | `DesyncDetected` only `PushError`s; match seed is `GD.Randi()` locally with no handshake path; `FighterStageController` never passes a seed. `InputArrivedTooLate`/`RollbackBudgetExceeded` have no production subscriber. | Package 7. |
| M | `FighterEntitySystems.cs:751, :754` | Teleport movement abilities clamp to hardcoded `±10` / `y ≥ 0` while six stages author walls at `±9`; effective distance near a wall depends on system order. | Read stage geometry. |
| M | `FighterSimulationSystems.cs:289-299` | Chronal Haste's +20 % air-control half is not applied (speed buff only in the grounded path). No de-dup against an anchor already holding an orb; no `MaxCount` check. | Apply in air path; guard spawns. |
| M | `FighterUltimateRules.cs:242-257` | Wardenclyffe destroys entities inside a `frame.Filter<…>()` loop while holding `ref` components — fragile w.r.t. Klotho storage compaction. | Collect then destroy. |
| M | `FighterMatchSystem` (`FighterSimulationSystems.cs:1545, :1554-1556`) | Stock mode never decrements the timer (unbounded stalemate); Time mode compares KO count only and never falls back to HP (0–0 at 8:00 always draws). | Design decision; then unify tie-break. |
| L | `FighterSimulationDriver.cs:967-974` | KO cinematic ticks the sim with neutral inputs after `Complete`; safe only because every system early-outs on `MatchState != 1`. Silent dependency. | Assert/guard, or stop advancing. |
| L | `scripts/Combat/ChronalOrbItem.cs:4-12` vs `FighterEntitySystems.cs:1530-1547` | Two orb taxonomies (Story 5 effects, Fighter 4), bridged by index in `FighterProxyStyle.OrbColor`. | One vocabulary. |
| L | `MatchResults.cs:80-134`, `LocalFighterPause.cs:104-204` | UI built in code inside script-root `.tscn` shells, contradicting `AGENTS.md`'s "every production screen is an authored scene". | Author or amend the doc. |
| L | `FighterSimulationComponents.cs:125-140`; `FighterLoadoutFactory.cs:25-125` | `FighterLoadout` mixes readonly/mutable; float→FP64 conversion at construction is safe only if both peers load byte-identical resources — nothing verifies (no content hash in the handshake). | Content hash in Package 7 handshake. |

### 1.4 Kits, enemies, bosses

| Sev | Where | Defect | Fix shape |
|---|---|---|---|
| H | `BossController.SelectAbilityIndex:376-383`; `EnemyAbilityData.cs:47`; `EnemyAbilityExecutor.cs:290-299` | Per-ability `CooldownSeconds` never read; `SummonMinions` uncapped — a boss can chain `tribunal_levy` (16 s authored) on consecutive rolls. | Track last-used tick per ability; cap live minions. |
| H | `BossController.cs:555-557` | `StaticCharge` = 35 % slow on bosses; daze everywhere else. Third hand-rolled copy of the status machine (`StatusController`, `EnemyController.cs:710-737`, `BossController.cs:536-563`) and they disagree. | One shared status resolver consumed by all three. |
| H | `mozart/movement.tres` (`MovementSpeed = 0`), `FighterEntitySystems.cs:726,739-741` | Sonata Drift in the sim = undirected upward pop at default speed 8; no platform. | Author speed; implement the platform or a directed float. |
| M | `StatusController.cs:27` | `TimeDilationStrategy` sets `StatusAnimationMultiplier = 0.5f` unconditionally — Yorick's Lament (intensity 0.6) slows movement 30 % but animation 50 %. | Use intensity. |
| M | `EinsteinRelativityWarp.cs:47,16`; `FighterEntitySystems.cs:758` | Warp `OnActive` uses `PhaseTimer` not `UseAuthoredPhaseFrames()` (`ActiveFrames=6` in `.tres` is dead); float is hardcoded 1.0 s / 60 f; the design's 3 s cap unenforced. | Authored frames; decide the cap. |
| M | `lincoln/movement.tres` (`BaseDamage=5`) vs sim | Rail Charge deals no contact damage in the sim. Splitting Strike's airborne spike is Story-only. Emancipator is a travelling wave in Story, static zone in sim. | Recorded approximations; make the audit list them explicitly. |
| M | `CharacterFactory.GetRequiredHitboxes:263-276`, `MakeHitbox:278-301`; `BaseSpecial.GetOrCreateChildHitbox:276-307` | Ten of twelve factory hitboxes per character are never referenced (allocation waste); factory hitboxes skip `HitConfirmed` wiring and `GetOrCreateChildHitbox` returns early on existing nodes → Lincoln's Splitting Strike never emits `ImpactVFX`/haptic. | Delete orphans; wire in the factory or don't pre-create. |
| M | `BossController.ResolveRng:92-95`, `OnSpawn:643-674` | Seeded `_rng` survives pool reuse; a pinned `SelectionSeed` does not reproduce on second spawn. | Null in `OnSpawn`. |
| M | `EnemyController.cs:496-508` | Elite ability cycling implemented but every elite authors exactly one ability; four enemies have no `PrimaryAttack` and run the synthesized fallback (`:514-532`); `EnemyData.JumpForce` authored ×27, read by nothing. | Content; dead-field test. |
| L | `FighterUltimateRules` consts vs `.tres` vs Story | Ultimate lifetime/tick triplicated; `FighterLoadoutFactory` carries no ultimate timing fields. | Add to loadout. |
| L | `EnemyAbilityExecutor.Tick` | Phase enter runs in the tick that zeroed the previous counter → `TotalFrames` under-reports by ~3. `BossController.SelectAbilityIndex` allocates a `List<int>` per attack. | Off-by-one; pool. |

### 1.5 UI, core services, release

| Sev | Where | Defect | Fix shape |
|---|---|---|---|
| H | *(none)* | **No `export_presets.cfg`.** Nothing has been exported for any target. | Author presets for Windows/macOS/Linux; run one export in CI. |
| M | `project.godot:19` vs `:171` | `config/features` = `Forward Plus` while `rendering_method = gl_compatibility`. | Fix the features array. |
| M | `TestArenaHUD.cs:30, :61` | F3 debug overlay live in release on all ten stages. | `OS.IsDebugBuild()` gate like `MainMenu.ShowDeveloperLevelSelect`. |
| M | `InputBindings.cs:174-188, :246-253` | `FindConflictingAction` ignores its `action` param (self-conflict on same-key rebind); `CaptureEffective` scans only `RemappableActions` so `ui_accept`/`ui_cancel` and the LB+RB ultimate chord are invisible to conflict detection. | Fix param; include engine and chord actions in the scan. |
| M | `EventBus.cs:280-281, :370-371, :244-249` | `OnCooldownComplete` has no `PlayerIndex` (HUD consumes unfiltered); `OnBossPhaseChanged(int)` has no `BossID`; no `_ExitTree` and no protection from a freed subscriber (`ObjectDisposedException` kills the invocation list). | Add fields; guard invocation. |
| M | `PoolManager.cs:132, :161-166, :171-177, :210-213` | `Spawn(PackedScene)` auto-registers with hardcoded `warmUp=1, capacity=50, Grow`, bypassing `ScenePoolConfig`; `RecycleOldest` → `Grow` inside a physics callback (deferred `Release`); grown pools never trim. | Refuse unregistered spawns in release; make recycle callback-aware; add trim. |
| M | `GameManager.cs:186-209` vs `LoadingScreen.cs:141` | `GameManager` is `Pausable` while the `LoadingScreen` it polls is `Always` — a `LoadScene` under `SceneTree.Paused` hangs forever. Safe today only by convention. | `ProcessMode = Always` on the autoload. |
| M | `LocalizationManager.cs` | Parses `en.csv` at runtime into a second lookup path that **nothing calls**; would return stale values vs the compiled `.translation` if used. | Delete or reduce to a `TranslationServer` facade. |
| M | `SaveManager.cs:24, :75`; `SaveEnvelope.cs:311` | `PlayTimeSeconds` and `TotalPlayTime` are persisted and never written by any code; the design's slot row is specified to display playtime. | Track and write. |
| M | `MainMenu.cs:427-447` | Slot row lacks the portrait and playtime the design specifies. | Add. |
| L | `InputManager.cs:371-375, :77-78, :301` | `ValidatePlayerIndex` throws from 60 Hz poll paths; `[Export] MaxPlayers` on an autoload; static mutable `UltimateChordScratch`. | Degrade instead of throw. |
| L | `CameraShake.cs:22`; `SettingsMenu.cs:209` | Reads `ScreenShakeScale` once; relies on Settings to push (inconsistent with the `HudOpacity` polling model). | Poll or subscribe. |
| L | `HapticFeedbackManager.cs:181-184` | Hit-confirmation magnitudes are literals despite the file's "table in one place" contract. | Name them. |
| L | `SaveManager.FormatNotice:277-279` | Swallows `FormatException` silently — masks a malformed translation. | Log. |
| L | `CreditsController.cs:25-44` | Literal `credits_name_placeholder` rows. | Content. |

---

## 2. Cross-mode parity — the full current divergence table

`AGENTS.md` records H-7 (gravity), M-16 (crouch/skid/coyote/jump-buffer), M-18 (drop-through input), and M-13 (special hitstun) as deliberately deferred. This review confirms all four and adds the rows marked **new**.

| Behaviour | Story (`PlayerController`) | Fighter sim | Status |
|---|---|---|---|
| Gravity | `18 × (0.8+0.4w)`, fall ×1.8, short-hop ×2.5, terminal 600 px/s ≈ 10 u/s | flat −30, no weight, no multipliers, **no terminal velocity** | H-7 (known). Design says g=30, fall 2.5, short 3.5, term 20. Story is 69 % of spec gravity and half the terminal. |
| Jump apex (Einstein) | ≈2.82 u | ≈2.60 u | H-7 |
| Crouch / skid / coyote / jump buffer | present | absent | M-16 (known) |
| Drop-through | double-tap Down (**broken**, §1.1), 15 f | Down+Jump, **30 f** | M-18 + **new** 2× window divergence |
| Special hitstun | authored `HitstunDuration` (default 12 f) | 18 f flat; ultimate 30 | M-13 |
| Attack direction | frontal | **omnidirectional** | **new (bug)** |
| Basic hitboxes | 3 escalating boxes per design | one 2.0×1.6 box | **new** |
| Knockback angle | authored per hit (−1/−1.5/−9 vertical) | fixed 45°, sets airborne | **new** |
| Aerial special gravity | 50 % during cast (design `:764`) | none | **new** |
| Aerial ultimate gravity | 0 during cast (design `:765`) | none | **new** |
| Ledge climb impulse | `force × 45` = 0.833× jump | 0.9× jump | **new** |
| Ledge capture | marker `Area2D`, rise ≤150 px/s, single occupancy | 0.5×1.2 box at platform ends, rise ≤2 u/s, shared occupancy, walk-off auto-grabs, no pull-up action | partly known (plan §2.11) |
| Guard-break shove | `Velocity +=` | replace | **new** |
| Meter accrual | float | int | **new** (drift) |
| Block regen | 180 f | 180 f | parity; design says 120 f (H-5) |
| Combo buffer start | after recovery | after recovery | parity; design says after active |
| Crouch hurtbox shrink, Resonance stack, footsteps | present | absent (by design / driver disables body) | known |
| Constructs | impulse-free, attackable | impulse-free, attackable, bottom-anchored | parity (2026-08-11) |
| Movement-ability wall clamp | scene collision | hardcoded ±10 / y≥0 | **new** |
| Status: TimeDilation anim | flat 0.5 | n/a | **new** (intensity ignored) |
| Status: StaticCharge on boss | slow | n/a | **new** |

**Recommendation.** Add a single cross-mode numeric parity suite: for one character, assert Story and sim agree (within tolerance) on jump apex, fall time to floor, drop-through window, ledge climb height, and knockback angle for hit 1/2/3. Today no test compares the two modes numerically, which is why H-7 has survived three retunes.

---

## 3. Incomplete or missing implementation, by area

Excludes the known Package 7/9/10 scope where `AGENTS.md` already lists it, except to sharpen what "foundation" means.

### 3.1 Fighter mode (local)
- `MatchSettings` not persisted (resets to Stock/3/480/High/High every launch) — Package 6 §2.6 declined a schema bump; revisit.
- No handicap, no team option, no sudden death, no stock-mode timer, no stage striking / "rematch on new stage"; results show no stats although win/loss/draw tallies are recorded.
- CPU never uses up-attack/down-air (`MoveY = 0` in its combat table) — recorded; still open.
- Fighter-mode footsteps do not run (driver disables the presentation body).
- `FighterSpawnIntervals` (Off/Low/Med/High cadences) have zero test references; hazard/orb timing is fixed, not random (design).

### 3.2 Netcode — what "foundation" currently means
Exists: 47-byte protocol-v2 packet, in-memory + UDP transports, `OnlineRollbackSession` with ≤7-frame corrections and a corrected-tick ring, confirmed-hash compare, budget diagnostics, CSPRNG room codes. **Absent** — and required before any real network test is meaningful: handshake (session/seed/characters/stage/rules/build/content-hash), start barrier (host free-runs until the first datagram; then every remote input is >7 frames stale → permanent divergence), configurable input delay, frame-advantage/rift management, input redundancy/NACK (one lost packet = unrecoverable silent desync), ack consumption, resync (`CaptureFullState/RestoreFullState` exist but no packet carries them; protocol has one message type), disconnect adjudication, jitter HUD, forfeit, spectators, NAT/relay/Steam. `NetworkManager`/`OnlineRollbackSession` are referenced nowhere outside `scripts/Networking/`. Determinism risk in the sim itself is low (only float exposure is `FighterLoadoutFactory`'s resource conversion, which is safe if both peers load identical resources — hash them in the handshake).

### 3.3 Story campaign, hub, progression
- **Hub**: single corridor, four hotspots, one NPC with two lines reused for 16 missions; portal gate not implemented (`_portalReady` const true); no codex; `IInteractable`/`InteractionArea` exist and the hub doesn't use them; no state check on interact.
- **Puzzles**: `PuzzleManager` (good, with rewind policies and persistence) instantiated in one level. `TreasureChest`, `PuzzleObject`, `MovableWeight` (outside L6), `SequenceLock` (scene-only L8) unused. No `TileMapLayer` anywhere — the design's tile palette section is unimplemented; all geometry is `BuildPlatform(x,y,w)` calls (40 % of each 370–830-line controller).
- **Content density**: 8 distinct interactive systems across 16 levels (`CyclicHazard` in 12). Enemies are proximity-wave-spawned, not pre-placed with `SpawnTrigger` as designed.
- **Dialogue**: 175 `dlg_*` keys, ~9 lines/level, player is `speaker_player` "Traveler" everywhere; L0 does not open at the character's nexus point.
- **Checkpoints**: walk-through (design: strike-to-activate), no fracture→rift visual, no "Timeline Anchor Stabilized" toast; three per level (design 2–3).
- **Extractors**: no idle cycle; not hidden; Florence authors zero (known).
- **Results**: no rank/kill count/completion %; no secrets system at all.
- **Enemies-before-checkpoint persistence** on collapse-restart is per-wave (`MarkWavesClearedThrough`) hand-written per level, not per-enemy.
- **Resonance**: 5 dead minors, 8 widened minors, `StatModifierIsPercent` unread.
- Design AI items unimplemented: mob hazard immunity, "mobs cannot recover from pits", "elites/bosses bounded to platforms".

### 3.4 Kits
- Sim genericisations beyond the audit's `canonical` label: Rail Charge no contact damage; Splitting Strike spike Story-only; Emancipator static; Golden Ratio fixed AABB; Joan's wings not hold-gated; Warp float hardcoded; Sonata Drift broken; nine movement abilities → four sim code paths.
- Frame data: 15/18 specials at 12/6/18; 8/9 movement abilities at 12/6/18; all cooldowns 10 s / 5 s; only two abilities author `HitstunDuration`.
- Einstein has no `KitTests`/`ContentTests` (every other character does).

### 3.5 Enemies / bosses
- Roster is name-varied, behaviour-thin (§1.4). `WeightedRandom` boss branch never exercised by content. Nine of fourteen phase transitions change only walk speed. `chronal_inventor`/`dread_admiral` structurally identical; `iron_chancellor`/`siegemaster_duke`/`vulcan_decimator`/`tidal_eraser` same shell; `borgia_inquisitor` has two abilities.

### 3.6 UI / audio / art / localization
- **Cutscene surface**: zero (`VideoStreamPlayer` unreferenced; no cutscene `AnimationPlayer`); intro cinematic goes difficulty → `StartCampaign`.
- **HUD**: dust counter in the wrong place with no fade; rewind pulse, 00:10 pulse+chime, ultimate-full flash, radial cooldowns, block-pip cap all absent.
- **Input glyphs**: binding names, not device glyphs; no device-switch.
- **Settings**: complete vs §12 (+UI bus); no UI scale/text size (theme hardcodes 18 px); design's colorblind/hold-toggle/deadzone are explicitly deferred.
- **Audio**: ~10 of ~34 designed SFX cues have any payload (all silence by directive); no `audio/music/`; reverb zones absent; low-pass at transparent 20500 Hz; Fighter climax is a decision with no arrangement to switch to.
- **Art**: 3 frames per animation vs design's 6–165; no separate aerial-combo sheet; no animation-priority arbiter (fifteen scattered `PlayAnimation` sites; FSM enforces the semantics); `RetroSpriteScaleNormalizer` compensates for a pipeline defect at runtime.
- **Localization**: 52 `{0}` keys via `string.Format`, no `TrN`/plurals; `CultureInfo.CurrentCulture` formatting; no font, no fallback, no `layout_direction`; 12 recorded orphan keys.
- **Save select**: no portrait/playtime.

### 3.7 Release
- No export presets; features/renderer mismatch; `stretch/aspect` not set in `project.godot` (runtime enforcer only); F3 overlay live; placeholder credits.

---

## 4. Architecture / code-quality observations

- **Three status-effect state machines** (player, enemy, boss) hand-rolled and diverging. Extract one resolver.
- **Four pixels-per-unit constants** in Story; **hardcoded stage bounds** in the sim's movement-ability path; **hardcoded ultimate timings** in `FighterUltimateRules` duplicating `.tres`.
- **Two orb taxonomies**; **two translation lookups** (one dead); **two knockback formulas** (boss).
- `EventBus` payload gaps (`PlayerIndex`, `BossID`) and no dead-subscriber protection.
- `PoolManager` policy holes (auto-register, recycle-in-callback, no trim).
- Autoload `ProcessMode` inconsistency (`GameManager` pausable, its `LoadingScreen` always).
- Dead fields/data: `GroundFriction`, `DirectionReversalPenalty`, `CurrentBlockCharges` (Story), `EnemyData.JumpForce` ×27, `EnemyAbilityData.CooldownSeconds` ×76, `StatModifierIsPercent`, `PlayTimeSeconds`/`TotalPlayTime`, ten factory hitboxes per character, `LocalizationManager`, `TreasureChest`, `IInteractable` in the hub. A "every authored field is read by some system" content test would catch the data half.
- Sim: `FighterRuntimeComponent` is exactly full (128 B) — any new sim state (tap timer, hitstop counter, DI) needs a new Klotho component (ID 310+). Plan for it once rather than per feature.

---

## 5. Documentation drift (fix in the same change as the code)

| Document | Says | Reality |
|---|---|---|
| `AGENTS.md` (Story progression bullet, "Known gaps" bullet) | "Einstein's three major perks are wired; the other eight … land with their kit passes" / "Bespoke perk execution is wired for Einstein … the other eight … remain" | All 27 majors are wired (`HasStoryPerk` sites in all nine ability files). The Fighter-foundation bullet already says so — the file contradicts itself. |
| `AGENTS.md` | "every production screen … is an authored, themed, focus-authored `.tscn`" | `MatchResults`, `LocalFighterPause` build UI in code. |
| `AGENTS.md` / `design-godot.md:22` | bottom blast zone costs a stock | unreachable on all production stages (H-11 open). |
| `docs/IMPLEMENTATION_STATUS.md:37, :45` | 300-frame buffer, 4× replay; "character-specific perk behavior remains to be connected" | 480 frames, half-duration playback; perks connected. |
| root `IMPLEMENTATION_STATUS.md` "Top Priority Gaps" | items 2, 3, 5, 6, 7 | closed by Packages 5/6/3/5/1 respectively; list is stale by two packages. Codebase statistics table is also stale. |
| `design-godot.md` | pre-retune numbers, dash, no fast-fall, 2 s regen, 300-frame buffer, .NET 8, "Root then Venom", persistent constructs block movement, one-jump characters | superseded — see the design report §3 for the reconciliation pass. |
| `docs/PACKAGE3_KIT_AUDIT.md` | every slot `canonical` | true for identity; the sim approximations in §3.4 should be listed per slot. |
| `IMPLEMENTATION_PLAN.md` Package 7 | "foundation" | §3.2 above — the handshake/redundancy/rift/resync work is the bulk of the package, not polish. |

---

## 6. Test coverage gaps (by value)

Coverage is strong on content contracts, determinism, localization, and Fighter match flow. The holes are behavioural:

1. **No Story movement suite at all** — nothing exercises `PlayerController` gravity, apex, short hop, coyote, jump buffer, skid, air control, fast-fall, or the drop-through *input path* (the pure predicate is tested; the bug in §1.1 is invisible).
2. **No cross-mode numeric parity test** (§2).
3. **No frontal-attack test in either mode** — the sim would fail today.
4. **`HubWorldController`: zero tests** (deposit, grid open/close, Holodeck round-trip, locked-character restore, Sarah).
5. **No end-to-end level playthrough test** (entrance → waves → transitions → boss → post-boss → results) and no reachability assertions on programmatic geometry; room transitions tested only statically; backtracking untested; rewind empty-buffer/no-checkpoint untested; checkpoint→save→resume round trip only in Level 5 (which is the template to copy).
6. **Dust accounting integration** (kill → pickup → wallet → HUD → results) — the H-1 shape.
7. **Block**: no `BlockSystem` charge/regen/break/shove tests; no empty-shield or airborne-block test.
8. **Orb effects** (heal %, ×1.4/×1.3, Aegis, lifetimes) untested; `FighterSpawnIntervals` untested.
9. **UDP transport, `NetworkManager`, `MatchmakingManager`**: zero tests; packet loss/dup/reorder never simulated; no CPU *behavioural* rate test.
10. **Perk execution**: nothing asserts any of the 27 majors changes behaviour; nothing fails on the 5 dead minors.
11. **`StatusControllerTests`**: 4 cases; boss/enemy copies untested; intensity-ignored animation, stun-outliving-status untested.
12. **Boss**: no per-ability cooldown test (only `> 0`), no summon-cap test, no RNG-across-pool-reuse test.
13. **Core**: `CameraShake`, `LocalizationManager`, `GameManager` load state machine, `InputManager` device topology (only incidental), `EventBus` roster/payload/lifetime, binding conflicts vs `ui_*`/chord, `ViewportEnforcer._Ready`.
14. **Balance guards**: a one-line sweep pinning ultimate totals into a band would catch Lincoln's 40; a "distinct sim behaviour per movement ability" test would catch Mozart.
15. **Dead-data guard**: "every `CharacterData`/`EnemyData`/`EnemyAbilityData` field is read somewhere".
16. **Einstein kit/content suites** (missing entirely).
17. **Human gates** unchanged: no end-to-end playthrough at each difficulty, no visual review of the presentation layer.

---

## 7. Prioritised implementation recommendations

**Round 1 — bugs that change how the game plays today (small, high value):**
1. Sim melee/special facing check + escalating basic hitboxes + per-hit knockback angle (§1.1, §2).
2. Story drop-through input path; Story airborne block gate; `charges > 0` gate on the block stance in both modes (§1.1).
3. Rewind implicit spawn anchor; bidirectional room transitions (§1.2).
4. Boss `StaticCharge` → shared status resolver; boss knockback through `DamageCalculator` (§1.4, §1.1).
5. Per-ability boss cooldowns + summon cap (§1.4).
6. Mozart movement `.tres` + sim float direction (§1.4).
7. CPU block-latch fix (§1.3).
8. F3 overlay debug gate; `config/features`; export presets (§1.5).

**Round 2 — correctness hazards and dead machinery:**
9. `FindConflictingAction`; conflict scan over `ui_*` + chord; `OnCooldownComplete` player index; `EventBus` dead-subscriber guard; `PoolManager` recycle-in-callback + auto-register policy; `GameManager` `ProcessMode.Always` (§1.5).
10. Extractor idle cycle + physical pickups for boss/extractor dust; puzzle-completion save; `ChronalRiftZone` as a zone flag; `PercentValue` + the two missing stat types (§1.2).
11. Delete `LocalizationManager` (or facade it), the ten orphan factory hitboxes, dead `CharacterData` fields; write playtime.
12. One shared cross-mode parity suite; a Story movement suite; hub and level-playthrough integration tests (§6).

**Round 3 — features (after the design decisions in the design report):**
13. Hitstop counter + one hitstun-agency verb (new Klotho component 310+ — plan the layout once).
14. Stage floor segments *or* delete the bottom-blast condition (H-11).
15. Block lockout / shieldstun per decision (H-5).
16. `MatchSettings` persistence; rematch-on-new-stage; stock-mode timer / sudden death per decision.
17. Hub gate + NPCs; L0 nexus openings; per-hero dialogue lines.
18. Package 7 proper: handshake (with content hash and seed), start barrier, input delay, rift management, input redundancy, ack use, resync packet, disconnect/forfeit/jitter UX — then transport.

**Round 4 — docs:** fix the drift table in §5 in the same commits; run the `design-godot.md` V7 reconciliation.
