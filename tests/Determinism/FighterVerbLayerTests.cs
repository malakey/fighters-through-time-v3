using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.1 verb layer in the deterministic simulation: universal hitstop
/// (both parties freeze, scaled by damage), directional influence (the held
/// direction at hitstop end bends a launch by up to ±15° without changing its
/// magnitude), Rally / Desperation Resonance (a fraction of every hit becomes
/// a briefly reclaimable echo with no meter double-earning), and Defy History
/// (a full meter refuses one lethal hit). All numbers come from
/// <see cref="BasicComboRules"/> and <c>FighterDamageRules</c> — the same
/// tables Story reads.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterVerbLayerTests {

    [TestCase]
    public void HitstopFreezesBothPartiesForTheSharedWindow() {
        // Tesla-profile opener: 10 x 0.8 = 8 damage -> HitstopFrames(8) frames.
        var simulation = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 0f), BuildVictim(maxHP: 400), seed: 901);
        int connectTick = LandOpener(simulation, out _);

        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent attackerVerb)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent victimVerb)).IsTrue();
        int expected = BasicComboRules.HitstopFrames(8);
        AssertThat(attackerVerb.HitstopFrames)
            .OverrideFailureMessage("The attacker must freeze for the shared hitstop window.")
            .IsEqual(expected);
        AssertThat(victimVerb.HitstopFrames)
            .OverrideFailureMessage("The victim must freeze for the same shared window.")
            .IsEqual(expected);

        // Hitstun does not tick while frozen: the victim's counter holds until
        // the freeze runs off, then decrements normally.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent frozen)).IsTrue();
        int stunAtConnect = frozen.HitstunFrames;
        simulation.Advance(Frame(connectTick + 1, 0, GameplayButtons.None), Frame(connectTick + 1, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent stillFrozen)).IsTrue();
        AssertThat(stillFrozen.HitstunFrames)
            .OverrideFailureMessage("Hitstun must not tick during hitstop.")
            .IsEqual(stunAtConnect);

        for (int tick = connectTick + 2; tick <= connectTick + expected + 1; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent thawed)).IsTrue();
        AssertThat(thawed.HitstopFrames).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent ticking)).IsTrue();
        AssertThat(ticking.HitstunFrames < stunAtConnect)
            .OverrideFailureMessage("Hitstun must resume once the freeze ends.")
            .IsTrue();
    }

    [TestCase]
    public void DirectionalInfluenceBendsTheLaunchWithoutChangingItsMagnitude() {
        // The same launching hit in two worlds; only the victim's held
        // direction during hitstop differs. DI must change the angle, not the
        // speed.
        // M05 (Package 12 W3b): only an authored launcher launches, so the
        // scenario uses a Special authored Launches = true (the string opener
        // is grounded knockback now).
        var neutral = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f, launcherSpecial: true), BuildVictim(maxHP: 400), seed: 902);
        var influenced = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f, launcherSpecial: true), BuildVictim(maxHP: 400), seed: 902);

        int connectTick = LandLauncher(neutral);
        LandLauncher(influenced);

        // Run both freezes off; the influenced victim holds Up throughout.
        for (int tick = connectTick + 1; tick <= connectTick + 8; tick++) {
            neutral.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            influenced.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None, moveY: -127));
            if (neutral.TryGetFighterVerb(1, out FighterVerbComponent verb) && verb.HitstopFrames == 0) {
                AssertThat(neutral.TryGetFighter(1, out FighterStateComponent plain)).IsTrue();
                AssertThat(influenced.TryGetFighter(1, out FighterStateComponent bent)).IsTrue();

                float plainX = plain.Velocity.x.ToFloat();
                float plainY = plain.Velocity.y.ToFloat();
                float bentX = bent.Velocity.x.ToFloat();
                float bentY = bent.Velocity.y.ToFloat();
                AssertThat(bentY > plainY)
                    .OverrideFailureMessage("Holding Up must bend the launch upward.")
                    .IsTrue();
                float plainSquared = plainX * plainX + plainY * plainY;
                float bentSquared = bentX * bentX + bentY * bentY;
                AssertThat(System.MathF.Abs(plainSquared - bentSquared) < plainSquared * 0.01f)
                    .OverrideFailureMessage("DI must never change the launch magnitude.")
                    .IsTrue();
                return;
            }
        }
        AssertThat(false).OverrideFailureMessage("The hitstop window never ended.").IsTrue();
    }

    [TestCase]
    public void LandingTechEndsTumbleWithAnInvulnerableRecovery() {
        // A launched victim who holds Block on ground contact techs: hitstun
        // ends, a 12-frame invulnerable recovery runs, and the next hit inside
        // that window is refused. A control victim without Block rides it out.
        // M05: an authored launcher (see the DI case above).
        var teched = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f, launcherSpecial: true), BuildVictim(maxHP: 400), seed: 903);
        var control = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f, launcherSpecial: true), BuildVictim(maxHP: 400), seed: 903);

        int connectTick = LandLauncher(teched);
        LandLauncher(control);

        bool techFired = false;
        int tick = connectTick + 1;
        for (; tick <= connectTick + 90; tick++) {
            teched.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            control.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(teched.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(teched.TryGetFighterVerb(1, out FighterVerbComponent techVerb)).IsTrue();
            // M05: the tech is the in-place lockout (a missed tech's knockdown
            // is also grounded, hitstun-free and invulnerable).
            if (victim.IsGrounded != 0 && victim.HitstunFrames == 0
                && techVerb.TechLockoutFrames > 0) {
                techFired = true;
                // Fix round A (CM-FIXA-SIM): the grant covers the landing tick's
                // own combat pass plus the 12 locked ticks after it, the
                // knockdown's +1 pattern — it read 12 and then sat frozen
                // through the lock, so it outlived it by 11 ticks.
                AssertThat(victim.InvulnerabilityFrames)
                    .OverrideFailureMessage("The tech grants its landing tick plus the authored recovery window.")
                    .IsEqual(BasicComboRules.LandingTechRecoveryFrames + 1);
                break;
            }
        }
        AssertThat(techFired)
            .OverrideFailureMessage("Holding Block on ground contact must tech the landing.")
            .IsTrue();
        // The invulnerability counts down WITH the lock and ends with it: every
        // locked tick is invulnerable, and the first free tick is not.
        for (int frame = 1; frame <= BasicComboRules.LandingTechRecoveryFrames; frame++) {
            tick++;
            teched.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            AssertThat(teched.TryGetFighter(1, out FighterStateComponent locked)).IsTrue();
            AssertThat(teched.TryGetFighterVerb(1, out FighterVerbComponent lockedVerb)).IsTrue();
            AssertThat(lockedVerb.TechLockoutFrames).IsEqual(BasicComboRules.LandingTechRecoveryFrames - frame);
            AssertThat(locked.InvulnerabilityFrames)
                .OverrideFailureMessage($"Tech tick L+{frame}: the invulnerability must count down with the lock.")
                .IsEqual(BasicComboRules.LandingTechRecoveryFrames + 1 - frame);
        }
        tick++;
        teched.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
        AssertThat(teched.TryGetFighter(1, out FighterStateComponent free)).IsTrue();
        AssertThat(free.InvulnerabilityFrames)
            .OverrideFailureMessage("The first free tick after the tech is not invulnerable.")
            .IsEqual(0);
    }

    /// <summary>
    /// Fix round A (CM-FIXA-SIM, ruling (b)): the tech's invulnerability lasts
    /// exactly as long as its action lock — design :1286, "locked in place, no
    /// actions", and Story's one 12-frame window. A hit probed on the last
    /// locked tick is refused; the same hit on the first free tick, the one
    /// whose presses are live again, lands. Before the fix the grant sat frozen
    /// through the lock and refused every hit for 11 more ticks, a free
    /// invulnerable reversal.
    /// </summary>
    [TestCase]
    public void AHitOnTheFirstTickAfterTheTechLockLandsAndOneInsideItIsRefused() {
        FighterSimulation simulation = NewTechSimulation(emptyShield: false);
        int tick = LaunchAndTech(simulation, 0, out _);
        AssertThat(ProbeHitLands(simulation))
            .OverrideFailureMessage("A hit on the tech's own landing tick must be refused.")
            .IsFalse();
        for (int frame = 1; frame <= BasicComboRules.LandingTechRecoveryFrames; frame++, tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(ProbeHitLands(simulation))
                .OverrideFailureMessage($"A hit on locked tech tick L+{frame} must be refused.")
                .IsFalse();
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent done)).IsTrue();
        AssertThat(done.TechLockoutFrames).IsEqual(0);
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        AssertThat(ProbeHitLands(simulation))
            .OverrideFailureMessage("A hit on the first tick after the tech lock must land.")
            .IsTrue();
    }

    /// <summary>
    /// Probes the hit pipeline against COPIES of the live components after the
    /// tick (the simulation is untouched): true when a plain basic from player
    /// one would land on player two — exactly what that tick's combat pass saw,
    /// since nothing after it changes the invulnerability.
    /// </summary>
    private static bool ProbeHitLands(FighterSimulation simulation) {
        simulation.TryGetFighter(0, out FighterStateComponent attacker);
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime);
        simulation.TryGetFighterVerb(0, out FighterVerbComponent attackerVerb);
        simulation.TryGetFighter(1, out FighterStateComponent target);
        simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent targetRuntime);
        simulation.TryGetFighterVerb(1, out FighterVerbComponent targetVerb);
        simulation.TryGetFighterDefense(1, out FighterDefenseComponent targetDefense);
        // The probe hit is never blocked: the question is the invulnerability.
        targetRuntime.HeldButtons = 0;
        FighterTuningComponent targetTuning = default;
        return FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
            FighterDamageRules.BasicAttackClass, 5, FP64.One, 12,
            (int)StatusType.None, 0, FP64.One, attacker.Position.x);
    }

    [TestCase]
    public void LandingTechFiresAtZeroChargesAndDuringTheShatterLockout() {
        // V7.3 ruling: the tech reads the raw Block INPUT, not the stance — an
        // empty shield and a running shatter lockout must not disable it.
        var fighter = new FighterStateComponent {
            PlayerID = 1,
            Stocks = 3,
            MaxHP = 100,
            CurrentHP = 60,
            BlockCharges = 0,
            IsGrounded = 1,
            HitstunFrames = 20
        };
        var runtime = new FighterRuntimeComponent { HeldButtons = (int)GameplayButtons.Block };
        var verb = new FighterVerbComponent {
            Tumble = 1,
            BlockLockoutFrames = BasicComboRules.BlockShatterLockoutFrames
        };

        AssertThat(FighterVerbRules.TryLandingTech(ref fighter, in runtime, ref verb))
            .OverrideFailureMessage("The tech must fire with zero charges and the lockout running.")
            .IsTrue();
        AssertThat(fighter.HitstunFrames).IsEqual(0);
        AssertThat(verb.TechLockoutFrames).IsEqual(BasicComboRules.LandingTechRecoveryFrames);
        // The tech consumed neither the lockout nor a phantom charge.
        AssertThat(verb.BlockLockoutFrames).IsEqual(BasicComboRules.BlockShatterLockoutFrames);
        AssertThat(fighter.BlockCharges).IsEqual(0);
    }

    [TestCase]
    public void RallyEchoAccruesDrainsAndDefersTheVictimMeter() {
        var simulation = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 0f), BuildVictim(maxHP: 400), seed: 904);
        int connectTick = LandOpener(simulation, out int damageDealt);

        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent hitVerb)).IsTrue();
        AssertThat(hitVerb.EchoPool > FP64.Zero)
            .OverrideFailureMessage("A survivable hit must stash a Rally echo on the victim.")
            .IsTrue();
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent hit)).IsTrue();
        float meterAtHit = hit.Influence.ToFloat();
        AssertThat(meterAtHit < damageDealt / 4f)
            .OverrideFailureMessage("The echo portion's meter must not accrue at hit time.")
            .IsTrue();

        // 150-frame drain plus the hitstop freeze and a small pad: the pool is
        // gone and the deferred meter has landed — total exactly damage/4.
        for (int tick = connectTick + 1; tick <= connectTick + 200; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent drained)).IsTrue();
        AssertThat(drained.EchoPool.RawValue).IsEqual(0L);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent settled)).IsTrue();
        AssertThat(System.MathF.Abs(settled.Influence.ToFloat() - damageDealt / 4f) < 0.05f)
            .OverrideFailureMessage(
                $"Drained echo must complete the victim's meter to damage/4 (is {settled.Influence.ToFloat()}).")
            .IsTrue();
    }

    [TestCase]
    public void RallyReclaimIsCappedAtTwiceTheReclaimingHitsDamage() {
        // V7.3 rework: a heavy opener stashes a big echo on the victim; their
        // light counter-hit reclaims only min(pool, hitDamage x 2). The
        // remainder persists and keeps draining — no more one-poke cashouts.
        var simulation = NewSimulation(
            BuildAttacker(damage: 300f, knockback: 0f),
            BuildAttacker(damage: 10f, knockback: 0f),
            seed: 905);
        int connectTick = LandOpener(simulation, out int openerDamage);
        AssertThat(openerDamage).IsEqual(240);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent wounded)).IsTrue();
        int hpAfterHit = wounded.CurrentHP;
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent pool)).IsTrue();
        AssertThat(pool.EchoPool > FP64.Zero).IsTrue();

        // Ride out hitstop + hitstun, then the victim swings back with their
        // own 8-damage opener. Its cap is 8 x 2 = 16 — far below the pool even
        // after the drain that ran in between.
        int tick = connectTick + 1;
        for (; tick <= connectTick + 45; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (int step = 1; step <= 12; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent struck)).IsTrue();
        int counterDamage = struck.MaxHP - struck.CurrentHP;
        AssertThat(counterDamage)
            .OverrideFailureMessage("The 8-damage counter-hit must have landed for the reclaim to trigger.")
            .IsEqual(8);
        int expectedReclaim = (int)(counterDamage * FTT.Combat.BasicComboRules.RallyReclaimDamageMultiplier);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent healed)).IsTrue();
        AssertThat(healed.CurrentHP)
            .OverrideFailureMessage("The reclaim must restore exactly hitDamage x 2 — never the whole pool.")
            .IsEqual(hpAfterHit + expectedReclaim);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent remainder)).IsTrue();
        AssertThat(remainder.EchoPool > FP64.Zero)
            .OverrideFailureMessage("The unreclaimed remainder must persist in the pool.")
            .IsTrue();
        AssertThat(remainder.EchoDrainPerFrame > FP64.Zero)
            .OverrideFailureMessage("The persisting remainder must keep draining at the unchanged rate.")
            .IsTrue();
    }

    [TestCase]
    public void ADefiedHitGeneratesNoRallyEcho() {
        // V7.3 guardrail pin: the meter consumed the entire defied blow —
        // "shatters to 0" must read as exactly that, so the survivor gets
        // neither a Rally echo nor victim meter from the hit.
        var simulation = NewSimulation(
            BuildAttacker(damage: 125f, knockback: 0f, maxHP: 400),
            BuildAttacker(damage: 125f, knockback: 0f, maxHP: 100),
            seed: 907);

        // Victim (player 1) lands first: +100 influence, full meter.
        int tick = 0;
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);

        // The lethal answer: Defy History fires instead of the KO.
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Frame(tick, 0, GameplayButtons.None));
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent survivor)).IsTrue();
        AssertThat(survivor.CurrentHP).IsEqual(1);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.DefyHistoryUsed).IsEqual(1);
        AssertThat(verb.EchoPool.RawValue)
            .OverrideFailureMessage("A defied hit must stash no Rally echo.")
            .IsEqual(0L);
        AssertThat(survivor.Influence.RawValue)
            .OverrideFailureMessage("A defied hit must accrue no victim meter — the meter shattered to zero.")
            .IsEqual(0L);
    }

    [TestCase]
    public void DefyHistorySavesExactlyOneLethalHitAtFullMeter() {
        // The victim's own 100-damage opener fills their meter in one hit;
        // the attacker's identical opener is lethal against their 100 HP.
        var simulation = NewSimulation(
            BuildAttacker(damage: 125f, knockback: 0f, maxHP: 400),
            BuildAttacker(damage: 125f, knockback: 0f, maxHP: 100),
            seed: 906);

        // Victim (player 1) lands first: +100 influence, full meter.
        int tick = 0;
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.BasicAttack));
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);

        // The lethal answer: Defy History fires instead of the KO.
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Frame(tick, 0, GameplayButtons.None));
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent survivor)).IsTrue();
        AssertThat(survivor.Stocks)
            .OverrideFailureMessage("A lethal hit against a full meter must not KO.")
            .IsEqual(3);
        AssertThat(survivor.CurrentHP).IsEqual(1);
        AssertThat(survivor.Influence.RawValue)
            .OverrideFailureMessage("Defy History shatters the meter to zero.")
            .IsEqual(0L);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent used)).IsTrue();
        AssertThat(used.DefyHistoryUsed).IsEqual(1);

        // V7.6 D04 (Package 11 A1b): the survivor is invulnerable through the
        // Defy presentation and for 60 resumed control ticks afterwards, so the
        // follow-up has to wait that window out before "once per match" can be
        // observed at all. The 60 idle steps above are consumed partly by the
        // 12-frame Defy hitstop, which pauses the countdown.
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterDefense(1, out FighterDefenseComponent window)).IsTrue();
        AssertThat(FighterDefenseRules.IsDefyProtected(in window))
            .OverrideFailureMessage("The protected second must have elapsed before the follow-up.")
            .IsFalse();

        // Once per match: the next lethal hit kills normally.
        tick++;
        simulation.Advance(Frame(tick, 0, GameplayButtons.BasicAttack), Frame(tick, 0, GameplayButtons.None));
        for (int step = 0; step < 60; step++) {
            tick++;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent defeated)).IsTrue();
        AssertThat(defeated.Stocks)
            .OverrideFailureMessage("Defy History is once per match — the second lethal hit lands.")
            .IsEqual(2);
    }

    // ---- 1.1(3): the landing tech locks every action (combat and movement update 2026-10-16) ----

    /// <summary>The verbs the tech lock refuses: the name, the press, and what is held with it.</summary>
    private static readonly (string Name, GameplayButtons Pressed, GameplayButtons Held)[] TechLockVerbs = {
        ("Special 1", GameplayButtons.Special1, GameplayButtons.Special1),
        ("Special 2", GameplayButtons.Special2, GameplayButtons.Special2),
        ("the movement ability", GameplayButtons.MovementAbility, GameplayButtons.MovementAbility),
        ("the grab chord", GameplayButtons.BasicAttack, GameplayButtons.Block | GameplayButtons.BasicAttack),
        ("the direct grab", GameplayButtons.Grab, GameplayButtons.Grab),
        ("BasicAttack", GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
        ("a paid Ultimate", GameplayButtons.Ultimate, GameplayButtons.Ultimate)
    };

    /// <summary>
    /// 1.1(3) (plan §2.1.3): the 12 invulnerable tech frames allow no action.
    /// Each verb pressed on every one of L+1..L+12 starts nothing, and the
    /// first press after the lock is accepted.
    /// </summary>
    [TestCase]
    public void TheLandingTechLocksEveryVerbForItsTwelveFramesAndThePressAfterIsAccepted() {
        foreach ((string name, GameplayButtons pressed, GameplayButtons held) in TechLockVerbs) {
            FighterSimulation simulation = NewTechSimulation(emptyShield: false);
            int tick = LaunchAndTech(simulation, 0, out _);
            AssertThat(simulation.SeedFighterInfluenceForTest(1, 100)).IsTrue();
            for (int frame = 1; frame <= BasicComboRules.LandingTechRecoveryFrames; frame++, tick++) {
                simulation.Advance(Frame(tick, 0, GameplayButtons.None), Pressing(tick, pressed, held));
                AssertThat(VerbStarted(simulation, 1, pressed, held))
                    .OverrideFailureMessage($"{name} started on tech frame {frame}.")
                    .IsFalse();
            }
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent done)).IsTrue();
            AssertThat(done.TechLockoutFrames).IsEqual(0);
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Pressing(tick, pressed, held));
            AssertThat(VerbStarted(simulation, 1, pressed, held))
                .OverrideFailureMessage($"{name} must be accepted on the first tick after the tech.")
                .IsTrue();
        }
    }

    /// <summary>
    /// 1.1(3): the landing tick L itself is locked too — the ability, combat
    /// and grab systems read the tick's presses after the landing resolves, so
    /// they are discarded there. The victim holds Block for the tech with an
    /// empty shield, so the stance never rises to refuse them instead.
    /// The BasicAttack row is skipped here (fix round A, CM-FIXA-SIM): the
    /// tech needs Block held, so BasicAttack on L is the grab chord — its own
    /// row — and a swing can never start on L anyway, because the movement
    /// system resolves the string while the victim is still in hitstun, before
    /// the landing. The row stays in the L+1..L+12 case, where it means
    /// something.
    /// </summary>
    [TestCase]
    public void NoVerbStartsOnTheTechsOwnLandingTick() {
        int landing = LaunchAndTech(NewTechSimulation(emptyShield: true), 0, out int connect) - 1;
        foreach ((string name, GameplayButtons pressed, GameplayButtons held) in TechLockVerbs) {
            if (pressed == GameplayButtons.BasicAttack && held == GameplayButtons.BasicAttack) continue;
            FighterSimulation simulation = NewTechSimulation(emptyShield: true);
            AssertThat(simulation.SeedFighterInfluenceForTest(1, 100)).IsTrue();
            int tick = 0;
            for (; tick < landing; tick++) {
                simulation.Advance(LauncherFrame(tick, 0),
                    tick <= connect ? Frame(tick, 0, GameplayButtons.None) : FrameHeld(tick, GameplayButtons.Block));
            }
            simulation.Advance(LauncherFrame(tick, 0), Pressing(tick, pressed, held | GameplayButtons.Block));
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            AssertThat(verb.TechLockoutFrames)
                .OverrideFailureMessage($"Harness: the {name} run did not tech on the probe's landing tick.")
                .IsEqual(BasicComboRules.LandingTechRecoveryFrames);
            AssertThat(VerbStarted(simulation, 1, pressed, held))
                .OverrideFailureMessage($"{name} started on the tech's own landing tick.")
                .IsFalse();
        }
    }

    /// <summary>
    /// 1.1(3): no D10 "not yet" refusal is classified inside the lock. The
    /// victim's own swing arms the ability lockout on the launch tick, so a
    /// Special or Ultimate press read during the tech would be a lockout-alone
    /// refusal and flash; discarded, it never reaches the classifier.
    /// </summary>
    [TestCase]
    public void NoNotYetRefusalIsClassifiedInsideTheTechLock() {
        LaunchAndTech(NewTechSimulation(emptyShield: true), 0, out int connect);
        FighterSimulation simulation = NewTechSimulation(emptyShield: true);
        AssertThat(simulation.SeedFighterInfluenceForTest(1, 100)).IsTrue();
        int tick = 0;
        for (; tick < connect; tick++) {
            simulation.Advance(LauncherFrame(tick, 0), Frame(tick, 0, GameplayButtons.None));
        }
        // The victim's swing arms A on the launch tick; the launch's hitstun
        // cancels it before its startup ends, so it never reaches the attacker.
        simulation.Advance(LauncherFrame(tick, 0), Frame(tick, 0, GameplayButtons.BasicAttack));
        tick++;
        const GameplayButtons presses = GameplayButtons.Special1 | GameplayButtons.Special2 | GameplayButtons.Ultimate;
        bool teched = false;
        int lockedTicks = 0;
        for (int step = 0; step < 120 && lockedTicks < BasicComboRules.LandingTechRecoveryFrames; step++, tick++) {
            LockoutRefusalBaseline baseline = Baseline(simulation, 1);
            bool inLock = teched;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None),
                inLock ? Pressing(tick, presses, presses) : FrameHeld(tick, GameplayButtons.Block));
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            if (!teched && verb.TechLockoutFrames == BasicComboRules.LandingTechRecoveryFrames) {
                teched = true;
                AssertThat(runtime.AbilityLockoutFrames > BasicComboRules.LandingTechRecoveryFrames)
                    .OverrideFailureMessage($"Harness: the lockout ({runtime.AbilityLockoutFrames}) must outlast the tech.")
                    .IsTrue();
            }
            if (inLock) {
                lockedTicks++;
                AssertThat(Classify(simulation, 1, in baseline))
                    .OverrideFailureMessage($"A press on tech frame {lockedTicks} was classified as a refusal.")
                    .IsEqual(FighterLockoutRefusal.None);
            }
        }
        AssertThat(lockedTicks)
            .OverrideFailureMessage("Harness: the victim never teched.")
            .IsEqual(BasicComboRules.LandingTechRecoveryFrames);
    }

    /// <summary>
    /// A tech victim with Area-execution Specials (accepted on press, so
    /// acceptance is visible as an armed cooldown). With
    /// <paramref name="emptyShield"/> its stance never rises, so a held Block
    /// only techs and only the tech lock can refuse a verb.
    /// </summary>
    private static FighterSimulation NewTechSimulation(bool emptyShield) {
        CharacterData victim = BuildVictim(maxHP: 400);
        if (emptyShield) victim.MaxBlockCharges = 0;
        victim.SpecialAttackOne = new AbilityData { ExecutionType = AbilityExecutionType.Area, BaseDamage = 2f };
        victim.SpecialAttackTwo = new AbilityData { ExecutionType = AbilityExecutionType.Area, BaseDamage = 2f };
        return NewSimulation(BuildAttacker(damage: 10f, knockback: 4f, launcherSpecial: true), victim, seed: 907);
    }

    /// <summary>The attacker's launcher press on <paramref name="start"/>, neutral otherwise.</summary>
    private static PlayerInputFrame LauncherFrame(int tick, int start) =>
        Frame(tick, 0, tick == start ? GameplayButtons.Special1 : GameplayButtons.None);

    /// <summary>
    /// Presses the attacker's launcher on <paramref name="start"/>, steps the
    /// idle victim until it connects (whatever the Special's startup), then
    /// has the victim hold Block until its landing tech. Returns the tick
    /// after the landing tick L; <paramref name="connect"/> is the launch tick.
    /// </summary>
    private static int LaunchAndTech(FighterSimulation simulation, int start, out int connect) {
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        connect = -1;
        int tick = start;
        for (int limit = start + 60; tick < limit && connect < 0; tick++) {
            simulation.Advance(LauncherFrame(tick, start), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (victim.CurrentHP < before.CurrentHP) connect = tick;
        }
        AssertThat(connect >= 0).OverrideFailureMessage("Harness: the launcher never connected.").IsTrue();
        for (int limit = tick + 120; tick < limit; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            if (verb.TechLockoutFrames == BasicComboRules.LandingTechRecoveryFrames) return tick + 1;
        }
        AssertThat(false).OverrideFailureMessage("Harness: the launched victim never teched.").IsTrue();
        return tick;
    }

    private static PlayerInputFrame Pressing(int tick, GameplayButtons pressed, GameplayButtons held) => new() {
        Tick = (uint)tick,
        Held = held,
        Pressed = pressed
    };

    /// <summary>Whether the verb a test pressed started (its cost is spent, or its phase began).</summary>
    private static bool VerbStarted(FighterSimulation simulation, int playerID, GameplayButtons pressed, GameplayButtons held) {
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        AssertThat(simulation.TryGetFighterUltimateActivation(playerID, out FighterUltimateActivationComponent activation)).IsTrue();
        if ((pressed & GameplayButtons.Special1) != 0) return runtime.SpecialOneReuseFrames > 0;
        if ((pressed & GameplayButtons.Special2) != 0) return runtime.SpecialTwoReuseFrames > 0;
        if ((pressed & GameplayButtons.MovementAbility) != 0) return runtime.MovementCooldownFrames > 0;
        if ((pressed & GameplayButtons.Ultimate) != 0) return activation.Phase != FighterUltimateActivationRules.PhaseNone;
        if ((pressed & GameplayButtons.Grab) != 0 || (held & GameplayButtons.Block) != 0) return verb.GrabPhase != 0;
        return runtime.AttackPhase != FighterBasicAttackRules.PhaseNone;
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

    // ---- Harness -------------------------------------------------------------

    /// <summary>
    /// Presses BasicAttack on tick 0 and advances until player 1 loses HP.
    /// Returns the tick the opener connected on and the damage it dealt.
    /// </summary>
    /// <summary>
    /// M05 (Package 12 W3b): lands the attacker's authored launching Special 1
    /// (see <c>BuildAttacker(launcherSpecial: true)</c>) and returns the
    /// connect tick.
    /// </summary>
    private static int LandLauncher(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        // Decision 1.2 (2026-10-16): the melee Special connects on its active
        // entry, T+S, so the connect tick is the last one stepped.
        int connectTick = SpecialCastTestKit.StepUntilFired(simulation, 0, 1) - 1;
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        AssertThat(after.CurrentHP < before.CurrentHP && after.IsGrounded == 0)
            .OverrideFailureMessage("The launching Special never connected and launched.")
            .IsTrue();
        return connectTick;
    }

    private static int LandOpener(FighterSimulation simulation, out int damageDealt) {
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        simulation.Advance(Frame(0, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        for (int tick = 1; tick <= 30; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            if (target.CurrentHP < before.CurrentHP) {
                damageDealt = before.CurrentHP - target.CurrentHP;
                return tick - 1;
            }
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
        damageDealt = before.CurrentHP - after.CurrentHP;
        AssertThat(damageDealt > 0)
            .OverrideFailureMessage("The opener never connected within 30 ticks.")
            .IsTrue();
        return 30;
    }

    private static FighterSimulation NewSimulation(CharacterData playerOne, CharacterData playerTwo, int seed) => new(
        FighterLoadoutFactory.FromCharacterData(playerOne),
        FighterLoadoutFactory.FromCharacterData(playerTwo),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildAttacker(
        float damage, float knockback, int maxHP = 400, bool launcherSpecial = false) => new() {
        CharacterID = "tesla",
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = damage,
        BasicAttackKnockback = knockback,
        SpecialAttackOne = launcherSpecial
            ? new AbilityData {
                BaseDamage = damage,
                KnockbackForce = new Godot.Vector2(knockback, -knockback),
                HitstunFrames = 30,
                Launches = true
            }
            : new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static CharacterData BuildVictim(int maxHP) => new() {
        CharacterID = "joan",
        MaxHP = maxHP,
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

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed, sbyte moveY = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = moveY,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame FrameHeld(int tick, GameplayButtons held) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = GameplayButtons.None
    };
}
