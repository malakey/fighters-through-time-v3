using FTT.Core;
using FTT.FighterSim;
using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using FP64 = xpTURN.Klotho.Deterministic.Math.FP64;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W6 — A02, the Fighter-mode Ultimate activation strike (component
/// 321). The universal rules, pinned on one generic straight-shot Ultimate
/// (five 10-damage hits, the AbilityData activation defaults: a six-unit
/// projectile, 20 wind-up / 10 active / 45 whiff recovery):
/// acceptance spends the meter and starts a hyper-armored wind-up; the strike
/// is avoided by distance, a jump or a roll; a whiff deals nothing, keeps the
/// meter spent and ends in a whiff recovery Echo Step cannot undo; an Aegis-
/// absorbed contact still starts the cinematic; the held victim cannot act; an
/// airborne start freezes the caster; the whole activation is rollback safe;
/// and the CPU commits only when the strike can plausibly connect.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UltimateActivationTests {

    private const int PerHit = 10;
    private const int Hits = 5;
    private const int Windup = UltimateActivationRules.DefaultWindupFrames;
    private const int Active = UltimateActivationRules.DefaultActiveFrames;
    private const int Whiff = UltimateActivationRules.DefaultWhiffRecoveryFrames;

    [After]
    public void DrainPendingFinalizers() {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [TestCase]
    public void AcceptanceSpendsTheMeterAndStartsAnArmoredWindup() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Press(simulation, tick);

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
        AssertThat(caster.HyperArmorFrames).IsEqual(Windup + Active);
        AssertThat(simulation.TryGetFighterUltimateActivation(0, out FighterUltimateActivationComponent activation)).IsTrue();
        AssertThat(activation.Phase).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        AssertThat(activation.PhaseFrames).IsEqual(Windup);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(before.CurrentHP);
    }

    [TestCase]
    public void AWhiffByDistanceDealsNothingKeepsTheMeterSpentAndRecovers() {
        // Ten units apart: beyond the six-unit shot.
        FighterSimulation simulation = NewSimulation(spawnDistance: 5, out int tick);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Press(simulation, tick++);
        tick = UltimateActivationTestKit.Idle(simulation, tick, Windup - 1);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
        tick = UltimateActivationTestKit.Idle(simulation, tick, 1);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseActive);
        // The last of the ten active frames is evaluated on the tick the
        // strike turns into the whiff recovery.
        tick = UltimateActivationTestKit.Idle(simulation, tick, Active - 1);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent recovering)).IsTrue();
        AssertThat(recovering.HyperArmorFrames).IsEqual(0);

        tick = UltimateActivationTestKit.Idle(simulation, tick, Whiff - 1);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        UltimateActivationTestKit.Idle(simulation, tick, 1);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);

        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.Zero.RawValue);
    }

    [TestCase]
    public void AJumpClearsTheStraightShot() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Press(simulation, tick++);
        // The victim reads the wind-up and jumps; the shot flies under them.
        tick = UltimateActivationTestKit.Idle(simulation, tick, 8);
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Jump));
        tick++;
        UltimateActivationTestKit.Idle(simulation, tick, Windup + Active);

        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
    }

    [TestCase]
    public void ARollThroughTheShotIsAvoidance() {
        // Four units apart: the shot's front reaches the victim on active frame 5.
        FighterSimulation simulation = NewSimulation(spawnDistance: 2, out int tick);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        Press(simulation, tick++);
        tick = UltimateActivationTestKit.Idle(simulation, tick, 16);
        // The victim rolls toward the caster: the invulnerable travel frames
        // cover the moment the shot reaches them, and the shot has passed
        // behind them once the invulnerability ends.
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
            UltimateActivationTestKit.Input(tick, -127, GameplayButtons.Roll));
        tick++;
        UltimateActivationTestKit.Idle(simulation, tick, Windup + Active);

        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
        AssertThat(simulation.ZoneCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP).IsEqual(before.CurrentHP);
    }

    [TestCase]
    public void EchoStepCannotUndoTheWhiffRecovery() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 5, out int tick);
        Press(simulation, tick++);
        tick = UltimateActivationTestKit.Idle(simulation, tick, Windup + Active + 5);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);

        // Give the caster Echo Step's 30 meter; the whiff recovery is part of
        // the Ultimate, so neither the direct bind nor the chord arms it.
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 50)).IsTrue();
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.EchoStep),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Block | GameplayButtons.Roll),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.EchoStepWindupFrames).IsEqual(0);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.FromInt(50).RawValue);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);
    }

    [TestCase]
    public void HyperArmorCarriesTheWindupThroughAHit() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();

        Press(simulation, tick++);
        // The victim swings into the wind-up.
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
            UltimateActivationTestKit.Input(tick, -127, GameplayButtons.BasicAttack));
        tick++;
        bool struck = false;
        bool connected = false;
        for (int i = 0; i < Windup + Active + 20 && !connected; i++, tick++) {
            simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
            if (caster.CurrentHP < before.CurrentHP) struck = true;
            // Damage applies, but the armor denies the hitstun and the wind-up runs on.
            AssertThat(caster.HitstunFrames).IsEqual(0);
            connected = UltimateActivationTestKit.IsCinematic(simulation, 0);
        }
        AssertThat(struck).IsTrue();
        AssertThat(connected).IsTrue();
    }

    [TestCase]
    public void AnAegisAbsorbedContactStillStartsTheCinematic() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        AssertThat(simulation.SeedFighterAegisForTest(1)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();

        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick, out bool connected);
        AssertThat(connected).IsTrue();
        // D03b/D03e: the bubble absorbed the contact tick's hit and is spent;
        // the rest of the cinematic lands.
        AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent defense)).IsTrue();
        AssertThat(defense.AegisActive).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent atContact)).IsTrue();
        AssertThat(atContact.CurrentHP).IsEqual(before.CurrentHP);

        UltimateActivationTestKit.Idle(simulation, tick, Hits * 18 + 10);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(before.CurrentHP - after.CurrentHP).IsEqual(PerHit * (Hits - 1));
    }

    [TestCase]
    public void TheHeldVictimCannotAct() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        tick = UltimateActivationTestKit.CastAndConnect(simulation, tick);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent casterBefore)).IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent anchored)).IsTrue();

        GameplayButtons[] attempts = {
            GameplayButtons.BasicAttack, GameplayButtons.Special1, GameplayButtons.Roll,
            GameplayButtons.Jump, GameplayButtons.Ultimate, GameplayButtons.Grab
        };
        for (int i = 0; i < 40; i++, tick++) {
            simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
                UltimateActivationTestKit.Input(tick, -127, attempts[i % attempts.Length]));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent held)).IsTrue();
        AssertThat(held.Position.x.RawValue).IsEqual(anchored.Position.x.RawValue);
        AssertThat(held.Position.y.RawValue).IsEqual(anchored.Position.y.RawValue);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent casterAfter)).IsTrue();
        AssertThat(casterAfter.CurrentHP).IsEqual(casterBefore.CurrentHP);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.GrabPhase).IsEqual(0);
    }

    [TestCase]
    public void AnAerialActivationFreezesTheCasterThroughTheWindup() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 5, out int tick);
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Jump),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        tick = UltimateActivationTestKit.Idle(simulation, tick + 1, 6);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent rising)).IsTrue();
        AssertThat(rising.IsGrounded).IsEqual(0);

        Press(simulation, tick++);
        AssertThat(simulation.TryGetFighterUltimateActivation(0, out FighterUltimateActivationComponent activation)).IsTrue();
        AssertThat(activation.StartedAerial).IsEqual(1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent frozen)).IsTrue();
        for (int i = 0; i < Windup - 1; i++, tick++) {
            UltimateActivationTestKit.Idle(simulation, tick, 1);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent hovering)).IsTrue();
            AssertThat(hovering.Position.y.RawValue).IsEqual(frozen.Position.y.RawValue);
            AssertThat(hovering.IsGrounded).IsEqual(0);
        }
    }

    [TestCase]
    public void TheActivationIsSnapshotAndRollbackSafeMidWindup() {
        FighterSimulation uninterrupted = NewSimulation(spawnDistance: 1, out int tick);
        FighterSimulation restored = NewSimulation(spawnDistance: 1, out int _);

        Press(uninterrupted, tick++);
        tick = UltimateActivationTestKit.Idle(uninterrupted, tick, 10);
        restored.RestoreFullState(uninterrupted.CaptureFullState());
        AssertThat(restored.CurrentHash).IsEqual(uninterrupted.CurrentHash);

        // Through the rest of the wind-up, the contact, the hold and the release.
        for (int i = 0; i < 160; i++, tick++) {
            long expected = uninterrupted.Advance(
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
                UltimateActivationTestKit.Input(tick, 60, GameplayButtons.None));
            long actual = restored.Advance(
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None),
                UltimateActivationTestKit.Input(tick, 60, GameplayButtons.None));
            AssertThat(actual).IsEqual(expected);
        }
        AssertThat(UltimateActivationTestKit.Phase(restored, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
    }

    [TestCase]
    public void TheCpuCommitsOnlyWhenTheStrikeCanPlausiblyConnect() {
        // A02 CPU rule: close range, target not invulnerable, not rolling, and
        // grounded. Each blocker alone keeps a 100% Ultimate band silent.
        CpuBandTuning always = CpuBandTuning.Normal with {
            UltimatePercent = 100,
            RequiresUltimateSetup = false,
            GrabAdmissionPercent = 0,
            EchoStepAdmissionPercent = 0
        };
        AssertThat(CountUltimatePresses(always, PlausibleOpportunity())).IsGreater(0);

        CpuDecisionObservation invulnerable = PlausibleOpportunity();
        invulnerable.TargetInvulnerabilityFrames = 8;
        AssertThat(CountUltimatePresses(always, invulnerable)).IsEqual(0);

        CpuDecisionObservation rolling = PlausibleOpportunity();
        rolling.TargetRolling = 1;
        AssertThat(CountUltimatePresses(always, rolling)).IsEqual(0);

        CpuDecisionObservation airborne = PlausibleOpportunity();
        airborne.TargetIsGrounded = 0;
        AssertThat(CountUltimatePresses(always, airborne)).IsEqual(0);

        CpuDecisionObservation distant = PlausibleOpportunity();
        distant.TargetPositionXRaw = FP64.FromInt(5).RawValue;
        AssertThat(CountUltimatePresses(always, distant)).IsEqual(0);

        // A Story-shaped observation (no target verb layer — the Mirror
        // adapter) falls back to the range rule alone.
        CpuDecisionObservation story = PlausibleOpportunity();
        story.HasTargetVerbState = 0;
        story.TargetIsGrounded = 0;
        AssertThat(CountUltimatePresses(always, story)).IsGreater(0);
    }

    private static int CountUltimatePresses(CpuBandTuning tuning, CpuDecisionObservation observation) {
        var cpu = new FighterCpuController(CpuDifficulty.Normal, 20260929, null, null, tuning);
        PlayerInputFrame previous = default;
        int presses = 0;
        for (uint tick = 0; tick < 600; tick++) {
            PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
            previous = frame;
            if (frame.IsPressed(GameplayButtons.Ultimate)) presses++;
        }
        return presses;
    }

    private static CpuDecisionObservation PlausibleOpportunity() => new() {
        SelfPositionXRaw = FP64.Zero.RawValue,
        TargetPositionXRaw = FP64.FromDouble(1.5).RawValue,
        Stocks = 3,
        IsGrounded = 1,
        RemainingJumps = 1,
        SelfCurrentHP = 100,
        SelfMaxHP = 100,
        InfluenceRaw = FP64.FromInt(100).RawValue,
        TargetCurrentHP = 100,
        TargetMaxHP = 100,
        HasVerbState = 1,
        SelfFacingRight = 1,
        HasTargetVerbState = 1,
        TargetIsGrounded = 1,
        HasStageBounds = 1,
        LeftWallRaw = FP64.FromInt(-9).RawValue,
        RightWallRaw = FP64.FromInt(9).RawValue,
        CeilingRaw = FP64.FromInt(9).RawValue,
        BottomBlastZoneRaw = FP64.FromInt(-5).RawValue
    };

    private static void Press(FighterSimulation simulation, int tick) =>
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Ultimate),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));

    /// <summary>
    /// A generic caster (the AbilityData activation defaults) facing a 400-HP
    /// target, with the meter seeded full after a short idle so the Echo Step
    /// history is live. Returns the next input tick.
    /// </summary>
    private static FighterSimulation NewSimulation(int spawnDistance, out int nextTick) {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildCaster()),
            FighterLoadoutFactory.FromCharacterData(BuildTarget()),
            seed: 13006,
            spawnDistance: spawnDistance,
            rules: FighterMatchRules.Disabled);
        nextTick = UltimateActivationTestKit.Idle(simulation, 0, 40);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        return simulation;
    }

    private static CharacterData BuildCaster() => new() {
        CharacterID = "einstein",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData {
            BaseDamage = PerHit,
            IsMultiHit = true,
            HitCount = Hits,
            DamageTickIntervalFrames = 18,
            KnockbackForce = new Vector2(6f, -4f)
        }
    };

    // Leonardo's long (115%) string reaches the caster from the two-unit spawn gap.
    private static CharacterData BuildTarget() => new() {
        CharacterID = "leonardo",
        MaxHP = 400,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 2,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };
}
