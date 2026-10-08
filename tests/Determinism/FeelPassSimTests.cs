using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// 2026-10-04 playtest feel pass, workstream FEEL — the Fighter-sim half
/// (docs/PLAYTEST_FEEL_2026-10-04_PLAN.md §2.1). The deterministic phase
/// machine mirrors Story's chain buffer (F3): an ACTIVE-frame press and a press
/// made inside the hitstop freeze both buffer the next hit, a second press
/// queues for the hit after (a spare <c>AttackFlags</c> bit, no new component),
/// and holding BasicAttack continues the chain. The provisional hitstop weight
/// (F6) reaches the sim through the one shared rule, and the presentation
/// driver poses attack sheets from the sim's phases (F1) without writing state.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FeelPassSimTests {

    [TestCase]
    public void AnActiveFramePressBuffersTheNextSwing() {
        // A whiff (far spawn), so no hitstop shifts the phase clock.
        var simulation = NewTeslaSimulation(seed: 4101, spawnDistance: 6);
        int startup = BasicComboRules.GroundStartupFrames[0];
        int active = BasicComboRules.GroundActiveFrames[0];
        int recovery = BasicComboRules.GroundRecoveryFrames[0];
        Press(simulation, 0);
        for (int tick = 1; tick <= startup; tick++) Neutral(simulation, tick);
        AssertThat(Runtime(simulation).AttackPhase).IsEqual(FighterBasicAttackRules.PhaseActive);

        Press(simulation, startup + 1);
        AssertThat((Runtime(simulation).AttackFlags & FighterBasicAttackRules.FlagBuffered) != 0)
            .OverrideFailureMessage("A press during the active frames must buffer the next swing.")
            .IsTrue();
        int recoveryEnd = startup + active + recovery;
        for (int tick = startup + 2; tick <= recoveryEnd; tick++) {
            Neutral(simulation, tick);
            AssertThat(Runtime(simulation).AttackPhase)
                .OverrideFailureMessage($"A buffered string must never fall into the chain hold (tick {tick}).")
                .IsNotEqual(FighterBasicAttackRules.PhaseChainHold);
        }
        FighterRuntimeComponent chained = Runtime(simulation);
        AssertThat(chained.ComboIndex).IsEqual(1);
        AssertThat(chained.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
    }

    [TestCase]
    public void APressInsideTheHitstopFreezeBuffersTheNextSwing() {
        var simulation = NewTeslaSimulation(seed: 4102, spawnDistance: 1);
        int startup = BasicComboRules.GroundStartupFrames[0];
        Press(simulation, 0);
        for (int tick = 1; tick <= startup; tick++) Neutral(simulation, tick);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent frozen)).IsTrue();
        AssertThat(frozen.HitstopFrames > 0)
            .OverrideFailureMessage("The opener must have connected and frozen the attacker.")
            .IsTrue();

        Press(simulation, startup + 1);
        AssertThat((Runtime(simulation).AttackFlags & FighterBasicAttackRules.FlagBuffered) != 0)
            .OverrideFailureMessage("A press made while frozen must buffer, not be dropped.")
            .IsTrue();
        bool sawHold = false;
        for (int tick = startup + 2; tick < startup + 80 && Runtime(simulation).ComboIndex == 0; tick++) {
            Neutral(simulation, tick);
            if (Runtime(simulation).AttackPhase == FighterBasicAttackRules.PhaseChainHold) sawHold = true;
        }
        AssertThat(Runtime(simulation).ComboIndex).IsEqual(1);
        AssertThat(sawHold).IsFalse();
    }

    [TestCase]
    public void HoldingBasicAttackRunsTheWholeStringAndNeverRestartsIt() {
        var simulation = NewTeslaSimulation(seed: 4103, spawnDistance: 6);
        Press(simulation, 0);
        int highest = 0;
        int tick = 1;
        for (; tick < 200; tick++) {
            Hold(simulation, tick);
            FighterRuntimeComponent runtime = Runtime(simulation);
            if (runtime.ComboIndex > highest) highest = runtime.ComboIndex;
            if (highest == BasicComboRules.ComboHits - 1
                && runtime.AttackPhase == FighterBasicAttackRules.PhaseNone) break;
        }
        AssertThat(highest)
            .OverrideFailureMessage("Holding the attack button continues the chain through the finisher.")
            .IsEqual(BasicComboRules.ComboHits - 1);
        for (int extra = 1; extra <= 20; extra++) Hold(simulation, tick + extra);
        AssertThat(Runtime(simulation).AttackPhase)
            .OverrideFailureMessage("A held button is not a press: the finisher exits and nothing restarts.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);
    }

    [TestCase]
    public void ThreePressesOnATwelveTickRhythmStartThreeSwings() {
        var simulation = NewTeslaSimulation(seed: 4104, spawnDistance: 6);
        int highest = 0;
        for (int tick = 0; tick < 150; tick++) {
            if (tick == 0 || tick == 12 || tick == 24) Press(simulation, tick);
            else Neutral(simulation, tick);
            int combo = Runtime(simulation).ComboIndex;
            if (combo > highest) highest = combo;
        }
        AssertThat(highest)
            .OverrideFailureMessage("The second press (active) buffers and the third (recovery) queues.")
            .IsEqual(2);
    }

    [TestCase]
    public void TheLaunchingFinisherFreezesBothFightersForTheLaunchBonus() {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 4105,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        int hits = 0;
        bool checkedOpener = false;
        bool checkedFinisher = false;
        for (int tick = 0; tick < 200 && hits < BasicComboRules.ComboHits; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            Press(simulation, tick);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            int dealt = before.CurrentHP - after.CurrentHP;
            if (dealt <= 0) continue;
            int step = hits;
            hits++;
            bool launches = BasicComboRules.StringHitLaunchesFor(BasicComboRules.TemplateStringProfile, step);
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent victim)).IsTrue();
            int expected = BasicComboRules.HitstopFrames(dealt, launches);
            AssertThat(attacker.HitstopFrames)
                .OverrideFailureMessage($"Hit {step + 1}: wrong attacker freeze.")
                .IsEqual(expected);
            AssertThat(victim.HitstopFrames)
                .OverrideFailureMessage($"Hit {step + 1}: wrong victim freeze.")
                .IsEqual(expected);
            if (step == 0) checkedOpener = !launches;
            if (step == BasicComboRules.ComboHits - 1) {
                checkedFinisher = launches;
                AssertThat(expected - BasicComboRules.HitstopFrames(dealt))
                    .IsEqual(BasicComboRules.LaunchHitstopBonusFrames);
            }
        }
        AssertThat(checkedOpener).OverrideFailureMessage("The opener never connected.").IsTrue();
        AssertThat(checkedFinisher).OverrideFailureMessage("The launching finisher never connected.").IsTrue();
    }

    [TestCase]
    public void ThePresentationPosesFollowTheSimPhases() {
        AssertThat(FighterSimulationDriver.BasicSwingPose(FighterBasicAttackRules.PhaseStartup))
            .IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(FighterSimulationDriver.BasicSwingPose(FighterBasicAttackRules.PhaseActive))
            .IsEqual(AttackPoseRules.StrikePose);
        AssertThat(FighterSimulationDriver.BasicSwingPose(FighterBasicAttackRules.PhaseRecovery))
            .IsEqual(AttackPoseRules.FollowThroughPose);

        AssertThat(FighterSimulationDriver.ActivationStrikePose(FighterUltimateActivationRules.PhaseWindup))
            .IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(FighterSimulationDriver.ActivationStrikePose(FighterUltimateActivationRules.PhaseActive))
            .IsEqual(AttackPoseRules.StrikePose);
        AssertThat(FighterSimulationDriver.ActivationStrikePose(FighterUltimateActivationRules.PhaseWhiffRecovery))
            .IsEqual(AttackPoseRules.FollowThroughPose);
        // R11b (fix pass): the cinematic is the Ultimate landing — the strike.
        AssertThat(FighterSimulationDriver.ActivationStrikePose(FighterUltimateActivationRules.PhaseCinematic))
            .IsEqual(AttackPoseRules.StrikePose);

        // R11a (fix pass): Specials and the Ultimate are posed; the movement
        // ability plays its reel, as it does in Story.
        AssertThat(FighterSimulationDriver.PosesAbilityHold("special_1")).IsTrue();
        AssertThat(FighterSimulationDriver.PosesAbilityHold("special_2")).IsTrue();
        AssertThat(FighterSimulationDriver.PosesAbilityHold("ultimate")).IsTrue();
        AssertThat(FighterSimulationDriver.PosesAbilityHold(FighterSimulationDriver.MovementAbilityAnimation)).IsFalse();

        AssertThat(FighterSimulationDriver.ForesightPose(FighterForesightRules.PhaseStartup))
            .IsEqual(AttackPoseRules.WindUpPose);
        AssertThat(FighterSimulationDriver.ForesightPose(FighterForesightRules.PhaseWindow))
            .IsEqual(AttackPoseRules.StrikePose);
        AssertThat(FighterSimulationDriver.ForesightPose(FighterForesightRules.PhaseSidestep))
            .IsEqual(AttackPoseRules.FollowThroughPose);
    }

    // === R11 (fix pass): the driver's ability presentation ======================

    [TestCase]
    public void TheFighterMovementAbilityPlaysItsReelRatherThanAPose() {
        AssertObject(InputManager.Instance).IsNotNull();
        (FighterSimulationDriver driver, Node host, PlayerController one) = CreateDriver("FeelMovementReelHost");
        var playerOne = new BufferedInputSource();
        var playerTwo = new BufferedInputSource();
        try {
            RunCountdown(driver, playerOne, playerTwo);
            DriverStep(driver, playerOne, playerTwo, GameplayButtons.MovementAbility, GameplayButtons.None);
            var sprite = one.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            int reelFrames = 0;
            GameplayButtons previous = GameplayButtons.MovementAbility;
            for (int frame = 0; frame < 30; frame++) {
                if (sprite.Animation == new StringName(FighterSimulationDriver.MovementAbilityAnimation)) {
                    reelFrames++;
                    AssertThat(one.IsShowingAttackPose)
                        .OverrideFailureMessage($"The movement ability was posed (frame {frame}); Story plays it as a reel.")
                        .IsFalse();
                    AssertThat(sprite.IsPlaying()).IsTrue();
                }
                DriverStep(driver, playerOne, playerTwo, GameplayButtons.None, previous);
                previous = GameplayButtons.None;
            }
            AssertThat(reelFrames > 0)
                .OverrideFailureMessage("The accepted movement ability never presented its sheet.")
                .IsTrue();
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host.Free();
        }
    }

    [TestCase]
    public void TheWholeActivationStrikeIsPosedFromItsPhase() {
        AssertObject(InputManager.Instance).IsNotNull();
        (FighterSimulationDriver driver, Node host, PlayerController one) = CreateDriver("FeelActivationPoseHost");
        var playerOne = new BufferedInputSource();
        var playerTwo = new BufferedInputSource();
        try {
            RunCountdown(driver, playerOne, playerTwo);
            AssertThat(driver.Simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            DriverStep(driver, playerOne, playerTwo, GameplayButtons.Ultimate, GameplayButtons.None);
            var sprite = one.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            var phasesSeen = new System.Collections.Generic.HashSet<int>();
            GameplayButtons previous = GameplayButtons.Ultimate;
            bool ended = false;
            for (int frame = 0; frame < 600; frame++) {
                AssertThat(driver.Simulation.TryGetFighterUltimateActivation(0, out FighterUltimateActivationComponent activation))
                    .IsTrue();
                if (activation.Phase == FighterUltimateActivationRules.PhaseNone) {
                    ended = true;
                    break;
                }
                phasesSeen.Add(activation.Phase);
                AssertThat(sprite.Animation)
                    .OverrideFailureMessage($"Phase {activation.Phase}: the caster left the ultimate sheet.")
                    .IsEqual(new StringName("ultimate"));
                AssertThat(sprite.Frame)
                    .OverrideFailureMessage($"Phase {activation.Phase}: wrong pose.")
                    .IsEqual(FighterSimulationDriver.ActivationStrikePose(activation.Phase));
                AssertThat(one.IsShowingAttackPose).IsTrue();
                DriverStep(driver, playerOne, playerTwo, GameplayButtons.None, previous);
                previous = GameplayButtons.None;
            }
            AssertThat(ended).OverrideFailureMessage("The activation never ended.").IsTrue();
            AssertThat(phasesSeen.Contains(FighterUltimateActivationRules.PhaseWhiffRecovery)
                    || phasesSeen.Contains(FighterUltimateActivationRules.PhaseCinematic))
                .OverrideFailureMessage("The strike never reached its whiff recovery or cinematic.")
                .IsTrue();
            // No cosmetic hold trails the activation: control is back, so is the body's reel.
            AssertThat(sprite.Animation).IsNotEqual(new StringName("ultimate"));
        } finally {
            InputManager.Instance.ClearInputSource(0);
            InputManager.Instance.ClearInputSource(1);
            host.Free();
        }
    }

    // === helpers ==============================================================

    private const double PresentationStep = 1.0 / 60.0;

    private static (FighterSimulationDriver driver, Node host, PlayerController one) CreateDriver(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = name };
        tree.Root.AddChild(host);
        PlayerController one = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        PlayerController two = CharacterFactory.CreateCharacter("joan", 1, applyStoryProgression: false);
        host.AddChild(one);
        host.AddChild(two);
        var driver = new FighterSimulationDriver { Name = "Driver" };
        host.AddChild(driver);
        driver.Initialize(one, two, MatchSettings.GetDefault(),
            stageHazardTypeID: 1, stageID: "florence_workshop", matchSeed: 2026);
        return (driver, host, one);
    }

    private static void RunCountdown(FighterSimulationDriver driver, BufferedInputSource one, BufferedInputSource two) {
        for (int frame = 0; frame < FighterMatchFlowRules.CountdownFrames + 2; frame++) {
            DriverStep(driver, one, two, GameplayButtons.None, GameplayButtons.None);
        }
    }

    /// <summary>
    /// One driver tick with player one holding <paramref name="held"/> against
    /// <paramref name="previousHeld"/>. Manual ticks share one engine physics
    /// frame ID, so the sources are re-registered to invalidate the
    /// InputManager's per-frame cache (FighterPresentationSyncTests' idiom).
    /// </summary>
    private static void DriverStep(FighterSimulationDriver driver, BufferedInputSource one, BufferedInputSource two,
        GameplayButtons held, GameplayButtons previousHeld) {
        uint tick = (uint)driver.CurrentTick;
        one.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, held, previousHeld));
        two.SetNextFrame(PlayerInputFrame.Create(tick, 0f, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(0, one);
        InputManager.Instance.SetInputSource(1, two);
        driver._PhysicsProcess(PresentationStep);
    }

    private static FighterSimulation NewTeslaSimulation(int seed, int spawnDistance) => new(
        FighterCharacterID.Tesla,
        FighterCharacterID.Joan,
        seed: seed,
        spawnDistance: spawnDistance,
        rules: FighterMatchRules.Disabled);

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static void Press(FighterSimulation simulation, int tick) =>
        Step(simulation, tick, GameplayButtons.BasicAttack, GameplayButtons.BasicAttack);

    private static void Hold(FighterSimulation simulation, int tick) =>
        Step(simulation, tick, GameplayButtons.BasicAttack, GameplayButtons.None);

    private static void Neutral(FighterSimulation simulation, int tick) =>
        Step(simulation, tick, GameplayButtons.None, GameplayButtons.None);

    private static void Step(FighterSimulation simulation, int tick, GameplayButtons held, GameplayButtons pressed) {
        simulation.Advance(
            new PlayerInputFrame { Tick = (uint)tick, Held = held, Pressed = pressed },
            new PlayerInputFrame { Tick = (uint)tick });
    }

    /// <summary>The parity suite's synthetic template attacker (basic damage 10, no knockback).</summary>
    private static CharacterData BuildZeroKnockbackAttacker() => new() {
        CharacterID = "tesla",
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
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
