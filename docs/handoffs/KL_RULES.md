# KL-RULES handoff (Phase A of the lingering-kit pass, 2026-10-31)

Branch `kl/rules` from `kl/integ`. Full run: `Failed: 0, Passed: 3907, Total: 3907` (2 m 15 s, lock held, orphans drained). Baseline 3879 + 28 new cases = 3907.

## API for Phase B

**Rulebook** `FTT.Combat.LingeringKitRules` (pure; distances the sim reads are `...Milli` ints, Story views are `...Units` doubles):
- charges: `CoilCharges` 3, `TurretCharges` 4, `TurretOverdriveBonusCharges` 1, `NestCharges` 3
- coil: `CoilOwnArcDamage/IntervalFrames/ReachMilli`, `CoilSyncedArcDamage` 8, `CoilSyncedArcHitstunFrames` 20, `CoilSyncedReachMilli/Units` 4
- discharge: `DischargeOwnerRangeMilli/Units` 8, `DischargeDamagePerCharge` 6, `DischargeHitstunFrames` 20, `DischargeCoilReachMilli/Units` 4, `DischargeMarkedReachMilli/Units` 8
- turret: `TurretOwnShotDamage/IntervalFrames/LifetimeSeconds`, `TurretOwnShotReachStoryUnits` 30, `TurretSyncedBoltDamage/HitstunFrames/SpeedMilli/SpeedUnitsPerSecond`, `TurretSyncedReachMilli/Units` 10, `TurretOverdriveIntervalPercent` 50
- nest: `NestOwnNip*`, `NestOwnReachPixels` 60, `NestSyncedLungeDamage/HoldFrames/VenomIntensityPercent/VenomSeconds`, `NestSyncedReachMilli/Units` 2.5
- Venom: `VenomDamagePerSecondPerIntensity` 3, `VenomTickFrames` 60, `VenomDamagePerTick(double)`, `VenomBlocksHeal(venomActive, isDefyHeal)`
- slow: `HeroSlowIntensity` 1.2, `HeroSlowLingerSeconds/Frames` 1.75/105, `BossSlowMoveMultiplierFloor` 0.7, `TimeDilationMoveMultiplier`, `BossTimeDilationMoveMultiplier`
- zones: `RiftCollapseDamage` 8, `VortexPullUnitsPerSecondStory`, `VortexPullMilliPerSecondSim` (7.0 each), `GoldenRatioPullPercent` 50, `GoldenRatioPullCapMilli/Units` 1.5
- Momentum: `LingeringMomentumEvent { Detonation, SyncedStrike, HoldCast }`, `MomentumUnitsFor(kind)` = 1440 / 360 / 360 (reads `BasicComboRules`)
- The Rift and Vortex radii are NOT in this file: `KitMotionRules.RelativityRiftRadiusUnits` and `KitReachRules.SandstormVortexRadiusUnits`, both 2.5. `KitMotionRules.RelativityRiftLingerFrames` is now 105.

**Sim view** `FTT.FighterSim.FighterLingeringKitRules`: `FromMilli`, `CoilSyncedReachSquared`, `TurretSyncedReachSquared`, `NestSyncedReachSquared`, `DischargeOwnerRangeSquared`, `DischargeCoilReachSquared`, `DischargeMarkedReachSquared`, `TurretSyncedBoltSpeedPerFrame`, `VortexPullPerFrame`, `GoldenRatioPullFraction`, `GoldenRatioPullCap`, `DistanceSquared`, `WithinReach(origin, target, reachSquared)` (2D circle).

**Marker.** Story: `HitPayload.CountsAsCombo` (default false). `BaseSpecial.NoteLandedHit` arms D3 for `Delivery == DirectHit || CountsAsCombo` (still needs `IsExecuting`, `ContactLanded` and the serial attribution). The payout is explicit and once per event: `PlayerController.PayLingeringMomentum(LingeringMomentumEvent)`. The marker does not set latch C, give echo or add hitstop. Sim: the marker is a call, `FighterLingeringKitRules.CountsAsCombo(ref cast, ref verb, hitSlot, sourceCastId, outcome, damage, payoutKind, armD3 = true, payoutAlreadyPaid = false)`. It arms 323 `FlagLandedDirectHit` only for the busy cast of `hitSlot` whose `SpecialCastId == sourceCastId`, and pays once; `payoutAlreadyPaid` lets a caller pay once per event across bodies.

**Story seam.** `PlayerController.OwnerHitLanded` (`event Action<OwnerHitLandedInfo>`; `PlayerController.Lingering.cs`). Raised once per landed (D1a) contact, only when `ContactLanded`: from `Hitbox.OnAreaEntered` right after the stamp, and from `BaseSpecial.Credit` (shape-query and ground-wave Specials). It carries `Target` (Hurtbox), `Payload`, `Landed` (always true), `Position`. It does NOT see zone ticks or construct nodes that call `Hurtbox.TakeDamage` directly (coil arc, nest bite); it DOES see the turret's pooled bolt (Construct delivery). `OwnerHitLandedInfo.TriggersSyncedStrike` is the plan 1.3 filter (landed, `DirectHit`, Basic or Special origin, not `CountsAsCombo`, not `throw`/`pulse`/`chain_lightning`, damage > 0); `IsBasicHit` too. Handlers run inside the physics-callback guard when raised from a Hitbox.

**Sim owed strike.** `FighterVerbComponent.OwedSyncedStrikes` (310, was `SpareInt1`; `SpareInt0` untouched). Written in `FighterCombatSystem.OnSwingResolved` (now `internal`) for every landed basic swing, and by `FighterLingeringKitRules.NoteOwnerSpecialHit` beside each `NoteLanded` for DirectHit Specials (reach thrust, projectile, burst, zone pulse, melee intent). Excluded: Ultimates, throws, construct hits, ticks, the Lorentz arc and chain, and Tesla's Lorentz Pulse zone pulse (the discharge replaces them). `FighterPersistentObjectSystem.Update` calls `TakeOwedStrikes` for every fighter right after `DespawnOwnedByFallenFighters` and discards the result: **KL-SIM should use the returned count there** (it is the only reader). Zone-pulse writes resolve after that system, so they are consumed next tick. Helpers: `NoteOwedStrike`, `OwedStrikes`, `TakeOwedStrikes`, `OwedStrikeCap` 255. Test seams in `FighterSimulation`: `SeedFighterStatusForTest(player, statusType, frames, intensity)` and `SeedFighterOwedStrikesForTest(player, owed)`.

**Heal gates.** Story `PlayerController.IsHealingBlocked` and `HealStory(amount, isDefyHeal = false)` (Defy passes true); sim `FighterDamageRules.CanHeal(in runtime)`.

## Files touched
New: `scripts/Combat/LingeringKitRules.cs`, `scripts/FighterSim/FighterLingeringKitRules.cs`, `scripts/Characters/PlayerController.Lingering.cs` (with `.uid`s). Edited: `HitboxSystem.cs` (marker), `Hitbox.cs` (seam call), `BaseSpecial.cs` (D3 condition, seam call in `Credit`), `StatusController.cs`, `EnemyController.cs`, `BossController.cs` (Venom constant, boss floor), `KitMotionRules.cs`, `KitReachRules.cs` (radii, linger frames), `RestorationFont.cs` (waits while poisoned), `PlayerController.Defy.cs` (Defy heal passes `isDefyHeal`), `FighterEntitySystems.cs` (CanHeal, orb and reclaim gates, owed writers, drain), `FighterKitRules.cs` (burst writer), `FighterSimulation.cs` (two seams), and three `.tres`: `einstein/special_2` (Rift), `cleopatra/special_2` (Vortex), `shakespeare/special_1` (Yorick), now `StatusIntensity` 1.2 and `StatusDuration` 1.75. The Rift's per-frame `RefreshLinger` reads those fields, so it still refreshes at 1.75 s.

**Hunks in the shared WIP files (all small):**
- `PlayerController.cs`: `ReclaimEchoPoolFraction` +2 lines (the heal-block early return before the debit, ~l.1912); `HealStory` gains `bool isDefyHeal = false` and +2 lines (~l.4260).
- `EnemyController.cs`: one line, the Venom tick (~l.2351), now `LingeringKitRules.VenomDamagePerTick`.
- `FighterSimulationComponents.cs`: `SpareInt1` renamed `OwedSyncedStrikes` with a doc comment (l.759).
- `FighterSimulationSystems.cs`: Venom tick damage and interval read the constants (l.1530, 1532); `OnSwingResolved` `private` to `internal` (l.2462) and +2 lines (owed write, l.2470); the melee-intent `NoteOwnerSpecialHit` call (+2, l.3213).
- Not touched: `BasicComboRules`, `FighterSimulationDriver`, `FighterGrabRules`, `en.csv`.

## Tests (+28 = 3907)
New: `LingeringKitRulesTests` 7 (pure), `FighterLingeringKitRulesTests` 11, `LingeringKitStoryTests` 6, `OwnerHitSeamTests` 4. Changed in place, no count change:
- `CleopatraContentTests.SandstormVortexResourceMatchesDesignSpecification` (1.75 s, 1.2, radius 150 px), `EinsteinContentTests` (renamed `RelativityRiftLingersAsTheHeroSlowAndIsThrownFiveUnits`; 2.5 u, 1.2), `ShakespeareContentTests.YoricksLament...` (1.75, 1.2): the new .tres numbers.
- `CleopatraKitTests.SandstormVortexPulls...` (radius reads `KitReachRules`), `StoryRiftCollapseTests` (150 px), `AbilityZoneTalentTests.ZoneRadiusMinor...` (165 px): the radii.
- `StatusControllerTests` (two cases, 98 to 97) and `BasicStringRiderTests.CleopatraFinisherMarks...` (35 to 37): Venom 3/s (the 0.5 mark rounds to 2 a tick).
- `BossControllerTests` (three cases, x0.5 to x0.7; the dash 300 to 420): the boss floor.
- `CleopatraUltimateTests` (renamed `StormLandsEightyFour...`, Venom total 12 to 18) and `UltimateActivationContentTests` (Cleopatra row 78/12 to 84/18): **Venom 3/s raises Wrath of the Nile's total from 78 to 84.** I kept the Ultimate's authored 2.0 intensity and moved the pins. To keep 78, set its finale intensity to 4/3. Decision for the orchestrator and KL-CLOSE (ABILITY_DATA.md, design master, ledger).

Fail-first: the changed pins failed with the old numbers on the first full run on the new code (their old-code evidence). For the new cases I mutated the tree (Venom constant 2, boss floor 0.1, both heal gates off, the three .tres reverted, both radii reverted, owed writer and seam no-op'd) and ran the new suites: 17 of 28 failed, covering the Venom, heal block, boss floor, slow, radii, owed-strike and seam-raise rules. Not detectable by that mutation set: the constant pins, the fixed-point view, the circle, the CountsAsCombo call and the persistent-system clear (all new API, no old-code behaviour), and `AWhiffRaisesNothing` (a guard that also passes on the old tree). The tree was restored with `git checkout` before these docs.

## Provisional / decisions
- The healing block gates `HealStory`: Mending, the orb, Feast and pickups get 0. The Font's trickle waits (clock and budget) while poisoned and resumes after: provisional (plan 1.8).
- The Lorentz Pulse zone pulse does not write an owed strike (Story consumers filter `pulse` / `chain_lightning` through `TriggersSyncedStrike`).
- `RelativityRiftLingerFrames` 30 to 105 so the .tres and the fallback agree.
- Nothing sets `CountsAsCombo` yet: the Story D3 / payout and the sim call are for SCON, SZONE and SIM.
- Not done: no consumer of the owed count; no Vortex pull or Golden Ratio change (constants exist, still 3 u/s); no enemy-ability slow touched.
- A cold worktree needs `--import` before the first test run (a cold cache gave a signature-2 crash with 207 failures). `.import` churn was reverted, not committed.
