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

        // 2026-11-03 gameplay review (9a+): five of the ten stages are Open, and
        // Florence stays the Sealed baseline.
        var open = new HashSet<string>();
        foreach (FighterStageData stage in catalog.Stages) {
            if (stage.IsOpenStage) open.Add(stage.StageID);
        }
        AssertThat(open.Count).IsEqual(5);
        foreach (string openID in new[] {
            "paris_bastille", "vesuvius_caldera", "nassau_flagship", "globe_theatre", "berlin_wall" }) {
            AssertThat(open.Contains(openID)).OverrideFailureMessage($"{openID} must be Open").IsTrue();
        }
        AssertThat(open.Contains("florence_workshop")).IsFalse();
        foreach (string stageID in GlobalSaveData.InitialStageIDs) {
            AssertThat(ids.Contains(stageID)).IsTrue();
        }
    }
}
