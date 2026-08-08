using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

[TestSuite]
[RequireGodotRuntime]
public class SceneSmokeTests {
    private static readonly string[] RequiredPrototypeScenes = {
        "res://scenes/menus/MainMenu.tscn",
        "res://scenes/arenas/TestArena.tscn",
        "res://scenes/campaign/HubWorld.tscn",
        "res://scenes/campaign/Level_00_Tutorial.tscn",
        "res://scenes/campaign/Level_01_Florence.tscn",
        // Package 5 Wave A.
        "res://scenes/campaign/Level_02_Orleans.tscn",
        "res://scenes/campaign/Level_03_Chicago.tscn",
        "res://scenes/campaign/Level_04_Paris.tscn",
        "res://scenes/campaign/Level_05_Titanic.tscn"
    };

    [TestCase]
    public void RequiredPrototypeScenesLoadAndInstantiate() {
        foreach (string path in RequiredPrototypeScenes) {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).IsNotNull();

            Node instance = scene.Instantiate();
            AssertObject(instance).IsNotNull();
            instance.Free();
        }
    }
}
