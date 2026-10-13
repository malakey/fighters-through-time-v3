using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The ability lockout (special global cooldown, user direction 2026-10-08),
/// Story half. Starting any basic swing, casting Special 1 or Special 2, or
/// having the Ultimate accepted arms <see cref="BasicComboRules.AbilityLockoutFrames"/>;
/// while it runs Special 1, Special 2 and the Ultimate are refused at no cost.
/// Armed on tick T, the next special or Ultimate is legal on T + 30. Basics,
/// the movement ability, roll and grab are never refused and never arm it. It
/// pauses under hitstop and Time Freeze and is cleared by a death rewind or a
/// checkpoint restore. Every case swings at nothing, so no hitstop interferes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAbilityLockoutTests {
    private const double Step = 1.0 / 60.0;
    private static readonly int Lockout = BasicComboRules.AbilityLockoutFrames;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(47000f, 0f);

    [TestCase]
    public void ASpecialIsRefusedForThirtyTicksAfterASwingStarts() {
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);

            // T+1 .. T+29: refused, nothing spent, one frame off the lockout per tick.
            for (int tick = 1; tick < Lockout; tick++) {
                Tick(player, GameplayButtons.Special1);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"Special 1 was accepted {tick} ticks after the swing started.")
                    .IsEqual(CharacterState.Attacking);
                AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
                AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout - tick);
            }

            // T+30: legal, and it cancels the swing's chain hold.
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Special 1 must be legal on the 30th tick after the swing.")
                .IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.SpecialOneCooldownTimer > 0f).IsTrue();
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });
    }

    [TestCase]
    public void TheUltimateWaitsOutTheLockoutWithTheMeterUntouched() {
        Run(player => {
            var meter = player.GetNode<UltimateMeter>("UltimateMeter");
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            AssertThat(meter.IsFull).IsTrue();

            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < Lockout; tick++) {
                Tick(player, GameplayButtons.Ultimate);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"The Ultimate was accepted {tick} ticks after the swing started.")
                    .IsEqual(CharacterState.Attacking);
                AssertThat(meter.CurrentValue).IsEqual(UltimateMeter.MaxValue);
                AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
            }

            Tick(player, GameplayButtons.Ultimate);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The Ultimate must be legal on the 30th tick after the swing.")
                .IsEqual(CharacterState.UsingUltimate);
        });
    }

    [TestCase]
    public void ARefusedPressArmsNothing() {
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < 20; tick++) Tick(player, GameplayButtons.None);

            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("A refused press must not re-arm the lockout.")
                .IsEqual(Lockout - 20);

            for (int tick = 21; tick < Lockout; tick++) Tick(player, GameplayButtons.None);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The swing's own lockout, not the refused press, sets the next legal tick.")
                .IsEqual(CharacterState.UsingSpecial);
        });
    }

    [TestCase]
    public void TheBasicStringIsNeverRefusedAndEveryChainHitReArmsTheLockout() {
        Run(player => {
            BasicStringProfile profile = BasicComboRules.StringProfileFor("einstein");
            int openerTotal = profile.GroundStartupFrames[0]
                + BasicComboRules.GroundActiveFrames[0] + BasicComboRules.GroundRecoveryFrames[0];
            // The case only means something if hit two starts inside hit one's lockout.
            AssertThat(openerTotal < Lockout).IsTrue();

            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            int hit = 0;
            int hitStart = 0;
            for (int tick = 1; tick < 200; tick++) {
                // One press early in each of the first two hits buffers the next.
                bool press = hit < 2 && tick == hitStart + 10;
                int before = player.AbilityLockoutFramesRemaining;
                Tick(player, press ? GameplayButtons.BasicAttack : GameplayButtons.None);
                if (player.CurrentState != CharacterState.Attacking) break;
                if (player.ComboCounter == hit) {
                    AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Math.Max(0, before - 1));
                    continue;
                }
                AssertThat(player.ComboCounter).IsEqual(hit + 1);
                AssertThat(before > 0)
                    .OverrideFailureMessage($"Hit {hit + 2} should start while the lockout still runs ({before}).")
                    .IsTrue();
                AssertThat(player.AbilityLockoutFramesRemaining)
                    .OverrideFailureMessage($"Hit {hit + 2}'s start must re-arm the full lockout.")
                    .IsEqual(Lockout);
                hit = player.ComboCounter;
                hitStart = tick;
            }
            AssertThat(hit)
                .OverrideFailureMessage("The whole three-hit string must run under the lockout.")
                .IsEqual(BasicComboRules.ComboHits - 1);
        });
    }

    [TestCase]
    public void ASpecialCastArmsTheLockoutAndHoldsOffTheOtherSpecial() {
        Run(player => {
            var special1 = player.GetNode<BaseSpecial>("Special1");
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            // The ability node processes after its owner, in the same frame.
            special1._PhysicsProcess(Step);

            // Hold Block through the cast: it cancels the recovery's first frame.
            int cancelTick = -1;
            for (int tick = 1; tick < 60; tick++) {
                Tick(player, GameplayButtons.Block);
                special1._PhysicsProcess(Step);
                if (player.CurrentState == CharacterState.Blocking) {
                    cancelTick = tick;
                    break;
                }
            }
            AssertThat(cancelTick).IsEqual(special1.Data.StartupFrames + special1.Data.ActiveFrames);
            AssertThat(cancelTick < Lockout - 2).IsTrue();
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout - cancelTick);

            // Drop the stance, then press Special 2 every tick.
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            for (int tick = cancelTick + 2; tick < Lockout; tick++) {
                Tick(player, GameplayButtons.Special2);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"Special 2 was accepted {tick} ticks after Special 1.")
                    .IsEqual(CharacterState.Idle);
                AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            }

            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Special 2 must be legal 30 ticks after Special 1 was cast.")
                .IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.GetNode<BaseSpecial>("Special2").IsExecuting).IsTrue();
            AssertThat(player.SpecialTwoCooldownTimer > 0f).IsTrue();
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });
    }

    [TestCase]
    public void TheUltimatesAcceptanceArmsTheLockout() {
        Run(player => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Tick(player, GameplayButtons.Ultimate);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            Tick(player, GameplayButtons.None);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout - 1);
        });
    }

    [TestCase]
    public void TheMovementAbilityIsNeverRefusedByTheLockout() {
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            // Jump-cancel the swing's recovery while the lockout still runs.
            for (int tick = 0; tick < 40 && player.CurrentState != CharacterState.Airborne; tick++) {
                Tick(player, GameplayButtons.Jump);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            int locked = player.AbilityLockoutFramesRemaining;
            AssertThat(locked > 0).IsTrue();

            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The lockout must never refuse the movement ability.")
                .IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("The movement ability must not touch the lockout.")
                .IsEqual(locked - 1);
        });
    }

    [TestCase]
    public void TheGrabTheRollAndTheMovementAbilityNeverArmTheLockout() {
        (GameplayButtons press, CharacterState expected)[] rows = {
            (GameplayButtons.BasicAttack | GameplayButtons.Block, CharacterState.Grabbing),
            (GameplayButtons.Roll, CharacterState.Rolling),
            (GameplayButtons.MovementAbility, CharacterState.UsingMovementAbility)
        };
        foreach ((GameplayButtons press, CharacterState expected) in rows) {
            Run(player => {
                Tick(player, press);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"{press} must reach {expected}.")
                    .IsEqual(expected);
                AssertThat(player.AbilityLockoutFramesRemaining)
                    .OverrideFailureMessage($"{press} must not arm the lockout.")
                    .IsEqual(0);
            });
        }
    }

    [TestCase]
    public void SameTickPressesResolveSwingThenUltimateThenSpecialOneThenSpecialTwo() {
        // A basic swing beats a special pressed on the same tick.
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack | GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });
        // Special 1 beats Special 2, and its lockout leaves Special 2 unspent.
        Run(player => {
            Tick(player, GameplayButtons.Special1 | GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.GetNode<BaseSpecial>("Special1").IsExecuting).IsTrue();
            AssertThat(player.GetNode<BaseSpecial>("Special2").IsExecuting).IsFalse();
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
        });
        // An Ultimate the meter pays for takes the tick from a special and the
        // movement ability: the default gamepad Ultimate is the Movement +
        // Special 2 chord, so all three arrive together.
        Run(player => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            Tick(player, GameplayButtons.Special2 | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The chord's Ultimate must win the tick.")
                .IsEqual(CharacterState.UsingUltimate);
            AssertThat(player.GetNode<BaseSpecial>("Special2").IsExecuting).IsFalse();
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });
        // An Ultimate the meter cannot pay for leaves the tick to the special,
        // which in turn takes it from the movement ability.
        Run(player => {
            Tick(player, GameplayButtons.Special2 | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.GetNode<BaseSpecial>("Special2").IsExecuting).IsTrue();
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);
        });
    }

    [TestCase]
    public void ASameTickChainSwingBeatsASpecialInsideTheString() {
        BasicStringProfile profile = BasicComboRules.StringProfileFor("einstein");
        int openerTotal = profile.GroundStartupFrames[0]
            + BasicComboRules.GroundActiveFrames[0] + BasicComboRules.GroundRecoveryFrames[0];
        int secondTotal = profile.GroundStartupFrames[1]
            + BasicComboRules.GroundActiveFrames[1] + BasicComboRules.GroundRecoveryFrames[1];

        // In the chain hold, after the opener's lockout ran out: a BasicAttack
        // and Special 1 pressed together continue the string (the sim starts
        // the swing before any special is polled).
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < Lockout + 4; tick++) Tick(player, GameplayButtons.None);
            AssertThat(Lockout + 4 < openerTotal + BasicComboRules.ChainHoldFrames)
                .OverrideFailureMessage("Harness: the press must land inside the chain hold.")
                .IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);

            Tick(player, GameplayButtons.BasicAttack | GameplayButtons.Special1);
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("Hit 2 must start in the chain hold.")
                .IsEqual(1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });

        // The hold also restarts on BasicAttack merely held (a press the hold
        // could not take, e.g. one made in hitstop): that start wins too.
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < Lockout + 4; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.ComboCounter).IsEqual(0);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);

            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(
                0, 0f, 0f,
                GameplayButtons.BasicAttack | GameplayButtons.Special1,
                previousHeld: GameplayButtons.BasicAttack));
            InputManager.Instance.SetInputSource(player.PlayerIndex, source);
            player._PhysicsProcess(Step);
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("A held BasicAttack must restart the chain over a same-tick special.")
                .IsEqual(1);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });

        // A buffered hit 3 starts on hit 2's last frame, which is no earlier
        // than the tick hit 2's lockout runs out: a Special 1 pressed on that
        // tick loses to the swing.
        Run(player => {
            AssertThat(secondTotal >= Lockout)
                .OverrideFailureMessage("Harness: hit 2 must outlast its own lockout.")
                .IsTrue();
            Tick(player, GameplayButtons.BasicAttack);
            int secondStart = -1;
            for (int tick = 1; tick < 120 && secondStart < 0; tick++) {
                Tick(player, tick == 10 ? GameplayButtons.BasicAttack : GameplayButtons.None);
                if (player.ComboCounter == 1) secondStart = tick;
            }
            AssertThat(secondStart > 0).IsTrue();
            for (int tick = secondStart + 1; tick < secondStart + secondTotal; tick++) {
                Tick(player, tick == secondStart + 10 ? GameplayButtons.BasicAttack : GameplayButtons.None);
            }
            AssertThat(player.ComboCounter).IsEqual(1);

            Tick(player, GameplayButtons.Special1);
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("The buffered hit 3 must start on hit 2's last frame.")
                .IsEqual(2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
        });
    }

    [TestCase]
    public void ADeathRewindOrACheckpointRestoreClearsTheLockout() {
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            player.CompleteStoryRewind(player.GlobalPosition, player.CurrentHP, grounded: true);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A death rewind must leave the special legal at once.")
                .IsEqual(CharacterState.UsingSpecial);
        });
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            player.RestoreStoryCheckpoint(player.GlobalPosition, player.CurrentHP, 0f);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A checkpoint restore must leave the special legal at once.")
                .IsEqual(CharacterState.UsingSpecial);
        });
    }

    [TestCase]
    public void HitstopAndTimeFreezeHoldTheLockout() {
        Run(player => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 0; tick < 4; tick++) Tick(player, GameplayButtons.None);
            int held = Lockout - 4;
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(held);

            // The fighter's own hitstop (the seam a landed hit uses).
            const int freeze = 6;
            player.ApplyHitstop(freeze);
            for (int tick = 0; tick < freeze; tick++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.AbilityLockoutFramesRemaining)
                    .OverrideFailureMessage($"The lockout ticked inside hitstop (frame {tick}).")
                    .IsEqual(held);
            }
            AssertThat(player.IsInHitstop).IsFalse();
            Tick(player, GameplayButtons.None);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(held - 1);

            // Time Freeze suspends it with the ability cooldowns.
            player.TimeFrozen = true;
            for (int tick = 0; tick < 5; tick++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.AbilityLockoutFramesRemaining)
                    .OverrideFailureMessage($"The lockout ticked under Time Freeze (frame {tick}).")
                    .IsEqual(held - 1);
            }
            player.TimeFrozen = false;
            Tick(player, GameplayButtons.None);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(held - 2);
        });
    }

    // === helpers ==============================================================

    private static void Run(Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "AbilityLockoutHost" };
        tree.Root.AddChild(host);
        // Scoped, so a cast's shot or zone never parks in the autoload's pool.
        var pools = new PoolManager { Name = "AbilityLockoutPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "AbilityLockoutFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
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
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
            body(player);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    /// <summary>One controller tick; every held button reads as freshly pressed.</summary>
    private static void Tick(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
