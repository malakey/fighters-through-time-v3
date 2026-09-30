using FTT.Characters;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W1 — A13 (Story half). The Story player's pushbox is the
/// universal 0.6 units (36 px at 60 px/unit), and the grab box — pivot to 0.8
/// forward, tested against the target's hurtbox — reaches a target at pushbox
/// contact and out to reach + half its hurtbox. The Fighter sweep over every
/// character pair is <c>tests/Determinism/GrabReachGeometryTests.cs</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCombatantGeometryTests {

    [TestCase]
    public void ThePlayerPushboxIsTheUniversalZeroPointSixUnits() {
        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        try {
            CombatantPushbox pushbox = player.GetNode<CombatantPushbox>("Pushbox");
            AssertThat(pushbox.BoxSize.X)
                .IsEqual(BasicComboRules.PushboxWidthUnits * KitMotionRules.StoryPixelsPerUnit);
            AssertThat(pushbox.BoxSize.X).IsEqual(36f);
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void TheStoryGrabReachesAtContactAndStopsAtTheHurtboxEdge() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        var target = new Node2D { Name = "GrabTarget" };
        tree.Root.AddChild(player);
        tree.Root.AddChild(target);
        try {
            player.GlobalPosition = Vector2.Zero;
            // No Hurtbox child: the 0.8-unit template (48 px) is the fallback.
            float half = PlayerController.HurtboxWidthPixels(target) * 0.5f;
            AssertThat(half).IsEqual(24f);
            float reach = BasicComboRules.GrabReachUnits * KitMotionRules.StoryPixelsPerUnit;

            target.GlobalPosition = new Vector2(36f, 0f);
            AssertThat(player.GrabBoxReaches(target))
                .OverrideFailureMessage("A grab must reach a target at pushbox contact.")
                .IsTrue();
            target.GlobalPosition = new Vector2(reach + half - 1f, 0f);
            AssertThat(player.GrabBoxReaches(target)).IsTrue();
            target.GlobalPosition = new Vector2(reach + half + 1f, 0f);
            AssertThat(player.GrabBoxReaches(target)).IsFalse();
        } finally {
            player.Free();
            target.Free();
        }
    }
}
