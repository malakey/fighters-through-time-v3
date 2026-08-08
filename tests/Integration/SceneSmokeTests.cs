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
        "res://scenes/campaign/Level_05_Titanic.tscn",
        // Package 5 Wave B.
        "res://scenes/campaign/Level_06_Pompeii.tscn",
        "res://scenes/campaign/Level_07_Nassau.tscn",
        "res://scenes/campaign/Level_08_Egypt.tscn",
        "res://scenes/campaign/Level_09_Berlin.tscn",
        "res://scenes/campaign/Level_10_Globe.tscn",
        "res://scenes/campaign/Level_11_Gettysburg.tscn",
        "res://scenes/campaign/Level_12_Lunar.tscn",
        // Package 5 Wave C.
        "res://scenes/campaign/Level_13_ChronalVoid.tscn",
        "res://scenes/campaign/Level_14_NeoEarth.tscn",
        "res://scenes/campaign/Level_15_Alexandria.tscn"
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
