using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.3 hit-pipeline correctness pass: a launching hit whose victim
/// takes no hitstop resolves its DI immediately (a stashed launch never sits
/// armed to replay under a later hit), construct and hazard ticks are exempt
/// from hitstop (only direct player-authored hits freeze), venom routes
/// through the unattributed-damage chokepoint (Defy History, Rally echo, and
/// victim meter all apply to DoT), and melee-execution specials are front-only
/// like every other melee hit. Numbers come from <see cref="BasicComboRules"/>
/// and <c>FighterDamageRules</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHitPipelineTests {

    [TestCase]
    public void AZeroDamageLaunchNeverReplaysOnALaterHit() {
        // A zero-damage launching hit (a zone expiry shove) applies no hitstop,
        // so its stashed DI launch must resolve immediately instead of sitting
        // armed until an unrelated later hit's hitstop replays it.
        FighterStateComponent attacker = NewFighter(0);
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;
        FighterStateComponent target = NewFighter(1);
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent targetVerb = default;
        FighterTuningComponent tuning = default;

        FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, in tuning,
            FighterDamageRules.SpecialAttackClass,
            0, FP64.FromInt(4), 20,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x - FP64.One,
            appliesHitstop: false);

        AssertThat(target.HitstunFrames).IsEqual(20);
        AssertThat(targetVerb.HitstopFrames).IsEqual(0);
        AssertThat(target.Velocity.y > FP64.Zero)
            .OverrideFailureMessage("The launch must have been applied immediately.")
            .IsTrue();
        AssertThat(targetVerb.PendingLaunchActive)
            .OverrideFailureMessage("With no hitstop the pending launch must resolve immediately, not stay armed.")
            .IsEqual(0);

        // The later, ordinary hit freezes the victim — and must find no stale
        // launch waiting to replay when that freeze ends.
        FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, in tuning,
            FighterDamageRules.BasicAttackClass,
            10, FP64.Zero, 10,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x - FP64.One);

        AssertThat(targetVerb.HitstopFrames > 0)
            .OverrideFailureMessage("The direct hit must apply its ordinary hitstop.")
            .IsTrue();
        AssertThat(targetVerb.PendingLaunchActive)
            .OverrideFailureMessage("No stale launch may be armed under the later hit's hitstop.")
            .IsEqual(0);
    }

    [TestCase]
    public void ConstructTicksApplyNoHitstop() {
        // Tesla's coil is the canonical deployed construct: its arc damages the
        // opponent, but neither party may freeze (V7.3 — only direct
        // player-authored hits carry hitstop).
        var simulation = new FighterSimulation(
            FighterCharacterID.Tesla,
            FighterCharacterID.Joan,
            seed: 990,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        bool arcLanded = false;
        for (int tick = 1; tick <= 300 && !arcLanded; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP < before.CurrentHP) {
                arcLanded = true;
                AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent victim)).IsTrue();
                AssertThat(victim.HitstopFrames)
                    .OverrideFailureMessage("A construct arc must not freeze its victim.")
                    .IsEqual(0);
                AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent owner)).IsTrue();
                AssertThat(owner.HitstopFrames)
                    .OverrideFailureMessage("A construct arc must not freeze its owner either.")
                    .IsEqual(0);
            }
        }
        AssertThat(arcLanded)
            .OverrideFailureMessage("The deployed coil never landed an arc — the scenario is broken.")
            .IsTrue();
    }

    [TestCase]
    public void HazardTicksApplyNoHitstop() {
        // ApplyEnvironmentHit is the one chokepoint every stage hazard routes
        // through; it is hard-wired hitstop-exempt — landing or blocked.
        FighterStateComponent target = NewFighter(1);
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent targetVerb = default;
        FighterTuningComponent tuning = default;

        bool landed = FighterDamageRules.ApplyEnvironmentHit(
            ref target, ref targetRuntime, ref targetVerb, in tuning,
            6, FP64.FromInt(2), 10, target.Position.x - FP64.One);
        AssertThat(landed).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(94);
        AssertThat(targetVerb.HitstopFrames)
            .OverrideFailureMessage("A hazard tick must not freeze its victim.")
            .IsEqual(0);

        // Blocked tick: the charge is spent, but the blocked 2f freeze is
        // exempt too.
        FighterStateComponent blocker = NewFighter(1);
        var blockerRuntime = new FighterRuntimeComponent { HeldButtons = (int)GameplayButtons.Block };
        FighterVerbComponent blockerVerb = default;
        bool blockedLanded = FighterDamageRules.ApplyEnvironmentHit(
            ref blocker, ref blockerRuntime, ref blockerVerb, in tuning,
            6, FP64.FromInt(2), 10, blocker.Position.x + FP64.One);
        AssertThat(blockedLanded).IsFalse();
        AssertThat(blocker.CurrentHP).IsEqual(100);
        AssertThat(blocker.BlockCharges)
            .OverrideFailureMessage("The blocked hazard tick still costs its one charge.")
            .IsEqual(2);
        AssertThat(blockerVerb.HitstopFrames)
            .OverrideFailureMessage("A blocked hazard tick must not freeze the blocker.")
            .IsEqual(0);
    }

    [TestCase]
    public void VenomCannotKillThroughAFullDefyMeter() {
        // The unattributed-damage chokepoint runs Defy History first: a lethal
        // venom tick against a full meter shatters the meter and leaves the
        // victim at 1 HP — once per match, like any hit.
        FighterStateComponent fighter = NewFighter(1);
        fighter.CurrentHP = 1;
        fighter.Influence = FP64.FromInt(100);
        var runtime = new FighterRuntimeComponent { UsesStocks = 1 };
        FighterVerbComponent verb = default;
        FighterTuningComponent tuning = default;

        FighterDamageRules.ApplyUnattributedDamage(ref fighter, ref runtime, ref verb, in tuning, 2);

        AssertThat(fighter.CurrentHP)
            .OverrideFailureMessage("A lethal venom tick against a full meter must not KO.")
            .IsEqual(1);
        AssertThat(fighter.Stocks).IsEqual(3);
        AssertThat(runtime.KnockoutsSuffered).IsEqual(0);
        AssertThat(fighter.Influence.RawValue)
            .OverrideFailureMessage("Defy History shatters the meter to zero.")
            .IsEqual(0L);
        AssertThat(verb.DefyHistoryUsed).IsEqual(1);

        // Once per match: the next lethal tick takes the stock.
        FighterDamageRules.ApplyUnattributedDamage(ref fighter, ref runtime, ref verb, in tuning, 2);
        AssertThat(runtime.KnockoutsSuffered).IsEqual(1);
        AssertThat(fighter.Stocks).IsEqual(2);
    }

    [TestCase]
    public void VenomTicksAccrueRallyEchoAndVictimMeter() {
        // Cleopatra's string finisher applies the authored venom mark; 60
        // frames later the DoT tick lands through the unattributed chokepoint:
        // the victim's Echo Pool jumps and their meter accrues, while the
        // poisoner earns nothing from the tick.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(NewCharacter("cleopatra")),
            FighterLoadoutFactory.FromCharacterData(NewCharacter("joan", maxHP: 400)),
            seed: 991,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);

        // Land the full three-hit string (the finisher carries the venom).
        int hits = 0;
        int tick = 0;
        for (; tick <= 200 && hits < 3; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            bool press = hits == 0
                ? runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                : runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            simulation.Advance(
                Frame(tick, press ? GameplayButtons.BasicAttack : GameplayButtons.None),
                Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP < before.CurrentHP) hits++;
        }
        AssertThat(hits).OverrideFailureMessage("The string must land all three hits.").IsEqual(3);

        // Idle until the venom tick: the victim's HP drops with no attack out.
        bool tickLanded = false;
        FP64 previousFrameMeterDelta = FP64.Zero;
        for (int step = 0; step <= 90 && !tickLanded; step++, tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent poisonerBefore)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verbBefore)).IsTrue();
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP >= before.CurrentHP) {
                // A non-tick idle frame: only the Rally drain feeds the meter.
                previousFrameMeterDelta = after.Influence - before.Influence;
                continue;
            }

            tickLanded = true;
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verbAfter)).IsTrue();
            AssertThat(verbAfter.EchoPool > verbBefore.EchoPool)
                .OverrideFailureMessage("The venom tick must accrue a Rally echo on the victim "
                    + $"(pool {verbBefore.EchoPool.ToFloat()} -> {verbAfter.EchoPool.ToFloat()}).")
                .IsTrue();
            // The tick frame's meter gain exceeds the drain-only baseline: the
            // chokepoint added the permanent portion's damage/4 on top.
            AssertThat(after.Influence - before.Influence > previousFrameMeterDelta)
                .OverrideFailureMessage("The venom tick must accrue victim meter beyond the drain baseline.")
                .IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent poisonerAfter)).IsTrue();
            AssertThat(poisonerAfter.Influence.RawValue)
                .OverrideFailureMessage("The poisoner earns nothing from an unattributed tick.")
                .IsEqual(poisonerBefore.Influence.RawValue);
        }
        AssertThat(tickLanded)
            .OverrideFailureMessage("The venom mark never ticked — the scenario is broken.")
            .IsTrue();
    }

    [TestCase]
    public void MeleeSpecialsNeverHitBehindTheAttacker() {
        // Joan's Divine Piercing is a melee-execution intent. Facing the
        // target it lands; turned away it must whiff (front-only, matching the
        // string hitboxes).
        var facing = NewJoanPair(seed: 992);
        facing.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(facing.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP < struck.MaxHP)
            .OverrideFailureMessage("The control case must land: the target stands in front.")
            .IsTrue();

        var turned = NewJoanPair(seed: 992);
        // One tick of held-left turns player one away from the opponent.
        turned.Advance(
            new PlayerInputFrame { Tick = 0, MoveX = -127, Held = GameplayButtons.None, Pressed = GameplayButtons.None },
            Frame(0, GameplayButtons.None));
        AssertThat(turned.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
        AssertThat(attacker.FacingRight).IsEqual(0);
        turned.Advance(Frame(1, GameplayButtons.Special2), Frame(1, GameplayButtons.None));
        for (int tick = 2; tick <= 6; tick++) {
            turned.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(turned.TryGetFighter(1, out FighterStateComponent untouched)).IsTrue();
        AssertThat(untouched.CurrentHP)
            .OverrideFailureMessage("A melee-execution special must never hit behind the attacker.")
            .IsEqual(untouched.MaxHP);
    }

    // ---- Harness -------------------------------------------------------------

    private static FighterStateComponent NewFighter(int playerID) => new() {
        PlayerID = playerID,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1
    };

    private static FighterSimulation NewJoanPair(int seed) => new(
        FighterCharacterID.Joan,
        FighterCharacterID.Joan,
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData NewCharacter(string characterID, int maxHP = 100) => new() {
        CharacterID = characterID,
        MaxHP = maxHP,
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

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = pressed,
        Pressed = pressed
    };
}
