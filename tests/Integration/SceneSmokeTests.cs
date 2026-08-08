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
        "res://scenes/campaign/Level_15_Alexandria.tscn",
        // Package 6: all ten production-contract Fighter stages. Florence shipped
        // before this list existed and was never added; the other nine land with
        // the Package 6 closeout, at which point every catalog ScenePath resolves
        // to its own scene instead of aliasing the Test Arena.
        "res://scenes/fighter/FighterStage_Florence.tscn",
        "res://scenes/fighter/FighterStage_Orleans.tscn",
        "res://scenes/fighter/FighterStage_Chicago.tscn",
        "res://scenes/fighter/FighterStage_Paris.tscn",
        "res://scenes/fighter/FighterStage_Vesuvius.tscn",
        "res://scenes/fighter/FighterStage_Nassau.tscn",
        "res://scenes/fighter/FighterStage_Alexandria.tscn",
        "res://scenes/fighter/FighterStage_Berlin.tscn",
        "res://scenes/fighter/FighterStage_Globe.tscn",
        "res://scenes/fighter/FighterStage_Gettysburg.tscn"
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
