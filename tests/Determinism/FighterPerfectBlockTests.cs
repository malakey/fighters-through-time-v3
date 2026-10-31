using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-BLOCK (combat and movement update 2026-10-16, decision 3.4 with the Q3
/// ruling; ledger <c>USER-PERFECT-BLOCK</c> / <c>PROVISIONAL-PERFECT-BLOCK-RULES</c>):
/// the Fighter perfect block. A block raised within five ticks before a contact
/// (contact ages 0–4 from the first live stance tick) spends no charge and sets
/// no shieldstun, keeping the 2-frame blocked hitstop; the window arms only on a
/// fresh raise with a Block press after the stance was down at least 20 ticks
/// (or never raised this stock). A Shield-Breaker still shatters. The clock is
/// component 323's <c>BlockTiming</c> (<see cref="FighterBlockTimingRules"/>).
/// Edge-reading rules are pinned with <see cref="PlayerInputFrame.Create"/>-built
/// input as well as hand-built frames (plan §1.8). Pure C#: hand-built default
/// loadouts touch no engine API.
/// </summary>
[TestSuite]
public class FighterPerfectBlockTests {

    // === 1. The window ========================================================

    [TestCase]
    public void ARaiseZeroToFourTicksBeforeContactSpendsNothingAndFiveIsOrdinary() {
        int contact = FindOpenerContactTick(out int damage);
        for (int lead = 0; lead <= 6; lead++) {
            var simulation = NewAdjacentSimulation();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent start)).IsTrue();
            var input = new Inputs();
            for (int tick = 0; tick <= contact; tick++) {
                input.Advance(simulation,
                    one: tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None,
                    two: tick >= contact - lead ? GameplayButtons.Block : GameplayButtons.None);
            }
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocker)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent action)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent attacker)).IsTrue();
            AssertThat(blocker.CurrentHP)
                .OverrideFailureMessage($"Lead {lead}: the opener must be blocked, perfect or not.")
                .IsEqual(start.CurrentHP);
            AssertThat(blocker.HitstunFrames).IsEqual(0);
            AssertThat(attacker.SwingLanded)
                .OverrideFailureMessage($"Lead {lead}: a block is never a D1a landing (no confirm, no Momentum).")
                .IsEqual(0);
            if (lead <= 4) {
                AssertThat(blocker.BlockCharges)
                    .OverrideFailureMessage($"A raise {lead} tick(s) before contact is perfect: no charge.")
                    .IsEqual(start.BlockCharges);
                AssertThat(verb.ShieldStunFrames)
                    .OverrideFailureMessage($"A perfect block (lead {lead}) sets no shieldstun.")
                    .IsEqual(0);
                AssertThat(verb.HitstopFrames)
                    .OverrideFailureMessage("A perfect block keeps the 2-frame blocked hitstop.")
                    .IsEqual(BasicComboRules.BlockedHitstopFrames);
                AssertThat(FighterBlockTimingRules.PerfectBlockCount(in action)).IsEqual(1);
            } else {
                AssertThat(blocker.BlockCharges)
                    .OverrideFailureMessage($"A raise {lead} ticks before contact is an ordinary block.")
                    .IsEqual(start.BlockCharges - 1);
                AssertThat(verb.ShieldStunFrames).IsEqual(BasicComboRules.ShieldstunFramesFor(damage));
                AssertThat(FighterBlockTimingRules.PerfectBlockCount(in action)).IsEqual(0);
            }
        }
    }

    // === 2. Raises that never arm =============================================

    [TestCase]
    public void AHeldRaiseAnEscapeRaisedStanceAndAPressInShieldstunNeverArm() {
        // A stance that rises while Block is merely held: Joan swings, holds
        // Block from her startup, and her recovery's Block cancel raises the
        // stance with no press on that tick.
        var held = NewAdjacentSimulation(spawnDistance: 6);
        var input = new Inputs();
        input.Advance(held, two: GameplayButtons.BasicAttack);
        bool rose = false;
        for (int step = 0; step < 60 && !rose; step++) {
            input.Advance(held, two: GameplayButtons.Block);
            rose = IsBlockStance(held, 1);
        }
        AssertThat(rose).OverrideFailureMessage("Harness: the held Block never raised the stance.").IsTrue();
        AssertThat(Window(held, 1))
            .OverrideFailureMessage("A stance raised with Block only held must not arm the window.")
            .IsEqual(0);

        // The hit-2 Block escape, even with Block pressed fresh on the escape
        // tick. A zero-knockback attacker keeps the victim grounded and in
        // reach (the FighterBasicStringParityTests escape harness); it presses
        // the opener from rest and hit 2 from the chain hold, then stops, so no
        // finisher lands on the escape tick.
        var escape = new FighterSimulation(
            ZeroKnockback(FighterCharacterID.Einstein), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 4401, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        var escapeInput = new Inputs();
        AssertThat(escape.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hits = 0;
        int lastHP = before.CurrentHP;
        bool escaped = false;
        for (int tick = 0; tick < 150 && !escaped; tick++) {
            AssertThat(escape.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            AssertThat(escape.TryGetFighterVerb(1, out FighterVerbComponent victimVerb)).IsTrue();
            AssertThat(escape.TryGetFighterRuntime(0, out FighterRuntimeComponent swinger)).IsTrue();
            bool swing = hits == 0
                ? swinger.AttackPhase == FighterBasicAttackRules.PhaseNone
                : hits == 1 && swinger.AttackPhase == FighterBasicAttackRules.PhaseChainHold;
            // Hit 2 opened the gate; the first unfrozen tick whose hitstun
            // outlives this tick's decrement is a Block-escape tick.
            bool escapeTick = hits >= 2 && victimVerb.HitstopFrames == 0 && victim.HitstunFrames > 1;
            escapeInput.Advance(escape,
                one: swing ? GameplayButtons.BasicAttack : GameplayButtons.None,
                two: escapeTick ? GameplayButtons.Block : GameplayButtons.None);
            AssertThat(escape.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP < lastHP) {
                hits++;
                lastHP = after.CurrentHP;
            }
            if (escapeTick) {
                AssertThat(after.HitstunFrames)
                    .OverrideFailureMessage("Harness: the fresh Block press must escape hitstun.")
                    .IsEqual(0);
                AssertThat(IsBlockStance(escape, 1)).IsTrue();
                escaped = true;
            }
        }
        AssertThat(escaped).OverrideFailureMessage("Harness: string hit 2 never opened a Block escape.").IsTrue();
        AssertThat(Window(escape, 1))
            .OverrideFailureMessage("A stance raised by the hit-2 escape never arms, even with a fresh press.")
            .IsEqual(0);

        // A press during shieldstun: the stance never came down, so it never rose.
        int contact = FindOpenerContactTick(out _);
        var stunned = NewAdjacentSimulation();
        var stunnedInput = new Inputs();
        for (int tick = 0; tick <= contact; tick++) {
            stunnedInput.Advance(stunned,
                one: tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None,
                two: GameplayButtons.Block);
        }
        AssertThat(stunned.TryGetFighterVerb(1, out FighterVerbComponent blockVerb)).IsTrue();
        AssertThat(blockVerb.ShieldStunFrames)
            .OverrideFailureMessage("Harness: the opener must be an ordinary block with shieldstun.")
            .IsGreater(0);
        bool pressedInStun = false;
        for (int step = 0; step < 8 && !pressedInStun; step++) {
            AssertThat(stunned.TryGetFighterVerb(1, out FighterVerbComponent stepVerb)).IsTrue();
            if (stepVerb.HitstopFrames == 0 && stepVerb.ShieldStunFrames > 1) {
                stunnedInput.Advance(stunned, two: GameplayButtons.Block);
                pressedInStun = true;
            } else {
                stunnedInput.Advance(stunned);
            }
        }
        AssertThat(pressedInStun).OverrideFailureMessage("Harness: no unfrozen shieldstun tick to press on.").IsTrue();
        AssertThat(IsBlockStance(stunned, 1)).IsTrue();
        AssertThat(Window(stunned, 1))
            .OverrideFailureMessage("A Block press inside shieldstun must not arm the window.")
            .IsEqual(0);
    }

    /// <summary>
    /// CM-FIXB-SIM (B5): a freshly raised stance that a hit from behind breaks
    /// counts as dropped, so the hit-2 Block escape on the very next tick — the
    /// stance back up with Block still held — raises it on the escape tick,
    /// which never arms, and the window armed before the hit is gone: a front
    /// contact right after is an ordinary block (a charge, shieldstun), as in
    /// Story. The movement system's per-tick clock calls, in its order, around a
    /// real contact through the production pipeline: a zero-knockback,
    /// block-cancelable construct bite from behind (hitstun 10, no hitstop),
    /// which the raised stance does not absorb.
    /// </summary>
    [TestCase]
    public void AStanceAHitFromBehindBreaksKeepsNoWindowThroughTheHitTwoEscape() {
        FighterStateComponent blocker = Blocker();
        FighterRuntimeComponent runtime = new() { HeldButtons = (int)GameplayButtons.Block };
        FighterVerbComponent verb = default;
        FighterDefenseComponent defense = default;
        FighterTuningComponent tuning = default;
        FighterActionStateComponent action = default;
        FighterStateComponent attacker = new() { PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1 };
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;

        // Tick A: a fresh raise with a Block press arms the window.
        FighterBlockTimingRules.BeginTick(ref action, in blocker);
        FighterBlockTimingRules.Observe(ref action, FighterBasicAttackRules.IsBlockStance(in blocker, in runtime, in verb), blockPressed: true);
        AssertThat(FighterBlockTimingRules.WindowFrames(in action))
            .OverrideFailureMessage("Harness: the fresh raise must arm the window.")
            .IsEqual(BasicComboRules.PerfectBlockWindowFrames);

        // Tick A+1: the stance is observed up, then the combat pass lands the
        // bite from behind (the blocker faces right; the bite is on its left).
        FighterBlockTimingRules.BeginTick(ref action, in blocker);
        FighterBlockTimingRules.Observe(ref action, FighterBasicAttackRules.IsBlockStance(in blocker, in runtime, in verb), blockPressed: false);
        FighterHitOutcome bite = FighterDamageRules.ResolveFighterHit(
            ref action,
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref blocker, ref runtime, ref verb, ref defense, in tuning,
            FighterDamageRules.BasicAttackClass, 4, FP64.Zero, 10,
            (int)StatusType.None, 0, FP64.One,
            blocker.Position.x - FP64.One,
            collectsEcho: false, appliesHitstop: false, launches: false);
        AssertThat((int)bite)
            .OverrideFailureMessage("Harness: a hit from behind is not absorbed by the stance.")
            .IsEqual((int)FighterHitOutcome.Landed);
        AssertThat(blocker.HitstunFrames).IsEqual(10);
        AssertThat(verb.HitstunBlockCancelBlocked).IsEqual(0);

        // Tick A+2: the first tick after the hit, Block still held — the escape.
        FighterBlockTimingRules.BeginTick(ref action, in blocker);
        if (BasicComboRules.CanBlockEscapeHitstun(
                grounded: blocker.IsGrounded != 0,
                blockHeld: (runtime.HeldButtons & (int)GameplayButtons.Block) != 0,
                tumbling: verb.Tumble != 0,
                stringHitOneGate: verb.HitstunBlockCancelBlocked != 0,
                stanceCanRise: blocker.BlockCharges > 0 && verb.BlockLockoutFrames <= 0)) {
            blocker.HitstunFrames = 0;
        }
        bool stance = FighterBasicAttackRules.IsBlockStance(in blocker, in runtime, in verb);
        FighterBlockTimingRules.Observe(ref action, stance, blockPressed: false);
        AssertThat(stance).OverrideFailureMessage("Harness: the held Block must escape the bite's hitstun.").IsTrue();
        AssertThat(FighterBlockTimingRules.WindowFrames(in action))
            .OverrideFailureMessage("The window armed before the hit must not survive the stance the hit broke.")
            .IsEqual(0);

        // Tick A+3: a front contact is an ordinary block.
        FighterBlockTimingRules.BeginTick(ref action, in blocker);
        FighterBlockTimingRules.Observe(ref action, FighterBasicAttackRules.IsBlockStance(in blocker, in runtime, in verb), blockPressed: false);
        FighterHitOutcome front = FighterDamageRules.ResolveFighterHit(
            ref action,
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref blocker, ref runtime, ref verb, ref defense, in tuning,
            FighterDamageRules.BasicAttackClass, ContactDamage, FP64.FromInt(4), 18,
            (int)StatusType.None, 0, FP64.One,
            blocker.Position.x + FP64.One);
        AssertThat((int)front).IsEqual((int)FighterHitOutcome.Blocked);
        AssertThat(blocker.BlockCharges)
            .OverrideFailureMessage("A front contact after the escape is an ordinary block: one charge.")
            .IsEqual(2);
        AssertThat(verb.ShieldStunFrames).IsEqual(BasicComboRules.ShieldstunFramesFor(ContactDamage));
        AssertThat(FighterBlockTimingRules.PerfectBlockCount(in action)).IsEqual(0);
    }

    // === 3. The anti-mash rearm ===============================================

    [TestCase]
    public void AReRaiseInsideTwentyFramesIsOrdinary() {
        foreach (int lowered in new[] { BasicComboRules.PerfectBlockRearmFrames - 1, BasicComboRules.PerfectBlockRearmFrames }) {
            var simulation = NewAdjacentSimulation(spawnDistance: 6);
            var input = new Inputs();
            for (int tick = 0; tick < 10; tick++) input.Advance(simulation, two: GameplayButtons.Block);
            AssertThat(IsBlockStance(simulation, 1)).IsTrue();
            for (int tick = 0; tick < lowered; tick++) input.Advance(simulation);
            AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent down)).IsTrue();
            AssertThat(FighterBlockTimingRules.LoweredFrames(in down)).IsEqual(lowered);
            input.Advance(simulation, two: GameplayButtons.Block);
            AssertThat(IsBlockStance(simulation, 1)).IsTrue();
            bool armed = lowered >= BasicComboRules.PerfectBlockRearmFrames;
            AssertThat(Window(simulation, 1))
                .OverrideFailureMessage($"A re-raise after {lowered} lowered ticks must {(armed ? "" : "not ")}arm.")
                .IsEqual(armed ? BasicComboRules.PerfectBlockWindowFrames : 0);
        }
    }

    // === 4. Streams and a fresh stock =========================================

    [TestCase]
    public void APressedEqualsHeldStreamArmsOnceAndAFreshStockIsEligibleAtOnce() {
        // Hand-built frames (Pressed = Held = Block on every tick) and real
        // edges (PlayerInputFrame.Create) both arm once, on the first stance tick.
        foreach (bool handBuilt in new[] { true, false }) {
            var simulation = NewAdjacentSimulation(spawnDistance: 6);
            var input = new Inputs { HandBuilt = handBuilt };
            for (int tick = 0; tick < 30; tick++) {
                input.Advance(simulation, two: GameplayButtons.Block);
                int expected = tick < BasicComboRules.PerfectBlockWindowFrames
                    ? BasicComboRules.PerfectBlockWindowFrames - tick
                    : 0;
                AssertThat(Window(simulation, 1))
                    .OverrideFailureMessage($"{(handBuilt ? "Hand-built" : "Create-built")} tick {tick}: one arming only.")
                    .IsEqual(expected);
            }
        }

        // A fresh stock: component 323 is zeroed on the respawn platform, so the
        // first raise after the drop arms even right after one before the fall.
        var falling = new FighterSimulation(stocks: 3, rules: FighterMatchRules.Disabled);
        var fallingInput = new Inputs();
        for (int tick = 0; tick < 4; tick++) fallingInput.Advance(falling, two: GameplayButtons.Block);
        AssertThat(falling.TryGetFighterRuntime(1, out FighterRuntimeComponent alive)).IsTrue();
        int knockouts = alive.KnockoutsSuffered;
        fallingInput.Advance(falling, two: GameplayButtons.Jump | GameplayButtons.Down);
        bool fell = false;
        for (int tick = 0; tick < 400 && !fell; tick++) {
            fallingInput.Advance(falling, two: GameplayButtons.Down);
            AssertThat(falling.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)).IsTrue();
            fell = runtime.KnockoutsSuffered > knockouts;
        }
        AssertThat(fell).OverrideFailureMessage("Harness: the drop never reached the blast zone.").IsTrue();
        bool landed = false;
        for (int tick = 0; tick < 900 && !landed; tick++) {
            fallingInput.Advance(falling);
            AssertThat(falling.TryGetFighter(1, out FighterStateComponent respawned)).IsTrue();
            landed = !FighterMatchFlowRules.IsOnRespawnPlatform(in respawned) && respawned.IsGrounded != 0
                && respawned.RespawnFramesRemaining <= 0;
        }
        AssertThat(landed).OverrideFailureMessage("Harness: the fighter never left the platform and landed.").IsTrue();
        AssertThat(falling.TryGetFighterActionState(1, out FighterActionStateComponent fresh)).IsTrue();
        AssertThat(FighterBlockTimingRules.LoweredFrames(in fresh))
            .OverrideFailureMessage("A fresh stock has never raised the stance.")
            .IsEqual(0);
        fallingInput.Advance(falling, two: GameplayButtons.Block);
        AssertThat(IsBlockStance(falling, 1)).IsTrue();
        AssertThat(Window(falling, 1))
            .OverrideFailureMessage("The first raise of a fresh stock arms at once.")
            .IsEqual(BasicComboRules.PerfectBlockWindowFrames);
    }

    // === 5. Shield-Breakers and Guard-Crush ===================================

    [TestCase]
    public void AShieldBreakerInsideTheWindowStillShattersButAnOrdinarySpecialIsPerfect() {
        // Q3: the window does not stop a Shield-Breaker's full shatter.
        Contact(open: true, FighterDamageRules.SpecialAttackClass, shieldBreaker: true, blockChargeCost: 0,
            out FighterStateComponent breaker, out FighterVerbComponent breakerVerb,
            out FighterActionStateComponent breakerAction, out FighterHitOutcome breakerOutcome);
        AssertThat((int)breakerOutcome).IsEqual((int)FighterHitOutcome.Blocked);
        AssertThat(breaker.BlockCharges).IsEqual(0);
        AssertThat(breaker.DazeFrames).IsEqual(60);
        AssertThat(breakerVerb.BlockLockoutFrames).IsEqual(BasicComboRules.BlockShatterLockoutFrames);
        AssertThat(FighterBlockTimingRules.PerfectBlockCount(in breakerAction)).IsEqual(0);

        // An ordinary Special inside the window spends neither of its two charges.
        Contact(open: true, FighterDamageRules.SpecialAttackClass, shieldBreaker: false, blockChargeCost: 0,
            out FighterStateComponent special, out FighterVerbComponent specialVerb,
            out FighterActionStateComponent specialAction, out FighterHitOutcome specialOutcome);
        AssertThat((int)specialOutcome).IsEqual((int)FighterHitOutcome.Blocked);
        AssertThat(special.BlockCharges).IsEqual(3);
        AssertThat(specialVerb.ShieldStunFrames).IsEqual(0);
        AssertThat(specialVerb.HitstopFrames).IsEqual(BasicComboRules.BlockedHitstopFrames);
        AssertThat(FighterBlockTimingRules.PerfectBlockCount(in specialAction)).IsEqual(1);

        // The same Special outside the window is ordinary (two charges, scaled shieldstun).
        Contact(open: false, FighterDamageRules.SpecialAttackClass, shieldBreaker: false, blockChargeCost: 0,
            out FighterStateComponent closed, out FighterVerbComponent closedVerb, out _, out _);
        AssertThat(closed.BlockCharges).IsEqual(1);
        AssertThat(closedVerb.ShieldStunFrames).IsEqual(BasicComboRules.ShieldstunFramesFor(ContactDamage));

        // An authored Guard-Crush cost keeps its effect inside the window.
        Contact(open: true, FighterDamageRules.BasicAttackClass, shieldBreaker: false,
            blockChargeCost: BasicComboRules.GuardCrushBlockChargeCost,
            out FighterStateComponent crushed, out FighterVerbComponent crushedVerb, out _, out _);
        AssertThat(crushed.BlockCharges).IsEqual(1);
        AssertThat(crushedVerb.ShieldStunFrames).IsEqual(BasicComboRules.ShieldstunFramesFor(ContactDamage));
    }

    // === 6. Hazards ============================================================

    [TestCase]
    public void AHazardTickInsideTheWindowIsPerfectBlocked() {
        foreach (bool open in new[] { true, false }) {
            FighterStateComponent target = Blocker();
            FighterRuntimeComponent runtime = new() { HeldButtons = (int)GameplayButtons.Block };
            FighterVerbComponent verb = default;
            FighterDefenseComponent defense = default;
            FighterTuningComponent tuning = default;
            FighterActionStateComponent action = WindowState(open);
            bool landed = FighterDamageRules.ApplyEnvironmentHit(
                ref action, ref target, ref runtime, ref verb, ref defense, in tuning,
                ContactDamage, FP64.FromInt(2), 12, target.Position.x + FP64.One,
                (int)StatusType.None, 0, FP64.One);
            AssertThat(landed).IsFalse();
            AssertThat(target.CurrentHP).IsEqual(100);
            AssertThat(target.BlockCharges)
                .OverrideFailureMessage(open
                    ? "A hazard tick inside the window is perfect-blocked: no charge."
                    : "A hazard tick outside the window costs its one charge (D03c).")
                .IsEqual(open ? 3 : 2);
            AssertThat(verb.ShieldStunFrames).IsEqual(open ? 0 : BasicComboRules.ShieldstunFramesFor(ContactDamage));
            AssertThat(verb.HitstopFrames)
                .OverrideFailureMessage("Hazard ticks carry no hitstop, perfect or not.")
                .IsEqual(0);
            AssertThat(FighterBlockTimingRules.PerfectBlockCount(in action)).IsEqual(open ? 1 : 0);
        }
    }

    // === 7. Rollback ===========================================================

    [TestCase]
    public void ASnapshotInsideTheWindowReplaysThePerfectBlock() {
        int contact = FindOpenerContactTick(out _);
        var simulation = NewAdjacentSimulation();
        var input = new Inputs();
        int raise = contact - 2;
        for (int tick = 0; tick < contact; tick++) {
            input.Advance(simulation,
                one: tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None,
                two: tick >= raise ? GameplayButtons.Block : GameplayButtons.None);
        }
        AssertThat(Window(simulation, 1)).IsGreater(0);
        byte[] snapshot = simulation.CaptureFullState();
        long hashAtCapture = simulation.CurrentHash;
        Inputs replayFrom = input.Copy();

        RunPast(simulation, input, 12);
        long firstHash = simulation.CurrentHash;
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent first)).IsTrue();
        AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent firstAction)).IsTrue();
        AssertThat(FighterBlockTimingRules.PerfectBlockCount(in firstAction)).IsEqual(1);

        simulation.RestoreFullState(snapshot);
        AssertThat(simulation.CurrentHash).IsEqual(hashAtCapture);
        RunPast(simulation, replayFrom, 12);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent second)).IsTrue();
        AssertThat(simulation.TryGetFighterActionState(1, out FighterActionStateComponent secondAction)).IsTrue();
        AssertThat(second.BlockCharges).IsEqual(first.BlockCharges);
        AssertThat(secondAction.BlockTiming).IsEqual(firstAction.BlockTiming);
        AssertThat(simulation.CurrentHash)
            .OverrideFailureMessage("A replay from inside the window must reproduce the perfect block exactly.")
            .IsEqual(firstHash);
    }

    // === Harness ================================================================

    private const int ContactDamage = 12;

    /// <summary>Per-player previous-held tracking, so frames can be <see cref="PlayerInputFrame.Create"/>-built.</summary>
    private sealed class Inputs {
        public bool HandBuilt;
        private GameplayButtons _previousOne;
        private GameplayButtons _previousTwo;

        public void Advance(FighterSimulation simulation,
            GameplayButtons one = GameplayButtons.None, GameplayButtons two = GameplayButtons.None) {
            uint tick = (uint)simulation.CurrentTick;
            simulation.Advance(Build(tick, one, ref _previousOne), Build(tick, two, ref _previousTwo));
        }

        public Inputs Copy() => new() { HandBuilt = HandBuilt, _previousOne = _previousOne, _previousTwo = _previousTwo };

        private PlayerInputFrame Build(uint tick, GameplayButtons held, ref GameplayButtons previous) {
            PlayerInputFrame frame = HandBuilt
                ? new PlayerInputFrame { Tick = tick, Held = held, Pressed = held }
                : PlayerInputFrame.Create(tick, 0f, 0f, held, previous);
            previous = held;
            return frame;
        }
    }

    /// <summary>The default kit of <paramref name="id"/> with no basic knockback, so a string keeps its victim in place.</summary>
    private static FighterLoadout ZeroKnockback(FighterCharacterID id) {
        FighterLoadout baseline = FighterLoadout.Default(id);
        return new FighterLoadout(
            baseline.CharacterID, baseline.MaxHP, baseline.MaxBlockCharges, baseline.MaxJumpCount,
            baseline.BasicDamage, baseline.SpecialOneDamage, baseline.SpecialTwoDamage, baseline.UltimateDamage,
            baseline.SpecialOneStatusType, baseline.SpecialOneStatusFrames,
            baseline.SpecialTwoStatusType, baseline.SpecialTwoStatusFrames,
            baseline.UltimateStatusType, baseline.UltimateStatusFrames,
            baseline.Weight, baseline.MoveSpeed, baseline.JumpSpeed,
            FP64.Zero, baseline.SpecialOneKnockback, baseline.SpecialTwoKnockback, baseline.UltimateKnockback,
            baseline.SpecialOneStatusIntensity, baseline.SpecialTwoStatusIntensity, baseline.UltimateStatusIntensity,
            baseline.AbilityModes, baseline.AirControl);
    }

    /// <summary>Einstein (player one) swings at Joan (player two), one unit apart on the flat arena.</summary>
    private static FighterSimulation NewAdjacentSimulation(int spawnDistance = 1) =>
        new(seed: 4401, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);

    /// <summary>
    /// The tick (counting the press as tick 0) on which the player-one opener
    /// first damages an open player two, and the damage it carries.
    /// </summary>
    private static int FindOpenerContactTick(out int damage) {
        var open = NewAdjacentSimulation();
        var input = new Inputs();
        AssertThat(open.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        for (int tick = 0; tick < 20; tick++) {
            input.Advance(open, one: tick == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None);
            AssertThat(open.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP < before.CurrentHP) {
                damage = before.CurrentHP - after.CurrentHP;
                AssertThat(tick)
                    .OverrideFailureMessage("Harness: the opener must take at least six ticks so every lead is reachable.")
                    .IsGreaterEqual(6);
                return tick;
            }
        }
        AssertThat(false).OverrideFailureMessage("Harness: the opener never reached the open target.").IsTrue();
        damage = 0;
        return 0;
    }

    private static void RunPast(FighterSimulation simulation, Inputs input, int ticks) {
        for (int tick = 0; tick < ticks; tick++) input.Advance(simulation, two: GameplayButtons.Block);
    }

    private static int Window(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)).IsTrue();
        return FighterBlockTimingRules.WindowFrames(in action);
    }

    private static bool IsBlockStance(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(playerID, out FighterRuntimeComponent runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
        return FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb);
    }

    /// <summary>A grounded, right-facing blocker on three charges at the origin.</summary>
    private static FighterStateComponent Blocker() => new() {
        PlayerID = 1,
        Stocks = 3,
        MaxHP = 100,
        CurrentHP = 100,
        BlockCharges = 3,
        IsGrounded = 1,
        FacingRight = 1
    };

    /// <summary>Component 323 with the stance up and the window open (2 frames left) or closed.</summary>
    private static FighterActionStateComponent WindowState(bool open) => new() {
        BlockTiming = FighterBlockTimingRules.StanceUpBit | FighterBlockTimingRules.ObservedBit | (open ? 2 : 0)
            | (open ? 0 : 30 << FighterBlockTimingRules.LoweredShift)
    };

    /// <summary>One contact on <see cref="Blocker"/> through the production pipeline overload.</summary>
    private static void Contact(
        bool open, int attackClass, bool shieldBreaker, int blockChargeCost,
        out FighterStateComponent target, out FighterVerbComponent targetVerb,
        out FighterActionStateComponent targetAction, out FighterHitOutcome outcome) {
        FighterStateComponent attacker = new() { PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100, IsGrounded = 1 };
        FighterRuntimeComponent attackerRuntime = default;
        FighterVerbComponent attackerVerb = default;
        target = Blocker();
        FighterRuntimeComponent targetRuntime = new() { HeldButtons = (int)GameplayButtons.Block };
        targetVerb = default;
        FighterDefenseComponent defense = default;
        FighterTuningComponent tuning = default;
        targetAction = WindowState(open);
        AssertThat(FighterBasicAttackRules.IsBlockStance(in target, in targetRuntime, in targetVerb)).IsTrue();
        outcome = FighterDamageRules.ResolveFighterHit(
            ref targetAction,
            ref attacker, ref attackerRuntime, ref attackerVerb,
            ref target, ref targetRuntime, ref targetVerb, ref defense, in tuning,
            attackClass, ContactDamage, FP64.FromInt(4), 18,
            (int)StatusType.None, 0, FP64.One,
            target.Position.x + FP64.One,
            blockChargeCost: blockChargeCost,
            shieldBreaker: shieldBreaker);
    }
}
