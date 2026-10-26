using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.1 time systems in the deterministic simulation: per-hit
/// Momentum (D2, 2026-10-11: every landed basic hit refunds special cooldown —
/// 20 / 20 / 60 frames — capped at 120 frames per slot per cycle), Overtime (the final minute flags every fighter), and Sudden Death
/// (a true tie respawns both at 1 HP and the first KO decides it). Numbers
/// come from <see cref="BasicComboRules"/> and <c>FighterMatchSystem</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterTimeSystemsTests {

    [TestCase]
    public void MomentumRefundArithmeticRespectsTheFloorAndTheCap() {
        int finisher = BasicComboRules.MomentumFinisherRefundFrames;
        var runtime = new FighterRuntimeComponent {
            SpecialOneCooldownFrames = 300,
            SpecialTwoCooldownFrames = 50
        };
        var verb = new FighterVerbComponent();

        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb, finisher);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - finisher);
        AssertThat(runtime.SpecialTwoCooldownFrames)
            .OverrideFailureMessage("A refund larger than the remaining cooldown floors at zero.")
            .IsEqual(0);
        AssertThat(verb.MomentumRefundedFramesSlotOne).IsEqual(finisher);
        AssertThat(verb.MomentumRefundedFramesSlotTwo)
            .OverrideFailureMessage("The cap counts the frames actually removed.")
            .IsEqual(50);

        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb, finisher);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - 2 * finisher);
        AssertThat(verb.MomentumRefundedFramesSlotOne).IsEqual(2 * finisher);
        // Slot two's cooldown already reached zero, so nothing is consumed.
        AssertThat(verb.MomentumRefundedFramesSlotTwo).IsEqual(50);

        // The cap: 120 frames per cycle, so a third finisher refunds nothing on slot one.
        AssertThat(2 * finisher).IsEqual(BasicComboRules.MomentumRefundCapFramesPerCycle);
        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb, finisher);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - 2 * finisher);
        AssertThat(verb.MomentumRefundedFramesSlotOne).IsEqual(BasicComboRules.MomentumRefundCapFramesPerCycle);
    }

    [TestCase]
    public void EveryConnectingStringHitRefundsARunningCooldownInMatch() {
        var simulation = NewSimulation(
            BuildCharacter("tesla", damage: 10f), BuildCharacter("joan", damage: 10f, maxHP: 400), seed: 951);

        // Arm slot one: the melee special intent fires in range and starts the
        // authored cooldown cycle.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
        AssertThat(armed.SpecialOneCooldownFrames > BasicComboRules.MomentumRefundCapFramesPerCycle).IsTrue();

        // Decision 1.2 (2026-10-16): the melee Special lands on its active
        // entry and its cast holds every input until its recovery ends, so run
        // the cast and the special's hitstop off first (a frozen fighter
        // consumes no presses), then mash the string until three hits have
        // landed — every landed hit refunds (D2): 20 + 20 + the finisher's 60.
        // Alternating press/release keeps a fresh press edge every other tick.
        int castEnd = SpecialCastTestKit.StepUntilFired(simulation, 0, 1);
        castEnd = SpecialCastTestKit.StepUntilCastEnds(simulation, 0, castEnd);
        for (int tick = castEnd; tick < castEnd + 10; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hits = 0;
        int previousHP = before.CurrentHP;
        for (int tick = castEnd + 10; tick <= castEnd + 400 && hits < 3; tick++) {
            // The special's knockback pushed the victim away — chase while
            // mashing (held movement steers a swing without cancelling it).
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent chaser)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent prey)).IsTrue();
            sbyte toward = prey.Position.x >= chaser.Position.x ? (sbyte)127 : (sbyte)-127;
            GameplayButtons buttons = tick % 2 == 0 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(Frame(tick, toward, buttons), Frame(tick, 0, GameplayButtons.None));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            if (target.CurrentHP < previousHP) hits++;
            previousHP = target.CurrentHP;
        }
        AssertThat(hits).IsEqual(3);
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent after)).IsTrue();
        AssertThat(after.SpecialOneCooldownFrames > 0)
            .OverrideFailureMessage("Harness: slot one must still be cooling, so every grant was its full nominal.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.MomentumRefundedFramesSlotOne)
            .OverrideFailureMessage("Three landed string hits must refund 20 + 20 + 60 frames.")
            .IsEqual(2 * BasicComboRules.MomentumStringHitRefundFrames + BasicComboRules.MomentumFinisherRefundFrames);
        AssertThat(verb.MomentumRefundedFramesSlotTwo)
            .OverrideFailureMessage("Slot two had no running cooldown — no refund is consumed.")
            .IsEqual(0);
    }

    [TestCase]
    public void OvertimeFlagsBothFightersInsideTheFinalMinuteOnly() {
        // A 5-second match is inside the final minute from its first live
        // frame; an untimed match never flags.
        var timed = new FighterSimulation(matchSeconds: 5, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        var untimed = new FighterSimulation(matchSeconds: 0, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        for (int tick = 0; tick < 5; tick++) {
            timed.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            untimed.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(timed.TryGetFighterVerb(playerID, out FighterVerbComponent hot)).IsTrue();
            AssertThat(hot.OvertimeActive)
                .OverrideFailureMessage($"Player {playerID} must be flagged inside the final minute.")
                .IsEqual(1);
            AssertThat(untimed.TryGetFighterVerb(playerID, out FighterVerbComponent cold)).IsTrue();
            AssertThat(cold.OvertimeActive).IsEqual(0);
        }
    }

    /// <summary>
    /// F22 (Package 11 A1c) replaced the V7.1 rule this case used to pin. Two
    /// untouched fighters tie at the buzzer and enter Sudden Death at their
    /// <b>real HP</b> — the 1-HP clamp is retired, so the decider is the next
    /// actual death rather than the first hit. Defy's spent flag is preserved
    /// rather than pre-marked used: the phase disable is derived from
    /// <c>SuddenDeathActive</c>, so entry consumes no use.
    /// </summary>
    [TestCase]
    public void SuddenDeathStartsAtRealHPAndTheNextDeathDecidesIt() {
        var simulation = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        int tick = 0;
        for (; tick < 60; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.SuddenDeathActive).IsEqual(1);
        AssertThat(match.TimerEnabled).IsEqual(0);
        // Entry runs the shared ready countdown; the match is not Complete.
        AssertThat(match.MatchState).IsNotEqual(FighterMatchStates.Complete);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
            AssertThat(fighter.CurrentHP)
                .OverrideFailureMessage("F22: a living fighter keeps its real HP — the 1-HP clamp is retired.")
                .IsEqual(fighter.MaxHP);
            AssertThat(fighter.Position.x.RawValue).IsEqual(fighter.SpawnPosition.x.RawValue);
            // V7.6 F22/F13 (Package 11 A1b): Defy is BARRED in Sudden Death
            // rather than pre-marked SPENT - the seal must be able to show
            // "unavailable in this context" without overwriting the underlying
            // spent flag, which the old pre-mark conflated.
            AssertThat(simulation.TryGetFighterVerb(playerID, out FighterVerbComponent verb)).IsTrue();
            AssertThat(verb.DefyHistoryUsed)
                .OverrideFailureMessage("Sudden Death must not mark an unused Defy as spent.")
                .IsEqual(0);
            AssertThat(simulation.TryGetFighterDefense(playerID, out FighterDefenseComponent defense)).IsTrue();
            AssertThat(defense.DefyBarred)
                .OverrideFailureMessage("Defy History is disabled during Sudden Death.")
                .IsEqual(1);
        }

        // The ready countdown freezes controls for both players and discards their
        // input, so the fall has to start after play resumes — an input spent
        // during the countdown is simply dropped.
        for (int step = 0; step < FighterMatchFlowRules.CountdownFrames + 2
            && simulation.GetMatchState().MatchState != FighterMatchStates.InProgress; step++) {
            simulation.Advance(
                Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.GetMatchState().MatchState).IsEqual(FighterMatchStates.InProgress);

        // One decisive life each: player two rides the floor out of the stage and
        // the real death — not a first hit — resolves the match.
        for (int step = 0; step < 600 && simulation.GetMatchState().MatchState != FighterMatchStates.Complete; step++) {
            PlayerInputFrame falling = step == 0
                ? Frame(tick, 0, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, 0, GameplayButtons.Down);
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), falling);
            tick++;
        }

        match = simulation.GetMatchState();
        AssertThat(match.MatchState)
            .OverrideFailureMessage("The next actual death must end Sudden Death.")
            .IsEqual(FighterMatchStates.Complete);
        AssertThat(match.WinnerPlayerID).IsEqual(0);
        AssertThat(match.IsTrueTie).IsEqual(0);
    }

    // ---- Harness -------------------------------------------------------------

    private static FighterSimulation NewSimulation(CharacterData playerOne, CharacterData playerTwo, int seed) => new(
        FighterLoadoutFactory.FromCharacterData(playerOne),
        FighterLoadoutFactory.FromCharacterData(playerTwo),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildCharacter(string characterID, float damage, int maxHP = 400) => new() {
        CharacterID = characterID,
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = damage,
        BasicAttackKnockback = 0f,
        SpecialAttackOne = new AbilityData { BaseDamage = 5f, CooldownDuration = 8f },
        SpecialAttackTwo = new AbilityData { BaseDamage = 5f, CooldownDuration = 8f },
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = 0,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame FrameChord(int tick, GameplayButtons held, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = pressed
    };
}
