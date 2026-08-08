using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class ContentTemplateContractTests {
    private static readonly string[] TemplatePaths = {
        "res://scenes/templates/StoryLevelTemplate.tscn",
        "res://scenes/templates/FighterStageTemplate.tscn",
        "res://scenes/templates/PlayerPresentationTemplate.tscn",
        "res://scenes/templates/StandardEnemyTemplate.tscn",
        "res://scenes/templates/EliteEnemyTemplate.tscn",
        "res://scenes/templates/BossTemplate.tscn",
        "res://scenes/templates/ProjectileTemplate.tscn",
        "res://scenes/templates/PersistentConstructTemplate.tscn",
        "res://scenes/templates/PickupTemplate.tscn",
        "res://scenes/templates/CheckpointTemplate.tscn",
        "res://scenes/templates/DialogueTriggerTemplate.tscn",
        "res://scenes/templates/PuzzleObjectTemplate.tscn",
        "res://scenes/templates/HazardTemplate.tscn",
        "res://scenes/templates/PooledVfxTemplate.tscn"
    };

    [TestCase]
    public void EveryAuthoredTemplateSatisfiesItsSceneContract() {
        foreach (string path in TemplatePaths) {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).IsNotNull();
            Node instance = scene.Instantiate();
            var marker = instance.GetNodeOrNull<ContentTemplateMarker>("ContentContract");
            AssertObject(marker).IsNotNull();
            AssertThat(marker.ValidateTemplate().Count).IsEqual(0);
            instance.Free();
        }
    }

    [TestCase]
    public void ContractValidatorReportsMissingRequiredNodes() {
        var root = new Node2D();
        var contract = new ContentSceneContract {
            ContractID = "test_contract",
            RequiredNodePaths = new[] { "MissingNode" }
        };

        var errors = ContentSceneContractValidator.Validate(root, contract);

        AssertThat(errors.Count).IsEqual(1);
        AssertThat(errors[0].Contains("MissingNode")).IsTrue();
        root.Free();
    }
}
