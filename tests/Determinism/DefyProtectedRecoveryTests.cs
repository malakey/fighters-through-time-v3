using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A1b — V7.6 D04 protected recovery, deterministic half.
///
/// <para>On a successful Defy the survivor still spends the full 100 meter,
/// survives at 1 HP, generates no echo and consumes its once-per-match use.
/// What is new: it is RELEASED from the triggering hit's forced hitstun,
/// stagger, knockback and capture — it may not be launched or stunned by the
/// very blow it defied — and is invulnerable through the presentation and for
/// exactly 60 active ticks once normal control resumes. Ticks 1–60 are
/// protected; tick 61 is not.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyProtectedRecoveryTests {

    [TestCase]
    public void ADefiedHitNeitherLaunchesNorStunsTheSurvivor() {
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);

        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30);

        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("Defy leaves the survivor at exactly 1 HP.")
            .IsEqual(1);
        AssertThat(f.Verb.DefyHistoryUsed).IsEqual(1);
        AssertThat(f.Target.Influence.RawValue)
            .OverrideFailureMessage("The meter shatters to zero.")
            .IsEqual(0L);
        AssertThat(f.Target.HitstunFrames)
            .OverrideFailureMessage("D04: the survivor is released from the defied hit's hitstun.")
            .IsEqual(0);
        AssertThat(f.Verb.PendingLaunchActive)
            .OverrideFailureMessage("D04: no stashed launch from the blow that was defied.")
            .IsEqual(0);
        AssertThat(f.Target.Velocity.y.RawValue)
            .OverrideFailureMessage("D04: the defied hit contributes no forced motion.")
            .IsEqual(0L);
    }

    /// <summary>
    /// CM-FIXB-SIM (B2): "the defied hit contributes no forced motion" holds for
    /// every impulse form, not only the scalar one above. An airborne survivor
    /// drifting at a known velocity keeps exactly that velocity and stays
    /// airborne through a lethal Down-Air slam (the signed spike, which the
    /// 1-HP missing-HP scale used to double), a lethal upward vector Special, a
    /// vector non-launcher, a scalar launcher (which used to zero the drift) and
    /// a hitstop-free zone pulse — as Story's defySuppressed path leaves the
    /// hero's velocity untouched. No tumble, hitstun, stashed launch or slam
    /// bounce comes of it.
    /// </summary>
    [TestCase]
    public void ADefiedHitLeavesTheSurvivorsMotionExactlyAsItWas() {
        var issues = new System.Collections.Generic.List<string>();
        foreach ((string name, int attackClass, FP64 knockback, bool vector, FP64 vertical, bool launches, bool hitstop) in new[] {
                     ("a lethal Down-Air slam", FighterDamageRules.BasicAttackClass, FP64.Zero, true, FP64.FromInt(-10), true, true),
                     ("a lethal upward vector Special", FighterDamageRules.SpecialAttackClass, FP64.FromInt(3), true, FP64.FromInt(9), true, true),
                     ("a lethal vector non-launcher", FighterDamageRules.SpecialAttackClass, FP64.FromInt(3), true, FP64.FromInt(-4), false, true),
                     ("a lethal scalar launcher", FighterDamageRules.BasicAttackClass, FP64.FromInt(8), false, FP64.Zero, true, true),
                     ("a lethal zone pulse", FighterDamageRules.SpecialAttackClass, FP64.FromInt(2), true, FP64.FromInt(5), false, false) }) {
            Fixture f = Fixture.New();
            f.Target.CurrentHP = 5;
            f.Target.Influence = FP64.FromInt(100);
            f.Target.IsGrounded = 0;
            f.Target.Position = new FPVector2(FP64.Zero, FP64.FromInt(3));
            var drift = new FPVector2(FP64.FromDouble(1.5), FP64.FromInt(-2));
            f.Target.Velocity = drift;
            FighterKnockdownComponent knockdown = default;

            f.HitWith(attackClass, damage: 40, knockback, hitstunFrames: 30, vector, vertical, launches, hitstop);
            FighterSlamRules.TryArm(in f.Target, in f.Verb, ref knockdown);

            if (f.Verb.DefyHistoryUsed != 1 || f.Target.CurrentHP != 1) {
                issues.Add($"{name}: setup, the blow must be defied");
                continue;
            }
            if (f.Target.Velocity.x.RawValue != drift.x.RawValue || f.Target.Velocity.y.RawValue != drift.y.RawValue) {
                issues.Add($"{name}: the survivor's velocity moved to ({f.Target.Velocity.x}, {f.Target.Velocity.y})");
            }
            if (f.Target.IsGrounded != 0) issues.Add($"{name}: the survivor's airborne state changed");
            if (f.Target.HitstunFrames != 0) issues.Add($"{name}: hitstun {f.Target.HitstunFrames}");
            if (f.Verb.Tumble != FighterVerbRules.TumbleNone) issues.Add($"{name}: tumble {f.Verb.Tumble}");
            if (f.Verb.PendingLaunchActive != 0) issues.Add($"{name}: a launch was stashed");
            if (FighterSlamRules.IsPending(in knockdown)) issues.Add($"{name}: a slam bounce was armed");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheDefiedHitImposesNoAttachedStatus() {
        // "The defied hit does not impose a new attached control/status effect
        // that would immediately re-lock the survivor."
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);

        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30,
              statusType: (int)StatusType.Root, statusFrames: 120);

        AssertThat(f.Runtime.StatusType).IsEqual((int)StatusType.None);
    }

    [TestCase]
    public void ASameFrameFollowUpHitIsRejectedOutright() {
        // The gate rejects subsequent damaging hits BEFORE Aegis, HP barriers
        // and block, immediately — it covers the presentation, not just the
        // post-control second.
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40, knockback: FP64.FromInt(8), hitstunFrames: 30);
        // Installed AFTER the proc: the Defy hit itself must reach HP, and the
        // point of this case is that the window rejects the FOLLOW-UP before
        // any of these layers is consulted.
        f.Defense.BarrierPoints = 10;
        f.Defense.BarrierCapacity = 10;
        f.Defense.AegisActive = 1;

        bool landed = f.Hit(damage: 10, knockback: FP64.FromInt(4), hitstunFrames: 20);

        AssertThat(landed).IsFalse();
        AssertThat(f.Target.CurrentHP).IsEqual(1);
        AssertThat(f.Defense.AegisActive)
            .OverrideFailureMessage("A rejected contact spends no shield capacity.")
            .IsEqual(1);
        AssertThat(f.Defense.BarrierPoints).IsEqual(10);
        AssertThat(f.Target.BlockCharges)
            .OverrideFailureMessage("A rejected contact spends no block charge.")
            .IsEqual(3);
        AssertThat(f.Target.HitstunFrames).IsEqual(0);
    }

    [TestCase]
    public void TheWindowIsExactlySixtyResumedTicksAndTickSixtyOneLands() {
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40);

        AssertThat(f.Defense.DefyProtectionAwaitControl)
            .OverrideFailureMessage("The proc opens awaiting control, not counting down.")
            .IsEqual(1);
        AssertThat(f.Defense.DefyProtectionFrames)
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);

        // Sixty resumed control ticks are protected.
        for (int tick = 0; tick < 60; tick++) {
            AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
                .OverrideFailureMessage($"Resumed tick {tick + 1} must still be protected.")
                .IsTrue();
            FighterDefenseRules.Tick(ref f.Defense, suspended: false, actionable: true);
        }

        AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
            .OverrideFailureMessage("Tick 61 is unprotected.")
            .IsFalse();
        AssertThat(f.Hit(damage: 10))
            .OverrideFailureMessage("A hit on tick 61 lands normally.")
            .IsTrue();
    }

    [TestCase]
    public void TheCountdownPausesWhileSuspendedAndBeforeControlResumes() {
        // "Pause its countdown during global hitstop, menus, Time Freeze and
        // suspended-combat presentations" and "the protected second begins at
        // the first resumed normal-control gameplay tick".
        var defense = new FighterDefenseComponent();
        FighterDefenseRules.BeginDefyProtection(ref defense, playerID: 1);

        for (int tick = 0; tick < 200; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: true, actionable: true);
        }
        AssertThat(defense.DefyProtectionFrames)
            .OverrideFailureMessage("A suspended tick consumes no protection and banks no catch-up.")
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);

        for (int tick = 0; tick < 200; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: false, actionable: false);
        }
        AssertThat(defense.DefyProtectionAwaitControl)
            .OverrideFailureMessage("Without resumed control the window never opens.")
            .IsEqual(1);
        AssertThat(defense.DefyProtectionFrames)
            .IsEqual(FighterDefenseRules.DefyProtectionTicks);
    }

    [TestCase]
    public void ADamagingHazardTickIsRejectedByTheWindow() {
        // The gate "covers all attackers, ongoing damaging-status ticks and
        // damaging stage hazards", not only the triggering attack.
        Fixture f = Fixture.New();
        f.Target.CurrentHP = 5;
        f.Target.Influence = FP64.FromInt(100);
        f.Hit(damage: 40);

        bool hazardLanded = FighterDamageRules.ApplyEnvironmentHit(
            ref f.Target, ref f.Runtime, ref f.Verb, ref f.Defense, in f.Tuning,
            damage: 6, knockback: FP64.FromInt(2), hitstunFrames: 10,
            hitOriginX: f.Target.Position.x - FP64.One);

        AssertThat(hazardLanded).IsFalse();
        AssertThat(f.Target.CurrentHP).IsEqual(1);

        FighterDamageRules.ApplyUnattributedDamage(
            ref f.Target, ref f.Runtime, ref f.Verb, ref f.Defense, in f.Tuning, damage: 4);
        AssertThat(f.Target.CurrentHP)
            .OverrideFailureMessage("A damaging DoT tick is discarded, not banked.")
            .IsEqual(1);
    }

    [TestCase]
    public void TheProcIdentityIsDeterministicAcrossResimulation() {
        // The pending-presentation identity must be deduplicated across a
        // rollback: the same proc replayed must produce the same value.
        var first = new FighterDefenseComponent();
        var second = new FighterDefenseComponent();
        FighterDefenseRules.BeginDefyProtection(ref first, playerID: 1);
        FighterDefenseRules.BeginDefyProtection(ref second, playerID: 1);

        AssertThat(first.DefyProcIdentity).IsEqual(second.DefyProcIdentity);
        AssertThat(first.DefyProcIdentity)
            .OverrideFailureMessage("An installed proc carries a non-zero identity.")
            .IsGreater(0);
    }

    [TestCase]
    public void AKnockoutClearsTheWindowTheBubbleAndTheBarrierButNotTheSpentFlag() {
        // "Death/stock loss, Death Rewind cleanup, scene reconstruction and F22
        // Sudden Death clear the temporary window." Defy itself is once per
        // match, not once per stock, so DefyHistoryUsed survives.
        var fighter = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 0,
            BlockCharges = 0, Weight = FP64.One
        };
        // The retired AegisHits slot now holds the D1 hit-confirm word
        // (2026-10-11): a stock loss leaves S and C to expire on the respawn
        // platform, so they are seeded here and must come through untouched.
        var runtime = new FighterRuntimeComponent { UsesStocks = 1, SpecialLockoutFrames = 12, SwingLanded = 1 };
        var verb = new FighterVerbComponent { DefyHistoryUsed = 1 };
        var defense = new FighterDefenseComponent {
            AegisActive = 1, BarrierPoints = 10, BarrierCapacity = 10, BarrierRemainingFrames = 200
        };
        FighterDefenseRules.BeginDefyProtection(ref defense, playerID: 1);
        var tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 };

        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, ref verb, ref defense, in tuning);

        AssertThat(FighterDefenseRules.IsDefyProtected(in defense)).IsFalse();
        AssertThat(defense.AegisActive).IsEqual(0);
        AssertThat(runtime.SpecialLockoutFrames)
            .OverrideFailureMessage("The stock loss must leave the new tenant of the AegisHits slot alone.")
            .IsEqual(12);
        AssertThat(runtime.SwingLanded).IsEqual(1);
        AssertThat(defense.BarrierPoints).IsEqual(0);
        AssertThat(verb.DefyHistoryUsed)
            .OverrideFailureMessage("Defy is once per MATCH: a stock loss does not hand it back.")
            .IsEqual(1);
    }

    [TestCase]
    public void TheGrantedShieldExpiresAtExactlyFourHundredAndEightyActiveTicks() {
        // D02c, sim mirror: eight seconds of LIVE gameplay, and a suspended tick
        // consumes no lifetime.
        var defense = new FighterDefenseComponent {
            BarrierPoints = 10,
            BarrierCapacity = 10,
            BarrierEffectID = 1,
            BarrierRemainingFrames = FighterDefenseRules.GrantedShieldLifetimeTicks
        };

        for (int tick = 0; tick < 100; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: true, actionable: true);
        }
        AssertThat(defense.BarrierRemainingFrames)
            .OverrideFailureMessage("A frozen world consumes no shield lifetime.")
            .IsEqual(FighterDefenseRules.GrantedShieldLifetimeTicks);

        for (int tick = 0; tick < FighterDefenseRules.GrantedShieldLifetimeTicks - 1; tick++) {
            FighterDefenseRules.Tick(ref defense, suspended: false, actionable: true);
        }
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("At tick 479 the shield is still live.")
            .IsEqual(10);

        FighterDefenseRules.Tick(ref defense, suspended: false, actionable: true);
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("Tick 480 expires it, discarding the remaining absorption.")
            .IsEqual(0);
        AssertThat(defense.BarrierEffectID).IsEqual(0);
    }

    // === CM-HIT (decision 4.3, both modes; amends D04) ===

    [TestCase]
    public void TheSurvivorsFirstSwingOrSpecialEndsTheProtectedWindow() {
        // The chokepoint: every offensive kind ends the window, presentation
        // wait included, and the next hit lands.
        foreach (FighterActionKind kind in new[] {
                     FighterActionKind.Swing, FighterActionKind.Special,
                     FighterActionKind.Ultimate, FighterActionKind.Grab }) {
            Fixture f = Fixture.New();
            f.Target.CurrentHP = 5;
            f.Target.Influence = FP64.FromInt(100);
            f.Hit(damage: 40);
            AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense)).IsTrue();
            FighterDefenseRules.Tick(ref f.Defense, suspended: false, actionable: true);
            FighterActionStateComponent action = default;
            FighterActionRules.OnActionAccepted(ref f.Target, ref f.Verb, ref f.Defense, ref action, kind);
            AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
                .OverrideFailureMessage($"{kind} must end the Defy window.")
                .IsFalse();
            AssertThat(f.Defense.DefyProcIdentity).IsEqual(0);
            AssertThat(f.Verb.DefyHistoryUsed)
                .OverrideFailureMessage("Ending the window never hands the spent use back.")
                .IsEqual(1);
            AssertThat(f.Hit(damage: 10)).OverrideFailureMessage($"After {kind} a hit lands.").IsTrue();
        }

        // In a match: the survivor's own swing ends it on its start tick, and a
        // Special does the same.
        foreach (GameplayButtons offence in new[] { GameplayButtons.BasicAttack, GameplayButtons.Special1 }) {
            FighterSimulation simulation = DefiedSurvivorSimulation(out int tick);
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, offence));
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            bool accepted = offence == GameplayButtons.BasicAttack
                ? FighterBasicAttackRules.IsSwinging(in runtime)
                : runtime.SpecialOneCooldownFrames > 0;
            AssertThat(accepted).OverrideFailureMessage($"The survivor's {offence} must be accepted.").IsTrue();
            AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent defense)).IsTrue();
            AssertThat(FighterDefenseRules.IsDefyProtected(in defense))
                .OverrideFailureMessage($"The survivor's {offence} ends the window on its acceptance tick.")
                .IsFalse();
        }
    }

    [TestCase]
    public void ARollAJumpOrAnyOtherMovementVerbKeepsTheProtectedWindow() {
        foreach (FighterActionKind kind in new[] {
                     FighterActionKind.Jump, FighterActionKind.AirJump, FighterActionKind.Roll,
                     FighterActionKind.AirDodge, FighterActionKind.MovementAbility,
                     FighterActionKind.LedgeGrab, FighterActionKind.LedgeGetUp }) {
            Fixture f = Fixture.New();
            f.Target.CurrentHP = 5;
            f.Target.Influence = FP64.FromInt(100);
            f.Hit(damage: 40);
            FighterDefenseRules.Tick(ref f.Defense, suspended: false, actionable: true);
            FighterActionStateComponent action = default;
            FighterActionRules.OnActionAccepted(ref f.Target, ref f.Verb, ref f.Defense, ref action, kind);
            AssertThat(FighterDefenseRules.IsDefyProtected(in f.Defense))
                .OverrideFailureMessage($"{kind} is not offence and keeps the window.")
                .IsTrue();
            AssertThat(f.Defense.DefyProtectionFrames).IsEqual(FighterDefenseRules.DefyProtectionTicks - 1);
        }

        // In a match: a roll keeps it, ticking down untouched.
        FighterSimulation simulation = DefiedSurvivorSimulation(out int tick);
        AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent before)).IsTrue();
        simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Roll));
        AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(runtime.UniversalMovementState).IsEqual((int)UniversalMovementPhase.RollStartup);
        AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent after)).IsTrue();
        AssertThat(FighterDefenseRules.IsDefyProtected(in after)).IsTrue();
        AssertThat(after.DefyProtectionFrames).IsEqual(before.DefyProtectionFrames - 1);
    }

    /// <summary>
    /// A real match in which player two (one HP, a full meter) Defies player
    /// one's first landed basic and is back in normal control with the 60-tick
    /// window counting. Returns the next tick label.
    /// </summary>
    private static FighterSimulation DefiedSurvivorSimulation(out int nextTick) {
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan),
            new FighterLoadout(
                (int)FighterCharacterID.Joan,
                1, 3, 2, 10, 14, 12, 20, 600, 600,
                (int)StatusType.None, 0, (int)StatusType.None, 0, (int)StatusType.None, 0,
                FP64.One, FP64.FromInt(8), FP64.FromInt(13),
                FP64.FromInt(3), FP64.FromInt(4), FP64.FromInt(4), FP64.FromInt(5),
                FP64.One, FP64.One, FP64.One,
                FighterAbilityLoadout.Default),
            seed: 71, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 100)).IsTrue();
        int tick = 0;
        bool defied = false;
        for (; tick < 120 && !defied; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.BasicAttack), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            defied = verb.DefyHistoryUsed == 1;
        }
        AssertThat(defied).OverrideFailureMessage("Player one's string must trigger player two's Defy.").IsTrue();
        // Wait out the proc's freeze and the string's hitstun until the window counts.
        for (int wait = 0; wait < 120; wait++, tick++) {
            simulation.TryGetFighterDefense(1, out FighterDefenseComponent defense);
            simulation.TryGetFighter(1, out FighterStateComponent survivor);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent survivorVerb);
            if (defense.DefyProtectionAwaitControl == 0 && defense.DefyProtectionFrames > 30
                && survivor.HitstunFrames <= 0 && survivorVerb.HitstopFrames <= 0 && survivor.IsGrounded != 0) break;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent counting)).IsTrue();
        AssertThat(counting.DefyProtectionAwaitControl).IsEqual(0);
        AssertThat(FighterDefenseRules.IsDefyProtected(in counting))
            .OverrideFailureMessage("The survivor must be protected and in control.")
            .IsTrue();
        nextTick = tick;
        return simulation;
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons buttons) => new() {
        Tick = (uint)tick,
        Held = buttons,
        Pressed = buttons
    };

    private struct Fixture {
        public FighterStateComponent Attacker;
        public FighterRuntimeComponent AttackerRuntime;
        public FighterVerbComponent AttackerVerb;
        public FighterStateComponent Target;
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterDefenseComponent Defense;
        public FighterTuningComponent Tuning;

        public static Fixture New() => new() {
            Attacker = NewFighter(0),
            Target = NewFighter(1),
            Tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 }
        };

        public bool Hit(
            int damage,
            FP64 knockback = default,
            int hitstunFrames = 0,
            int statusType = (int)StatusType.None,
            int statusFrames = 0) {
            Attacker.Position = new FPVector2(Target.Position.x - FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, damage, knockback, hitstunFrames,
                statusType, statusFrames, FP64.One, Attacker.Position.x);
        }

        /// <summary>One hit with an explicit class, launch flag, hitstop and (optionally) a D10 signed vector.</summary>
        public bool HitWith(
            int attackClass, int damage, FP64 knockback, int hitstunFrames,
            bool hasKnockbackVector, FP64 knockbackVertical, bool launches, bool appliesHitstop) {
            Attacker.Position = new FPVector2(Target.Position.x - FP64.One, Target.Position.y);
            return FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                attackClass, damage, knockback, hitstunFrames,
                (int)StatusType.None, 0, FP64.One, Attacker.Position.x,
                appliesHitstop: appliesHitstop,
                launches: launches,
                hasKnockbackVector: hasKnockbackVector,
                knockbackVertical: knockbackVertical);
        }
    }

    private static FighterStateComponent NewFighter(int playerID) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1,
        Weight = FP64.One
    };
}
