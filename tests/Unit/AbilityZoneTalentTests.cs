using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The re-pointed talent minors reach their designed targets through the
/// ability-scoped Story multipliers. Story-only - every multiplier defaults to
/// 1f, and Fighter loadouts never read them.
///
/// <para>Package 11 A4 moved all four from the roster-generic lanes
/// (GlideDuration, ZoneRadius, ZoneDuration, PersistentHealth) onto the V7.6
/// ability-SCOPED lanes, so each case now sets the exact (key, scope) pair the
/// authored node carries. Cleopatra's case changed target as well: V7.6 retires
/// her vortex radius minor outright and re-scopes her duration minor onto the
/// SERPENT NEST, leaving the vortex on its authored numbers.</para>
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
            // pocahontas_glide_duration: AbilityDuration(pocahontas_breeze_glide) +20%.
            player.StoryScopedStats = Scoped("AbilityDuration", "pocahontas_breeze_glide", 1.2f);
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
            // einstein_rift_range: AbilityRange(relativity_rift) +10%.
            player.StoryScopedStats = Scoped("AbilityRange", "relativity_rift", 1.1f);
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
    public void NestDurationMinorScalesCleopatrasSerpentNestAndLeavesTheVortexAlone() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("cleopatra");
        tree.Root.AddChild(player);
        try {
            // cleopatra_nest_duration: AbilityDuration(cleopatra_serpent_nest)
            // +20%. Authoring the NEST scope must leave the vortex untouched -
            // V7.6 retires her vortex-radius minor and moves the duration minor
            // off the vortex entirely.
            player.StoryScopedStats = Scoped("AbilityDuration", "cleopatra_serpent_nest", 1.2f);
            player.TransitionTo(CharacterState.Airborne);
            player.Velocity = Vector2.Down;
            SendInput(player, GameplayButtons.Special2);

            var vortex = player.GetNode<CleopatraSandstormVortex>("Special2");
            for (int frame = 0; frame <= vortex.Data.StartupFrames; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsTrue();
            AssertThat(vortex.ActiveVortexRadiusPixels)
                .IsEqualApprox(CleopatraSandstormVortex.VortexRadiusPixels, 0.01f);

            int baseLifetimeFrames = Mathf.CeilToInt(vortex.Data.Lifetime * 60f);

            // The vortex lapses on its AUTHORED lifetime: the nest-scoped node
            // must not reach it.
            for (int frame = 0; frame < baseLifetimeFrames + 10; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsFalse();

            // ...and a fresh vortex arms its own Vortex Step allowance.
            AssertThat(vortex.VortexStepAvailable).IsTrue();
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
            // leonardo_spiral_range: AbilityRange(leonardo_golden_ratio) +10%.
            player.StoryScopedStats = Scoped("AbilityRange", "leonardo_golden_ratio", 1.1f);
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

    /// <summary>
    /// Builds a one-entry ability-scoped bucket, the shape
    /// <c>CharacterFactory</c> copies out of <c>StoryStatProfile</c>.
    /// </summary>
    private static FTT.Environment.ScopedStoryStats Scoped(string key, string scope, float value) =>
        new(new System.Collections.Generic.Dictionary<FTT.Environment.ScopedStatKey, float> {
            [new FTT.Environment.ScopedStatKey(key, scope)] = value
        });

    private static void SendInput(PlayerController player, GameplayButtons buttons, double delta = 1.0 / 60.0) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(delta);
    }
}
