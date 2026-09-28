using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W8 (M02): persistent objects are non-solid. Combatant bodies no
/// longer collide with the <c>PersistentObject</c> layer — constructs, checkpoint
/// strike surfaces and extractors are struck, never stood on — while the Level 6
/// movable weights, which must still block and be carried by floors, moved to
/// <c>Environment</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NonSolidConstructTests {
    private static readonly string[] CombatantBodyScenes = {
        "res://scenes/characters/Player.tscn",
        "res://scenes/enemies/StandardEnemy.tscn",
        "res://scenes/enemies/EliteEnemy.tscn",
        "res://scenes/enemies/Boss.tscn",
        "res://scenes/templates/PlayerPresentationTemplate.tscn",
        "res://scenes/templates/StandardEnemyTemplate.tscn",
        "res://scenes/templates/EliteEnemyTemplate.tscn",
        "res://scenes/templates/BossTemplate.tscn",
    };

    private static readonly string[] ConstructScenes = {
        "res://scenes/constructs/TeslaCoil.tscn",
        "res://scenes/constructs/ClockworkTurret.tscn",
        "res://scenes/constructs/SerpentNest.tscn",
        "res://scenes/constructs/SonataPlatform.tscn",
        "res://scenes/constructs/VineSnare.tscn",
        "res://scenes/templates/PersistentConstructTemplate.tscn",
    };

    [TestCase]
    public void CombatantBodyMasksExcludePersistentObjectsWhileAttacksStillReachThem() {
        AssertThat(CollisionLayers.PlayerBodyMask & CollisionLayers.PersistentObject).IsEqual(0u);
        AssertThat(CollisionLayers.EnemyBodyMask & CollisionLayers.PersistentObject).IsEqual(0u);
        AssertThat(CollisionLayers.PlayerBodyMask).IsEqual(CollisionLayers.Environment | CollisionLayers.OneWayPlatform);
        AssertThat(CollisionLayers.EnemyBodyMask).IsEqual(CollisionLayers.Environment | CollisionLayers.OneWayPlatform);
        // Being non-solid is not being unhittable: strikes still reach the layer.
        AssertThat(CollisionLayers.PlayerHitboxMask & CollisionLayers.PersistentObject).IsEqual(CollisionLayers.PersistentObject);
        AssertThat(CollisionLayers.EnemyHitboxMask & CollisionLayers.PersistentObject).IsEqual(CollisionLayers.PersistentObject);
    }

    [TestCase]
    public void AuthoredCombatantBodiesUseTheNonSolidMask() {
        var issues = new List<string>();
        foreach (string path in CombatantBodyScenes) {
            Node root = ResourceLoader.Load<PackedScene>(path).Instantiate();
            try {
                CharacterBody2D body = root as CharacterBody2D ?? root.GetNodeOrNull<CharacterBody2D>("Body");
                if (body == null) {
                    issues.Add($"{path}: no CharacterBody2D");
                    continue;
                }
                if ((body.CollisionMask & CollisionLayers.PersistentObject) != 0u) {
                    issues.Add($"{path}: body mask {body.CollisionMask} still includes PersistentObject");
                }
                if ((body.CollisionMask & CollisionLayers.Environment) == 0u) {
                    issues.Add($"{path}: body mask {body.CollisionMask} lost Environment");
                }
            } finally {
                root.Free();
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void NoConstructAuthorsASolidBodyOnThePersistentObjectLayer() {
        var issues = new List<string>();
        foreach (string path in ConstructScenes) {
            Node root = ResourceLoader.Load<PackedScene>(path).Instantiate();
            try {
                bool hasHurtbox = false;
                var stack = new Stack<Node>();
                stack.Push(root);
                while (stack.Count > 0) {
                    Node node = stack.Pop();
                    foreach (Node child in node.GetChildren()) stack.Push(child);
                    if (node is Area2D area && (area.CollisionLayer & CollisionLayers.PersistentObject) != 0u) {
                        hasHurtbox = true;
                    }
                    if (node is PhysicsBody2D physicsBody
                        && (physicsBody.CollisionLayer & CollisionLayers.PersistentObject) != 0u) {
                        issues.Add($"{path}: {node.Name} is a solid body on PersistentObject");
                    }
                }
                if (!hasHurtbox) issues.Add($"{path}: no PersistentObject hurtbox (constructs must stay strikeable)");
            } finally {
                root.Free();
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void MovableWeightsStaySolidOnTheEnvironmentLayer() {
        Node root = ResourceLoader.Load<PackedScene>("res://scenes/templates/MovableWeightTemplate.tscn").Instantiate();
        try {
            AssertThat(root is WeightedObject).IsTrue();
            var weight = (RigidBody2D)root;
            AssertThat(weight.CollisionLayer).IsEqual(CollisionLayers.Environment);
            // It still rests on floors and one-way ledges...
            AssertThat(weight.CollisionMask).IsEqual(CollisionLayers.Environment | CollisionLayers.OneWayPlatform);
            // ...and still blocks both combatant kinds, which is what the Level 6 haul needs.
            AssertThat(CollisionLayers.PlayerBodyMask & weight.CollisionLayer).IsNotEqual(0u);
            AssertThat(CollisionLayers.EnemyBodyMask & weight.CollisionLayer).IsNotEqual(0u);
        } finally {
            root.Free();
        }
    }
}
