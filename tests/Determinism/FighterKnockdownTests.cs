using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 12 W3b (M05/M07), the Fighter-side pins: only an authored launcher
/// launches (a non-launcher is grounded knockback, which re-opens the hit-2
/// Block escape — the M06 follow-up); a missed tech is a 30-frame invulnerable
/// knockdown on Klotho component 320 followed by a 10-frame neutral or 14-frame
/// roll get-up with no invulnerability; Lincoln's hit 2 launches and its victim
/// DIs and techs instead of block-escaping; the melee intent reads its authored
/// hit contract; and the knockdown is ordinary rollback state. The two
/// COMBAT_VALIDATION scenarios (Lincoln Hit 2 launch; knockdown and get-up)
/// are pinned here as deterministic scenario captures. Pure C#: no engine.
/// </summary>
[TestSuite]
public class FighterKnockdownTests {

    [TestCase]
    public void TheLaunchFlagTableIsTheAuthoredOneAndTheIntentReadsItsContract() {
        // M05/M07: the string's finisher launches, hits 1-2 do not — except
        // Lincoln's hit 2. Directional strikes and throws are launchers.
        foreach (string id in new[] {
                     "einstein", "joan", "leonardo", "lincoln", "cleopatra",
                     "tesla", "shakespeare", "mozart", "pocahontas" }) {
            AssertThat(BasicComboRules.StringHitLaunchesFor(id, 0)).IsFalse();
            AssertThat(BasicComboRules.StringHitLaunchesFor(id, 1))
                .OverrideFailureMessage($"{id}: only Lincoln's hit 2 launches (M07).")
                .IsEqual(id == "lincoln");
            AssertThat(BasicComboRules.StringHitLaunchesFor(id, 2)).IsTrue();
        }
        AssertThat(BasicComboRules.DirectionalAttackLaunches).IsTrue();
        AssertThat(BasicComboRules.ThrowLaunches).IsTrue();

        // DEFER-SIM-ABILITY-HITSTUN (generic half): a projected contract wins;
        // an unprojected (zero) slot keeps the legacy 18/30 frames and launches.
        var authored = new FighterAbilityHitData { HitstunFrames = 12, Launches = false };
        FighterCombatSystem.ResolveIntentContract(in authored, 18, out int hitstun, out bool launches);
        AssertThat(hitstun).IsEqual(12);
        AssertThat(launches).IsFalse();
        FighterAbilityHitData unprojected = default;
        FighterCombatSystem.ResolveIntentContract(in unprojected, 30, out hitstun, out launches);
        AssertThat(hitstun).IsEqual(30);
        AssertThat(launches).IsTrue();

        // A live melee Special authored non-launching: grounded knockback at the
        // authored 12 frames, never a tumble.
        var simulation = new FighterSimulation(
            Loadout(LauncherModes(launches: false, hitstunFrames: 12)),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 41, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
        AssertThat(victim.CurrentHP < victim.MaxHP).IsTrue();
        AssertThat(victim.HitstunFrames).IsEqual(12);
        AssertThat(victim.IsGrounded).IsEqual(1);
        AssertThat(verb.Tumble).IsEqual(0);
        AssertThat(verb.PendingLaunchActive).IsEqual(0);
        AssertThat(victim.Velocity.x > FP64.Zero)
            .OverrideFailureMessage("Grounded knockback is a horizontal slide away from the attacker.")
            .IsTrue();
        AssertThat(victim.Velocity.y).IsEqual(FP64.Zero);
    }

    [TestCase]
    public void AMissedTechIsAThirtyFrameInvulnerableKnockdownThenATenFrameNeutralGetUp() {
        // COMBAT_VALIDATION "Knockdown and get-up (M05)", neutral half, flat ground.
        FighterSimulation simulation = LaunchedSimulation(seed: 51);
        int tick = LandWithoutTech(simulation);

        AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent landed)).IsTrue();
        AssertThat(landed.KnockdownFrames).IsEqual(BasicComboRules.KnockdownFrames);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent down)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent downVerb)).IsTrue();
        AssertThat(down.HitstunFrames).IsEqual(0);
        AssertThat(downVerb.Tumble).IsEqual(0);
        AssertThat(downVerb.TechLockoutFrames)
            .OverrideFailureMessage("A missed tech is not a tech.")
            .IsEqual(0);
        AssertThat(down.Velocity).IsEqual(FPVector2.Zero);

        // The 30 knockdown frames: down, invulnerable, and a hit is refused —
        // no damage, so no meter and no Rally can be earned from the fighter.
        for (int frame = 1; frame <= BasicComboRules.KnockdownFrames; frame++) {
            tick++;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(victim.InvulnerabilityFrames > 0)
                .OverrideFailureMessage($"Knockdown frame {frame} must be invulnerable.")
                .IsTrue();
            AssertThat(TryDirectHit(simulation))
                .OverrideFailureMessage($"A hit on knockdown frame {frame} must be refused.")
                .IsFalse();
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            if (frame < BasicComboRules.KnockdownFrames) {
                AssertThat(knockdown.KnockdownFrames).IsEqual(BasicComboRules.KnockdownFrames - frame);
            } else {
                // No direction held: the neutral get-up is automatic.
                AssertThat(knockdown.KnockdownFrames).IsEqual(0);
                AssertThat(knockdown.GetUpKind).IsEqual(BasicComboRules.GetUpNeutral);
                AssertThat(knockdown.GetUpFrames).IsEqual(BasicComboRules.NeutralGetUpFrames);
            }
        }

        // The 10 neutral get-up frames: in place, no invulnerability, no actions.
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent standing)).IsTrue();
        FP64 standX = standing.Position.x;
        for (int frame = 1; frame <= BasicComboRules.NeutralGetUpFrames; frame++) {
            tick++;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Jump));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(victim.InvulnerabilityFrames)
                .OverrideFailureMessage($"Get-up frame {frame} has no invulnerability.")
                .IsEqual(0);
            AssertThat(victim.Position.x).IsEqual(standX);
            AssertThat(victim.IsGrounded)
                .OverrideFailureMessage("The get-up is action-locked: Jump must not fire.")
                .IsEqual(1);
            if (frame == 1) {
                AssertThat(TryDirectHit(simulation))
                    .OverrideFailureMessage("The get-up is vulnerable.")
                    .IsTrue();
                // Undo nothing: the probe hit ran on copies, the sim is untouched.
            }
        }
        AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent done)).IsTrue();
        AssertThat(FighterKnockdownRules.IsActive(in done)).IsFalse();

        // Control returns on the next tick.
        tick++;
        simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Jump));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent free)).IsTrue();
        AssertThat(free.IsGrounded)
            .OverrideFailureMessage("After the get-up the fighter acts again.")
            .IsEqual(0);
    }

    [TestCase]
    public void HoldingADirectionAtTheEndOfTheKnockdownRollsFourteenFramesThatWay() {
        // COMBAT_VALIDATION "Knockdown and get-up (M05)", roll half, both directions.
        foreach (sbyte held in new sbyte[] { 127, -127 }) {
            FighterSimulation simulation = LaunchedSimulation(seed: 52);
            int tick = LandWithoutTech(simulation);
            for (int frame = 1; frame <= BasicComboRules.KnockdownFrames; frame++) {
                tick++;
                // Only the knockdown's final frame reads the stick.
                sbyte moveX = frame == BasicComboRules.KnockdownFrames ? held : (sbyte)0;
                simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, moveX));
            }
            AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent chosen)).IsTrue();
            AssertThat(chosen.GetUpKind).IsEqual(BasicComboRules.GetUpRoll);
            AssertThat(chosen.GetUpFrames).IsEqual(BasicComboRules.RollGetUpFrames);
            AssertThat(chosen.GetUpDirection).IsEqual(held > 0 ? 1 : -1);

            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            FP64 previousX = before.Position.x;
            int travelFrames = 0;
            for (int frame = 1; frame <= BasicComboRules.RollGetUpFrames; frame++) {
                tick++;
                simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block));
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent rolling)).IsTrue();
                AssertThat(rolling.InvulnerabilityFrames)
                    .OverrideFailureMessage("The roll get-up has no invulnerability.")
                    .IsEqual(0);
                bool moved = held > 0 ? rolling.Position.x > previousX : rolling.Position.x < previousX;
                if (moved) travelFrames++;
                previousX = rolling.Position.x;
            }
            AssertThat(travelFrames)
                .OverrideFailureMessage($"The roll get-up must travel every one of its {BasicComboRules.RollGetUpFrames} frames (held {held}).")
                .IsEqual(BasicComboRules.RollGetUpFrames);
            AssertThat(simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent done)).IsTrue();
            AssertThat(FighterKnockdownRules.IsActive(in done)).IsFalse();
        }
    }

    [TestCase]
    public void LincolnsHitTwoLaunchesAndItsVictimDisAwayAndTechs() {
        // COMBAT_VALIDATION "Lincoln Hit 2 launch (M07)": flat ground, full HP,
        // DI away versus no DI, tech held. Lincoln's victim gets DI and landing
        // tech instead of the grounded hit-2 Block escape.
        var neutral = new FighterSimulation(
            FighterCharacterID.Lincoln, FighterCharacterID.Joan, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var influenced = new FighterSimulation(
            FighterCharacterID.Lincoln, FighterCharacterID.Joan, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        int tick = LandHitTwo(neutral);
        AssertThat(LandHitTwo(influenced)).IsEqual(tick);

        AssertThat(neutral.TryGetFighter(1, out FighterStateComponent launched)).IsTrue();
        AssertThat(neutral.TryGetFighterVerb(1, out FighterVerbComponent launchedVerb)).IsTrue();
        AssertThat(launched.IsGrounded)
            .OverrideFailureMessage("M07: Lincoln's hit 2 launches its victim off the ground.")
            .IsEqual(0);
        AssertThat(launchedVerb.Tumble).IsEqual(1);

        // The victim sits to the attacker's right; DI away holds right.
        bool compared = false;
        bool teched = false;
        bool knockedDown = false;
        for (int step = 0; step < 120 && !teched; step++) {
            tick++;
            neutral.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block));
            influenced.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.Block, 127));
            AssertThat(influenced.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            if (!compared && verb.HitstopFrames == 0) {
                compared = true;
                AssertThat(neutral.TryGetFighter(1, out FighterStateComponent plain)).IsTrue();
                AssertThat(influenced.TryGetFighter(1, out FighterStateComponent bent)).IsTrue();
                AssertThat(bent.Velocity.x > plain.Velocity.x)
                    .OverrideFailureMessage("DI away must bend the launch away from the attacker.")
                    .IsTrue();
                float plainSquared = Square(plain.Velocity.x) + Square(plain.Velocity.y);
                float bentSquared = Square(bent.Velocity.x) + Square(bent.Velocity.y);
                AssertThat(System.MathF.Abs(plainSquared - bentSquared) < plainSquared * 0.01f)
                    .OverrideFailureMessage("DI never changes the launch magnitude.")
                    .IsTrue();
            }
            if (verb.TechLockoutFrames > 0) teched = true;
            influenced.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            if (FighterKnockdownRules.IsActive(in knockdown)) knockedDown = true;
        }
        AssertThat(compared).OverrideFailureMessage("The launch hitstop never ended.").IsTrue();
        AssertThat(teched)
            .OverrideFailureMessage("Block held on ground contact must tech Lincoln's hit-2 launch.")
            .IsTrue();
        AssertThat(knockedDown).OverrideFailureMessage("A teched landing is never a knockdown.").IsFalse();
    }

    [TestCase]
    public void NonLaunchingHitsStayGroundedAndReopenTheHitTwoBlockEscape() {
        // M06 follow-up: with launches selective, hits 1-2 are grounded
        // knockback, so a grounded victim holding Block escapes after hit 2 and
        // the finisher meets the stance (or whiffs) instead of landing.
        var simulation = new FighterSimulation(
            FighterCharacterID.Joan, FighterCharacterID.Joan, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        int hits = 0;
        int lastHP = int.MinValue;
        bool leftTheGround = false;
        bool slid = false;
        bool raisedStance = false;
        bool holdBlock = false;
        for (int tick = 0; tick < 240; tick++) {
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (lastHP == int.MinValue) lastHP = victim.CurrentHP;
            if (victim.CurrentHP < lastHP) {
                hits++;
                holdBlock = true;
            }
            lastHP = victim.CurrentHP;
            if (hits >= 1 && hits <= 2 && victim.IsGrounded == 0) leftTheGround = true;
            if (hits >= 1 && victim.Velocity.x != FP64.Zero) slid = true;
            GameplayButtons attacker = tick % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(Frame(tick, attacker), Frame(tick, holdBlock ? GameplayButtons.Block : GameplayButtons.None));
            simulation.TryGetFighter(1, out FighterStateComponent after);
            simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent afterRuntime);
            simulation.TryGetFighterVerb(1, out FighterVerbComponent afterVerb);
            if (hits >= 2 && FighterBasicAttackRules.IsBlockStance(in after, in afterRuntime, in afterVerb)) {
                raisedStance = true;
            }
            if (raisedStance && tick > 150) break;
        }
        AssertThat(leftTheGround)
            .OverrideFailureMessage("M05: non-launching hits 1-2 must keep a grounded victim on the floor.")
            .IsFalse();
        AssertThat(slid).OverrideFailureMessage("Grounded knockback must still push the victim.").IsTrue();
        AssertThat(raisedStance)
            .OverrideFailureMessage("M06: after hit 2 the grounded victim's held Block must escape into the stance.")
            .IsTrue();
        AssertThat(hits)
            .OverrideFailureMessage($"The finisher must not land on an escaped victim (took {hits}).")
            .IsEqual(2);
    }

    [TestCase]
    public void TheKnockdownIsRollbackStateAndReplaysBitIdentically() {
        // Component 320 is snapshot and hash state: restoring a mid-knockdown
        // snapshot and replaying the same inputs reproduces the same hashes,
        // and a second fresh simulation fed the same inputs agrees too.
        FighterSimulation first = LaunchedSimulation(seed: 53);
        FighterSimulation second = LaunchedSimulation(seed: 53);
        int tick = LandWithoutTech(first);
        AssertThat(LandWithoutTech(second)).IsEqual(tick);
        for (int frame = 0; frame < 10; frame++) {
            tick++;
            first.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            second.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(first.TryGetFighterKnockdown(1, out FighterKnockdownComponent mid)).IsTrue();
        AssertThat(FighterKnockdownRules.IsDown(in mid)).IsTrue();
        byte[] snapshot = first.CaptureFullState();
        int snapshotTick = tick;

        var hashes = new long[40];
        for (int frame = 0; frame < hashes.Length; frame++) {
            tick++;
            sbyte moveX = frame == 19 ? (sbyte)127 : (sbyte)0;
            hashes[frame] = first.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, moveX));
            long other = second.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, moveX));
            AssertThat(other).OverrideFailureMessage($"Fresh replay diverged at frame {frame}.").IsEqual(hashes[frame]);
        }

        first.RestoreFullState(snapshot);
        AssertThat(first.TryGetFighterKnockdown(1, out FighterKnockdownComponent restored)).IsTrue();
        AssertThat(restored.KnockdownFrames).IsEqual(mid.KnockdownFrames);
        tick = snapshotTick;
        for (int frame = 0; frame < hashes.Length; frame++) {
            tick++;
            sbyte moveX = frame == 19 ? (sbyte)127 : (sbyte)0;
            long replayed = first.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None, moveX));
            AssertThat(replayed).OverrideFailureMessage($"Restored replay diverged at frame {frame}.").IsEqual(hashes[frame]);
        }
    }

    // === Harness ===

    /// <summary>A simulation whose player one owns a launching melee Special 1.</summary>
    private static FighterSimulation LaunchedSimulation(int seed) => new(
        Loadout(LauncherModes(launches: true, hitstunFrames: 40)),
        FighterLoadout.Default(FighterCharacterID.Joan),
        seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);

    /// <summary>
    /// Fires the launching Special on tick 0 and runs until the victim lands
    /// without Block held. Returns the landing tick (the knockdown's start).
    /// </summary>
    private static int LandWithoutTech(FighterSimulation simulation) {
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent launched)).IsTrue();
        AssertThat(launched.Tumble)
            .OverrideFailureMessage("The authored launcher must put the victim into tumble.")
            .IsEqual(1);
        for (int tick = 1; tick < 120; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            simulation.TryGetFighterKnockdown(1, out FighterKnockdownComponent knockdown);
            if (FighterKnockdownRules.IsDown(in knockdown)) return tick;
        }
        AssertThat(false).OverrideFailureMessage("The tumble never landed into a knockdown.").IsTrue();
        return -1;
    }

    /// <summary>
    /// Runs player one's string until hit 2 connects and returns that tick.
    /// Both sims fed this see identical inputs up to the returned tick.
    /// </summary>
    private static int LandHitTwo(FighterSimulation simulation) {
        int hits = 0;
        int lastHP = int.MinValue;
        for (int tick = 0; tick < 200; tick++) {
            GameplayButtons attacker = tick % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(Frame(tick, attacker), Frame(tick, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (lastHP == int.MinValue) lastHP = victim.MaxHP;
            if (victim.CurrentHP < lastHP) hits++;
            lastHP = victim.CurrentHP;
            if (hits == 2) return tick;
        }
        AssertThat(false).OverrideFailureMessage($"Hit 2 never connected (hits {hits}).").IsTrue();
        return -1;
    }

    /// <summary>
    /// Probes the hit pipeline against COPIES of the live components (the
    /// simulation itself is untouched): true when a plain basic would land.
    /// </summary>
    private static bool TryDirectHit(FighterSimulation simulation) {
        simulation.TryGetFighter(0, out FighterStateComponent attacker);
        simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime);
        simulation.TryGetFighterVerb(0, out FighterVerbComponent attackerVerb);
        simulation.TryGetFighter(1, out FighterStateComponent target);
        simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent targetRuntime);
        simulation.TryGetFighterVerb(1, out FighterVerbComponent targetVerb);
        simulation.TryGetFighterDefense(1, out FighterDefenseComponent targetDefense);
        FighterTuningComponent targetTuning = default;
        return FighterDamageRules.ApplyFighterHit(
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref targetDefense, in targetTuning,
            FighterDamageRules.BasicAttackClass, 5, FP64.One, 12,
            (int)StatusType.None, 0, FP64.One, attacker.Position.x);
    }

    private static float Square(FP64 value) {
        float f = value.ToFloat();
        return f * f;
    }

    private static FighterAbilityLoadout LauncherModes(bool launches, int hitstunFrames) =>
        FighterAbilityLoadout.Default with {
            SpecialOneExecutionType = 0,
            SpecialOneHit = new FighterAbilityHitData {
                HitstunFrames = hitstunFrames,
                BlockClass = (int)BlockClass.Special,
                Launches = launches,
                Delivery = (int)HitDelivery.DirectHit,
                Origin = (int)HitOrigin.Special
            }
        };

    /// <summary>The Default kit's numbers with the given ability modes.</summary>
    private static FighterLoadout Loadout(FighterAbilityLoadout modes) => new(
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
        FP64.FromInt(4),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        modes);

    private static PlayerInputFrame Frame(int tick, GameplayButtons buttons, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = buttons,
        Pressed = buttons
    };
}
