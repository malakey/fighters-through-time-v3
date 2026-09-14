using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.1 time systems in the deterministic simulation: Resonance
/// Momentum (a connecting finisher refunds special cooldown, capped per
/// cycle), Echo Step (Block+Roll in recovery snaps ~30 frames back for 30
/// meter), Overtime (the final minute flags every fighter), and Sudden Death
/// (a true tie respawns both at 1 HP and the first KO decides it). Numbers
/// come from <see cref="BasicComboRules"/> and <c>FighterMatchSystem</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterTimeSystemsTests {

    [TestCase]
    public void MomentumRefundArithmeticRespectsTheFloorAndTheCap() {
        var runtime = new FighterRuntimeComponent {
            SpecialOneCooldownFrames = 300,
            SpecialTwoCooldownFrames = 50
        };
        var verb = new FighterVerbComponent();

        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - BasicComboRules.MomentumRefundFrames);
        AssertThat(runtime.SpecialTwoCooldownFrames)
            .OverrideFailureMessage("A refund larger than the remaining cooldown floors at zero.")
            .IsEqual(0);
        AssertThat(verb.MomentumRefundsSlotOne).IsEqual(1);
        AssertThat(verb.MomentumRefundsSlotTwo).IsEqual(1);

        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - 2 * BasicComboRules.MomentumRefundFrames);
        AssertThat(verb.MomentumRefundsSlotOne).IsEqual(2);
        // Slot two's cooldown already reached zero, so no refund is consumed.
        AssertThat(verb.MomentumRefundsSlotTwo).IsEqual(1);

        // The cap: a third connecting finisher refunds nothing on slot one.
        FighterCombatSystem.ApplyMomentumRefund(ref runtime, ref verb);
        AssertThat(runtime.SpecialOneCooldownFrames).IsEqual(300 - 2 * BasicComboRules.MomentumRefundFrames);
        AssertThat(verb.MomentumRefundsSlotOne).IsEqual(BasicComboRules.MomentumRefundCapPerCycle);
    }

    [TestCase]
    public void AConnectingFinisherRefundsARunningCooldownInMatch() {
        var simulation = NewSimulation(
            BuildCharacter("tesla", damage: 10f), BuildCharacter("joan", damage: 10f, maxHP: 400), seed: 951);

        // Arm slot one: the melee special intent fires in range and starts the
        // authored cooldown cycle.
        simulation.Advance(Frame(0, 0, GameplayButtons.Special1), Frame(0, 0, GameplayButtons.None));
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent armed)).IsTrue();
        AssertThat(armed.SpecialOneCooldownFrames > BasicComboRules.MomentumRefundFrames).IsTrue();

        // Run off the special's hitstop first (a frozen fighter consumes no
        // presses), then mash the string until three hits have landed — the
        // third is the finisher and fires exactly one refund. Alternating
        // press/release keeps a fresh press edge every other tick.
        for (int tick = 1; tick <= 10; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
        int hits = 0;
        int previousHP = before.CurrentHP;
        for (int tick = 11; tick <= 400 && hits < 3; tick++) {
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
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.MomentumRefundsSlotOne)
            .OverrideFailureMessage("The connecting finisher must consume exactly one refund.")
            .IsEqual(1);
        AssertThat(verb.MomentumRefundsSlotTwo)
            .OverrideFailureMessage("Slot two had no running cooldown — no refund is consumed.")
            .IsEqual(0);
    }

    [TestCase]
    public void EchoStepSnapsBackToThePositionThirtyFramesAgo() {
        // The opener (125 x 0.8 = 100 damage) fills the attacker's meter in
        // one connecting hit against the 400 HP opponent.
        var simulation = NewSimulation(
            BuildCharacter("tesla", damage: 125f), BuildCharacter("joan", damage: 10f, maxHP: 400), seed: 952);
        int tick = 0;
        simulation.Advance(Frame(tick++, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        for (int step = 0; step < 60; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);

        // Walk away (left) so the next swing whiffs — no hitstop to shift the
        // timeline — and the position 30 frames back is clearly distinct.
        for (int step = 0; step < 40; step++) {
            simulation.Advance(Frame(tick, -127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent beforeSwing)).IsTrue();
        FP64 walkX = beforeSwing.Position.x;

        // Whiff an opener, then the Block+Roll chord in its recovery frames.
        FTT.Combat.BasicStringProfile profile = BasicComboRules.StringProfileFor("tesla");
        simulation.Advance(Frame(tick++, 0, GameplayButtons.BasicAttack), Frame(tick - 1, 0, GameplayButtons.None));
        int recoveryTick = profile.GroundStartupFrames[0] + BasicComboRules.GroundActiveFrames[0] + 2;
        for (int step = 0; step < recoveryTick; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.Roll, GameplayButtons.Roll),
            Frame(tick - 1, 0, GameplayButtons.None));

        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent spent)).IsTrue();
        AssertThat(spent.Influence.RawValue)
            .OverrideFailureMessage("Echo Step must spend exactly 30 meter at initiation.")
            .IsEqual(FP64.FromInt(100 - BasicComboRules.EchoStepMeterCost).RawValue);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent winding)).IsTrue();
        AssertThat(winding.EchoStepWindupFrames > 0).IsTrue();
        AssertThat(winding.EchoStepCooldownFrames > 0).IsTrue();

        // The wind-up runs, then the snap: back toward where the fighter stood
        // ~30 frames earlier (before/early in the leftward walk's tail). The
        // fighter was walking left, so the snap lands to the RIGHT of where
        // the chord was pressed... but the walk ended before the swing, so the
        // snap target is simply near the whiff-swing position. Assert the snap
        // actually moved the fighter to the stashed destination.
        FP64 destX = winding.EchoStepDestX;
        for (int step = 0; step <= BasicComboRules.EchoStepWindupFrames; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent snapped)).IsTrue();
        AssertThat(snapped.Position.x.RawValue)
            .OverrideFailureMessage("The snap must land exactly on the stashed ring sample.")
            .IsEqual(destX.RawValue);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent done)).IsTrue();
        AssertThat(done.EchoStepWindupFrames).IsEqual(0);
        // The stashed sample is from the standing whiff window or the very end
        // of the walk, so it sits at (or right of) the walk's end position.
        AssertThat(snapped.Position.x >= walkX - FP64.One).IsTrue();
    }

    [TestCase]
    public void EchoStepNeverFiresDuringAGrabOrWhileThrown() {
        // V7.3: being seized cancels an armed Echo Step wind-up outright — no
        // snap, no refund, the cooldown stands — and a fighter inside any grab
        // phase refuses to start one. Player one arms a wind-up on a whiffed
        // swing away from the opponent; player two's grab connects inside the
        // 8-frame wind-up.
        var simulation = NewSimulation(
            BuildCharacter("tesla", damage: 125f), BuildCharacter("joan", damage: 10f, maxHP: 400), seed: 953);
        int tick = 0;

        // Charge player one's meter with the big opener, then let the dust settle.
        simulation.Advance(Frame(tick++, 0, GameplayButtons.BasicAttack), Frame(0, 0, GameplayButtons.None));
        for (int step = 0; step < 60; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent charged)).IsTrue();
        AssertThat(charged.Influence.RawValue).IsEqual(FP64.FromInt(100).RawValue);

        // Player one turns AWAY (the front-only swing will whiff); player two
        // walks into contact so the later grab reach holds (the pushbox stops
        // them at exactly the 0.8-unit grab reach).
        simulation.Advance(
            Frame(tick, -127, GameplayButtons.None),
            Frame(tick, -127, GameplayButtons.None));
        tick++;
        for (int step = 0; step < 90; step++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent one)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent two)).IsTrue();
            if (FP64.Abs(two.Position.x - one.Position.x)
                <= FP64.FromDouble(BasicComboRules.GrabReachUnits)) break;
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, -127, GameplayButtons.None));
            tick++;
        }
        // A settle tick bleeds player two's walk speed off before the script.
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent turned)).IsTrue();
        AssertThat(turned.FacingRight).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent closed)).IsTrue();
        AssertThat(FP64.Abs(closed.Position.x - turned.Position.x)
                <= FP64.FromDouble(BasicComboRules.GrabReachUnits))
            .OverrideFailureMessage("The fighters must stand inside grab reach before the script runs.")
            .IsTrue();

        // T+0: player one whiffs an opener leftward. T+5: player two chords a
        // grab (active window T+15..18). T+13: player one chords Echo Step in
        // the swing's recovery (wind-up T+13..21). The seize at ~T+15 lands
        // inside the wind-up.
        FTT.Combat.BasicStringProfile profile = BasicComboRules.StringProfileFor("tesla");
        int swingTick = tick;
        int grabTick = swingTick + 5;
        int chordTick = swingTick + profile.GroundStartupFrames[0] + BasicComboRules.GroundActiveFrames[0] + 1;
        bool windupArmed = false;
        bool seized = false;
        for (int step = 0; step < 40; step++, tick++) {
            GameplayButtons p1Pressed = GameplayButtons.None;
            GameplayButtons p1Held = GameplayButtons.None;
            if (tick == swingTick) { p1Pressed = GameplayButtons.BasicAttack; p1Held = GameplayButtons.BasicAttack; }
            if (tick == chordTick) {
                p1Pressed = GameplayButtons.Roll;
                p1Held = GameplayButtons.Block | GameplayButtons.Roll;
            }
            GameplayButtons p2Pressed = GameplayButtons.None;
            GameplayButtons p2Held = GameplayButtons.None;
            if (tick == grabTick) {
                p2Pressed = GameplayButtons.BasicAttack;
                p2Held = GameplayButtons.Block | GameplayButtons.BasicAttack;
            }
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, Held = p1Held, Pressed = p1Pressed },
                new PlayerInputFrame { Tick = (uint)tick, Held = p2Held, Pressed = p2Pressed });

            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
            if (tick == chordTick) {
                AssertThat(verb.EchoStepWindupFrames > 0)
                    .OverrideFailureMessage("The Echo Step wind-up must arm on the recovery chord.")
                    .IsTrue();
                windupArmed = true;
            }
            if (verb.BeingHeld == 1) { seized = true; break; }
        }
        AssertThat(windupArmed).IsTrue();
        AssertThat(seized)
            .OverrideFailureMessage("The grab must seize player one inside the wind-up.")
            .IsTrue();

        // One more tick: the cancel (which runs BEFORE the movement loop's
        // IsBusy short-circuit) clears the wind-up. No snap ever fires and the
        // cooldown (and the spent meter) stand — the read was paid for.
        simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        tick++;
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent canceled)).IsTrue();
        AssertThat(canceled.EchoStepWindupFrames)
            .OverrideFailureMessage("Entering a grab must cancel the armed wind-up.")
            .IsEqual(0);
        AssertThat(canceled.EchoStepCooldownFrames > 0)
            .OverrideFailureMessage("The cancel refunds nothing — the cooldown stands.")
            .IsTrue();
        AssertThat(canceled.BeingHeld).IsEqual(1);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent held)).IsTrue();
        AssertThat(held.Influence.RawValue)
            .OverrideFailureMessage("The spent meter is not refunded.")
            .IsEqual(FP64.FromInt(100 - BasicComboRules.EchoStepMeterCost).RawValue);
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

    [TestCase]
    public void SuddenDeathRespawnsAtOneHPAndTheFirstKnockoutDecidesIt() {
        var simulation = new FighterSimulation(matchSeconds: 1, rules: new FighterMatchRules(
            (int)MatchMode.Stock, false, 0, false, 0));
        int tick = 0;
        for (; tick < 60; tick++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }

        FighterMatchComponent match = simulation.GetMatchState();
        AssertThat(match.MatchState).IsEqual(1);
        AssertThat(match.SuddenDeathActive).IsEqual(1);
        AssertThat(match.TimerEnabled).IsEqual(0);
        for (int playerID = 0; playerID < 2; playerID++) {
            AssertThat(simulation.TryGetFighter(playerID, out FighterStateComponent fighter)).IsTrue();
            AssertThat(fighter.CurrentHP)
                .OverrideFailureMessage("Sudden Death respawns both fighters at 1 HP.")
                .IsEqual(1);
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

        // Walk player one in and land any hit: at 1 HP it is the KO, and the
        // match resolves with a real winner — no draw.
        for (int step = 0; step < 300 && simulation.GetMatchState().MatchState == 1; step++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
            sbyte toward = target.Position.x >= attacker.Position.x ? (sbyte)127 : (sbyte)-127;
            bool inRange = FP64.Abs(target.Position.x - attacker.Position.x) < FP64.FromInt(2);
            simulation.Advance(
                Frame(tick, inRange ? (sbyte)0 : toward, inRange ? GameplayButtons.BasicAttack : GameplayButtons.None),
                Frame(tick, 0, GameplayButtons.None));
            tick++;
        }

        match = simulation.GetMatchState();
        AssertThat(match.MatchState)
            .OverrideFailureMessage("The first KO must end Sudden Death.")
            .IsEqual(2);
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
