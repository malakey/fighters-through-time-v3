using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the two directional basics added by the gameplay-feel batch §2.8: the
/// up-attack (Up held + BasicAttack, grounded or airborne) and the down-air
/// (airborne + Down held + BasicAttack). Both are single strikes outside the
/// three-hit chain — no chain-hold, no buffering, combo index reset — and both
/// launch the victim upward. Every number comes from
/// <see cref="BasicComboRules"/>, the same table Story's
/// <c>PlayerController</c> reads, so drift in either mode breaks this suite.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DirectionalAttackTests {

    // Quantized "Up held" (MoveY < -30) and "Down held" (the Down button bit),
    // exactly as the sampler produces them.
    private const sbyte UpAxis = -127;

    [TestCase]
    public void TheSharedSelectionRuleIsTheOneSourceForBothModes() {
        // Up wins outright, grounded or airborne.
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: true, downHeld: false, airborne: false))
            .IsEqual(BasicComboRules.VariantUpAttack);
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: true, downHeld: false, airborne: true))
            .IsEqual(BasicComboRules.VariantUpAttack);
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: true, downHeld: true, airborne: true))
            .IsEqual(BasicComboRules.VariantUpAttack);
        // Down is the down-air only in the air.
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: false, downHeld: true, airborne: true))
            .IsEqual(BasicComboRules.VariantDownAir);
        // Grounded Down is explicitly the normal string, not a down-attack.
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: false, downHeld: true, airborne: false))
            .IsEqual(BasicComboRules.VariantChain);
        AssertThat(BasicComboRules.SelectAttackVariant(upHeld: false, downHeld: false, airborne: false))
            .IsEqual(BasicComboRules.VariantChain);
    }

    [TestCase]
    public void TheUpAttackRunsItsAuthoredFramesGroundedAndAirborne() {
        // Grounded: startup commits, then the hit lands on the first active tick.
        var grounded = NewOverlappingSimulation(seed: 501);
        AssertThat(grounded.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        Advance(grounded, 0, p1MoveY: UpAxis, p1Buttons: GameplayButtons.BasicAttack);
        AssertThat(grounded.TryGetFighterRuntime(0, out FighterRuntimeComponent started)).IsTrue();
        AssertThat(started.AttackFlags & FighterBasicAttackRules.FlagUpAttack)
            .OverrideFailureMessage("Up + BasicAttack while grounded must latch the up-attack flag.")
            .IsEqual(FighterBasicAttackRules.FlagUpAttack);
        AssertThat(started.AttackFlags & FighterBasicAttackRules.FlagDownAir).IsEqual(0);

        for (int tick = 1; tick < BasicComboRules.UpAttackStartupFrames; tick++) {
            Advance(grounded, tick, p1MoveY: UpAxis);
            AssertThat(grounded.TryGetFighter(1, out FighterStateComponent duringStartup)).IsTrue();
            AssertThat(duringStartup.CurrentHP)
                .OverrideFailureMessage($"No damage may land during up-attack startup (tick {tick}).")
                .IsEqual(before.CurrentHP);
        }
        Advance(grounded, BasicComboRules.UpAttackStartupFrames, p1MoveY: UpAxis);
        AssertThat(grounded.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        int expectedDamage = FighterLoadout.Default(FighterCharacterID.Tesla).BasicDamage;
        AssertThat(before.CurrentHP - struck.CurrentHP)
            .OverrideFailureMessage("The up-attack deals a flat 1.0x basic.")
            .IsEqual(expectedDamage);
        AssertThat(struck.HitstunFrames).IsEqual(BasicComboRules.DirectionalAttackHitstunFrames);

        // The whole swing is startup + active + recovery — plus the attacker's
        // own V7.1 hitstop freeze from the connect, which holds the phase
        // machine for the shared window before active/recovery resume.
        int total = BasicComboRules.UpAttackStartupFrames
            + BasicComboRules.UpAttackActiveFrames
            + BasicComboRules.UpAttackRecoveryFrames
            + BasicComboRules.HitstopFrames(expectedDamage);
        for (int tick = BasicComboRules.UpAttackStartupFrames + 1; tick < total; tick++) {
            Advance(grounded, tick, p1MoveY: UpAxis);
            AssertThat(grounded.TryGetFighterRuntime(0, out FighterRuntimeComponent midSwing)).IsTrue();
            AssertThat(midSwing.AttackPhase)
                .OverrideFailureMessage($"The up-attack must still be running at tick {tick}.")
                .IsNotEqual(FighterBasicAttackRules.PhaseNone);
        }
        Advance(grounded, total, p1MoveY: UpAxis);
        AssertThat(grounded.TryGetFighterRuntime(0, out FighterRuntimeComponent finished)).IsTrue();
        AssertThat(finished.AttackPhase)
            .OverrideFailureMessage("The up-attack exits straight out — never into the chain-hold window.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);

        // Airborne: the same variant, the same single frame table (there is no
        // aerial up-attack variant).
        var airborne = NewOverlappingSimulation(seed: 502);
        Advance(airborne, 0, p1Buttons: GameplayButtons.Jump);
        Advance(airborne, 1, p1MoveY: UpAxis, p1Buttons: GameplayButtons.BasicAttack);
        AssertThat(airborne.TryGetFighter(0, out FighterStateComponent inAir)).IsTrue();
        AssertThat(inAir.IsGrounded).IsEqual(0);
        AssertThat(airborne.TryGetFighterRuntime(0, out FighterRuntimeComponent aerialSwing)).IsTrue();
        AssertThat(aerialSwing.AttackFlags & FighterBasicAttackRules.FlagUpAttack)
            .IsEqual(FighterBasicAttackRules.FlagUpAttack);
        AssertThat(aerialSwing.AttackPhaseFrames)
            .OverrideFailureMessage("An airborne up-attack uses the same startup as a grounded one.")
            .IsEqual(BasicComboRules.UpAttackStartupFrames);
    }

    [TestCase]
    public void TheDownAirIsAerialOnlyAndGroundedDownRunsTheNormalString() {
        // Grounded Down + BasicAttack: the ordinary string opener, no variant.
        var grounded = NewOverlappingSimulation(seed: 503);
        Advance(grounded, 0,
            p1Buttons: GameplayButtons.BasicAttack | GameplayButtons.Down,
            p1Held: GameplayButtons.Down);
        AssertThat(grounded.TryGetFighterRuntime(0, out FighterRuntimeComponent chain)).IsTrue();
        AssertThat(chain.AttackFlags & FighterBasicAttackRules.VariantMask)
            .OverrideFailureMessage("A grounded down-attack is a standard attack, not a variant.")
            .IsEqual(0);
        AssertThat(chain.AttackPhaseFrames).IsEqual(BasicComboRules.GroundStartupFrames[0]);

        // Airborne Down + BasicAttack: the down-air. Thrown from the apex — the
        // jump goes up first (Down held on the rise would fast-fall instantly),
        // which also keeps Down+Jump off the same tick so it cannot read as a
        // platform drop-through.
        var airborne = NewOverlappingSimulation(seed: 504);
        AssertThat(airborne.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int diveTick = JumpToApex(airborne, 0);
        Advance(airborne, diveTick,
            p1Buttons: GameplayButtons.BasicAttack | GameplayButtons.Down,
            p1Held: GameplayButtons.Down);
        AssertThat(airborne.TryGetFighterRuntime(0, out FighterRuntimeComponent dive)).IsTrue();
        AssertThat(dive.AttackFlags & FighterBasicAttackRules.FlagDownAir)
            .OverrideFailureMessage("Airborne Down + BasicAttack must latch the down-air flag.")
            .IsEqual(FighterBasicAttackRules.FlagDownAir);
        AssertThat(dive.AttackPhaseFrames).IsEqual(BasicComboRules.DownAirStartupFrames);

        for (int tick = diveTick + 1; tick <= diveTick + BasicComboRules.DownAirStartupFrames; tick++) {
            Advance(airborne, tick, p1Held: GameplayButtons.Down);
        }
        AssertThat(airborne.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        int expectedDamage = FighterLoadout.Default(FighterCharacterID.Tesla).BasicDamage;
        AssertThat(before.CurrentHP - struck.CurrentHP)
            .OverrideFailureMessage("The down-air deals a flat 1.0x basic to the fighter below.")
            .IsEqual(expectedDamage);
        AssertThat(struck.HitstunFrames).IsEqual(BasicComboRules.DirectionalAttackHitstunFrames);
    }

    [TestCase]
    public void BothVariantsLaunchTheVictimUpward() {
        var up = NewOverlappingSimulation(seed: 505);
        for (int tick = 0; tick <= BasicComboRules.UpAttackStartupFrames; tick++) {
            Advance(up, tick,
                p1MoveY: UpAxis,
                p1Buttons: tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None);
        }
        AssertThat(up.TryGetFighter(1, out FighterStateComponent launched)).IsTrue();
        AssertThat(launched.Velocity.y > FP64.Zero)
            .OverrideFailureMessage("The up-attack must launch the victim upward (world Y is up).")
            .IsTrue();
        AssertThat(launched.IsGrounded)
            .OverrideFailureMessage("A launched victim leaves the ground.")
            .IsEqual(0);
        AssertThat(launched.HitstunFrames).IsEqual(BasicComboRules.DirectionalAttackHitstunFrames);

        var down = NewOverlappingSimulation(seed: 506);
        int diveTick = JumpToApex(down, 0);
        for (int tick = diveTick; tick <= diveTick + BasicComboRules.DownAirStartupFrames; tick++) {
            Advance(down, tick,
                p1Buttons: tick == diveTick
                    ? GameplayButtons.BasicAttack | GameplayButtons.Down
                    : GameplayButtons.None,
                p1Held: GameplayButtons.Down);
        }
        AssertThat(down.TryGetFighter(1, out FighterStateComponent spiked)).IsTrue();
        AssertThat(spiked.Velocity.y > FP64.Zero)
            .OverrideFailureMessage("The down-air also launches upward (§2.8 — no spike in this batch).")
            .IsTrue();

        // The launch is mostly vertical: the authored 2.5 / 0.3 ratio means the
        // upward component dominates the horizontal one by a wide margin.
        AssertThat(FP64.Abs(spiked.Velocity.y) > FP64.Abs(spiked.Velocity.x) * FP64.FromInt(4))
            .OverrideFailureMessage("Directional knockback must be mostly vertical.")
            .IsTrue();
    }

    [TestCase]
    public void AVariantSwingNeverChainsAndResetsTheComboIndex() {
        // Land the first two hits of the ordinary string, then interrupt the
        // chain-hold window with Up + BasicAttack: the string is abandoned and
        // an up-attack starts from a zeroed combo index.
        var simulation = NewZeroKnockbackSimulation(seed: 507);
        Advance(simulation, 0, p1Buttons: GameplayButtons.BasicAttack);
        // Hit 1 connects (10 x 0.8 = 8 damage), so the attacker's own V7.1
        // hitstop freeze delays the phase machine by the shared window.
        int holdTick = BasicComboRules.GroundStartupFrames[0]
            + BasicComboRules.GroundActiveFrames[0]
            + BasicComboRules.GroundRecoveryFrames[0]
            + BasicComboRules.HitstopFrames(8);
        for (int tick = 1; tick <= holdTick; tick++) {
            Advance(simulation, tick);
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent holding)).IsTrue();
        AssertThat(holding.AttackPhase).IsEqual(FighterBasicAttackRules.PhaseChainHold);

        Advance(simulation, holdTick + 1, p1MoveY: UpAxis, p1Buttons: GameplayButtons.BasicAttack);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent redirected)).IsTrue();
        AssertThat(redirected.AttackFlags & FighterBasicAttackRules.FlagUpAttack)
            .OverrideFailureMessage("Up out of the chain-hold window starts an up-attack, not hit two.")
            .IsEqual(FighterBasicAttackRules.FlagUpAttack);
        AssertThat(redirected.ComboIndex)
            .OverrideFailureMessage("A directional attack resets the combo index.")
            .IsEqual(0);

        // Mashing BasicAttack through a variant swing must not buffer into the
        // string: the swing ends at PhaseNone with the index still zero.
        int total = BasicComboRules.UpAttackStartupFrames
            + BasicComboRules.UpAttackActiveFrames
            + BasicComboRules.UpAttackRecoveryFrames;
        for (int tick = holdTick + 2; tick <= holdTick + total; tick++) {
            Advance(simulation, tick, p1MoveY: UpAxis, p1Buttons: GameplayButtons.BasicAttack);
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent afterVariant)).IsTrue();
        AssertThat(afterVariant.ComboIndex)
            .OverrideFailureMessage("A variant swing never advances the chain.")
            .IsEqual(0);
        AssertThat(afterVariant.AttackPhase)
            .OverrideFailureMessage("A variant swing never enters the chain-hold window.")
            .IsNotEqual(FighterBasicAttackRules.PhaseChainHold);
    }

    [TestCase]
    public void LandingCancelsTheDownAirWithNoLag() {
        // Thrown from the apex, and §2.9's fast-fall drags the attacker down at
        // 20 units/s (M01) while the 32-frame swing runs — so the fall finishes first
        // and the landing is genuinely what ends the swing.
        var simulation = NewOverlappingSimulation(seed: 508);
        int diveTick = JumpToApex(simulation, 0);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent falling)).IsTrue();
        AssertThat(falling.IsGrounded)
            .OverrideFailureMessage("The attacker must still be airborne when the dive starts.")
            .IsEqual(0);

        Advance(simulation, diveTick,
            p1Buttons: GameplayButtons.BasicAttack | GameplayButtons.Down,
            p1Held: GameplayButtons.Down);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent diving)).IsTrue();
        AssertThat(diving.AttackFlags & FighterBasicAttackRules.FlagDownAir)
            .IsEqual(FighterBasicAttackRules.FlagDownAir);

        int landingTick = -1;
        bool swingRanUntilLanding = false;
        for (int tick = diveTick + 1; tick <= diveTick + 200 && landingTick < 0; tick++) {
            Advance(simulation, tick, p1Held: GameplayButtons.Down);
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if (attacker.IsGrounded != 0) {
                landingTick = tick;
                swingRanUntilLanding = runtime.AttackPhase != FighterBasicAttackRules.PhaseNone;
            }
        }
        AssertThat(landingTick >= 0)
            .OverrideFailureMessage("The down-air attacker never returned to the ground.")
            .IsTrue();
        AssertThat(swingRanUntilLanding)
            .OverrideFailureMessage("The down-air finished in the air — the landing cancel was not exercised.")
            .IsTrue();

        // The ground snap happens after the phase machine has already run for
        // that tick, so the cancel lands on the very next tick — which is what
        // "no landing lag" means here, and is the aerial string's behaviour too.
        // M01 (Package 12 W3): at the 20 u/s fast-fall the dive can connect on
        // the landing tick itself, and a connected hit's hitstop suspends the
        // attacker's phase machine. "No lag" is measured on the first tick the
        // attacker is actually running again.
        int nextTick = landingTick + 1;
        for (int guard = 0; guard < BasicComboRules.HitstopMaxFrames + 2; guard++) {
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent frozen)).IsTrue();
            if (frozen.HitstopFrames <= 0) break;
            Advance(simulation, nextTick++, p1Held: GameplayButtons.Down);
        }
        Advance(simulation, nextTick, p1Held: GameplayButtons.Down);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent onLanding)).IsTrue();
        AssertThat(onLanding.AttackPhase)
            .OverrideFailureMessage("Landing must cancel the down-air with no lag.")
            .IsEqual(FighterBasicAttackRules.PhaseNone);
        AssertThat(onLanding.AttackFlags & FighterBasicAttackRules.VariantMask).IsEqual(0);
    }

    [TestCase]
    public void ACpuStyleZeroVerticalStreamNeverProducesAVariant() {
        // The CPU's combat table emits MoveY = 0 and never holds Down, so it can
        // only ever produce the ordinary string (§2.8 accepted gap). This pins
        // that the variant selection cannot fire without a real direction.
        var simulation = NewZeroKnockbackSimulation(seed: 509);
        for (int tick = 0; tick < 400; tick++) {
            Advance(simulation, tick, p1Buttons: GameplayButtons.BasicAttack);
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(runtime.AttackFlags & FighterBasicAttackRules.VariantMask)
                .OverrideFailureMessage($"A MoveY = 0 stream produced a directional variant at tick {tick}.")
                .IsEqual(0);
        }
    }

    [TestCase]
    public void TwoSimulationsAgreeTickForTickWhileTheVariantsAreUsed() {
        var first = NewOverlappingSimulation(seed: 510);
        var second = NewOverlappingSimulation(seed: 510);
        int upAttacks = 0;
        int downAirs = 0;

        for (int tick = 0; tick < 360; tick++) {
            // A deterministic script that cycles jump, up-attack and down-air so
            // both variant bits, their hit application, and the launch impulse
            // all pass through the snapshot and the state hash. The down-air is
            // thrown ~22 frames after the jump (near the apex) because §2.9's
            // fast-fall makes a Down-held rise impossible.
            int phase = tick % 90;
            sbyte moveY = 0;
            GameplayButtons pressed = GameplayButtons.None;
            GameplayButtons held = GameplayButtons.None;
            if (phase == 0) {
                pressed = GameplayButtons.BasicAttack;
                moveY = UpAxis;
            } else if (phase < 12) {
                moveY = UpAxis;
            } else if (phase == 40) {
                pressed = GameplayButtons.Jump;
            } else if (phase == 62) {
                pressed = GameplayButtons.BasicAttack | GameplayButtons.Down;
                held = GameplayButtons.Down;
            } else if (phase > 62 && phase < 76) {
                held = GameplayButtons.Down;
            }

            long firstHash = Advance(first, tick, p1MoveY: moveY, p1Buttons: pressed, p1Held: held);
            long secondHash = Advance(second, tick, p1MoveY: moveY, p1Buttons: pressed, p1Held: held);
            AssertThat(firstHash)
                .OverrideFailureMessage($"Two identical simulations diverged at tick {tick}.")
                .IsEqual(secondHash);

            AssertThat(first.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            if ((runtime.AttackFlags & FighterBasicAttackRules.FlagUpAttack) != 0
                && runtime.AttackPhaseFrames == BasicComboRules.UpAttackStartupFrames
                && runtime.AttackPhase == FighterBasicAttackRules.PhaseStartup) {
                upAttacks++;
            }
            if ((runtime.AttackFlags & FighterBasicAttackRules.FlagDownAir) != 0
                && runtime.AttackPhaseFrames == BasicComboRules.DownAirStartupFrames
                && runtime.AttackPhase == FighterBasicAttackRules.PhaseStartup) {
                downAirs++;
            }
        }

        // The run must not have been vacuous.
        AssertThat(upAttacks)
            .OverrideFailureMessage("The convergence run never started an up-attack.")
            .IsGreater(0);
        AssertThat(downAirs)
            .OverrideFailureMessage("The convergence run never started a down-air.")
            .IsGreater(0);
        AssertThat(first.CurrentHash)
            .OverrideFailureMessage("Final state hashes diverged after a variant-heavy run.")
            .IsEqual(second.CurrentHash);
    }

    /// <summary>
    /// Spawns both fighters on the same point so the pushbox settles them to its
    /// 0.8-unit minimum — inside the up-attack's 1.2 and the down-air's 1.0
    /// horizontal reach, which the 2-unit chain range would otherwise mask.
    /// </summary>
    /// <summary>
    /// Jumps player one and advances, without holding Down, to the top of the
    /// arc. §2.9's fast-fall clamps a Down-held airborne fighter straight to
    /// 16 units/s downward, so a down-air cannot be thrown on the way up at
    /// all — it has to come from height. Returns the tick the caller should
    /// press on; the fighter is airborne and no longer rising.
    /// </summary>
    private static int JumpToApex(FighterSimulation simulation, int startTick) {
        Advance(simulation, startTick, p1Buttons: GameplayButtons.Jump);
        for (int tick = startTick + 1; tick < startTick + 120; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (fighter.IsGrounded == 0 && fighter.Velocity.y <= FP64.Zero) return tick;
            Advance(simulation, tick);
        }
        AssertThat(false)
            .OverrideFailureMessage("The jump never reached its apex.")
            .IsTrue();
        return startTick;
    }

    private static FighterSimulation NewOverlappingSimulation(int seed) => new(
        FighterCharacterID.Tesla,
        FighterCharacterID.Joan,
        seed: seed,
        spawnDistance: 0,
        rules: FighterMatchRules.Disabled);

    private static FighterSimulation NewZeroKnockbackSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildZeroKnockbackAttacker()),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed,
        spawnDistance: 0,
        rules: FighterMatchRules.Disabled);

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

    private static long Advance(
        FighterSimulation simulation,
        int tick,
        sbyte p1MoveY = 0,
        GameplayButtons p1Buttons = GameplayButtons.None,
        GameplayButtons p1Held = GameplayButtons.None) => simulation.Advance(
            new PlayerInputFrame {
                Tick = (uint)tick,
                MoveY = p1MoveY,
                Held = p1Buttons | p1Held,
                Pressed = p1Buttons
            },
            new PlayerInputFrame { Tick = (uint)tick });
}
