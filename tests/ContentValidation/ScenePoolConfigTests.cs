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
    /// Package 4 Section 3.6: per-level warm-up budgets for levels 2-15. A config
    /// enters <c>scene_pool_catalog.tres</c> when its Package 5 scene lands (levels
    /// 2-5 are mapped as of the Wave A integration); the catalog only maps scenes
    /// that exist, so 6-15 stay out until their waves merge.
    /// </summary>
    private static readonly string[] LevelConfigPaths =
        Enumerable.Range(2, 14)
            .Select(level => $"res://resources/Pools/level_pool_configs/level_{level:00}_pool_config.tres")
            .ToArray();

    /// <summary>
    /// Package 6 closeout: the ten production-contract Fighter stages. Florence
    /// warmed no pools at all before this package — it had no config and no catalog
    /// row — so a Florence match paid a cold first spawn for every projectile, orb
    /// and damage number.
    /// </summary>
    private static readonly string[] FighterStageEras = {
        "florence", "orleans", "chicago", "paris", "vesuvius",
        "nassau", "alexandria", "berlin", "globe", "gettysburg"
    };

    private static readonly string[] FighterStageConfigPaths =
        FighterStageEras
            .Select(era => $"res://resources/Pools/fighter_stage_configs/fighter_stage_{era}_pool_config.tres")
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

    /// <summary>
    /// Package 6 §6 item 2: every Fighter stage carries its own valid, uniquely
    /// identified warm-up budget, and that budget matches the Test Arena's — a
    /// stage that quietly halved a pool would still be "valid" but would spawn
    /// cold mid-match.
    /// </summary>
    [TestCase]
    public void EveryFighterStagePoolConfigIsAuthoredValidAndUniquelyIdentified() {
        AssertThat(FighterStageConfigPaths.Length).IsEqual(10);
        ScenePoolConfig arena = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(
            "res://resources/Pools/test_arena_pool_config.tres");
        AssertObject(arena).IsNotNull();

        var configIDs = new HashSet<string>();
        for (int index = 0; index < FighterStageConfigPaths.Length; index++) {
            string path = FighterStageConfigPaths[index];
            string era = FighterStageEras[index];
            ScenePoolConfig config = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(path);
            AssertObject(config).OverrideFailureMessage($"{path} did not load.").IsNotNull();

            IReadOnlyList<string> errors = config.ValidateBudget();
            AssertThat(errors.Count).OverrideFailureMessage(
                $"{path}: {string.Join("; ", errors)}").IsEqual(0);
            AssertThat(config.ConfigID).OverrideFailureMessage(
                $"{path} declares ConfigID '{config.ConfigID}'.")
                .IsEqual($"fighter_stage_{era}_pools");
            AssertThat(configIDs.Add(config.ConfigID)).OverrideFailureMessage(
                $"{path} reuses ConfigID '{config.ConfigID}'.").IsTrue();
            AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
            AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();

            // Plan Section 5 item 3: each stage mirrors the Test Arena's pool set
            // and budgets. Comparing the totals catches a silently trimmed pool
            // that a validity-only check would wave through.
            AssertThat(config.GetWarmUpInstanceCount()).OverrideFailureMessage(
                $"{path} warms {config.GetWarmUpInstanceCount()} instances; the Test Arena " +
                $"warms {arena.GetWarmUpInstanceCount()}.").IsEqual(arena.GetWarmUpInstanceCount());
            AssertThat(config.GetMaxCapacityCount()).OverrideFailureMessage(
                $"{path} caps at {config.GetMaxCapacityCount()}; the Test Arena " +
                $"caps at {arena.GetMaxCapacityCount()}.").IsEqual(arena.GetMaxCapacityCount());

            foreach (string poolID in new[] {
                "fighter_projectile", "fighter_vfx", "fighter_environment_vfx",
                "chronal_orb", "damage_numbers", "fighter_construct"
            }) {
                AssertThat(config.PoolDefinitions.Any(d => d.PoolID == poolID)).OverrideFailureMessage(
                    $"{path} does not warm the '{poolID}' pool.").IsTrue();
            }
        }
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

        // Package 5 Wave A: every authored campaign level must resolve to its own
        // per-level budget, or the level warms nothing and spawns cold.
        foreach ((string scene, string configID) in new[] {
            ("res://scenes/campaign/Level_02_Orleans.tscn", "level_02_orleans_pools"),
            ("res://scenes/campaign/Level_03_Chicago.tscn", "level_03_chicago_pools"),
            ("res://scenes/campaign/Level_04_Paris.tscn", "level_04_paris_pools"),
            ("res://scenes/campaign/Level_05_Titanic.tscn", "level_05_titanic_pools"),
            // Package 5 Wave B. Level 10's config id is the era name, not the file
            // stem - a row pointed at the wrong config warms another level's budget
            // and nothing else in the suite would notice.
            ("res://scenes/campaign/Level_06_Pompeii.tscn", "level_06_pompeii_pools"),
            ("res://scenes/campaign/Level_07_Nassau.tscn", "level_07_nassau_pools"),
            ("res://scenes/campaign/Level_08_Egypt.tscn", "level_08_egypt_pools"),
            ("res://scenes/campaign/Level_09_Berlin.tscn", "level_09_berlin_pools"),
            ("res://scenes/campaign/Level_10_Globe.tscn", "level_10_globe_pools"),
            ("res://scenes/campaign/Level_11_Gettysburg.tscn", "level_11_gettysburg_pools"),
            ("res://scenes/campaign/Level_12_Lunar.tscn", "level_12_lunar_pools"),
            // Package 5 Wave C. Levels 13-15 use era-name config ids too, so the same
            // silent mis-point is possible here.
            ("res://scenes/campaign/Level_13_ChronalVoid.tscn", "level_13_chronal_void_pools"),
            ("res://scenes/campaign/Level_14_NeoEarth.tscn", "level_14_neo_earth_pools"),
            ("res://scenes/campaign/Level_15_Alexandria.tscn", "level_15_alexandria_pools"),
            // Package 6 closeout: the ten Fighter stages. Several era names are
            // shared with a campaign level (orleans, chicago, paris, nassau,
            // berlin, globe, gettysburg, alexandria), so a row pointed at the
            // level's config would warm Story enemy pools in a Fighter match and
            // nothing else in the suite would notice.
            ("res://scenes/fighter/FighterStage_Florence.tscn", "fighter_stage_florence_pools"),
            ("res://scenes/fighter/FighterStage_Orleans.tscn", "fighter_stage_orleans_pools"),
            ("res://scenes/fighter/FighterStage_Chicago.tscn", "fighter_stage_chicago_pools"),
            ("res://scenes/fighter/FighterStage_Paris.tscn", "fighter_stage_paris_pools"),
            ("res://scenes/fighter/FighterStage_Vesuvius.tscn", "fighter_stage_vesuvius_pools"),
            ("res://scenes/fighter/FighterStage_Nassau.tscn", "fighter_stage_nassau_pools"),
            ("res://scenes/fighter/FighterStage_Alexandria.tscn", "fighter_stage_alexandria_pools"),
            ("res://scenes/fighter/FighterStage_Berlin.tscn", "fighter_stage_berlin_pools"),
            ("res://scenes/fighter/FighterStage_Globe.tscn", "fighter_stage_globe_pools"),
            ("res://scenes/fighter/FighterStage_Gettysburg.tscn", "fighter_stage_gettysburg_pools")
        }) {
            ScenePoolConfig config = catalog.Find(scene);
            AssertObject(config).OverrideFailureMessage(
                $"{scene} has no scene_pool_catalog.tres entry.").IsNotNull();
            AssertThat(config.ConfigID).OverrideFailureMessage(
                $"{scene} maps to '{config.ConfigID}', expected '{configID}'.").IsEqual(configID);
        }
    }
}
