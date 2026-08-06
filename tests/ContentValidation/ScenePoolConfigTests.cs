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

    [TestCase]
    public void InitialScenePoolBudgetsAreValidAndBounded() {
        foreach (string path in ConfigPaths) {
            ScenePoolConfig config = ResourceLoader.Load<ScenePoolConfig>(path);
            AssertObject(config).IsNotNull();
            AssertThat(config.ValidateBudget().Count).IsEqual(0);
            AssertThat(config.GetWarmUpInstanceCount() <= config.MaxWarmUpInstances).IsTrue();
            AssertThat(config.GetMaxCapacityCount() >= config.GetWarmUpInstanceCount()).IsTrue();
        }
    }

    [TestCase]
    public void CatalogMapsEveryCurrentGameplaySceneToItsPoolBudget() {
        ScenePoolCatalog catalog = ScenePoolCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();
        AssertObject(catalog.Find("res://scenes/campaign/Level_00_Tutorial.tscn")).IsNotNull();
        AssertObject(catalog.Find("res://scenes/campaign/Level_01_Florence.tscn")).IsNotNull();
        AssertObject(catalog.Find("res://scenes/arenas/TestArena.tscn")).IsNotNull();
    }
}
