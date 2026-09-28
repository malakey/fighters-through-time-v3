using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W4 — Spirit Strike in Story (design §5): "Pocahontas is carried
/// diagonally up-forward (a forced dash of about 3 units at 45° over 15
/// frames), while the eagle's hitbox swoops down-forward in a diagonal arc ahead
/// of her… The two vectors are separate." The retired interim shape moved her
/// along (h, −0.6) at 420 px/s for 21 frames with the hitbox on her own body.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PocahontasSpiritStrikeStoryTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void TheCarryIsThreeUnitsAtFortyFiveDegreesOverFifteenFrames() {
        AbilityData data = AuthoredResources.Load<AbilityData>("res://resources/Abilities/pocahontas/special_1.tres");
        AssertThat(data.ActiveFrames).IsEqual(KitMotionRules.SpiritStrikeCarryFrames);

        Vector2 carry = PocahontasSpiritStrike.CarryVelocity(1f);
        // Up (negative Y in Godot) and forward by the same amount: 45 degrees.
        AssertFloat(carry.X).IsEqualApprox(-carry.Y, 0.001f);
        AssertFloat(carry.X).IsGreater(0f);
        float travelled = carry.Length() * KitMotionRules.SpiritStrikeCarryFrames / 60f;
        AssertFloat(travelled).IsEqualApprox(
            KitMotionRules.SpiritStrikeCarryUnits * KitMotionRules.StoryPixelsPerUnit, 0.5f);
        AssertFloat(PocahontasSpiritStrike.CarryVelocity(-1f).X).IsLess(0f);
    }

    [TestCase]
    public void TheEagleDivesDownForwardAheadOfHerWhileSheRises() {
        Vector2 first = PocahontasSpiritStrike.EagleOffsetPixels(0, 1f);
        Vector2 last = PocahontasSpiritStrike.EagleOffsetPixels(KitMotionRules.SpiritStrikeCarryFrames - 1, 1f);
        // Always ahead of her, starting above her and ending below: the eagle
        // falls relative to her while she climbs.
        AssertFloat(first.X).IsGreater(0f);
        AssertFloat(first.Y).IsLess(0f);
        AssertFloat(last.Y).IsGreater(0f);
        AssertFloat(PocahontasSpiritStrike.EagleOffsetPixels(3, -1f).X).IsLess(0f);

        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        Node host = Attach(player);
        try {
            var spirit = player.GetNode<PocahontasSpiritStrike>("Special1");
            player.TransitionTo(CharacterState.Airborne);
            Press(player, GameplayButtons.Special1);
            AssertThat(spirit.IsExecuting).IsTrue();
            for (int frame = 0; frame < spirit.Data.StartupFrames; frame++) spirit._PhysicsProcess(Tick);
            AssertThat(spirit.CurrentPhase).IsEqual(AbilityPhase.Active);

            spirit._PhysicsProcess(Tick);
            float facing = player.IsFacingRight ? 1f : -1f;
            AssertThat(player.Velocity).IsEqual(PocahontasSpiritStrike.CarryVelocity(facing));
            var eagle = spirit.GetNode<Hitbox>("EagleHitbox");
            AssertThat(eagle.IsActive).IsTrue();
            Vector2 offset = eagle.GlobalPosition - player.GlobalPosition;
            Vector2 expected = PocahontasSpiritStrike.EagleOffsetPixels(0, facing);
            AssertFloat(offset.X).IsEqualApprox(expected.X, 0.01f);
            AssertFloat(offset.Y).IsEqualApprox(expected.Y, 0.01f);
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
        var host = new Node { Name = "SpiritStrikeHost" };
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
