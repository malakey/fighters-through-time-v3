using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B5. The seventeen Story <see cref="StageAudioSet"/> resources — the
/// tutorial, the hub, and levels 1-15 — against the paths the content manifest
/// reserved for them, and against the shared stem contract the ten Fighter stage
/// sets already satisfy.
///
/// <para>These sets are the one piece of B5 content whose absence is completely
/// silent: a missing or misnamed file makes a level play no music at all, with no
/// error and no visible symptom. So every row is resolved the way the game resolves
/// it — through <see cref="AudioSetPaths"/> — rather than by re-listing the file
/// names here, which would let the resolver and the content drift apart while both
/// halves looked correct.</para>
///
/// <para>The manifest rows themselves stay <c>Planned</c> until Package 8 C1 flips
/// them; this suite deliberately asserts the reserved <em>paths</em>, not the
/// implementation state.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryAudioSetContentTests {

    /// <summary>manifest row id -> the level/scene identity written into StageID.</summary>
    private static readonly (string SetID, string StageID)[] StorySets = {
        ("audio_tutorial", "level_00_tutorial"),
        ("audio_hub", AudioSetPaths.HubStageID),
        ("audio_level_01", "level_01_florence"),
        ("audio_level_02", "level_02_orleans"),
        ("audio_level_03", "level_03_chicago"),
        ("audio_level_04", "level_04_paris"),
        ("audio_level_05", "level_05_titanic"),
        ("audio_level_06", "level_06_pompeii"),
        ("audio_level_07", "level_07_nassau"),
        ("audio_level_08", "level_08_egypt"),
        ("audio_level_09", "level_09_berlin"),
        ("audio_level_10", "level_10_globe"),
        ("audio_level_11", "level_11_gettysburg"),
        ("audio_level_12", "level_12_lunar"),
        ("audio_level_13", "level_13_chronal_void"),
        ("audio_level_14", "level_14_neo_earth"),
        ("audio_level_15", "level_15_alexandria")
    };

    [TestCase]
    public void EverySeventeenStorySetExistsAtItsManifestPathAndSatisfiesTheStemContract() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        Dictionary<string, string> rows = manifest.ForCategory(ContentCategory.AudioSet)
            .ToDictionary(entry => entry.ContentID, entry => entry.ResourcePath);

        var issues = new List<string>();
        foreach ((string setID, string stageID) in StorySets) {
            if (!rows.TryGetValue(setID, out string path)) {
                issues.Add($"{setID}: no manifest row");
                continue;
            }
            if (!ResourceLoader.Exists(path)) {
                issues.Add($"{setID}: nothing authored at {path}");
                continue;
            }
            var set = AuthoredResources.Load<StageAudioSet>(path);
            foreach (string issue in StageAudioSetTests.Validate(set, setID, stageID)) {
                issues.Add($"{setID}: {issue}");
            }
        }

        AssertThat(StorySets.Length).IsEqual(17);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The resolver is the only thing standing between a level id and its music, and
    /// it derives the path rather than reading it from a table, so it gets checked
    /// against the authored files for every campaign slot.
    /// </summary>
    [TestCase]
    public void TheResolverLandsEveryCampaignLevelOnAnAuthoredSet() {
        string[] levelIDs = {
            "level_00_tutorial", "level_01_florence", "level_02_orleans", "level_03_chicago",
            "level_04_paris", "level_05_titanic", "level_06_pompeii", "level_07_nassau",
            "level_08_egypt", "level_09_berlin", "level_10_globe", "level_11_gettysburg",
            "level_12_lunar", "level_13_chronal_void", "level_14_neo_earth", "level_15_alexandria"
        };

        var issues = new List<string>();
        foreach (string levelID in levelIDs) {
            string path = AudioSetPaths.ForStoryLevel(levelID);
            if (string.IsNullOrEmpty(path)) { issues.Add($"{levelID}: resolved to nothing"); continue; }
            if (!ResourceLoader.Exists(path)) issues.Add($"{levelID}: resolved to missing {path}");
        }

        AssertThat(AudioSetPaths.ForStoryLevel("level_00_tutorial")).IsEqual(AudioSetPaths.Tutorial);
        AssertThat(ResourceLoader.Exists(AudioSetPaths.Hub)).IsTrue();
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// A resolver that answers confidently for garbage would hand a controller a
    /// path that silently fails to load. Anything that is not a real campaign slot
    /// must come back empty so the caller attaches no director at all.
    /// </summary>
    [TestCase]
    public void TheResolverRefusesAnythingThatIsNotACampaignLevelID() {
        AssertThat(AudioSetPaths.ForStoryLevel("")).IsEqual("");
        AssertThat(AudioSetPaths.ForStoryLevel(null)).IsEqual("");
        AssertThat(AudioSetPaths.ForStoryLevel("hub")).IsEqual("");
        AssertThat(AudioSetPaths.ForStoryLevel("level_16_beyond")).IsEqual("");
        AssertThat(AudioSetPaths.ForStoryLevel("level_2_orleans")).IsEqual("");
        AssertThat(AudioSetPaths.ForStoryLevel("stage_orleans")).IsEqual("");
    }

    /// <summary>
    /// The same resolver serves Fighter Mode, where the era token is the first
    /// segment of the catalog stage id. All ten Package 6 stages must land on the
    /// set their own per-stage suite already validates.
    /// </summary>
    [TestCase]
    public void TheResolverLandsEveryFighterStageOnItsAuthoredSet() {
        FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
        AssertThat(catalog).IsNotNull();

        var issues = new List<string>();
        int checkedStages = 0;
        foreach (FighterStageData stage in catalog.Stages) {
            checkedStages++;
            string path = AudioSetPaths.ForFighterStage(stage.StageID);
            if (!ResourceLoader.Exists(path)) {
                issues.Add($"{stage.StageID}: resolved to missing {path}");
                continue;
            }
            var set = AuthoredResources.Load<StageAudioSet>(path);
            if (set?.StageID != stage.StageID) {
                issues.Add($"{stage.StageID}: set at {path} claims StageID '{set?.StageID}'");
            }
        }

        AssertThat(checkedStages).IsEqual(10);
        AssertThat(AudioSetPaths.ForFighterStage("")).IsEqual("");
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The two shared environment cues B5 plays directly (hazard telegraph/impact and
    /// loot pickup) resolve. <c>AudioManager.PlayOneShot</c> silently returns on a
    /// null stream, so a moved placeholder goes quiet rather than erroring.
    /// </summary>
    [TestCase]
    public void TheSharedEnvironmentCuesResolveToRealStreams() {
        AssertThat(ResourceLoader.Exists(EnvironmentAudioCues.HazardCuePath)).IsTrue();
        AssertThat(ResourceLoader.Exists(EnvironmentAudioCues.PickupCuePath)).IsTrue();
        AssertThat(EnvironmentAudioCues.Hazard).IsNotNull();
        AssertThat(EnvironmentAudioCues.Pickup).IsNotNull();
    }
}
