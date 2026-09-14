using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.6 F14 Option A (Package 11 A7a) — the Eraser's Siphon Snare, transcribed
/// from the design's validation matrix.
///
/// <para>The whole mechanic is <b>cast → attachment check → channel</b>: no
/// hitbox, no HP damage, no hitstun, knockback, status slot, Rally echo or
/// hitstop. What it takes is Influence, at 10 points per live second, capped at
/// 30 per cast and 3 s of tether, and every break condition is evaluated
/// <i>before</i> the next drain tick.</para>
///
/// <para>These cases drive the executor and
/// <see cref="SiphonTetherChannel.TickChannel"/> directly rather than waiting on
/// physics frames, which is what keeps them deterministic headless — the same
/// reason the production attachment check sweeps the <c>"Players"</c> group by
/// distance instead of trusting an overlap flush.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SiphonSnareTests {
    private const float Step = 1f / 60f;
    private const string EraserID = "unbound_eraser";
    private const string AbilityPath =
        "res://resources/Enemies/Abilities/unbound_eraser/siphon_snare.tres";

    // === The authored contract ===

    [TestCase]
    public void TheAuthoredSnareCarriesTheF14NumbersAndNoDamageOfAnyKind() {
        var ability = AuthoredResources.Load<EnemyAbilityData>(AbilityPath);
        AssertObject(ability).IsNotNull();
        AssertThat(ability.Archetype).IsEqual(EnemyAbilityArchetype.SiphonTether);
        // 45 frames = 0.75 s telegraph, 3 units = 180 px, 10 s cooldown,
        // 3 s of tether, 10 points/s, 30 points per cast.
        AssertThat(ability.TelegraphFrames).IsEqual(45);
        AssertFloat(ability.PulseRadius).IsEqual(180f);
        AssertFloat(ability.CooldownSeconds).IsEqual(10f);
        AssertFloat(ability.FieldDurationSeconds).IsEqual(3f);
        AssertFloat(ability.MeterDrainPerSecond).IsEqual(10f);
        AssertFloat(ability.MeterDrainCap).IsEqual(30f);

        // "no HP damage, no hitstun/knockback/movement lock, no status-slot entry".
        AssertFloat(ability.Damage).IsEqual(0f);
        AssertFloat(ability.HitstunDuration).IsEqual(0f);
        AssertThat(ability.KnockbackForce).IsEqual(Vector2.Zero);
        AssertThat(ability.AppliedStatus).IsEqual(StatusType.None);
        AssertThat(ability.HasValidArchetypeFields())
            .OverrideFailureMessage("The SiphonTether validation case must accept the authored snare.")
            .IsTrue();
        // The active phase must hold the full channel; the executor cuts it short
        // the moment the tether breaks.
        AssertThat(ability.ResolvedActiveFrames).IsGreaterEqual(180);
    }

    // === Drain arithmetic: the three authored starting meters ===

    [TestCase]
    public void AFullMeterPlayerWhoDoesNothingEndsAtExactlySeventy() {
        using var world = new SnareWorld(playerMeter: 100f);
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        SiphonTetherChannel channel = world.Channel;

        // Three live seconds at ten points a second, and not a point more.
        for (int frame = 0; frame < 180; frame++) channel.TickChannel(Step);

        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(70f, 0.01f);
        AssertFloat(channel.DrainedTotal).IsEqualApprox(30f, 0.01f);
        AssertThat(channel.IsLive).IsFalse();
    }

    [TestCase]
    public void TheThirtyPointCapHoldsEvenIfTheChannelIsTickedPastItsDuration() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        for (int frame = 0; frame < 600; frame++) channel.TickChannel(Step);

        AssertFloat(channel.DrainedTotal)
            .OverrideFailureMessage("A single cast may never take more than 30 points.")
            .IsEqualApprox(30f, 0.01f);
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(70f, 0.01f);
    }

    [TestCase]
    public void AFivePointMeterDrainsToZeroAndTheChannelEndsThere() {
        using var world = new SnareWorld(playerMeter: 5f);
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        SiphonTetherChannel channel = world.Channel;

        for (int frame = 0; frame < 180 && channel.IsLive; frame++) channel.TickChannel(Step);

        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(0f, 0.01f);
        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.MeterEmpty);
        // Fractions are preserved per tick, so the total is the meter itself, not
        // a rounded sip past it.
        AssertFloat(channel.DrainedTotal).IsEqualApprox(5f, 0.01f);
    }

    [TestCase]
    public void AZeroMeterTargetIsRefusedOutrightAndNoChannelExists() {
        using var world = new SnareWorld(playerMeter: 0f);
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.NoTarget);
        AssertObject(world.Channel)
            .OverrideFailureMessage("A refused cast must leave no lingering pulse.")
            .IsNull();
        AssertThat(world.Eraser.IsChannelling).IsFalse();
    }

    // === Blocks: front, rear, zero charge, final charge ===

    [TestCase]
    public void AFrontFacingBlockAbsorbsTheCastForOneChargeWithNoDrainAndNoHpChip() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.RaiseBlockFacingTheEraser();
        int chargesBefore = world.Block.CurrentCharges;
        int hpBefore = world.Player.CurrentHP;

        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Blocked);

        AssertThat(world.Block.CurrentCharges).IsEqual(chargesBefore - 1);
        AssertThat(world.Block.IsInShieldStun)
            .OverrideFailureMessage("A non-shatter absorb applies the ordinary shieldstun.")
            .IsTrue();
        AssertThat(world.Player.CurrentHP)
            .OverrideFailureMessage("The Snare deals no HP damage, blocked or not.")
            .IsEqual(hpBefore);
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(100f, 0.01f);
        AssertObject(world.Channel)
            .OverrideFailureMessage("A blocked cast creates no lingering pulse.")
            .IsNull();
    }

    [TestCase]
    public void ABlockFacingTheWrongWayAbsorbsNothingAndTheTetherAttaches() {
        using var world = new SnareWorld(playerMeter: 100f);
        // Stance up, but turned away from the Eraser standing behind.
        world.Eraser.GlobalPosition = world.Player.GlobalPosition - new Vector2(90f, 0f);
        world.Player.IsFacingRight = true;
        world.Block.StartBlock();
        world.Player.TransitionTo(CharacterState.Blocking);
        int chargesBefore = world.Block.CurrentCharges;

        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        AssertThat(world.Block.CurrentCharges)
            .OverrideFailureMessage("A block facing the wrong way spends nothing.")
            .IsEqual(chargesBefore);

        for (int frame = 0; frame < 60; frame++) world.Channel.TickChannel(Step);
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(90f, 0.05f);
    }

    [TestCase]
    public void AZeroChargeStanceCannotAbsorbAndTheTetherAttaches() {
        using var world = new SnareWorld(playerMeter: 100f);
        // Shatter first: zero charges plus a running lockout means the stance
        // cannot rise, so there is nothing to absorb with.
        AssertThat(world.Block.DepleteCharges(world.Block.MaxCharges)).IsEqual(BlockResult.GuardBroken);
        world.Block.StartBlock();
        AssertThat(world.Block.IsBlocking).IsFalse();
        world.Player.IsFacingRight = true;
        world.Player.TransitionTo(CharacterState.Blocking);

        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        AssertThat(world.Block.CurrentCharges).IsEqual(0);
    }

    [TestCase]
    public void AFinalChargeBlockShattersNormallyAndStillPreventsAllDrain() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Block.DepleteCharges(world.Block.MaxCharges - 1);
        world.RaiseBlockFacingTheEraser();
        AssertThat(world.Block.CurrentCharges).IsEqual(1);

        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Blocked);

        AssertThat(world.Block.CurrentCharges).IsEqual(0);
        AssertThat(world.Player.CurrentState)
            .OverrideFailureMessage("Spending the last charge shatters the guard as it normally would.")
            .IsEqual(CharacterState.Dazed);
        AssertFloat(world.Player.CurrentUltimateMeter)
            .OverrideFailureMessage("A shattering block still prevents all drain.")
            .IsEqualApprox(100f, 0.01f);
    }

    // === Break conditions ===

    [TestCase]
    public void LeavingTheThreeUnitRadiusBreaksTheTetherBeforeTheNextDrainTick() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);
        float afterOneTick = world.Player.CurrentUltimateMeter;

        // 181 px against a 180 px radius: one pixel outside is outside.
        world.Player.GlobalPosition = world.Eraser.GlobalPosition + new Vector2(181f, 0f);
        channel.TickChannel(Step);

        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.OutOfRange);
        AssertFloat(world.Player.CurrentUltimateMeter)
            .OverrideFailureMessage("Break conditions run BEFORE the drain; there is no last sip.")
            .IsEqualApprox(afterOneTick, 0.001f);
    }

    [TestCase]
    public void TheRadiusBoundaryItselfIsStillInsideTheTether() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        world.Player.GlobalPosition = world.Eraser.GlobalPosition + new Vector2(180f, 0f);
        channel.TickChannel(Step);

        AssertThat(channel.IsLive)
            .OverrideFailureMessage("Exactly 3 units centre-to-centre is inside the radius, not outside.")
            .IsTrue();
    }

    [TestCase]
    public void AuthoredSolidCoverBetweenTheTwoBreaksTheTether() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);

        SiphonTetherChannel.LineOfSightProbe = static (_, _, _) => false;
        try {
            channel.TickChannel(Step);
        } finally {
            SiphonTetherChannel.LineOfSightProbe = SiphonTetherChannel.DefaultLineOfSight;
        }
        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.LineOfSightBlocked);
    }

    [TestCase]
    public void RollInvulnerabilityBreaksTheTetherBeforeTheNextDrainTick() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);
        float afterOneTick = world.Player.CurrentUltimateMeter;

        // The roll's opening i-frames are named explicitly in the break list.
        world.Player.TransitionTo(CharacterState.Rolling);
        for (int frame = 0; frame < 6 && !world.Player.IsRollInvulnerable; frame++) {
            world.Player._PhysicsProcess(Step);
        }
        AssertThat(world.Player.IsRollInvulnerable)
            .OverrideFailureMessage("The roll must be invulnerable on its opening travel frames.")
            .IsTrue();

        channel.TickChannel(Step);
        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.TargetInvulnerable);
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(afterOneTick, 0.001f);
    }

    [TestCase]
    public void SuppressionNewlyAppliedToTheTargetBreaksTheTether() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);

        world.Player.GetNode<StatusController>("StatusController")
            .ApplyStatus(StatusType.Suppression, 2f, 1f);
        channel.TickChannel(Step);

        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.TargetSuppressed);
    }

    [TestCase]
    public void ASuccessfulStaggerInterruptBreaksTheTether() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);
        AssertThat(world.Eraser.IsStaggerArmored)
            .OverrideFailureMessage("This case needs an UNarmored Eraser to be meaningful.")
            .IsFalse();

        world.Eraser.ApplyStun(0.6f);
        AssertThat(channel.BreakReason).IsEqual(SiphonBreakReason.CasterInterrupted);
    }

    [TestCase]
    public void AHitRejectedByStaggerArmorIsNotAnInterruptAndTheTetherSurvives() {
        using var world = new SnareWorld(playerMeter: 100f);
        // Trip the V7.4 elite stagger budget first, with no tether in play. The
        // Eraser's StunResistance 0.5 halves every applied stun, so six 0.9 s
        // hits are what it takes to cross the 2.0 s budget.
        for (int hit = 0; hit < 6 && !world.Eraser.IsArmoredRecovery; hit++) {
            world.Eraser.ApplyStun(0.9f);
        }
        AssertThat(world.Eraser.IsArmoredRecovery)
            .OverrideFailureMessage("The elite budget must trip into Armored Recovery.")
            .IsTrue();

        // Now cast under that protection and land another hit.
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        SiphonTetherChannel channel = world.Channel;
        channel.TickChannel(Step);
        world.Eraser.ApplyStun(0.9f);

        AssertThat(channel.IsLive)
            .OverrideFailureMessage("A hit the armor rejected is explicitly NOT an interrupt.")
            .IsTrue();
        channel.TickChannel(Step);
        AssertFloat(channel.DrainedTotal)
            .OverrideFailureMessage("The tether kept draining through the armored hit.")
            .IsEqualApprox(2f * 10f * Step, 0.01f);
    }

    // === Channelling gate, overlap, freeze, reconstruction, mode isolation ===

    [TestCase]
    public void AChannellingEraserIsStationaryAndSelectsNoOtherAttack() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        AssertThat(world.Eraser.IsChannelling).IsTrue();

        world.Eraser.Velocity = new Vector2(400f, 0f);
        world.Eraser.PumpAttackState(Step);

        AssertFloat(world.Eraser.Velocity.X)
            .OverrideFailureMessage("The Eraser is stationary while maintaining the tether.")
            .IsEqualApprox(0f, 0.001f);
        AssertThat(world.Eraser.ActiveAbility.Archetype)
            .OverrideFailureMessage("No other attack may be selected mid-channel.")
            .IsEqual(EnemyAbilityArchetype.SiphonTether);
    }

    [TestCase]
    public void OnlyOneTetherHoldsAPlayerAndASimultaneousContestGoesToTheLowerStableID() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Eraser.Name = "eraser_b";
        AssertThat(world.Cast()).IsEqual(SiphonAttachResult.Attached);
        SiphonTetherChannel first = world.Channel;

        // A later cast simply fails — the one-tether rule, no stacked rate.
        var contender = new SiphonTetherChannel { Name = "Contender", StableActorID = "eraser_c" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(contender);
        try {
            contender.AttachFrameOverride = first.AttachFrame + 1UL;
            AssertThat(contender.Attach(world.Ability, world.Eraser, world.Player))
                .OverrideFailureMessage("A later cast may not stack a tether on a tethered player.")
                .IsFalse();
            AssertThat(first.IsLive).IsTrue();

            // A genuinely simultaneous pair resolves by stable actor ID:
            // ordinal-least keeps the tether and the loser never drains.
            contender.StableActorID = "eraser_a";
            contender.AttachFrameOverride = first.AttachFrame;
            AssertThat(contender.Attach(world.Ability, world.Eraser, world.Player))
                .OverrideFailureMessage("'eraser_a' sorts below 'eraser_b' and must win the contest.")
                .IsTrue();
            AssertThat(first.BreakReason).IsEqual(SiphonBreakReason.Contested);
            AssertFloat(first.DrainedTotal).IsEqualApprox(0f, 0.001f);
            AssertObject(SiphonTetherChannel.ActiveFor(world.Player)).IsEqual(contender);
        } finally {
            contender.Break(SiphonBreakReason.Released);
        }
    }

    [TestCase]
    public void AWorldFreezePausesTheChannelAndThawRunsNoCatchUpTick() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        for (int frame = 0; frame < 30; frame++) channel.TickChannel(Step);
        float meterAtFreeze = world.Player.CurrentUltimateMeter;
        float secondsAtFreeze = channel.ChannelSeconds;

        // Time Freeze and the death rewind share the latch; both are pure pauses.
        channel.SetTimeFrozen(true);
        for (int frame = 0; frame < 300; frame++) channel.TickChannel(Step);
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(meterAtFreeze, 0.001f);
        AssertFloat(channel.ChannelSeconds).IsEqualApprox(secondsAtFreeze, 0.001f);

        channel.SetTimeFrozen(false);
        channel.TickChannel(Step);
        AssertFloat(world.Player.CurrentUltimateMeter)
            .OverrideFailureMessage("Thaw drains exactly one tick — a freeze is a pause, never a debt.")
            .IsEqualApprox(meterAtFreeze - 10f * Step, 0.01f);
    }

    [TestCase]
    public void ACheckpointReconstructionEndsTheTetherAndNeverGrantsDrainedMeterBack() {
        using var world = new SnareWorld(playerMeter: 100f);
        world.Cast();
        SiphonTetherChannel channel = world.Channel;
        for (int frame = 0; frame < 60; frame++) channel.TickChannel(Step);
        float meterAfterOneSecond = world.Player.CurrentUltimateMeter;
        AssertFloat(meterAfterOneSecond).IsEqualApprox(90f, 0.05f);

        // F10 reconstruction / room departure ends the transient tether and keeps
        // the latest durable meter. Nothing is repeated and nothing is refunded.
        channel.Release();
        AssertThat(channel.IsLive).IsFalse();
        AssertObject(SiphonTetherChannel.ActiveFor(world.Player)).IsNull();
        AssertFloat(world.Player.CurrentUltimateMeter).IsEqualApprox(meterAfterOneSecond, 0.001f);
    }

    [TestCase]
    public void NeitherTheSnareNorItsStatusCanReachTheFighterSimulation() {
        // Story-only by construction, proven two ways rather than asserted.
        // 1. The deterministic sim refuses Suppression outright (A1's guard), so
        //    the Eraser's rider cannot enter Fighter state even if authored on a
        //    resource that somehow reached a Fighter hit path.
        var state = new FTT.FighterSim.FighterStateComponent { HitstunFrames = 0 };
        var runtime = new FTT.FighterSim.FighterRuntimeComponent();
        FTT.FighterSim.FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.Suppression, 120, xpTURN.Klotho.Deterministic.Math.FP64.One);
        AssertThat(runtime.StatusType)
            .OverrideFailureMessage("Suppression must not enter deterministic Fighter state.")
            .IsEqual((int)StatusType.None);
        AssertThat(runtime.DamageStatusType).IsEqual((int)StatusType.None);

        // 2. The Snare lives on EnemyAbilityData, which is Story-only content by
        //    contract: no Fighter loadout, projectile or verb path constructs one.
        var ability = AuthoredResources.Load<EnemyAbilityData>(AbilityPath);
        AssertObject(ability).IsNotNull();
        AssertThat(typeof(FTT.Combat.AbilityData).IsAssignableFrom(typeof(EnemyAbilityData)))
            .OverrideFailureMessage(
                "EnemyAbilityData must never become a Fighter AbilityData: that resource "
                + "requires a CharacterID and feeds Fighter loadouts.")
            .IsFalse();
    }

    // === Fixture ===

    /// <summary>
    /// One real authored Eraser and one real player, both inside the live scene
    /// tree so the group sweep, the block system and the stagger discipline all
    /// behave exactly as they do in a level.
    /// </summary>
    private sealed class SnareWorld : IDisposable {
        public readonly PlayerController Player;
        public readonly BlockSystem Block;
        public readonly EnemyController Eraser;
        public readonly EnemyAbilityData Ability;

        public SiphonTetherChannel Channel => Eraser.ActiveSiphonTether;

        public SnareWorld(float playerMeter) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();

            Player = CharacterFactory.CreateCharacter("einstein");
            tree.Root.AddChild(Player);
            Player.GlobalPosition = new Vector2(1000f, 500f);
            Block = Player.GetNode<BlockSystem>("BlockSystem");
            Player.CurrentUltimateMeter = playerMeter;
            Player.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.SetValue(playerMeter);

            EnemyData canonical = AuthoredResources.Load<EnemyData>(
                $"res://resources/Enemies/{EraserID}.tres");
            var data = (EnemyData)canonical.Duplicate();
            PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
            Eraser = scene.Instantiate<EnemyController>();
            Eraser.Data = data;
            tree.Root.AddChild(Eraser);
            Eraser.OnSpawn();
            Eraser.GlobalPosition = Player.GlobalPosition + new Vector2(90f, 0f);

            Ability = data.EliteAbilities[1];
            AssertThat(Ability.Archetype).IsEqual(EnemyAbilityArchetype.SiphonTether);
        }

        /// <summary>Turns the player toward the Eraser and raises a legal stance.</summary>
        public void RaiseBlockFacingTheEraser() {
            Player.IsFacingRight = Eraser.GlobalPosition.X >= Player.GlobalPosition.X;
            Block.StartBlock();
            Player.TransitionTo(CharacterState.Blocking);
        }

        /// <summary>
        /// Commits the cast through the ordinary <see cref="EnemyController.BeginAttack"/>
        /// path and walks the telegraph to its single attachment check.
        /// </summary>
        public SiphonAttachResult Cast() {
            Eraser.BeginAttack(Ability);
            for (int frame = 0; frame < Ability.TelegraphFrames; frame++) {
                Eraser.TickAbilityExecutor(Step);
            }
            return Eraser.LastSiphonResult;
        }

        public void Dispose() {
            Eraser.ActiveSiphonTether?.Release();
            if (GodotObject.IsInstanceValid(Eraser)) Eraser.Free();
            if (GodotObject.IsInstanceValid(Player)) Player.Free();
        }
    }
}
