using FTT.Core;
using Godot;

namespace FTT.Environment {

    public partial class StoryDropSystem : Node {
        private const string DustPoolID = "chronal_dust";
        private const string ItemPoolID = "story_item";
        private const string DustScenePath = "res://scenes/templates/ChronalDustPickupTemplate.tscn";
        private const string ItemScenePath = "res://scenes/templates/StoryItemPickupTemplate.tscn";

        [Export] public StoryDropTable DropTable;
        [Export] public ulong RandomSeed;

        private readonly RandomNumberGenerator _random = new();

        public override void _Ready() {
            DropTable ??= StoryDropTable.LoadDefault();
            if (RandomSeed == 0) _random.Randomize();
            else _random.Seed = RandomSeed;
            EnsurePools();
            if (EventBus.Instance != null) EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
        }

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            // A kill landed by a hit raises this inside the physics in/out
            // flush, where the pooled pickups' Area2Ds cannot enter the tree;
            // spawn right after the flush. Direct raises (tests, script kills)
            // stay synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) {
                Callable.From(() => SpawnDrops(payload)).CallDeferred();
                return;
            }
            SpawnDrops(payload);
        }

        private void SpawnDrops(EnemyKilledPayload payload) {
            if (PoolManager.Instance == null) return;
            ChronalDustPickup dust = PoolManager.Instance.Spawn(DustPoolID, payload.Position, GetParent()) as ChronalDustPickup;
            dust?.Setup(Mathf.Max(1, payload.ChronalDustDrop));

            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            StoryDropProfile profile = DropTable?.GetProfile(difficulty);
            // Per-enemy multiplier scales the profile chance; 0 (unset payload) is
            // treated as the neutral 1.0 so older raisers keep their behavior.
            float multiplier = payload.ItemDropChanceMultiplier <= 0f ? 1f : payload.ItemDropChanceMultiplier;
            if (profile == null || !profile.ShouldDrop(_random.Randf(), multiplier)) return;
            StoryPickup pickup = PoolManager.Instance.Spawn(ItemPoolID, payload.Position + new Vector2(24f, -12f), GetParent()) as StoryPickup;
            pickup?.Setup(profile.ChooseItem(_random.Randf()), profile);
        }

        private static void EnsurePools() {
            PoolManager pools = PoolManager.Instance;
            if (pools == null) return;
            if (!pools.IsRegistered(DustPoolID)) {
                PackedScene scene = GD.Load<PackedScene>(DustScenePath);
                if (scene != null) pools.RegisterPool(DustPoolID, scene, 12, 50, PoolOverflowPolicy.Reject);
            }
            if (!pools.IsRegistered(ItemPoolID)) {
                PackedScene scene = GD.Load<PackedScene>(ItemScenePath);
                if (scene != null) pools.RegisterPool(ItemPoolID, scene, 8, 30, PoolOverflowPolicy.Reject);
            }
        }
    }
}
