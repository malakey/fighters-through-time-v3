using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-HIT (combat and movement update 2026-10-16, decision 3.2, finding
/// PFC-04; ledger <c>USER-TUMBLE-AFTER-HITSTUN</c>), the Fighter half: tumble
/// outlives hitstun (the Smash rule). A launch whose hitstun runs out in the air
/// leaves the fighter tumbling (component 310 <c>Tumble</c> value 2) with full
/// control; any action — a jump, the air dodge, a swing, a Special — leaves the
/// tumble and lands plainly, while a fighter that only drifts or fast-falls
/// lands in it: the owed slam bounce, held Block's tech, otherwise the M05
/// knockdown. Pure C#: hand-built loadouts, no engine.
/// </summary>
[TestSuite]
public class FighterTumbleTests {

    /// <summary>The launcher's hitstun: far shorter than its arc, so it always ends in the air.</summary>
    private const int ShortHitstun = 10;

    [TestCase]
    public void APassiveTumbleWhoseHitstunEndsInTheAirKnocksDownAndBlockTechsIt() {
        // Passive: the landing is a missed tech, exactly as inside hitstun.
        FighterSimulation passive = LaunchedSimulation(seed: 3201);
        int tick = RideToHitstunEnd(passive);
        int landing = RideToLanding(passive, tick, GameplayButtons.None, moveX: 0, out bool _);
        AssertThat(passive.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown)).IsTrue();
        AssertThat(FighterKnockdownRules.IsDown(in knockdown))
            .OverrideFailureMessage("A passive fighter lands in its tumble: the knockdown, after hitstun ended.")
            .IsTrue();
        AssertThat(passive.TryGetFighterVerb(1, out FighterVerbComponent downVerb)).IsTrue();
        AssertThat(downVerb.Tumble).IsEqual(FighterVerbRules.TumbleNone);
        AssertThat(landing > 0).IsTrue();

        // Block held through the fall: the landing techs.
        FighterSimulation teching = LaunchedSimulation(seed: 3201);
        tick = RideToHitstunEnd(teching);
        RideToLanding(teching, tick, GameplayButtons.Block, moveX: 0, out bool _);
        AssertThat(teching.TryGetFighterVerb(1, out FighterVerbComponent techVerb)).IsTrue();
        AssertThat(techVerb.TechLockoutFrames > 0)
            .OverrideFailureMessage("Held Block techs a post-hitstun tumble landing.")
            .IsTrue();
        AssertThat(techVerb.Tumble).IsEqual(FighterVerbRules.TumbleNone);
        AssertThat(teching.TryGetFighterKnockdown(1, out FighterKnockdownComponent noKnockdown)).IsTrue();
        AssertThat(FighterKnockdownRules.IsActive(in noKnockdown)).IsFalse();
    }

    [TestCase]
    public void TumbleReadsOneInHitstunAndTwoBetweenTheHitstunEndAndTheLanding() {
        FighterSimulation simulation = LaunchedSimulation(seed: 3202);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent launched)).IsTrue();
        AssertThat(launched.Tumble).IsEqual(FighterVerbRules.TumbleInHitstun);

        int tick = 1;
        bool sawPostHitstun = false;
        for (; tick < 200; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(1, out FighterStateComponent victim);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            if (FighterKnockdownRules.IsDown(in knockdown)) break;
            if (victim.HitstunFrames > 0) {
                AssertThat(verb.Tumble)
                    .OverrideFailureMessage($"Tick {tick}: a launch in hitstun is value 1.")
                    .IsEqual(FighterVerbRules.TumbleInHitstun);
            } else if (victim.IsGrounded == 0 && verb.HitstopFrames == 0 && sawPostHitstun) {
                // The tick hitstun reaches zero inside TickCounters still reads 1;
                // from the next tick on, airborne, it is 2.
                AssertThat(verb.Tumble)
                    .OverrideFailureMessage($"Tick {tick}: a tumble past its hitstun, airborne, is value 2.")
                    .IsEqual(FighterVerbRules.TumbleAfterHitstun);
            }
            if (victim.HitstunFrames <= 0 && victim.IsGrounded == 0) sawPostHitstun = true;
        }
        AssertThat(sawPostHitstun)
            .OverrideFailureMessage("setup: the launch's hitstun must end before the landing.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent landed)).IsTrue();
        AssertThat(FighterKnockdownRules.IsDown(in landed)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent final)).IsTrue();
        AssertThat(final.Tumble).IsEqual(FighterVerbRules.TumbleNone);
    }

    [TestCase]
    public void EachExitActionLeavesTheTumbleAndLandsPlainly() {
        var issues = new List<string>();
        foreach ((string name, GameplayButtons press) in new[] {
                     ("the air jump", GameplayButtons.Jump),
                     ("the air dodge", GameplayButtons.Roll),
                     ("the air strike", GameplayButtons.BasicAttack),
                     ("Special 1", GameplayButtons.Special1) }) {
            FighterSimulation simulation = LaunchedSimulation(seed: 3203);
            int tick = RideToHitstunEnd(simulation);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent before);
            if (before.Tumble != FighterVerbRules.TumbleAfterHitstun) {
                issues.Add($"{name}: setup, the tumble must read 2 before the press (read {before.Tumble})");
                continue;
            }
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, press));
            tick++;
            simulation.TryGetFighterVerb(1, out FighterVerbComponent after);
            if (after.Tumble != FighterVerbRules.TumbleNone) {
                issues.Add($"{name} must leave the tumble on its acceptance tick (read {after.Tumble})");
            }
            RideToLanding(simulation, tick, GameplayButtons.None, moveX: 0, out bool _);
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent landed);
            if (FighterKnockdownRules.IsActive(in knockdown)) issues.Add($"{name}: the landing knocked down");
            if (landed.TechLockoutFrames > 0) issues.Add($"{name}: the landing teched");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void DriftAndFastFallKeepTheTumble() {
        FighterSimulation simulation = LaunchedSimulation(seed: 3204);
        int tick = RideToHitstunEnd(simulation);
        // Holding a direction and Down every tick: air control and the fast-fall
        // are not actions, so the fighter still lands in its tumble.
        RideToLanding(simulation, tick, GameplayButtons.Down, moveX: 127, out bool tumbledThroughout);
        AssertThat(tumbledThroughout)
            .OverrideFailureMessage("Drift and fast-fall never leave the tumble.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown)).IsTrue();
        AssertThat(FighterKnockdownRules.IsDown(in knockdown)).IsTrue();
    }

    [TestCase]
    public void AnOwedSlamBounceSurvivesTheHitstunEndAndIsStillForced() {
        // A12 meets 3.2: a Down-Air slam's bounce is owed for as long as its
        // victim tumbles, not only while its hitstun runs. The rules the
        // movement system calls each tick, in its order.
        var fighter = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 80,
            IsGrounded = 0, HitstunFrames = 0, Velocity = new FPVector2(FP64.One, FP64.FromInt(-8))
        };
        var verb = new FighterVerbComponent { Tumble = FighterVerbRules.TumbleInHitstun };
        var knockdown = new FighterKnockdownComponent { SlamBouncePending = 1, SlamBounceSpeed = FP64.FromInt(6) };

        FighterVerbRules.UpdateTumble(in fighter, ref verb);
        AssertThat(verb.Tumble).IsEqual(FighterVerbRules.TumbleAfterHitstun);
        FighterSlamRules.ClearIfStale(in fighter, in verb, ref knockdown);
        AssertThat(FighterSlamRules.IsPending(in knockdown))
            .OverrideFailureMessage("The bounce survives the end of the slam's hitstun while the fighter tumbles.")
            .IsTrue();

        // Ground contact: the bounce fires, forced — neither tech nor knockdown —
        // and is a fresh launch (value 1, the directional hitstun).
        fighter.IsGrounded = 1;
        FighterRuntimeComponent runtime = default;
        AssertThat(FighterSlamRules.TryBounce(ref fighter, in runtime, ref verb, ref knockdown)).IsTrue();
        AssertThat(fighter.IsGrounded).IsEqual(0);
        AssertThat(fighter.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(fighter.HitstunFrames).IsEqual(BasicComboRules.DirectionalAttackHitstunFrames);
        AssertThat(verb.Tumble).IsEqual(FighterVerbRules.TumbleInHitstun);
        AssertThat(FighterSlamRules.IsPending(in knockdown)).IsFalse();

        // An action out of the tumble drops an owed bounce on the next tick.
        var acted = new FighterVerbComponent { Tumble = FighterVerbRules.TumbleAfterHitstun };
        var owed = new FighterKnockdownComponent { SlamBouncePending = 1, SlamBounceSpeed = FP64.FromInt(6) };
        var free = new FighterStateComponent { PlayerID = 1, Stocks = 3, IsGrounded = 0 };
        FighterVerbRules.ExitTumbleOnAction(in free, ref acted);
        FighterSlamRules.ClearIfStale(in free, in acted, ref owed);
        AssertThat(FighterSlamRules.IsPending(in owed)).IsFalse();
    }

    /// <summary>
    /// CM-FIXB-SIM (B1; M05 meets 3.2): a stunning hit that is not itself a
    /// launch ends the tumble whatever frame it lands on — inside the launch's
    /// hitstun (value 1) as well as after it (value 2) — and the landing is
    /// plain, neither tech nor knockdown, as Story's <c>OnHurtboxHit</c> rewrites
    /// <c>_stunTumble</c> on every stunning hit (StoryTumbleTests'
    /// <c>ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstun</c>). The
    /// non-launcher is the air strike (impulse, no launch) or a zero-knockback
    /// construct bite (hitstun only, no hitstop); an owed slam bounce goes with
    /// the tumble. The hits run through the production pipeline and the landing
    /// through the movement system's own rules, in its order.
    /// </summary>
    [TestCase]
    public void ANonLaunchingHitEndsTheTumbleInsideOrAfterTheLaunchsHitstunAndTheLandingIsPlain() {
        var issues = new List<string>();
        foreach (bool afterHitstun in new[] { false, true }) {
            foreach ((string name, FP64 knockback, int hitstun, bool hitstop) in new[] {
                         ("the air strike", FP64.FromInt(4), BasicComboRules.AirAttackHitstunFrames, true),
                         ("a construct bite", FP64.Zero, 10, false) }) {
                string label = $"{name} {(afterHitstun ? "after" : "inside")} the launch's hitstun";
                var juggle = JuggleFixture.Launched();
                if (juggle.Verb.Tumble != FighterVerbRules.TumbleInHitstun) {
                    issues.Add($"{label}: setup, the launch must tumble (read {juggle.Verb.Tumble})");
                    continue;
                }
                if (afterHitstun) {
                    juggle.Target.HitstunFrames = 0;
                    FighterVerbRules.UpdateTumble(in juggle.Target, ref juggle.Verb);
                    if (juggle.Verb.Tumble != FighterVerbRules.TumbleAfterHitstun) {
                        issues.Add($"{label}: setup, the tumble must read 2 past its hitstun (read {juggle.Verb.Tumble})");
                        continue;
                    }
                }
                // An owed Down-Air bounce rides the tumble (A12).
                juggle.Knockdown.SlamBouncePending = 1;
                juggle.Knockdown.SlamBounceSpeed = FP64.FromInt(6);
                juggle.Hit(knockback, hitstun, launches: false, appliesHitstop: hitstop);
                if (juggle.Target.HitstunFrames != hitstun) issues.Add($"{label}: setup, the hit must stun");
                if (juggle.Verb.Tumble != FighterVerbRules.TumbleNone) {
                    issues.Add($"{label} must end the tumble where it lands (read {juggle.Verb.Tumble})");
                }
                // The next movement tick: the tumble stays over and the bounce goes.
                FighterVerbRules.UpdateTumble(in juggle.Target, ref juggle.Verb);
                FighterSlamRules.ClearIfStale(in juggle.Target, in juggle.Verb, ref juggle.Knockdown);
                if (FighterSlamRules.IsPending(in juggle.Knockdown)) issues.Add($"{label}: the owed slam bounce survived");
                // The landing, passive: no bounce, no tech, no knockdown.
                juggle.Target.IsGrounded = 1;
                if (FighterSlamRules.TryBounce(ref juggle.Target, in juggle.Runtime, ref juggle.Verb, ref juggle.Knockdown)) {
                    issues.Add($"{label}: the landing bounced");
                }
                if (FighterVerbRules.TryLandingTech(ref juggle.Target, in juggle.Runtime, ref juggle.Verb)) {
                    issues.Add($"{label}: the landing teched");
                }
                if (FighterKnockdownRules.TryBeginKnockdown(ref juggle.Target, ref juggle.Verb, ref juggle.Knockdown)) {
                    issues.Add($"{label}: the landing knocked down");
                }
            }
        }
        // A launch on a tumbling fighter is a fresh tumble (value 1) — the
        // juggle's own launcher keeps the rule it always had.
        var relaunch = JuggleFixture.Launched();
        relaunch.Target.HitstunFrames = 0;
        FighterVerbRules.UpdateTumble(in relaunch.Target, ref relaunch.Verb);
        relaunch.Hit(FP64.FromInt(8), 30, launches: true, appliesHitstop: true);
        if (relaunch.Verb.Tumble != FighterVerbRules.TumbleInHitstun) {
            issues.Add($"a second launch must re-tumble (read {relaunch.Verb.Tumble})");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// Component copies around one airborne victim, hit by player one from its
    /// left through <see cref="FighterDamageRules.ApplyFighterHit"/>.
    /// </summary>
    private struct JuggleFixture {
        public FighterStateComponent Attacker;
        public FighterRuntimeComponent AttackerRuntime;
        public FighterVerbComponent AttackerVerb;
        public FighterStateComponent Target;
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterDefenseComponent Defense;
        public FighterKnockdownComponent Knockdown;
        public FighterTuningComponent Tuning;

        /// <summary>The victim, already airborne, takes a 30-frame launcher: tumble value 1.</summary>
        public static JuggleFixture Launched() {
            var fixture = new JuggleFixture {
                Attacker = new FighterStateComponent {
                    PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100, BlockCharges = 3,
                    IsGrounded = 1, FacingRight = 1, Weight = FP64.One
                },
                Target = new FighterStateComponent {
                    PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100, BlockCharges = 3,
                    IsGrounded = 0, FacingRight = 0, Weight = FP64.One,
                    Position = new FPVector2(FP64.One, FP64.FromInt(2))
                },
                Tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 }
            };
            fixture.Hit(FP64.FromInt(8), 30, launches: true, appliesHitstop: true);
            // Its hitstop resolved the launch's DI; the juggle lands later.
            fixture.Verb.HitstopFrames = 0;
            fixture.AttackerVerb.HitstopFrames = 0;
            FighterVerbRules.ResolvePendingLaunch(ref fixture.Target, in fixture.Runtime, ref fixture.Verb);
            return fixture;
        }

        public void Hit(FP64 knockback, int hitstunFrames, bool launches, bool appliesHitstop) {
            FighterDamageRules.ApplyFighterHit(
                ref Attacker, ref AttackerRuntime, ref AttackerVerb,
                ref Target, ref Runtime, ref Verb, ref Defense, in Tuning,
                FighterDamageRules.BasicAttackClass, 5, knockback, hitstunFrames,
                (int)StatusType.None, 0, FP64.One, Target.Position.x - FP64.One,
                appliesHitstop: appliesHitstop, launches: launches);
        }
    }

    [TestCase]
    public void ADepthSevenRollbackReplaysThePostHitstunTumbleAndItsExit() {
        // Player two's air jump out of the tumble arrives late: the predicted
        // simulation runs seven ticks on a repeated idle input (still tumbling),
        // then the correction resimulates the jump. Both must converge, and
        // neither may knock down.
        FighterSimulation probe = LaunchedSimulation(seed: 3205);
        int hitstunEnd = RideToHitstunEnd(probe);
        int jumpTick = hitstunEnd + 2;
        var script = new List<(PlayerInputFrame One, PlayerInputFrame Two)> {
            (Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None))
        };
        for (int tick = 1; tick < jumpTick + 60; tick++) {
            script.Add((Frame(tick, GameplayButtons.None),
                Frame(tick, tick == jumpTick ? GameplayButtons.Jump : GameplayButtons.None)));
        }

        var authoritative = NewLaunchSimulation(seed: 3205);
        var predicted = NewLaunchSimulation(seed: 3205);
        int predictFrom = jumpTick - 3;
        int predictTo = predictFrom + 7;
        for (int tick = 0; tick < script.Count; tick++) {
            authoritative.Advance(script[tick].One, script[tick].Two);
            if (tick >= predictFrom && tick < predictTo) predicted.AdvanceWithPredictedPlayerTwo(script[tick].One);
            else predicted.Advance(script[tick].One, script[tick].Two);
            if (tick == predictTo - 1) {
                predicted.TryGetFighterVerb(1, out FighterVerbComponent mispredicted);
                AssertThat(mispredicted.Tumble)
                    .OverrideFailureMessage("The misprediction keeps the victim tumbling.")
                    .IsEqual(FighterVerbRules.TumbleAfterHitstun);
            }
            if (tick != predictTo) continue;
            for (int corrected = predictFrom; corrected < predictTo; corrected++) {
                AssertThat(predicted.CorrectPlayerTwoInput(corrected, script[corrected].Two)).IsTrue();
            }
            AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
            predicted.TryGetFighterVerb(1, out FighterVerbComponent corrected7);
            AssertThat(corrected7.Tumble)
                .OverrideFailureMessage("The resimulated jump leaves the tumble.")
                .IsEqual(FighterVerbRules.TumbleNone);
        }
        AssertThat(predicted.CurrentHash).IsEqual(authoritative.CurrentHash);
        AssertThat(predicted.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown)).IsTrue();
        AssertThat(FighterKnockdownRules.IsActive(in knockdown)).IsFalse();
    }

    // === Harness ===

    /// <summary>Player one's launching Special connects on tick 0 (the fallback press-tick timing).</summary>
    private static FighterSimulation LaunchedSimulation(int seed) {
        FighterSimulation simulation = NewLaunchSimulation(seed);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent launched)).IsTrue();
        AssertThat(launched.Tumble)
            .OverrideFailureMessage("setup: the authored launcher must put the victim into tumble.")
            .IsEqual(FighterVerbRules.TumbleInHitstun);
        return simulation;
    }

    private static FighterSimulation NewLaunchSimulation(int seed) => new(
        LauncherLoadout(),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Idles both fighters until the victim's hitstun and hitstop are over and
    /// it is still airborne, tumbling (value 2). Returns the next tick label.
    /// </summary>
    private static int RideToHitstunEnd(FighterSimulation simulation) {
        for (int tick = 1; tick < 120; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighter(1, out FighterStateComponent victim);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            AssertThat(victim.IsGrounded)
                .OverrideFailureMessage("setup: the launch must outlast its hitstun.")
                .IsEqual(0);
            if (verb.Tumble == FighterVerbRules.TumbleAfterHitstun) return tick + 1;
        }
        AssertThat(false).OverrideFailureMessage("setup: the tumble never outlived its hitstun.").IsTrue();
        return -1;
    }

    /// <summary>
    /// Advances with the victim holding <paramref name="held"/> and the stick
    /// at <paramref name="moveX"/> until it touches down; returns that tick.
    /// <paramref name="tumbledThroughout"/> reports whether every airborne tick
    /// read a tumble.
    /// </summary>
    private static int RideToLanding(
        FighterSimulation simulation, int startTick, GameplayButtons held, sbyte moveX, out bool tumbledThroughout) {
        tumbledThroughout = true;
        for (int tick = startTick; tick < startTick + 200; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, held, moveX));
            simulation.TryGetFighter(1, out FighterStateComponent victim);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent verb);
            if (victim.IsGrounded != 0) return tick;
            if (!FighterVerbRules.IsTumbling(in verb)) tumbledThroughout = false;
        }
        AssertThat(false).OverrideFailureMessage("The victim never landed.").IsTrue();
        return -1;
    }

    /// <summary>
    /// A hand-built kit whose Special 1 is a melee-execution launcher with a
    /// short hitstun and a strong knockback (16 → about 9 units/s each axis on
    /// the Joan victim), so the arc always outlasts the hitstun.
    /// </summary>
    private static FighterLoadout LauncherLoadout() => new(
        (int)FighterCharacterID.Einstein,
        100, 3, 2,
        10, 14, 12, 20,
        600, 600,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(16),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = ShortHitstun,
                BlockClass = (int)BlockClass.Special,
                Launches = true,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        });

    private static PlayerInputFrame Frame(int tick, GameplayButtons buttons, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
