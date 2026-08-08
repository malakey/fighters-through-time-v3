using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class ScenePoolConfigTests {
    private static readonly string[] ConfigPaths = {
        "res://resources/Pools/tutorial_pool_config.tres",
        "res://resources/Pools/florence_pool_config.tres",
        "res://resources/Pools/test_arena_pool_config.tres"
    };

    /// <summary>
    /// Package 4 Section 3.6: per-level warm-up budgets for levels 2-15. The scenes
    /// themselves are Package 5, so these are deliberately NOT in
    /// <c>scene_pool_catalog.tres</c> yet — the catalog only maps existing scenes.
    /// </summary>
    private static readonly string[] LevelConfigPaths =
        Enumerable.Range(2, 14)
            .Select(level => $"res://resources/Pools/level_pool_configs/level_{level:00}_pool_config.tres")
            .ToArray();

    // AGENTS.md performance budget proxies: <=60 active AnimatedSprite2D and the
    // plan's derived pool caps (standard+elite <=30 per level, projectiles <=40).
    private const int MaxWarmCombatants = 30;
    private const int MaxWarmEnemyProjectiles = 40;

    [TestCase]
    public void InitialScenePoolBudgetsAreValidAndBounded() {
        foreach (string path in ConfigPaths) {
            ScenePoolConfig config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(path);
            AssertObject(config).IsNotNull();
            AssertThat(config.ValidateBudget().Count).IsEqual(0);
            AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
            AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();
        }
    }

    [TestCase]
    public void EveryCampaignLevelPoolConfigIsAuthoredValidAndUniquelyIdentified() {
        AssertThat(LevelConfigPaths.Length).IsEqual(14);
        var configIDs = new HashSet<string>();

        foreach (string path in LevelConfigPaths) {
            ScenePoolConfig config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(path);
            AssertObject(config).OverrideFailureMessage($"{path} did not load.").IsNotNull();

            IReadOnlyList<string> errors = config.ValidateBudget();
            AssertThat(errors.Count).OverrideFailureMessage(
                $"{path}: {string.Join("; ", errors)}").IsEqual(0);
            AssertThat(configIDs.Add(config.ConfigID)).OverrideFailureMessage(
                $"{path} reuses ConfigID '{config.ConfigID}'.").IsTrue();
            AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
            AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

            foreach (PoolDefinition definition in config.PoolDefinitions) {
                AssertThat(definition.HasValidBudget()).OverrideFailureMessage(
                    $"{path} -> pool '{definition.PoolID}' has an invalid budget.").IsTrue();
            }

            // Every level must be able to serve the shared gameplay pools the Story
            // runtime spawns into; a missing definition means a cold first spawn.
            foreach (string poolID in new[] {
                "standard_enemy", "elite_enemy", "enemy_projectile", "chronal_dust",
                "story_item", "combat_vfx", "environment_vfx", "damage_numbers",
                "persistent_construct"
            }) {
                AssertThat(config.PoolDefinitions.Any(d => d.PoolID == poolID)).OverrideFailureMessage(
                    $"{path} does not warm the '{poolID}' pool.").IsTrue();
            }
        }
    }

    [TestCase]
    public void WorstCaseLevelEncounterStaysInsideThePerformanceBudget() {
        // Plan Section 3.6: the largest authored per-level warm-up must fit the
        // documented budgets before level production scales in Package 5.
        int worstCombatants = 0;
        int worstProjectiles = 0;
        string worstPath = "";

        foreach (string path in LevelConfigPaths) {
            ScenePoolConfig config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(path);
            int combatants = Warm(config, "standard_enemy") + Warm(config, "elite_enemy");
            int projectiles = Warm(config, "enemy_projectile");

            AssertThat(combatants <= MaxWarmCombatants).OverrideFailureMessage(
                $"{path} warms {combatants} combatants; the cap is {MaxWarmCombatants}.").IsTrue();
            AssertThat(projectiles <= MaxWarmEnemyProjectiles).OverrideFailureMessage(
                $"{path} warms {projectiles} enemy projectiles; the cap is {MaxWarmEnemyProjectiles}.").IsTrue();

            if (combatants > worstCombatants) {
                worstCombatants = combatants;
                worstPath = path;
            }
            worstProjectiles = Mathf.Max(worstProjectiles, projectiles);
        }

        // Guards against a silently empty sweep passing the caps vacuously.
        AssertThat(worstCombatants > 0 && worstProjectiles > 0).IsTrue();
        AssertThat(worstPath.Length > 0).IsTrue();
        AssertThat(worstCombatants <= MaxWarmCombatants).IsTrue();
    }

    private static int Warm(ScenePoolConfig config, string poolID) =>
        config.PoolDefinitions.FirstOrDefault(definition => definition.PoolID == poolID)?.WarmUpCount ?? 0;

    [TestCase]
    public void CatalogMapsEveryCurrentGameplaySceneToItsPoolBudget() {
        ScenePoolCatalog catalog = ScenePoolCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();
        AssertObject(catalog.Find("res://scenes/campaign/Level_00_Tutorial.tscn")).IsNotNull();
        AssertObject(catalog.Find("res://scenes/campaign/Level_01_Florence.tscn")).IsNotNull();
        AssertObject(catalog.Find("res://scenes/arenas/TestArena.tscn")).IsNotNull();
    }
}
