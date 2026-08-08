using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Cross-checks the content manifest's FighterStage/AudioSet/VisualSet rows against
/// the authored stage catalog and the deterministic geometry table.
///
/// <para>Package 6 §2.1: the manifest had shipped two stage ids the catalog never
/// used — <c>pompeii_caldera</c> and <c>egypt_chambers</c> versus the catalog's
/// <c>vesuvius_caldera</c> and <c>alexandria_chambers</c>. Because
/// <c>UnlockedStages</c> is persisted save data, the catalog spelling wins and the
/// manifest was renamed to match. Nothing enforced the agreement before, which is
/// how the drift survived; these tests are that enforcement.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStageManifestTests {

    [TestCase]
    public void EveryManifestFighterStageRowNamesACatalogStage() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();

        var catalogIDs = new HashSet<string>();
        foreach (FighterStageData stage in catalog.Stages) catalogIDs.Add(stage.StageID);

        string[] manifestIDs = manifest.ForCategory(ContentCategory.FighterStage)
            .Select(entry => entry.ContentID)
            .ToArray();

        AssertThat(manifestIDs.Length).IsEqual(catalogIDs.Count);
        foreach (string id in manifestIDs) {
            if (!catalogIDs.Contains(id)) {
                AssertThat($"manifest FighterStage '{id}' is not a catalog StageID").IsEqual("");
            }
        }
        foreach (string id in catalogIDs) {
            if (!manifestIDs.Contains(id)) {
                AssertThat($"catalog stage '{id}' has no manifest FighterStage row").IsEqual("");
            }
        }
    }

    /// <summary>
    /// The manifest's declared scene path must be the catalog's ScenePath once the
    /// stage is wired (closeout), and must at minimum never be the shared Test
    /// Arena alias.
    /// </summary>
    [TestCase]
    public void ManifestScenePathsAgreeWithTheCatalogWhereTheCatalogIsWired() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        const string testArena = "res://scenes/arenas/TestArena.tscn";

        foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.FighterStage)) {
            FighterStageData stage = catalog.Find(entry.ContentID);
            AssertObject(stage).IsNotNull();
            AssertThat(entry.ResourcePath).IsNotEqual(testArena);
            if (stage.ScenePath != testArena && stage.ScenePath != entry.ResourcePath) {
                AssertThat(
                    $"stage '{entry.ContentID}' catalog ScenePath '{stage.ScenePath}' " +
                    $"disagrees with manifest path '{entry.ResourcePath}'").IsEqual("");
            }
        }
    }

    /// <summary>
    /// The per-stage AudioSet and VisualSet rows are keyed by an era suffix. Each
    /// stage id begins with that era, so a renamed stage that forgets its audio or
    /// visual row is caught here rather than at Package 8.
    /// </summary>
    [TestCase]
    public void EveryStageHasMatchingAudioSetAndVisualSetRows() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();

        var audioIDs = new HashSet<string>(
            manifest.ForCategory(ContentCategory.AudioSet).Select(entry => entry.ContentID));
        var visualIDs = new HashSet<string>(
            manifest.ForCategory(ContentCategory.VisualSet).Select(entry => entry.ContentID));

        foreach (FighterStageData stage in catalog.Stages) {
            string era = stage.StageID.Split('_')[0];
            if (!audioIDs.Contains($"audio_stage_{era}")) {
                AssertThat($"missing manifest row 'audio_stage_{era}' for stage '{stage.StageID}'").IsEqual("");
            }
            if (!visualIDs.Contains($"visual_stage_{era}")) {
                AssertThat($"missing manifest row 'visual_stage_{era}' for stage '{stage.StageID}'").IsEqual("");
            }
        }
    }

    /// <summary>
    /// The retired spellings must not come back: they would silently orphan
    /// existing saves' <c>UnlockedStages</c> entries.
    /// </summary>
    [TestCase]
    public void RetiredStageSpellingsAreGoneFromTheManifest() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        string[] retired = {
            "pompeii_caldera", "egypt_chambers",
            "audio_stage_pompeii", "audio_stage_egypt",
            "visual_stage_pompeii", "visual_stage_egypt"
        };
        foreach (ContentManifestEntry entry in manifest.Entries) {
            if (retired.Contains(entry.ContentID)) {
                AssertThat($"retired manifest id '{entry.ContentID}' is back").IsEqual("");
            }
        }
    }

    /// <summary>
    /// Every catalog stage resolves to authored deterministic geometry with the
    /// hazard identity the catalog declares — the two halves of a stage's identity
    /// live in different files and nothing else ties them together.
    /// </summary>
    [TestCase]
    public void EveryCatalogStageHasAuthoredGeometryAndADistinctHazardIdentity() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        var hazardIDs = new HashSet<int>();
        foreach (FighterStageData stage in catalog.Stages) {
            FighterStageGeometry geometry = FighterStageGeometry.ForStage(stage.StageID);
            if (geometry.StageID != stage.StageID) {
                AssertThat($"stage '{stage.StageID}' falls back to the legacy flat arena").IsEqual("");
            }
            AssertThat(stage.HazardTypeID >= 1 && stage.HazardTypeID <= FighterHazardTypeID.Count).IsTrue();
            AssertThat(hazardIDs.Add(stage.HazardTypeID)).IsTrue();
        }
        AssertThat(hazardIDs.Count).IsEqual(FighterHazardTypeID.Count);
    }

    /// <summary>
    /// The preview-image slot exists on the schema so the closeout can populate it;
    /// it is deliberately still empty until every stage ships a preview.
    /// </summary>
    [TestCase]
    public void StageDataExposesAPreviewTextureSlot() {
        var stage = new FighterStageData();
        AssertThat(stage.PreviewTexturePath).IsEqual("");
    }
}
