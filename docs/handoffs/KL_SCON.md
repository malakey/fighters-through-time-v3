# KL-SCON handoff (Phase B, Story constructs, lingering-kit pass, 2026-10-31)

Branch `kl/scon` from `kl/integ` (Phase A, KL-RULES, included). Full run: `Failed: 0, Passed: 3938, Total: 3938` (2 m 28 s, lock held, orphans drained) = 3907 + 32 - 1.

## 1. API

**`FTT.Combat.ConstructStrikes`** (`scripts/Combat/ConstructStrikes.cs`, Story only, nothing under `scripts/FighterSim/` reads it):
- hitbox IDs `CoilSyncedArcHitboxID` ("coil_synced_arc"), `TurretSyncedBoltHitboxID` ("turret_synced_bolt"), `NestSyncedLungeHitboxID` ("nest_synced_lunge"), `CoilDischargeHitboxID` ("coil_discharge")
- `IsStrike(in HitPayload)` = `CountsAsCombo && Delivery == Construct`: the one test the victims key their exemptions on
- `IsSyncedStrike(in HitPayload)`: `IsStrike` with one of the three synced IDs (the Momentum payer)
- `InReach(construct, target, reachUnits, rangeMultiplier = 1)`: the 2D circle (plan 1.6)
- `Deliver(owner, target, in payload, out dealt)`: `TakeHit`, the D1a stamp from the receiver, the owner's Influence credit, then `owner.RaiseOwnerHitLanded` (which pays the synced Momentum)

**Nodes.** `TeslaCoilNode`, `LeonardoTurretNode`, `SerpentNestNode`: `Charges`, `MaxCharges` (turret 4, 5 with Overdrive; coil 3; nest 3). Each subscribes to its owner's `OwnerHitLanded` in `Initialize`, unsubscribes in `OnDespawn` and `_ExitTree`, and handles `info.TriggersSyncedStrike` + the circle reach. Charges are decremented before the strike is delivered; the construct fizzles through its destroy path at 0. `TeslaCoilNode.TryBuildDischarge(out HitPayload)`, `FinishDischarge()`, `EnemiesWithin(px)`; `LeonardoTurretNode.SyncedBoltsFired`, `BoltsFired` (now also counts instant and Extractor shots), static test seam `InstantStrikesForTesting`; `BoltsRemaining` is renamed `Charges`. New `ConstructChargePips` (code-drawn node, child "ChargePips" of the three construct scenes, below the HP bar).

**Pooled synced bolt.** `Hitbox.CountsAsCombo` (new field, stamped by `CreatePayload`) and `PlaceholderProjectile.ConfigureSyncedStrike(hitstunSeconds)` (construct hit + `CountsAsCombo` + the strike's hitbox ID). `Hitbox.IsProjectileHitbox` also matches the synced bolt ID.

**Victim rules.** A strike is a plain Construct payload with `CountsAsCombo`: (a) `EnemyController`: exempt from S2 chain proration (no chain count, no dim), charges no standard poise, never shortens a running stun (`ApplyStun(..., keepLongerStun)`), the elite V7.4 budget still charges; (b) `PlayerController` (the Level 13 clone): `ConstructStrikeKeepsCurrentStun` skips `ApplyStun` when the body is already stunned, thrown, tumbling or knocked down (a plain stun is extended by max), so a launch or tumble is never cancelled; (c) bosses ignore hitstun as before.

## 2. Behaviour shipped

- **Coil:** 3 charges; synced arc 8 dmg, 20 f hitstun, 4 u circle (x the Story range minor); own arc and fence unchanged and spend nothing.
- **Turret:** 4 charges (+1 Overdrive); synced bolt 8 dmg, 20 f hitstun, 24 u/s straight, 10 u circle; own shot 4 dmg every 180 f (rulebook, not the .tres) with the 30 u reach and LOS, no self-destruct, 15 s lifetime; the glide bolt spends a charge; Overdrive = +1 charge and an own shot at x0.5 interval against Extractors only (a direct environment hit at the own-shot damage x the Overdrive Extractor multiplier, taken only when no enemy is in range); Re-placement unchanged.
- **Nest:** 3 charges; own nip 3 dmg / 60 f / 0.15 s (9 f) hitstun / no Venom / 60 px; synced lunge 6 dmg, 60 f hold, Venom (1.0, 4 s, Story status lanes and Asp's Bite kept), 2.5 u circle.
- **Lorentz discharge** (`TeslaLorentzPulse.DischargeCoils`): replaces the mark chain (the chain arcs, `ChainArcHitstunFrames` and the three chain helpers are deleted). Each own coil within 8 u of Tesla with charges and a target (nearest enemy within the coil's 4 u, else the nearest Conductive-marked enemy within 8 u of the coil) bursts 6 x charges, 20 f hitstun, Construct delivery, `CountsAsCombo`, then fizzles; delivered and credited through `Credit(...)` so it arms D3 for the Pulse cast (`ConfirmAbilityHit` -> `NoteLandedHit`); one `Detonation` Momentum per Pulse, only if a burst landed. The mark is not consumed.
- **Synced Momentum:** 0.05 pip once per strike, paid in `PlayerController.RaiseOwnerHitLanded` for `IsSyncedStrike` payloads with damage (all three strikes pass it; the pooled bolt reaches it through `Hitbox.OnAreaEntered`, so a bolt that lands after its turret fizzled still pays).

## 3. Files and shared-file hunks

New: `scripts/Combat/ConstructStrikes.cs`, `scripts/Characters/Abilities/ConstructChargePips.cs` (+ `.uid`s), tests below.
Rewritten/edited: `TeslaCoilNode.cs`, `LeonardoTurretNode.cs` (rewritten whole), `SerpentNestNode.cs`, `TeslaAbilities.cs` (Lorentz Pulse region only), `scenes/constructs/{TeslaCoil,ClockworkTurret,SerpentNest}.tscn` (a ChargePips node).
`LeonardoAbilities.cs`, `CleopatraAbilities.cs` untouched (the Re-placement, Overdrive flag and glide bolt call sites needed no change).

Shared-file hunks (all small, additive):
- `PlayerController.cs`: one hunk, the hitstun block's condition in `OnHurtboxHit` gains `&& !ConstructStrikeKeepsCurrentStun(in hit)` (2 lines).
- `PlayerController.Lingering.cs` (KL-RULES' new partial): `ConstructStrikeKeepsCurrentStun` method, and 4 lines in `RaiseOwnerHitLanded` paying the synced Momentum.
- `EnemyController.cs`: `ApplyStun` gets a trailing `bool keepLongerStun = false` and the `_stunTimer` assignment becomes `keepLongerStun && !opensStun ? Max(...) : stun`; the `TakeHit` call site passes `keepLongerStun` and ANDs `!IsStrike` into `chargesStandardPoise`; `ResolveIntakeScale`'s `exempt` ORs `IsStrike`. (4 small edits, comments marked `KL-SCON`.)
- `Hitbox.cs`: `CountsAsCombo` field, its payload stamp, `IsProjectileHitbox` matches the synced bolt. `PlaceholderProjectile.cs`: `Setup` resets the marker, new `ConfigureSyncedStrike`.
- Not touched: `BasicComboRules.cs`, `en.csv`, anything under `scripts/FighterSim/`.

## 4. Tests

New suites (one `[TestSuite]` per file, all `[RequireGodotRuntime]`; helper `ConstructStrikeKit.cs` is not a suite): `StoryCoilSyncedStrikeTests` 9, `StoryTurretChargesTests` 7, `StoryNestStrikeTests` 5, `StoryLorentzDischargeTests` 7, `ConstructStrikeVictimTests` 4 = **+32**. Removed: `StoryLorentzChainTests.OnlyACoilWithinEightUnitsChainsAnEightDamageArc` (the chain is gone; covered by `TheOldMarkChainIsGoneSoAMarkedMobInThePulsePaysThePulseAndTheBurstOnly`) = **-1**. Net **+31** (3907 -> 3938 expected).

Changed in place, no count change: `LeonardoMidGlideBoltTests` (`BoltsRemaining` -> `Charges`; case renamed `TheTurretCarriesFourChargesAndFiveWithClockworkOverdrive`), `StoryTurretLineOfSightTests` (`Charges`), `StoryFirstSpecialTaxTests` (the Lorentz Pulse model is the pulse only: the discharge bursts are poise-exempt), `StoryLorentzChainTests` (header; the pulse-root, attraction and pulse-radius cases stay).

Fail-first evidence: the charges, strikes, nip, lunge, discharge and pips are new API (the old tree does not compile against these suites). The victim rules were checked by mutation: `ConstructStrikes.IsStrike` forced to false (the old victim behaviour) fails all four `ConstructStrikeVictimTests` cases. The no-chain case fails on the old tree by construction (it expects pulse + 18, the old tree gave pulse + 8).

## 5. Provisional values and choices

- Story construct own-shot numbers (turret 4 / 180 f, nest 3 / 60 f / 9 f) are read from `LingeringKitRules`, **not** the `.tres` (`leonardo/special_2.tres` still authors BaseDamage 6, interval 120, HitCount 4; `cleopatra/special_1.tres` BaseDamage 6): the `.tres` values are now unread by Story for those fields. KL-SIM/KL-CLOSE decide whether to align the `.tres` (content tests pin the old numbers, so I left them).
- The discharge does not consume the Conductive mark; a coil with no target keeps its charges; a coil whose burst target is invulnerable still fizzles (the burst is spent); the discharge target is the nearest in the coil's reach, preferring that over a marked enemy farther out.
- The turret's Extractor shot is a direct environment hit (no projectile), like the coil's Overdrive arc.
- A synced strike is Basic-class, Construct delivery, no knockback, no hitstop; a strike is blockable as a Basic hit on the clone.
- The turret's pooled synced bolt is spawned off the physics flush (`CallDeferred`) when raised from a Hitbox callback; a direct call spawns at once.
- Own turret shots at a stunned mob are still prorated after three hits (plain construct hit); only strikes are exempt.

## 6. Dossier risks hit / not done

- Hitstun vs launch (dossier A section 5): handled for mobs (max semantics) and the clone (skip while stunned/tumbling/knocked down). The clone's `HasStoryProjectileImmunity` check reads `HitboxID == "projectile"`, so a synced bolt (ID `turret_synced_bolt`) is not refused by it: not changed, no clone has that immunity today.
- Not done: the Fighter sim half (KL-SIM); the pip row in the sim driver; no bot/telemetry changes (the bots read no constructs); `en.csv` descriptions (KL-CLOSE).
