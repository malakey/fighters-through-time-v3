# KL-SIM handoff (Phase B of the lingering-kit pass, 2026-10-31)

Branch `kl/sim` from `kl/integ` (Phase A included). Full run: see the plan section 9 entry (3907 + 25 = 3932 expected).

## API
New file `scripts/FighterSim/FighterLingeringKit.cs`:
- `FighterConstructCharges` (`IsChargeConstruct`, `InitialCharges` coil 3 / turret 4 / nest 3 else -1, `HasCharge`, `Spend`). Charges are 303 `RemainingAttacks`; no component grew, no ID allocated, 310 `SpareInt0` untouched.
- `FighterSyncedStrikeRules.Resolve(ref Frame, ownerID, owed)`: per owed hit, each own coil / nest / turret with the opponent inside its synced 2D circle fires once and spends a charge. Coil arc 8 / 20 f, nest lunge 6 / 60 f + Venom 1.0 for 240 f, turret bolt = a new projectile with type slot digit `SyncedBoltSlot` 6, 8 dmg, 20 f, 24 u/s aimed at fire time. `IsSyncedBolt`.
- `FighterLorentzDischargeRules.Discharge` (called from the zone system on the first update of Tesla's Pulse zone; flag 309 `ExecutionFlags` 8).
- `FighterVortexRules` (`Step`, `End`, `IgnoresPull`; inside bit in 309 `CollapseTargetMask`; payout flag 4).
- `FighterSpiralRules` (`Pull`, flags 2 first tick done / 4 paid, `FirstTickHitstunFrames` 6).
In `FighterLingeringKitRules`: `OwedFinisherBit` 256 (`NoteOwedFinisher`, `TakeOwed(out finisher)`), `Strike(...)` (blockable construct-class hit, no knockback / hitstop / Rally). `FighterDamageRules.ResolveFighterHit` gained a last parameter `preserveStun`: hitstun only lengthens (max), the hit-1 block-cancel gate is not re-armed and a tumble is not ended. `FighterRiftRules`: `TryCollapse(..., castId, armsD3)`, `TryCollapseOnFinisher`, `FlagCollapseArmsD3` 16; the collapse Launch is now an 8-damage Special hit (Detonation). `LingeringKitRules.NestSyncedLungeVenomFrames` (240) added for the sim.
Removed: `FighterLorentzChainRules` and `FighterZoneSystem.TryResolveLorentzChain` / `CoilArcDamage` (decision 25). `FighterConductiveRules.TryConsumeChain` stays (unused by the sim now).
Test seams in `FighterSimulation` (all deterministic-state writers): `MutateFighterForTest`, `MutatePersistentForTest`, `MutateZoneForTest`, `SpawnPersistentForTest`.

## Behaviour
- Own attacks: coil 5 / 120 f (unchanged), turret 4 / 180 f, nest nip 3 / 60 f, 9 f hitstun, no Venom (the persistent object carries no status). None spends a charge. The turret no longer self-destructs; 0 charges destroys any of the three through the existing path. The glide bolt spends a charge.
- Momentum: strike 360 once per strike (`CountsAsCombo`, `armD3: false`: the triggering hit already arms D3 as a direct hit; a basic hit is no cast). Discharge 1440 once per Pulse, arms D3 for the Pulse cast. Rift collapse 1440 once, arms D3 only when an E=mc2 burst started it (its `CastId` is stored on the rift). Vortex final pulse 1440 once, arms D3. Golden Ratio 360 once per cast, no extra arming.
- Rift: radius 2.5 comes from `KitMotionRules` (already). Second trigger: the finisher site (`FighterSimulationSystems.cs`) sets the owed finisher bit on a `Landed` hit 3; the persistent system collapses an own rift whose circle holds the target's position. Collapse damage 8 is dealt on the launch frame.
- Vortex: pull 7 u/s, x only (as before), skipped for roll travel and all air-dodge phases and while invulnerable; its pulses carry no status; the lingering slow (the .tres 1.2 / 105 f) is applied when the victim leaves the box or the zone ends with him inside.
- Golden Ratio: each pulse pulls half the way, capped 1.5 u, floor-bound when grounded; tick 1 gets 6 f hitstun (Story's churn value; sim ticks had none).
- Driver: new partial `FighterSimulationDriver.Lingering.cs`: charge pip row above the HP bar; the strike / burst / fizzle effect is edge-detected as a charge count below the lowest already presented for that entity ID (rollback and rematch safe); the Rift collapse burst once per rift entity. Two call lines in `FighterSimulationDriver.cs`.

## Shared-file hunks
- `FighterSimulationSystems.cs`: 2 lines at the finisher site (`NoteOwedFinisher`).
- `FighterSimulationDriver.cs`: 2 lines (`SyncLingeringProxy` in the persistent loop, `SyncLingeringZones()` after the zone loop).
- `FighterSimulationComponents.cs`, `BasicComboRules.cs`, `FighterGrabRules.cs`, `en.csv`, Story files: untouched.
- Other sim files edited: `FighterEntitySystems.cs`, `FighterKitRules.cs`, `FighterSimulation.cs`, `FighterLingeringKitRules.cs`, `scripts/Combat/LingeringKitRules.cs` (one const).

## Tests (+25)
New: `LingeringConstructSimTests` 15, `LingeringZoneSimTests` 9, and `TeslaLorentzChainTests` (5) was rewritten and renamed `TeslaLorentzDischargeTests` (6). Changed in place: `CleopatraKitTests` (nest: no Venom, nip 3, 9 f; Vortex pull 7 u/s timings), `LeonardoKitTests` (Golden Ratio tick-1 hitstun; the turret test renamed `...FiresFourOwnShotsAndOutlivesThem`, 4 per shot, still alive; rollback test: turret survives), `LeonardoTurretSimTests` (own shot spends no charge), `RiftCollapseSimTests` (collapse deals 8, credits meter), `TeslaKitTests` (the Pulse now discharges the coil: 95 - 12 - 18).
Fail-first: I mutated the new code (strike resolver, discharge, Vortex step and Golden Ratio pull disabled) and ran the three new suites: 21 of 30 failed. The rest are the old-code-by-value cases (own-attack charge, turret lifetime, nest nip, finisher collapse), which fail against the old numbers (-1 charges, self-destruct, 6 dmg Venom bite, 0-damage collapse) by construction.

## Provisional / decisions for the orchestrator
- A coil with no recipient does NOT discharge or fizzle (plan wording is ambiguous); align KL-SCON.
- The sim Vortex has no final launch (never had; the D03g comment assumed one). The marker rides the final pulse; no launch added.
- The Vortex pull is horizontal only. Golden Ratio tick 1 hitstun 6 f is my choice; ticks are now blockable on tick 1 (hitstun makes them carry impulse).
- Synced strikes use `AttackClass` Basic; discharge bursts Special class.
- Not done: no Cataclysm change (it never read charges). The CPU / Mirror unchanged.
