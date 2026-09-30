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
/// table, and the on-disk scenes must agree for every campaign slot. This suite
/// exists because level 10 shipped as `Level_10_London.tscn` in StoryManager while
/// the manifest and its pool config both said `level_10_globe` — a silent route
/// break that nothing caught until Package 5 planning.
///
/// <para>Package 11 A12 raised the route to seventeen slots for the per-character
/// Level 4A (enum value 16, played between Levels 4 and 5). <b>Package 13 W2 (S27)
/// retired Level 4A</b>: the route is sixteen slots again, 0–15 in order, and
/// <c>CampaignLevel.LegacyNexus = 16</c> survives only as an <c>[Obsolete]</c>
/// reserved identity that resolves to nothing.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignRouteTests {

    /// <summary>The campaign: Levels 0–15.</summary>
    private const int CampaignRouteLength = 16;

    /// <summary>The retired Level 4A ordinal, spelled as a number so no case needs the obsolete member.</summary>
    private const int RetiredLegacyOrdinal = 16;

    // === Route shape ===

    [TestCase]
    public void TheCampaignRouteIsSixteenSlotsInEnumOrder() {
        IReadOnlyList<CampaignLevel> route = StoryManager.CampaignRoute;
        AssertThat(route.Count).IsEqual(CampaignRouteLength);

        // Nothing renumbered: every slot keeps the value it shipped with, and the
        // route position IS the enum value again now that 4A is gone.
        for (int index = 0; index < CampaignRouteLength; index++) {
            AssertThat((int)route[index]).OverrideFailureMessage(
                $"Route slot {index} is {route[index]}.").IsEqual(index);
        }
        AssertThat((int)CampaignLevel.Paris).IsEqual(4);
        AssertThat((int)CampaignLevel.Titanic).IsEqual(5);
        AssertThat((int)CampaignLevel.Alexandria).IsEqual(15);
        AssertThat(route.Distinct().Count()).IsEqual(CampaignRouteLength);
        AssertThat(route[route.Count - 1]).IsEqual(CampaignLevel.Alexandria);
    }

    [TestCase]
    public void TheRetiredLegacyOrdinalIsReservedAndRoutesToNothing() {
        var retired = (CampaignLevel)RetiredLegacyOrdinal;
        // The member still exists (saves cast level indices) ...
        AssertThat(Enum.IsDefined(typeof(CampaignLevel), retired)).IsTrue();
        // ... but it is off the route, has no scene and no level ID, whoever the
        // session's hero is, and none of the nine deleted variant scenes remains.
        AssertThat(StoryManager.RouteIndexOf(retired)).IsEqual(-1);
        AssertString(StoryManager.GetLevelScenePath(retired)).IsEqual("");
        AssertString(StoryManager.GetLevelID(retired)).IsEqual("");
        AssertThat(StoryManager.CampaignRoute.Contains(retired)).IsFalse();
        AssertThat(ResourceLoader.Exists("res://scenes/campaign/Level_04A_einstein.tscn")).IsFalse();
    }

    // === Manifest agreement ===

    [TestCase]
    public void EveryManifestStoryLevelRowMatchesTheStoryManagerScenePath() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(CampaignRouteLength);

        for (int index = 0; index < CampaignRouteLength; index++) {
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
            AssertString(row.ContentID).IsEqual(StoryManager.GetLevelID((CampaignLevel)index));
        }

        // S27: no Level 4A row survives in the manifest.
        AssertThat(rows.Any(row => row.ContentID.StartsWith("level_04a_", StringComparison.Ordinal)))
            .IsFalse();
    }

    [TestCase]
    public void EveryCampaignRouteSlotResolvesToADistinctAuthoredScenePath() {
        var seen = new HashSet<string>();
        foreach (CampaignLevel level in StoryManager.CampaignRoute) {
            string path = StoryManager.GetLevelScenePath(level);
            AssertThat(path.StartsWith("res://scenes/campaign/", StringComparison.Ordinal)).IsTrue();
            AssertThat(path.EndsWith(".tscn", StringComparison.Ordinal)).IsTrue();
            AssertThat(seen.Add(path))
                .OverrideFailureMessage($"Duplicate campaign route: {path}").IsTrue();
        }
        AssertThat(seen.Count).IsEqual(CampaignRouteLength);
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
    /// the whole campaign. A route pointing at a scene that is missing, unloadable,
    /// or not a campaign level is a hard failure, never a silently skipped index.
    /// </summary>
    [TestCase]
    public void EveryRoutedSceneExistsLoadsAndInstantiates() {
        var missing = new List<string>();
        foreach (CampaignLevel level in StoryManager.CampaignRoute) {
            string path = StoryManager.GetLevelScenePath(level);
            if (!ResourceLoader.Exists(path)) { missing.Add($"{level} -> {path}"); continue; }

            PackedScene scene = ResourceLoader.Load<PackedScene>(path);
            AssertObject(scene).OverrideFailureMessage($"{path} exists but did not load.").IsNotNull();
            Node instance = scene.Instantiate();
            AssertObject(instance)
                .OverrideFailureMessage($"{path} loaded but would not instantiate.").IsNotNull();
            instance.Free();
        }

        AssertThat(missing.Count == 0).OverrideFailureMessage(
            "Every campaign route slot is authored; these resolve to nothing: "
            + string.Join(", ", missing)).IsTrue();
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
    /// were built before the <c>StoryLevelControllerBase</c> convention, and were not
    /// retrofitted to it.</para>
    /// </summary>
    [TestCase]
    public void EveryManifestStoryLevelRowPointsAtAnAuthoredScene() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(CampaignRouteLength);

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
    public void OffRouteCampaignIndicesRouteToNothingRatherThanThrowing() {
        AssertString(StoryManager.GetLevelScenePath((CampaignLevel)(-1))).IsEqual("");
        AssertString(StoryManager.GetLevelScenePath((CampaignLevel)17)).IsEqual("");
        AssertThat(StoryManager.RouteIndexOf((CampaignLevel)17)).IsEqual(-1);
    }

    /// <summary>
    /// Driven on a detached StoryManager: the autoload's campaign pointer is shared
    /// session state and only moves forward, so walking the real one to the finale
    /// would leak into every later test.
    /// </summary>
    [TestCase]
    public void SequentialAdvanceWalksTheWholeSixteenSlotRouteAndCapsAtAlexandria() {
        AssertObject(StoryManager.Instance)
            .OverrideFailureMessage("StoryManager autoload is missing.").IsNotNull();

        var manager = new StoryManager();
        try {
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Tutorial);
            AssertThat(ResourceLoader.Exists(manager.GetCurrentLevelPath())).OverrideFailureMessage(
                $"Campaign start routes to '{manager.GetCurrentLevelPath()}', which does not exist.")
                .IsTrue();
            for (int step = 1; step < CampaignRouteLength; step++) {
                manager.AdvanceToNextLevel();
                AssertThat(manager.CurrentLevel).OverrideFailureMessage(
                    $"Step {step} of the route landed on {manager.CurrentLevel}.")
                    .IsEqual(StoryManager.CampaignRoute[step]);
                string path = manager.GetCurrentLevelPath();
                AssertThat(ResourceLoader.Exists(path)).OverrideFailureMessage(
                    $"Advancing to route step {step} routes to '{path}', which does not exist.")
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

    /// <summary>
    /// S27 in one case: a campaign advancing from Level 4 lands straight on the
    /// Titanic, which is now the first full-kit level.
    /// </summary>
    [TestCase]
    public void AdvancingFromParisLandsOnTheTitanic() {
        var manager = new StoryManager();
        try {
            for (int step = 0; step < 4; step++) manager.AdvanceToNextLevel();
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Paris);

            manager.AdvanceToNextLevel();
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Titanic);
            AssertString(manager.GetCurrentLevelPath())
                .IsEqual("res://scenes/campaign/Level_05_Titanic.tscn");
        } finally {
            manager.Free();
        }
    }

    [TestCase]
    public void EveryCampaignRouteSlotHasAUniqueLocalizedHubMissionName() {
        TranslationServer.SetLocale("en");
        var keys = new HashSet<string>();
        foreach (CampaignLevel level in StoryManager.CampaignRoute) {
            string key = HubWorldController.CampaignLevelNameKey(level);
            AssertThat(key.StartsWith("campaign_level_", StringComparison.Ordinal)).IsTrue();
            AssertThat(keys.Add(key))
                .OverrideFailureMessage($"Two campaign levels share the hub label key '{key}'.").IsTrue();
            AssertThat(TranslationServer.Translate(key).ToString() != key)
                .OverrideFailureMessage($"Hub label key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }
        AssertThat(keys.Count).IsEqual(CampaignRouteLength);
    }
}
