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
        // The sixteen campaign levels. Package 11 raised this to 25 with the nine
        // Level 4A variants; Package 13 W2 (S27) retired them.
        AssertThat(manifest.ForCategory(ContentCategory.StoryLevel).Count()).IsEqual(16);
        AssertThat(manifest.ForCategory(ContentCategory.FighterStage).Count()).IsEqual(10);
        // Package 11 A6b: the roster-sized rows are pinned as AGREEMENT rather
        // than as the number nine. This is the single surviving manifest
        // roster-count pin, so an accidental deletion still fails hard, while a
        // deliberate roster addition flows through every derived count with no
        // test edit. Four abilities and one Resonance grid per character are
        // real per-character contracts, so they scale with the roster instead of
        // standing as independent magic numbers.
        int rosterCount = FTT.Core.CharacterRoster.Count;
        AssertThat(rosterCount > 0).IsTrue();
        AssertThat(manifest.ForCategory(ContentCategory.Character).Count()).IsEqual(rosterCount);
        AssertThat(manifest.ForCategory(ContentCategory.Ability).Count()).IsEqual(rosterCount * 4);
        AssertThat(manifest.ForCategory(ContentCategory.ResonanceGrid).Count()).IsEqual(rosterCount);
        AssertThat(manifest.ForCategory(ContentCategory.Enemy).Count() >= 26).IsTrue();
        AssertThat(manifest.ForCategory(ContentCategory.Boss).Count()).IsEqual(15);
        AssertThat(manifest.ForCategory(ContentCategory.Template).Count()).IsEqual(14);
    }

    /// <summary>
    /// The manifest must keep surfacing unauthored content rather than quietly
    /// reporting a finished game.
    ///
    /// <para>Package 5 C1 note: this used to be backed largely by the Planned
    /// StoryLevel rows for levels 2-15. All sixteen levels are Implemented now, so
    /// the premise rests on the remaining families instead — the 27 per-level
    /// AudioSet rows (Package 8), the nine unbuilt FighterStage rows, the twelve
    /// unbuilt UIScreen rows, and three DialogueSet rows. If a later package
    /// authors ALL of those, this assertion's premise is genuinely gone and the
    /// right move is to retire the test with a note, not to invent a Planned row
    /// to keep it fed.</para>
    /// </summary>
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
