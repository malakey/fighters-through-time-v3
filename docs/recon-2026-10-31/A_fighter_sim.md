# Recon A - Fighter simulation side of the Momentum gauge + active Defy (2026-10-31)

Scope: scripts/FighterSim/, scripts/Networking/, InputManager/PlayerInputFrame, driver reads, CPU. Read-only recon at main 56031750.
Rulings: docs/recon-2026-10-31/DECISIONS_meter.md. "REC" marks a recommendation; everything else was read in code.
Path keys: SC = scripts/FighterSim/FighterSimulationComponents.cs, SS = scripts/FighterSim/FighterSimulationSystems.cs, ES = scripts/FighterSim/FighterEntitySystems.cs, CPU = scripts/FighterSim/FighterCpuController.cs.

## 0. Facts that shape the design (verified)

- Influence lives on component 300 `FighterStateComponent.Influence` (FP64, cap 100 = `FighterMeterRules.Cap`), SC:505. Component 300 is exactly full at 128 B (SC:495-501). 305 is full (SC:511-), 310 is full (SC:743-, last 4 B = packed grab partner :798).
- Room to grow: 312 `FighterDefenseComponent` is 9 ints = 36 B (SC:841-866); 323 is 9 ints = 36 B (FighterSpecialCast.cs:38-90). Klotho limit is 128 B per component. Highest ID is 323; 324 is unallocated (record the need, do not pick it).
- Snapshot baseline: largest 3,277 B, 0.090 ms median / 0.137 ms p95 against an 8 ms budget (docs/PERFORMANCE_BASELINE.md:40). A new 24-36 B/fighter component adds 48-72 B per snapshot.
- System order (FighterSimulation.cs:57-71): World, Input(Pre), Countdown(Pre), Movement(Update), AbilityEntity(Update), Pushbox, Combat(Post), Projectile, Persistent, Zone, Hazard, Orb, Match(Late).
- FighterMovementSystem.Update (SS:289) loops fighters with early `continue` branches that also discard buttons: hitstop (SS:312-368), captured (SS:410-417), M05 knockdown (SS:440-464), landing-tech lock (SS:479-). FighterAbilityEntitySystem then reads presses (ES:~1330-1440) behind `CanReadAbilityPress`. FighterCombatSystem works on the pair (first/second), the only system holding both fighters (SS:~1850-1910).

## 1. Special cooldowns today -> Momentum gauge

### 1.1 Where cooldowns live
| Role | Site |
|---|---|
| State `SpecialOneCooldownFrames`, `SpecialTwoCooldownFrames`, `MovementCooldownFrames` (305) | SC:527-529 |
| Authored cooldown on tuning (306) / loadout | SC:686-687, 353-354, 412-413; FighterLoadoutFactory.cs:32-33,120; spawn copy SS:113-114 |
| Press gate + arm | ES:1382-1397: `PressedButtons & SpecialOneButton && SpecialOneCooldownFrames <= 0`; arm `= PositiveCooldown(tuning.Special*CooldownFrames)`; resets `verb.MomentumRefundedFramesSlotOne/Two = 0` |
| Tick down (unfrozen ticks only, TickCounters) | SS:1477-1479 (S1, S2, Movement). TickCounters is skipped on hitstop ticks (continue at SS:367) but runs in the captured (SS:412) and knockdown (SS:441) branches |
| D2 Momentum refund | `OnSwingResolved` SS:2444-2456 -> `ApplyMomentumRefund` SS:2465-2477 -> `BasicComboRules.MomentumGrant` (BasicComboRules.cs:820; consts :781-787) |
| Momentum callers (basic swings only, so G2.4 "only basics refill" already holds) | ledge attack SS:2313 (pays as VariantChain step 0 = 20 f), air strike SS:2327, directional SS:2341, chain hit SS:2423 (20/20/60 by step) |
| Requiem -> Fortissimo shave (reads/writes S2 cooldown) | FighterKitMotion.Reach.cs:547-550 `ShaveFortissimo` |
| Sudden Death | SS:3502-3504 (S1/S2/Movement cooldowns = 0), SS:3493-3494 (Momentum counters), SS:3469 (Influence = 0) |
| Stock loss | `ApplyStockLoss` SS:3693-; retention SS:3734 `FighterMeterRules.RetainOnStockLoss` (FighterMeterRules.cs:41-42,67-68); comment SS:3765-3767: cooldowns and Defy flag are kept. Momentum counters are NOT reset by stock loss |
| `CanReadAbilityPress` | FighterAbilityLockoutRules.cs:50-87 (no cooldown term) |
| Ultimate gate | `UltimatePressPayable`/`Accepted`/`HeldBackByLockout` SS:2716-2780 (needs `Influence >= Cap`) |
| Lockout A/S/C + freeze buffer | FighterAbilityLockoutRules.cs (A 30 f, S 30 f, `AllowsSpecial`, `ArmAbility`, `Tick`, `Clear`) |
| D10 not-yet classifier | FighterLockoutRefusalRules.cs:28-52 (baseline holds both cooldowns), :121-125 |
| Driver | FighterSimulationDriver.cs:966-967 (cooldown timers to HUD), :1259-1266 (cooldown RISE used as the accepted-cast edge for the cosmetic hold), :708-709 |
| Test seam | FighterSimulation.cs:373-384 `SeedFighterSpecialCooldownsForTest` |
| Vestigial | `AttackIntent.CooldownFrames` (SS:3230,3249) and the intent-time `tuning.Special*CooldownFrames` args (SS:3096,3127,3142): nothing reads them; safe to delete |

Cleopatra's "Vortex Step halves Vortex cooldown" has no sim site (Story only). Only Requiem's shave exists in the sim. Mozart's staff refund and Extra Note touch the movement cooldown (SS:882, FighterKitMotion.Reach.cs:638), which stays (G1.5).

### 1.2 What replaces them
Needs: gauge value, two per-slot 2 s reuse timers, regen, landed-basic grants, start value, Sudden Death value.

REC representation: integer "momentum units", 7200 units = 1 pip.
- Max 4 pips = 28,800. Start 2 pips = 14,400. Spend 7,200 per Special at acceptance (the site that arms the cooldown today).
- Regen 1 pip / 12 s = 7200 / 720 frames = exactly 10 units/frame. G4.3 "+10 % regen" = 11 units/frame, exact (Story only).
- String hit 1/2 +0.05 pip = 360; finisher +0.2 pip = 1,440; full landed string = 2,160 (= 0.3). Directional / air strike default 360. All integer, no FP. Milli-pips (1000/pip) does NOT work: 1000/720 regen is not integral.
- Constants go in `BasicComboRules` (shared Godot-free rulebook, like `AbilityLockoutFrames`) with a sim reader `FighterMomentumRules` in the style of FighterMeterRules; Story reads the same ints.

State placement (REC option A):
- A, no new component ID: gauge in 310 `MomentumRefundedFramesSlotOne` (SC:766) renamed `MomentumUnits`; slot-Two int becomes a spare. Reuse delays in 305 `SpecialOneCooldownFrames`/`SpecialTwoCooldownFrames` (SC:527-528) renamed `...ReuseFrames`, armed to 120 at accept (replacing ES:1391/1395), ticked by the existing SS:1477-1478 lines (so they pause in hitstop as cooldowns did). The existing 305 fields CAN be repurposed directly: test seam, driver reads and CPU reads keep compiling; rename so grep finds every stale reader. Regen = one line in TickCounters beside SS:1477. Tuning 306 `Special*CooldownFrames` become dead (leave, or reuse as per-character gain scale for the kit pass).
- B, new component 324 `FighterMomentumComponent` (units + 2 reuse + Defy state, 6-8 ints = 24-32 B/fighter, +48-64 B/snapshot): cleaner names; costs one ID plus SpawnFighter (SS:78-200), Sudden Death setup and snapshot/hash test updates.
- Do not put the gauge in 323: 323 is zeroed by `= default` at the respawn-platform hold (SS:386) and Sudden Death (SS:3566), and the gauge must survive stock loss (G3.2).

Grants: replace `ApplyMomentumRefund` in `OnSwingResolved` (SS:2444). It already has the right "landed": outcome `Landed || Absorbed`, `damage > 0`, once per swing via the `NoteSwingLanded` 0->1 edge (SS:2452). Blocked/refused/Foresight-counter/zero-damage pay nothing. `MomentumRefundFramesFor` -> `MomentumGainUnitsFor(variant, step)`; `MomentumGrant`, the per-cycle cap and `MomentumRefundCapFramesPerCycle` die. Clamp at 4 pips. The ledge attack currently pays (SS:2313): decide.

Press gate: ES:1382-1386 -> `units >= PipCost && reuseFrames == 0`; spend at ES:1391-1396. Lockout gates (`AllowsSpecial` ES:1381, `ultimateClaimsTick/HeldBack` ES:1379-1380) unchanged (G2.2). A refused press spends/arms nothing. Foresight (ES:1402) and melee-intent Specials need no change (spend happens at acceptance).

Sudden Death (default carried: gauge = match-start 2 pips): SS:3502-3503 -> `units = StartUnits`, reuse 0; delete SS:3493-3494. Movement cooldown reset (SS:3504) stays. Stock loss: no code change (nothing resets cooldowns there today); regen continues on the respawn platform because TickCounters runs there.

`CanReadAbilityPress`: leave as is. Defy must NOT go through it.
D10 flash: `LockoutRefusalBaseline` and the "slot off cooldown at read time" test (FighterLockoutRefusalRules.cs:121-125) become "pip available and reuse clear at read time". A no-pip refusal should not flash as "lockout-only" (HUD question).
## 2. Defy History today vs the active Defy

### 2.1 Today (passive; to be deleted or replaced)
- 312: `DefyProtectionAwaitControl`, `DefyProtectionFrames`, `DefyProcIdentity`, `DefyBarred` (Aegis/Barrier fields STAY): SC:843-865. 310: `DefyHistoryUsed` (SC:761).
- `FighterDefenseRules` ES:239-365: `DefyProtectionTicks` 60, `IsDefyProtected` :250, `BeginDefyProtection` :258, `Tick` :274 (called from FighterCombatSystem SS:1894-1897 with `IsDefyActionable` SS:1941), `EndOnOffence` :302 (called from `FighterActionRules.OnActionAccepted`, FighterActionRules.cs:95), `CanDefy` :315 (unspent, unbarred, Influence >= 100), `IsDefyUnspentAndUnbarred` :327, `Seal` :357 (HUD).
- Procs: `ApplyFighterHit` lethal branch ES:800-837 (Influence 0, HP 1, 12 f hitstop, release hitstun/daze/launch/tumble/shieldstun/BeingHeld/GrabPhase, zero knockback/status); DoT path `ApplyUnattributedDamage` ES:1060-1076. `defied` suppresses echo and victim meter (ES:856, 867).
- Protection consumers (dead once the window dies): `ApplyFighterHit` gate ES:673; ES:1039; FighterKitRules.cs:285,353; FighterUltimateActivation.cs:300 (`IsAvoided`); SS:2817 (cinematic aborts if victim Defy-protected); test seam `SeedFighterDefyProtectionForTest` FighterSimulation.cs:338.
- Sudden Death bar: SS:3535-3538 `defense.DefyBarred = 1` (ruling 5.2 deletes it).
- Presentation: the Fighter driver has NO Defy presentation (no "Defy" in FighterSimulationDriver*.cs); only the HUD seal reads `verb.DefyHistoryUsed` (scripts/UI/FighterHUD.cs:803-819).

### 2.2 What the active Defy needs (hooks)
1. Input: `GameplayButtons.Defy = 1<<15` (section 3); sim reads `runtime.PressedButtons & (1<<15)`.
2. Where to read the press. Defy must be legal from hitstun, tumble, knockdown, tech lock, shieldstun/block stance, grab-held, ledge hang, cast recovery and hitstop. Each is an early-`continue` / button-discard branch in FighterMovementSystem.Update (SS:312, 410, 440, 479) and `CanReadAbilityPress` refuses them. REC: read and start Defy at the very top of the per-fighter loop in FighterMovementSystem.Update, right after `UpdateJumpLatch` (SS:306) and before the hitstop gate (SS:312): one site covers every branch, including the victim's own hitstop (the "being hit" moment). Gate: `Stocks > 0`, `RespawnFramesRemaining <= 0`, `Influence >= 50`, not already in Defy, NOT captured (321 `IsCaptured`), match live; own-Ultimate-busy per open question. Explicitly not gated by A/S lockouts, `CanReadAbilityPress`, hitstun, daze, hitstop, grab, ledge, block.
3. Versus an Ultimate wind-up: legal, and the invulnerability beats the strike because `FighterUltimateActivationRules.IsAvoided` (FighterUltimateActivation.cs:293-301) already tests `target.InvulnerabilityFrames > 0`. Once captured (cinematic) the press is refused (ruling 3.3).
4. Claim the tick: when Defy starts, discard this fighter's other buttons (pattern: `DiscardButtons`, FighterUltimateActivation.cs:~203-220; `ultimateClaimsTick` ES:1353) so no Special/Ultimate/movement/grab starts the same tick.
5. Start effects (one `FighterDefyRules.Begin`): spend 50 Influence (FP64 compare exact); `fighter.InvulnerabilityFrames = max(.., 6)` (reuses the existing invuln pipeline honoured by ES:666 and `IsAvoided`; it decrements at SS:481/541, FighterVerbRules.cs:272, FighterMovementSystem.HitReaction.cs:47, so the Defy action branch must decrement it itself like the tech/knockdown branches); clear `HitstunFrames`, `DazeFrames`, `PendingLaunchActive`, `Tumble`, `ShieldStunFrames`, `HitstunBlockCancelBlocked`, `TechLockoutFrames`, and (decision) `HitstopFrames`; clear knockdown (320 `FighterKnockdownRules.Clear`), Foresight (322), the Special cast and D3 buffer (323), universal movement (`FighterUniversalMovementRules.Cancel`), the basic string (`CancelString`), ledge hang (`FighterLedgeRules.ClearHang`), freeze buffer. Grab: victim side `BeingHeld = 0; ClearGrabPartner()` (the grabber's `AdvanceGrabs` ends the hold when `victimVerb.BeingHeld == 0`, SS:2100-2105); if the grabber presses Defy, clear its own `GrabPhase`. Call `FighterActionRules.OnActionAccepted(.., FighterActionKind.Defy)` (add ordinal 13; ordinals are not serialized, FighterActionRules.cs:9-34) with `IsOffensive(Defy) = false` (REC).
6. Phase machine (6 invulnerable startup / blast on frame 6 / ~24 vulnerable recovery): a per-fighter countdown clocked in the same top-of-loop block, action-locked (buttons discarded each tick like a cast), gravity/physics continuing via a branch modelled on the tech-lock branch (SS:479-). A hit during the vulnerable recovery frames interrupts it normally (template: `FighterSpecialCastRules.IsInterrupted`).
7. Blast on frame 6: needs both fighters, so resolve it in FighterCombatSystem (pair access, SS:~1850), before the intents (`ApplyIntent` calls SS:1875-1876) so a same-tick strike loses (open ordering question). Opponent within ~2.5 u (fixed-point squared distance or axis box, no sqrt, no float) and not invulnerable and `Stocks > 0`: `HitstunFrames = 30`, push away (grounded: through the shove path `Velocity.x *= FighterKnockbackFeelRules.ShoveScale`, ES:962), cancel the victim's swing/cast/Foresight. Unblockable: do NOT route through `ApplyFighterHit`: with damage 0 and hitstun > 0 it reaches the block branch (`carriesImpulse`, ES:711-720) unless `attackClass == UltimateAttackClass`, which would drag Ultimate-origin meter/Rally rules along. REC a dedicated `FighterDefyBlastRules.Apply`: no damage, no meter either side, no echo, no Rally, no hitstop, no D1a Landed signal (so no Momentum), cannot kill. Aegis/barrier are untouched (`resolvedIncoming > 0` guard ES:686).
8. Constructs/projectiles/zones: not required by 3.2 ("the opponent"). Owners: FighterProjectileSystem, FighterPersistentObjectSystem, FighterZoneSystem. REC leave out of v1; planner question.
9. Heal = Rally echo cash-in with a 10 % max-HP floor: `heal = min(missingHP, max(round(EchoPool), ceil(MaxHP/10)))`; rounding with the existing `(raw + One/2)/One` idiom (ES:894); then `EchoPool = 0`, `EchoDrainPerFrame = 0`. Zeroing the pool forfeits its drain-time victim-Influence payout (TickCounters SS:1434-1444 pays only as it drains), consistent with "heal the HP, not the meter". With an empty pool it is a free 10 % heal (question).
10. Passive deletion list: lethal branch ES:800-837, DoT branch ES:1060-1076, `CanDefy`, `BeginDefyProtection`, the `DefyProtection*`/`DefyProcIdentity`/`DefyBarred` fields, `DefyHistoryUsed`, `defied` special cases (ES:856, 867), `IsDefyProtected` gate sites (ES:673, 1039, FighterKitRules.cs:285,353, FighterUltimateActivation.cs:300, SS:2817), `EndOnOffence` and its call (FighterActionRules.cs:95), the Sudden Death bar (SS:3535-3538), `Seal`. `SeedFighterDefyProtectionForTest` needs a replacement seam (`SeedFighterInvulnerabilityForTest` exists at FighterSimulation.cs:355).

### 2.3 State: what fits where (bytes)
Needed: `DefyFrames` (0 idle), a "blast fired" bit and a wrapping 4-bit presentation counter (like the wall-bounce counter, 323 `ActionFlags` bits 8-11): about 2 ints / 8 B.
- Option A (no new ID, REC): 323 gets one appended int `DefyFrames` plus `ActionFlags` bit 5 (blast fired) and bits 12-15 (wrapping Defy count). 323 grows 36 -> 40 B (well under 128). 323 is also zeroed at KO-platform and Sudden Death, which is exactly right for Defy. Delete the dead 312 Defy ints (or leave as spares). Net snapshot +8 B.
- Option B (new 324): gauge + 2 reuse + Defy frames/flags = 6 ints = 24 B/fighter, +48 B/snapshot.
- `verb.EchoPool` (310) already holds the heal source; no new field.
REC: Option A for both gauge and Defy: gauge in 310 slot-1, reuse in the two 305 ints, Defy in 323 (+4 B). No new component ID. Fall back to B only for named fields; the stated need would be "1 component, <= 32 B".

### 2.4 Rule answers
Legal from any state: see 2.2(2). Refusals: dead/out of stocks/respawn platform, < 50 Influence, captured by an A02 hold, Defy running, match not live (Countdown clears input, SS:236-244). Ignores A/S lockouts and does not arm them (5.4). Time Freeze and Suppression are Story only. Sudden Death: Influence starts at 0 there (SS:3469 unchanged); `DefyBarred` goes.

## 3. Input and protocol

- `GameplayButtons : ushort` (scripts/Core/PlayerInputFrame.cs:7-52): bits 0-11 used, 12 retired (Echo Step, reserved), 13 Grab, 14 DirectOrigin, 15 free (comment :51). Interact (8) and Pause (9) are set by InputManager; grep found no sim consumer (not exhaustively verified) - still, do not reuse wire bits.
- Add `Defy = 1 << 15`. ushort max 0xFFFF so `WriteUInt16LittleEndian` Held/Pressed/Released (:115-117) is unchanged: `PlayerInputFrame.SerializedSize` stays 12, `RollbackInputPacket.SerializedSize` stays 47 (scripts/Networking/RollbackProtocol.cs:37-38). `(int)input.Held` into the command (FighterSimulation.cs:518-520) stays non-negative. After this the wire has zero free bits.
- `ProtocolVersion = 4` (RollbackProtocol.cs:37). Layout is unchanged but bit-15 semantics and every hash change. Precedent: the 2026-10-16 update and Echo Step's removal kept v4 (LAN de-scoped). REC keep v4; if bumped, RosterSwapProtocolTests.cs:42 pins `(ushort)4`.
- InputManager.cs (partly another agent's): new `Actions.Defy` + `RemappableActions` entry (~:122) + `ActionToButton` (:1248-1260) + `AddIfHeld(ref held, GameplayButtons.Defy, ReadActionPressed(Actions.Defy, ..))` beside :1085-1092 + label key (:147-148 pattern) + project.godot InputMap (ruling 3.4: pad R3 = JoyButton 8, currently the Dialogue Log, which moves to Back 4; keyboard F). `FighterInputSystem.OnCommand` (SS:204-223) passes bits through unchanged. `DirectOrigin` is irrelevant (Defy is a plain direct bind).
- CPU `EdgeButtons` (CPU:71-74) lists edge-pulsed buttons; see 4.3.
EOF_MARK
## 4. CPU

### 4.1 Every read today (CPU = scripts/FighterSim/FighterCpuController.cs)
- Observation build CPU:711-716: `InfluenceRaw`, `SpecialOneCooldownFrames`, `SpecialTwoCooldownFrames`, `MovementCooldownFrames`, `AbilityLockoutFrames`, `SpecialLockoutFrames`; target `TargetInfluenceRaw` (:725), `TargetPressedButtons` (:724; threat read incl. Special1/2/Basic at :1597). Struct: CpuDecisionObservation.cs:55-58, 70-84.
- Special ladder `DecideCombat` CPU:1583-1660: close range Ultimate (:1615) / wait for viable Ultimate (:1617) / grab (:1623) / Special1 when `SpecialOneCooldownFrames <= 0` and `SpecialLockoutClearAtDelivery` (:1629-1632) / Special2 (:1633-1636) / BasicAttack; ranged Special1 (:1651-1654); movement ability on `MovementCooldownFrames` (:1655).
- `MobilitySpecialReady` CPU:1222-1226 (dormant: no character approves a mobility Special since the Tubman swap).
- Ultimate: `UltimateSetupViable` CPU:1674-1688 reads `InfluenceRaw >= MaxInfluence` (:101).
- Hitstun defense: `ApplyHitstunDefense` CPU:509-568 (exclusive Block hold; `Decide` returns None in hitstun, :944-949). A hitstun Defy decision must be a reflex-layer addition here (the layer "bypasses the decision schedule", :496-503), because a 30 f hitstun ends before Easy's 30-45 f reaction delay could deliver anything.
- `IsDefyUnspentAndUnbarred` (ES:327) has no remaining CPU caller (grep): the old Defy reserve valuation died with Echo Step.
- Story mirror: MirrorParadoxController.cs:679-687 fills `InfluenceRaw`, both cooldowns and lockouts from `PlayerController` (the Story agent's file; the contract is the observation fields).

### 4.2 "Spend pips like ready cooldowns" (G4.4)
Replace `observation.SpecialOneCooldownFrames <= 0` / `SpecialTwoCooldownFrames <= 0` (CPU:1225-1226, 1630, 1634, 1652) with one predicate `SpecialPipReady(slot)` = `obs.MomentumUnits >= PipCost && reuse(slot) <= band delay`. Keep `SpecialLockoutClearAtDelivery` and the `NextPercent() < SpecialOneClosePercent / SpecialTwoPercent / SpecialOneRangedPercent` draws in the same order. The cooldown test currently precedes the draw (CPU:1630-1631), so "draw only when ready" is preserved and Easy (zero percents) is unchanged. Add `MomentumUnits` (and keep the two existing fields as reuse delays) to `CpuDecisionObservation`, `Observe` (CPU:700-726) and the Mirror adapter. Hand-built test observations default to 0 gauge: REC make 0 mean "legacy: ignore the gauge" (as `SelfStringReachWidthPercent == 0` does) so they keep working. Expected behaviour: the CPU empties the gauge fast, then a Special every ~12 s when not hitting; less spammy than today's 7-13 s cooldowns.

### 4.3 Band-tuned Defy
- `CpuBandTuning` (CPU:~1800-2040; Easy/Medium/Hard statics, plus `BossOverride`): add `DefyPercent` (Easy 0), string-length trigger, corner flag, Ultimate-windup flag, keep-for-Ultimate flag. Code-owned, never a resource.
- Hook: a reflex branch in `ApplyHitstunDefense` beside the Block hold. Roll ONCE per hitstun instance with `NextPercent()` on the existing `newInstance` edge (CPU:512-518), the same pattern as `_hitstunHoldBlockActive`; "long string" needs a per-instance hit counter within N frames. Emit Defy directly in `held` (the reflex path writes the output frame) so `PlayerInputFrame.Create(.., previousFrame.Held)` derives the Pressed edge; for non-reflex uses (corner escape, Ultimate wind-up, from neutral) add Defy to `EdgeButtons` (CPU:71) so the schedule ring pulses it.
- Corner: reuse the observation's `HasStageBounds`, `LeftWallRaw/RightWallRaw`, `CorneredMargin` (as CPU:1684-1687 does for the target).
- Beat an Ultimate wind-up: needs the TARGET's A02 phase, absent from the observation. Add `TargetUltimateWindupFrames` via `ICpuWorldObserver.TryGetVerbState` (CpuVerbState already carries self `UltimateCasterBusy`; add the phase). Opponent info goes through the reaction buffer; Easy/Medium cannot react to a 20 f wind-up, Hard can. Timing: 6 invulnerable frames only cover the ~10 f activation strike if Defy is pressed roughly 14-19 f into the wind-up; if pressed early the CPU eats the strike in the 24 f vulnerable recovery. Question for the planner.
- Keep-for-Ultimate: `UltimateSetupViable` needs Influence >= 100 while Defy costs 50 (G2.1): Hard should not spend below 100 when the Ultimate setup is viable unless a Defy rule fires.
- RNG: extra `NextPercent()` calls shift every downstream draw for Medium/Hard. Mitigate: draw only when `InfluenceRaw >= 50 && DefyPercent > 0` (Easy never draws), once per hitstun instance. Seeded expectations (FighterCpuBehaviorTests, CpuVerbPolicyTests, RollbackReadinessTests CPU matches, FighterCpuSpecialTimingSmokeTests) will move.
- Mirror clone follows its band (`CpuBandTuning.BossOverride`); the Story side must emit Defy through `InputManager.SetInputSource`.

## 5. Tests that pin the affected behaviour
(R = rewrite, D = delete, A = adjust in place. Counts are `[TestCase]` per suite from grep; "refs" are matching lines.)

Special cooldowns / Momentum refund
- tests/Determinism/FighterHitConfirmTests.cs: 18 cases, ~43 refs; D1 hit-confirm cases stay, Momentum-refund cases R (est. 6-8).
- tests/Determinism/FighterTimeSystemsTests.cs: 4 cases; `EveryConnectingStringHitRefundsARunningCooldownInMatch` and friends (lines 26-93: cooldown 300, cap 120) R.
- tests/Determinism/FighterAbilityLockoutTests.cs: 18 cases, 16 refs; lockout stays; cooldown-seeded setups A (new seed seam).
- tests/Unit/AbilityLockoutRulesTests.cs: 5 (pure); mostly stays.
- tests/Unit/FighterLockoutRefusalRulesTests.cs: 5 (pure), 12 refs to the cooldown baseline: R.
- tests/Determinism/RequiemCooldownShaveTests.cs: 3 cases (9 refs): G4.2 disables the shave: D or R into a "does nothing until the kit pass" pin.
- tests/Determinism/FighterMatchFlowTests.cs (41 cases): SD reset assertions at lines 1353-1355 (S1/S2/Movement cooldowns 0) R to "gauge = 2 pips, reuse 0"; lines 905-921 (Defy not spent, `DefyBarred == 1`, Influence 0) R; dual-Defy trade lines ~930-991 D/R.
- Other cooldown refs (A, ~25 files, 185 refs overall): FighterSpecialTimingTests (18; 5), FighterSpecialRecoveryCancelTests (14; 4), FighterCombatContractW3Tests (5), FighterSimulationTests (23; 4), RollbackReadinessTests (8; 4), RollbackProtocolTests (4; 4), FighterVerbLayerTests (12; 2), UltimateActivationTests (2), JoanKit/MozartKit/ShakespeareKit/TubmanKit/TeslaLorentzChain/SplittingStrikeSpike/DivinePiercingLunge/FighterKnockdown/FighterLedge/FighterAirAttack/FighterPerfectBlock/CpuSelfActionState/CpuRecoveryMatrix/FighterCpuBehavior/RiftCollapseSim (1-2 each), tests/Unit/MirrorParadoxTests (34 cases; 3 refs). The 14 sim suites that press Specials 30 ticks apart (AGENTS 2744 note) will starve on a 2-pip start: seed the gauge.
- Story side (other agent): StoryHitConfirmTests (19 refs), StoryAirAttackTests (2).

Defy History (passive)
- tests/Determinism/DefyProtectedRecoveryTests.cs: 12 cases, 33 refs: D (replace with active-Defy suites).
- tests/Unit/DefySealStateTests.cs (4) and DefySealTests.cs (5): D (seal retired, ruling 4.2).
- tests/Determinism/FighterHitPipelineTests.cs: 7 cases, 9 refs: lethal-branch cases R/D.
- FighterVerbLayerTests (3 refs, defied hit no echo), FighterLedgeGetUpTests (4; uses `SeedFighterDefyProtectionForTest`), FighterCombatContractW3Tests (2), FighterTimeSystemsTests (2), FighterHitConfirmTests (1): R.
- ContentValidation: CoreMechanicCueCatalogTests (6 cases; add a Defy Critical cue slot per 4.1), FighterHudContentTests, GlassKitContentTests (defy icons), InfluenceMeterSourceScanTests (3): A.
- UI/Story (other agents): FighterHudSceneTests (12), FighterHudLayoutRulesTests, StoryHud* suites, TutorialCalibrationV76Tests (6), StoryDefenceOrderTests (12), SaveEnvelopeTests, StoryAttemptStateTests.
- New suites needed: Defy legality matrix (each early-continue state), blast radius/stagger/no-damage/unblockable/no meter, heal floor, hold release, vs A02 wind-up and hold, rollback replay + hash equality, CPU band matrix, input bit 15 round trip.

Stock-loss 75 % retention
- tests/Determinism/FighterSimulationTests.cs:81 `StockLossRetainsSeventyFivePercentOfInfluence`: R (-> 50 %).
- tests/Unit/InfluenceMeterRulesTests.cs:29-48, 78 (3 cases, pure): num/den 3/4 and 0.75f: R.
- tests/Unit/UltimateMeterTests.cs:23 `StockLossRetainsSeventyFivePercent` (3 cases): Story dead path; D with `ApplyStockLossRetention`.
- tests/ContentValidation/InfluenceMeterSourceScanTests.cs:118 (readers of `InfluenceMeterRules.StockLossRetention`): A.
- Code: `InfluenceMeterRules.StockLossRetentionNumerator/Denominator` (scripts/Combat/InfluenceMeterRules.cs:39-40) 3/4 -> 1/2; FighterMeterRules.cs:41-42,67-68 compute `influence * num / den`, so it is a constant edit that moves hashes.

Input bit layout / protocol
- PlayerInputFrameTests (5 cases; Dash reserved-bit round trip lines 12-28: add Defy), InputBindingSchemaTests.cs:600 (`SerializedSize == 12`), RosterSwapProtocolTests.cs:42-45 (`ProtocolVersion == 4`, packet size), RollbackProtocolTests.cs:20, FighterSimulationTests.cs:584 `TheReservedDashBitHasNoEffectOnSimulationState` (add a Defy-bit case), GamepadDefaultBindingTests (action-count floor; "no button shared by two actions": R3 = 8 vs Dialogue Log), GamepadOverrideRepairTests, PadMenu* and the Controls-tab tests: A.

CPU cooldown reads
- FighterCpuBehaviorTests (35 cases; refs incl. line 412), CpuVerbPolicyTests (4), CpuSelfActionStateTests (4), CpuMeleeBandTests (8), CpuRecoveryMatrixTests (11), FighterCpuHitstunDefenseTests (10; gains Defy rows), FighterCpuSpecialTimingSmokeTests (1; counts casts), MirrorParadoxTests (34): A. Hand-built observations need the gauge field (see 4.2).

## 6. Risks
- Determinism: integer units + `FP64.FromInt` only; the blast radius test in fixed point (squared distance or box, no sqrt); heal rounding via the existing FP64 idiom; CPU through the xorshift `NextPercent`, recorded-input path. A new `KLOTHO_DET002` warning is a bug.
- Hash moves: every Fighter hash moves (gauge, reuse, Defy, 1/2 retention, Sudden Death). No suite pins an absolute hash, but seeded CPU matches move.
- Snapshot size: Option A +8 B (323 appended int and 310/305 reuse), Option B about +48-64 B, on a 3,277 B baseline. Re-measure per PERFORMANCE_BASELINE.
- Rollback: Defy presentation (ring, "DEFIED", screen pulse, cue) must be driven by a wrapping counter edge-detected on a short forward step only (FU-KNOCK `IsForwardCounterStep` precedent), or a rollback correction replays the effect. The ~8 f world freeze: if it must freeze the simulation it is state; REC reuse `FighterVerbRules.ApplyHitstop` (310 `HitstopFrames`, already snapshot state) on the opponent/both, rather than a driver-only time scale.
- LAN de-scope (Package 7): bit 15 rides the existing packet; builds from before/after already desync (precedent), `ProtocolVersion` is the only pin.
- Wire full: after bit 15 `GameplayButtons` has no free bit; the next Fighter verb forces a packet-size/protocol change.
- Renaming 305/310 ints: do it, so a grep finds every stale reader; compile errors enumerate them.

## 7. Open questions for the planner
1. State layout: Option A (no new ID; gauge in 310, reuse in 305, Defy in 323) vs Option B (new 324)?
2. Gain for directional strikes, the air strike and the ledge attack (default 0.05 each; the ledge attack pays today as chain step 0 - pay or exclude)?
3. Hitstop: may Defy start on a hitstop-frozen tick (REC yes), does it clear the Defier's own hitstop, and is the 8 f world freeze sim state (hitstop) or driver-only?
4. Defy vs the Defier's OWN Ultimate activation (wind-up, active, 45 f whiff recovery, cinematic): refuse like `UltimatePressPayable`, or allow as a cancel?
5. Does the blast cancel the opponent's A02 wind-up, Special cast, grab startup/hold, Foresight stance? Does it push the opponent out of a hold on the Defier?
6. Blast vs constructs, projectiles and zones: ignore (REC) or push/destroy?
7. Same-tick ordering: Defy blast vs a strike landing the same tick (REC: invulnerable frames 0-5 and blast resolved before intents in FighterCombatSystem).
8. Ultimate timing: 6 invulnerable frames only beat the strike if pressed ~14-19 f into the 20 f wind-up; intended skill test? Should the Hard CPU get a timed reaction (needs the opponent's A02 phase in the observation)?
9. Heal timing (at start vs at the blast) and whether the 10 % floor applies when the pool is empty (free heal).
10. Regen during the respawn platform, Countdown, captured and A02 hold (today's cooldowns tick in the captured and knockdown branches; REC the same).
11. Confirm: stock loss keeps the gauge; Sudden Death sets 2 pips even if the fighter had more.
12. Protocol: keep v4 (REC) or bump to 5?
13. D10 not-yet flash for a no-pip refusal: flash or stay silent?
14. Hand-built CPU observation default for the gauge (REC 0 = legacy ignore).