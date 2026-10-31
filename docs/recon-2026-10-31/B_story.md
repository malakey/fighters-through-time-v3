# Recon B (Story side) — Momentum gauge, active Defy, Story meter scale

Read-only recon, 2026-10-31, build at main 56031750. Scope: the Story half of `DECISIONS_meter.md` (Momentum gauge replacing Special cooldowns; active Defy History on its own button at 50 Influence; Story gain scale 0.35 -> 0.5 with scaled flat effects). Everything is verified in code unless marked **(inferred)**. Recommendations are **REC**; planner questions **Q**. Paths are relative to the repo root; `PC` = `scripts/Characters/PlayerController.cs`.

---

## 0. Headline findings

1. **The Special cooldown plumbing is small and concentrated.** Two floats on `PC` (`SpecialOneCooldownTimer`/`SpecialTwoCooldownTimer`, PC:95-96), written by four things (`BaseSpecial.StartCooldown`, `RefundMomentum`, `Mozart.ShaveFortissimo`, `RestoreStoryResourceTimers`), ticked in one place (`UpdateCooldowns`, PC:5970), read at two cast gates (PC:5387, 5400) plus HUD, Mirror, bots, telemetry. The movement ability shares the machinery and **keeps it** (G1.5).
2. **REC: keep the two timers and repurpose them as the 2 s per-slot reuse delay; add a new `SpecialGauge` float.** Cast gates, HUD sync (StoryHUD.cs:364-365), F10 keys (`special1`/`special2`), Mirror projection, hitstop/Time Freeze pause behaviour and ~127 test references (17 files) keep working; only meaning changes. New: pip spend, gauge regen, gauge persistence.
3. **Cleopatra's Vortex Step does NOT halve a Special cooldown. It halves Desert Mirage's, her MOVEMENT ability** (`scripts/Characters/Abilities/CleopatraAbilities.cs:416-417`: `CooldownScaleForThisCast` is an override on `CleopatraDesertMirage`, class at :367; constant 0.5 on the vortex at :121; en.csv:1120 "casting Desert Mirage from inside her own Sandstorm Vortex halves that cast's cooldown"). Under G1.5 the movement ability keeps its cooldown, so **G4.2's "disable Vortex Step" is not forced; its premise (it halves Sandstorm Vortex) is wrong.** Q1.
4. **Mozart's staff refund and Extra Note touch only the movement ability** (MozartAbilities.cs:535-567 use `MovementAbilityCooldownTimer` only). Joan's Wings Refresh also only resets the movement timer (PC:2000). Requiem -> Fortissimo is the only kit writing a Special cooldown.
5. **The hitstop early-return is the main trap for an any-time Defy button.** `_PhysicsProcess` returns at PC:2110 for the whole freeze (4-11 frames = exactly the being-hit moment) and again at the tech-lock branch (PC:2181-2188). Defy must be read/buffered inside the hitstop branch (cf. `BufferChainPress` PC:2896, `BufferFreezeSpecialPress` PC:967, `BufferSpecialCancelPress`) or a press on the hit frame is silently lost.
6. **No save schema bump is needed** (section 5): the gauge rides the existing `StoryPlayerResourceTimers.AbilityCooldowns` string-keyed map; `DefyHistoryUsed`/`StoryDefyHistoryUsed` become dead data (RatingByLevel / EchoStepCooldownFrames precedent). The CharacterFactory spent-Defy seeding leak disappears with the field.
7. **Flat-effect scaling must not go inside `DrainUltimateMeter`**: it is also the Ultimate's spend (PC:5519, Tubman:544, Mozart:666) and the Level 0 hold's baseline drain (Level00Controller.cs:263, 377). Scale at the four call sites (4.2).
8. **Scene overrides**: Level_04_Paris.tscn overrides searchlight drain (3 occurrences), so scale at drain time, never by editing data.

---

## 1. Story Special cooldowns today

### 1.1 Write sites
| Site | file:line | What |
|---|---|---|
| Arm on cast | `BaseSpecial.StartCooldown` `scripts/Combat/BaseSpecial.cs:237-270`, called from `TryExecute(bool armCooldown)` :59-73 (line 69) | `cooldown = Data.CooldownDuration * Owner.StoryScoped(CooldownReduction, abilityID) * CooldownScaleForThisCast`; assigns Special1/2/Movement timer by `Data.Slot`; `Owner.OnSpecialCooldownArmed(slot)` (resets the Momentum 120-frame cap, PC:3054); raises `EventBus.RaiseCooldownStarted` (:265). Starts on the first cast frame; an interrupted cast still burns it. `RearmCooldown()` :235 (Mozart Extra Note, movement slot). |
| Per-hit Momentum (D2) | `PC.NoteSwingLanded` PC:3019 -> `ApplyMomentumRefund` :3036 -> `RefundMomentum` :3043; amounts `BasicComboRules.MomentumRefundFramesForHitbox` BasicComboRules.cs:806 (20 string 1-2 and directional strikes, 60 finisher), cap 120 per slot per cycle via `MomentumGrant` :820 | Subtracts from BOTH Special timers. |
| Kit: Requiem -> Fortissimo shave | `MozartAbilities.cs:119-124` `ShaveFortissimo` (callers :93, :236); `KitReachRules.RequiemFortissimoShaveSeconds` 2.0 (`scripts/Combat/KitReachRules.cs:42`) | Writes `SpecialTwoCooldownTimer`. Fighter twin FighterKitMotion.Reach.cs:547 (sim recon). |
| Restore | `PC.RestoreStoryResourceTimers` PC:6029-6050 (6038-6047) | From `AbilityCooldowns["special1"/"special2"/"movement"]`. |
| Fighter presentation proxy | `scripts/FighterSim/FighterSimulationDriver.cs:966-967` writes `player.SpecialOneCooldownTimer = runtime.SpecialOneCooldownFrames / 60f` | The shared `PlayerController` is also the Fighter presentation body; if the Story timers change meaning, the driver feed and FighterHUD follow the sim recon's design. |
| Wings Refresh | PC:2000 | movement timer only. |

### 1.2 Tick / pause
`PC.UpdateCooldowns` PC:5970-5979: `cooldownDt = dt / max(0.05, StoryCooldownMultiplier)`, ticks all three timers. Called at PC:2163 inside `if (!TimeFrozen)` beside the lockout counters (2162-2168). So the timers (and any regen placed beside them):
- **pause** in the hero's own hitstop (PC:2110 return precedes it), under **Time Freeze** (`TimeFrozen`, PC:4558) and while the SceneTree is paused (pause menu, PausesGameplay dialogue);
- **keep ticking** in Stunned/Dazed/knockdown/ledge/air dodge, during a non-pausing dialogue, in Dead (inferred: no earlier return for Dead), and through the death-rewind hold and Post-Landing Hold.
`StoryCooldownMultiplier` (PC:139, CharacterFactory.cs:235) is character-wide; **no grid node feeds it today** (all three shipped `CooldownReduction` nodes are ability-scoped). It also speeds the movement timer.

### 1.3 Cast gates and refusals
`PC.CheckSpecialInput` PC:5357-5411:
- `TimeFrozen` refuse (5358); Suppression refuse + null-tone, timers untouched (5362-5367); `_ultimateHeldBackThisTick` claims the tick (5372);
- **ability lockout / D1 gate**: `BasicComboRules.IsSpecialLocked(_abilityLockoutFrames, _specialLockoutFrames, _swingLanded)` (5378; rules BasicComboRules.cs:869-898, `SpecialGateFrames = C ? S : A`) -> `ReportReadySpecialRefusals` (D10 flash) and refuse;
- per slot: press + `IsAbilityUnlocked(slot)` (PC:408) + **`SpecialOneCooldownTimer <= 0` (5387) / `SpecialTwoCooldownTimer <= 0` (5400)** + `_special.TryExecute()` -> `ArmAbilityLockout()` -> `UsingSpecial`.
- `SpecialPressIsReady` PC:1006-1012 duplicates the ready test for the not-yet flash (reads the timers, :1011). **A pip-less press must not flash "not yet"** (the flash means lockout-only refusal): gauge >= 1 must join `SpecialPressIsReady`; a no-pip press needs its own feedback (Q14).
- Freeze-only Special buffer: `BufferFreezeSpecialPress` PC:967 / `ReleaseFreezeSpecialBuffer` re-present on the thaw tick; ordinary gates judge, so a pip check in `CheckSpecialInput` suffices.
- `BaseSpecial.TryConsumeCooldownBypass` (BaseSpecial.cs:182; PC:5709-5712) is movement-only.

### 1.4 Legacy Unlock Dormant
`scripts/Core/LegacyUnlockSchedule.cs:39-87` (keys `movement`/`special1`/`special2`); `PC.ApplyLegacyUnlockLocks` PC:377 (raises `OnAbilitySlotLockChanged` :393), `IsAbilityUnlocked` PC:408, Suppressed path PC:6228. Special 1 after Level 2, Special 2 after Level 3. REC: spend a pip only on an accepted cast so a Dormant slot never spends; the gauge fills from Level 0 (G4 default).

### 1.5 HUD
- `StoryHUD.SyncCooldownAuthority` StoryHUD.cs:361-367 -> `HudAbilityIndicatorModel.SyncCooldownRemaining` (scripts/UI/HudAbilityIndicatorModel.cs:121); `OnCooldownStarted` -> `StartCooldown` (StoryHUD.cs:856; model :102). With the timers repurposed the lens sweep shows the 2 s reuse delay unchanged (G5.1). Needed: dim lens when gauge < 1, the 4-pip widget (HUD recon). REC poll the gauge each frame like `SyncCooldownAuthority` (comment StoryHUD.cs:350-360: the owner of the authoritative value writes the fill).
- UiCapture calls `StartCooldown` (UiCaptureTargets.Huds.cs:85, 242).

### 1.6 Grid cooldown minors
`joan_piercing_cooldown` (resources/Resonance/joan_grid.tres:80-91: `AbilityScope="joan_divine_piercing"`, 0.1, `GatedAbilityID="special2"`), plus mozart_grid.tres:86 and lincoln_grid.tres:85; all Special 2 only. Resolver ResonanceProgression.cs:399-468 stores a scoped reduction as the multiplier `max(0.05, 1 - sum)` (0.9). It is read ONLY at `BaseSpecial.StartCooldown` :247 via `Owner.StoryScoped(CooldownReduction, abilityID)`. The unscoped path feeds `StoryCooldownMultiplier` (ResonanceProgression.cs:431, 468).
- When `StartCooldown` stops reading it the stat key has no reader and `ResonanceReaderCoverageTests` (tests/ContentValidation, 5 cases) fails: a new reader is mandatory (G4.3).
- G4.3 says "character-wide" but the nodes are scoped to one Special 2. Q2: (a) keep the scope and add a `PC` accessor "any scope's CooldownReduction" (REC), or (b) unscope the three nodes, which feeds `StoryCooldownMultiplier` and would also speed the movement timer 11 %. Q3: "+10 %" as x1.1 or the existing divide-by-0.9 idiom (+11.1 %).

---

## 2. Kit rules to disable / confirm

| Rule | Where | Verdict |
|---|---|---|
| Requiem -> Fortissimo -2 s | MozartAbilities.cs:93, 119-124, 236; KitReachRules.cs:42; test `StoryReachKitTests` (4 cases, :51/:55) | Disable (G4.2): make `ShaveFortissimo` a no-op; otherwise it would shave the reuse delay (wrong). |
| Cleopatra Vortex Step halving | CleopatraAbilities.cs:118-121, 416-417, 445; tests `ResonanceTraversalTests` :36, :170 | Acts on Desert Mirage (movement). See finding 3 / Q1. `CooldownScaleForThisCast` (BaseSpecial.cs:228) has this as its only override. |
| Mozart staff refund / Extra Note | MozartAbilities.cs:501-567 | Movement-only. No change. `RearmCooldown` keeps using the movement branch of `StartCooldown`. |
| Joan Wings Refresh | PC:1988-2001 | Movement-only. No change. |
| Other Special-timer readers | PC, BaseSpecial, MozartAbilities:122, StoryHUD:364-365, MirrorParadoxController.cs:680-681, Diagnostics (PlaytestBots.cs:812,815,1079-1093; PlaytestFeel.cs:125-128; PlaytestTelemetry.cs:816-817) | Bots/telemetry/Mirror ready tests become "pip >= 1 and reuse delay <= 0": REC expose `PC.SpecialReady(slot)`. |

---
## 3. Defy History today, and what the active Defy needs in Story

### 3.1 Current passive implementation (to remove/replace)
- **Trigger** `PC.ApplyDamage` PC:4237-4281: `CurrentHP <= 0 && !_storyDefyHistoryUsed && CurrentUltimateMeter >= 100` -> used flag, `StoryManager.Instance.StoryDefyHistoryUsed = true` if `HasLiveAttempt` (4250-4253), `_defyFiredThisHit`, HP 1, `_ultimateMeter.Consume()`, `ApplyHitstop(12)` + shake (4261-4262), arms `_defyProtectionAwaitControl` + `_defyProtectionFrames = StoryDefenseRules.DefyProtectionFrames` (60; StoryDefense.cs:131), `_defySuppressHitEffects`, clears launch/tumble/grab, leaves Stunned/Dazed (4270-4280). Every damage path (hurtbox, DoT, hazard, `ApplyEnvironmentalDamage`) reaches it through `ApplyDamage`.
- **Protection window** `IsDefyProtected` PC:574 (awaiting control or frames > 0); ticks in `TickStoryDefense` PC:694-704 (only once actionable; suspended by hitstop/Time Freeze via `StoryDefenseRules.ClockSuspended` StoryDefense.cs:138). Consumers: PC:1580, 4127, 4152 (damage/status gates), 5513 (`IsUltimateActivationAvoided`), PlayerController.EnvironmentalBlock.cs:66, EinsteinRelativityRift.cs:309. Early end via `ClearDefyProtection` PC:602 at PC:934, 943 (swing start), 3327, 4641.
- **State** `_storyDefyHistoryUsed` PC:869 (getter :1110, `SetStoryDefyHistoryUsed` :595); `StoryManager.StoryDefyHistoryUsed` StoryManager.cs:1018 (reset :1005, to save :1039, from save :1326); `StoryManager.DefyHistoryUsed` -> `_attempt.DefyHistoryUsed` StoryManager.cs:1964-1972 (Bump + `CommitCriticalEvent`); `StoryAttemptState.DefyHistoryUsed` StoryAttemptState.cs:272 (legacy recovery sets true :366; cleared :512); `StorySaveData.StoryDefyHistoryUsed` SaveManager.cs:76 (reset :1323).
- **Known leak** `CharacterFactory.cs:174-178` seeds a spent Defy on every default-built hero from `StoryManager.StoryDefyHistoryUsed` (guarded by `HasLiveAttempt`). Gone with the field.
- **Seal/HUD** `PC.DefySeal` PC:613-620, `PublishDefySealIfChanged` PC:628 (called PC:2152) -> `EventBus.RaiseDefySealChanged` (EventBus.cs:436-445, 797). Subscribers: StoryHUD.cs:97, 179, 248, 670-688, 1238; FirstUseTooltipPresenter.cs:121, 327 (tooltip id `FirstUseTooltips.Defy`, scripts/Core/FirstUseTooltips.cs:28); CoreMechanicCuePresenter.cs:45, 156, 207-212 (`DefySealShatter`; catalog CoreMechanicCueCatalog.cs:31, 61, 77); Level00Controller.cs:921; `scripts/UI/DefySealModel.cs`; FighterHUD (Fighter side). Tests `DefySealTests` (5), `DefySealStateTests` (4).
- **Telemetry** PlaytestTelemetry.cs:599, 606 track Defy via `player.StoryDefyHistoryUsed` (the procs-per-run metric dies with the passive save).
- **Mirror clone** built with `applyStoryProgression:false` (unused Defy each spawn) and `AppliesStoryMeterGainScale=false` (MirrorParadoxController.cs:250); driven by the Fighter CPU through `InputManager.SetInputSource`; observation MirrorParadoxController.cs:654-710 (`InfluenceRaw` :679, cooldown frames :680-682).

### 3.2 What the active Defy needs
**State.** No existing state fits "legal from anywhere". REC append `CharacterState.Defying` after `Thrown` (enum PC:8-~60; ordinals are int-cast so append only; only 7 `CharacterState.Thrown` uses in PC and 4 elsewhere) with a phase counter like `Grabbing` / the air-dodge sub-phase of `Rolling`: 6 fully invulnerable startup frames, blast on the 6th, about 24 vulnerable recovery. A sub-phase of `UsingSpecial` is worse (`ProcessUsingSpecial` assumes `_special1/_special2`).

**Where it is read.** One `TryBeginDefy()` before the state `switch` (PC:2190+), and also:
- in the hitstop branch (PC:2085-2110). Q4: act inside the freeze (clear `_hitstopFramesRemaining`, drop `_hasPendingLaunch` and the DI resolution at PC:2101-2108) vs buffer to thaw and cancel the already-applied launch;
- at/before the tech-lock early return (PC:2181-2188; invulnerable anyway). Q5: allow in tech lock / knockdown / get-up / Post-Landing Hold?
- release path to copy: PC:4273-4279 (`_hasPendingLaunch=false; _stunTumble=false; ClearTumbleAfterHitstun(); ReleaseGrabState(); TransitionTo(...)`) plus `CancelActiveAttack()` and `InterruptActiveAbilities()` (PC:4086, used 4295-4296), ledge exit, roll/air-dodge exit, Dazed exit.

**Refusals (5.4).** `TimeFrozen` (as PC:5358 / 5418); ignore `_abilityLockoutFrames`/`_specialLockoutFrames` and `_abilityCastSuppressed`; never call `ArmAbilityLockout()`; no `IsAbilityUnlocked` gate (5.3). Needs `CurrentUltimateMeter >= 50`; cost via `DrainUltimateMeter(50)` (unscaled, a spend). `_ultimateHeldBackThisTick` must not block it.

**Ultimate interplay.** The 20-frame wind-up/hold exists only for the Mirror clone's activation strike (`UsesFighterUltimateActivation`, PC:5506-5530; the Story hero is never held, `VERIFY-STORY-MIRROR-HOLD`), so decision 3.3's "only before the hold" is moot for the Story hero; reusing `IsDefyProtected` for the startup makes the clone's strike whiff via `IsUltimateActivationAvoided` (PC:5513). Q6: Defy out of the hero's own Story Ultimate cast (hyper-armoured startup)?

**Invulnerability.** Reuse `_defyProtectionFrames` for the 6 startup frames. The await-control semantics and 60-frame window go away, and the early-end calls at PC:934/943/3327/4641 must not cancel the startup. Zero protection at blast time so recovery is vulnerable. Every gate (PC:4127/4152/1580, EnvironmentalBlock:66, status gates) already honours `IsDefyProtected`.

**Blast** (2.5 u = 150 px at 60 px/unit):
- Enemies: iterate group `Enemies` (what Time Freeze sweeps) or a `StoryShapeQuery` (scripts/Combat) and dispose wrappers with `AsDisposable()`. Per `EnemyController`: `ApplyStun(duration, minSeconds, fromSpecial)` (public overloads EnemyController.cs:2089-2121; scaled by `StunResistance`, **refused by `IsStaggerArmored`** getup/Armored Recovery windows, feeds the elite budget unless `chargesStaggerBudget:false` on the private overload :2157), `ApplyKnockback(Vector2, facingRight)` (:2044 -> `TryApplyKnockback` :2054: refused when armored, refuses a zero vector, weight-scaled by `DamageCalculator.CalculateKnockback`, resets the finisher carry/bounce latch), `ApplyHitstop` (:2084). Skip dormant lock members (`IsEncounterDormant`, EnemyController.Encounter.cs:90; hits refused at EnemyController.cs:155, 244, 2249), summons and `DrillInvulnerable`. Q7: should the blast ignore the V7.4 armor windows? REC a dedicated `EnemyController.ApplyDefyBlast(...)` so the rule is explicit.
- Bosses (`BossController`): no stun system (intake ignores hitstun). Interrupt precedent: `CancelTelegraphIntoRecovery()` BossController.cs:213 -> `Executor.CancelIntoRecovery()` (EnemyAbilityExecutor.cs:202), `Executor.Cancel()` :214, `IsBusy` :56, `LastTelegraphInterrupted` :205, threshold interrupt BossController.cs:1225-1229, transition cancel :1300. Push via `ApplyKnockback` BossController.cs:1803, which already returns for `Data.IsKnockbackImmune` (BossData.cs:34, **default true**), matching 4.3. Q8: must also cancel an Active attack window, not only a telegraph (read EnemyAbilityExecutor.cs:200-225). Honour `IsArenaWarded`.
- Q9: does the hero's blast shove the Level 13 Mirror clone (a `PlayerController`, hit-payload based knockback)?
- No damage, no meter, no Rally echo, never `HitPayload.ContactLanded` (must not feed `NoteSwingLanded`, the gauge, or the freeze buffers).
- Presentation: ~8-frame freeze via `ApplyHitstop(8)` on hero and each enemy in range; `CameraShake`; "DEFIED" world label (`FloatingDamageNumber.Show(int...)`, PC:1820, needs a string overload); new Critical cue id in `CoreMechanicCueIDs` replacing `DefySealShatter`; `ComfortSettings` gate for the pulse.

**Heal (2.4).** `_echoPool` PC:867 (+ `_echoDrainPerFrame` :868, `EchoPool` getter :1107) is HP that will be permanently lost, draining over `EchoDrainFrames` (PC:2133-2142) and crediting damage-taken meter as it drains. Helpers: `ReclaimEchoPoolFraction` PC:2009, damage-scaled reclaim in `AddInfluenceFromDamageDealt` PC:4373-4398, `HealStory` PC:4438 (HP-only; reclaimed HP grants no meter). REC new `CashInEchoPool(0.10)`: heal = clamp(max(pool, 0.10 * MaximumHP), 0, missing HP), zero the pool. Q10: does the floor apply with an empty pool; does cashing forfeit the pool's uncredited damage-taken meter (the existing reclaim does)?

**Mirror/CPU.** Needs a Defy request bit: `GameplayButtons` bit 15 is the only free bit (PlayerInputFrame.cs:7-44; 11 Dash, 12 Echo, 13 Grab, 14 DirectOrigin; wire size unchanged, packet/protocol is sim recon). `MirrorParadoxController.Observe()` must project Defy availability (meter >= 50) and the hero's activation phase for the Hard "beats a wind-up" band. Same 50 cost, unscaled (clone is `AppliesStoryMeterGainScale=false`).

---

## 4. Story meter scale and flat effects

### 4.1 Scale
`StoryCombatRules.MeterGainScale = 0.35f` (scripts/Combat/StoryCombatRules.cs:38); `MeterGainFactor(storyScaled, buildRateLane)` :46-47; `PC.StoryMeterGainFactor` PC:187-188; `PC.AppliesStoryMeterGainScale` PC:175 (true only for the campaign hero, CharacterFactory.cs:160; false for the clone, MirrorParadoxController.cs:250, and the bot A/B flag, PlaytestRunner.cs:366). Read by `UltimateMeter.GainFactor` (UltimateMeter.cs:40-41) and `PC.RallyUncreditedMeter` (PC:5996). **Change = one constant.** Pins: `StoryMeterGainScaleTests` (8 cases, 18 refs), `StoryMeterGainFactorTests` (6), `InfluenceMeterSourceScanTests` (3), `MirrorParadoxTests`, `UltimateMeterTests` (3), `StoryRallyTests`, `StoryDefenceOrderTests`, `PlaceholderZoneTests`, `PuzzleEnvironmentToolkitTests`, `Level09ContentTests`, `ResonanceMajorReworkTests`.

### 4.2 Flat effects on the campaign hero
| Effect | Where | Today | After | Notes |
|---|---|---|---|---|
| Extractor discharge | ChronalExtractor.cs:161-176 (line 175; export :12 = 20) | -20 even when blocked | -10 | Resonance Hold stand-ins inherit the field but have no discharge (M16); verify no override. |
| Searchlight (L4, L9 beams) | SearchlightZone.cs:91-109 (line 104; export :31 = 5/s) | -5/s | -2.5/s | Level_04_Paris.tscn overrides (3x), SearchlightZoneTemplate.tscn (1x). Scale at drain time. |
| Siphon Snare | SiphonTetherChannel.cs:258-279 (`wanted` :267, cap :268-270; defaults :121/:124; resources/Enemies/Abilities/unbound_eraser/siphon_snare.tres) | -10/s cap 30 | -5/s cap 15 | Scale wanted and the cap; `SiphonSnareTests` (21 cases) pin 10/30. |
| Shield of Orleans | PC.ApplyShieldOfOrleans PC:2035-2054 (line 2043; const PC:68 = 5) | +5 | +2.5 | |
| Level 0 hold drain | Level00Controller.cs:263, 377 | drain to baseline | unchanged | not a flat effect |
| Ultimate spend | PC:5519, Tubman:544, Mozart:666 | -100 | unchanged | spend |
| Defy spend / Level 0 set | new / Level00Controller.cs:883 | | unchanged | spend / direct set |
| MeterBoost orb | ChronalOrbItem.cs:126-129 (`AddFlat(Data.Value)`) | unspawned | Q11 | 6.2 "any other flat grant" vs 7.2 "no change" conflict |

REC helper: `StoryCombatRules.FlatScale(bool storyScaled) => storyScaled ? MeterGainScale : 1f` and `PlayerController.ScaleStoryFlatMeter(float)`; the four call sites use it. Do NOT reuse `StoryMeterGainFactor` for flats: it multiplies Mozart's build-rate lane (a Mozart with the +10 % node would drain 10 % faster). Q12. Keep the helper out of `InfluenceMeterRules` and scripts/FighterSim/; teach `InfluenceMeterSourceScanTests`.
Side effects: a save written under 0.35 carries `RallyUncreditedMeter` in old-rate units (PC:5992-5994); one resume across the change mis-credits once (negligible).
Dead-path cleanup: `PC.ApplyStockLossMeterRetention` PC:4478 / `UltimateMeter.ApplyStockLossRetention` UltimateMeter.cs:53 (no caller; pinned in `UltimateMeterTests`); forwards `InfluenceMeterRules.StockLossRetention` (UltimateMeter.cs:17), which 6.4 changes 3/4 -> 1/2 in the shared rulebook.

---
## 5. Save / persistence

### 5.1 What exists
`StoryPlayerResourceTimers` StoryAttemptState.cs:129-154 (`AbilityCooldowns` map :133, dead `EchoStepCooldownFrames` :138, `RallyUncreditedMeter`, `TimeFreezeCooldownSeconds`, `WardenclyffeRechargeSeconds`). Capture `PC.CaptureStoryResourceTimers` PC:6000-6017 (from `StoryManager.CapturePlayerResources` StoryManager.cs:1092-1097, only when `IsLevelTimerRunning` and `TryGetAttemptHero`); restore `PC.RestoreStoryResourceTimers` PC:6029-6050 (only on `resumedMidLevel`, `StoryLevelControllerBase.RestoreDurableResources` :611-634, line 632; Florence via Level01Controller.cs:815). Root meter is `save.CurrentUltimateMeter`.

### 5.2 Lifecycle
| Event | Today (timers) | Gauge |
|---|---|---|
| Fresh entry / full Restart / Restart from Game Over | `MintFreshAttempt` StoryManager.cs:1983-1993 -> `ResetDurableResourcesForFreshAttempt` :1183-1189; `RestoreDurableResources` fresh branch (StoryLevelControllerBase.cs:615-622) discards `PlayerResourceTimers = new` and keeps the factory-fresh hero | factory-fresh hero starts at 2 pips; nothing else needed |
| Ordinary load / mid-level resume | restores the dictionary | new key `"special_gauge"`; absent key -> start fill 2 (not 0, not 4); clamp restored special1/2 reuse values to <= 2 s |
| Collapse resume, Anchor Snap | rebuilt hero, same path | persisted gauge restored (G3.3) |
| Death rewind | same `PlayerController` instance; ChronalRewindManager stores no cooldowns | untouched (consistent with G3.3) |
| Fighter/Holodeck/Drills/Test Arena | default hero | Q13: shared class default is harmless; sim recon owns a proxy feed |

### 5.3 Schema decision
- **No bump.** A new key in the string-keyed `AbilityCooldowns` map is additive with a legacy-meaning default (absent -> start fill); schema stays v8. The start-fill constant lives in a pure rule class (no resource read in a payload initializer or `Normalize()`; engine-free payload rule).
- Defy fields become **dead data** (keep for newer-schema rejection and `SaveEnvelopeTests` round trips): `StorySaveData.StoryDefyHistoryUsed` (SaveManager.cs:76), `StoryAttemptState.DefyHistoryUsed` (:272), `StoryManager.StoryDefyHistoryUsed`/`DefyHistoryUsed`. REC stop writing them, comment as dead (RatingByLevel precedent). Delete CharacterFactory.cs:174-178 and `PC.SetStoryDefyHistoryUsed`.
- Q15: keep persisting the 2 s reuse delay (REC; zero cost, existing tests pin the dictionary) or drop it.

---

## 6. Recommended Story state layout (REC)

On `PlayerController`:
- `public float SpecialGauge` (pips, continuous 0..4), start 2.
- `SpecialOneCooldownTimer`/`SpecialTwoCooldownTimer` kept; meaning = per-slot reuse delay seconds, set to 2.0 on accept, ticking in `UpdateCooldowns` exactly as now. `MovementAbilityCooldownTimer` unchanged. Keep the names (a rename touches ~127 test refs in 17 files) and update doc comments.
- Constants in `BasicComboRules` (replace the Momentum region :766-860, whose names collide with the new Momentum gauge; the sim still uses them at FighterSimulationSystems.cs:2470-2475 until the sim recon retires them): max pips 4, start 2, cost 1, reuse delay 2 s, regen 1/12 pip/s, gains (`combo_1` 0.05, `combo_2` 0.05, `combo_3` 0.2, directional/air strike 0.05 via the same id switch as :806-811), Defy cost 50, startup 6, blast frame 6, recovery 24, stun 30, radius 2.5 u, freeze 8, heal floor 0.10.
- Regen: in `UpdateCooldowns` (PC:5970) so it shares hitstop/Time Freeze/tree-pause semantics; no pause after a spend (G1.4).
- Gain: in `NoteSwingLanded` (PC:3019) replace `ApplyMomentumRefund` with a gauge grant. The D1a landed predicate is already `payload.ContactLanded && IsBasicStringHitbox` at PC:1834, once per swing on the first landed contact (props, strike surfaces, blocks, invulnerable targets and 0-damage payloads never count). Specials never call it (G2.4).
- Spend: REC at `CheckSpecialInput` (PC:5387-5409) before `TryExecute` so a failed `Validate()` leaves no half-state; `StartCooldown`'s Special branches set the reuse delay and drop `OnSpecialCooldownArmed`. `AbilityData.CooldownDuration` of the Special assets becomes unread in Story (validators may pin it).
- Defy: `CharacterState.Defying` + phase counter, `TryBeginDefy()`, `ApplyDefyBlast()`, `CashInEchoPool()`; delete the passive block PC:4237-4281, `_storyDefyHistoryUsed`, `DefySeal`/`PublishDefySealIfChanged`.
- Mirror: `Observe()` keeps the reuse-delay fields, adds a pip count to the shared `CpuDecisionObservation` (sim recon owns the struct).

---

## 7. Tests (case counts by `[TestCase]` grep; REWRITE / DELETE / KEEP)

Cooldown / Momentum / D1 gate:
- tests/Unit/StoryHitConfirmTests.cs (15 cases, 44 refs): Momentum refund cases (lines ~237-375: per-hit amounts, cap 120, exact-zero, per-slot cap) REWRITE to gauge gains (~8); D1 gate cases KEEP/adapt (~7).
- StoryAbilityLockoutTests (13, 13 refs) KEEP/adapt; AbilityLockoutRulesTests (5) and the pure gate KEEP (+ new Momentum-constant tests).
- StoryLockoutRefusalEventTests (2), StorySpecialRecoveryCancelTests (12), StoryAirAttackTests (6 refs), UltimateChordStoryTests (2), AbilityInterruptionTests (5), StoryCombatContractW3Tests, StoryKitMotionTravelTests, StoryRelativityWarpFoldTests, ResonanceProgressionTests: mostly seed timers to 0; with the timers kept they need only a starting gauge >= 1 (default 2): KEEP.
- SuppressionTests (6, 12 refs): REWRITE the "timers untouched" lines to gauge untouched.
- StoryResourceTimerPersistenceTests (3) and LevelStartResourceResetTests (10): REWRITE/extend (gauge round trip, absent key -> 2, fresh -> 2, reuse clamp).
- StoryReachKitTests (4; Fortissimo shave :51, :55): REWRITE or DELETE the shave cases. ResonanceTraversalTests (10; Vortex Step :36, :170): KEEP if Q1 leaves it. MozartSonataDriftChargeTests (3): KEEP.
- ResonanceReaderCoverageTests (5): REWRITE for the new regen reader. PlaytestCombatStatsTests: refusal-condition list (`cooldown`) REWRITE.
- MirrorParadoxTests (34): ~4 projection cases REWRITE, rest KEEP.

Defy:
- DefySealTests (5), DefySealStateTests (4), `DefySealModel` uses in StoryHudPulpTests (15 cases, 9 refs), StoryHudContractTests, StoryHudCompactTests, GlassKitContentTests (icons `defy_ready/spent/barred`), FighterHudContentTests, FighterHudLayoutRulesTests: DELETE or REWRITE (seal retired, notch at 50).
- StoryDefenceOrderTests (11, 12 refs): REWRITE (active invulnerability; the Aegis-vs-Defy order is still meaningful). StoryEnvironmentalDamageTests (11; 6 refs, 2-3 Defy cases): REWRITE/DELETE those. StoryKillBoundaryTests (5; pit ignores Defy): KEEP. FeelPassStoryTests (27; 4 refs), StoryRallyTests (3; 2), StoryTumbleTests (6; 1, "a Defy proc ends a tumble"): REWRITE the Defy lines.
- StoryAttemptStateTests (8), SaveEnvelopeTests (7 refs), RetireLegacyLevelV8Tests (2): KEEP as dead-data round trips.
- TutorialCalibrationTests / TutorialCalibrationV76Tests (8) (`RegisterDefyProc`, `SkipDefyLesson`), TutorialCalibrationContentTests (keys `tutorial_step_defy`, `tutorial_step_defy_spent`; en.csv:996-997), TimeFreezeTests.cs:658, StoryMeterGainScaleTests.TheMeterAndDefyLessonsLethalHitIsDefiedOnTheScaledCampaignHero (:185), Level00FlowSceneTests: REWRITE for the button-driven lesson (Level00Controller.cs:864-927 `BeginMeterAndDefyLesson` / `ProcessMeterAndDefyLesson`; step in TutorialCalibration.cs:36, 177-250; set meter 50, trap the hero in a drone string, complete on the press).
- CoreMechanicCueCatalogTests (3 refs): cue rename.
- Sim (other recon): DefyProtectedRecoveryTests (33 refs), FighterVerbLayerTests, FighterHitPipelineTests, FighterLedgeGetUpTests, FighterMatchFlowTests, FighterCombatContractW3Tests, FighterHitConfirmTests, FighterTimeSystemsTests.

Meter scale / flats / input:
- StoryMeterGainScaleTests (8), StoryMeterGainFactorTests (6): REWRITE 0.35 -> 0.5. InfluenceMeterSourceScanTests (3): update the scan. UltimateMeterTests (3), InfluenceMeterRulesTests (3; retention only if 6.4 changes the shared rulebook). SiphonSnareTests (21): REWRITE numbers for a scaled target. Extractor pin (StoryEnvironmentalDamageTests), Level04ContentTests (searchlight 5/s, 3 refs), EraMechanicToolkitTests (2), ActThreeChainingTests (1): REWRITE constants. Shield of Orleans pins (grep `ShieldOfOrleansMeterPerBlock`): REWRITE 5 -> 2.5 on the campaign hero.
- GamepadDefaultBindingTests (pins Dialogue Log on RightStick 8, nothing shared): REWRITE for R3 -> Defy and the Log on Back.

New suites: pure gauge rules; spend/refuse/flash; regen pauses (hitstop, Time Freeze); gain per hitbox; persistence incl. absent key; Defy legality across every `CharacterState`; startup invulnerable / recovery vulnerable; blast vs mob/elite/armored/dormant/boss (immune and not); heal floor; Mirror observation; flat-scale helper.

---

## 8. Other touch points
- Input: new `gameplay_defy` action in `InputManager.Actions`, `project.godot` (keyboard F; pad R3 = RightStick 8, **currently the Dialogue Log**, moved to Back 4 per 3.4; Time Freeze is L3 = LeftStick 7), RemappableActions, Controls tab.
- Influence tube notch at 50 (HUD recon); first-use tooltip `FirstUseTooltips.Defy` currently triggers on seal Ready/Spent and needs a new trigger.
- Docs to amend: AGENTS.md D5 and Defy paragraphs, ledger, `DEFENSIVE_EFFECTS` D04, `STORY_PERSISTENCE` F10, `HUD_CONTRACT` F13, design master/mirror.

## 9. Open questions (Story side)
1. Vortex Step acts on Desert Mirage (movement): disable anyway (G4.2) or leave?
2. Grid cooldown minors: keep scope plus an any-scope accessor, or unscope (also speeds movement)?
3. "+10 % regen": x1.1 or divide-by-0.9?
4. Defy inside the hit's own hitstop: act in the freeze, or buffer to thaw and cancel the applied launch?
5. Defy legal in tech lock / knockdown / get-up / Post-Landing Hold?
6. Defy out of a Story Ultimate cast?
7. Does the blast ignore V7.4 getup/Armored-Recovery armor and the elite stagger budget?
8. Boss: cancel an Active window too, or telegraph only?
9. Does the blast push the Mirror clone?
10. Echo cash-in: floor with an empty pool; forfeit uncredited meter?
11. MeterBoost orb flat grant: scaled or untouched?
12. Flat scaling: D5 scale only (REC) or full factor incl. Mozart's lane?
13. Fighter/Holodeck use of the shared PlayerController: sim-fed gauge proxy or Story-only?
14. No-pip Special press: silent, null-tone or own flash; pip check before or after the D1 lockout refusal?
15. Regen while Dead / during the death-rewind hold / non-pausing dialogue: allow (current cooldown behaviour) or pause?