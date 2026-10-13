using FTT.Characters;
using FTT.Combat;
using FTT.Diagnostics;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The playtest bots' strike range (2026-10-11). It was a fixed 92 px for
/// every hero against every foe; it is now the hero's own string-opener reach:
/// hit 1's grounded box far edge from the one cross-mode table (with the
/// hero's reach width and Story AttackRange minor), plus the target's hurtbox
/// half-width, less the CPU's melee-band margin in pixels.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlaytestBotStrikeRangeTests {

    [TestCase]
    public void TheStrikeRangeIsTheHerosOpenerReachAgainstTheTargetsHurtbox() {
        PlayerController lincoln = null;
        PlayerController cleopatra = null;
        PlayerController target = null;
        Node2D bare = null;
        try {
            lincoln = CharacterFactory.CreateCharacter("lincoln", 0, applyStoryProgression: false);
            cleopatra = CharacterFactory.CreateCharacter("cleopatra", 0, applyStoryProgression: false);
            target = CharacterFactory.CreateCharacter("tesla", 1, applyStoryProgression: false);
            bare = new Node2D { Name = "NoHurtbox" };
            float margin = (float)(FighterCpuController.MeleeBandMarginUnits * KitMotionRules.StoryPixelsPerUnit);
            float targetHalf = PlayerController.HurtboxWidthPixels(target) * 0.5f;

            foreach (PlayerController hero in new[] { lincoln, cleopatra }) {
                PlayerController.ResolveGroundStringBoxPixels(
                    0, BasicComboRules.StringProfileFor(hero.Data.CharacterID), 1f,
                    out Vector2 size, out Vector2 offset);
                float expected = offset.X + size.X * 0.5f + targetHalf - margin;
                AssertThat(StoryBot.StrikeRangeFor(hero, target)).IsEqualApprox(expected, 0.001f);
                // The bot strikes from inside the box's real reach.
                AssertThat(StoryBot.StrikeRangeFor(hero, target) < offset.X + size.X * 0.5f + targetHalf).IsTrue();
            }

            // Reach-aware: the short rail and the long scepter no longer share a
            // range, and a wider target is struck from further away (a body with
            // no hurtbox reads the 0.8-unit template, wider than the hero's 40 px).
            AssertThat(StoryBot.StrikeRangeFor(lincoln, target) < StoryBot.StrikeRangeFor(cleopatra, target)).IsTrue();
            AssertThat(StoryBot.StrikeRangeFor(lincoln, bare) > StoryBot.StrikeRangeFor(lincoln, target)).IsTrue();

            // The Story AttackRange minor moves the far edge by its full percentage.
            float before = StoryBot.StrikeRangeFor(lincoln, target);
            lincoln.StoryAttackRangeMultiplier = 1.1f;
            PlayerController.ResolveGroundStringBoxPixels(
                0, BasicComboRules.StringProfileFor("lincoln"), 1f, out Vector2 baseSize, out Vector2 baseOffset);
            float farEdge = baseOffset.X + baseSize.X * 0.5f;
            AssertThat(StoryBot.StrikeRangeFor(lincoln, target) - before).IsEqualApprox(farEdge * 0.1f, 0.01f);
        } finally {
            if (lincoln != null && GodotObject.IsInstanceValid(lincoln)) lincoln.Free();
            if (cleopatra != null && GodotObject.IsInstanceValid(cleopatra)) cleopatra.Free();
            if (target != null && GodotObject.IsInstanceValid(target)) target.Free();
            if (bare != null && GodotObject.IsInstanceValid(bare)) bare.Free();
        }
    }
}
