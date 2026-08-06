using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class FighterStageCatalogTests {
    [TestCase]
    public void InitialCatalogDefinesTenPlayableLocalizedDeterministicStages() {
        TranslationServer.SetLocale("en");
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();
        AssertThat(catalog.Stages.Length).IsEqual(10);
        var ids = new HashSet<string>();
        var hazardTypes = new HashSet<int>();
        foreach (FighterStageData stage in catalog.Stages) {
            AssertObject(stage).IsNotNull();
            AssertThat(ids.Add(stage.StageID)).IsTrue();
            AssertThat(hazardTypes.Add(stage.HazardTypeID)).IsTrue();
            AssertThat(stage.IsPlayable).IsTrue();
            AssertThat(ResourceLoader.Exists(stage.ScenePath)).IsTrue();
            AssertThat(TranslationServer.Translate(stage.DisplayNameKey) != stage.DisplayNameKey).IsTrue();
            AssertThat(TranslationServer.Translate(stage.LayoutDescriptionKey) != stage.LayoutDescriptionKey).IsTrue();
            AssertThat(TranslationServer.Translate(stage.HazardNameKey) != stage.HazardNameKey).IsTrue();
            AssertThat(TranslationServer.Translate(stage.HazardDescriptionKey) != stage.HazardDescriptionKey).IsTrue();
        }
        AssertThat(ids.Count).IsEqual(10);
        AssertThat(hazardTypes.Count).IsEqual(10);
        foreach (string stageID in GlobalSaveData.InitialStageIDs) {
            AssertThat(ids.Contains(stageID)).IsTrue();
        }
    }
}
