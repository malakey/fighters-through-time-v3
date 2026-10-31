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
/// meter spent and ends in a whiff recovery no verb can undo; an Aegis-
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
    public void ARollCannotUndoTheWhiffRecovery() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 5, out int tick);
        Press(simulation, tick++);
        tick = UltimateActivationTestKit.Idle(simulation, tick, Windup + Active + 5);
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);

        // The whiff recovery is part of the Ultimate, so neither a bare Roll
        // nor the Block+Roll pair starts a roll or ends the recovery.
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Roll),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        tick++;
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Block | GameplayButtons.Roll),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.None);
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

    /// <summary>
    /// Combat and movement update 2026-10-16, decision 2.3: no Ultimate from
    /// the block stance. The press is refused, the meter stays full, and the
    /// refused press claims nothing — no Special 2 or movement ability beside
    /// it (the default pad Ultimate is the Movement + Special 2 chord) and no
    /// "not yet" flash. Story's Blocking state never polled the Ultimate.
    /// </summary>
    [TestCase]
    public void AnUltimatePressedFromTheBlockStanceIsRefusedKeepsTheMeterAndClaimsNothing() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 5, out int tick);
        for (int i = 0; i < 3; i++, tick++) {
            simulation.Advance(UltimateActivationTestKit.Holding(tick, GameplayButtons.Block),
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent stance)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent stanceRuntime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent stanceVerb)).IsTrue();
        AssertThat(FighterBasicAttackRules.IsBlockStance(in stance, in stanceRuntime, in stanceVerb))
            .OverrideFailureMessage("Harness: a held Block must raise the stance.")
            .IsTrue();

        const GameplayButtons chord = GameplayButtons.Block | GameplayButtons.Ultimate
            | GameplayButtons.Special2 | GameplayButtons.MovementAbility;
        for (int i = 0; i < 5; i++, tick++) {
            LockoutRefusalBaseline baseline = Baseline(simulation, 0);
            simulation.Advance(new PlayerInputFrame {
                    Tick = (uint)tick, Held = chord,
                    Pressed = chord & ~GameplayButtons.Block
                },
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
            AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
            AssertThat(Classify(simulation, 0, in baseline)).IsEqual(FighterLockoutRefusal.None);
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.SpecialTwoReuseFrames).IsEqual(0);
        AssertThat(runtime.MovementCooldownFrames).IsEqual(0);
        AssertThat(simulation.ZoneCount + simulation.ProjectileCount).IsEqual(0);

        // Positive control: out of the stance the same press is accepted.
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Ultimate),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseWindup);
    }

    /// <summary>
    /// Decision 2.3: no Ultimate in shieldstun either. A blocked swing locks the
    /// caster into the stance for its shieldstun; an Ultimate pressed with Block
    /// already released is still refused there, with the meter kept.
    /// </summary>
    [TestCase]
    public void AnUltimatePressedInShieldstunIsRefusedAndKeepsTheMeter() {
        FighterSimulation simulation = NewSimulation(spawnDistance: 1, out int tick);
        bool stunned = false;
        for (int i = 0; i < 40 && !stunned; i++, tick++) {
            simulation.Advance(UltimateActivationTestKit.Holding(tick, GameplayButtons.Block),
                UltimateActivationTestKit.Input(tick, -127, i == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None));
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
            stunned = verb.HitstopFrames == 0 && verb.ShieldStunFrames >= 2;
        }
        AssertThat(stunned)
            .OverrideFailureMessage("Harness: the target's swing never left the blocker in shieldstun.")
            .IsTrue();

        LockoutRefusalBaseline baseline = Baseline(simulation, 0);
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Ultimate),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent during)).IsTrue();
        AssertThat(during.ShieldStunFrames > 0)
            .OverrideFailureMessage("Harness: the press must land inside the shieldstun.")
            .IsTrue();
        AssertThat(UltimateActivationTestKit.Phase(simulation, 0)).IsEqual(FighterUltimateActivationRules.PhaseNone);
        AssertThat(Classify(simulation, 0, in baseline)).IsEqual(FighterLockoutRefusal.None);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
        AssertThat(caster.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);
    }

    private static LockoutRefusalBaseline Baseline(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        return LockoutRefusalBaseline.Capture(in runtime, in verb, in fighter, in activation);
    }

    private static FighterLockoutRefusal Classify(FighterSimulation simulation, int playerID, in LockoutRefusalBaseline baseline) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)).IsTrue();
        return FighterLockoutRefusalRules.Evaluate(in baseline, in fighter, in runtime, in verb, in activation, in action);
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

    // === Fix round A (CM-FIXA-SIM, ruling (c)): the A02 thaw tick =============

    /// <summary>The verbs pressed on the caster's thaw tick: the name, the press, what is held, and how a start shows.</summary>
    private static readonly (string Name, GameplayButtons Pressed, GameplayButtons Held)[] ThawVerbs = {
        ("Special 2", GameplayButtons.Special2, GameplayButtons.Special2),
        ("the movement ability", GameplayButtons.MovementAbility, GameplayButtons.MovementAbility),
        ("the direct grab", GameplayButtons.Grab, GameplayButtons.Grab),
        ("the grab chord", GameplayButtons.BasicAttack, GameplayButtons.Block | GameplayButtons.BasicAttack),
        ("held Block", GameplayButtons.None, GameplayButtons.Block)
    };

    /// <summary>
    /// Fix round A (ruling (c)): the caster's own slow shot, fired before the
    /// Ultimate, lands on the far opponent while the caster is in its wind-up
    /// or its whiff recovery and freezes it (the owner of a landed projectile
    /// takes the hitstop). The hitstop branch runs before A02's discard, so the
    /// thaw tick — whose presses the ability and combat systems read — must
    /// hold the lock itself: no Special, movement ability, grab or block stance
    /// starts there, and the Ultimate runs on. In the whiff recovery the
    /// lockout the acceptance armed has expired, so without the lock a Special
    /// would undo the punish window too.
    /// </summary>
    [TestCase]
    public void NoVerbStartsOnAnOwnShotsThawTickInTheWindupOrTheWhiffRecovery() {
        foreach (int phase in new[] { FighterUltimateActivationRules.PhaseWindup, FighterUltimateActivationRules.PhaseWhiffRecovery }) {
            int ultimateOffset = -1;
            int thawTick = -1;
            for (int candidate = 30; candidate <= 130 && thawTick < 0; candidate++) {
                thawTick = FindCasterThawTick(candidate, phase);
                if (thawTick >= 0) ultimateOffset = candidate;
            }
            AssertThat(thawTick >= 0)
                .OverrideFailureMessage($"Harness: no run froze the caster with its own shot in phase {phase}.")
                .IsTrue();
            foreach ((string name, GameplayButtons pressed, GameplayButtons held) in ThawVerbs) {
                FighterSimulation simulation = NewShooterSimulation(out int start);
                int tick = start;
                for (; tick < thawTick; tick++) {
                    simulation.Advance(ShooterFrame(tick, start, ultimateOffset), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
                }
                AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent frozen)).IsTrue();
                AssertThat(frozen.HitstopFrames)
                    .OverrideFailureMessage($"Harness: the {name} run did not reach the thaw tick frozen.")
                    .IsEqual(1);
                simulation.Advance(new PlayerInputFrame { Tick = (uint)tick, Pressed = pressed, Held = held },
                    UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
                // The thaw tick's own clock step may move the wind-up into its
                // active frames; the Ultimate is still running either way.
                AssertThat(UltimateActivationTestKit.Phase(simulation, 0))
                    .OverrideFailureMessage($"{name}: the Ultimate must run on through its thaw tick (phase {phase}).")
                    .IsNotEqual(FighterUltimateActivationRules.PhaseNone);
                AssertThat(CasterVerbStarted(simulation, pressed, held))
                    .OverrideFailureMessage($"{name} started on the caster's thaw tick in phase {phase}.")
                    .IsFalse();
            }
        }
    }

    /// <summary>
    /// Fix round A (ruling (c)): the A02 thaw-tick lock discards buttons, never
    /// the stick. A caster punished out of its whiff recovery is still the
    /// Ultimate's owner through the punish's freeze (the activation clock waits
    /// out the hitstop), and its DI is read from the stick on that freeze's
    /// thaw tick: holding Up must still bend the launch, by angle only.
    /// </summary>
    [TestCase]
    public void ACasterLaunchedOutOfItsWhiffRecoveryKeepsItsDiRead() {
        FighterSimulation neutral = NewPunishSimulation(out int tick);
        FighterSimulation influenced = NewPunishSimulation(out _);
        // The opponent jumps the straight shot and lands beside the caster.
        Press(neutral, tick);
        Press(influenced, tick);
        tick++;
        for (int step = 0; step < 8; step++, tick++) {
            neutral.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
            influenced.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        }
        neutral.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Jump));
        influenced.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Jump));
        tick++;
        // The opponent presses its launcher once it has landed beside the
        // whiffing caster; the (clocked) Special lands on its active entry. The
        // influenced caster holds Up from the press on — the Ultimate's own
        // discard eats the stick until the punish freezes it.
        AssertThat(neutral.TryGetFighter(0, out FighterStateComponent unhurt)).IsTrue();
        bool pressed = false;
        bool landed = false;
        for (int step = 0; step < 120 && !landed; step++, tick++) {
            AssertThat(neutral.TryGetFighter(1, out FighterStateComponent opponent)).IsTrue();
            bool press = !pressed && opponent.IsGrounded != 0
                && UltimateActivationTestKit.Phase(neutral, 0) == FighterUltimateActivationRules.PhaseWhiffRecovery;
            pressed |= press;
            GameplayButtons punish = press ? GameplayButtons.Special1 : GameplayButtons.None;
            neutral.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, punish));
            influenced.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None, moveY: pressed ? (sbyte)-127 : (sbyte)0),
                UltimateActivationTestKit.Input(tick, 0, punish));
            AssertThat(neutral.TryGetFighter(0, out FighterStateComponent caster)).IsTrue();
            landed = caster.CurrentHP < unhurt.CurrentHP;
        }
        AssertThat(pressed && landed).OverrideFailureMessage("Harness: the opponent never punished the whiff recovery.").IsTrue();
        AssertThat(neutral.TryGetFighterVerb(0, out FighterVerbComponent struckVerb)).IsTrue();
        AssertThat(neutral.TryGetFighter(0, out FighterStateComponent struck)).IsTrue();
        AssertThat(struckVerb.HitstopFrames > 0 && struck.HitstunFrames > 0 && struckVerb.Tumble == 1)
            .OverrideFailureMessage(
                $"Harness: the punish must launch the caster (hitstop {struckVerb.HitstopFrames}, hitstun {struck.HitstunFrames}, tumble {struckVerb.Tumble}).")
            .IsTrue();
        AssertThat(UltimateActivationTestKit.Phase(neutral, 0))
            .OverrideFailureMessage("Harness: the caster still owns the Ultimate through the punish's freeze.")
            .IsEqual(FighterUltimateActivationRules.PhaseWhiffRecovery);

        bool compared = false;
        for (int step = 0; step < 30 && !compared; step++, tick++) {
            neutral.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
            influenced.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None, moveY: -127),
                UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
            AssertThat(neutral.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
            if (verb.HitstopFrames != 0) continue;
            compared = true;
            AssertThat(neutral.TryGetFighter(0, out FighterStateComponent plain)).IsTrue();
            AssertThat(influenced.TryGetFighter(0, out FighterStateComponent bent)).IsTrue();
            AssertThat(bent.Velocity.y > plain.Velocity.y)
                .OverrideFailureMessage("Holding Up through the punish's freeze must bend the caster's launch upward.")
                .IsTrue();
            float plainSquared = plain.Velocity.x.ToFloat() * plain.Velocity.x.ToFloat() + plain.Velocity.y.ToFloat() * plain.Velocity.y.ToFloat();
            float bentSquared = bent.Velocity.x.ToFloat() * bent.Velocity.x.ToFloat() + bent.Velocity.y.ToFloat() * bent.Velocity.y.ToFloat();
            AssertThat(System.MathF.Abs(plainSquared - bentSquared) < plainSquared * 0.01f)
                .OverrideFailureMessage("DI never changes the launch magnitude.")
                .IsTrue();
        }
        AssertThat(compared).OverrideFailureMessage("Harness: the punish's freeze never ended.").IsTrue();
    }

    /// <summary>
    /// One dry run with the Ultimate pressed <paramref name="ultimateOffset"/>
    /// ticks after the caster's slow shot: the first tick on which the caster
    /// starts on its freeze's last tick (HitstopFrames 1) in
    /// <paramref name="phase"/>, or -1.
    /// </summary>
    private static int FindCasterThawTick(int ultimateOffset, int phase) {
        FighterSimulation simulation = NewShooterSimulation(out int start);
        for (int tick = start; tick < start + 260; tick++) {
            simulation.TryGetFighterVerb(0, out FighterVerbComponent verb);
            if (verb.HitstopFrames == 1 && UltimateActivationTestKit.Phase(simulation, 0) == phase) return tick;
            simulation.Advance(ShooterFrame(tick, start, ultimateOffset), UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));
        }
        return -1;
    }

    /// <summary>The caster fires its slow shot on <paramref name="start"/> and presses the Ultimate <paramref name="ultimateOffset"/> ticks later.</summary>
    private static PlayerInputFrame ShooterFrame(int tick, int start, int ultimateOffset) =>
        UltimateActivationTestKit.Input(tick, 0,
            tick == start ? GameplayButtons.Special1
            : tick == start + ultimateOffset ? GameplayButtons.Ultimate
            : GameplayButtons.None);

    /// <summary>
    /// Ten units apart (beyond the six-unit shot), the generic caster also owns
    /// a slow 5 u/s shot (Special 1, about 100 ticks to the target) and an area
    /// Special 2 whose acceptance shows as an armed cooldown.
    /// </summary>
    private static FighterSimulation NewShooterSimulation(out int nextTick) {
        CharacterData caster = BuildCaster();
        caster.SpecialAttackOne = new AbilityData {
            ExecutionType = AbilityExecutionType.Projectile,
            BaseDamage = 10f,
            ProjectileSpeed = 300f,
            ProjectileLifetime = 3f,
            StartupFrames = 0
        };
        caster.SpecialAttackTwo = new AbilityData { ExecutionType = AbilityExecutionType.Area, BaseDamage = 2f };
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(caster),
            FighterLoadoutFactory.FromCharacterData(BuildTarget()),
            seed: 13007,
            spawnDistance: 5,
            rules: FighterMatchRules.Disabled);
        nextTick = UltimateActivationTestKit.Idle(simulation, 0, 40);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        return simulation;
    }

    /// <summary>Two units apart; the opponent owns a launching melee Special 1 with no startup (it lands on its press tick).</summary>
    private static FighterSimulation NewPunishSimulation(out int nextTick) {
        CharacterData target = BuildTarget();
        target.SpecialAttackOne = new AbilityData {
            BaseDamage = 10f,
            KnockbackForce = new Vector2(4f, -4f),
            HitstunFrames = 30,
            Launches = true,
            StartupFrames = 0
        };
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildCaster()),
            FighterLoadoutFactory.FromCharacterData(target),
            seed: 13008,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        nextTick = UltimateActivationTestKit.Idle(simulation, 0, 40);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
        return simulation;
    }

    /// <summary>Whether the caster's thaw-tick press started: its cost is spent, its grab began, or the stance rose.</summary>
    private static bool CasterVerbStarted(FighterSimulation simulation, GameplayButtons pressed, GameplayButtons held) {
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        if ((pressed & GameplayButtons.Special2) != 0) return runtime.SpecialTwoReuseFrames > 0;
        if ((pressed & GameplayButtons.MovementAbility) != 0) return runtime.MovementCooldownFrames > 0;
        if ((pressed & (GameplayButtons.Grab | GameplayButtons.BasicAttack)) != 0) return verb.GrabPhase != 0;
        return (held & GameplayButtons.Block) != 0 && FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
    }

    private static void Press(FighterSimulation simulation, int tick) =>
        simulation.Advance(UltimateActivationTestKit.Input(tick, 0, GameplayButtons.Ultimate),
            UltimateActivationTestKit.Input(tick, 0, GameplayButtons.None));

    /// <summary>
    /// A generic caster (the AbilityData activation defaults) facing a 400-HP
    /// target, with the meter seeded full after a short idle. Returns the next input tick.
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
