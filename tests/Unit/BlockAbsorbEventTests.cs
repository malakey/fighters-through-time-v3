using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit M-3 plumbing: the tutorial's block calibration counts successful
/// absorbs through the new <c>EventBus.OnBlockAbsorbed</c> event, and the
/// training dummy's scripted telegraphed swipe delivers a real hit through the
/// hurtbox/block path. These tests pin both halves: a held block absorbs the
/// swipe for zero damage and raises the event with the fallen charge count,
/// while an open stance takes the small lesson hit.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlockAbsorbEventTests {

    [TestCase]
    public void ASuccessfulBlockRaisesTheAbsorbEventWithTheRemainingCharges() {
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        var host = new Node2D { Name = "BlockAbsorbHost" };
        tree.Root.AddChild(host);

        int eventCount = 0;
        int reportedPlayer = -1;
        int reportedCharges = -1;
        void OnAbsorbed(int playerIndex, int remaining) {
            eventCount++;
            reportedPlayer = playerIndex;
            reportedCharges = remaining;
        }

        EventBus.Instance.OnBlockAbsorbed += OnAbsorbed;
        try {
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            player.Position = new Vector2(0, 0);
            host.AddChild(player);

            var blockSystem = player.GetNode<BlockSystem>("BlockSystem");
            int chargesBefore = blockSystem.CurrentCharges;
            AssertThat(chargesBefore > 1).IsTrue();
            blockSystem.StartBlock();

            BlockResult result = blockSystem.ResolveHit(new HitPayload {
                AttackerIndex = 99,
                AttackID = "tutorial_dummy_swipe",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                HitOrigin = new Vector2(100, 0),
                AttackerFacingRight = false
            });

            AssertThat(result).IsEqual(BlockResult.Blocked);
            AssertThat(eventCount).IsEqual(1);
            AssertThat(reportedPlayer).IsEqual(0);
            AssertThat(reportedCharges).IsEqual(chargesBefore - 1);
        } finally {
            EventBus.Instance.OnBlockAbsorbed -= OnAbsorbed;
            host.Free();
        }
    }

    [TestCase]
    public void TheDummyScriptedSwipeIsAbsorbedByAHeldBlockAndHitsAnOpenStance() {
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        var host = new Node2D { Name = "ScriptedSwipeHost" };
        tree.Root.AddChild(host);

        int absorbEvents = 0;
        void OnAbsorbed(int playerIndex, int remaining) => absorbEvents++;

        EventBus.Instance.OnBlockAbsorbed += OnAbsorbed;
        try {
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            player.Position = new Vector2(0, 0);
            host.AddChild(player);

            var dummy = new TrainingDummy { Name = "SwipeDummy", Position = new Vector2(200, 0) };
            host.AddChild(dummy);
            dummy.BeginScriptedAttacks(player);
            AssertThat(dummy.ScriptedAttacksActive).IsTrue();

            // Held block (facing right toward the dummy): the swipe is absorbed
            // for zero damage and the absorb event fires.
            var blockSystem = player.GetNode<BlockSystem>("BlockSystem");
            player.TransitionTo(CharacterState.Blocking);
            blockSystem.StartBlock();
            int hpBefore = player.CurrentHP;
            int chargesBefore = blockSystem.CurrentCharges;

            float blockedDamage = dummy.DeliverScriptedStrike();
            AssertThat(blockedDamage).IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
            AssertThat(blockSystem.CurrentCharges).IsEqual(chargesBefore - 1);
            AssertThat(absorbEvents).IsEqual(1);

            // Open stance: the same swipe applies its small lesson damage.
            blockSystem.EndBlock();
            player.TransitionTo(CharacterState.Idle);
            float openDamage = dummy.DeliverScriptedStrike();
            AssertThat(openDamage > 0f).IsTrue();
            AssertThat(player.CurrentHP < hpBefore).IsTrue();
            AssertThat(absorbEvents).IsEqual(1);

            dummy.EndScriptedAttacks();
            AssertThat(dummy.ScriptedAttacksActive).IsFalse();
        } finally {
            EventBus.Instance.OnBlockAbsorbed -= OnAbsorbed;
            host.Free();
        }
    }
}
