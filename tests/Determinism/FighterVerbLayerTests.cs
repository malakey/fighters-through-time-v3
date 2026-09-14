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
        var neutral = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f), BuildVictim(maxHP: 400), seed: 902);
        var influenced = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f), BuildVictim(maxHP: 400), seed: 902);

        int connectTick = LandOpener(neutral, out _);
        LandOpener(influenced, out _);

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
        var teched = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f), BuildVictim(maxHP: 400), seed: 903);
        var control = NewSimulation(
            BuildAttacker(damage: 10f, knockback: 4f), BuildVictim(maxHP: 400), seed: 903);

        int connectTick = LandOpener(teched, out _);
        LandOpener(control, out _);

        bool techFired = false;
        for (int tick = connectTick + 1; tick <= connectTick + 90; tick++) {
            teched.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            control.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            AssertThat(teched.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (victim.IsGrounded != 0 && victim.HitstunFrames == 0
                && victim.InvulnerabilityFrames > 0) {
                techFired = true;
                AssertThat(victim.InvulnerabilityFrames <= BasicComboRules.LandingTechRecoveryFrames)
                    .OverrideFailureMessage("The tech grants exactly the authored recovery window.")
                    .IsTrue();
                break;
            }
        }
        AssertThat(techFired)
            .OverrideFailureMessage("Holding Block on ground contact must tech the landing.")
            .IsTrue();
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

    // ---- Harness -------------------------------------------------------------

    /// <summary>
    /// Presses BasicAttack on tick 0 and advances until player 1 loses HP.
    /// Returns the tick the opener connected on and the damage it dealt.
    /// </summary>
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

    private static CharacterData BuildAttacker(float damage, float knockback, int maxHP = 400) => new() {
        CharacterID = "tesla",
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = damage,
        BasicAttackKnockback = knockback,
        SpecialAttackOne = new AbilityData(),
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
