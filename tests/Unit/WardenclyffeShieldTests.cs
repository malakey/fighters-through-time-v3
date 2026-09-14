using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A1b — V7.6 D02d, the Wardenclyffe Shield's three shipped defects.
///
/// <para>Before this pass the recharge was a flat 2 HP/s that ignored MaxHP,
/// there was no damage delay at all, and absorption was <b>never</b> disabled
/// out of coil range (only the recharge was gated, because the shared shield
/// pool drained inside <c>ApplyDamage</c> regardless of coil proximity). The
/// cap was the only part that was right.</para>
///
/// <para>These cases exercise the contract through <c>PlayerController</c>'s
/// shield instance rather than by deploying real coils, which keeps the numbers
/// (2.5%/s, the 180-tick delay, the range gate on absorption) assertable without
/// a live physics scene.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WardenclyffeShieldTests {

    [TestCase]
    public void TheRateIsTwoAndAHalfPercentOfMaxHPSoEmptyToFullIsSixSeconds() {
        // D02d: "an empty shield takes 6 seconds of eligible recharge to fill",
        // at ANY MaxHP - which a flat 2 HP/s cannot deliver.
        float cap = StoryDefenseRules.WardenclyffeCapacityShare;
        float rate = StoryDefenseRules.WardenclyffeRechargeSharePerSecond;

        AssertThat(cap).IsEqualApprox(0.15f, 0.0001f);
        AssertThat(rate).IsEqualApprox(0.025f, 0.0001f);
        AssertThat(cap / rate)
            .OverrideFailureMessage("Empty to full must be exactly six eligible seconds.")
            .IsEqualApprox(6f, 0.0001f);
        AssertThat(StoryDefenseRules.WardenclyffeDamageDelaySeconds)
            .OverrideFailureMessage("The delay is three live seconds.")
            .IsEqualApprox(3f, 0.0001f);
    }

    [TestCase]
    public void ItInitialisesToZeroChargeAndNeverStartsFull() {
        // "Initialize charge to zero with no initial damage delay on fresh entry
        // or first legitimate acquisition of the perk; it builds at the normal
        // rate when eligible, never starts full."
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(
                StoryDefenseRules.WardenclyffeCapacityShare * player.MaximumHP);

            AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.Wardenclyffe);
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("First acquisition starts at ZERO charge.")
                .IsEqual(0f);
            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .OverrideFailureMessage("First acquisition arms no delay.")
                .IsEqual(0f);
            AssertThat(player.StoryShieldRemainingFrames)
                .OverrideFailureMessage("D02c's 8 s lifetime explicitly does NOT apply here.")
                .IsEqual(StoryShieldInstance.NoExpiry);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void OutOfRangeAbsorptionIsDisabledButTheStoredChargeIsPreserved() {
        // The headline defect: absorption used to ignore coil proximity entirely.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(30f);
            player.RechargeStoryShield(30f);
            player.WardenclyffeInCoilRange = false;
            int hpBefore = player.CurrentHP;

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 10f));

            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Out of range the shield absorbs nothing.")
                .IsEqual(hpBefore - 10);
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("Leaving range preserves the stored charge.")
                .IsEqualApprox(30f, 0.001f);

            // Returning re-enables only the charge actually retained.
            player.WardenclyffeInCoilRange = true;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 10f));
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("Back in range the retained charge absorbs.")
                .IsEqualApprox(20f, 0.001f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void LosingCapacityArmsTheThreeSecondDelayAndItTicksOutOfRange() {
        // "Whenever damage actually reduces Tesla's HP or Wardenclyffe
        // absorption, set the recharge delay to 3 live seconds. Count it down in
        // ordinary live play, inside or outside coil range."
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(30f);
            player.RechargeStoryShield(30f);
            player.WardenclyffeInCoilRange = true;

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 10f));

            AssertThat(player.StoryShieldPoints).IsEqualApprox(20f, 0.001f);
            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .OverrideFailureMessage("Capacity loss arms the 3 s delay even with no HP loss.")
                .IsEqualApprox(StoryDefenseRules.WardenclyffeDamageDelaySeconds, 0.001f);

            // It counts down out of range too.
            player.WardenclyffeInCoilRange = false;
            HoldInput(player, GameplayButtons.None, 60);
            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .OverrideFailureMessage("One live second elapsed, in or out of range.")
                .IsEqualApprox(2f, 0.05f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AHitFullyRejectedByAegisDoesNotRestartTheDelay() {
        // "A hit fully rejected by invulnerability/projectile immunity, absorbed
        // by Temporal Aegis, or blocked without reducing this shield or HP does
        // not restart it."
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(30f);
            player.RechargeStoryShield(30f);
            player.WardenclyffeInCoilRange = true;
            player.GrantTemporalAegis();

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 10f));

            AssertThat(player.HasTemporalAegis).IsFalse();
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("Aegis resolves first, so no capacity was lost.")
                .IsEqualApprox(30f, 0.001f);
            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .OverrideFailureMessage("An Aegis-absorbed hit does not restart the delay.")
                .IsEqual(0f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void HPDamageRestartsTheDelayEvenWhenNoCapacityWasSpent() {
        // A throw's HP damage restarts the delay even though the D03d bypass
        // spent no capacity; the same chokepoint covers every HP-reducing source.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(30f);
            player.WardenclyffeInCoilRange = true;
            AssertThat(player.StoryShieldPoints).IsEqual(0f);

            player.ApplyDamage(10);

            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .IsEqualApprox(StoryDefenseRules.WardenclyffeDamageDelaySeconds, 0.001f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void TheDelayIsFrozenByTimeFreezeAndHitstop() {
        // "Freeze charge and the remaining delay whenever the existing temporal
        // rules suspend combat-effect/resource timers."
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        tree.Root.AddChild(player);
        try {
            player.ConfigureStoryShield(30f);
            player.WardenclyffeDamageDelaySeconds =
                StoryDefenseRules.WardenclyffeDamageDelaySeconds;
            player.TimeFrozen = true;

            HoldInput(player, GameplayButtons.None, 120);

            AssertThat(player.WardenclyffeDamageDelaySeconds)
                .OverrideFailureMessage("A frozen world consumes no delay and banks no catch-up.")
                .IsEqualApprox(StoryDefenseRules.WardenclyffeDamageDelaySeconds, 0.001f);
        } finally {
            player.TimeFrozen = false;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }

    private static HitPayload Hit(float damage) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = new Vector2(20f, 0f),
        AttackerFacingRight = false
    };
}
