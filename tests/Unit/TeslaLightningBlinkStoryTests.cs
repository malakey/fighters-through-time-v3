using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — Tesla's Lightning Blink in Story (design §5, timing
/// 2026-09-26): 6 startup / 12 translation / 10 recovery frames over 3.0 units,
/// 3.5 with the Story-only Long Blink node, and projectiles pass through him
/// during the translation only. Before W4 the <c>long_blink</c> node was
/// authored on the grid but nothing in <c>scripts/</c> read it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TeslaLightningBlinkStoryTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void LongBlinkAddsHalfAUnitToTheAuthoredThreeUnitBlink() {
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        Node host = Attach(player);
        try {
            var blink = player.GetNode<TeslaLightningBlink>("MovementAbility");
            float baseDistance = KitMotionRules.LightningBlinkDistanceUnits * KitMotionRules.StoryPixelsPerUnit;
            AssertFloat(blink.ResolveBlinkDistancePixels()).IsEqualApprox(baseDistance, 0.01f);

            player.StoryAbilityPerks.Add(TeslaLightningBlink.LongBlinkPerkKey);
            AssertFloat(blink.ResolveBlinkDistancePixels()).IsEqualApprox(
                (KitMotionRules.LightningBlinkDistanceUnits + KitMotionRules.LongBlinkBonusUnits)
                    * KitMotionRules.StoryPixelsPerUnit,
                0.01f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void ProjectilesPassThroughOnlyDuringTheTwelveFrameTranslation() {
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        Node host = Attach(player);
        try {
            var blink = player.GetNode<TeslaLightningBlink>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);

            int startup = 0, translation = 0, recovery = 0;
            for (int frame = 0; frame < 40 && blink.IsExecuting; frame++) {
                switch (blink.CurrentPhase) {
                    case AbilityPhase.Startup:
                        startup++;
                        AssertThat(player.PassesThroughProjectiles).IsFalse();
                        break;
                    case AbilityPhase.Active:
                        translation++;
                        AssertThat(player.PassesThroughProjectiles).IsTrue();
                        break;
                    case AbilityPhase.Recovery:
                        recovery++;
                        AssertThat(player.PassesThroughProjectiles).IsFalse();
                        break;
                }
                blink._PhysicsProcess(Tick);
            }
            AssertThat(startup).IsEqual(KitMotionRules.LightningBlinkStartupFrames);
            AssertThat(translation).IsEqual(KitMotionRules.LightningBlinkTravelFrames);
            AssertThat(recovery).IsEqual(KitMotionRules.LightningBlinkRecoveryFrames);
            AssertThat(player.PassesThroughProjectiles).IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    private static void Press(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Tick);
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "TeslaBlinkHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host, PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
