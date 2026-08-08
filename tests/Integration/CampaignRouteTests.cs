using System;
using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 5 A1: the campaign route contract. The manifest, StoryManager's scene
/// table, and the on-disk scenes must agree for all sixteen levels. This suite
/// exists because level 10 shipped as `Level_10_London.tscn` in StoryManager while
/// the manifest and its pool config both said `level_10_globe` — a silent route
/// break that nothing caught until Package 5 planning.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignRouteTests {
    private const int CampaignLevelCount = 16;

    [TestCase]
    public void EveryManifestStoryLevelRowMatchesTheStoryManagerScenePath() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(CampaignLevelCount);

        for (int index = 0; index < CampaignLevelCount; index++) {
            string prefix = $"level_{index:00}_";
            ContentManifestEntry row = rows.FirstOrDefault(entry =>
                entry.ContentID.StartsWith(prefix, StringComparison.Ordinal));
            AssertThat(row != null)
                .OverrideFailureMessage($"No StoryLevel manifest row for campaign index {index}.")
                .IsTrue();

            string routed = StoryManager.GetLevelScenePath((CampaignLevel)index);
            AssertThat(row.ResourcePath == routed).OverrideFailureMessage(
                $"Manifest row '{row.ContentID}' points at '{row.ResourcePath}' but StoryManager " +
                $"routes {(CampaignLevel)index} to '{routed}'.").IsTrue();
        }
    }

    [TestCase]
    public void EveryCampaignLevelEnumValueRoutesToADistinctAuthoredScenePath() {
        var seen = new HashSet<string>();
        for (int index = 0; index < CampaignLevelCount; index++) {
            string path = StoryManager.GetLevelScenePath((CampaignLevel)index);
            AssertThat(path.StartsWith("res://scenes/campaign/", StringComparison.Ordinal)).IsTrue();
            AssertThat(path.EndsWith(".tscn", StringComparison.Ordinal)).IsTrue();
            AssertThat(seen.Add(path))
                .OverrideFailureMessage($"Duplicate campaign route: {path}").IsTrue();
        }
        AssertThat(seen.Count).IsEqual(CampaignLevelCount);
    }

    [TestCase]
    public void LevelTenRoutesToTheGlobeSceneTheManifestAndPoolConfigNames() {
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.London))
            .IsEqual("res://scenes/campaign/Level_10_Globe.tscn");

        // The Package 4 pool config already called level 10 "globe"; the route
        // said "London". This assertion is the tripwire for that split.
        var poolConfig = FTT.Core.AuthoredResources.Load<ScenePoolConfig>(
            "res://resources/Pools/level_pool_configs/level_10_pool_config.tres");
        AssertObject(poolConfig).IsNotNull();
        AssertString(poolConfig.ConfigID).IsEqual("level_10_globe_pools");
    }

    /// <summary>
    /// Package 5 C1 raised this from "every routed scene that happens to exist" to
    /// all sixteen. The campaign is fully authored, so a route pointing at a scene
    /// that is missing, unloadable, or not a campaign level is now a hard failure
    /// rather than a silently skipped index.
    /// </summary>
    [TestCase]
    public void EveryRoutedSceneExistsLoadsAndInstantiates() {
        var missing = new List<string>();
        for (int index = 0; index < CampaignLevelCount; index++) {
            string path = StoryManager.GetLevelScenePath((CampaignLevel)index);
            if (!ResourceLoader.Exists(path)) { missing.Add($"{(CampaignLevel)index} -> {path}"); continue; }

            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).OverrideFailureMessage($"{path} exists but did not load.").IsNotNull();
            Node instance = scene.Instantiate();
            AssertObject(instance)
                .OverrideFailureMessage($"{path} loaded but would not instantiate.").IsNotNull();
            instance.Free();
        }

        AssertThat(missing.Count == 0).OverrideFailureMessage(
            "Every campaign level 0-15 is authored; these routes resolve to nothing: " +
            string.Join(", ", missing)).IsTrue();
    }

    /// <summary>
    /// Package 5 C1: the manifest's own StoryLevel paths must all be on disk too.
    /// The route test above walks StoryManager; this walks the manifest, so a row
    /// edited to point somewhere plausible-but-absent cannot hide behind the
    /// path-agreement assertion alone.
    ///
    /// <para>The state assertion is "not <c>Planned</c>, and <c>Valid</c>", not
    /// "<c>Implemented</c>". Levels 2-15 are <c>Implemented</c>; the Tutorial and
    /// Florence are deliberately still <c>Prototype</c> — they predate Package 5,
    /// were built before the <c>StoryLevelControllerBase</c> convention, and were
    /// not retrofitted to it. What matters here is that no campaign level claims to
    /// be unbuilt when its scene is on disk.</para>
    /// </summary>
    [TestCase]
    public void EveryManifestStoryLevelRowPointsAtAnAuthoredScene() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(CampaignLevelCount);

        foreach (ContentManifestEntry row in rows) {
            AssertThat(ResourceLoader.Exists(row.ResourcePath)).OverrideFailureMessage(
                $"StoryLevel '{row.ContentID}' points at '{row.ResourcePath}', which is not on disk.")
                .IsTrue();
            AssertThat(row.ImplementationState != ContentImplementationState.Planned)
                .OverrideFailureMessage(
                    $"StoryLevel '{row.ContentID}' has an authored scene on disk but is " +
                    "still marked Planned.").IsTrue();
            AssertThat(row.ValidationState == ContentValidationState.Valid)
                .OverrideFailureMessage(
                    $"StoryLevel '{row.ContentID}' is authored but its validation state is " +
                    $"{row.ValidationState}.").IsTrue();
        }
    }

    [TestCase]
    public void OutOfRangeCampaignIndicesRouteToNothingRatherThanThrowing() {
        AssertString(StoryManager.GetLevelScenePath((CampaignLevel)(-1))).IsEqual("");
        AssertString(StoryManager.GetLevelScenePath((CampaignLevel)CampaignLevelCount)).IsEqual("");
    }

    /// <summary>
    /// Driven on a detached StoryManager: the autoload's campaign pointer is shared
    /// session state and only moves forward, so walking the real one to the finale
    /// would leak into every later test.
    /// </summary>
    [TestCase]
    public void SequentialAdvanceWalksTheWholeCampaignAndCapsAtFifteen() {
        AssertObject(StoryManager.Instance)
            .OverrideFailureMessage("StoryManager autoload is missing.").IsNotNull();

        var manager = new StoryManager();
        try {
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Tutorial);
            // Package 5 C1: the old form was `Exists(path) || path.Length > 0`, which
            // any non-empty string satisfied. Every level is authored now, so each
            // step of the walk must land on a scene that is really there.
            AssertThat(ResourceLoader.Exists(manager.GetCurrentLevelPath())).OverrideFailureMessage(
                $"Campaign start routes to '{manager.GetCurrentLevelPath()}', which does not exist.")
                .IsTrue();
            for (int expected = 1; expected <= 15; expected++) {
                manager.AdvanceToNextLevel();
                AssertThat((int)manager.CurrentLevel).IsEqual(expected);
                string path = manager.GetCurrentLevelPath();
                AssertThat(ResourceLoader.Exists(path)).OverrideFailureMessage(
                    $"Advancing to level {expected} routes to '{path}', which does not exist.")
                    .IsTrue();
            }
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Alexandria);

            // The cap is hard: advancing past the finale is a no-op, not a wrap.
            manager.AdvanceToNextLevel();
            manager.AdvanceToNextLevel();
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Alexandria);
        } finally {
            manager.Free();
        }

        // The detached walk must not have disturbed the live autoload.
        AssertObject(StoryManager.Instance).IsNotNull();
    }

    [TestCase]
    public void EveryCampaignLevelHasAUniqueLocalizedHubMissionName() {
        TranslationServer.SetLocale("en");
        var keys = new HashSet<string>();
        for (int index = 0; index < CampaignLevelCount; index++) {
            string key = HubWorldController.CampaignLevelNameKey((CampaignLevel)index);
            AssertThat(key.StartsWith("campaign_level_", StringComparison.Ordinal)).IsTrue();
            AssertThat(keys.Add(key))
                .OverrideFailureMessage($"Two campaign levels share the hub label key '{key}'.").IsTrue();
            AssertThat(TranslationServer.Translate(key).ToString() != key)
                .OverrideFailureMessage($"Hub label key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }
        AssertThat(keys.Count).IsEqual(CampaignLevelCount);
    }
}
