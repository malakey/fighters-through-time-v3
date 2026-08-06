using System.Linq;
using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

[TestSuite]
[RequireGodotRuntime]
public class ContentManifestTests {
    [TestCase]
    public void DefaultManifestCoversEveryInitialReleaseContentFamilyWithoutContractErrors() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var issues = ContentManifestValidator.Validate(manifest);

        AssertThat(manifest.SchemaVersion).IsEqual(ContentManifest.CurrentSchemaVersion);
        AssertThat(ContentManifestValidator.HasErrors(issues)).IsFalse();
        AssertThat(manifest.ForCategory(ContentCategory.StoryLevel).Count()).IsEqual(16);
        AssertThat(manifest.ForCategory(ContentCategory.FighterStage).Count()).IsEqual(10);
        AssertThat(manifest.ForCategory(ContentCategory.Character).Count()).IsEqual(9);
        AssertThat(manifest.ForCategory(ContentCategory.Ability).Count()).IsEqual(36);
        AssertThat(manifest.ForCategory(ContentCategory.ResonanceGrid).Count()).IsEqual(9);
        AssertThat(manifest.ForCategory(ContentCategory.Enemy).Count() >= 26).IsTrue();
        AssertThat(manifest.ForCategory(ContentCategory.Boss).Count()).IsEqual(15);
        AssertThat(manifest.ForCategory(ContentCategory.Template).Count()).IsEqual(14);
    }

    [TestCase]
    public void PlannedResourcesRemainVisibleUntilTheyAreAuthored() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var issues = ContentManifestValidator.Validate(manifest);

        AssertThat(issues.Count(issue => issue.Severity == ManifestIssueSeverity.Warning) > 0).IsTrue();
        AssertThat(manifest.Entries.Any(entry =>
            entry.ValidationState == ContentValidationState.Pending &&
            entry.ImplementationState == ContentImplementationState.Planned)).IsTrue();
    }

    [TestCase]
    public void FighterStageTargetsAreIndependentScenesRatherThanTestArenaAliases() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        string[] paths = manifest.ForCategory(ContentCategory.FighterStage)
            .Select(entry => entry.ResourcePath)
            .ToArray();

        AssertThat(paths.Distinct().Count()).IsEqual(10);
        AssertThat(paths.All(path => path != "res://scenes/arenas/TestArena.tscn")).IsTrue();
    }
}
