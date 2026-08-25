using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit H-4: Story abilities must be interruptible. A landed stun (or death)
/// cancels an executing special/ultimate — no further phase advancement, damage
/// ticks, or steering — and an expiring ability phase never stomps the
/// interposed Stunned state. Hyper-armor remains the designed exception: while
/// an armor window covers the incoming attack class, the cast completes.
/// Also pins the design-776 rule that a cooldown starts on the first frame of
/// the cast action (and therefore survives an interrupted cast).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityInterruptionTests {

    [TestCase]
    public void CooldownStartsOnTheCastFrameNotAtTheActivePhase() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Special1);

            var ability = player.GetNode<BaseSpecial>("Special1");
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            // Still in startup: the active phase has not begun, yet the cooldown
            // is already running at the full authored duration.
            AssertThat((int)ability.CurrentPhase).IsEqual((int)AbilityPhase.Startup);
            AssertThat(player.SpecialOneCooldownTimer)
                .IsEqualApprox(ability.Data.CooldownDuration, 0.001f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void StunMidSpecialInterruptsTheCastPreservesTheStunAndKeepsTheCooldown() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Special1);
            var ability = player.GetNode<BaseSpecial>("Special1");
            AssertThat(ability.IsExecuting).IsTrue();

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic, hitstunSeconds: 0.5f));

            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(ability.IsExecuting).IsFalse();
            // The interrupted cast still burned its cooldown (design 776).
            AssertThat(player.SpecialOneCooldownTimer > 0f).IsTrue();

            // Pump the ability node far past every authored phase: an expiring
            // phase timer must never advance again nor stomp the stun state.
            for (int frame = 0; frame < 120; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat((int)ability.CurrentPhase).IsEqual((int)AbilityPhase.Inactive);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);

            // The V7.1 hitstop from the interrupting hit froze the player's
            // clock; run the short freeze off in real 60 Hz steps first, then
            // the stun itself runs its normal course.
            for (int frame = 0; frame < BasicComboRules.HitstopFrames(10); frame++) {
                SendInput(player, GameplayButtons.None);
            }
            SendInput(player, GameplayButtons.None, 0.6);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Stunned);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void HyperArmoredCastCompletesThroughABasicHitButAnUltimateInterruptsIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.MovementAbility);
            var ability = player.GetNode<BaseSpecial>("MovementAbility");
            AssertThat(ability.IsExecuting).IsTrue();
            AssertThat(player.HasActiveHyperArmor).IsTrue();

            Hurtbox hurtbox = player.GetNode<Hurtbox>("Hurtbox");
            hurtbox.TakeHit(Hit(AttackClass.Basic));
            // Armor window covers basics: no stun, the charge keeps executing.
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(ability.IsExecuting).IsTrue();

            hurtbox.TakeHit(Hit(AttackClass.Ultimate));
            // Ultimates bypass hyper-armor: the stun lands and the cast dies.
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(ability.IsExecuting).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void UltimateInterruptedMidChargeStopsFiringAndReleasesSteering() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("joan");
        tree.Root.AddChild(player);
        try {
            player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Ultimate);
            var ability = player.GetNode<BaseSpecial>("Ultimate");
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(ability.IsExecuting).IsTrue();

            // Advance the cavalry charge into its active window: the ability
            // force-sets the owner's horizontal velocity every frame.
            int startupFrames = ability.Data.StartupFrames;
            for (int frame = 0; frame <= startupFrames; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat((int)ability.CurrentPhase).IsEqual((int)AbilityPhase.Active);
            float chargeSpeed = ability.Data.ProjectileSpeed;
            AssertThat(Mathf.Abs(player.Velocity.X)).IsEqualApprox(chargeSpeed, 0.01f);

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(AttackClass.Basic, hitstunSeconds: 0.5f));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(ability.IsExecuting).IsFalse();

            // Steering is released: pumping the ability no longer force-sets the
            // charge velocity over the knockback the hit imposed.
            Vector2 velocityAfterHit = player.Velocity;
            AssertThat(Mathf.Abs(velocityAfterHit.X) < chargeSpeed).IsTrue();
            for (int frame = 0; frame < 60; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(player.Velocity).IsEqual(velocityAfterHit);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void DeathMidCastInterruptsTheAbilityUnconditionally() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        tree.Root.AddChild(player);
        try {
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.MovementAbility);
            var ability = player.GetNode<BaseSpecial>("MovementAbility");
            AssertThat(ability.IsExecuting).IsTrue();
            AssertThat(player.HasActiveHyperArmor).IsTrue();

            // Hyper-armor prevents hitstun, never death: lethal damage ends the
            // cast even through the armor window.
            player.ApplyDamage(player.MaximumHP);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dead);
            AssertThat(ability.IsExecuting).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    private static void SendInput(PlayerController player, GameplayButtons buttons, double delta = 1.0 / 60.0) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(delta);
    }

    private static HitPayload Hit(AttackClass attackClass, float hitstunSeconds = 0.25f) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = attackClass,
        Damage = 10f,
        Knockback = new Vector2(4f, -2f),
        HitstunDuration = hitstunSeconds,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
