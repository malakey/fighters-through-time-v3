using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
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
        var simulation = new FighterSimulation(seed: 1, spawnDistance: 1);
        int attackTick = 0;
        while (simulation.TryGetFighter(1, out FighterStateComponent target) && target.Stocks == 3) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            sbyte attackerAxis = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            simulation.Advance(
                Frame(attackTick, attackerAxis, GameplayButtons.BasicAttack),
                Frame(attackTick, (sbyte)-attackerAxis, GameplayButtons.None));
            for (int cooldown = 0; cooldown < 18; cooldown++) {
                attackTick++;
                simulation.TryGetFighter(0, out attacker);
                simulation.TryGetFighter(1, out target);
                attackerAxis = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
                simulation.Advance(
                    Frame(attackTick, attackerAxis, GameplayButtons.None),
                    Frame(attackTick, (sbyte)-attackerAxis, GameplayButtons.None));
            }
            attackTick++;
            AssertThat(attackTick < 400).IsTrue();
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defeated)).IsTrue();
        AssertThat(defeated.Stocks).IsEqual(2);
        AssertThat(defeated.Influence.RawValue).IsEqual(
            xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(18.75).RawValue);
    }

    [TestCase]
    public void LoadoutFactoryUsesNormalizedCharacterResources() {
        CharacterData joan = FTT.Core.AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres");
        AssertObject(joan).IsNotNull();

        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(joan);
        AssertThat(loadout.CharacterID).IsEqual((int)FighterCharacterID.Joan);
        AssertThat(loadout.MaxHP).IsEqual(110);
        AssertThat(loadout.MaxBlockCharges).IsEqual(3);
        AssertThat(loadout.BasicDamage).IsEqual(12);
        AssertThat(loadout.SpecialOneDamage).IsEqual(14);
        AssertThat(loadout.SpecialOneStatusType).IsEqual((int)StatusType.RadiantBurn);
        AssertThat(loadout.SpecialOneStatusFrames).IsEqual(180);
    }

    [TestCase]
    public void NewSpecialStatusCompletelyReplacesPreviousStatus() {
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
        AssertThat(firstStatus.StatusType).IsEqual((int)StatusType.RadiantBurn);

        simulation.Advance(
            Frame(1, 0, GameplayButtons.Special2),
            Frame(1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent replacement)).IsTrue();
        AssertThat(replacement.StatusType).IsEqual((int)StatusType.Root);
        AssertThat(replacement.StatusFrames).IsEqual(120);
        AssertThat(replacement.StatusTickFrames).IsEqual(0);
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
            (int)MatchMode.Hybrid,
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
        var stock = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        var timed = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.TimeLimit, false, 0, false, 0));
        var hybrid = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Hybrid, false, 0, false, 0));
        for (int tick = 0; tick < 60; tick++) {
            PlayerInputFrame empty = Frame(tick, 0, GameplayButtons.None);
            stock.Advance(empty, empty);
            timed.Advance(empty, empty);
            hybrid.Advance(empty, empty);
        }

        AssertThat(stock.GetMatchState().MatchState).IsEqual(1);
        AssertThat(timed.GetMatchState().MatchState).IsEqual(2);
        AssertThat(timed.GetMatchState().IsTrueTie).IsEqual(1);
        AssertThat(hybrid.GetMatchState().MatchState).IsEqual(2);
    }

    [TestCase]
    public void GroundRunAcceleratesOverEightFrames() {
        var simulation = new FighterSimulation(spawnDistance: 8, rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, 127, GameplayButtons.None), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent firstFrame)).IsTrue();
        AssertThat(firstFrame.Velocity.x > xpTURN.Klotho.Deterministic.Math.FP64.Zero).IsTrue();
        AssertThat(firstFrame.Velocity.x < xpTURN.Klotho.Deterministic.Math.FP64.FromInt(8)).IsTrue();

        for (int tick = 1; tick < UniversalMovementRules.RunAccelerationFrames; tick++) {
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent accelerated)).IsTrue();
        AssertThat(accelerated.Velocity.x.RawValue).IsEqual(xpTURN.Klotho.Deterministic.Math.FP64.FromInt(8).RawValue);
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
        FighterTuningComponent tuning = new() { MaxBlockCharges = 3, MaxJumpCount = 1 };

        bool basicHit = FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in tuning,
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
            ref attacker, ref attackerRuntime, ref target, ref targetRuntime, in tuning,
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
        for (int tick = 0; tick < UniversalMovementRules.RollTotalFrames; tick++) {
            GameplayButtons playerOne = tick == 0 ? GameplayButtons.Roll : GameplayButtons.None;
            GameplayButtons playerTwo = tick == UniversalMovementRules.RollStartupFrames
                ? GameplayButtons.BasicAttack
                : GameplayButtons.None;
            simulation.Advance(Frame(tick, 127, playerOne), Frame(tick, 0, playerTwo));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent roller)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
        AssertThat(roller.CurrentHP).IsEqual(roller.MaxHP);
        AssertThat(roller.Position.x > opponent.Position.x).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);

        int recoveryCompleteTick = UniversalMovementRules.RollTotalFrames;
        simulation.Advance(
            Frame(recoveryCompleteTick, 0, GameplayButtons.None),
            Frame(recoveryCompleteTick, 0, GameplayButtons.BasicAttack));
        // Basics are real swings now (BasicComboRules): this press lands inside
        // the first swing's recovery, so it buffers and chains into hit two,
        // whose active window arrives a few dozen ticks later. Drain enough
        // ticks to cover the chained swing before asserting the hit connected.
        for (int tick = 1; tick <= 60; tick++) {
            simulation.Advance(
                Frame(recoveryCompleteTick + tick, 0, GameplayButtons.None),
                Frame(recoveryCompleteTick + tick, 0, GameplayButtons.None));
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
