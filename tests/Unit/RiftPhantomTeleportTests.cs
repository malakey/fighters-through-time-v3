using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W9 (M20): the Rift Phantom is the roster's Teleport line
/// (design §6: "at least one mob line uses Teleport — the Level 13 Chronal Void
/// Rift Phantom"), carried through the new opt-in standard-tier secondary-ability
/// path rather than a promotion to Elite.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RiftPhantomTeleportTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void TheRiftPhantomStaysAStandardFlyerAndAuthorsItsTeleportAsAStandardSecondary() {
        EnemyData data = AuthoredResources.Load<EnemyData>("res://resources/Enemies/rift_phantom.tres");
        AssertThat(data.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(data.Behavior).IsEqual(DefaultBehavior.Flying);
        AssertThat(data.PhasesThroughWalls).IsTrue();
        AssertThat(data.HasEliteAbilities).IsFalse();
        AssertThat(data.HasSecondaryAbilities).IsTrue();
        AssertThat(data.SecondaryAbilities.Length).IsEqual(1);
        EnemyAbilityData step = data.SecondaryAbilities[0];
        AssertThat(step.AbilityID).IsEqual("enemy.rift_phantom.rift_step");
        AssertThat(step.Archetype).IsEqual(EnemyAbilityArchetype.Teleport);
        AssertThat(step.HasValidArchetypeFields()).IsTrue();
        AssertThat(step.TelegraphFrames > 0).IsTrue();
        AssertThat(data.SecondaryAbilityCooldown > 0f).IsTrue();
        // The standard path is inert on elites, and the elite path on standards.
        AssertThat(data.ActiveSecondaryAbilities).IsEqual(data.SecondaryAbilities);
    }

    [TestCase]
    public async Task FromRangeThePhantomBlinksThroughRiftSpaceToJustPastItsTarget() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            PlayerController player = CreateTargetPlayer(new Vector2(420f, -80f));
            EnemyController phantom = CreateEnemy("rift_phantom");
            try {
                phantom.GlobalPosition = new Vector2(0f, -80f);
                float cooldownBefore = phantom.Data.AttackCooldown;
                // Patrol -> aggro -> Chase -> the engage blink (420 px is past 1.5x its
                // 96 px striking distance and inside its 560 px aggro radius).
                for (int frame = 0; frame < 3 && !phantom.IsEngageBlinking; frame++) phantom._PhysicsProcess(Step);
                AssertThat(phantom.IsEngageBlinking).IsTrue();
                AssertThat(phantom.CurrentState).IsEqual(EnemyState.Attacking);
                AssertThat(phantom.ActiveAbility?.AbilityID).IsEqual("enemy.rift_phantom.rift_step");
                // A standard's secondary is never Guard-Crush by tier.
                AssertThat(phantom.ResolveGuardCrush(phantom.ActiveAbility)).IsFalse();

                int guard = 0;
                while (phantom.AbilityPhase != EnemyAbilityPhase.Active && guard++ < 60) {
                    phantom.TickAbilityExecutor(Step);
                }
                AssertThat(phantom.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
                float gap = phantom.GlobalPosition.X - player.GlobalPosition.X;
                AssertThat(gap > 0f && gap <= 110.5f)
                    .OverrideFailureMessage($"The blink should land just past the target; gap {gap}.").IsTrue();

                // Once the blink resolves the phantom is straight back on the hunt:
                // the primary attack's cooldown was never armed.
                guard = 0;
                while (phantom.AbilityPhase != EnemyAbilityPhase.Idle && guard++ < 60) phantom.TickAbilityExecutor(Step);
                phantom._PhysicsProcess(Step);
                AssertThat(phantom.IsEngageBlinking).IsFalse();
                AssertThat(phantom.CurrentState is EnemyState.Chase or EnemyState.Attacking).IsTrue();
                AssertFloat(phantom.Data.AttackCooldown).IsEqualApprox(cooldownBefore, 0.0001f);
            } finally {
                phantom.Free();
                player.Free();
            }
        });
    }

    [TestCase]
    public async Task AtPointBlankThePhantomFightsInsteadOfBlinking() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            PlayerController player = CreateTargetPlayer(new Vector2(80f, -80f));
            EnemyController phantom = CreateEnemy("rift_phantom");
            try {
                phantom.GlobalPosition = new Vector2(0f, -80f);
                for (int frame = 0; frame < 20; frame++) {
                    phantom._PhysicsProcess(Step);
                    AssertThat(phantom.IsEngageBlinking).IsFalse();
                }
                AssertThat(phantom.CurrentState).IsEqual(EnemyState.Attacking);
            } finally {
                phantom.Free();
                player.Free();
            }
        });
    }

    private static EnemyController CreateEnemy(string enemyID) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }

    /// <summary>A bare grouped PlayerController target (the EnemyControllerTests idiom).</summary>
    private static PlayerController CreateTargetPlayer(Vector2 position) {
        var player = new PlayerController { Name = "RiftPhantomTarget", Position = position };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.AddToGroup("Players");
        return player;
    }
}
