using System;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D3 — the on-hit Special recovery cancel (user direction 2026-10-11, ledger
/// USER-SPECIAL-RECOVERY-CANCEL; the agent-chosen details are
/// PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES). A Special whose execution LANDED
/// a direct hit (the D1a predicate) may cancel its recovery into a roll or a
/// jump from recovery frame 0, and into a basic attack — hit 1, the Up-Attack
/// or the Down-Air by the held direction (the 2026-10-11 addendum) — or the
/// movement ability from frame 6. A whiff keeps its whole recovery, Echo Step
/// never comes out of a Special, and neither a Special nor the Ultimate is
/// polled there. A one-slot cancel buffer holds a press for six unfrozen ticks.
///
/// <para>Manual ticks in the tree order: the controller, then its four ability
/// nodes. The landed hit is Righteous Smite's own wave strike
/// (<see cref="JoanRighteousSmite.StrikeWithWave"/>), which runs the real
/// <c>Credit(struck, ...)</c> path, on a real <c>chrono_slasher</c> standing
/// behind Joan so the travelling wave never reaches it on its own.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StorySpecialRecoveryCancelTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(-58500f, 1500f);
    private static readonly string[] AbilityNodes = { "Special1", "Special2", "MovementAbility", "Ultimate" };

    // === Roll / jump from frame 0 ==============================================

    [TestCase]
    public void ALandedSpecialCancelsItsRecoveryIntoARollOrAJumpFromRecoveryFrameZero() {
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 0);
            Tick(player, GameplayButtons.Roll);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A landed Special's recovery cancels into the roll on frame 0.")
                .IsEqual(CharacterState.Rolling);
            AssertThat(smite.IsExecuting).IsFalse();
            AssertThat(player.SpecialOneCooldownTimer > 0f)
                .OverrideFailureMessage("The cooldown the cast armed stands.")
                .IsTrue();
        });

        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 0);
            float cancelRise = MeasureHeldJumpRise(player);
            AssertThat(smite.IsExecuting).IsFalse();

            for (int tick = 0; tick < 240 && !(player.IsOnFloor() && player.CurrentState == CharacterState.Idle); tick++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            float idleRise = MeasureHeldJumpRise(player);
            AssertFloat(cancelRise)
                .OverrideFailureMessage($"The jump cancel rises {cancelRise} px against an Idle jump's {idleRise} px.")
                .IsEqualApprox(idleRise, 1f);
        });
    }

    // === Basic attack / movement ability from frame 6 =========================

    [TestCase]
    public void ALandedSpecialCancelsIntoHitOneOrTheMovementAbilityOnlyFromRecoveryFrameSix() {
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 5);
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Recovery frame 5 is too early for hit 1.")
                .IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.BasicAttack);
            // Frame 6, nothing pressed: the buffered press (age 1) fires.
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual("combo_1");
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("Hit 1 arms the swing lockout like any swing.")
                .IsEqual(BasicComboRules.AbilityLockoutFrames);
            AssertThat(smite.IsExecuting).IsFalse();
        });

        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(smite.IsExecuting).IsFalse();
        });
    }

    // === Whiffs (regression guard) =============================================

    /// <summary>Regression guard: passes on the pre-D3 code too.</summary>
    [TestCase]
    public void AWhiffedSpecialKeepsItsWholeRecoveryAndOnlyBlockStillCancelsIt() {
        const GameplayButtons Everything = GameplayButtons.Roll | GameplayButtons.Jump
            | GameplayButtons.BasicAttack | GameplayButtons.MovementAbility;
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastSmite(player);
            AdvanceUntilReading(player, smite, 0);
            int recovery = smite.PhaseFramesRemaining;
            AssertThat(recovery).IsEqual(smite.Data.RecoveryFrames);
            for (int frame = 0; frame < recovery; frame++) {
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"A whiff must hold its recovery at frame {frame}.")
                    .IsEqual(CharacterState.UsingSpecial);
                Tick(player, Everything);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
        });

        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastSmite(player);
            AdvanceUntilReading(player, smite, 0);
            Tick(player, GameplayButtons.Block);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);
            AssertThat(smite.IsExecuting).IsFalse();
        });
    }

    // === What arms the latch ===================================================

    [TestCase]
    public void TicksConstructsStrikeSurfacesAndZeroDamageHitsNeverArmTheCancelButADirectHitDoes() {
        Run("joan", (player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(-200f, 0f));
            Hurtbox mobHurtbox = mob.GetNode<Hurtbox>("Hurtbox");
            var surface = new EnvironmentHurtboxAdapter {
                Name = "D3StrikeSurface",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.PersistentObject,
                CollisionMask = CollisionLayers.PlayerHitbox,
                Position = Origin + new Vector2(-300f, -40f)
            };
            surface.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 60f) } });
            int surfaceStrikes = 0;
            surface.OnHit += _ => {
                surfaceStrikes++;
                return 10f;
            };
            host.AddChild(surface);

            JoanRighteousSmite smite = CastSmite(player);
            // A Special-class tick and a construct hit on the mob: both land for
            // D1a, neither is a direct hit.
            CreditLikeAKit(smite, player, mobHurtbox, 6f, HitDelivery.Tick);
            CreditLikeAKit(smite, player, mobHurtbox, 6f, HitDelivery.Construct);
            // A direct hit on a strike surface: it takes damage, never lands.
            smite.StrikeWithWave(surface, player.GlobalPosition);
            AssertThat(surfaceStrikes).OverrideFailureMessage("Harness: the surface was never struck.").IsEqual(1);
            // A zero-damage direct hit on a live mob never lands (orchestrator ruling, spec B §0.2).
            CreditLikeAKit(smite, player, mobHurtbox, 0f, HitDelivery.DirectHit);
            AssertThat(smite.LandedDirectHitThisExecution).IsFalse();

            AdvanceUntilReading(player, smite, 0);
            Tick(player, GameplayButtons.Roll);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("None of them may open the recovery cancel.")
                .IsEqual(CharacterState.UsingSpecial);

            // Positive control, same execution: the wave's own direct hit on the mob.
            smite.StrikeWithWave(mobHurtbox, mob.GlobalPosition);
            AssertThat(smite.LandedDirectHitThisExecution).IsTrue();
            bool rolled = false;
            for (int tick = 0; tick < 30 && !rolled; tick++) {
                Tick(player, GameplayButtons.Roll);
                rolled = player.CurrentState == CharacterState.Rolling;
                if (!rolled) AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            }
            AssertThat(rolled).OverrideFailureMessage("A landed direct hit opens the roll cancel.").IsTrue();
        });
    }

    [TestCase]
    public void ABarrierAbsorptionArmsTheCancelButABlockOrAnInvulnerableTargetDoesNot() {
        Run("joan", (player, host) => {
            PlayerController target = CharacterFactory.CreateCharacter("einstein", 1,
                applyStoryProgression: false, applyLegacyUnlockLocks: false);
            target.Position = Origin + new Vector2(-150f, -8f);
            host.AddChild(target);
            Settle(target);
            target.IsFacingRight = true;
            Hurtbox targetHurtbox = target.GetNode<Hurtbox>("Hurtbox");
            var smite = player.GetNode<JoanRighteousSmite>("Special1");
            int hp = target.CurrentHP;

            // A full absorption by a protection layer lands.
            AssertThat(smite.TryExecute()).IsTrue();
            target.GrantTemporalAegis();
            smite.StrikeWithWave(targetHurtbox, player.GlobalPosition);
            AssertThat(target.CurrentHP).OverrideFailureMessage("Harness: the Aegis must absorb it whole.").IsEqual(hp);
            AssertThat(smite.LandedDirectHitThisExecution).IsTrue();
            smite.Interrupt();

            // The Hitbox path (a pooled shot, a child ability hitbox): an absorbed
            // 0-damage contact still reaches the latch.
            AssertThat(smite.TryExecute()).IsTrue();
            target.GrantTemporalAegis();
            var box = new Hitbox {
                Name = "D3AbilityHitbox",
                AttackID = smite.Data.AbilityID,
                HitboxID = "test_shot",
                AttackClass = AttackClass.Special,
                Origin = HitOrigin.Special,
                Delivery = HitDelivery.DirectHit,
                OwnerPlayerIndex = 0,
                Damage = 10f,
                IsActive = true,
                SourcePlayer = player,
                CollisionLayer = CollisionLayers.PlayerHitbox,
                CollisionMask = CollisionLayers.PlayerHurtbox,
                // Far from everything: the contact is driven by the signal below.
                Position = Origin + new Vector2(0f, -3000f)
            };
            box.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20f, 20f) } });
            host.AddChild(box);
            box.Activate(); // _Ready deactivates every hitbox
            float dealt = -1f;
            box.HitConfirmed += (_, damage) => dealt = damage;
            box.EmitSignal(Area2D.SignalName.AreaEntered, targetHurtbox);
            AssertFloat(dealt).OverrideFailureMessage("Harness: the hitbox contact never resolved.").IsEqual(0f);
            AssertThat(smite.LandedDirectHitThisExecution)
                .OverrideFailureMessage("A shield-absorbed Hitbox contact arms the cancel.")
                .IsTrue();
            smite.Interrupt();

            // A roll-invulnerable target does not land. It rolls away from Joan.
            AssertThat(smite.TryExecute()).IsTrue();
            target.IsFacingRight = false;
            Tick(target, GameplayButtons.Roll);
            for (int tick = 0; tick < 12 && !target.IsRollInvulnerable; tick++) Tick(target, GameplayButtons.None);
            AssertThat(target.IsRollInvulnerable).OverrideFailureMessage("Harness: the roll never turned invulnerable.").IsTrue();
            smite.StrikeWithWave(targetHurtbox, player.GlobalPosition);
            AssertThat(target.CurrentHP).IsEqual(hp);
            AssertThat(smite.LandedDirectHitThisExecution).IsFalse();
            smite.Interrupt();
            for (int tick = 0; tick < 60 && target.CurrentState != CharacterState.Idle; tick++) {
                Tick(target, GameplayButtons.None);
            }
            AssertThat(target.CurrentState).IsEqual(CharacterState.Idle);

            // A block does not land.
            AssertThat(smite.TryExecute()).IsTrue();
            // Raise the stance through input, as a held Block does.
            Tick(target, GameplayButtons.Block);
            Tick(target, GameplayButtons.Block, previous: GameplayButtons.Block);
            AssertThat(target.CurrentState).IsEqual(CharacterState.Blocking);
            // CM-BLOCK (decision 3.4): hold the stance past its 5-frame
            // perfect-block window, so the strike meets an ordinary block.
            for (int tick = 0; tick < FTT.Combat.BasicComboRules.PerfectBlockWindowFrames; tick++) {
                Tick(target, GameplayButtons.Block, previous: GameplayButtons.Block);
            }
            AssertThat(target.IsPerfectBlockWindowOpen).IsFalse();
            target.IsFacingRight = true;
            AssertThat(target.GlobalPosition.X < player.GlobalPosition.X).IsTrue();
            smite.StrikeWithWave(targetHurtbox, player.GlobalPosition);
            AssertThat(target.CurrentHP).OverrideFailureMessage("Harness: the block must stop it.").IsEqual(hp);
            AssertThat(smite.LandedDirectHitThisExecution).IsFalse();
            smite.Interrupt();
        });
    }

    [TestCase]
    public void ALateShotFromAnEarlierCastNeverArmsTheCurrentCast() {
        Run("joan", (player, host) => {
            var smite = player.GetNode<JoanRighteousSmite>("Special1");
            AssertThat(smite.TryExecute()).IsTrue();
            smite.Interrupt();
            AssertThat(smite.TryExecute()).IsTrue();
            int serial = smite.ExecutionSerial;
            AssertThat(serial >= 2).IsTrue();

            smite.ConfirmAbilityHit(LandedPayload(smite, serial - 1), 0f, Origin);
            AssertThat(smite.LandedDirectHitThisExecution)
                .OverrideFailureMessage("A late shot from the previous cast must not arm this one.")
                .IsFalse();
            HitPayload notLanded = LandedPayload(smite, 0);
            notLanded.ContactLanded = false;
            smite.ConfirmAbilityHit(notLanded, 0f, Origin);
            AssertThat(smite.LandedDirectHitThisExecution).IsFalse();
            smite.ConfirmAbilityHit(LandedPayload(smite, serial), 0f, Origin);
            AssertThat(smite.LandedDirectHitThisExecution).IsTrue();

            smite.Interrupt();
            AssertThat(smite.TryExecute()).IsTrue();
            AssertThat(smite.LandedDirectHitThisExecution)
                .OverrideFailureMessage("A fresh execution starts unlatched.")
                .IsFalse();
            smite.ConfirmAbilityHit(LandedPayload(smite, 0), 0f, Origin);
            AssertThat(smite.LandedDirectHitThisExecution)
                .OverrideFailureMessage("Serial 0 belongs to the execution current when it lands.")
                .IsTrue();
            smite.Interrupt();
        });
    }

    // === What never comes out ==================================================

    [TestCase]
    public void NoSpecialOrEchoStepComesOutOfALandedRecovery() {
        Run("joan", (player, host) => {
            LiftIntoTheAir(player, 900f);
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            GameplayButtons[] refused = {
                GameplayButtons.Special1,
                GameplayButtons.Special2,
                GameplayButtons.EchoStep,
                // The airborne Echo Step chord (Block + Roll forms the EchoStep bit).
                GameplayButtons.Block | GameplayButtons.Roll | GameplayButtons.EchoStep,
                GameplayButtons.None
            };
            foreach (GameplayButtons press in refused) {
                Tick(player, press);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"{press} must not come out of a landed Special's recovery.")
                    .IsEqual(CharacterState.UsingSpecial);
                AssertThat(player.BufferedSpecialCancel)
                    .OverrideFailureMessage($"{press} must not be buffered.")
                    .IsEqual(PlayerController.SpecialCancelVerb.None);
                AssertThat(player.IsOnFloor()).OverrideFailureMessage("Harness: the hero landed.").IsFalse();
            }
            AssertThat(smite.IsExecuting).IsTrue();
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);

            // Positive control: a neutral Attack press cancels into the air
            // strike (GU-A: an air press is never the string's hit 1).
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual(BasicComboRules.AirAttackHitboxID);
        });
    }

    /// <summary>
    /// The 2026-10-11 addendum: from recovery frame 6 every basic variant comes
    /// out — the Up-Attack, the Down-Air or hit 1 by the held direction at the
    /// press — so a held direction never eats the press.
    /// </summary>
    [TestCase]
    public void EveryBasicVariantComesOutOfALandedRecoveryByTheHeldDirection() {
        // Up + Attack, grounded: the Up-Attack.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.BasicAttack, vertical: -1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
        });

        // Down + Attack, airborne: the Down-Air.
        Run("joan", (player, host) => {
            LiftIntoTheAir(player, 900f);
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.BasicAttack | GameplayButtons.Down, vertical: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual(BasicComboRules.DownAirHitboxID);
        });

        // Down + Attack, grounded: the string's hit 1 (grounded Down is the string).
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.BasicAttack | GameplayButtons.Down, vertical: 1f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual("combo_1");
        });

        // Up + Attack pressed at frame 2 and released: the press's direction fires at frame 6.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 2);
            Tick(player, GameplayButtons.BasicAttack, vertical: -1f);
            for (int frame = 3; frame <= 5; frame++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            }
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
        });
    }

    // === Foresight =============================================================

    [TestCase]
    public void CancellingForesightsRecoveryEndsItsSidestep() {
        Run("tubman", (player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(60f, 0f));
            var foresight = player.GetNode<TubmanForesight>("Special2");
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            for (int tick = 0; tick < 60 && !foresight.IsWindowOpen; tick++) Tick(player, GameplayButtons.None);
            AssertThat(foresight.IsWindowOpen).IsTrue();

            // The mob's swing resolves in the physics flush, before the controller.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.strike",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 12f,
                HitOrigin = mob.GlobalPosition,
                Delivery = HitDelivery.DirectHit,
                Origin = HitOrigin.Basic
            });
            AssertThat(foresight.CountersLanded).IsEqual(1);
            AssertThat(foresight.IsSidestepping).IsTrue();
            AssertThat(foresight.RecoveryFramesElapsed).IsEqual(0);

            // Frame 0: the answer has not landed yet, so the Jump is only buffered.
            Tick(player, GameplayButtons.Jump);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Foresight cannot cancel at recovery frame 0.")
                .IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.Jump);
            AssertThat(foresight.LandedDirectHitThisExecution)
                .OverrideFailureMessage("The lantern answer lands on the ability's tick.")
                .IsTrue();
            AssertThat(player.IsInHitstop).OverrideFailureMessage("The answer freezes its caster.").IsTrue();

            for (int tick = 0; tick < 30 && player.IsInHitstop; tick++) {
                Tick(player, GameplayButtons.Jump, previous: GameplayButtons.Jump);
                if (player.IsInHitstop) AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            }
            AssertThat(foresight.RecoveryFramesElapsed).IsEqual(1);
            Tick(player, GameplayButtons.Jump, previous: GameplayButtons.Jump);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The buffered Jump fires at recovery frame 1.")
                .IsEqual(CharacterState.Airborne);
            AssertThat(foresight.IsSidestepping)
                .OverrideFailureMessage("Cancelling the recovery ends the sidestep.")
                .IsFalse();
            AssertThat(foresight.IsExecuting).IsFalse();
        });
    }

    /// <summary>
    /// CM-FIXB-SIM (B12, the Story half): a caught hit's lantern answer lands on
    /// the ability node's tick, which runs after the controller's. Block held
    /// through the window used to Block-cancel the recovery on the controller's
    /// first sidestep tick, interrupting the cast before the answer, which
    /// <c>OnInterrupted</c> then dropped. The cancel now waits while the answer
    /// is owed: the answer lands on that tick, and the held Block cancels the
    /// recovery once the answer's freeze is over.
    /// </summary>
    [TestCase]
    public void AHeldBlockWaitsForTheAnswerForesightOwes() {
        Run("tubman", (player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(60f, 0f));
            int mobHP = mob.CurrentHP;
            var foresight = player.GetNode<TubmanForesight>("Special2");
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            for (int tick = 0; tick < 60 && !foresight.IsWindowOpen; tick++) {
                Tick(player, GameplayButtons.Block, previous: GameplayButtons.Block);
            }
            AssertThat(foresight.IsWindowOpen).IsTrue();

            // The mob's swing resolves in the physics flush, before the controller.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 1,
                TargetIndex = 0,
                AttackID = "test.strike",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 12f,
                HitOrigin = mob.GlobalPosition,
                Delivery = HitDelivery.DirectHit,
                Origin = HitOrigin.Basic
            });
            AssertThat(foresight.CountersLanded).IsEqual(1);
            AssertThat(foresight.IsAnswerPending).IsTrue();

            // The first sidestep tick, Block still held: the answer lands first.
            Tick(player, GameplayButtons.Block, previous: GameplayButtons.Block);
            AssertThat(foresight.LandedDirectHitThisExecution)
                .OverrideFailureMessage("The held Block must not drop the owed lantern answer.")
                .IsTrue();
            AssertThat(mob.CurrentHP < mobHP).OverrideFailureMessage("The answer strikes the attacker.").IsTrue();
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The Block cancel waits for the owed answer.")
                .IsEqual(CharacterState.UsingSpecial);

            bool blocking = false;
            for (int tick = 0; tick < 30 && !blocking; tick++) {
                Tick(player, GameplayButtons.Block, previous: GameplayButtons.Block);
                blocking = player.CurrentState == CharacterState.Blocking;
            }
            AssertThat(blocking).OverrideFailureMessage("The held Block cancels the recovery after the answer.").IsTrue();
            AssertThat(foresight.IsExecuting).IsFalse();
            AssertThat(foresight.IsSidestepping).IsFalse();
        });
    }

    // === Chord guards ==========================================================

    [TestCase]
    public void AnUltimateChordPressNeverCancelsALandedRecoveryIntoTheMovementAbility() {
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.None);
        });

        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 4);
            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.MovementAbility);
            // The chord's second half a tick later (LB still held, RB pressed) clears it.
            Tick(player, GameplayButtons.MovementAbility | GameplayButtons.Ultimate,
                previous: GameplayButtons.MovementAbility);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.None);
            Tick(player, GameplayButtons.MovementAbility | GameplayButtons.Ultimate,
                previous: GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(smite.RecoveryFramesElapsed >= 6).IsTrue();
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A frame-apart LB+RB chord must not fire the movement cancel.")
                .IsEqual(CharacterState.UsingSpecial);
        });

        // Positive control: Movement alone at frame 6.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
        });
    }

    // === The buffer ============================================================

    [TestCase]
    public void ACancelPressIsBufferedOneDeepForSixFrames() {
        // A Roll pressed in the caster freeze fires at recovery frame 0.
        Run("joan", (player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(-200f, 0f));
            JoanRighteousSmite smite = CastSmite(player);
            for (int tick = 0; tick < 60 && !(smite.CurrentPhase == AbilityPhase.Active && smite.PhaseFramesRemaining == 1); tick++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(smite.CurrentPhase).IsEqual(AbilityPhase.Active);
            smite.StrikeWithWave(mob.GetNode<Hurtbox>("Hurtbox"), mob.GlobalPosition);
            AssertThat(player.IsInHitstop).IsTrue();
            Tick(player, GameplayButtons.Roll);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.Roll);
            for (int tick = 0; tick < 30 && player.IsInHitstop; tick++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.BufferedSpecialCancelAge)
                    .OverrideFailureMessage("A freeze never ages the buffer.")
                    .IsEqual(0);
            }
            AssertThat(player.IsInHitstop).IsFalse();
            Tick(player, GameplayButtons.None); // the last Active frame
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(smite.RecoveryFramesElapsed).IsEqual(0);
            Tick(player, GameplayButtons.None); // recovery frame 0
            AssertThat(player.CurrentState).IsEqual(CharacterState.Rolling);
        });

        // An Attack pressed at frame 0 fires at frame 6, six ticks old.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 0);
            Tick(player, GameplayButtons.BasicAttack);
            for (int frame = 1; frame <= 5; frame++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
                AssertThat(player.BufferedSpecialCancelAge).IsEqual(frame);
            }
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        });

        // An Attack pressed in the active frames has expired by frame 6.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            for (int tick = 0; tick < 60 && smite.CurrentPhase != AbilityPhase.Active; tick++) Tick(player, GameplayButtons.None);
            AssertThat(smite.CurrentPhase).IsEqual(AbilityPhase.Active);
            AssertThat(player.IsInHitstop).IsFalse();
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.BasicAttack);
            AdvanceUntilReading(player, smite, 6);
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A press more than six ticks old has expired.")
                .IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.None);
        });

        // The latest press wins: Attack at frame 1, Movement at frame 2 -> the movement ability.
        Run("joan", (player, host) => {
            JoanRighteousSmite smite = CastAndLandSmite(player, host);
            AdvanceUntilReading(player, smite, 1);
            Tick(player, GameplayButtons.BasicAttack);
            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.BufferedSpecialCancel).IsEqual(PlayerController.SpecialCancelVerb.MovementAbility);
            for (int frame = 3; frame <= 5; frame++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            }
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
        });
    }

    // === Harness ===============================================================

    /// <summary>Presses Special 1 (Joan's Righteous Smite) from Idle; nothing is struck.</summary>
    private static JoanRighteousSmite CastSmite(PlayerController player) {
        var smite = player.GetNode<JoanRighteousSmite>("Special1");
        player.IsFacingRight = true;
        Tick(player, GameplayButtons.Special1);
        AssertThat(player.CurrentState).OverrideFailureMessage("Harness: Special 1 was refused.").IsEqual(CharacterState.UsingSpecial);
        AssertThat(smite.IsExecuting).IsTrue();
        AssertThat(smite.LandedDirectHitThisExecution).IsFalse();
        return smite;
    }

    /// <summary>
    /// Casts Righteous Smite and lands its wave strike on a mob standing behind
    /// Joan (the wave travels forward, so it never strikes that mob again), then
    /// waits out the caster freeze.
    /// </summary>
    private static JoanRighteousSmite CastAndLandSmite(PlayerController player, Node2D host) {
        bool airborne = !player.IsOnFloor();
        EnemyController mob = Mob(host, (airborne ? player.GlobalPosition : Origin) + new Vector2(-200f, airborne ? 8f : 0f));
        JoanRighteousSmite smite = CastSmite(player);
        smite.StrikeWithWave(mob.GetNode<Hurtbox>("Hurtbox"), mob.GlobalPosition);
        AssertThat(smite.LandedDirectHitThisExecution)
            .OverrideFailureMessage("Harness: the wave strike did not land.")
            .IsTrue();
        AssertThat(player.IsInHitstop).OverrideFailureMessage("Harness: the landed strike must freeze Joan.").IsTrue();
        for (int tick = 0; tick < 30 && player.IsInHitstop; tick++) Tick(player, GameplayButtons.None);
        AssertThat(player.IsInHitstop).IsFalse();
        return smite;
    }

    /// <summary>Ticks with nothing pressed until the controller's NEXT tick reads <paramref name="frame"/>.</summary>
    private static void AdvanceUntilReading(PlayerController player, BaseSpecial ability, int frame) {
        for (int tick = 0; tick < 120; tick++) {
            if (ability.CurrentPhase == AbilityPhase.Recovery && ability.RecoveryFramesElapsed == frame
                && !player.IsInHitstop) {
                AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
                return;
            }
            Tick(player, GameplayButtons.None);
        }
        AssertThat(false).OverrideFailureMessage($"Harness: recovery frame {frame} was never reached.").IsTrue();
    }

    /// <summary>
    /// Presses and holds Jump from the current (grounded) state and returns the
    /// height of the arc's apex above the take-off position.
    /// </summary>
    private static float MeasureHeldJumpRise(PlayerController player) {
        float startY = player.GlobalPosition.Y;
        Tick(player, GameplayButtons.Jump);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
        float minY = player.GlobalPosition.Y;
        for (int tick = 0; tick < 180 && player.Velocity.Y < 0f; tick++) {
            Tick(player, GameplayButtons.Jump, previous: GameplayButtons.Jump);
            minY = Mathf.Min(minY, player.GlobalPosition.Y);
        }
        return startY - minY;
    }

    /// <summary>The kit Credit path for a payload built by hand: TakeHit, the D1a stamp, the confirm.</summary>
    private static void CreditLikeAKit(BaseSpecial ability, PlayerController owner, Hurtbox struck,
        float damage, HitDelivery delivery) {
        HitPayload hit = BaseSpecial.WithAbilityContract(new HitPayload {
            AttackerIndex = owner.PlayerIndex,
            AttackID = ability.Data.AbilityID,
            HitboxID = "test_hit",
            AttackClass = AttackClass.Special,
            Damage = damage,
            HitOrigin = struck.GlobalPosition,
            AttackerFacingRight = owner.IsFacingRight
        }, ability.Data, owner, delivery);
        float dealt = struck.TakeHit(hit);
        hit.ContactLanded = struck.DidLand(hit.ContactId);
        if (delivery != HitDelivery.DirectHit) {
            AssertThat(hit.ContactLanded)
                .OverrideFailureMessage("Harness: a damaging tick or construct hit lands for D1a.")
                .IsTrue();
        }
        ability.ConfirmAbilityHit(hit, dealt, struck.GlobalPosition);
    }

    private static HitPayload LandedPayload(BaseSpecial ability, int serial) => new() {
        AttackerIndex = 0,
        AttackID = ability.Data.AbilityID,
        HitboxID = "test_shot",
        AttackClass = AttackClass.Special,
        Damage = 10f,
        Delivery = HitDelivery.DirectHit,
        Origin = HitOrigin.Special,
        ContactId = HitClassification.NextContactId(),
        ContactLanded = true,
        SourceExecutionSerial = serial
    };

    private static void LiftIntoTheAir(PlayerController player, float height) {
        player.GlobalPosition += new Vector2(0f, -height);
        player.Velocity = Vector2.Zero;
        player.TransitionTo(CharacterState.Airborne);
        for (int tick = 0; tick < 20 && (player.CurrentState != CharacterState.Airborne || player.IsOnFloor()); tick++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.IsOnFloor()).IsFalse();
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
    }

    private static EnemyController Mob(Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        enemy.CurrentHP = 9999;
        return enemy;
    }

    private static void Run(string heroID, Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool paused = tree.Paused;
        var host = new Node2D { Name = "SpecialCancelHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "SpecialCancelPools" };
        tree.Root.AddChild(pools);
        host.AddChild(Floor());
        PlayerController player = CharacterFactory.CreateCharacter(heroID, 0,
            applyStoryProgression: false, applyLegacyUnlockLocks: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            Settle(player);
            body(player, host);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            InputManager.Instance?.ClearInputSource(1);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
            tree.Paused = paused;
        }
    }

    private static StaticBody2D Floor() {
        var floor = new StaticBody2D {
            Name = "SpecialCancelFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        return floor;
    }

    private static void Settle(PlayerController player) {
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            Tick(player, GameplayButtons.None);
        }
        for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
            .IsEqual(CharacterState.Idle);
    }

    /// <summary>
    /// One tick in the tree order: the controller, then its four ability
    /// nodes. <paramref name="held"/> minus <paramref name="previous"/> reads
    /// as pressed this tick.
    /// </summary>
    private static void Tick(PlayerController player, GameplayButtons held,
        GameplayButtons previous = GameplayButtons.None, float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, held, previous));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
        foreach (string name in AbilityNodes) {
            if (player.GetNodeOrNull<BaseSpecial>(name) is { } ability && GodotObject.IsInstanceValid(ability)) {
                ability._PhysicsProcess(Step);
            }
        }
    }
}
