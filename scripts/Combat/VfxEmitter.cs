using System;
using FTT.Core;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Spawns short-lived visual effects from the pools every scene already warms.
    /// Before Package 8 the <c>combat_vfx</c> / <c>environment_vfx</c> /
    /// <c>fighter_vfx</c> / <c>fighter_environment_vfx</c> pools were registered by
    /// every pool config but nothing ever spawned from them, and
    /// <c>AbilityData.CastVFXScene</c> / <c>ImpactVFXScene</c> were exported and
    /// consumed by nothing.
    ///
    /// Everything here is null-safe and best-effort: a missing pool, missing scene,
    /// or missing parent returns null rather than throwing, because B6 authors the
    /// actual VFX resources afterwards and gameplay must never depend on them.
    /// </summary>
    public static class VfxEmitter {
        public const string CombatPoolID = "combat_vfx";
        public const string EnvironmentPoolID = "environment_vfx";
        public const string FighterCombatPoolID = "fighter_vfx";
        public const string FighterEnvironmentPoolID = "fighter_environment_vfx";

        private const string CombatFallbackScenePath = "res://scenes/templates/PooledVfxTemplate.tscn";
        private const string EnvironmentFallbackScenePath = "res://scenes/templates/EnvironmentalVfxTemplate.tscn";
        private const int FallbackWarmUp = 4;
        private const int FallbackCapacity = 32;

        /// <summary>
        /// Spawns one pooled effect. Falls back from the Story pool ID to the
        /// Fighter one (and finally to registering the shared template) so a single
        /// call site works in both modes.
        /// </summary>
        public static Node2D Emit(string poolID, Vector2 position, Node parent, Color tint = default) {
            PoolManager pools = PoolManager.Instance;
            if (pools == null || parent == null || !IsInstanceUsable(parent)) return null;

            string resolved = ResolvePool(pools, poolID);
            if (resolved == null) return null;

            if (pools.Spawn(resolved, position, parent) is not Node2D spawned) return null;
            if (tint.A > 0f) spawned.Modulate = tint;
            return spawned;
        }

        /// <summary>Combat-tier effect (ability cast, impact, hit spark).</summary>
        public static Node2D EmitCombat(Vector2 position, Node parent, Color tint = default) =>
            Emit(CombatPoolID, position, parent, tint);

        /// <summary>Environmental-tier effect (hazard, pickup, destruction).</summary>
        public static Node2D EmitEnvironment(Vector2 position, Node parent, Color tint = default) =>
            Emit(EnvironmentPoolID, position, parent, tint);

        /// <summary>
        /// Spawns an authored one-off VFX <c>PackedScene</c> through its own pool,
        /// keyed on the scene path. Used for <c>AbilityData.CastVFXScene</c> and
        /// <c>ImpactVFXScene</c>; returns null when the field is unassigned.
        /// </summary>
        public static Node2D EmitScene(PackedScene scene, Vector2 position, Node parent, Color tint = default) {
            PoolManager pools = PoolManager.Instance;
            if (scene == null || pools == null || parent == null || !IsInstanceUsable(parent)) return null;
            string poolID = string.IsNullOrEmpty(scene.ResourcePath)
                ? scene.GetInstanceId().ToString()
                : scene.ResourcePath;
            if (!pools.IsRegistered(poolID)) {
                pools.RegisterPool(poolID, scene, 1, FallbackCapacity, PoolOverflowPolicy.RecycleOldest);
            }
            if (pools.Spawn(poolID, position, parent) is not Node2D spawned) return null;
            if (tint.A > 0f) spawned.Modulate = tint;
            return spawned;
        }

        /// <summary>
        /// Spawns one of the six shared ability-family scenes through an
        /// ability-specific pool, then replaces its placeholder shape with the
        /// ability's authored retro atlas animation. The distinct pool key keeps
        /// that replacement from leaking into another ability or an enemy effect
        /// that shares the same base PackedScene.
        /// </summary>
        public static Node2D EmitAbilityScene(PackedScene scene, AbilityData data,
            Vector2 position, Node parent, Color fallbackTint = default, bool impact = false) {
            PoolManager pools = PoolManager.Instance;
            if (scene == null || data == null || pools == null || parent == null
                    || !IsInstanceUsable(parent)) return null;

            string sceneKey = string.IsNullOrEmpty(scene.ResourcePath)
                ? scene.GetInstanceId().ToString()
                : scene.ResourcePath;
            string poolID = $"{sceneKey}::{data.AbilityID}::{(impact ? "impact" : "cast")}";
            if (!pools.IsRegistered(poolID)) {
                pools.RegisterPool(poolID, scene, 1, FallbackCapacity, PoolOverflowPolicy.RecycleOldest);
            }
            if (pools.Spawn(poolID, position, parent) is not Node2D spawned) return null;

            bool authored = false;
            if (spawned is VfxEffect effect
                    && AbilityVisualLibrary.TryResolve(data.AbilityID,
                        out SpriteFrames frames, out StringName animation)) {
                float scale = data.Slot switch {
                    AbilitySlot.Ultimate => impact ? 0.8f : 1.05f,
                    AbilitySlot.MovementAbility => 0.5f,
                    _ => impact ? 0.44f : 0.58f
                };
                authored = effect.UseAnimatedVisual(frames, animation, scale);
            }

            spawned.Modulate = authored || fallbackTint.A <= 0f ? Colors.White : fallbackTint;
            return spawned;
        }

        /// <summary>
        /// Spawns a roster presentation effect through an event-specific pool and
        /// replaces the taxonomy placeholder with the owning enemy or boss atlas.
        /// Resolution happens before spawn and the visual is reapplied on every
        /// checkout because PoolManager aliases IDs sharing one taxonomy scene.
        /// </summary>
        public static Node2D EmitEnemyAbilityScene(PackedScene scene, string presentationEventID,
            EnemyPresentationPhase phase, Vector2 position, Node parent,
            Color fallbackTint = default) {
            PoolManager pools = PoolManager.Instance;
            if (scene == null || string.IsNullOrWhiteSpace(presentationEventID)
                    || pools == null || parent == null || !IsInstanceUsable(parent)) return null;

            // PoolManager aliases IDs that share one PackedScene to the same
            // underlying pool. Do not spawn first and resolve later: an unresolved
            // beat (notably "{enemyID}.death", which has no ability-atlas row)
            // could otherwise reuse a VfxEffect whose AnimatedSprite2D still holds
            // a previous ability's frames instead of taking the generic fallback.
            if (!EnemyAbilityVisualLibrary.TryResolve(presentationEventID,
                    out SpriteFrames frames, out StringName animation, out bool isBoss)) {
                return null;
            }

            string sceneKey = string.IsNullOrEmpty(scene.ResourcePath)
                ? scene.GetInstanceId().ToString()
                : scene.ResourcePath;
            string poolID = $"{sceneKey}::{presentationEventID}::{phase}";
            if (!pools.IsRegistered(poolID)) {
                pools.RegisterPool(poolID, scene, 1, FallbackCapacity,
                    PoolOverflowPolicy.RecycleOldest);
            }
            if (pools.Spawn(poolID, position, parent) is not Node2D spawned) return null;

            bool authored = false;
            if (spawned is VfxEffect effect) {
                float scale = isBoss ? 0.9f : 0.62f;
                if (phase == EnemyPresentationPhase.Telegraph) scale *= 0.82f;
                authored = effect.UseAnimatedVisual(frames, animation, scale);
            }
            spawned.Modulate = authored || fallbackTint.A <= 0f ? Colors.White : fallbackTint;
            return spawned;
        }

        /// <summary>
        /// Maps an authored <c>PresentationEventID</c> to a pooled effect. The roster
        /// convention is <c>{sourceID}.{telegraph|active|recovery|death}</c>; B6
        /// refines the per-suffix mappings on top of this default routing.
        /// </summary>
        public static Node2D EmitForPresentationEvent(string presentationEventID, EnemyPresentationPhase phase,
            Vector2 position, Node parent) {
            if (parent == null) return null;
            Color tint = phase switch {
                EnemyPresentationPhase.Telegraph => new Color(1f, 0.85f, 0.3f, 0.75f),
                EnemyPresentationPhase.Active => new Color(1f, 0.45f, 0.2f, 0.9f),
                EnemyPresentationPhase.Recovery => new Color(0.6f, 0.7f, 0.9f, 0.5f),
                _ => new Color(0.85f, 0.3f, 0.9f, 0.85f)
            };
            string pool = phase == EnemyPresentationPhase.Death ? EnvironmentPoolID : CombatPoolID;
            _ = presentationEventID; // B6 keys bespoke mappings off the suffix.
            return Emit(pool, position, parent, tint);
        }

        /// <summary>
        /// Resolves a usable pool ID, trying the Fighter twin and finally registering
        /// the shared placeholder template so a scene that never warmed a VFX pool
        /// still gets an effect instead of silently dropping it.
        /// </summary>
        private static string ResolvePool(PoolManager pools, string poolID) {
            if (!string.IsNullOrWhiteSpace(poolID) && pools.IsRegistered(poolID)) return poolID;

            string twin = poolID switch {
                CombatPoolID => FighterCombatPoolID,
                FighterCombatPoolID => CombatPoolID,
                EnvironmentPoolID => FighterEnvironmentPoolID,
                FighterEnvironmentPoolID => EnvironmentPoolID,
                _ => null
            };
            if (twin != null && pools.IsRegistered(twin)) return twin;

            string fallbackScene = poolID is EnvironmentPoolID or FighterEnvironmentPoolID
                ? EnvironmentFallbackScenePath
                : CombatFallbackScenePath;
            if (string.IsNullOrWhiteSpace(poolID) || !ResourceLoader.Exists(fallbackScene)) return null;
            // Scenes are streamed presentation, never AuthoredResources cache entries.
            var packed = ResourceLoader.Load<PackedScene>(fallbackScene);
            if (packed == null) return null;
            pools.RegisterPool(poolID, packed, FallbackWarmUp, FallbackCapacity, PoolOverflowPolicy.RecycleOldest);
            return poolID;
        }

        private static bool IsInstanceUsable(Node node) =>
            GodotObject.IsInstanceValid(node) && node.IsInsideTree();
    }
}
