using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A1b — the Story half of the V7.6 defensive contract.
///
/// <para>Before this pass the finite HP barrier drained inside
/// <c>ApplyDamage</c>, i.e. <b>after</b> block — exactly backwards under D01. A
/// blocking Shakespeare with a live Henry's Bastion spent a block charge, and
/// could shatter, on a hit the guard should have absorbed for free. The order
/// is now invulnerability → projectile immunity → Temporal Aegis → HP barrier →
/// block → HP, resolved in <c>PlayerController.ResolveIncomingHit</c>, and it
/// stops at the first layer that fully prevents the hit.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryDefenceOrderTests {

    [TestCase]
    public void AFullyAbsorbedHitSpendsNoBlockChargeAndTouchesNoHP() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("shakespeare");
        tree.Root.AddChild(player);
        try {
            var block = player.GetNode<BlockSystem>("BlockSystem");
            player.GrantStoryShield(StoryShieldEffect.HenrysBastion, capacity: 30f, grantEventId: 1);
            player.TransitionTo(CharacterState.Blocking);
            block.StartBlock();
            int hpBefore = player.CurrentHP;

            float dealt = player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 20f));

            AssertThat(dealt).IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("The barrier absorbs the whole 20.")
                .IsEqualApprox(10f, 0.001f);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage(
                    "Holding Block does not turn a shield absorption into a successful block.")
                .IsEqual(3);
            AssertThat(block.IsInShieldStun)
                .OverrideFailureMessage("A shield absorption produces no block response.")
                .IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void APartialAbsorptionPassesOnlyTheRemainderToBlockAtOneCharge() {
        // D01's worked example: a 30-damage Basic hit meets a barrier with 10
        // left. The barrier eats 10; a legal block absorbs the remaining 20 for
        // exactly ONE charge, at the original Basic classification.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("shakespeare");
        tree.Root.AddChild(player);
        try {
            var block = player.GetNode<BlockSystem>("BlockSystem");
            player.GrantStoryShield(StoryShieldEffect.HenrysBastion, capacity: 10f, grantEventId: 1);
            player.TransitionTo(CharacterState.Blocking);
            block.StartBlock();
            int hpBefore = player.CurrentHP;

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 30f));

            AssertThat(player.StoryShieldPoints).IsEqual(0f);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A Basic remainder costs exactly one charge.")
                .IsEqual(2);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A fully blocked remainder deals no HP damage.")
                .IsEqual(hpBefore);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void TemporalAegisResolvesBeforeTheBarrierAndLeavesItIntact() {
        // D02b: with a live barrier AND an Aegis, an eligible hit consumes the
        // Aegis and leaves the barrier untouched, even though the barrier alone
        // could have absorbed the whole thing. Story had no Aegis at all before
        // this pass.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("shakespeare");
        tree.Root.AddChild(player);
        try {
            player.GrantStoryShield(StoryShieldEffect.HenrysBastion, capacity: 10f, grantEventId: 1);
            player.GrantTemporalAegis();
            int hpBefore = player.CurrentHP;

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 8f));

            AssertThat(player.HasTemporalAegis)
                .OverrideFailureMessage("Aegis is one hit: this contact spends it.")
                .IsFalse();
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("The barrier must still read 10.")
                .IsEqualApprox(10f, 0.001f);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void ADuplicateGrantOfTheSameShieldIsRefusedByGrantIdentity() {
        // D02a: a refused input, polling the condition, a duplicate callback, a
        // recreated visual and restored state all grant nothing. A genuinely new
        // grant event refills to the cap and restarts the timer, never adding.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        tree.Root.AddChild(player);
        try {
            AssertThat(player.GrantStoryShield(StoryShieldEffect.LeafBarrier, 10f, 480, 7)).IsTrue();
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 6f));
            AssertThat(player.StoryShieldPoints).IsEqualApprox(4f, 0.001f);

            AssertThat(player.GrantStoryShield(StoryShieldEffect.LeafBarrier, 10f, 480, 7))
                .OverrideFailureMessage("The same grant event must be refused.")
                .IsFalse();
            AssertThat(player.StoryShieldPoints).IsEqualApprox(4f, 0.001f);

            AssertThat(player.GrantStoryShield(StoryShieldEffect.LeafBarrier, 10f, 480, 8)).IsTrue();
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("4/10 refills to 10/10 - never 14, never 20.")
                .IsEqualApprox(10f, 0.001f);
            AssertThat(player.StoryShieldRemainingFrames)
                .OverrideFailureMessage("The lifetime restarts at 8 seconds, it is not added to.")
                .IsEqual(StoryDefenseRules.GrantedShieldLifetimeFrames);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AGrantedShieldExpiresAtFourHundredAndEightyActiveTicks() {
        // D02c: eight seconds of LIVE gameplay, or until depletion. Expiry
        // discards the remaining absorption with no heal, meter, block event or
        // expiry proc.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        tree.Root.AddChild(player);
        try {
            player.GrantStoryShield(StoryShieldEffect.LeafBarrier, 10f, 480, 1);
            int hpBefore = player.CurrentHP;
            float meterBefore = player.CurrentUltimateMeter;

            HoldInput(player, GameplayButtons.None,
                StoryDefenseRules.GrantedShieldLifetimeFrames - 1);
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("At tick 479 the shield is still live.")
                .IsEqualApprox(10f, 0.001f);

            HoldInput(player, GameplayButtons.None, 2);
            AssertThat(player.StoryShieldPoints)
                .OverrideFailureMessage("Tick 480 expires it.")
                .IsEqual(0f);
            AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.None);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Expiry grants no heal.")
                .IsEqual(hpBefore);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("Expiry grants no meter.")
                .IsEqualApprox(meterBefore, 0.001f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AChargeRestoreDuringLockoutLeavesTheStanceUnavailable() {
        // V7.6 F17: Temporal Aegis is a separate one-hit shield; a charge
        // restore no longer ends the shatter lockout. The retired V7.3 sentence
        // "a Chronal Shield-Restore orb ends the lockout" is deleted.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            var block = player.GetNode<BlockSystem>("BlockSystem");
            block.DepleteCharges(3);
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage("The shatter armed the lockout.")
                .IsFalse();

            block.RestoreAllCharges();

            AssertThat(block.CurrentCharges).IsEqual(3);
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage(
                    "F17: restored charges stay unusable until the 5 s lockout ends.")
                .IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AStoryDefyReleasesTheSurvivorAndProtectsSixtyResumedTicks() {
        // V7.6 D04, Story mirror. The survivor is not launched or stunned by the
        // blow it defied, and a same-frame follow-up is rejected outright.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.CurrentUltimateMeter = UltimateMeter.MaxValue;
            player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
            player.ApplyDamage(player.CurrentHP - 1);
            AssertThat(player.CurrentHP).IsEqual(1);

            int hpBefore = player.CurrentHP;
            player.ApplyDamage(player.MaximumHP);

            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("The lethal hit fired Defy.")
                .IsTrue();
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(player.IsDefyProtected)
                .OverrideFailureMessage("The window opens immediately, covering the presentation.")
                .IsTrue();
            AssertThat(player.DefyProtectionFramesRemaining)
                .IsEqual(StoryDefenseRules.DefyProtectionFrames);

            // A follow-up contact of any kind is rejected while protected.
            AssertThat(player.ApplyEnvironmentalDamage(20))
                .OverrideFailureMessage("A damaging hazard tick is rejected by the window.")
                .IsEqual(0);
            AssertThat(player.ApplyPersistentDamage(5))
                .OverrideFailureMessage("A damaging DoT tick is discarded, not banked.")
                .IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AStoryZoneTickReclaimsNothingWhileADirectHitReclaims() {
        // V7.6 D03g: PlaceholderZone now passes collectsEcho: false. Before
        // this it defaulted to true and EVERY Story zone tick reclaimed.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 40f));
            float poolAfterHit = player.EchoPool;
            AssertThat(poolAfterHit > 0f).IsTrue();
            int hpAfterHit = player.CurrentHP;

            player.AddInfluenceFromDamageDealt(6f, collectsEcho: false);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A zone tick reclaims zero.")
                .IsEqual(hpAfterHit);
            AssertThat(player.EchoPool).IsEqualApprox(poolAfterHit, 0.001f);

            player.AddInfluenceFromDamageDealt(6f);
            AssertThat(player.CurrentHP > hpAfterHit)
                .OverrideFailureMessage("A 6-HP direct hit reclaims up to 12.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AFullHealthAttackerReclaimsNothingAndKeepsItsPool() {
        // V7.6 D03f: the reclaim is clamped by missing HP and only the HP that
        // actually became health leaves the pool.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Hit(damage: 40f));
            float pool = player.EchoPool;
            player.HealStory(player.MaximumHP);
            AssertThat(player.CurrentHP).IsEqual(player.MaximumHP);

            player.AddInfluenceFromDamageDealt(20f);

            AssertThat(player.EchoPool)
                .OverrideFailureMessage("A reclaim at full HP must cost nothing from the pool.")
                .IsEqualApprox(pool, 0.001f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void AnUltimateOriginHitEarnsTheCasterNoMeter() {
        // V7.6 D03h, Story mirror.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            player.AddInfluenceFromDamageDealt(70f, ultimateOrigin: true);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("Ultimate-origin damage refills nothing.")
                .IsEqual(0f);

            player.AddInfluenceFromDamageDealt(8f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("An independent ordinary hit still earns its 8.")
                .IsEqualApprox(8f, 0.01f);
        } finally {
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

    /// <summary>An impulse-free, stun-free test hit from the front.</summary>
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
