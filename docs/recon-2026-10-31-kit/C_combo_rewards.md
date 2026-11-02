# Recon C: what "counts as landed" means today, and what it would take to extend it

Scope: committed HEAD `ce813449` (read with `git show HEAD:<path>`; the grab WIP in the working tree was ignored). Read-only. Decision source: `docs/recon-2026-10-31/DECISIONS_kit_lingering.md` decision 3 (holds and cash-outs count as landed and earn Momentum like a basic swing; ordinary ticks and a construct's own slow attack do not), 9, 10, 12-14, 16, 18.

## 0. Headline findings

1. "Landed" is three separate things, each keyed differently.
   - D1a contact-landed: the receiver's report (`HitPayload.ContactLanded`; Story `Hurtbox.ReportLanded/DidLand`; sim `FighterHitOutcome.Landed|Absorbed` with damage > 0). Root predicate, already delivery-agnostic.
   - D1 latch C plus Momentum: basic swings only, both modes.
   - D3 cancel flag: DirectHit-delivery contacts of the current cast only.
2. The delivery enum (`DirectHit/Tick/Construct/Hazard`) is the only switch the Special-side rules read. The Lorentz chain arc and Golden Ratio already are DirectHit, so they already arm D3. The Rift collapse, Vortex final launch and every construct hit do not.
3. Nothing pays Momentum except basic swings: Story `NoteSwingLanded` (PlayerController.cs:2926) and sim `OnSwingResolved` (FighterSimulationSystems.cs:2460-2475). A cash-out payout is a new call at each site plus a once-per-trigger guard.
4. The D1 latch C is moot for any Special-sourced cash-out (section 1); leave it alone.
5. Construct deliveries are NOT exempt from S2 chain proration or standard poise today (section 7).

## 1. D1 hit-confirm latch C (opens the Special gate)

| | Story | Sim |
|---|---|---|
| State | `_swingLanded`, getter `SwingLanded` (PlayerController.cs:846) | `FighterRuntimeComponent.SwingLanded`, 305 D1 word bit 16 (FighterSimulationComponents.cs:621) |
| Cleared | every swing start | `FighterAbilityLockoutRules.ArmSwing` (FighterAbilityLockoutRules.cs:90-96), `Clear` (Sudden Death) |
| Set | `OnMeleeHitConfirmed` (PlayerController.cs:1735): `payload.ContactLanded && basicSwing` (`IsBasicStringHitbox`) | `OnSwingResolved` (FighterSimulationSystems.cs:2460): Landed/Absorbed and damage > 0; `NoteSwingLanded` true on 0 to 1 edge only |
| Scope | per swing, first landed contact, however many bodies | per swing, one attempt (`FlagHitResolved`) |

Gate is `SpecialGateFrames(A, S, C) = C ? S : A`. A and S are both armed to 30 by every Special acceptance (`ArmAbility`, FighterAbilityLockoutRules.cs:98-102); a swing arms only A.

Consequences of letting a cash-out or construct strike set C:
- During or after a Special cast (every Special-sourced cash-out: Rift collapse from E=mc2, Vortex final launch, Lorentz discharge, Golden Ratio): S is already 30, so C true gives S = 30, same as A. No-op. Special to Special stays at 30.
- During a basic swing's lockout (a synced strike triggered by a landed basic hit; Einstein's finisher collapse): the triggering hit already set C. No-op.
- It would only matter for a strike with no hero hit behind it, which decision 3 excludes; decision 18 triggers strikes on a landed hit.
- Risk if forced: a strike landing after a whiffed swing would open the gate early and defeat "a whiff still locks 30". Recommend not touching C.

## 2. D3 Special recovery cancel

| | Story | Sim |
|---|---|---|
| Flag | `BaseSpecial.LandedDirectHitThisExecution` (BaseSpecial.cs:150), cleared in `TryExecute` (:67) | 323 `SpecialFlags` bit 8 `FlagLandedDirectHit` (FighterSpecialCast.cs:166) |
| Armed by | `NoteLandedHit` (BaseSpecial.cs:169-174), first statement of `ConfirmAbilityHit` (:372). Needs `IsExecuting`, `ContactLanded`, `Delivery == DirectHit`, `SourceExecutionSerial` 0 or equal to current `ExecutionSerial` (R12) | `FighterSpecialCancelRules.NoteLanded` (FighterSpecialCancel.cs:124-152). Needs `sourceCastId == cast.SpecialCastId` (MM-FIXB2C), Landed/Absorbed, damage > 0, cast busy and `SpecialSlot == hitSlot`, contract `Delivery == DirectHit` |
| Read | `TryFireSpecialCancel` (PlayerController.cs:3088) | `FighterSpecialCancelRules.LandedDirectHit` (FighterSpecialCancel.cs:105) |
| Call sites | kit shape queries via `Credit`; `Hitbox.OnAreaEntered` via `ConfirmAbilityHitboxHit` (PlayerController.cs:1786) | projectile contact FighterEntitySystems.cs:2206 (not constructBolt, not UltimateOrigin), burst :3348, zone pulse :3209 (slot 1/2, not Ultimate zone), Lorentz arc FighterKitRules.cs:193 and :594, FighterSimulationSystems.cs:3028 |

Decided cash-outs versus the hero's own cast recovery:

| Cash-out | Source cast | Arms D3 today |
|---|---|---|
| Lorentz discharge (new) | Tesla Special 2 itself | Yes if built on the chain-arc path: Story arc is already `Stamp(..., HitDelivery.DirectHit)` (TeslaAbilities.cs ~314), sim arc calls `NoteLanded(... SlotSpecialTwo ...)` (FighterKitRules.cs:594) |
| Rift collapse | the E=mc2 cast, or none (finisher) | No. `rift_collapse` is 0 damage (EinsteinRelativityRift.cs ~260-285) which fails the D1a zero-damage rule, and the Rift's delivery is Tick |
| Vortex final launch | Cleopatra Special 2 | No. Tick contract, `NoteLanded` returns false |
| Synced construct strike | none (fires in the hero's hit) | No (Construct; sim excludes `constructBolt`). If the triggering hit is the hero's own Special's, that hit already arms it |
| Golden Ratio ticks | Leonardo Special 1 | Yes already (authored DirectHit) |

So D3 needs new arming only for the Rift collapse and Vortex final launch, and only if they fire in the same caster's recovery; they need a nonzero-damage payload (or a relaxed zero-damage rule for this kind) and a delivery D3 accepts.

## 3. Momentum gain

`BasicComboRules.MomentumGainUnitsFor(variant, step)` (BasicComboRules.cs:836, sim) and `MomentumGainUnitsForHitbox(id)` (:847, Story). Units: 7200 per pip. String hits 1-2, air strike, Up-Attack, Down-Air, ledge attack 360 (0.05); finisher `combo_3` 1440 (0.2); any other ID returns 0 (throws, constructs, `rift_collapse`, `chain_lightning`).

| | Story | Sim |
|---|---|---|
| Entry | `NoteSwingLanded` then `GainMomentumUnits` (PlayerController.cs:2926-2930) | `FighterMomentumRules.Gain(attackerVerb.MomentumUnits, units)` (FighterSimulationSystems.cs:2471) |
| Once rule | `_swingLanded` latch | `SwingLanded` 0 to 1 edge |
| Bosses | pay (receiver's `ContactLanded`) | n/a |

How a non-swing source could pay:
- Story: call `GainMomentumUnits(n)` from the cash-out site via `Owner`. KitAlignmentContractTests only bans literal `AddInfluenceFromDamageDealt(` and bare `new HitPayload {` under `scripts/Characters/Abilities`, so a payout is allowed; best home is `BaseSpecial.Credit`/`ConfirmAbilityHit` keyed on a payload flag so kits do not each pay.
- Sim: every relevant site (zone pulse, persistent hit, projectile, Lorentz arc) already holds `ref attackerVerb`, so `attackerVerb.MomentumUnits = FighterMomentumRules.Gain(...)` sits beside `NoteLanded`. Put new constants in `BasicComboRules` (one canonical value).
- The once-per-trigger guard is the real design item. The swing latch cannot be reused: a collapse hits several bodies, the chain can fire two arcs, a string can spend three construct charges. Options: once per cash-out event, once per triggering hit (decision 18), or once per Special cast (sim `SpecialFlags` used bits 1,2,4,8,16; Story a bool on `BaseSpecial` cleared in `TryExecute`).
- Natural amounts: held or synced strike = string hit = 0.05 pip; detonation (collapse, final launch, discharge) = finisher = 0.2 pip. A synced strike paying on top of the hit that triggered it double-pays that exchange; suggest 0, or one 0.05 per triggering swing.

## 4. Rally echo and Influence credit for constructs and zones today

`HitClassification.CollectsEcho(delivery) => delivery == DirectHit` (HitboxSystem.cs:79).
- Hitbox-delivered payloads (incl. construct): Hitbox.cs:292-296 `AddInfluenceFromDamageDealt(dealt, collectsEcho: CollectsEcho(delivery), ultimateOrigin)`. Construct hits earn Influence, no echo.
- Kit shape queries: `BaseSpecial.CreditDealt` (BaseSpecial.cs:505), same rule.
- Story zone tick: `PlaceholderZone.CreditOwner` (:252) uses constant `TickDelivery`, so zones earn Influence only, no echo.
- Sim `collectsEcho:false` at FighterEntitySystems.cs:1054 (hazard), :2190 (`!constructBolt`), :2301 (persistent hit), :2378 (fence), :3331 (zone pulse, true only for the Requiem burst), :3477 (spiral expiry); FighterUltimateRules.cs:180; FighterKitRules.cs:333. The Lorentz arc leaves the default true, consistent with Story's DirectHit arc.
A new kind must default to false in `CollectsEcho` to keep "only Momentum".

## 5. Hitstop (should a "real hitstun" synced strike freeze?)

Hitstop (shared freeze) and hitstun (victim stun) are separate; constructs carry hitstun but no hitstop.
- Victim skip: `StoryEnemyCombatRules.VictimSkipsHitstop` (Tick, Construct, Hazard, or `ExemptFromHitstop`), read at EnemyController.cs:2706 and BossController.cs:1972; `DealtHitShakes` (~:497) uses the same predicate.
- `Hitbox.CreatePayload` stamps `ExemptFromHitstop = Delivery == Construct` (Hitbox.cs:206); coil, turret and nest payloads set it explicitly (TeslaCoilNode.cs:312, LeonardoTurretNode.cs:279, SerpentNestNode.cs:169).
- Caster freeze: `AbilityHitFreezesCaster` needs DirectHit, not Exempt, Special/Ultimate origin (PlayerController.cs:1801-1805); `ConfirmAbilityHit` gives VFX/haptics/caster freeze only for `dealt > 0 && DirectHit` (BaseSpecial.cs:374).
- Sim: `appliesHitstop` is an explicit arg at every site; false for constructs, fence, spiral expiry, zone pulses and the Lorentz arc (FighterEntitySystems.cs:2191, 2302, 2379, 3332; FighterKitRules.cs:589); applied only when target alive and damage > 0 (:849).
Recommendation: the triggering hit already froze both parties, so a synced strike should NOT freeze again (keep Exempt/false) but keep real hitstun (Story coil arc is 0.15 s, Rift collapse 30 frames, sim `ArcHitstunFrames`, `NestBiteHitstunFrames` 10, `FenceHitstunFrames`); lengthen it by authoring, not by hitstop.

## 6. HitDelivery: values and every branch

Enum (HitboxSystem.cs:50): DirectHit=0, Tick=1, Construct=2, Hazard=3. Authored on `AbilityData.Delivery` ([Export], AbilityData.cs:49) and `Hitbox.Delivery` (Hitbox.cs:46); `.tres` store the ordinal.

| Consumer | file:line | DirectHit | Tick | Construct | Hazard |
|---|---|---|---|---|---|
| `CollectsEcho` | HitboxSystem.cs:79 | yes | no | no | no |
| `IsProrationExempt` | StoryEnemyCombatRules.cs:86 | not exempt | exempt | NOT exempt | exempt |
| `specialTick` (Special-class Tick joins chain) | EnemyController.cs:2931 | | joins | | |
| `VictimSkipsHitstop` / `DealtHitShakes` | StoryEnemyCombatRules.cs ~377 / ~497 | freeze | skip | skip | skip |
| `IsPunishEligible` | :625 | yes | no | no | no |
| `IsPlayerDealt` | :505 | yes | yes | yes | no |
| D3 `NoteLandedHit`; `ConfirmAbilityHit` feedback | BaseSpecial.cs:171, 374 | yes | no | no | no |
| `AbilityHitFreezesCaster` | PlayerController.cs:1803 | yes | no | no | no |
| `Hitbox.CreatePayload` Exempt | Hitbox.cs:206 | | | exempt | |
| `WithAbilityContract` ShieldBreaker | BaseSpecial.cs:484 | | | never breaker | |
| sim `NoteLanded` | FighterSpecialCancel.cs:149 | yes | no | no | no |
| Observers only | PlaytestBots.cs:221, PlaytestTelemetry.cs:1052, MirrorParadoxController.cs:631, PlayerController.DamageObservation.cs:92 | | tick = not a hit | | hazard |
| `PlaceholderZone.TickDelivery` | PlaceholderZone.cs:47 | | const | | |
| `PlaceholderProjectile` | :82 turret bolt Construct, :206 DirectHit | | | | |

Sim: `FighterAbilityHitData.Delivery` is an int projected by `FighterLoadoutFactory` (FighterLoadoutFactory.cs:137); its only sim read is `NoteLanded`. Everything else in the tick uses explicit bool args. Loadout data, not snapshot state: no hash or component change.

A new value (append `SyncedStrike = 4`, never renumber) would touch: the enum and `CollectsEcho` (default false); `IsProrationExempt`, `VictimSkipsHitstop`, `DealtHitShakes`, `IsPunishEligible`, `NoteLandedHit`, sim `NoteLanded`, `AbilityHitFreezesCaster` (each needs an explicit decision); tests `AbilityHitContractTests` (:78-80 expected delivery per ability, :147-150 CollectsEcho pins) and `KitAlignmentContractTests` (:155-166). No serialization impact (ordinal int in `.tres`, int in sim loadout, nothing in saves or snapshots). Reusing DirectHit would wrongly enable echo, hitstop, Punish and caster freeze; reusing Construct keeps the exemptions but cannot arm D3 or pay Momentum. A separate flag (`HitPayload.CountsAsCombo` plus one bool on `FighterAbilityHitData`) is the lowest-touch route because it leaves the existing branch table intact.

## 7. Story poise, stagger and diminishing special stun

Mobs only. `BossController` ignores `HitstunDuration` entirely (BossController.cs:128), so bosses take damage/knockback/status but never stun.

`EnemyController.ApplyStun` (EnemyController.cs:2157-2230), reached from :2754-2780:
- `IsStaggerArmored` (36 f getup armor, Armored Recovery) returns early: damage lands, no stun.
- `StunResistance` scales duration.
- Diminishing special stun: if `fromSpecial` (`AttackClass == Special`), a stun within 4 s of the previous special-sourced stun is x0.5 and refreshes the window. A Special-class coil arc (the Lorentz chain arc is Special/DirectHit) would be halved after a prior Special and would refresh the window against the next real Special. Basic-class construct hits avoid it.
- Elite budget 2.0 s: every applied stun accrues; a trip starts Armored Recovery and replaces the stun.
- Standard poise 2.325 s (drain 4 s/s): `ChargesStandardPoise` (StoryEnemyCombatRules.cs:234) is true for everything except Ultimate class/origin, so a synced strike's stun charges poise like any hit, x1.5 only for a taxed second-or-later Special. Three charges spent in one string likely trip Armored Recovery; tune stun, or mark the kind poise-free.
- S2 proration: `ResolveIntakeScale` (EnemyController.cs:2924-2965). Construct is not exempt, so a construct strike increments `_confirmChainHits` (x0.85 after 3 free, floor 0.5), and a Special-class one is classed by `ClassifyChainSpecial` (first-Special slot by `AttackID`, otherwise x0.6 when it lands on hitstun). A strike on the same frame as the hero's hit always lands on an already-stunned mob, so expect dimmed damage and a shortened free-hit run. Decide whether to exempt the new kind like Tick.
- Punish window: DirectHit only, so synced strikes never trigger it.
- Knockback: constructs author zero (impulse-free since 2026-08-11); no wall bounce or finisher carry.

Sim: no poise/armor (`EnemyStaggerRules` must stay out of `scripts/FighterSim`); the synced strike's strength is just its authored `hitstunFrames`, with `launches:false` giving a grounded stun.

## 8. Tests that would fail or need edits

| Test | Why |
|---|---|
| `AbilityHitContractTests` (:78-80, :115-150) | expected delivery per ability; CollectsEcho pins for all four values |
| `KitAlignmentContractTests` payload scan | bans bare `new HitPayload {` and literal `AddInfluenceFromDamageDealt(` under `scripts/Characters/Abilities`; strikes must use `Stamp`/`StampConstruct` and `Credit` |
| `KitAlignmentContractTests` damage-composition scan | strike damage must use `StoryAbilityDamage` / `StoryAbilityHitboxDamage` only |
| `InfluenceMeterSourceScanTests` | bans Story-only Momentum identifiers (`StoryMeterGainFactor`, `MomentumRegen`, `MomentumRegenMultiplier`, `LegacyUnlockSchedule`) under `scripts/FighterSim`; shared constants in `BasicComboRules` are fine |
| `ResonanceReaderCoverageTests` | FighterSim must not name Resonance/Story-perk types |
| `FighterSpecialRecoveryCancelTests` (`OnlyAContactFromTheCurrentCastArmsTheLandedDirectHitFlag`, `OnlyTheKnownDirectHitSlotsCanOutliveTheReuseDelay`) | pin DirectHit-only and cast-id attribution; the known-slot list changes if a discharge or collapse arms D3 |
| `FighterHitConfirmTests`, `StoryHitConfirmTests` | pin C per swing |
| `FighterMomentumGaugeTests`, `StoryMomentumGaugeTests`, `MeterDefyRulesTests` | pin gains and "constructs/zones pay nothing" |
| `StoryEnemyCombatRulesTests`, `StoryEnemyCombatTests`, `EnemyControllerTests`, `StoryFirstSpecialTaxTests`, `StoryPunishWindowTests` | proration, poise, hitstop, chain role, Punish |
| `SpecialCooldownDataTests` | Special `.tres` must keep `CooldownDuration = 0.0` |
| Kit suites (Tesla, Leonardo, Cleopatra, Einstein), `RollbackReadinessTests` | hashes move; cases counting construct hits or charges change |
| `PlaytestCombatStatsTests` | Momentum `gained_by` attribution files unknown sources under `other` |

Unverified: exact line numbers marked "~" are from partial views; I did not read the Cleopatra Vortex final-launch code or the sim persistent-object trigger path in full, and did not run any tests.

## 9. Recommendation: how to define "counts as landed"

1. Keep D1a (receiver-reported, damage > 0, Landed or Absorbed) as the only definition of "landed", and add one orthogonal marker (`CountsAsCombo` on the payload, one bool on the sim contract) set only on cash-outs, held Golden Ratio ticks and synced strikes. It gates exactly two consumers, D3 arming and a new Momentum payout; leave `HitDelivery`, `CollectsEcho`, hitstop and the C latch untouched.
2. Pay Momentum once per event, not per body: detonations 0.2 pip, holds and synced strikes 0.05, with a once-per-cast guard for Specials and once-per-triggering-swing for constructs so strings cannot farm; no echo, no hitstop.
3. Give synced strikes real hitstun, exempt the marker from chain proration and standard poise, and note a zero-damage collapse needs real damage or an explicit relaxation of the D1a zero-damage rule to arm D3.
