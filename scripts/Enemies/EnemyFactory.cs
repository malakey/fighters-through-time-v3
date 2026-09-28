using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Enemies {

    /// <summary>
    /// Spawns roster enemies from authored scenes through the shared tier pools.
    /// One scene per tier: canonical tuning arrives with the assigned
    /// <see cref="EnemyData"/> resource, so adding a roster entry never needs a new
    /// scene, pool, or factory method.
    /// </summary>
    public static class EnemyFactory {
        public const string StandardPoolID = "standard_enemy";
        public const string ElitePoolID = "elite_enemy";
        public const string StandardScenePath = "res://scenes/enemies/StandardEnemy.tscn";
        public const string EliteScenePath = "res://scenes/enemies/EliteEnemy.tscn";
        private const string EnemyResourceFolder = "res://resources/Enemies/";

        private const int StandardWarmUp = 8;
        private const int StandardCapacity = 40;
        private const int EliteWarmUp = 4;
        private const int EliteCapacity = 16;

        private static readonly Dictionary<string, EnemyData> DataCache = new(StringComparer.Ordinal);

        // === Generic entry points ===

        /// <summary>Pooled spawn keyed by canonical enemy ID.</summary>
        public static EnemyController Spawn(
            string enemyID,
            Node parent,
            Vector2 position,
            Vector2? waypointA = null,
            Vector2? waypointB = null) {
            EnemyData data = LoadData(enemyID);
            if (parent == null || PoolManager.Instance == null) {
                EnemyController fallback = Create(enemyID, position, waypointA, waypointB);
                parent?.AddChild(fallback);
                return fallback;
            }

            string poolID = PoolIDForTier(data.Tier);
            EnsurePool(poolID);
            if (PoolManager.Instance.Spawn(poolID, position, parent) is not EnemyController enemy) return null;
            enemy.Name = data.EnemyID.ToPascalCase();
            enemy.ApplyData(data);
            enemy.ConfigureSpawn(position, waypointA, waypointB);
            if (!enemy.IsInGroup("Enemies")) enemy.AddToGroup("Enemies");
            return enemy;
        }

        /// <summary>
        /// Package 12 W2 (GAP-04): a <c>SummonMinions</c> spawn. Identical to
        /// <see cref="Spawn"/> except that the body is marked
        /// <see cref="EnemyController.IsSummoned"/>, so its death draws no F05
        /// award and can never consume an authored encounter's finite source of
        /// the same enemy type. Set after the pool's <c>OnSpawn</c> reset.
        /// </summary>
        public static EnemyController SpawnSummoned(string enemyID, Node parent, Vector2 position) {
            EnemyController enemy = Spawn(enemyID, parent, position);
            if (enemy != null) enemy.IsSummoned = true;
            return enemy;
        }

        /// <summary>Unpooled instance, used when no pool manager is available.</summary>
        public static EnemyController Create(
            string enemyID,
            Vector2 position,
            Vector2? waypointA = null,
            Vector2? waypointB = null) {
            EnemyData data = LoadData(enemyID);
            PackedScene scene = GD.Load<PackedScene>(ScenePathForTier(data.Tier))
                ?? throw new InvalidOperationException($"Enemy scene not found for tier '{data.Tier}'.");
            var enemy = scene.Instantiate<EnemyController>();
            enemy.Name = data.EnemyID.ToPascalCase();
            enemy.Data = data;
            enemy.Position = position;
            enemy.ConfigureSpawn(position, waypointA, waypointB);
            if (!enemy.IsInGroup("Enemies")) enemy.AddToGroup("Enemies");
            return enemy;
        }

        public static string PoolIDForTier(EnemyTier tier) =>
            tier == EnemyTier.Standard ? StandardPoolID : ElitePoolID;

        public static string ScenePathForTier(EnemyTier tier) =>
            tier == EnemyTier.Standard ? StandardScenePath : EliteScenePath;

        public static EnemyData LoadData(string enemyID) {
            if (string.IsNullOrWhiteSpace(enemyID)) throw new ArgumentException("Enemy ID is empty.", nameof(enemyID));
            if (DataCache.TryGetValue(enemyID, out EnemyData cached) && cached != null) return cached;
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"{EnemyResourceFolder}{enemyID}.tres")
                ?? throw new InvalidOperationException($"Enemy data not found for '{enemyID}'.");
            DataCache[enemyID] = data;
            return data;
        }

        /// <summary>
        /// Registers a tier pool when a scene has not warmed it from its
        /// <see cref="ScenePoolConfig"/>. Configured warm-ups always win.
        /// </summary>
        public static void EnsurePool(string poolID) {
            PoolManager pools = PoolManager.Instance;
            if (pools == null || pools.IsRegistered(poolID)) return;
            bool standard = poolID == StandardPoolID;
            PackedScene scene = GD.Load<PackedScene>(standard ? StandardScenePath : EliteScenePath);
            if (scene == null) return;
            pools.RegisterPool(
                poolID,
                scene,
                standard ? StandardWarmUp : EliteWarmUp,
                standard ? StandardCapacity : EliteCapacity,
                PoolOverflowPolicy.RecycleOldest);
        }

        // === Named convenience wrappers (existing level controllers) ===

        public static EnemyController CreateChronoSlasher(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Create("chrono_slasher", position, waypointA, waypointB);

        public static EnemyController CreateTechEnforcer(Vector2 position) => Create("tech_enforcer", position);

        public static EnemyController CreateCyberGuard(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Create("cyber_guard", position, waypointA, waypointB);

        public static EnemyController CreateSteamAutomaton(Vector2 position) => Create("steam_automaton", position);

        public static EnemyController CreateHologramDrone(Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Create("hologram_drone", position, waypointA, waypointB);

        public static EnemyController SpawnChronoSlasher(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("chrono_slasher", parent, position, waypointA, waypointB);

        public static EnemyController SpawnCyberGuard(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("cyber_guard", parent, position, waypointA, waypointB);

        public static EnemyController SpawnSteamAutomaton(Node parent, Vector2 position) =>
            Spawn("steam_automaton", parent, position);

        public static EnemyController SpawnTechEnforcer(Node parent, Vector2 position) =>
            Spawn("tech_enforcer", parent, position);

        public static EnemyController SpawnHologramDrone(Node parent, Vector2 position, Vector2? waypointA = null, Vector2? waypointB = null) =>
            Spawn("hologram_drone", parent, position, waypointA, waypointB);
    }
}
