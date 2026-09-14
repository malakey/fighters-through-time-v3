using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

[TestSuite]
[RequireGodotRuntime]
public class FighterSimulationTests {
    [TestCase]
    public void IdenticalInputsProduceIdenticalHashesForTwoThousandTicks() {
        var first = new FighterSimulation(seed: 42);
        var second = new FighterSimulation(seed: 42);

        for (int tick = 0; tick < 2000; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            long firstHash = first.Advance(p1, p2);
            long secondHash = second.Advance(p1, p2);
            AssertThat(firstHash).IsEqual(secondHash);
        }
    }

    [TestCase]
    public void FullStateRestoreResumesTheSameHashSequence() {
        var uninterrupted = new FighterSimulation(seed: 99);
        for (int tick = 0; tick < 90; tick++) {
            uninterrupted.Advance(InputFor(0, tick), InputFor(1, tick));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();

        var restored = new FighterSimulation(seed: 99);
        restored.RestoreFullState(snapshot);
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        for (int tick = 90; tick < 240; tick++) {
            long expected = uninterrupted.Advance(InputFor(0, tick), InputFor(1, tick));
            long actual = restored.Advance(InputFor(0, tick), InputFor(1, tick));
            AssertThat(actual).IsEqual(expected);
        }
    }

    [TestCase]
    public void CorrectedPredictedInputsRollbackAndConverge() {
        var authoritative = new FighterSimulation(seed: 7);
        var predicted = new FighterSimulation(seed: 7);

        for (int tick = 0; tick < 80; tick++) {
            PlayerInputFrame p1 = InputFor(0, tick);
            PlayerInputFrame p2 = InputFor(1, tick);
            authoritative.Advance(p1, p2);
            if (tick >= 20 && tick < 30) predicted.AdvanceWithPredictedPlayerTwo(p1);
            else predicted.Advance(p1, p2);
        }
        AssertThat(predicted.CurrentHash == authoritative.CurrentHash).IsFalse();

        for (int tick = 20; tick < 30; tick++) {
            AssertThat(predicted.CorrectPlayerTwoInput(tick, InputFor(1, tick))).IsTrue();
        }

        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
    }

    [TestCase]
    public void StockLossRetainsSeventyFivePercentOfInfluence() {
        // One 100-damage opener (125 x tesla's 0.8 shape) fells the 100 HP
        // victim outright. A lethal hit accrues no Rally echo (V7.1), so the
        // victim's meter credit is exactly damage/4 = 25 — and the stock loss
        // must retain 75% of it: 18.75.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildLethalOpenerAttacker()),
            FighterLoadoutFactory.FromCharacterData(BuildHundredHpVictim()),
            seed: 1,
            spawnDistance: 1);
        simulation.Advance(Frame(0, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        int settle = FTT.Combat.BasicComboRules.StringProfileFor("tesla").GroundStartupFrames[0] + 2;
        for (int tick = 1; tick <= settle; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defeated)).IsTrue();
        AssertThat(defeated.Stocks).IsEqual(2);
        AssertThat(defeated.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(18.75).RawValue);
    }

    private static CharacterData BuildLethalOpenerAttacker() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        // 125 x tesla's 0.8 opener shape = exactly the victim's 100 HP.
        BasicAttackDamage = 125f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildHundredHpVictim() => new() {
        CharacterID = "joan",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    [TestCase]
    public void LoadoutFactoryUsesNormalizedCharacterResources() {
        CharacterData joan = FTT.Core.AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
        AssertObject(joan).IsNotNull();

        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(joan);
        AssertThat(loadout.CharacterID).IsEqual((int)FighterCharacterID.Joan);
        AssertThat(loadout.MaxHP).IsEqual(110);
        AssertThat(loadout.MaxBlockCharges).IsEqual(3);
        AssertThat(loadout.BasicDamage).IsEqual(6);
        AssertThat(loadout.SpecialOneDamage).IsEqual(28);
        AssertThat(loadout.SpecialOneStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(loadout.SpecialOneStatusFrames).IsEqual(180);
    }

    [TestCase]
    public void NewSpecialStatusReplacesOnlyItsOwnSlot() {
        CharacterData character = BuildStatusTestCharacter();
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(character),
            FighterLoadout.Default(FighterCharacterID.Einstein),
            seed: 15,
            spawnDistance: 1);

        simulation.Advance(
            Frame(0, 0, GameplayButtons.Special1),
            Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent firstStatus)).IsTrue();
        // V7 two-slot rule: RadiantBurn is a damaging status and occupies the
        // damage slot, leaving the control slot free.
        AssertThat(firstStatus.DamageStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(firstStatus.StatusType).IsEqual((int)StatusType.None);

        simulation.Advance(
            Frame(1, 0, GameplayButtons.Special2),
            Frame(1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent layered)).IsTrue();
        // Root lands in the control slot while the burn keeps running — the
        // two coexist instead of the newest deleting the previous.
        AssertThat(layered.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(layered.StatusFrames).IsEqual(120);
        AssertThat(layered.DamageStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(layered.StatusTickFrames).IsEqual(0);
    }

    [TestCase]
    public void ProjectileAbilityUsesAuthoritativeEntityLifecycle() {
        CharacterData character = BuildProjectileTestCharacter();
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(character),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 31,
            spawnDistance: 3);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.ProjectileCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent projectile)).IsTrue();
        AssertThat(projectile.Damage).IsEqual(13);

        int initialHP = 100;
        for (int tick = 1; tick < 90; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (simulation.TryGetFighter(1, out FighterStateComponent target) && target.CurrentHP < initialHP) break;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent hitTarget)).IsTrue();
        AssertThat(hitTarget.CurrentHP).IsEqual(87);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
    }

    [TestCase]
    public void SnapshotRestoreConvergesAcrossProjectileFlight() {
        FighterLoadout projectileLoadout = FighterLoadoutFactory.FromCharacterData(BuildProjectileTestCharacter());
        var uninterrupted = new FighterSimulation(projectileLoadout, FighterLoadout.Default(FighterCharacterID.Joan), seed: 82, spawnDistance: 4);
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 12; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();
        AssertThat(uninterrupted.ProjectileCount).IsEqual(1);

        var restored = new FighterSimulation(projectileLoadout, FighterLoadout.Default(FighterCharacterID.Joan), seed: 82, spawnDistance: 4);
        restored.RestoreFullState(snapshot);
        for (int tick = 12; tick < 100; tick++) {
            long expected = uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            long actual = restored.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
    }

    [TestCase]
    public void PersistentDeployLimitReplacesOldestStableEntity() {
        FighterLoadout persistentLoadout = FighterLoadoutFactory.FromCharacterData(BuildPersistentTestCharacter());
        var simulation = new FighterSimulation(persistentLoadout, FighterLoadout.Default(FighterCharacterID.Joan), seed: 46, spawnDistance: 4);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent first)).IsTrue();
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);

        simulation.Advance(Frame(1, 0, GameplayButtons.Special1), Frame(1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent replacement)).IsTrue();
        AssertThat(simulation.PersistentObjectCount).IsEqual(1);
        AssertThat(replacement.EntityID > first.EntityID).IsTrue();
        AssertThat(replacement.CurrentHP).IsEqual(25);
        AssertThat(replacement.LifetimeFrames > 1700).IsTrue();
    }

    [TestCase]
    public void HazardAndOrbSpawnsAreDeterministicAndSnapshotSafe() {
        FighterMatchRules rules = new(true, 3, true, 3);
        var first = new FighterSimulation(seed: 104, rules: rules);
        var second = new FighterSimulation(seed: 104, rules: rules);
        for (int tick = 0; tick < 1805; tick++) {
            PlayerInputFrame empty = Frame(tick, 0, GameplayButtons.None);
            AssertThat(first.Advance(empty, empty)).IsEqual(second.Advance(empty, empty));
        }
        AssertThat(first.HazardCount).IsEqual(1);
        AssertThat(first.OrbCount >= 1).IsTrue();
        AssertThat(first.TryGetFirstHazard(out FighterHazardComponent hazard)).IsTrue();
        AssertThat(hazard.Phase).IsEqual(0);

        byte[] snapshot = first.CaptureFullState();
        var restored = new FighterSimulation(seed: 104, rules: rules);
        restored.RestoreFullState(snapshot);
        for (int tick = 1805; tick < 1920; tick++) {
            PlayerInputFrame empty = Frame(tick, 0, GameplayButtons.None);
            AssertThat(restored.Advance(empty, empty)).IsEqual(first.Advance(empty, empty));
        }
    }

    [TestCase]
    public void SelectedStageHazardTypeIsPartOfDeterministicState() {
        FighterMatchRules rules = new(
            (int)MatchMode.Stock,
            itemsEnabled: false,
            itemFrequency: 0,
            hazardsEnabled: true,
            hazardFrequency: (int)HazardTriggerFrequency.High,
            stageHazardTypeID: 7);
        var simulation = new FighterSimulation(rules: rules);

        for (int tick = 0; tick < 1805; tick++) simulation.Advance(default, default);

        AssertThat(simulation.TryGetFirstHazard(out FighterHazardComponent hazard)).IsTrue();
        AssertThat(hazard.HazardTypeID).IsEqual(7);
        AssertThat(simulation.GetMatchState().StageHazardTypeID).IsEqual(7);
    }

    [TestCase]
    public void MatchModesApplyTheirDistinctEndConditions() {
        // V7: Stock mode runs the match clock too when it is enabled — expiry
        // compares stocks, then HP. A Stock match with the timer Off (0 match
        // seconds) is the only untimed configuration.
        var stock = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        var stockUntimed = new FighterSimulation(matchSeconds: 0, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        var timed = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.TimeLimit, false, 0, false, 0));
        // F21 retired the third mode; this arm was authored as Hybrid and is kept
        // as a second timed Stock match, because Hybrid's behaviour WAS Stock's —
        // it only ever reached the match system's silent default arm.
        var hybrid = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        for (int tick = 0; tick < 60; tick++) {
            PlayerInputFrame empty = Frame(tick, 0, GameplayButtons.None);
            stock.Advance(empty, empty);
            stockUntimed.Advance(empty, empty);
            timed.Advance(empty, empty);
            hybrid.Advance(empty, empty);
        }

        // A true tie at expiry records no draw — the match enters Sudden Death.
        // F22 (Package 11 A1c): entry runs the shared match-start ready countdown,
        // with controls and sim clocks frozen together for both players, so the
        // transition tick leaves MatchState at Countdown rather than InProgress.
        // Neither is Complete, which is the claim that matters here. The untimed
        // stock match simply keeps running.
        AssertThat(stock.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);
        AssertThat(stock.GetMatchState().SuddenDeathActive).IsEqual(1);
        AssertThat(stockUntimed.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);
        AssertThat(stockUntimed.GetMatchState().SuddenDeathActive).IsEqual(0);
        AssertThat(timed.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);
        AssertThat(timed.GetMatchState().SuddenDeathActive).IsEqual(1);
        AssertThat(hybrid.GetMatchState().MatchState).IsEqual(FighterMatchStates.Countdown);
        AssertThat(hybrid.GetMatchState().SuddenDeathActive).IsEqual(1);
    }

    /// <summary>
    /// The grounded run ramp: <c>UniversalMovementRules.RunAccelerationFrames</c>
    /// frames from a standstill to the default loadout's full move speed, and not
    /// a frame sooner. Both constants moved with the 2026-08-10 feel batch
    /// (§2.1: ramp 8 → 14 frames, default move speed 8 → 7 u/s), so the ramp
    /// length is read from the rulebook and only the terminal speed is a literal.
    /// </summary>
    [TestCase]
    public void GroundRunAcceleratesOverTheAuthoredRunRamp() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        xpTURN.Klotho.Deterministic.Math.FP64 fullSpeed =
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(7);

        // Every frame short of the ramp length must still be below full speed —
        // that is what rejects a shorter ramp sneaking back in.
        for (int tick = 0; tick < UniversalMovementRules.RunAccelerationFrames - 1; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent midRamp)).IsTrue();
            AssertThat(midRamp.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
            AssertThat(midRamp.Velocity.x < fullSpeed).OverrideFailureMessage(
                $"the run ramp topped out on frame {tick + 1} of "
                + $"{UniversalMovementRules.RunAccelerationFrames}.").IsTrue();
        }

        int lastRampTick = UniversalMovementRules.RunAccelerationFrames - 1;
        simulation.Advance(
            Frame(lastRampTick, 127, GameplayButtons.None),
            Frame(lastRampTick, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent accelerated)).IsTrue();
        AssertThat(accelerated.Velocity.x.RawValue).IsEqual(fullSpeed.RawValue);
    }

    /// <summary>
    /// The companion grounded stop ramp (§2.1): releasing the stick at full speed
    /// bleeds to a standstill over <c>RunDecelerationFrames</c>, a separate and
    /// deliberately shorter constant than the accel ramp. The one-frame slack on
    /// the terminal assertion is fixed-point rounding on <c>speed / 12</c>, not
    /// tolerance for a wrong constant — the "still moving" assertion two frames
    /// earlier is what rejects the old shared 8-frame ramp.
    /// </summary>
    [TestCase]
    public void ReleasedStickDeceleratesOverTheAuthoredStopRamp() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        xpTURN.Klotho.Deterministic.Math.FP64 fullSpeed =
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(7);

        for (int tick = 0; tick <= UniversalMovementRules.RunAccelerationFrames; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent running)).IsTrue();
        AssertThat(running.Velocity.x.RawValue).IsEqual(fullSpeed.RawValue);

        int released = UniversalMovementRules.RunAccelerationFrames + 1;
        for (int step = 0; step < UniversalMovementRules.RunDecelerationFrames - 2; step++) {
            simulation.Advance(
                Frame(released + step, 0, GameplayButtons.None),
                Frame(released + step, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent slowing)).IsTrue();
        AssertThat(slowing.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero)
            .OverrideFailureMessage("the stop ramp is shorter than RunDecelerationFrames.").IsTrue();
        AssertThat(slowing.Velocity.x < fullSpeed).IsTrue();

        for (int step = UniversalMovementRules.RunDecelerationFrames - 2;
             step <= UniversalMovementRules.RunDecelerationFrames;
             step++) {
            simulation.Advance(
                Frame(released + step, 0, GameplayButtons.None),
                Frame(released + step, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent stopped)).IsTrue();
        AssertThat(stopped.Velocity.x.RawValue)
            .IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.Zero.RawValue);
    }

    /// <summary>
    /// Fast-fall (§2.9): stateless, derived from held Down every airborne tick.
    /// The clamp lands on the same tick the input arrives — no ramp — and it is
    /// suppressed while the victim is in hitstun so a launch cannot be cut short
    /// by a player who happens to be holding Down.
    /// </summary>
    [TestCase]
    public void HoldingDownInTheAirFastFallsImmediatelyButNeverDuringHitstun() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        xpTURN.Klotho.Deterministic.Math.FP64 floor =
            -xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(UniversalMovementRules.FastFallSpeed);

        // Rise on a plain jump, confirm the fighter is genuinely climbing.
        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 6; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent rising)).IsTrue();
        AssertThat(rising.IsGrounded).IsEqual(0);
        AssertThat(rising.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();

        simulation.Advance(Frame(6, 0, GameplayButtons.Down), Frame(6, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent falling)).IsTrue();
        AssertThat(falling.Velocity.y.RawValue).OverrideFailureMessage(
            "fast-fall did not pin vertical velocity on the first tick Down was held.")
            .IsEqual(floor.RawValue);

        // Hitstun owns the victim. Player two holds Down for the whole run while
        // player one lands one basic: every basic launches (Velocity.y = force,
        // IsGrounded = 0), so the victim spends its hitstun airborne with Down
        // held — exactly the case the rule must ignore.
        var stunned = new FighterSimulation(spawnDistance: 1, rules: FighterMatchRules.Disabled);
        bool sawAirborneHitstun = false;
        bool clampedDuringHitstun = false;
        for (int tick = 0; tick < 140; tick++) {
            stunned.Advance(
                Frame(tick, 0, tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None),
                Frame(tick, 0, GameplayButtons.Down));
            AssertThat(stunned.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (victim.HitstunFrames <= 0) continue;
            if (victim.IsGrounded == 0) sawAirborneHitstun = true;
            if (victim.Velocity.y.RawValue == floor.RawValue) clampedDuringHitstun = true;
        }
        AssertThat(sawAirborneHitstun).OverrideFailureMessage(
            "the scenario never put the victim into airborne hitstun.").IsTrue();
        AssertThat(clampedDuringHitstun).OverrideFailureMessage(
            "fast-fall engaged while the victim was in hitstun.").IsFalse();
    }

    /// <summary>
    /// §2.3 gave the whole roster two jumps. Tesla's authored kit is the check
    /// that matters: it shipped at <c>MaxJumpCount = 1</c>, so a loadout built
    /// from its resource must now grant a real second airborne jump.
    /// </summary>
    [TestCase]
    public void EveryAuthoredKitCarriesASecondJump() {
        // Package 11 A6b: every AUTHORED kit, read from the manifest roster.
        foreach (string id in FTT.Core.CharacterRoster.IDs) {
            CharacterData data =
                FTT.Core.AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            AssertObject(data).IsNotNull();
            AssertThat(FighterLoadoutFactory.FromCharacterData(data).MaxJumpCount)
                .OverrideFailureMessage($"{id} does not have the universal double jump.").IsEqual(2);
        }

        CharacterData tesla =
            FTT.Core.AuthoredResources.Load<CharacterData>("res://resources/Characters/tesla_data.tres");
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(tesla),
            FighterLoadout.Default(FighterCharacterID.Einstein),
            seed: 7,
            spawnDistance: 8);

        simulation.Advance(Frame(0, 0, GameplayButtons.Jump), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent afterFirst)).IsTrue();
        AssertThat(afterFirst.RemainingJumps).IsEqual(1);

        // Wait for the rise to stall, then spend the second jump.
        for (int tick = 1; tick < 40; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            if (simulation.TryGetFighter(0, out FighterStateComponent airborne)
                && airborne.Velocity.y < xpTURN.Klotho.Deterministic.Math.FP64.Zero) break;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent descending)).IsTrue();
        AssertThat(descending.IsGrounded).IsEqual(0);

        simulation.Advance(Frame(40, 0, GameplayButtons.Jump), Frame(40, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent doubleJumped)).IsTrue();
        AssertThat(doubleJumped.Velocity.y > xpTURN.Klotho.Deterministic.Math.FP64.Zero)
            .OverrideFailureMessage("the second jump did not launch the fighter.").IsTrue();
        AssertThat(doubleJumped.RemainingJumps).IsEqual(0);
    }

    [TestCase]
    public void FighterHyperArmorKeepsDamageSuppressesBasicInterruptionAndAllowsUltimateInterruption() {
        FighterStateComponent attacker = new() {
            CurrentHP = 100,
            MaxHP = 100,
            Stocks = 3,
            Weight = xpTURN.Klotho.Deterministic.Math.FP64.One,
            Position = new xpTURN.Klotho.Deterministic.Math.FPVector2(
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-2),
                xpTURN.Klotho.Deterministic.Math.FP64.Zero)
        };
        FighterRuntimeComponent attackerRuntime = default;
        FighterStateComponent target = new() {
            CurrentHP = 100,
            MaxHP = 100,
            Stocks = 3,
            Weight = xpTURN.Klotho.Deterministic.Math.FP64.One,
            HyperArmorFrames = 30,
            IsGrounded = 1,
            Position = xpTURN.Klotho.Deterministic.Math.FPVector2.Zero,
            Velocity = xpTURN.Klotho.Deterministic.Math.FPVector2.Zero
        };
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent attackerVerb = default;
        FighterVerbComponent targetVerb = default;
        FighterDefenseComponent targetDefense = default;
        FighterTuningComponent tuning = new() { MaxBlockCharges = 3, MaxJumpCount = 1 };

        bool basicHit = FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in tuning,
            FighterDamageRules.BasicAttackClass, 10,
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(4), 15,
            0, 0, xpTURN.Klotho.Deterministic.Math.FP64.One,
            attacker.Position.x);
        AssertThat(basicHit).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(90);
        AssertThat(target.Velocity.x.RawValue).IsEqual(0L);
        AssertThat(target.Velocity.y.RawValue).IsEqual(0L);
        AssertThat(target.HitstunFrames).IsEqual(0);

        bool ultimateHit = FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb, ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in tuning,
            FighterDamageRules.UltimateAttackClass, 10,
            xpTURN.Klotho.Deterministic.Math.FP64.FromInt(4), 15,
            0, 0, xpTURN.Klotho.Deterministic.Math.FP64.One,
            attacker.Position.x);
        AssertThat(ultimateHit).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(80);
        AssertThat(target.Velocity.x.RawValue != 0L).IsTrue();
        AssertThat(target.HitstunFrames).IsEqual(15);
    }

    /// <summary>
    /// The universal dash was removed on 2026-08-09 (user directive). Its wire
    /// bit stays allocated in the protocol, so a frame carrying it must leave
    /// every fighter's gameplay state identical to the same frame without it.
    /// (The full state hash is deliberately not compared: it covers the raw
    /// input mirror, which legitimately differs by exactly the reserved bit.)
    /// </summary>
    [TestCase]
    public void TheReservedDashBitHasNoEffectOnSimulationState() {
        var plain = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        var flagged = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 120; tick++) {
            GameplayButtons buttons = tick % 3 == 0 ? GameplayButtons.Jump : GameplayButtons.None;
            plain.Advance(Frame(tick, 127, buttons), Frame(tick, -127, GameplayButtons.None));
            flagged.Advance(
                Frame(tick, 127, buttons | GameplayButtons.Dash),
                Frame(tick, -127, GameplayButtons.Dash));
        }
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(plain.TryGetFighter(playerID, out FighterStateComponent plainState)).IsTrue();
            AssertThat(flagged.TryGetFighter(playerID, out FighterStateComponent flaggedState)).IsTrue();
            AssertThat(flaggedState.Equals(plainState)).IsTrue();
            AssertThat(plain.TryGetFighterRuntime(playerID, out FighterRuntimeComponent plainRuntime)).IsTrue();
            AssertThat(flagged.TryGetFighterRuntime(playerID, out FighterRuntimeComponent flaggedRuntime)).IsTrue();
            AssertThat(flaggedRuntime.UniversalMovementState).IsEqual(plainRuntime.UniversalMovementState);
            AssertThat(flaggedRuntime.AttackPhase).IsEqual(plainRuntime.AttackPhase);
        }
    }

    [TestCase]
    public void FighterPushboxesPreventNormalMovementOverlap() {
        var simulation = new FighterSimulation(spawnDistance: 1, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 90; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent first)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
        var distance = xpTURN.Klotho.Deterministic.Math.FP64.Abs(second.Position.x - first.Position.x);
        AssertThat(distance >= FighterPushboxSystem.MinimumHorizontalDistance).IsTrue();
    }

    [TestCase]
    public void RollPassesThroughOpponentAndIgnoresHitsOnlyDuringInvulnerableFrames() {
        var simulation = new FighterSimulation(spawnDistance: 1, rules: FighterMatchRules.Disabled);
        // Walk the pair into pushbox contact (0.8 units) before rolling. The roll
        // covers 11 travel frames at 1.5x move speed — 1.925 units since the
        // 2026-08-10 −15% move-speed retune (feel batch §2.1), down from 2.2 —
        // so the pass-through is now exercised from the contact range a real
        // match actually produces rather than across the full 2-unit spawn gap.
        for (int approach = 0; approach < 40; approach++) {
            simulation.Advance(
                Frame(approach, 127, GameplayButtons.None),
                Frame(approach, -127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent beforeRoll)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocker)).IsTrue();
        AssertThat(beforeRoll.Position.x < blocker.Position.x).IsTrue();

        for (int tick = 0; tick < UniversalMovementRules.RollTotalFrames; tick++) {
            GameplayButtons playerOne = tick == 0 ? GameplayButtons.Roll : GameplayButtons.None;
            GameplayButtons playerTwo = tick == UniversalMovementRules.RollStartupFrames
                ? GameplayButtons.BasicAttack
                : GameplayButtons.None;
            simulation.Advance(Frame(40 + tick, 127, playerOne), Frame(40 + tick, 0, playerTwo));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent roller)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(roller.CurrentHP).IsEqual(roller.MaxHP);
        AssertThat(roller.Position.x > opponent.Position.x).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);

        int recoveryCompleteTick = 40 + UniversalMovementRules.RollTotalFrames;
        // V7 normative boxes: a swing never hits behind the attacker's back,
        // and the roller now stands on the opponent's OTHER side — so the
        // attacker holds toward the roller, flipping facing before the swing.
        simulation.Advance(
            Frame(recoveryCompleteTick, 0, GameplayButtons.None),
            Frame(recoveryCompleteTick, 127, GameplayButtons.BasicAttack));
        // Basics are real swings now (BasicComboRules): this press lands inside
        // the first swing's recovery, so it buffers and chains into hit two,
        // whose active window arrives a few dozen ticks later. Drain enough
        // ticks to cover the chained swing before asserting the hit connected.
        for (int tick = 1; tick <= 60; tick++) {
            simulation.Advance(
                Frame(recoveryCompleteTick + tick, 0, GameplayButtons.None),
                Frame(recoveryCompleteTick + tick, 127, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent vulnerableAfterRoll)).IsTrue();
        AssertThat(vulnerableAfterRoll.CurrentHP < vulnerableAfterRoll.MaxHP).IsTrue();
    }

    [TestCase]
    public void AreaSpecialSpawnsZoneThatAppliesStatusWithoutImpulseAndBuffsTheOwner() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildZoneTestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 61,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));

        AssertThat(simulation.ZoneCount).IsEqual(1);
        AssertThat(simulation.TryGetFirstZone(out FighterZoneComponent zone)).IsTrue();
        AssertThat(zone.ZoneTypeID).IsEqual((int)FighterCharacterID.Einstein * 10 + 2);
        AssertThat(zone.TickIntervalFrames).IsEqual(30);
        AssertThat(zone.StatusType).IsEqual((int)StatusType.TimeDilation);
        AssertThat(zone.GrantsOwnerSpeedBonus).IsEqual(1);

        // The first pulse fires on the spawn frame: chip damage plus TimeDilation,
        // with no knockback, hitstun, or block interaction.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(98);
        AssertThat(target.HitstunFrames).IsEqual(0);
        AssertThat(target.BlockCharges).IsEqual(3);
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent targetRuntime)).IsTrue();
        AssertThat(targetRuntime.StatusType).IsEqual((int)StatusType.TimeDilation);

        // Einstein standing inside his own rift carries the +25% speed bonus flag.
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent ownerRuntime)).IsTrue();
        AssertThat(ownerRuntime.ZoneSpeedBonusFrames > 0).IsTrue();

        for (int tick = 1; tick <= 200; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void ZoneLifecycleIsSnapshotAndRollbackSafe() {
        FighterLoadout zoneLoadout = FighterLoadoutFactory.FromCharacterData(BuildZoneTestCharacter());
        var uninterrupted = new FighterSimulation(
            zoneLoadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 73, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        uninterrupted.Advance(Frame(0, 0, GameplayButtons.Special2), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick < 40; tick++) {
            uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        byte[] snapshot = uninterrupted.CaptureFullState();
        AssertThat(uninterrupted.ZoneCount).IsEqual(1);

        var restored = new FighterSimulation(
            zoneLoadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 73, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        restored.RestoreFullState(snapshot);
        for (int tick = 40; tick < 260; tick++) {
            long expected = uninterrupted.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            long actual = restored.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
        AssertThat(restored.ZoneCount).IsEqual(0);
    }

    [TestCase]
    public void WarpMovementTravelsInHeldInputDirectionAndGrantsFloatFrames() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildWarpTestCharacter()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 88,
            spawnDistance: 4,
            rules: FighterMatchRules.Disabled);

        // Hold up while triggering the warp: world Y is up, stick up is negative.
        simulation.Advance(
            Frame(0, 0, GameplayButtons.MovementAbility, moveY: -127),
            Frame(0, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent warped)).IsTrue();
        AssertThat(warped.Position.y > xpTURN.Klotho.Deterministic.Math.FP64.FromInt(2)).IsTrue();
        AssertThat(warped.IsGrounded).IsEqual(0);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.FloatFrames > 0).IsTrue();

        // The float glide's reduced gravity keeps the fighter airborne well past a
        // normal fall over the same window.
        for (int tick = 1; tick <= 30; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent floating)).IsTrue();
        AssertThat(floating.Position.y > xpTURN.Klotho.Deterministic.Math.FP64.One).IsTrue();
    }

    private static CharacterData BuildZoneTestCharacter() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData {
            ExecutionType = AbilityExecutionType.Area,
            BaseDamage = 1.5f,
            DamageTickIntervalFrames = 30,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 10f,
            Lifetime = 3f,
            AppliedStatus = StatusType.TimeDilation,
            StatusDuration = 3f,
            StatusIntensity = 1f
        },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildWarpTestCharacter() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData {
            MovementType = MovementType.Warp,
            MovementDuration = 0.2f,
            DistanceMoved = 150f,
            MovementSpeed = 750f,
            CooldownDuration = 5f
        },
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildStatusTestCharacter() => new() {
        CharacterID = "joan",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            BaseDamage = 0f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 1f,
            AppliedStatus = StatusType.RadiantBurn,
            StatusDuration = 3f,
            StatusIntensity = 1f
        },
        SpecialAttackTwo = new AbilityData {
            BaseDamage = 0f,
            KnockbackForce = Vector2.Zero,
            CooldownDuration = 1f,
            AppliedStatus = StatusType.Root,
            StatusDuration = 2f,
            StatusIntensity = 1f
        },
        UltimateAttack = new AbilityData {
            BaseDamage = 20f,
            KnockbackForce = new Vector2(5f, -3f)
        }
    };

    private static CharacterData BuildProjectileTestCharacter() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 13f,
            KnockbackForce = new Vector2(3f, 0f),
            ProjectileSpeed = 360f,
            ProjectileLifetime = 3f,
            CooldownDuration = 1f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildPersistentTestCharacter() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.PersistentObject,
            BaseDamage = 5f,
            PersistentObjectID = "tesla_coil",
            MaxActiveObjects = 1,
            Lifetime = 30f,
            CooldownDuration = 0f
        },
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame InputFor(int playerID, int tick) {
        int cycle = (tick + playerID * 11) % 120;
        sbyte axis = cycle < 40 ? (sbyte)90 : cycle < 80 ? (sbyte)-90 : (sbyte)0;
        GameplayButtons held = GameplayButtons.None;
        if (tick % 47 == playerID) held |= GameplayButtons.Jump;
        if (tick % 31 == playerID * 3) held |= GameplayButtons.BasicAttack;
        if (tick % 211 == 50 + playerID) held |= GameplayButtons.Special1;
        return Frame(tick, axis, held);
    }

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };
}
