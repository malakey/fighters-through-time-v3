using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// MM-SDEFY (meter and Defy update 2026-10-31, plan 2.4 to 2.7): the Story active Defy History.
/// The press at 50 Influence, the frame table (1 to 6 invulnerable, the blast on 6, recovery to
/// 30, free on 31), legality from any state (O9), the refusals (P21), the hitstop buffer (O10)
/// and what the action ignores (the lockout, Suppression, the Legacy Unlock Schedule).
/// The blast itself is <c>StoryDefyBlastTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryDefyTests {

    [TestCase]
    public void APressAtFiftyBeginsADefySpendsExactlyFiftyAndArmsNoLockout() {
        Run(player => {
            player.SetUltimateMeter(80f);
            AssertThat(player.CanDefyNow).IsTrue();
            Step(player, GameplayButtons.Defy);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Defying);
            AssertThat(player.DefyFrame).IsEqual(1);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("A Defy spends exactly the 50 Influence cost, unscaled.")
                .IsEqualApprox(80f - BasicComboRules.DefyInfluenceCost, 0.001f);
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("Defy arms neither lockout (5.4).")
                .IsEqual(0);
            AssertThat(player.SpecialGateFramesRemaining).IsEqual(0);
        });
    }

    [TestCase]
    public void BelowFiftyInfluenceThePressDoesNothing() {
        Run(player => {
            player.SetUltimateMeter(BasicComboRules.DefyInfluenceCost - 0.5f);
            AssertThat(player.CanDefyNow).IsFalse();
            Step(player, GameplayButtons.Defy);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Defying);
            AssertThat(player.CurrentUltimateMeter)
                .IsEqualApprox(BasicComboRules.DefyInfluenceCost - 0.5f, 0.001f);
        });
    }

    [TestCase]
    public void FramesOneToSixAreInvulnerableAndSevenOnIsNot() {
        Run(player => {
            player.SetUltimateMeter(100f);
            Step(player, GameplayButtons.Defy);
            for (int expected = 1; expected <= BasicComboRules.DefyInvulnerableFrames; expected++) {
                AssertThat(player.DefyFrame).IsEqual(expected);
                AssertThat(player.IsDefyProtected).IsTrue();
                int hp = player.CurrentHP;
                AssertThat(player.ApplyEnvironmentalDamage(20))
                    .OverrideFailureMessage($"Frame {expected} must refuse a damaging contact.")
                    .IsEqual(0);
                AssertThat(player.CurrentHP).IsEqual(hp);
                if (expected == BasicComboRules.DefyInvulnerableFrames) break;
                Step(player, GameplayButtons.None);
            }
            // The blast froze the Defier for 8 frames; it stays on frame 6, still protected.
            AssertThat(player.IsInHitstop).IsTrue();
            for (int i = 0; i < BasicComboRules.DefyBlastHitstopFrames; i++) {
                AssertThat(player.IsDefyProtected).IsTrue();
                Step(player, GameplayButtons.None);
            }
            Step(player, GameplayButtons.None);
            AssertThat(player.DefyFrame).IsEqual(BasicComboRules.DefyBlastFrame + 1);
            AssertThat(player.IsDefyProtected)
                .OverrideFailureMessage("Frame 7 is the vulnerable recovery.")
                .IsFalse();
            AssertThat(player.ApplyEnvironmentalDamage(5)).IsGreater(0);
        });
    }

    [TestCase]
    public void TheDefierIsFreeThirtyEightFramesAfterThePress() {
        Run(player => {
            player.SetUltimateMeter(100f);
            Step(player, GameplayButtons.Defy);
            int ticks = 1;
            while (player.IsDefying && ticks < 100) {
                Step(player, GameplayButtons.None);
                ticks++;
            }
            // Press tick, 30 Defy frames and the 8-frame blast freeze are 39 ticks, and the
            // hero acts on the last of them: free 38 frames after the press (P11).
            AssertThat(ticks)
                .OverrideFailureMessage("The Defy lasts 30 frames plus the 8-frame blast freeze.")
                .IsEqual(BasicComboRules.DefyTotalFrames + BasicComboRules.DefyBlastHitstopFrames + 1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.DefyFrame).IsEqual(0);
        });
    }

    [TestCase]
    public void TheDefierHasNoVelocityAndNoGravityThroughFrameSix() {
        Run(player => {
            player.GlobalPosition = new Vector2(0f, -400f);
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = new Vector2(300f, 200f);
            player.SetUltimateMeter(100f);
            Step(player, GameplayButtons.Defy);
            float y = player.GlobalPosition.Y;
            AssertThat(player.Velocity).IsEqual(Vector2.Zero);
            for (int frame = 2; frame <= BasicComboRules.DefyBlastFrame; frame++) {
                Step(player, GameplayButtons.None);
                AssertThat(player.Velocity)
                    .OverrideFailureMessage($"Frame {frame}: no velocity, no gravity.")
                    .IsEqual(Vector2.Zero);
            }
            AssertThat(player.GlobalPosition.Y).IsEqualApprox(y, 0.01f);
            for (int i = 0; i < BasicComboRules.DefyBlastHitstopFrames + 3; i++) Step(player, GameplayButtons.None);
            AssertThat(player.Velocity.Y)
                .OverrideFailureMessage("Frames 7 on fall under normal gravity.")
                .IsGreater(0f);
            AssertThat(player.Velocity.X).IsEqual(0f);
        });
    }

    [TestCase]
    public void DefyIsLegalFromAnySwingStunBlockRollOrDazeState() {
        var states = new Dictionary<string, System.Action<PlayerController>> {
            ["swing"] = p => { Step(p, GameplayButtons.BasicAttack); AssertThat(p.CurrentState).IsEqual(CharacterState.Attacking); },
            ["hitstun"] = p => { p.ApplyStun(0.5f); AssertThat(p.CurrentState).IsEqual(CharacterState.Stunned); },
            ["block"] = p => { Step(p, GameplayButtons.Block); AssertThat(p.CurrentState).IsEqual(CharacterState.Blocking); },
            ["roll"] = p => { Step(p, GameplayButtons.Roll); AssertThat(p.CurrentState).IsEqual(CharacterState.Rolling); },
            ["daze"] = p => p.TransitionTo(CharacterState.Dazed),
        };
        foreach (KeyValuePair<string, System.Action<PlayerController>> entry in states) {
            Run(player => {
                player.SetUltimateMeter(100f);
                entry.Value(player);
                Step(player, GameplayButtons.Defy);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"A Defy must begin from {entry.Key}.")
                    .IsEqual(CharacterState.Defying);
                AssertThat(player.DefyFrame).IsEqual(1);
            });
        }
    }

    [TestCase]
    public void TheRefusalsOfP21LeaveTheMeterAlone() {
        var setups = new Dictionary<string, System.Action<PlayerController>> {
            ["time freeze"] = p => p.TimeFrozen = true,
            ["scripted"] = p => p.DefyDisabledByScript = true,
            ["own ultimate"] = p => p.TransitionTo(CharacterState.UsingUltimate),
            ["dead"] = p => p.TransitionTo(CharacterState.Dead),
            ["rewind"] = p => p.SetRewindSuspended(true),
        };
        foreach (KeyValuePair<string, System.Action<PlayerController>> entry in setups) {
            Run(player => {
                player.SetUltimateMeter(100f);
                entry.Value(player);
                AssertThat(player.CanDefyNow).OverrideFailureMessage($"{entry.Key} must refuse.").IsFalse();
                Step(player, GameplayButtons.Defy);
                AssertThat(player.IsDefying).OverrideFailureMessage($"{entry.Key} must not begin a Defy.").IsFalse();
                AssertThat(player.CurrentUltimateMeter).IsEqual(100f);
                player.TimeFrozen = false;
                player.SetRewindSuspended(false);
            });
        }
    }

    [TestCase]
    public void DefyIgnoresTheAbilityLockoutSuppressionAndDormantSlotsAndArmsNone() {
        Run(player => {
            player.SetUltimateMeter(100f);
            player.ApplyLegacyUnlockLocks(new HashSet<AbilitySlot>());
            player.ApplyStatusEffect(StatusType.Suppression, 2f, 1f);
            AssertThat(player.IsAbilityCastSuppressed).IsTrue();
            // A swing arms the 30-frame lockout; Defy neither reads nor re-arms it.
            Step(player, GameplayButtons.BasicAttack);
            int lockout = player.AbilityLockoutFramesRemaining;
            AssertThat(lockout).IsGreater(0);
            Step(player, GameplayButtons.Defy);
            AssertThat(player.IsDefying)
                .OverrideFailureMessage("Suppression, the lockout and Dormant slots do not gate Defy (5.3, 5.4).")
                .IsTrue();
            AssertThat(player.AbilityLockoutFramesRemaining)
                .OverrideFailureMessage("Defy must not re-arm the lockout.")
                .IsLessEqual(lockout);
        });
    }

    [TestCase]
    public void APressInsideTheDefiersOwnHitstopIsHeldAndBeginsOnTheThawTick() {
        Run(player => {
            player.SetUltimateMeter(100f);
            player.ApplyHitstop(3);
            Step(player, GameplayButtons.Defy); // frozen: held, not begun
            AssertThat(player.IsDefying).IsFalse();
            AssertThat(player.CurrentUltimateMeter).IsEqual(100f);
            Step(player, GameplayButtons.None);
            Step(player, GameplayButtons.None);
            AssertThat(player.IsInHitstop).IsFalse();
            AssertThat(player.IsDefying)
                .OverrideFailureMessage("The thaw tick has not run yet.")
                .IsFalse();
            Step(player, GameplayButtons.None); // the first unfrozen tick
            AssertThat(player.IsDefying)
                .OverrideFailureMessage("O10: the held press begins on the thaw tick.")
                .IsTrue();
            AssertThat(player.DefyFrame).IsEqual(1);
            AssertThat(player.CurrentUltimateMeter).IsEqualApprox(50f, 0.001f);
        });
    }

    [TestCase]
    public void DefyClaimsItsTickOverASpecialPress() {
        Run(player => {
            player.SetUltimateMeter(100f);
            Step(player, GameplayButtons.Defy | GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("O11: no Special starts the same tick as a Defy.")
                .IsEqual(CharacterState.Defying);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
        });
    }

    [TestCase]
    public void TheBlastFramePublishesOneDefyExecutedWithTheMeterBeforeAndTheHeal() {
        Run(player => {
            player.SetUltimateMeter(70f);
            player.ApplyDamage(40);
            int hp = player.CurrentHP;
            var seen = new List<DefyExecutedPayload>();
            void Handler(DefyExecutedPayload p) => seen.Add(p);
            EventBus.Instance.OnDefyExecuted += Handler;
            try {
                Step(player, GameplayButtons.Defy);
                for (int i = 0; i < 12; i++) Step(player, GameplayButtons.None);
            } finally {
                EventBus.Instance.OnDefyExecuted -= Handler;
            }
            AssertThat(seen.Count).IsEqual(1);
            AssertThat(seen[0].PlayerIndex).IsEqual(player.PlayerIndex);
            AssertThat(seen[0].MeterBefore).IsEqualApprox(70f, 0.001f);
            AssertThat(seen[0].Healed).IsGreater(0);
            AssertThat(player.CurrentHP).IsEqual(hp + seen[0].Healed);
            AssertThat(player.DefyExecutionCount).IsEqual(1);
        });
    }

    // === Harness ===

    private static void Run(System.Action<PlayerController> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var floor = new StaticBody2D {
            Name = "DefyFloor", CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        tree.Root.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter(
            "einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        tree.Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 10; frame++) Step(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            body(player);
        } finally {
            player.TimeFrozen = false;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    private static void Step(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
