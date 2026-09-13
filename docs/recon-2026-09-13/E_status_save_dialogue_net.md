# Recon dossier E — Rollback/netcode, Status effects, Event Bus, Dialogue, Secure Save, Lives & Stock, Online, Accessibility & Controls

Scope: master lines 1647–2319 of `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md` plus
`docs/ROLLBACK_STATE_CONTRACT.md` (S01), `docs/MULTIPLAYER_DELIVERY.md` (M01), `docs/COMFORT_SETTINGS.md`
(C01a/b/c), `docs/STORY_PERSISTENCE.md` (F10), `docs/DESIGN_BUILD_DEVIATIONS.md` (P04), and the F21/F22
half of `docs/FIGHTER_MATCH_RULES.md` that the Lives & Stock section points at.

Repo pinned at `main` @ `31fed14` (V7.4 stun-lock pass, 2026-08-29), working tree clean at session start.
Repo `design-godot.md` is a stale V7.3 mirror — everything below is measured against the **master**.

Legend: **IMPL** implemented · **PART** partial · **MISS** missing · **RET** retired by design (repo code
must be removed/gated) · **CONFLICT** repo actively does the opposite of the new design.

Sizes are engineering-effort estimates for the code change only (S ≤ ~0.5 day, M ~1–3 days, L > 3 days or
cross-cutting). "Shared" lists files other workstreams in this review will also edit.

---

## 0. Headline findings (the six that change other people's plans)

1. **`FighterVerbComponent` (Klotho ID 310) has exactly 4 bytes of headroom.** 19 `int` (76 B) + 6 `FP64`
   (48 B) = **124 B** against Klotho's 128-byte per-component budget. One more `int` fits; *nothing else*.
   Every new sim field below (D04 Defy protection, Echo Step arm/activation/generation, Suppression,
   stocks-lost, F22 phase generation) needs **new components at ID 312+** (310 and 311 are taken).
2. **Echo Step's ring is structurally wrong for S01 and cannot fit one component.** Repo stores 5 samples
   every 6 frames (`FighterEchoRingComponent`, ID 311, 88 B). S01 requires **31 consecutive start-of-tick
   positions** + head + latest tick + valid count + history generation = 31×2 `FP64` (496 B) + 4 ints.
   That is ≥ 5 components (ID 312–316) or a redesigned storage strategy. This is the single largest
   deterministic-state change in the section.
3. **The crash fee contradicts F10.** `SessionExitGuard` + `SaveManager.ApplyAbnormalExitFeeIfMarked`
   charge the 20% undeposited-dust fee on *crash*. F10 says a deliberate live-attempt exit charges it once
   and **crash recovery charges nothing**. CONFLICT — a player-facing regression against the new design.
4. **`InputBindingSet` cannot represent "explicitly Unbound".** `InputBindingSet.Normalize()` drops any
   action row whose event list is empty (`if (events.Count == 0) continue;`). C01c requires explicit
   Unbound to be distinguishable from "no override, inherit default" for Ultimate/Grab/Echo Step. This is
   a hard blocker on the C01c save shape, independent of the schema bump.
5. **Dialogue skip is scoped wrong in both directions.** Repo gates hold-to-skip on
   `save.IsCompleted && save.ViewedDialogueIDs.Contains(id)` (per-slot). Master moves the seen set to
   **`GlobalSaveData.seenDialogueIDs`**, makes skipping available generally with a **first-skip
   confirmation**, and requires skipping to run the **same idempotent gameplay-effect resolution** as
   completion. None of the effect-ledger half exists.
6. **`FighterCpuController` state is entirely outside the ECS** (`_randomState`, `_schedule[128]`,
   `_sustainedHeld`, `_activeEdges`, `_pendingEdges`, `_edgeFramesRemaining`, `_edgeGapFramesRemaining`,
   `_lastObservedHitstunFrames`, `_escapeStanceLingerRemaining`, `_hitstunHoldBlockActive`,
   `_hitstunDiActive`). S01 explicitly requires CPU difficulty/profile, reaction history, next evaluation
   tick, plan stage, opportunity IDs, consumed probability checks and action budget to be snapshot state.
   A rollback in a live CPU match today silently diverges CPU behaviour.

---

## 1. §4 Rollback Netcode & Physics (S01)

### 1.1 Math-engine and budget wording (master 1653–1662, 1731–1734)

Design now: bit-identical cross-platform results are a **validation requirement**, not a property of
`FP64`; `< 0.1 ms` snapshot and `8.0 ms` 7-frame resim are **unmeasured targets for the full inventory**.

Repo: `docs/PERFORMANCE_BASELINE.md` records 0.058 ms median / 0.104 ms p95 worst-case resim, 2 699 B
largest snapshot — but that is against *today's* partial component set on one Windows Debug machine.

- Status: **PART** (numbers exist, inventory they measure is incomplete; no cross-platform run).
- Change list: docs-only in the repo (`AGENTS.md` "rollback-readiness gate" wording,
  `docs/PERFORMANCE_BASELINE.md` caveat) until the inventory lands, then re-measure.
- Size: **S** (docs) / **L** (re-measure after §1.2–1.5).

### 1.2 `PlayerSnapshot` two-slot status with intensity (master 1674–1690)

Design: `DamageStatusType/Duration/Intensity` + `ControlStatusType/Duration/Intensity`, "lives on the new
Klotho component (ID 310+)".

Repo: already two-slot, but on **ID 305** `FighterRuntimeComponent`, bit-packed:
`_statusTypesPacked` (low 16 control / high 16 damage), `_statusFramesPacked` (same split),
`_controlStatusIntensityMilli` / `_damageStatusIntensityMilli` (milli-quantized ints), plus
`StatusTickFrames`. Accessors `StatusType`, `DamageStatusType`, `StatusFrames`, `DamageStatusFrames`,
`StatusIntensity`, `DamageStatusIntensity`, `PresentedStatusType`.

- Status: **IMPL** (functionally equivalent; component ID differs from the design's prose, which is
  descriptive, not normative — do **not** migrate it off 305, that would break the 128 B budget there).
- Residual: the design snapshot lists *duration* as `FP64`; repo stores 16-bit frame counts. Fine and
  arguably better for determinism, but record it in the S01 field manifest as a deliberate encoding.
- Size: **S** (manifest entry only).

### 1.3 D04 Defy protection state (master 1690–1698)

Design: `DefyHistoryUsed` + **`DefyProtectionAwaitControl`** + **`DefyProtectionFrames`** (spent use,
awaiting control, remaining live ticks), retaining proc/presentation identity.

Repo: `FighterVerbComponent.DefyHistoryUsed` only. No awaiting-control phase, no 60-tick protection
window, no proc identity.

- Status: **PART**.
- Change list: 2 new ints. One fits in `FighterVerbComponent`'s 4 spare bytes; the second forces a new
  component. Recommendation: put **both** on a new `[KlothoComponent(312, MaxCount = 2)]
  FighterDefenseComponent` together with the D04 presentation/proc identity, leaving 310's last int as
  headroom. Consumers: `FighterSimulationSystems` damage chokepoint (`FighterDamageRules.ApplyFighterHit`),
  `FighterSimulationDriver` (presentation), `FighterHudModel` (Defy seal, see §3.1).
  Story mirror lives on `PlayerController` (`STORY_PERSISTENCE.md` D04 paragraph).
- Size: **M**. Shared: `scripts/FighterSim/FighterSimulationComponents.cs`,
  `FighterSimulationSystems.cs` (combat workstream also edits both).

### 1.4 Echo Step ring and activation state (master 1690–1698; S01 "Echo Step and combat allowances")

Design requires, per fighter:
`PositionRing[31]` (consecutive start-of-tick positions), `PositionRingHead`, `PositionRingLatestTick`,
`PositionRingValidCount`, `PositionHistoryGeneration` (reset at spawn/recovery anchors, **no synthetic
history**), `EchoStepArmed`, `EchoStepDestination` (locked at activation to the exact tick t−30),
`EchoStepActivationTick`, `EchoStepWindupFrames`, `EchoStepCooldownFrames`.

Repo: `FighterEchoRingComponent` (ID 311) = 5 `FP64` pairs + `RingIndex` + `SampleCountdown`; sampling
every 6 frames, oldest sample *approximates* 30 frames ago. Arm/destination/windup/cooldown live on
`FighterVerbComponent` (`EchoStepCooldownFrames`, `EchoStepWindupFrames`, `EchoStepDestX/Y`) — there is no
armed flag (windup > 0 stands in), no activation tick, no history generation, no valid count.

- Status: **PART / structurally non-conforming**.
- Change list: replace ID 311 with a bank of components (e.g. 312–316, 8 samples each = 8×16 B + ints,
  comfortably under 128 B) or a single `MaxCount = 62` per-sample component keyed by `(playerID, slot)`;
  add `PositionRingHead/LatestTick/ValidCount/Generation` and `EchoStepArmed/ActivationTick` on the new
  312 defense/temporal component. Sampling moves from every-6-frames to **every tick**. F22 Sudden Death
  entry must "initialize a new Echo Step history generation at the spawn with one valid sample" and
  **require 30 subsequent ticks before use** — that is the `ValidCount`/`Generation` gate.
- Story side: `PlayerController.AdvanceEchoStep` / `TryStartEchoStep` and
  `BasicComboRules.EchoStepLookbackFrames` (30) keep their own history; mirror the generation reset on
  death rewind / checkpoint reconstruction.
- Size: **L**. Shared: `FighterSimulationComponents.cs`, `FighterSimulationSystems.cs`,
  `scripts/Characters/PlayerController.cs`, `tests/Determinism/RollbackReadinessTests.cs`.

### 1.5 The full S01 authoritative inventory + field manifest

S01 requires a **field-level manifest** (component owner, runtime field, serializer location,
initial/inactive value, hash participation, reconstruction recipe, version/migration rule, verification
case) for 16 categories. Audited against the repo:

| S01 category | Repo location | Status |
|---|---|---|
| Match identity/configuration | `FighterMatchComponent` (301, singleton): mode, stocks, timer, items, hazards, `WorldSeed`, `StageHazardTypeID`. **No** rules/content/protocol version, no stage ID, no negotiated content hash | PART |
| Match progression | `FighterMatchComponent` MatchState/Countdown/GoBanner/SuddenDeathActive/Remaining/KO counters. **No** phase *generation*, no result-event identity, no frozen regulation totals (F21/F22) | PART |
| Stable identities / lifecycle | `NextEntityID` on 301; `EntityID` on projectile/persistent/hazard/orb/zone components | IMPL |
| Inputs and action recognition | `MoveX/MoveY/HeldButtons/PressedButtons/ReleasedButtons` on `FighterRuntimeComponent`. **No** buffered-action expiry, no chord arbitration record, no direct-vs-preset origin (C01c), no tap-window state | PART |
| Body and traversal | `FighterStateComponent` + `FighterRuntimeComponent` (ledge anchor/frames/lockout, drop-through, jumps, roll via `UniversalMovementState`) | IMPL |
| Physics world | No `FPPhysicsWorld`; movement is hand-integrated against `FighterStageGeometry` (constant, not snapshotted — documented as re-derived). Acceptable under "derived with a reconstruction recipe", **needs the recipe written down** | PART |
| Actor state and timing | hitstop/hitstun/shieldstun/daze/getup on 300/305/310 | IMPL |
| Attacks and ability executions | `AttackPhase/AttackPhaseFrames/AttackFlags`, cooldowns. **No** per-execution generation, no per-execution contact records | PART |
| Grab and throw | `FighterVerbComponent` GrabPhase/Frames/ThrowDirection/BeingHeld/ThrowImmunityFrames | IMPL |
| HP, meter, Rally | `CurrentHP/MaxHP/Influence`, `EchoPool/EchoDrainPerFrame`. **No** uncredited victim-damage records / settlement status (D03f) | PART |
| Defenses and Defy | `BlockCharges/BlockRegenFrames/ShieldStunFrames/BlockLockoutFrames`, `AegisHits`. **No** D04 fields, no block-event IDs | PART |
| Statuses, marks, modifiers | two slots packed on 305. **No** slot generation, no source/owner/execution, **no Conductive marks at all** (see §2.5), no Ultimate-origin tag (D03h) | PART |
| Echo Step and allowances | §1.4; `MomentumRefundsSlotOne/Two` present | PART |
| Projectiles / zones / summons | components 302/309 with owner, status payload, lifetime. **No** hit/re-hit records, no pierce/bounce, no Ultimate-origin flag | PART |
| Persistent combat objects | component 303 incl. `LinkTickFramesRemaining` fence pairing | IMPL |
| Orbs and arena | 308 + 304 (hazard phase/dwell/hit mask), `NextOrbSpawnFrames`, `NextHazardSpawnFrames` | IMPL |
| Randomness and CPU | `RandomState0/1` on 301 ✔; **CPU controller state entirely outside the ECS** ✘ (headline #6) | PART |
| Event queues and ledgers | None — damage resolves inline each tick; no pending-event queue, no reward/claim flags | MISS |
| Phase reset and respawn | `RespawnFramesRemaining`, `InvulnerabilityFrames`, `SpawnPosition` ✔; F22 atomic retained-HP transition ✘ | PART |

- Change list: (a) write the field manifest as a repo doc, (b) move CPU controller state into a Klotho
  component (or a deterministic side-buffer that is saved/restored with the frame), (c) add phase/result
  generation + frozen totals for F21/F22, (d) add per-execution contact ledgers,
  (e) add negotiated identity (rules/content hash, stage ID, protocol version) to the hashed set.
- Size: **L** (multi-week; this is Package 7's entry work). Shared: everything under `scripts/FighterSim/`.

### 1.6 Execution sequence and replay side effects (master 1739–1741)

New requirements: snapshots are end-of-tick; retain received actual inputs; discard the superseded branch
and its later gameplay ledgers; reconcile persistent visuals; **deduplicate predicted one-shot
sounds/VFX/rumble by stable logical event ID**; confirm results before durable writes; presentation may
never re-trigger gameplay.

Repo: `FighterSimulation.CorrectRemoteInput` restores and resimulates through Klotho snapshots (correct in
shape). Presentation (`FighterSimulationDriver`) reads component values each frame and fires damage
numbers / camera shake / glow directly — **no event identity, no dedup, no replay tolerance**.
`FighterMatchStatistics` / global win-loss writes are not gated on a confirmed result.

- Status: **PART** (sim loop ok; presentation dedup and durable-write confirmation **MISS**).
- Change list: introduce a logical event ID `(matchGeneration, phaseGeneration, tick, sourceExecution,
  target, ordinal)`; a presentation-side dedup set outside the gameplay hash; gate
  `SaveManager`/`GlobalSaveData` win/loss/draw writes on a confirmed match-result event.
- Size: **M**. Shared: `scripts/FighterSim/FighterSimulationDriver.cs`, `scripts/Core/CameraShake.cs`,
  `scripts/Core/HapticFeedbackManager.cs`, `scripts/UI/FighterHUD.cs`.

### 1.7 Desync hash protocol (master 1744–1745)

Design: hash the canonical inventory at matching confirmed ticks **including negotiated content/rules
identity and PRNG state**, excluding presentation/transport and the checksum field; full-state resync
proposal retained; **minimum behaviour is fail-closed — halt with a visible error, offer rematch/disconnect,
no partial state installation**.

Repo: `FighterSimulation.CurrentHash => _simulation.GetStateHash()` (Klotho component hash — PRNG included
because `RandomState0/1` are components; content/rules identity **not** included).
`RollbackInputPacket` (protocol v2, 47 B) carries `HashTick`+`StateHash`. `NetworkManager` exposes
`DesyncCount` / `LastDesyncTick` as **diagnostics only** — no halt, no error UI, no resync.
`OnlineRollbackSession.CaptureFullState()` / restore exist but are unused by a failure path.

- Status: **PART** → effectively MISS in production (the whole LAN path is unreachable, §5.1).
- Change list: add rules/content hash to the hashed identity and to a pre-start handshake; implement the
  fail-closed halt + `network_desync_*` en.csv strings + a match-halt UI state.
- Size: **M** (Package 7). Shared: `scripts/Networking/RollbackProtocol.cs`, `NetworkManager.cs`.

---

## 2. Status Effect Architecture (V7.6)

### 2.1 `StatusType` gains `Suppression`

Design: `Suppression` (the Eraser line) — **Control slot**, **Story-only source**. Locks Special 1/2,
Movement Ability and Ultimate *cast*; cooldowns keep ticking; basics, block, grab, Rally, DI, tech, Defy
History and rewinds unaffected. Visual: persistent gold resonance aura **desaturates to cold grey**,
outline shader drops to base priority, three cooldown slots + ultimate ring show a cold cross-out, refused
inputs play a dull null-tone.

Repo: `enum StatusType` lives in **`scripts/Core/EventBus.cs:260`** with exactly
`None, TimeDilation, Venom, StaticCharge, RadiantBurn, Root`.

- Status: **MISS**.
- Change list:
  - `scripts/Core/EventBus.cs` — append `Suppression` (value 6; append-only, the sim casts the enum to
    `int` in ~30 places in `FighterEntitySystems.cs`, so **never reorder**).
  - `scripts/Combat/StatusController.cs` — `SuppressionStrategy` + `StrategyFactory` entry;
    `IsDamageStatus` already routes anything non-Venom/non-RadiantBurn to control, so routing is free.
  - `scripts/Characters/PlayerController.cs` — a `IsAbilityCastSuppressed` gate at the four cast sites
    (`Special1` press ~line 1185/2610, `Special2`, movement ability, ultimate) that refuses without
    consuming cooldown/meter, plus the null-tone SFX hook.
  - `scripts/Enemies/EnemyController.cs` / `BossController.cs` — `ApplyStatusEffect` switch arms (may be
    a no-op for enemies; decide explicitly rather than silently).
  - Fighter sim: **must refuse Suppression** (Story-only). Add a guard in
    `FighterEntitySystems.ApplyStatus` (line ~445) so an authored `.tres` carrying it cannot enter the
    deterministic sim, + a test.
  - `localization/en.csv` — `status_suppression`, plus any HUD cross-out tooltip.
  - Presentation: `GlowPresentationController` / `UIPalette` cold-grey aura + cooldown cross-out.
- Size: **M**. Shared: `scripts/Core/EventBus.cs` (everyone), `PlayerController.cs` (combat workstream),
  `localization/en.csv`.

### 2.2 `StatusSlots` / `StatusSlot` / `StatusRouting` as data

Design: a `StatusSlots { StatusEffectData Damage; StatusEffectData Control; }` container carried by
`PlayerController`, `EnemyController`, `BossController` **and** (as six fields) `PlayerSnapshot`; a
`StatusSlot { Damage, Control }` enum; a `StatusRouting.SlotOf(StatusType)` compile-time table.

Repo: no `StatusSlots`, no `StatusSlot`, no `StatusRouting`. Equivalent logic is a private
`StatusController.Slot` class + `StatusController.IsDamageStatus(type)` static, **re-implemented
independently three more times**: `EnemyController.ApplyStatusEffect` (1051–1102),
`BossController.ApplyStatusEffect` (620–670), `FighterEntitySystems.ApplyStatus` (445–460).

- Status: **PART** (behaviour present, contracted shape absent, logic duplicated 4×).
- Change list: add `StatusSlot`, `StatusRouting`, `StatusEffectData`, `StatusSlots` to
  `scripts/Combat/StatusController.cs` (or a new `scripts/Combat/StatusSlots.cs`); refactor the four call
  sites onto `StatusRouting.SlotOf`. Keeping `StatusController.IsDamageStatus` as a forwarder avoids
  touching the sim's hot path signature.
- Size: **S–M**. Shared: `EnemyController.cs`, `BossController.cs`, `FighterEntitySystems.cs`.

### 2.3 `IStatusEffectTarget` interface

Design:
```csharp
public interface IStatusEffectTarget : IDamageable {
    StatusSlots ActiveStatuses { get; set; }
    void ApplyStatusEffect(StatusType status, float duration, float intensity); // routes + stronger-wins
    void ClearStatusEffect(StatusSlot slot);
    void ClearAllStatusEffects();                 // death/respawn, Restart Level
    Node2D TargetNode { get; }
}
```
Repo: **the interface does not exist anywhere** (`grep -r IStatusEffectTarget` → no hits).
`PlayerController`, `EnemyController` and `BossController` each expose an ad-hoc
`ApplyStatusEffect(StatusType, float, float = 1f)` with no common contract; clearing is
`StatusController.ClearStatus()` / `ClearStatus(type)` / per-class `ClearControlStatusSlot()`.

- Status: **MISS**.
- Change list: declare the interface (probably `scripts/Combat/StatusController.cs` alongside
  `IStatusStrategy`), implement on the three controllers, add `ClearAllStatusEffects()` calls at
  death/respawn and Restart Level (Story) and at F22 Sudden Death entry (Fighter).
  `IDamageable` exists in the repo — confirm the inheritance is compatible before wiring.
- Size: **M**. Shared: same three controllers.

### 2.4 Stronger-wins overwrite (**replaces newest-overwrites**)

Design, within a slot:
- different type → always replaces;
- same type → replaces only if `newIntensity × newDuration >= currentIntensity × currentRemaining`;
  it brings its own duration, nothing is added or refreshed;
- weaker same-type application **does nothing** (no replace, no refresh);
- the comparison is **fixed-point in the sim** (both factors are snapshot state) so it is rollback-safe;
- the other slot is never touched (unchanged).

Repo: newest-always-wins in **all four** sites —
`StatusController.ApplyStatus` (`ClearSlot(slot, raiseEvents:false)` then assign),
`EnemyController.ApplyStatusEffect` (`ClearDamageStatusSlot()` / `ClearControlStatusSlot()` then assign),
`BossController.ApplyStatusEffect` (same), `FighterEntitySystems.ApplyStatus` (unconditional assign).

- Status: **CONFLICT / MISS** — this is the single behavioural rule change in the section.
- Change list: one shared pure helper, e.g.
  `StatusRouting.ShouldReplace(StatusType current, float currentIntensity, float currentRemaining,
   StatusType incoming, float incomingIntensity, float incomingDuration)` for the Story/Godot side, and a
  fixed-point sibling in the sim using `DamageStatusFrames`/`StatusFrames` (int) × the milli-intensity int
  — an `int64` product, exactly reproducible, no `FP64` division needed. Wire all four sites.
- Tests: extend `tests/unit/StatusControllerTests.cs` (weaker-same-type no-op, equal strength replaces,
  different-type always replaces, other slot untouched); add sim cases to
  `tests/Determinism/FighterVerbLayerTests.cs` or a new `FighterStatusSlotTests`; add
  enemy/boss cases to `EnemyControllerTests`/`BossControllerTests`.
- Size: **M**. Shared: `StatusController.cs`, `EnemyController.cs`, `BossController.cs`,
  `FighterEntitySystems.cs`.

### 2.5 Conductive mark (F07) — a caster-owned combo mark, **not** a status slot

Design: a bounded per-source entry on each target holding **source identity + remaining duration**
(90 baseline frames; 150 only for an upgraded Story finisher; linked fences stay at 90). Must be added to
the **actor save/restore contract** and the **deterministic Fighter snapshot** with **stable owner
ordering** and **no wall-clock timer**. Must store **consumed/processed attack state** so a restored
multi-hit Pulse cannot duplicate chains. Visual: small **non-pulsing circuit glyph** near the target's
health display, owner-identified in Fighter; never occupies a status HUD slot and never resembles the stun
outline.

Repo: nothing. `grep -r Conductive` hits only `scripts/Environment/ConductiveCoil.cs` — an unrelated
Level 3 beam-routing puzzle prop.

- Status: **MISS** (100%).
- Change list: new mark table on `PlayerController`/`EnemyController`/`BossController` (Story) and a new
  Klotho component (ID 312+, `MaxCount` = targets × max sources) in the sim; add to S01 inventory; HUD
  glyph in `FighterHudModel`/`StoryHUD`; `localization/en.csv` tooltip. Tesla's kit owns application and
  consumption — coordinate with the character workstream.
- Size: **M–L**. Shared: Tesla kit files, `FighterSimulationComponents.cs`, HUD.

### 2.6 Status visual-feedback deltas

- `StaticCharge` (F24): the yellow **pulsing outline** is downgraded to "yellow secondary glow/flash +
  crackling sparks for the interrupt"; Story may keep a yellow effect outline; **Fighter's ownership edge
  is unchanged** (an ownership outline must never be confused with a status outline). The control-slot
  glyph/radial stays independently visible.
  Repo: `GlowPresentationController` arbitrates a three-channel outline stack and `SetStatus(type)` paints
  a single status colour. Status: **PART** — needs the Fighter-side separation audit.
- `Suppression`: see §2.1.
- Both status slots must be independently visible. Repo presents **one** pip:
  `FighterRuntimeComponent.PresentedStatusType` (control leads) and
  `StatusController.ClearSlot`'s "re-announce the survivor" hack that exists precisely because the HUD
  only tracks one status. Status: **PART**; the HUD contract workstream owns the pip count, but the
  re-announce hack should be deleted once two pips exist.
- Size: **S–M**. Shared: `scripts/Combat/GlowPresentationController.cs`, `scripts/UI/FighterHudModel.cs`.

---

## 3. Event Bus & Signal Architecture

### 3.1 `OnUltimateMeterChanged`

Design: consumers become "HUD Ultimate Gauge, **Defy Seal**, Character Aura"; fires on **all gains, spends
and drains**; F13 seal also recomputes on **used/life/mode changes and on load/rollback after the full
state update**.

Repo: `EventBus.OnUltimateMeterChanged` (`UltimateMeterPayload`) exists and is raised by
`UltimateMeter`/`PlayerController`. **No Defy seal consumer exists** (`grep Defy` in `FighterHUD.cs` /
`FighterHudModel.cs` → no hits). No post-rollback recompute hook.

- Status: **PART**.
- Change list: add a Defy-seal model field to `FighterHudModel` (and Story HUD), recompute it from
  `DefyHistoryUsed` + meter + life generation + mode, and call it after
  `FighterSimulationDriver`'s post-rollback sync, not only on the event.
- Size: **S**. Shared: `scripts/UI/FighterHudModel.cs`, `scripts/UI/FighterHUD.cs`.

### 3.2 `OnLevelComplete` (N01)

Design: "emitted **once by the accepted sealing/completion transaction**, not automatically on boss
defeat".

Repo: `EventBus.RaiseLevelComplete(string levelID)` is called by the level controllers at their completion
tail; `SaveManager.SaveLevelCompletion` subscribes. There is no sealing interaction, no
`attemptState.sealReadiness`, no `completionTransaction`.

- Status: **PART** (the event exists with the right signature; its *trigger* and once-only transaction
  do not). The N01 sealing anchor itself is the narrative/campaign workstream's; the **save-side
  once-only commit** is mine (§4.6).
- Size: **M** (save half). Shared: `scripts/Environment/StoryLevelControllerBase.cs`, `SaveManager.cs`.

### 3.3 Everything else in the event table

The diff shows no other row changes. `OnStatusEffectApplied` keeps its `StatusEffectPayload` — but that
payload carries a **single** `Type/Duration/Intensity` (`scripts/Core/EventBus.cs`), which is why
`StatusController.ClearSlot` re-announces a survivor. With two-slot HUD pips the payload should gain a
slot discriminator. Status: **PART**, size **S**.

---

## 4. Dialogue System + Secure Save System (F10 / N05 / P04)

### 4.1 Dialogue completion and confirmed skipping (master 1960–1962, 2084–2101; STORY_PERSISTENCE §"Dialogue")

Design now requires:
1. `GlobalSaveData.seenDialogueIDs : HashSet<string>` — **global across slots**, union after completion
   **or confirmed skip**, never cleared by level restart or slot change; **affects skip confirmation only**.
2. **Confirmed skipping invokes the same idempotent gameplay-effect resolution as normal completion**
   (unlock Legacy abilities, latch gates, commit completion, select a destination).
3. Presentation ID and gameplay-effect ID are **separate**; per-attempt
   `attemptState.appliedScriptEffectIDs` records applied effects; `pendingGlobalSeenIDs` drains by
   idempotent union.
4. "Seeing a scene in another slot suppresses only the **first-skip confirmation**; it never suppresses
   another slot's gameplay effects."
5. An interrupted unresolved scene resumes its required flow; a resolved scene cannot award twice.
6. `lastViewedDialogueID` is demoted to a **presentation cursor only** — gameplay completion/unlocks must
   use applied script-effect IDs.

Repo (`scripts/UI/DialogueManager.cs`):
- `HoldToSkipSeconds = 0.75f`; `_skipEligible = save != null && save.IsCompleted &&
  save.ViewedDialogueIDs.Contains(sequence.DialogueID)` (line 301–303) — skipping is **gated on a
  completed campaign** and a **per-slot** seen list (`StorySaveData.ViewedDialogueIDs`, schema v5).
- `EndSequence()` → `RecordLastViewedDialogue(id)` (sets `LastViewedDialogueID` and appends to
  `ViewedDialogueIDs`) → `EventBus.RaiseDialogueComplete(id)`. No effect ledger, no idempotency, no
  first-skip confirmation.
- `dialogue_hold_skip` en.csv key + fill bar exist.

- Status: **PART / wrong scope**.
- Change list:
  - Move the seen set: `GlobalSaveData.SeenDialogueIDs` (new field). Keep
    `StorySaveData.ViewedDialogueIDs` for one migration pass (union into global, then stop writing it) —
    do **not** delete it in the same release; `SaveSchemaMigrator` needs it as the source.
  - Drop the `IsCompleted` gate; skipping becomes generally available.
  - Add a **first-skip confirmation** modal (reuse `scripts/UI/ConfirmModal.cs`) shown when the sequence
    ID is *not* in the global set; suppress it when it is.
  - Split effect resolution out of `EndSequence()` into an idempotent
    `ResolveDialogueEffects(sequenceID)` keyed by a gameplay-effect ID, guarded by
    `attemptState.appliedScriptEffectIDs`; call it from **both** the completion and the confirmed-skip
    paths.
  - `pendingGlobalSeenIDs` drain on the next successful global write.
  - en.csv: `dialogue_skip_confirm_title` / `_body` / `_confirm` / `_cancel`.
- Tests: `tests/unit/DialoguePresentationTests.cs` (existing hold-to-skip cases will need rewriting —
  they currently assert the `IsCompleted` gate), plus new cases for cross-slot seen IDs and
  effects-applied-exactly-once for both watch and skip.
- Size: **M**. Shared: `SaveManager.cs`, `localization/en.csv`, `scripts/UI/ConfirmModal.cs`.

### 4.2 Story save schema — required new fields

Current repo shape: `StorySaveData` (SaveManager.cs:9–76), `GlobalSaveData` (100–170),
`SaveSchemaMigrator.CurrentVersion = 5` (SaveEnvelope.cs:274, shared by **both** payloads).

| Master field | Repo today | Status |
|---|---|---|
| `lastViewedDialogueID` (presentation cursor only) | `LastViewedDialogueID` — already cursor-only | IMPL |
| `currentHP` / `currentLives` / `currentUltimateMeter` = **latest durable**, ordinary load never restores an older checkpoint value, never refreshes | `CurrentHP` / `CurrentLives` / `CurrentUltimateMeter`, written **only at checkpoint save** (`SaveManager.SaveCheckpoint` 615–622) and at collapse | PART — semantics right, durability wrong (§4.5) |
| `checkpointIntegrity` (separate allowance for paid Collapse/Snap; F11 granted-timer minimum ≥ 25) | **absent**; only `LevelIntegrityPercent` (live gauge) | MISS |
| `attemptState : StoryAttemptState` (21 sub-fields, §4.3) | **absent** as an object; a *subset* lives as loose root fields | PART |
| `completedLevels` | `CompletedLevels` | IMPL |
| `levelIntegrity : Dictionary<string,float>` — **PreBoss-locked**, unrounded, exactly 15 IDs (2–15 + one 4A) for N05 | `IntegrityByLevel` — written at **completion** from `LastLevelIntegrityPercent`, no PreBoss lock, name differs | PART |
| `depositedChronalDust` | `DepositedChronalDust` | IMPL |
| `levelChronalDust` (death rewind preserves; Collapse/exit fee once; Restart clears; completion banks once) | `LevelChronalDust` | PART (fee-once ledger missing, §4.5) |
| `gridProgress` | `GridProgress` | IMPL |
| `unlockedLegacyAbilities : Dictionary<string,List<string>>` (V7.5 Legacy Unlock Schedule) | **absent** | MISS |
| `playTime` | `PlayTimeSeconds` | IMPL |
| `isCompleted` — committed **after Level 15 victory + reward settlement, before credits** | `IsCompleted`, set by `MarkCampaignCompleted()` which `CampaignCompletionSequence` calls **as the credits begin** | PART |
| `lastSavedTimestamp`, `saveVersion` | present | IMPL |

Repo extras not in the master table (keep, but record in the manifest): `RatingByLevel`,
`SecretsFoundByLevel`, `CompletedPuzzleIDs`, and the v5 loose attempt fields
(`ActivatedCheckpointIDs`, `FontUsesConsumed`, `DestroyedExtractorIDs`, `FoundSecretIDs`,
`LevelIntegrityPercent`, `ViewedDialogueIDs`, `HasSeenCollapseBeat`).

### 4.3 `StoryAttemptState` — the whole object is new

F10 requires a root `attemptState` with: `attemptID`, `levelID`, `revision`, `status`
(`Active | AwaitingHubResume | RecoveryPending | Smothered | CompletionPending | Completed |
LegacyRecoveryRequired`), `anchorChargesRemaining`, `defyHistoryUsed`, `checkpointRecord`
(anchor ID/role, baseline encounter IDs + version, activated checkpoint IDs, granted-benefit IDs),
`currentIntegrity`, `startingExtractorCount`, `destroyedExtractorIDs`, `foundSecretIDs`,
`puzzleStateByID`, `preBossLocked`, `finalGateIntegrity`, `fontUsesByID`, `collectedHealingIDs`,
`pendingHealing` (source ID, uncredited amount, remaining duration), `rewardSources`
(unissued / spawned-uncollected / collected + quantity + pickup type/location + fixed random-drop
outcome), `playerResourceTimers` (block charges, regen/lockout, ability + Echo Step cooldowns, Rally
uncredited meter, **Time Freeze remaining cooldown**, D02d Wardenclyffe recharge delay),
`recoveryEvent`, `sealReadiness`, `completionTransaction`, `appliedScriptEffectIDs`,
`pendingGlobalSeenIDs`, `presentationFlags`.

Repo equivalent (`scripts/Core/StoryManager.cs` 328–409 + the v5 save fields):
`_activatedCheckpoints`, `_destroyedExtractors`, `_foundSecrets`, `_fontUsesConsumed`,
`TimelineIntegrityPercent`, `HasSeenCollapseBeat`, written by `WriteAttemptStateToSave` and read by
`RestoreAttemptStateFromSave`. Attempt identity is inferred (`"active save parked on this scene with a
non-empty LastCheckpointID"`), not stored.

- Status: **PART** — roughly 6 of 21 records exist, all as loose root fields with no attempt ID, no
  status machine, no revision, no reward/recovery/healing/completion ledgers.
- Change list: new `scripts/Core/StoryAttemptState.cs` (+ `StoryAttemptStatus` enum); move the six loose
  fields into it during the v5→v6 migration (they are the trustworthy existing records F10 says to derive
  from); extend `StoryManager` to own it; route every commit through it.
- Size: **L**. Shared: `StoryManager.cs`, `SaveManager.cs`, `StoryLevelControllerBase.cs`,
  `ChronalRewindManager.cs`, `PauseMenu.cs`, the dust-economy workstream (`rewardSources`), the
  Integrity workstream (`currentIntegrity`/`startingExtractorCount`/`preBossLocked`).

### 4.4 Global save schema — required new fields

| Master field | Repo today | Status |
|---|---|---|
| `seenDialogueIDs : HashSet<string>` | per-slot `StorySaveData.ViewedDialogueIDs` | MISS at global scope (§4.1) |
| `reducedTemporalEffects : bool` (C01a, default Off, versioned, missing legacy value defaults Off **without** inferring from Screen Shake) | **absent** | MISS (§7.1) |
| C01c direct binds + per-device shortcut On/Off flags + **explicit Unbound** | `InputBindings : InputBindingSet` — cannot express Unbound (headline #4), no shortcut flags | PART (§7.3) |
| `unlockedCharacters` / `unlockedStages` / `totalPlayTime` / `totalWins` / `totalLosses` | present (+ repo extras `TotalDraws`, `CharacterWins/Losses`, `LastMatchSettings`) | IMPL |

### 4.5 Autosave triggers, ordering, durability, and the crash fee

Design (master 2101–2110 + STORY_PERSISTENCE §"Save ordering and crash limits"):
- Level completion commits reward/bonus/deposit/final Integrity/completion marker/unlocks/destination
  **once**, before results/dialogue/credits; an already-claimed boss reward cannot be issued twice; an
  uncollected physical reward remains pending.
- Checkpoint activation commits anchor + Integrity + baseline encounter record + Mending + difficulty
  rewind refresh **together**, once.
- **Additional triggers:** resource expenditure, Defy, death recovery / Snap / Smothered,
  pickup/healing consumption, Extractor/secret/puzzle changes, grid purchase/respec, voluntary exit,
  completion — **as coupled events**.
- Continuous resource/timer state saves **at least once per live second**; explicit Save/exit flushes.
- Capture immutable state on the game thread, then **one ordered asynchronous writer per slot with
  revision checks**; ignore obsolete revisions; on write failure retain the prior valid revision, report
  the failure and **prevent a transition from pretending it saved**.
- **Crash recovery charges no fee.**

Repo:
- Triggers wired: `OnCheckpointCommitted → SaveCheckpoint`, `OnLevelComplete → SaveLevelCompletion`,
  `OnTalentNodeUnlocked → SaveTalentUnlock` (`SaveManager._Ready` 371–374). That is **three** of the
  design's dozen.
- Writes are **synchronous** (`SaveStorySlot` → envelope → `File` write; no `Task.Run`, no writer queue,
  no `revision` field, no failure gate on scene transitions).
- No per-second continuous snapshot — between checkpoints, HP/meter/lives/cooldowns are not durable.
- **`SessionExitGuard` + `SaveManager.ApplyAbnormalExitFeeIfMarked` charge the 20% fee on crash**
  (the marker survives a power loss; the boot check bills the slot and shows
  `save_notice_abnormal_exit_fee`). **CONFLICT with F10.**
- No settled-event ID, so reopening an already-settled failure/results screen is not protected against a
  repeat fee.

- Status: **PART / CONFLICT**.
- Change list:
  - Add `attemptState.revision` + an ordered per-slot async writer (`Task`-chained queue), obsolete-revision
    rejection, prior-revision retention on failure, and a `SaveFailed` notice key.
  - Add a per-second continuous snapshot of `playerResourceTimers` + HP/meter.
  - Wire the missing triggers (list above), each as one coupled transaction.
  - Split the exit marker into **deliberate exit** (fee) vs **abnormal termination** (no fee): keep the
    marker for resume routing, drop `ApplyAbnormalExitFee` from the boot path, and charge the fee only in
    `PauseMenu`'s explicit exit / voluntary-exit transaction, keyed by a settled-failure event ID.
  - `tests/unit/SessionExitGuardTests.cs` will need rewriting (it currently pins the crash fee).
- Size: **L**. Shared: `SaveManager.cs`, `SaveEnvelope.cs`, `SessionExitGuard.cs`, `PauseMenu.cs`,
  `StoryManager.cs`, `localization/en.csv`.

### 4.6 Checkpoint system, respawn and load routing

Design: an `Area2D` may detect proximity but **does not replace strike-to-activate**; the authored entry
checkpoint self-activates once on fresh entry; re-touch/reload grants no second benefit; an inactive Hard
checkpoint grants none. Ordinary loading reconstructs at the last activated checkpoint (implicit entrance
if none) with **latest durable** resources; a live death rewind uses its recorded grounded-history
landing; a committed pending recovery resumes once; **Smothered loads Game Over**;
**AwaitingHubResume loads the hub in Acts I–II**; a completion transaction routes forward; none of these
loads triggers fresh-entry resource initialization.

Repo: strike-to-activate is **IMPL** (V7.3 `CheckpointTrigger` + `StrikeSurface`,
`StoryManager.TryActivateCheckpoint` once-per-attempt, two-beat
`OnCheckpointReached`/`OnCheckpointActivated` then `OnCheckpointCommitted`, `SelfActivating` entry
checkpoint). Load routing is **MISS**: `StoryManager` has only `HasPendingTimelineRestart`,
`CollapsedLevel`, `CollapsedCheckpointID`; there is no status machine, no Smothered, no
CompletionPending, no RecoveryPending resume.

- Status: checkpoints **IMPL**; routing **MISS**.
- Change list: drive load destination from `attemptState.status`; add the Game Over surface for Smothered.
- Size: **M** (routing) + whatever Act III costs (§6.4). Shared: `StoryManager.cs`, `GameManager.cs`,
  `scripts/UI/TimelineRestartPanel.cs`, `MainMenu.cs`.

### 4.7 Migration (F10 / F08 / F03 / F21)

Design: versioned converters validated against trustworthy existing records; **never** default an active
legacy attempt to full anchors / unused Defy / unused healing / unclaimed rewards; if essential history
cannot be reconstructed, **retain the original save and balances and mark `LegacyRecoveryRequired`**,
telling the player an explicit Restart Level is needed; apply F08's affected-grid refund migration once;
assign the actual version number after inspecting shipped formats.

Repo: `SaveSchemaMigrator` is a single shared `CurrentVersion = 5` for **both** payloads; story steps are
`<2` legacy and `<3` puzzle; global step is `<4` input bindings; v4→v5 was purely additive (no step).
`RejectFutureVersion` matches the design's "newer save rejected" rule (**IMPL**), but the rejection
message is thrown as `SaveVersionException` — confirm the UI surfaces the exact designed copy.

- Status: **PART**.
- Change list — one **v5 → v6** bump covering:
  1. story: create `attemptState` from the six loose v5 fields; `checkpointIntegrity` seeded from
     `LevelIntegrityPercent` (no invention); `unlockedLegacyAbilities` derived from `completedLevels` per
     the Legacy Unlock Schedule, else `LegacyRecoveryRequired`; `levelIntegrity` renamed/re-scoped from
     `IntegrityByLevel` with the PreBoss caveat recorded;
  2. global: `seenDialogueIDs` = union of every slot's `ViewedDialogueIDs`; `reducedTemporalEffects`
     = false; C01c shortcut flags default On (reproducing today's chords); `gameplay_rewind` override
     migrated to `gameplay_time_freeze` **without** overriding an explicit newer Time Freeze bind (F03);
     `SavedMatchSettings.Mode == 2` (Hybrid) normalized to Stock with a valid positive timer (F21, §6.6);
  3. `LegacyRecoveryRequired` path + notice key.
  Because `CurrentVersion` is shared, a global-only bump also bumps story payloads — decide explicitly
  whether to split the two version counters (recommended) or accept the coupled bump.
- Size: **L**. Shared: `SaveEnvelope.cs`, `SaveManager.cs`, `tests/unit/SaveEnvelopeTests.cs`,
  `tests/unit/InputBindingSchemaTests.cs`, `tests/unit/MatchSettingsTests.cs`.

---

## 5. Fighter Mode Netcode / Online Infrastructure (M01)

### 5.1 Launch delivery: Local Versus + Steam Remote Play Together

Design (M01 Option A): on supported Steam builds the Fighter menu offers **Local Versus** and a clearly
labelled **Steam Remote Play Together** entry that shows short guidance and then enters the same local
flow. **Hide** the Steam entry when Steam integration is unavailable; show an honest unavailable message
if it fails after opening; **never** show disabled Native Online / Coming Later / LAN / matchmaking /
ping-browser entries. Either assigned fighter can invoke the existing local pause on the host. Controller,
disconnect and response validation are release gates.

Repo: `MainMenu.cs` 139–152 — the LAN button was removed in V7.3 and any persisted
`FighterOpponentType.Lan` session is coerced back to `Cpu`. There is **no** Remote Play Together entry, no
Steam availability probe, no Steam SDK dependency in `FightersThroughTime.csproj`.
`scripts/UI/NetworkSelectScreen.cs`, `scenes/menus/NetworkSelect.tscn` and `scripts/Networking/` remain in
tree, gated, as Package 7's starting point — which matches the design's instruction to keep them.
`FighterOpponentType.Lan` still exists in `GameManager.cs` (harmless; the coercion covers it).

- Status: Local Versus **IMPL**; Remote Play Together **MISS**; "no disabled online entries" **IMPL**.
- Change list: a Fighter-menu branch with `menu_fighter_local_versus` and
  `menu_fighter_remote_play_together` + a guidance panel (`remote_play_*` keys); a Steam availability
  check (currently no Steam integration at all — this may be a dependency decision, not just UI);
  `MainMenuSceneTests` additions pinning the hidden-when-unavailable behaviour.
- Size: **M** (UI) / **L** if a Steam SDK integration is in scope. Shared: `MainMenu.cs`,
  `CharacterSelectScreen.cs`, `localization/en.csv`, `FightersThroughTime.csproj`.

### 5.2 Package 7 addendum items

| Requirement | Repo | Status |
|---|---|---|
| Match-start barrier (shared start tick ack) | none | MISS |
| Desync → visible halt, rematch/disconnect | diagnostics only (`DesyncCount`) | MISS |
| Input redundancy N ≥ 4 frames per packet | `RollbackInputPacket` carries exactly **one** `PlayerInputFrame` (47 B, protocol v2) | MISS |
| Fail-closed identity check: protocol version **and build/content hash** | protocol version checked in `TryDeserialize`; **no content hash** | PART |
| Synced pause (serialized input on a common tick, unpause countdown, per-player budget); local `SceneTree.Paused` forbidden in a session | `LocalFighterPause` writes `SceneTree.Paused` via `PauseMenuBase` | MISS (correct for local; structurally incompatible with a future session) |
| Per-match seed, rematches reroll | `FighterMatchComponent.WorldSeed` + `FighterMatchSeedTests` | IMPL |
| Negotiated input delay 0–3 frames | no field in the protocol | MISS |
| Transport decision: Steam Networking Sockets + relay fallback | `UdpRollbackTransport` (direct IP) only | MISS |
| "Delay-based-**only** native netcode is not acceptable" (wording relaxed) | n/a | docs |

- Size: **L**, all Package 7. Shared: `scripts/Networking/*`, `scripts/FighterSim/FighterSimulation.cs`.

---

## 6. Lives & Stock Systems

### 6.1 Death-rewind landing anchor (master 2029–2032)

Design: search the 480-frame history **oldest → newest**; validate **full-body clearance, support and
kill bounds against the landing world, including reversed moving-platform state**; **recheck before
release**; skip invalid samples; then validate last-known grounded → activated checkpoint → implicit
entrance in that order. Fallback never spends another charge, refreshes checkpoint benefits or restores
puzzle progress. Safe zero-distance recovery is permitted. If **every** anchor is invalid, keep the
recovery **pending** and report a configuration error rather than placing the player in danger.

Repo: `ChronalRewindBuffer` + `ChronalRewindManager` land on the deepest grounded frame, with a
last-known-grounded fallback and then the physical checkpoint. **No clearance/support/kill-bounds
validation, no re-check before release, no pending-recovery error state.** 480-frame capacity, 45-frame
pre-hold, half-duration playback, `PathIndexForTick` pacing — all **IMPL** and unchanged by this revision.

- Status: **PART**.
- Change list: an anchor validator (shape-cast against the landing world + moving-platform reversed
  state), a `RecoveryPending` status + configuration-error notice, and tests in
  `tests/unit/ChronalRewindTests.cs` / `DeathTriggeredRewindTests.cs`.
- Size: **M**. Shared: `ChronalRewindManager.cs`, `ChronalRewindBuffer.cs`, `StoryManager.cs`.

### 6.2 Time Freeze replaces Manual Rewind + Stasis Echo (master 2043–2052) — the largest Story change

Design: Story-only, single-player, from Level 0, every difficulty, even at zero rewind charges.
Freezes everything except player/camera/UI for **exactly 5 s**. Input: a dedicated
**`gameplay_time_freeze`** action — press **R** / **Back or Select** once; no hold/scrub/commit; migrate
any customized `gameplay_rewind` binding to it. Unavailable during hitstun, daze, grabs, attack/ability
execution, death or scripted presentations; **Suppression does not lock it**. Running/jumping/roll stay
available; attacks, grabs, specials, ultimates, Echo Step and movement abilities are disabled until it
ends. Freeze is **camera-room scoped** with adjacent-room simulation suspended and newly revealed actors
frozen before acting; ordinary room crossings preserve the remaining duration. All actors resume preserved
positions/velocities/attack phases/timers at expiry — no catch-up ticks, no projectile clearing, no
accumulated damage, no delayed input burst. **Enemies and bosses are invulnerable throughout**, including
against existing projectiles/constructs/zones/DoT; attacks attempted during the freeze are **discarded**.
No damage, stagger, Rally reclaim, meter, healing, pickups, checkpoint activation, puzzle interaction or
objective credit during the effect. Passive recovery, other cooldowns and status timers are **suspended**;
movement and the freeze clock continue; the 5 s pauses in the pause menu. **No charges, no meter cost;
45-second cooldown starting when the freeze ends (including an early end)**, advancing only during live
Story play, pausing in menus/dialogue/death-rewind presentations, not refreshable by checkpoint, death
rewind, Anchor Snap or reloading the same session; fresh entry / full Restart starts ready; the remaining
cooldown is **saved** (`attemptState.playerResourceTimers`). Background saves during an active freeze store
a **45 s reload cooldown** while the live freeze continues; an explicit Save/exit **ends** the freeze and
stores 45 s; reload never resumes or renews the effect. **Timeline Integrity keeps draining** during the
freeze at the current normalized rate; never restored on activation/expiry/load. `PathMovingPlatform`
rewinds during death rewind but only **freezes in place** during Time Freeze. **Stasis Echo and Manual
Rewind are retired**; authored Echo gates must be reauthored with ordinary latched switches (never a
mandatory Time Freeze gate). Level 0 teaches the scripted death rewind, then a separate five-second escape
drill with invulnerable enemies and a safe destination, with a ready freeze on each retry. Presentation:
a **separate** HUD icon with Ready / 5-s countdown / cooldown countdown states + a brief thaw warning;
never attached to the death-rewind counter. Scene animations and action audio stop; player movement and a
subdued time-stop cue stay audible.

Repo (`scripts/Environment/ChronalRewindManager.cs`): `ManualHoldSeconds = 0.5f`,
`ScrubFramesPerTick = 4`, `ManualRewindCooldownSeconds = 12f`, `StasisEchoSeconds = 10f`,
`IsManualRewindFree` (Easy/scripted), `BeginScrub`/`AdvanceScrub`/`CancelScrub`/`CommitScrub`,
`ScrubPreviewPosition`, `IRewindScrubbable` platform scrubbing, `_spawnEchoOnComplete`,
`scripts/Environment/StasisEcho.cs` (Environment|PersistentObject body layer, pressure plates,
`SearchlightZone` occlusion, one-way platform). Input action `gameplay_rewind` in
`InputManager.Actions.Rewind` + `RemappableActions`. en.csv `controls_action_rewind,Chronal Rewind`.
Tests: `ManualRewindScrubTests`, `StasisEchoPhysicsTests`, `ScriptedRewindTests`,
`TutorialCalibrationTests`.

- Status: **RET (manual rewind + Stasis Echo)** / **MISS (Time Freeze)**.
- Change list:
  - `project.godot` InputMap: rename `gameplay_rewind` → `gameplay_time_freeze` (R + Back/Select);
    `InputManager.Actions.TimeFreeze`; keep `Rewind` as a deprecated alias only long enough for the save
    migration (§4.7), then remove from `RemappableActions`.
  - New `scripts/Environment/TimeFreezeManager.cs` (or a Time Freeze mode on `ChronalRewindManager`):
    5 s clock, 45 s cooldown armed **at thaw**, room-scoped freeze set, invulnerability flag on every
    frozen actor, attack-discard gate, suspended cooldown/status timers, Integrity drain *not* suspended
    (note: the existing extractor freeze sweep pauses drain during a rewind — Time Freeze must **not**
    reuse that path).
  - Delete/retire: the scrub state machine, `StasisEcho.cs`, `IRewindScrubbable` scrub preview, the 12 s
    manual cooldown, `IsManualRewindFree`, and the Echo-gate authoring in any level that uses it
    (`grep StasisEcho` across `scenes/campaign/` before deleting).
  - HUD: separate Time Freeze icon (`scripts/UI/StoryHUD.cs`) with three states + thaw warning; remove
    the manual-rewind cooldown pip from the rewind counter.
  - Save: `attemptState.playerResourceTimers.timeFreezeCooldownRemaining`, with the
    "active freeze → store 45 s" rule.
  - Tutorial: replace the manual-scrub calibration step with the escape drill
    (`scripts/Environment/Level00Controller.cs` + `TutorialCalibrationTests`).
  - en.csv: `controls_action_time_freeze`, `hud_time_freeze_ready` / `_active` / `_cooldown`,
    `time_freeze_thaw_warning`, the drill copy; retire `controls_action_rewind` per the recorded-orphan
    process (`UnusedTranslationKeyTests`).
  - `PathMovingPlatform`: freeze-in-place branch distinct from its rewind-with-the-player branch.
  - Audio: a C01b "Time Freeze" background profile (§7.4) that suspends frozen actors' action audio.
- Size: **L** (the biggest single item in this dossier). Shared: `project.godot`, `InputManager.cs`,
  `StoryHUD.cs`, `StoryManager.cs`, `SaveManager.cs`, `localization/en.csv`, every campaign scene that
  authored an Echo gate, `AudioSnapshotMixer.cs`.

### 6.3 Timeline Collapse trigger conditions (master 2053)

Design: collapse fires **only** when (a) a lethal event is not prevented by Defy History **and** no
death-rewind charge is available, or (b) **Timeline Integrity reaches zero** (every difficulty).
Spending the final charge 1 → 0 **completes** the rewind; being alive at zero charges never collapses.
Time Freeze cannot bypass an Integrity-zero collapse. Presentation gains an era-lost Sarah variant
("*The era's slipping away — pulling you out!*") for the timer-caused collapse, and the V7.6 framing of
the beat as an interrupted *Smothered* sequence.

Repo: `ChronalRewindManager.OnPlayerDied` → `if (RemainingRewinds <= 0) CollapseTimeline(); else
BeginRewind();` — condition (a) **IMPL** (and the 1→0 case works, since the charge is spent inside
`BeginRewind`). Condition (b) **MISS**: `grep` finds no Integrity-zero collapse trigger anywhere;
`TimelineIntegrityRules.EndingThresholdPercent` is authored but unconsumed.
`CollapseBeatSeconds = 4f`, `HasSeenCollapseBeat`, skip-after-first — **IMPL**.

- Status: **PART**.
- Change list: an Integrity-zero watcher in `StoryManager.DrainTimelineIntegrityAmount` raising collapse
  with a `TimerCaused` cause; a second Sarah line key
  (`dialogue_collapse_sarah_era_lost`); `TimelineCollapseBeatTests` extension.
- Size: **S–M**. Shared: `StoryManager.cs`, `ChronalRewindManager.cs`, `localization/en.csv`.

### 6.4 Collapse recovery, Act III Anchor Snap, Smothered

Design: Acts I–II → hub with status **AwaitingHubResume**, same attempt, paid recovery (not a fresh
attempt); the hub portal resumes at the last checkpoint with the already-committed recovery; full Restart
Level is the separate explicit option. Enemy persistence rebuilds from the checkpoint's **authored
baseline encounter IDs**, then overlays broken Extractors / secrets / solved gates / spent healing /
reward claims; encounters beyond the baseline may respawn but pay no claimed reward again; do **not**
infer baseline membership from enemy position at death. Player health on **paid** recovery only: full HP +
full difficulty rewind pool committed once, retaining meter, Defy-used, healing uses, anchors and
cooldowns; ordinary quit/crash load retains latest durable HP and rewind count instead.
Integrity: paid non-timer recovery restores `checkpointIntegrity`; a granted timer recovery uses
`max(checkpointIntegrity, recoveryMinimum)` with ≥ 25%. **Act III (Levels 13–15)**: no extraction — a
collapse becomes an **Anchor Snap** (respawn at the last activated checkpoint *in place*, same fee, same
resets, spending one Beacon **anchor charge**: Easy 3 / Normal 2 / Hard 1 per level), or with no charge
left the **Smothered** Game Over (Restart Level or Quit to Menu). Dust: `floor(levelChronalDust * 0.20)`
subtracted **once** for the uniquely identified failure or live-play exit; reopening a settled screen does
not repeat it; claims stay spent even when dust is lost.

Repo: `StoryManager.BeginTimelineCollapse`, `HasPendingTimelineRestart`, `CollapsedLevel/CheckpointID`,
`RestartCollapsedLevel(bool resumeFromTimelineAnchor)`, `ApplyTimelineCollapseDustPenalty()`,
`CalculateTimelineCollapseDust` (80% retained), `scripts/UI/TimelineRestartPanel.cs`. Enemy persistence is
whatever the level controller rebuilds — **no authored baseline encounter IDs**. No Anchor Snap, no
anchor charges, no Smothered, no `recoveryEvent`, no `checkpointIntegrity`, no once-only fee ledger.

- Status: Acts I–II shape **PART**; Act III **MISS** entirely.
- Change list: `attemptState.recoveryEvent` (unique ID, cause, resolved HP/rewind/anchor/wallet/Integrity
  outcome, F11 checkpoint/role/difficulty/budget version/resolved minimum) resolved **before** the
  animation and finished once on resume; `anchorChargesRemaining`; a Smothered Game Over screen +
  routing; `checkpointIntegrity`; `checkpointRecord.baselineEncounterIDs` authored per checkpoint (a
  content task across Levels 2–15); once-only fee keyed by failure event ID.
- Size: **L**. Shared: `StoryManager.cs`, `SaveManager.cs`, `TimelineRestartPanel.cs`,
  `StoryLevelControllerBase.cs` + every campaign level scene (baseline encounter IDs), the Act III
  narrative workstream.

### 6.5 Healing-loop persistence deltas (master 2061–2071)

The V7.2 healing numbers are unchanged. New F10 persistence rules:
- **Checkpoint Mending:** commit the checkpoint's **granted-benefit ID** with the HP/rewind changes;
  loading or paid recovery never grants it again. Repo: `TryActivateCheckpoint` gates once-per-attempt
  (**IMPL in spirit**) but stores only activated IDs, not granted-benefit IDs — **PART**.
- **Restoration Font:** channel is **range-anchored** (already V7.3, `ChannelRangePixels = 90`, **IMPL**);
  new: persist the use decrement **and the pending heal** together at channel completion; ordinary reload
  resumes only **uncredited** healing; a death recovery that replaces HP **cancels pending healing without
  refunding** the use. Repo: `FontUsesConsumed` dictionary only, no `pendingHealing` record — **PART**.
  Also new F12 4A exception: the 4A variant's single Font sits on the late approach after the Eraser
  encounter, before PreBoss, and grants no checkpoint (content task).
- **Chronal Feast / Salve:** persist the **pickup ID** and HP benefit together; **issued random-drop
  outcomes are retained across reload rather than rerolled**. Repo: no `collectedHealingIDs`, no fixed
  drop outcome — **MISS**.
- Size: **M**. Shared: `StoryManager.cs`, `scripts/Environment/RestorationFont.cs`, the drop system,
  `tests/unit/StoryHealingLoopTests.cs`, `StoryDropsAndRewindTests.cs`.

### 6.6 Fighter Mode: modes, stocks-lost, Sudden Death (F21/F22)

**Mode consolidation (F21 Option A).** Only **Stock** and **Time** are selectable. Retire Hybrid; keep
`Stock = 0`, `TimeLimit = 1` **explicit**; **reserve 2 for legacy decoding**, never reuse it, never list it.
Migrate recognized saved Hybrid to **timed Stock** preserving a valid timer and stock count; a Hybrid
timer that is Off/missing/nonfinite becomes 480 s, shown repaired before launch; invalid/missing stock
count → 1–5 range, default 3; unknown mode values are **not** Hybrid (preserve the source and require a
valid selection). Normalization is idempotent and runs before displaying loaded settings or creating the
next match. Label the second choice **Time** while retaining the internal `TimeLimit` identifier.
In `MatchSettings`, `TimeLimit` seconds: **0 means Off in Stock only**; Time requires a positive timer and
**Off is unavailable**; switching untimed Stock → Time selects 480 s and shows it before launch; keep
characters/stage/items/hazards unchanged when switching.

Repo: `MatchMode { Stock, TimeLimit, Hybrid }` (`GameManager.cs:12`) with **implicit** values;
`CharacterSelectScreen.cs:620–622` and `HolodeckConsolePanel.cs:100–102` both add the three items;
`FighterAudioRules.cs:20` treats Stock **or Hybrid** as stock-based; `FighterSimulationSystems.cs:2280–2284`
has a `_ =>` default arm that silently handles Hybrid; `SavedMatchSettings.Mode` is a raw int in the global
payload; `MatchSettings.GetDefault()` is Stock/3/480/Medium/Medium. en.csv has `fighter_mode_hybrid`.

- Status: **CONFLICT** (Hybrid is live and selectable).
- Change list: make the enum values explicit with `Hybrid = 2` marked `[Obsolete]`/reserved; remove both
  menu items; normalize on load in `SaveManager`/`GameManager` + before match launch; fix
  `FighterAudioRules`; replace the `_ =>` arm with an explicit Stock fallback; retire
  `fighter_mode_hybrid` via the recorded-orphan process; update
  `tests/unit/MatchSettingsTests.cs`, `CharacterSelectSceneTests`, `HolodeckConsoleTests`,
  `FighterAudioRulesTests`.
- Size: **M**. Shared: `GameManager.cs`, `CharacterSelectScreen.cs`, `HolodeckConsolePanel.cs`,
  `SaveManager.cs`, `localization/en.csv`.

**Time mode = fewest stocks lost (F21).** Each player gets a nonnegative `stocksLost` counter starting
at 0; **+1 on every actual stock loss regardless of cause** (opponent damage, pit, hazard, self-KO, DoT,
surviving projectile/construct). No attacker credit, ownership window or self-KO penalty. A hit prevented
by Defy or invulnerability is not a loss; co-occurring lethal damage + blast-zone crossing is **one**
increment. Applied atomically with the living→KO transition, before respawn; duplicate callbacks cannot
repeat it. The counter is **rollback snapshot/hash state**; resimulation replaces it rather than adding to
an external counter. Respawning never resets it; a new match/rematch starts at 0. Resolve all KOs in a
regulation tick before comparing; the final active tick settles both players' losses first. **Fewest wins;
equal totals (including 0–0) enter Sudden Death with no HP tiebreak.** HUD replaces finite stock icons
with a localized **"Stocks lost: N"** under each HP bar; results show the final totals; never label it
KOs scored / points / remaining lives.

Repo: `FighterMatchComponent.PlayerOneKOs` / `PlayerTwoKOs` count **KOs scored by** that player
(`TrackKnockouts` at 2380–2400 credits the *opponent*), and
`FighterMatchSystem` picks `PlayerOneKOs > PlayerTwoKOs ? 0 : 1` — i.e. **most KOs scored wins**, the
opposite bookkeeping. `FighterRuntimeComponent.KnockoutsSuffered` is the per-victim count and is already
snapshot state — the right quantity is available, only the comparison and HUD are wrong.
`UsesStocks = MatchMode == TimeLimit ? 0 : 1` already gives unlimited stocks in Time.

- Status: **PART** — semantically inverted but a cheap fix because `KnockoutsSuffered` exists.
- Change list: compare `KnockoutsSuffered` (victim-side) instead of the scored counters, or rename the
  match fields to `PlayerOneStocksLost`/`PlayerTwoStocksLost` and freeze them at regulation end; forbid an
  Off timer in Time; HUD `fighter_hud_stocks_lost,Stocks lost: {0}` + results line; remove the HP
  tiebreak in Time; update `FighterMatchFlowTests`, `FighterMatchStatisticsTests`, `FighterHudModelTests`.
- Size: **M**. Shared: `FighterSimulationSystems.cs`, `FighterSimulationComponents.cs`,
  `FighterHudModel.cs`, `MatchResults.cs`, `localization/en.csv`.

**Sudden Death retained-HP (F22 Option A).** The old 1-HP / first-hit rule is **retired**. Living fighters
keep their exact current positive HP (including unequal HP in a Time tie) — no heal, normalization or
clamp to 1. A fighter already dead or in the normal respawn sequence gets **normal Fighter respawn HP**,
resolved once, with no extra stock loss, no change to frozen regulation totals and no new spawn
protection. Everything else is a fresh reset: spawn points + zero velocity + released ledge/grab;
attacks/grabs/throws/hitstun/daze/hitstop/armed Echo Step cancelled with empty input buffers; **meter 0**
and all cooldowns ready; block charges refilled, shatter lockout/shieldstun/regen cleared; jumps and ledge
regrab reset; a **new Echo Step history generation** at the spawn with one valid sample and 30 ticks
before use; both status slots, marks, Root/tethers, timed orb buffs and Aegis cleared; Rally echo pool and
pending echo-meter accounting cleared, no Overtime multiplier; **Defy disabled throughout the phase but
its spent flag preserved** and the HUD seal shows unavailable (no new use consumed on entry); every
regulation projectile/zone/summon/construct/temporary platform removed with their pending events; arena
restored to authored round-start state and **the match's hazard toggle respected** (Off → no hazards;
On → first full warning after play resumes, twice-normal cadence by halving idle/recovery while retaining
warning and active durations, never multiplied again by regulation Overtime); orbs removed and disabled;
prior respawn platforms/invulnerability cleared; no timer, one decisive life each; **frozen regulation
stocks-lost totals** preserved for results; CPU plans/opportunity tracking reset without changing
difficulty; the match PRNG state continues. Ready countdown shows "Sudden Death — next death loses".
Ending: after each active tick resolve all real deaths; one dead fighter loses; two in the same tick is the
existing Draw; no respawn of a Sudden Death victim; commit the phase transition and result atomically in
snapshots/hashes with phase/life generations and result identity.

Repo `EnterSuddenDeath` (`FighterSimulationSystems.cs:2309–2360`): sets `CurrentHP = 1` for both,
`TimerEnabled = 0`, clears orbs and **forces hazards on** (`HazardsEnabled = 1`, frequency 3 if 0) even
when house rules had them off, `DefyHistoryUsed = 1` (spent-flag semantics, not a separate disable),
resets position/hitstun/daze/respawn/invuln/echo/hitstop/tumble/echo-step-windup/shieldstun/block-lockout/
ledge counters, grants a phantom stock at 0. Does **not** reset meter to 0, does not refill block charges,
does not clear status slots, does not remove projectiles/constructs/zones, does not restore the arena,
does not freeze regulation totals, does not reset CPU plans, has no phase generation.

- Status: **PART / CONFLICT** (1-HP clamp and forced hazards are both now wrong).
- Change list: rewrite `EnterSuddenDeath` against the F22 table; add a phase-generation int (the one spare
  `FighterVerbComponent` int, or better the new 312 component); freeze regulation totals on the match
  component; add the dead-entry respawn-HP branch; reset CPU controller state (blocked on headline #6);
  `FighterMatchFlowTests` rewrites.
- Size: **M–L**. Shared: `FighterSimulationSystems.cs`, `FighterCpuController.cs`,
  `tests/Determinism/FighterMatchFlowTests.cs`.

### 6.7 Unified Difficulty Scaling Table deltas (master 2227–2243)

| Row | Design now | Repo |
|---|---|---|
| Manual rewind cost | **row deleted** | `ChronalRewindManager.IsManualRewindFree` | 
| Time Freeze (all difficulties) | 5 s / no charges / 45 s cooldown | MISS (§6.2) |
| Siphon Clock drain rate 0.1/0.1/0.2 %/s per Extractor | **replaced** by "Timeline Integrity all-Extractors-alive **time budget**: 2.0× / 1.5× / 1.2× entry-to-boss-gate par" (normalized drain) | `TimelineIntegrityRules` + per-extractor share cap/grace — old model |
| Checkpoint Rewind Refresh | full (5) / +1 (cap 3) / none | `ChronalRewindManager.ApplyCheckpointRefresh` — **IMPL** |
| Middle Checkpoint | Active / Active / Inert (visibly fractured), **active in Act III** | not modelled |
| Anchor charges per Act III level | 3 / 2 / 1 | MISS |
| Rewind HP restore 70/50/30, Mending 100/50/25, Font 2×50 / 1×50 / 1×25, Feast 3×50 / 2×35 / 1×20, Salve 30/15/5 % & 50/25/10 HP | unchanged | **IMPL** |

The normalized-Integrity row is primarily the campaign/Integrity workstream's; flagged here because it
sits in my table and because Time Freeze must **not** pause that drain.

---

## 7. Accessibility & Controls

### 7.1 C01a — Reduced Temporal Effects

Design: one toggle in **Settings → Gameplay beside Screen Shake**, help text "Reduces distortion,
after-images and screen flashes. Keeps gameplay cues and timing unchanged." Default **Off**. Persist
`reducedTemporalEffects` in the **global** settings payload through versioned handling; a missing legacy
value defaults Off **without** overwriting an explicit value or inferring from Screen Shake. Applied
**before the first loading/portal effect**; covers Story, Fighter, training, menus/loading and
scripted/cinematic effects. Live application to current and new effects without restarting a level,
replaying a proc or resetting a timer; clear already-emitted decorative after-images when switched On;
switching Off resumes only the current event's remaining presentation. The reduced treatment table covers
chromatic aberration / scanlines / RGB split (incl. Relativity Rift, Death Rewind, portals), decorative
ghost trails (preserving the **fixed Echo Step destination indicator** and real decoys), full-screen
KO/Ultimate/rewind/transition flashes (replaced by a restrained stable overlay or smooth fade — never
another full-screen flash), heavy desaturation / pulsing vignette / flicker, armor/status/spawn/D04 Defy
feedback (steady glow + smooth expiry fade, keeping Fighter ownership outlines, **both status slots** and
the spent Defy seal), attack-class telegraphs (Basic/Guard-Crush/Unblockable glyphs and timing retained,
distinct Special-class projectile signature retained), and loading portals/cinematic transitions.
The preset **takes precedence over per-ability presentation requests** ("maximum chromatic aberration",
full-screen flash) for all nine kits, bosses and items; no Ultimate or cutscene bypasses it. Stored and
applied **locally, outside match snapshots/hashes**; it cannot influence hit eligibility, targeting, RNG or
timers; reconcile reduced visuals after rollback without regenerating suppressed effects. Graphics quality
settings must respect it rather than re-enabling an expensive pass. Screen Shake stays independent.

Repo: no such setting. `GlobalSaveData` has `ScreenShakeScale`, `HudOpacity`, `DamageNumbersVisible`,
`UiScale`. The rewind overlay (`RewindPresentationOverlay`) and `assets/shaders/outline_glow.gdshader`
have no reduced path. `SettingsMenu` Gameplay tab nodes are
`Gameplay/HapticToggle`, `HapticSlider`, `DamageNumbersToggle`, `HudOpacitySlider`, `ScreenShakeSlider`,
`UiScaleSlider` (scene `scenes/ui/Settings.tscn`).

- Status: **MISS**.
- Change list: `GlobalSaveData.ReducedTemporalEffects` (bool, default false) + `Normalize`; a
  `Gameplay/ReducedEffectsToggle` CheckButton + label in `Settings.tscn` and `SettingsMenu.cs` (apply/read
  at 480/500); a static `FTT.Core.ComfortSettings.ReducedTemporalEffects` read by
  `RewindPresentationOverlay`, `GlowPresentationController`, `VfxEmitter`, `LoadingScreen`,
  `FighterPresentationOverlay` and the Relativity Rift zone visual; applied by `ViewportEnforcer`/boot
  before the first transition; en.csv `settings_reduced_temporal_effects` + `_help`.
- Tests: `SettingsMenuSceneTests`, `DisplaySettingsPersistenceTests`, `RewindCuePresentationTests`,
  plus a rollback-invariance pin (same gameplay hash with the toggle On/Off).
- Size: **M** (plumbing) + **M** per-effect art work. Shared: `SaveManager.cs`, `SettingsMenu.cs`,
  `scenes/ui/Settings.tscn`, `localization/en.csv`, the VFX workstream.

### 7.2 C01b — One prioritized background audio treatment

Design: a **single mix controller**; gameplay/presentation states *request* profiles and the **highest
active priority wins** (Paused > Death Rewind/Collapse/Anchor Snap > Ultimate cinematic > Defy/boss
phase/scripted > Time Freeze > Low Integrity/Collapse Tremor > Low health > Underwater > NormalGameplay).
Individual scripts must not multiply bus gain, append filters or restore cached old volumes. Apply the
winner's gain/filter/pitch **once** relative to the user's baseline (Ultimate over LowHealth is the
Ultimate treatment alone; a prior 12 dB rewind duck cannot make it 24 dB); blend with authored envelopes;
re-evaluate active requests when a profile ends rather than blindly restoring Normal; rapid transitions
must not accumulate. Separate **Music/World SFX** from a **Critical Cues** path (attack-class/hazard
warnings, impending platform danger, freeze/thaw and recovery/phase cues, timer danger cues, confirmed
result announcements) that bypasses background low-pass/pitch/pan/ducking while still respecting Master
and the cue's own category volume/mute. Shared local play uses one global background selection (two
low-health fighters cannot double the duck). Reverb: select/blend one environmental send, never stack.
Derive requests from authoritative state, apply locally outside snapshots/hashes, reconcile on
load/scene-change/rollback without replaying stingers.

Repo: `scripts/Core/AudioSnapshotMixer.cs` — "Filter cutoffs stack by **minimum**… the most aggressive
active" and snapshots layer additively (pause/low-health/ultimate/rewind). `StemDirector` is additive by
design (ambient + combat + climax). No Critical Cues bus; `AudioBuses` is
Master → Music, SFX → {Combat, Movement, Environmental}, UI.

- Status: **CONFLICT** (stacking is exactly what C01b forbids) / Critical Cues **MISS**.
- Change list: replace the stacking mixer with a priority-select controller (request/release API +
  priority table + envelope blend + re-evaluate on release); add a **Critical Cues** bus under SFX in
  `resources/Audio/default_bus_layout.tres` and route warning/thaw/result cues to it; add the Time Freeze
  profile (§6.2) and Anchor Snap/Collapse profiles; rewrite `AudioSnapshotMixerTests` and
  `AudioSnapshotTriggerTests`.
- Size: **M–L**. Shared: `AudioManager.cs`, `AudioSnapshotMixer.cs`, `AudioBuses.cs`,
  `resources/Audio/default_bus_layout.tres`.

### 7.3 C01c — Direct bindings + optional preset shortcuts

Design: the remapping screen lists **every** gameplay action — Left, Right, Up, Down, Jump, Roll, Block,
Basic Attack, Special 1, Special 2, Movement Ability, **Ultimate**, Interact, **Grab**, **Echo Step**,
**Time Freeze**, Pause — with **one editable direct-binding override per device kind** (keyboard/mouse
separate from gamepad). Defaults/shortcuts:

| Action | Direct default | Optional preset shortcut (On/Off per device kind) |
|---|---|---|
| Ultimate | Keyboard `U`; gamepad direct slot **Unbound** | Gamepad MovementAbility + Special2 (LB+RB at defaults), **on by default**; no new keyboard Ultimate chord |
| Grab | Direct slots **Unbound** (retain an existing explicit user bind) | Block held + BasicAttack press, on for keyboard and gamepad |
| Echo Step | Direct slots **Unbound** | Block + Roll under the existing timing/recovery rule, on for both |
| Time Freeze | `R` / Back-or-Select, preserving **migrated** user overrides | none; Story-only label |

Rules: a direct bind and an enabled shortcut coexist; disabling a shortcut leaves its components working
normally; assigning a direct bind must **not** auto-disable the shortcut. Direct input and a recognized
shortcut produce the **same semantic action request, deduplicated per actor/tick**; **never synthesize
component presses**; a refused direct action does nothing and spends nothing. Both routes enter the
existing legality/priority resolver, retaining chord precedence over component singles, legal Echo Step
over block-cancel in attack recovery, and the authored illegal-grab-chord fallback to a legal
block-cancel. A direct binding grants no extra priority/cancel/leniency/resource bypass. Capture remaps
with gameplay input suspended; apply atomically at a safe input boundary; clear stale buffered/held-edge
state and require fresh presses. Conflicts: reject duplicate direct bindings between simultaneously
available actions on the same device, naming the conflicting action; a preset's intentional sharing with
its components is **not** a duplicate error; never accept an ambiguous duplicate just because an action is
currently locked in the campaign. A device profile must retain a reachable route for required actions —
explain and keep the last valid configuration rather than leaving Ultimate/Grab/Echo Step unreachable.
Locked/not-yet-unlocked actions are shown for configuration without granting the ability. Glyphs
everywhere (HUD, tooltips, move lists, drills, Nexus prompts, interaction prompts); prefer the direct
binding in a single-action prompt, otherwise the enabled preset's full current chord; describe Echo Step
and Grab **by action name**, not hard-coded Block+Roll/Block+Attack text. Persist direct overrides,
**explicit Unbound choices** and shortcut enable flags in **versioned global settings** (not snapshots),
per-device independent; legacy missing flags reproduce the existing shortcuts; per-action reset restores
that action's direct default **and** preset flags for the selected device; global reset restores the whole
profile; validate the whole proposed profile before committing. **S01:** serialize the resulting *logical
action requests*, not local key codes; version protocol input capabilities when adding direct actions;
carry direct-vs-preset origin and the component candidates for the illegal-chord fallback; peers replay
normalized input and never re-recognize a remote chord with their own shortcut settings. **Add
`gameplay_echo_step` and its explicit logical intent; do not reuse the reserved Dash bit.**

Repo:
- `InputManager.RemappableActions` = MoveLeft, MoveRight, Jump, Down, Up, BasicAttack, Special1, Special2,
  MovementAbility, Block, Roll, Interact, **Rewind**, Pause (14). `ReadOnlyActions = { Ultimate }` — the
  LB+RB chord is surfaced **read-only** because "the per-event remap UI cannot express" a conjunction.
- **No `gameplay_grab`, no `gameplay_echo_step`, no `gameplay_time_freeze` actions at all.**
  Grab is chord-only (`FighterGrabRules.ChordPressed` at `FighterSimulationSystems.cs:1633/1638`,
  Story mirror in `PlayerController.ProcessGrabbing`); Echo Step is Block+Roll
  (`PlayerController.TryStartEchoStep` at 1350, sim `TryStartEchoStep` at 949).
- `GameplayButtons : ushort` uses bits 0–11 (11 = reserved `Dash`); **bits 12–15 are free**, so
  `EchoStep = 1 << 12` and `Grab = 1 << 13` fit without changing `PlayerInputFrame.SerializedSize` (12 B)
  or `RollbackInputPacket.SerializedSize` (47 B). A direct-vs-preset **origin** flag needs another bit
  (14) or a parallel byte — protocol v3 either way per C01c's "version protocol input capabilities".
- `InputBindingSet.Normalize()` **deletes empty rows** → explicit Unbound is unrepresentable
  (headline #4). No shortcut enable flags. `InputBindingConflicts.FindConflictingAction` blocks-not-swaps
  (**IMPL** and matches the design) but has no concept of a preset's legitimate component sharing and no
  unreachable-layout rejection.
- `InputBindingService.RestorableActions` is built from `RemappableActions`, so new actions must be added
  there or restores will be silently dropped.

- Status: **PART** (direct binds for 14 actions exist; the four new direct actions, Unbound, shortcut
  flags, dedup-once resolution and reachability validation are **MISS**).
- Change list:
  - `InputManager.Actions`: add `Grab`, `EchoStep`, `TimeFreeze`; move `Ultimate` from `ReadOnlyActions`
    into `RemappableActions`; remove `Rewind`.
  - `project.godot` InputMap: add the three actions (Ultimate keyboard `U` already exists as an action;
    give it an empty gamepad direct slot).
  - `GameplayButtons`: `EchoStep = 1 << 12`, `Grab = 1 << 13`; wire the sim's grab/Echo-Step initiation to
    `Pressed(Grab)` **OR** the existing chord, deduplicated once per tick, never synthesizing components.
  - `InputBindingSet`: an explicit `Unbound` representation (e.g. a sentinel event kind or a parallel
    `HashSet<string> UnboundActions`) that survives `Normalize()`; per-device shortcut flags
    (`Dictionary<string, bool>` keyed `action|deviceKind`).
  - `SettingsMenu` Controls tab: 17 rows, per-device-kind slots, shortcut On/Off toggles, per-action and
    global reset semantics, reachability validation with a localized explanation.
  - `MoveListScreen` / interaction prompts: prefer the direct bind, else the preset's live chord; describe
    by action name.
  - en.csv: `controls_action_grab`, `controls_action_echo_step`, `controls_action_time_freeze`,
    `controls_action_ultimate` (exists), `controls_shortcut_*`, `controls_unreachable_action`.
  - Protocol: bump to v3 when the origin flag lands (`RollbackProtocol.ProtocolVersion`).
- Tests: `InputBindingSchemaTests` (Unbound round-trip, shortcut flags, migration), `SettingsMenuSceneTests`,
  `MoveListScreenTests`, new dedup-once sim tests in `FighterGrabTests` / an Echo Step suite.
- Size: **L**. Shared: `project.godot`, `InputManager.cs`, `InputBindings.cs`, `SettingsMenu.cs`,
  `scenes/ui/Settings.tscn`, `SaveManager.cs`, `PlayerInputFrame.cs`, `FighterSimulationSystems.cs`,
  `RollbackProtocol.cs`, `localization/en.csv`.

### 7.4 Device-appropriate input glyphs

Design row unchanged ("required before any controller-first playtest"). Repo: `InputBindingService.Describe`
+ `DialogueManager.DescribeInteractBinding` produce live text labels, not device-correct **glyphs**
(`[E]` keycap / `Ⓑ` button face). Status: **PART**, size **M**, shared with the UI workstream.

---

## 8. Haptics / Performance / Camera Shake

The diff contains **no hunks** for the Haptic Feedback, Performance Optimization or Camera Shake
subsections. The only cross-cutting note is C01a's "Screen Shake remains independently controlled by its
existing slider; zero still disables shake" — which the repo already satisfies
(`GlobalSaveData.ScreenShakeScale`, `CameraShake` scaling, `SettingsMenu` slider).

Status: **IMPL / unchanged**. No work item.

---

## 9. Consolidated change list by file (my section only)

| File | Work |
|---|---|
| `scripts/Core/EventBus.cs` | append `StatusType.Suppression`; slot discriminator on `StatusEffectPayload` |
| `scripts/Combat/StatusController.cs` | `StatusSlot`/`StatusRouting`/`StatusEffectData`/`StatusSlots`, `IStatusEffectTarget`, stronger-wins, `SuppressionStrategy`, drop the survivor re-announce once two pips exist |
| `scripts/Enemies/EnemyController.cs`, `BossController.cs` | route via `StatusRouting`, stronger-wins, implement `IStatusEffectTarget` |
| `scripts/FighterSim/FighterEntitySystems.cs` | stronger-wins in fixed point; refuse Suppression |
| `scripts/FighterSim/FighterSimulationComponents.cs` | new components 312+ (D04 defense, Conductive marks, Echo Step ring bank, phase generation); **310 has only 4 spare bytes** |
| `scripts/FighterSim/FighterSimulationSystems.cs` | F21 stocks-lost comparison, F22 Sudden Death rewrite, Echo Step per-tick sampling + generation, D04 protection, event/contact ledgers |
| `scripts/FighterSim/FighterCpuController.cs` | move mutable state into snapshot storage; F22 plan reset |
| `scripts/FighterSim/FighterSimulationDriver.cs` | logical event IDs + presentation dedup; confirmed-result gate on durable writes |
| `scripts/Networking/RollbackProtocol.cs`, `NetworkManager.cs` | content hash in identity, fail-closed halt, input redundancy, start barrier, negotiated delay (all Package 7) |
| `scripts/UI/DialogueManager.cs` | global seen set, first-skip confirmation, idempotent effect resolution |
| `scripts/Core/SaveManager.cs` | `checkpointIntegrity`, `attemptState`, `levelIntegrity`, `unlockedLegacyAbilities`, global `SeenDialogueIDs` + `ReducedTemporalEffects` + C01c flags; ordered async writer + revisions; expanded triggers; per-second snapshot; completion before credits |
| `scripts/Core/SaveEnvelope.cs` | v5 → v6 migrator (+ consider splitting story/global version counters), `LegacyRecoveryRequired` |
| `scripts/Core/SessionExitGuard.cs` | separate deliberate exit (fee) from crash (no fee); settled-event ID |
| `scripts/Core/StoryManager.cs` | `StoryAttemptState` ownership, status machine, Integrity-zero collapse, anchor charges, recovery events |
| `scripts/Environment/ChronalRewindManager.cs` | anchor validation; retire manual scrub + 12 s cooldown; Time Freeze manager |
| `scripts/Environment/StasisEcho.cs` | **delete** (plus Echo-gate reauthoring in campaign scenes) |
| `scripts/Core/InputManager.cs`, `scripts/Core/InputBindings.cs`, `project.godot` | C01c actions, Unbound, shortcut flags |
| `scripts/Core/PlayerInputFrame.cs` | `EchoStep`/`Grab` bits (12/13) |
| `scripts/Core/GameManager.cs` | explicit `MatchMode` values, `Hybrid` reserved |
| `scripts/UI/CharacterSelectScreen.cs`, `HolodeckConsolePanel.cs` | two modes only, Time timer validation |
| `scripts/UI/MainMenu.cs` | Local Versus / Steam Remote Play Together |
| `scripts/UI/SettingsMenu.cs`, `scenes/ui/Settings.tscn` | Reduced Temporal Effects; 17-row Controls tab |
| `scripts/Core/AudioSnapshotMixer.cs`, `AudioBuses.cs`, `resources/Audio/default_bus_layout.tres` | C01b priority select + Critical Cues bus |
| `scripts/UI/StoryHUD.cs`, `FighterHudModel.cs`, `FighterHUD.cs`, `MatchResults.cs` | Time Freeze icon, stocks-lost label, Defy seal, two status pips, Conductive glyph |
| `localization/en.csv` | ~25 new keys; retire `fighter_mode_hybrid`, `controls_action_rewind`, `menu_lan_match` (already a recorded orphan) via `UnusedTranslationKeyTests` |
| `AGENTS.md` | status/schema/baseline updates are mandatory in the same change |

## 10. Test-baseline note

Current baseline is **1638**. Suites that will need **rewriting rather than extending** (their assertions
pin retired behaviour): `SessionExitGuardTests` (crash fee), `ManualRewindScrubTests` +
`StasisEchoPhysicsTests` (retired mechanic), `DialoguePresentationTests` (IsCompleted skip gate),
`MatchSettingsTests` / `CharacterSelectSceneTests` / `HolodeckConsoleTests` / `FighterAudioRulesTests`
(Hybrid), `FighterMatchFlowTests` (1-HP Sudden Death, Time-mode KO comparison),
`StatusControllerTests` (newest-wins), `AudioSnapshotMixerTests` (stacking),
`TutorialCalibrationTests` (manual-rewind calibration step), `SaveEnvelopeTests` +
`InputBindingSchemaTests` (v5, no Unbound). Plan for a net **negative** delta in some families before the
additions land — do not treat a dropping `Total:` here as a regression signature.
