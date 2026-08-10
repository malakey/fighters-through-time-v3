using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Audit Low "Kits/talents": the five re-pointed talent minors now reach their
/// designed targets through the ability-scoped Story multipliers. Story-only —
/// every multiplier defaults to 1f, and Fighter loadouts never read them.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityZoneTalentTests {

    [TestCase]
    public void GlideDurationMinorExtendsPocahontasBreezeGlideWindow() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("pocahontas");
        tree.Root.AddChild(player);
        try {
            // pocahontas_wr2 "Glide Duration +20%".
            player.StoryGlideDurationMultiplier = 1.2f;
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.MovementAbility);

            var ability = player.GetNode<BaseSpecial>("MovementAbility");
            AssertThat(ability.IsExecuting).IsTrue();
            var data = (MovementAbilityData)ability.Data;
            int baseGlideFrames = Mathf.CeilToInt(data.MovementDuration * 60f);
            int scaledGlideFrames = Mathf.CeilToInt(data.MovementDuration * 1.2f * 60f);

            // Run the cast through startup + active into the glide.
            int leadInFrames = data.StartupFrames + data.ActiveFrames + 1;
            for (int frame = 0; frame < leadInFrames; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(ability.IsExecuting).IsTrue();

            // Past the unmodified window the glide is still going...
            for (int frame = 0; frame < baseGlideFrames + 10; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(ability.IsExecuting).IsTrue();

            // ...and it ends once the scaled window expires.
            for (int frame = 0; frame < scaledGlideFrames - baseGlideFrames + 10; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(ability.IsExecuting).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void ZoneRadiusMinorWidensEinsteinRelativityRift() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            // einstein_u2 "Rift Range +10%".
            player.StoryZoneRadiusMultiplier = 1.1f;
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;

            SendInput(player, GameplayButtons.Special2);
            var ability = player.GetNode<BaseSpecial>("Special2");
            for (int frame = 0; frame <= ability.Data.StartupFrames; frame++) {
                ability._PhysicsProcess(1.0 / 60.0);
            }

            // 100 px authored rift radius x 1.10 = 110 — a radius no other
            // authored zone uses, so finding it proves the deployed rift was
            // scaled (pooled zones from other tests keep their old radii).
            PlaceholderZone rift = null;
            Godot.Collections.Array<Node> zones = tree.GetNodesInGroup("story_zone");
            using (zones.AsDisposable()) {
                foreach (Node node in zones) {
                    if (node is not PlaceholderZone zone) continue;
                    var zoneShape = zone.GetNodeOrNull<CollisionShape2D>("Area/CollisionShape2D");
                    if (zoneShape?.Shape is CircleShape2D circle
                        && Mathf.Abs(circle.Radius - 110f) < 0.01f) {
                        rift = zone;
                    }
                }
            }
            AssertObject(rift).IsNotNull();
            rift.ReturnToPool();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void SandRadiusAndSandDurationMinorsScaleCleopatrasVortex() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("cleopatra");
        tree.Root.AddChild(player);
        try {
            // cleopatra_dm1 "Sand Radius +15%", cleopatra_dm2 "Sand Duration +20%".
            player.StoryZoneRadiusMultiplier = 1.15f;
            player.StoryZoneDurationMultiplier = 1.2f;
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Special2);

            var vortex = player.GetNode<CleopatraSandstormVortex>("Special2");
            for (int frame = 0; frame <= vortex.Data.StartupFrames; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsTrue();
            AssertThat(vortex.ActiveVortexRadiusPixels)
                .IsEqualApprox(CleopatraSandstormVortex.VortexRadiusPixels * 1.15f, 0.01f);

            int baseLifetimeFrames = Mathf.CeilToInt(vortex.Data.Lifetime * 60f);
            int scaledLifetimeFrames = Mathf.CeilToInt(vortex.Data.Lifetime * 1.2f * 60f);

            // Past the unmodified lifetime the sand is still churning...
            for (int frame = 0; frame < baseLifetimeFrames + 5; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsTrue();

            // ...and it lapses once the scaled lifetime expires.
            for (int frame = 0; frame < scaledLifetimeFrames - baseLifetimeFrames + 10; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void SpiralRangeMinorWidensLeonardosGoldenRatioExpansion() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("leonardo");
        tree.Root.AddChild(player);
        try {
            // leonardo_a2 "Spiral Range +10%".
            player.StoryZoneRadiusMultiplier = 1.1f;
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Special1);

            var spiral = player.GetNode<LeonardoGoldenRatio>("Special1");
            // Run well past every tick: the expansion caps at its full radius.
            for (int frame = 0; frame < spiral.Data.StartupFrames + 200; frame++) {
                spiral._PhysicsProcess(1.0 / 60.0);
            }
            // 120 px authored maximum spiral radius x 1.10 = 132.
            AssertThat(spiral.ActiveSpiralRadiusPixels).IsEqualApprox(132f, 0.5f);
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
}
