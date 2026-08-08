using System;
using Godot;

namespace FTT.Environment {

    [GlobalClass]
    public partial class FighterStageCatalog : Resource {
        public const string DefaultPath = "res://resources/FighterStages/stage_catalog.tres";

        [Export] public int SchemaVersion = 1;
        [Export] public FighterStageData[] Stages;

        public FighterStageData Find(string stageID) {
            foreach (FighterStageData stage in Stages ?? Array.Empty<FighterStageData>()) {
                if (stage?.StageID == stageID) return stage;
            }
            return null;
        }

        public FighterStageData FirstPlayable() {
            foreach (FighterStageData stage in Stages ?? Array.Empty<FighterStageData>()) {
                if (stage?.IsPlayable == true && ResourceLoader.Exists(stage.ScenePath)) return stage;
            }
            return null;
        }

        public static FighterStageCatalog LoadDefault() =>
            FTT.Core.AuthoredResources.Load<FighterStageCatalog>(DefaultPath);
    }
}
