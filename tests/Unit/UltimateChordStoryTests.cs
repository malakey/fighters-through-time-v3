using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 9.2 (2026-10-16, CM-INPUT; ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>,
/// closing <c>VERIFY-GAMEPAD-ULTIMATE-CHORD</c> (a)): the pad Ultimate chord no
/// longer leaks its Movement Ability component. Before, LB a frame ahead of RB
/// cast the movement ability, and so did the whole chord inside the ability
/// lockout (the held-back Ultimate claims the tick from the Specials, never from
/// the movement ability).
///
/// <para>Real physics frames on purpose: the hero samples a fake pad through the
/// live <see cref="InputManager"/> (device sampling, the chord window and the
/// hero's own readiness report together), which a synchronous
/// <c>_PhysicsProcess</c> loop cannot do because the manager caches one frame
/// per engine tick.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UltimateChordStoryTests {
    private const int Pad = 0;
    private static readonly Vector2 Origin = new(52000f, 0f);

    [TestCase]
    public async Task AFullMeterChordInsideTheLockoutCastsNoMovementAbility() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        var host = new Node2D { Name = "UltimateChordStoryHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "UltimateChordStoryPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "UltimateChordStoryFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        hero.Position = Origin + new Vector2(0f, -8f);
        JoyButton attack = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.BasicAttack);
        JoyButton jump = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Jump);
        JoyButton lb = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.MovementAbility);
        JoyButton rb = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Special2);
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, Pad);
        try {
            input.AssignJoypadToPlayer(Pad, 0);
            host.AddChild(hero);
            for (int frame = 0; frame < 180 && !(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle); frame++) {
                await Frames(tree, 1);
            }
            AssertThat(hero.IsOnFloor() && hero.CurrentState == CharacterState.Idle)
                .OverrideFailureMessage($"Harness: the hero is {hero.CurrentState}, on floor {hero.IsOnFloor()}.")
                .IsTrue();
            hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
            await Frames(tree, 2);
            AssertThat(hero.IsUltimateAffordableForInput).IsTrue();

            // A swing arms the 30-frame lockout; a jump out of its recovery puts
            // the hero in the air (where the movement ability is legal) with the
            // lockout still running.
            probe.SetButton(Pad, attack, true);
            for (int frame = 0; frame < 6 && hero.CurrentState != CharacterState.Attacking; frame++) await Frames(tree, 1);
            probe.SetButton(Pad, attack, false);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Attacking);
            await Frames(tree, 14);
            probe.SetButton(Pad, jump, true);
            for (int frame = 0; frame < 12 && hero.CurrentState == CharacterState.Attacking; frame++) await Frames(tree, 1);
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage($"Harness: the jump did not cancel the swing ({hero.CurrentState}).")
                .IsEqual(CharacterState.Airborne);
            int lockoutAtChord = hero.AbilityLockoutFramesRemaining;
            AssertThat(lockoutAtChord > 6)
                .OverrideFailureMessage($"Harness: only {lockoutAtChord} lockout frames left for the chord.")
                .IsTrue();

            // LB first, RB a frame later: inside the window, so only the Ultimate
            // is pressed, which the lockout then refuses.
            probe.SetButton(Pad, lb, true);
            await Frames(tree, 1);
            probe.SetButton(Pad, rb, true);
            for (int frame = 0; frame < 8; frame++) {
                await Frames(tree, 1);
                AssertThat(hero.CurrentState)
                    .OverrideFailureMessage($"The chord cast the movement ability inside the lockout (frame {frame}).")
                    .IsNotEqual(CharacterState.UsingMovementAbility);
                AssertThat(hero.CurrentState).IsNotEqual(CharacterState.UsingUltimate);
            }
            AssertThat(hero.MovementAbilityCooldownTimer)
                .OverrideFailureMessage("The movement ability's cooldown started: the chord leaked LB.")
                .IsEqual(0f);
            AssertThat(hero.CurrentUltimateMeter).IsEqual(InfluenceMeterRules.MaxValue);

            // Control: a lone LB is only delayed — once its window runs out the
            // movement ability casts, so the setup could have leaked it.
            probe.SetButton(Pad, lb, false);
            probe.SetButton(Pad, rb, false);
            probe.SetButton(Pad, jump, false);
            await Frames(tree, 2);
            probe.SetButton(Pad, lb, true);
            bool cast = false;
            for (int frame = 0; frame < UltimateChordWindow.LeniencyFrames + 6 && !cast; frame++) {
                await Frames(tree, 1);
                cast = hero.CurrentState == CharacterState.UsingMovementAbility || hero.MovementAbilityCooldownTimer > 0f;
            }
            AssertThat(cast)
                .OverrideFailureMessage("Control: a lone LB never cast the movement ability.")
                .IsTrue();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// CM-FIXA-INPUT (2026-10-16): the live <see cref="InputManager"/>'s pause /
    /// focus-loss notification forgets chord waits but keeps the masks. LB + RB
    /// that cast the Ultimate and are still held through a pause must not reach
    /// the resume tick as fresh Movement Ability and Special 2 presses once the
    /// meter is spent. Until the fix the notification dropped the whole window.
    /// No hero: the test reports the slot's readiness itself, once per tick.
    /// </summary>
    [TestCase]
    public async Task AChordHeldAcrossAPauseFiresNoComponentOnTheWayBack() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        InputManager input = InputManager.Instance;
        AssertObject(input).IsNotNull();
        JoyButton lb = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.MovementAbility);
        JoyButton rb = FakeInputDeviceProbe.PadButtonFor(InputManager.Actions.Special2);
        FakeInputDeviceProbe probe = FakeInputDeviceProbe.Install(input, Pad);
        try {
            input.AssignJoypadToPlayer(Pad, 0);
            await ReportedFrames(tree, input, affordable: true, 2);

            probe.SetButton(Pad, lb, true);
            probe.SetButton(Pad, rb, true);
            await ReportedFrames(tree, input, affordable: true, 1);
            PlayerInputFrame chord = input.GetFrame(0);
            AssertThat(chord.IsHeld(GameplayButtons.Ultimate) && !Leaks(chord))
                .OverrideFailureMessage($"Harness: the affordable chord carried {chord.Held}.")
                .IsTrue();

            // The Ultimate is cast: the meter is spent, the shoulders stay down.
            await ReportedFrames(tree, input, affordable: false, 2);
            PlayerInputFrame spent = input.GetFrame(0);
            AssertThat(spent.IsHeld(GameplayButtons.Ultimate) && !Leaks(spent)).IsTrue();

            input.Notification((int)Node.NotificationPaused);
            for (int frame = 0; frame < 2 * UltimateChordWindow.LeniencyFrames; frame++) {
                await ReportedFrames(tree, input, affordable: false, 1);
                PlayerInputFrame resumed = input.GetFrame(0);
                AssertThat(Leaks(resumed))
                    .OverrideFailureMessage($"Frame {frame} after the pause: the held chord leaked {resumed.Held}.")
                    .IsFalse();
                AssertThat(resumed.IsHeld(GameplayButtons.Ultimate)).IsTrue();
            }

            // Letting go fires nothing either.
            probe.SetButton(Pad, lb, false);
            await ReportedFrames(tree, input, affordable: false, 1);
            AssertThat(Leaks(input.GetFrame(0)) || input.GetFrame(0).IsHeld(GameplayButtons.Ultimate)).IsFalse();
            probe.SetButton(Pad, rb, false);
            await ReportedFrames(tree, input, affordable: false, 1);
            AssertThat(Leaks(input.GetFrame(0)) || input.GetFrame(0).IsHeld(GameplayButtons.Ultimate)).IsFalse();

            // Control: the slot is live — a fresh LB is an ordinary press now.
            probe.SetButton(Pad, lb, true);
            await ReportedFrames(tree, input, affordable: false, 1);
            AssertThat(input.GetFrame(0).IsPressed(GameplayButtons.MovementAbility))
                .OverrideFailureMessage("Control: a fresh LB after the chord was not a press.")
                .IsTrue();
        } finally {
            probe.ReleaseAll();
            FakeInputDeviceProbe.Restore(input);
        }
    }

    /// <summary>Whether a frame carries either chord component.</summary>
    private static bool Leaks(in PlayerInputFrame frame) =>
        frame.IsHeld(GameplayButtons.MovementAbility) || frame.IsHeld(GameplayButtons.Special2);

    /// <summary>Waits <paramref name="count"/> physics frames, reporting player 1's Ultimate readiness before each.</summary>
    private static async Task ReportedFrames(SceneTree tree, InputManager input, bool affordable, int count) {
        for (int frame = 0; frame < count; frame++) {
            input.ReportUltimateAffordable(0, affordable);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
    }

    private static async Task Frames(SceneTree tree, int count) {
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }
}
