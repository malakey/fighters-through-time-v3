using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The single air strike, Fighter side (GU-A, user request 2026-10-12: "For
/// aerial attacks, it should just be a standard attack and no longer chain the
/// combo. It should be a single attack." — both modes, by the user's scope
/// ruling). An air press with neither Up nor Down held latches
/// <see cref="FighterBasicAttackRules.FlagAirAttack"/> (the spare
/// <c>AttackFlags</c> bit 128 on component 305: no new component, no protocol
/// change) and runs the profile's air startup with the universal 7 active / 13
/// recovery frames, never buffering, holding or continuing. It swings string
/// hit 1's box for a flat 1.0x basic with hit 1's knockback and 30-frame
/// hitstun and never launches. No chain hit ever starts in the air: a
/// continuation that would begin airborne ends the string. On every authored
/// stage a walk-off is caught by the ledge grab first (which cancels the
/// string), so the rule is pinned on the phase machine itself.
///
/// <para>Pure C#: default loadouts and hand-built components, no Godot
/// runtime.</para>
/// </summary>
[TestSuite]
public class FighterAirAttackTests {
    private const int BasicButton = 1 << 2;

    [TestCase]
    public void AnAirPressLatchesTheAirStrikeWithItsProfilesFramesAndNeverChains() {
        // The profile's air startup is the retired aerial opener's: Joan 4,
        // the template 5, Einstein 6, Lincoln 7; active and recovery are 7 / 13.
        (FighterCharacterID Id, int Startup)[] cases = {
            (FighterCharacterID.Joan, 4),
            (FighterCharacterID.Tesla, 5),
            (FighterCharacterID.Einstein, 6),
            (FighterCharacterID.Lincoln, 7)
        };
        foreach ((FighterCharacterID id, int startup) in cases) {
            AssertThat(FighterBasicAttackRules.StringProfileFor((int)id).AirAttackStartupFrames).IsEqual(startup);
            // Eight units apart: nothing can land, so no hitstop bends the clock.
            var simulation = new FighterSimulation(
                FighterLoadout.Default(id),
                FighterLoadout.Default(FighterCharacterID.Tesla),
                seed: 611, spawnDistance: 4, rules: FighterMatchRules.Disabled);
            Step(simulation, GameplayButtons.Jump);
            Step(simulation, GameplayButtons.BasicAttack);
            AssertThat(Fighter(simulation, 0).IsGrounded).IsEqual(0);
            FighterRuntimeComponent started = Runtime(simulation, 0);
            AssertThat(started.AttackFlags & FighterBasicAttackRules.FlagAirAttack)
                .OverrideFailureMessage($"{id}: an air press must latch the air strike.")
                .IsEqual(FighterBasicAttackRules.FlagAirAttack);
            AssertThat(started.AttackFlags & FighterBasicAttackRules.FlagAerial).IsEqual(FighterBasicAttackRules.FlagAerial);
            AssertThat(started.AttackFlags & (FighterBasicAttackRules.FlagUpAttack | FighterBasicAttackRules.FlagDownAir))
                .IsEqual(0);
            AssertThat(FighterBasicAttackRules.IsVariantSwing(in started)).IsTrue();
            AssertThat(started.ComboIndex).IsEqual(0);
            AssertThat(started.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
            AssertThat(started.AttackPhaseFrames).IsEqual(startup);
            AssertThat(FighterBasicAttackRules.NextChainStep(in started))
                .OverrideFailureMessage($"{id}: the CPU band must read an opener during the air strike.")
                .IsEqual(0);

            // Mash BasicAttack on every tick of the strike: every press is dropped.
            int previousPhase = FighterBasicAttackRules.PhaseStartup;
            int ticks = 0;
            for (; ticks < 120; ticks++) {
                Step(simulation, GameplayButtons.BasicAttack);
                FighterRuntimeComponent runtime = Runtime(simulation, 0);
                AssertThat(runtime.AttackPhase)
                    .OverrideFailureMessage($"{id}: the air strike entered the chain hold.")
                    .IsNotEqual(FighterBasicAttackRules.PhaseChainHold);
                AssertThat(runtime.AttackFlags & (FighterBasicAttackRules.FlagBuffered | FighterBasicAttackRules.FlagQueuedBuffered))
                    .OverrideFailureMessage($"{id}: a press during the air strike was buffered.")
                    .IsEqual(0);
                AssertThat(runtime.ComboIndex).IsEqual(0);
                if (runtime.AttackPhase == previousPhase) continue;
                if (runtime.AttackPhase == FighterBasicAttackRules.PhaseActive) {
                    AssertThat(runtime.AttackPhaseFrames).IsEqual(BasicComboRules.AirAttackActiveFrames);
                } else if (runtime.AttackPhase == FighterBasicAttackRules.PhaseRecovery) {
                    AssertThat(runtime.AttackPhaseFrames).IsEqual(BasicComboRules.AirAttackRecoveryFrames);
                } else if (runtime.AttackPhase == FighterBasicAttackRules.PhaseNone) {
                    ticks++;
                    break;
                }
                previousPhase = runtime.AttackPhase;
            }
            AssertThat(ticks)
                .OverrideFailureMessage($"{id}: the air strike must run its whole window and exit straight out.")
                .IsEqual(startup + BasicComboRules.AirAttackActiveFrames + BasicComboRules.AirAttackRecoveryFrames);
            AssertThat(Fighter(simulation, 0).IsGrounded)
                .OverrideFailureMessage($"{id}: harness — the strike must end in the air, not on a landing cancel.")
                .IsEqual(0);
        }
    }

    [TestCase]
    public void TheAirStrikeLandsOneBasicAtHitOnesKnockbackWithoutLaunching() {
        // Joan's string hit 1 is authored at 1.0x (DamageTenths[0] = 10), the
        // air strike's flat multiplier, so her grounded hit 1 is the exact
        // control: the same damage, the same knockback multiplier, the same
        // 30-frame hitstun, and the same grounded slide on the victim.
        AssertThat(BasicComboRules.StringProfileFor("joan").DamageTenths[0]).IsEqual(10);
        AssertThat(BasicComboRules.AirAttackDamageMultiplier).IsEqual(1f);

        Struck grounded = StrikeTesla(fromTheAir: false);
        Struck air = StrikeTesla(fromTheAir: true);
        int basicDamage = FighterLoadout.Default(FighterCharacterID.Joan).BasicDamage;

        AssertThat(air.Flags & FighterBasicAttackRules.FlagAirAttack).IsEqual(FighterBasicAttackRules.FlagAirAttack);
        AssertThat(grounded.Flags & FighterBasicAttackRules.VariantMask).IsEqual(0);
        AssertThat(air.Damage).OverrideFailureMessage("The air strike deals a flat 1.0x basic.").IsEqual(basicDamage);
        AssertThat(grounded.Damage).IsEqual(basicDamage);
        AssertThat(air.Victim.HitstunFrames).IsEqual(BasicComboRules.AirAttackHitstunFrames);
        AssertThat(grounded.Victim.HitstunFrames).IsEqual(BasicComboRules.HitstunFrames[0]);
        AssertThat(air.Victim.Velocity.x.RawValue)
            .OverrideFailureMessage("The air strike's knockback must be string hit 1's.")
            .IsEqual(grounded.Victim.Velocity.x.RawValue);
        AssertThat(air.Victim.Velocity.x > FP64.Zero)
            .OverrideFailureMessage("The air strike pushes the victim away.")
            .IsTrue();
        AssertThat(air.Victim.IsGrounded)
            .OverrideFailureMessage("A non-launching hit leaves a grounded victim on the ground.")
            .IsEqual(1);
        AssertThat(air.Victim.Velocity.y).IsEqual(FP64.Zero);

        // D1 / D2: a landed air strike opens the hit-confirm and pays string hit
        // 1's 20 frames of per-hit Momentum on both running cooldowns.
        AssertThat(air.SwingLanded).IsEqual(1);
        AssertThat(air.MomentumSlotOne).IsEqual(BasicComboRules.MomentumStringHitRefundFrames);
        AssertThat(air.MomentumSlotTwo).IsEqual(BasicComboRules.MomentumStringHitRefundFrames);
    }

    [TestCase]
    public void AChainContinuationThatWouldStartAirborneEndsTheStringInsteadOfChaining() {
        // The phase machine itself, on a hand-built Tesla mid-string: grounded,
        // every continuation starts hit 2; airborne (a string dropped through a
        // one-way platform during its startup or active frames), the held or
        // buffered continuation ends the string and a fresh press in the chain
        // hold is an air press.
        foreach (bool airborne in new[] { false, true }) {
            string where = airborne ? "airborne" : "grounded";

            FighterRuntimeComponent held = Mid(FighterBasicAttackRules.PhaseRecovery, held: BasicButton);
            Process(airborne, ref held);
            AssertContinuedOrEnded(in held, airborne, $"{where}, held at the recovery end");

            FighterRuntimeComponent buffered = Mid(FighterBasicAttackRules.PhaseRecovery);
            buffered.AttackFlags = FighterBasicAttackRules.FlagBuffered;
            Process(airborne, ref buffered);
            AssertContinuedOrEnded(in buffered, airborne, $"{where}, buffered at the recovery end");

            FighterRuntimeComponent heldInHold = Mid(FighterBasicAttackRules.PhaseChainHold, held: BasicButton);
            Process(airborne, ref heldInHold);
            AssertContinuedOrEnded(in heldInHold, airborne, $"{where}, held in the chain hold");

            FighterRuntimeComponent pressedInHold = Mid(FighterBasicAttackRules.PhaseChainHold, held: BasicButton);
            pressedInHold.PressedButtons = BasicButton;
            Process(airborne, ref pressedInHold);
            AssertThat(pressedInHold.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseStartup);
            if (airborne) {
                AssertThat(pressedInHold.AttackFlags & FighterBasicAttackRules.FlagAirAttack)
                    .OverrideFailureMessage("A press in an airborne chain hold must start the air strike.")
                    .IsEqual(FighterBasicAttackRules.FlagAirAttack);
                AssertThat(pressedInHold.ComboIndex).IsEqual(0);
            } else {
                AssertThat(pressedInHold.AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);
                AssertThat(pressedInHold.ComboIndex).IsEqual(1);
            }
        }
    }

    // === helpers ==============================================================

    private readonly struct Struck {
        public Struck(FighterStateComponent victim, int damage, int flags, int swingLanded, int momentumOne, int momentumTwo) {
            Victim = victim;
            Damage = damage;
            Flags = flags;
            SwingLanded = swingLanded;
            MomentumSlotOne = momentumOne;
            MomentumSlotTwo = momentumTwo;
        }

        public FighterStateComponent Victim { get; }
        public int Damage { get; }
        public int Flags { get; }
        public int SwingLanded { get; }
        public int MomentumSlotOne { get; }
        public int MomentumSlotTwo { get; }
    }

    /// <summary>
    /// A Joan walked up to a standing Tesla, then either a grounded opener or a
    /// jump and an air press; the state at the end of the tick the hit lands
    /// (the victim's knockback is set and the shared freeze has begun).
    /// </summary>
    private static Struck StrikeTesla(bool fromTheAir) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan),
            FighterLoadout.Default(FighterCharacterID.Tesla),
            seed: 612, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        for (int step = 0; step < 60; step++) {
            bool close = Fighter(simulation, 1).Position.x - Fighter(simulation, 0).Position.x <= FP64.FromDouble(1.0);
            if (close) break;
            Step(simulation, GameplayButtons.None, moveX: 127);
        }
        for (int step = 0; step < 20; step++) Step(simulation, GameplayButtons.None);
        AssertThat(Fighter(simulation, 0).FacingRight).IsEqual(1);
        AssertThat(simulation.SeedFighterSpecialCooldownsForTest(0, 300, 300)).IsTrue();
        int hpBefore = Fighter(simulation, 1).CurrentHP;
        if (fromTheAir) {
            Step(simulation, GameplayButtons.Jump);
            Step(simulation, GameplayButtons.BasicAttack);
            AssertThat(Fighter(simulation, 0).IsGrounded).IsEqual(0);
        } else {
            Step(simulation, GameplayButtons.BasicAttack);
        }
        int flags = Runtime(simulation, 0).AttackFlags;
        for (int step = 0; step < 30 && Fighter(simulation, 1).CurrentHP == hpBefore; step++) {
            Step(simulation, GameplayButtons.None);
        }
        FighterStateComponent victim = Fighter(simulation, 1);
        AssertThat(victim.CurrentHP < hpBefore)
            .OverrideFailureMessage($"Harness: the {(fromTheAir ? "air strike" : "grounded opener")} never landed.")
            .IsTrue();
        if (fromTheAir) AssertThat(Fighter(simulation, 0).IsGrounded).IsEqual(0);
        FighterVerbComponent verb = Verb(simulation, 0);
        return new Struck(
            victim, hpBefore - victim.CurrentHP, flags, Runtime(simulation, 0).SwingLanded,
            verb.MomentumRefundedFramesSlotOne, verb.MomentumRefundedFramesSlotTwo);
    }

    /// <summary>A Tesla's string at hit 1, one frame from the end of <paramref name="phase"/>.</summary>
    private static FighterRuntimeComponent Mid(int phase, int held = 0) => new() {
        AttackPhase = phase,
        AttackPhaseFrames = phase == FighterBasicAttackRules.PhaseChainHold ? 10 : 1,
        ComboIndex = 0,
        HeldButtons = held,
        LedgeAnchor = FighterLedgeRules.NoAnchor
    };

    private static void Process(bool airborne, ref FighterRuntimeComponent runtime) {
        var fighter = new FighterStateComponent {
            CharacterID = (int)FighterCharacterID.Tesla,
            Stocks = 3,
            MaxHP = 100,
            CurrentHP = 100,
            BlockCharges = 3,
            FacingRight = 1,
            IsGrounded = airborne ? 0 : 1
        };
        var verb = new FighterVerbComponent();
        FighterMovementSystem.ProcessBasicAttackPhase(ref fighter, ref runtime, in verb);
    }

    private static void AssertContinuedOrEnded(in FighterRuntimeComponent runtime, bool airborne, string what) {
        if (airborne) {
            AssertThat(runtime.AttackPhase)
                .OverrideFailureMessage($"{what}: no chain hit may start in the air; the string must end.")
                .IsEqual(FighterBasicAttackRules.PhaseNone);
            AssertThat(runtime.ComboIndex).IsEqual(0);
            AssertThat(runtime.AttackFlags).IsEqual(0);
        } else {
            AssertThat(runtime.AttackPhase)
                .OverrideFailureMessage($"{what}: the grounded string must continue.")
                .IsEqual(FighterBasicAttackRules.PhaseStartup);
            AssertThat(runtime.ComboIndex).IsEqual(1);
            AssertThat(runtime.AttackFlags & FighterBasicAttackRules.FlagAerial).IsEqual(0);
        }
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent state)).IsTrue();
        return state;
    }

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        return runtime;
    }

    private static FighterVerbComponent Verb(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return verb;
    }

    private static long Step(FighterSimulation simulation, GameplayButtons playerOne, sbyte moveX = 0) =>
        simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)simulation.CurrentTick,
                MoveX = moveX,
                Held = playerOne,
                Pressed = playerOne
            },
            new PlayerInputFrame { Tick = (uint)simulation.CurrentTick });
}
