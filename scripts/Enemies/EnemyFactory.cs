using Godot;
using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;

namespace FTT.Enemies {

    public static class EnemyFactory {
        private static readonly Dictionary<string, PackedScene> Templates = new(StringComparer.Ordinal);

        public static EnemyController CreateChronoSlasher(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) {
            return Create("chrono_slasher", position, new Vector2(30, 50), new Color(0.4f, 0.1f, 0.6f), waypointA, waypointB);
        }

        public static EnemyController CreateTechEnforcer(Vector2 position) {
            return Create("tech_enforcer", position, new Vector2(44, 60), new Color(0.2f, 0.2f, 0.5f));
        }

        public static EnemyController CreateCyberGuard(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) {
            return Create("cyber_guard", position, new Vector2(34, 54), new Color(0.6f, 0.5f, 0.1f), waypointA, waypointB);
        }

        public static EnemyController CreateSteamAutomaton(Vector2 position) {
            return Create("steam_automaton", position, new Vector2(50, 68), new Color(0.5f, 0.35f, 0.15f));
        }

        public static EnemyController CreateHologramDrone(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) {
            return Create("hologram_drone", position, new Vector2(28, 44), new Color(0.0f, 0.8f, 0.9f, 0.7f), waypointA, waypointB);
        }

        public static EnemyController SpawnChronoSlasher(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("chrono_slasher", parent, position, new Vector2(30, 50), new Color(0.4f, 0.1f, 0.6f), waypointA, waypointB);

        public static EnemyController SpawnCyberGuard(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("cyber_guard", parent, position, new Vector2(34, 54), new Color(0.6f, 0.5f, 0.1f), waypointA, waypointB);

        public static EnemyController SpawnSteamAutomaton(Node parent, Vector2 position) =>
            Spawn("steam_automaton", parent, position, new Vector2(50, 68), new Color(0.5f, 0.35f, 0.15f));

        public static EnemyController SpawnHologramDrone(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("hologram_drone", parent, position, new Vector2(28, 44), new Color(0.0f, 0.8f, 0.9f, 0.7f), waypointA, waypointB);

        private static EnemyController Spawn(
            string enemyID,
            Node parent,
            Vector2 position,
            Vector2 bodySize,
            Color placeholderColor,
            Vector2? waypointA = null,
            Vector2? waypointB = null) {
            if (parent == null || PoolManager.Instance == null) {
                EnemyController fallback = Create(enemyID, position, bodySize, placeholderColor, waypointA, waypointB);
                parent?.AddChild(fallback);
                return fallback;
            }

            string poolID = $"story_enemy.{enemyID}";
            if (!PoolManager.Instance.IsRegistered(poolID)) {
                PackedScene template = GetOrCreateTemplate(enemyID, bodySize, placeholderColor);
                PoolManager.Instance.RegisterPool(poolID, template, 4, 25, PoolOverflowPolicy.Grow);
            }
            EnemyController enemy = PoolManager.Instance.Spawn(poolID, position, parent) as EnemyController;
            enemy?.ConfigureSpawn(position, waypointA, waypointB);
            return enemy;
        }

        private static PackedScene GetOrCreateTemplate(string enemyID, Vector2 bodySize, Color placeholderColor) {
            if (Templates.TryGetValue(enemyID, out PackedScene existing)) return existing;
            EnemyController source = Create(enemyID, Vector2.Zero, bodySize, placeholderColor);
            AssignOwnerRecursive(source, source);
            var template = new PackedScene();
            Error error = template.Pack(source);
            source.Free();
            if (error != Error.Ok) throw new InvalidOperationException($"Could not pack pooled enemy template '{enemyID}': {error}.");
            Templates[enemyID] = template;
            return template;
        }

        private static void AssignOwnerRecursive(Node root, Node current) {
            foreach (Node child in current.GetChildren()) {
                child.Owner = root;
                AssignOwnerRecursive(root, child);
            }
        }

        private static EnemyController Create(
            string enemyID,
            Vector2 position,
            Vector2 bodySize,
            Color placeholderColor,
            Vector2? waypointA = null,
            Vector2? waypointB = null) {
            EnemyData data = GD.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres")
                ?? throw new InvalidOperationException($"Enemy data not found for '{enemyID}'.");

            var enemy = new EnemyController {
                Name = data.EnemyID.ToPascalCase(),
                Data = data,
                Position = position,
                CollisionLayer = CollisionLayers.Enemy,
                CollisionMask = CollisionLayers.EnemyBodyMask,
                MotionMode = CharacterBody2D.MotionModeEnum.Grounded,
                UpDirection = Vector2.Up,
                FloorStopOnSlope = true
            };

            SetupEnemyBody(enemy, bodySize, placeholderColor);
            SetupEnemyPushbox(enemy, bodySize);
            SetupEnemyHitboxes(enemy, bodySize);
            SetupPatrolWaypoints(enemy, position, waypointA, waypointB);
            enemy.AddToGroup("Enemies");
            return enemy;
        }

        private static void SetupEnemyBody(EnemyController enemy, Vector2 size, Color color) {
            var collision = new CollisionShape2D { Name = "CollisionShape2D", Position = new Vector2(0, -size.Y / 2f) };
            collision.Shape = new RectangleShape2D { Size = size };
            enemy.AddChild(collision);

            enemy.AddChild(new ColorRect {
                Name = "Body", Size = size, Position = new Vector2(-size.X / 2f, -size.Y), Color = color
            });

            var nameLabel = new Label {
                Name = "NameLabel",
                Text = enemy.Data.DisplayName,
                Position = new Vector2(-40, -size.Y - 20),
                CustomMinimumSize = new Vector2(80, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 9);
            nameLabel.AddThemeColorOverride("font_color", color);
            enemy.AddChild(nameLabel);

            enemy.AddChild(new ProgressBar {
                Name = "HPBar",
                CustomMinimumSize = new Vector2(size.X + 10, 6),
                Position = new Vector2(-(size.X + 10) / 2f, -size.Y - 10),
                MaxValue = enemy.Data.MaxHP,
                Value = enemy.Data.MaxHP,
                ShowPercentage = false
            });
        }

        private static void SetupEnemyHitboxes(EnemyController enemy, Vector2 bodySize) {
            EnemyData data = enemy.Data;
            var hurtbox = new Hurtbox {
                Name = "Hurtbox",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.EnemyHurtbox,
                CollisionMask = CollisionLayers.PlayerHitbox | CollisionLayers.Projectile,
                Monitorable = true,
                Monitoring = true
            };
            var hurtShape = new CollisionShape2D { Position = new Vector2(0, -bodySize.Y / 2f) };
            hurtShape.Shape = new RectangleShape2D { Size = bodySize };
            hurtbox.AddChild(hurtShape);
            enemy.AddChild(hurtbox);

            var hitbox = new Hitbox {
                Name = "AttackHitbox",
                AttackID = $"{data.EnemyID}.basic",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = data.AttackDamage,
                KnockbackForce = new Vector2(data.AttackKnockback, -1.5f),
                HitstunDuration = 0.15f,
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.EnemyHitbox,
                CollisionMask = CollisionLayers.EnemyHitboxMask,
                Monitorable = true,
                Monitoring = false
            };
            var attackShape = new CollisionShape2D { Position = new Vector2(30, -25) };
            attackShape.Shape = new RectangleShape2D { Size = new Vector2(40, 40) };
            hitbox.AddChild(attackShape);
            enemy.AddChild(hitbox);
        }

        private static void SetupEnemyPushbox(EnemyController enemy, Vector2 bodySize) {
            var pushbox = new CombatantPushbox {
                Name = "Pushbox",
                BoxSize = new Vector2(Mathf.Max(20f, bodySize.X - 8f), Mathf.Max(32f, bodySize.Y - 10f)),
                Position = new Vector2(0f, -bodySize.Y * 0.45f),
                CollisionLayer = CollisionLayers.Enemy,
                CollisionMask = CollisionLayers.Player,
                Monitoring = false,
                Monitorable = false
            };
            pushbox.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = pushbox.BoxSize }
            });
            enemy.AddChild(pushbox);
        }

        private static void SetupPatrolWaypoints(
            EnemyController enemy,
            Vector2 position,
            Vector2? waypointA,
            Vector2? waypointB) {
            enemy.AddChild(new Marker2D {
                Name = "WaypointA",
                Position = waypointA.HasValue ? waypointA.Value - position : new Vector2(-150, 0)
            });
            enemy.AddChild(new Marker2D {
                Name = "WaypointB",
                Position = waypointB.HasValue ? waypointB.Value - position : new Vector2(150, 0)
            });
        }
    }
}
