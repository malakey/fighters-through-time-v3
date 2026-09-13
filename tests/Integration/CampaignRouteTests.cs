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
/// <para>Package 11 A12 (V7.6) raised it from sixteen slots to <b>seventeen</b>.
/// Level 4A, the per-character Legacy Level, is appended to the enum at 16 so
/// nothing renumbers, and the campaign's play order moved out of "the enum value is
/// the array index" into an explicit route — 0,1,2,3,4,<b>16</b>,5…15. Its scene
/// path is the one route in the campaign that depends on the locked hero, so half of
/// what this suite now proves is that resolution happens by ID and by character,
/// never by position.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CampaignRouteTests {

    /// <summary>Shared campaign levels: the sixteen slots that are not Level 4A.</summary>
    private const int SharedCampaignLevelCount = 16;

    /// <summary>Shared levels plus Level 4A.</summary>
    private const int CampaignRouteLength = 17;

    private const string ExemplarHero = "einstein";

    /// <summary>
    /// The StoryLevel manifest rows: the sixteen shared slots plus <b>one row per
    /// authored Level 4A variant</b>. That is not <see cref="CampaignRouteLength"/> —
    /// the route has one Legacy slot, but the manifest carries a row for every hero
    /// whose variant exists. Equating the two held only while Einstein was the single
    /// authored variant (A12); it is derived from the manifest here so it stays true
    /// as B1, B2 and B3 land the other eight (Package 11 B3).
    /// </summary>
    private static int ExpectedStoryLevelRowCount(List<ContentManifestEntry> rows) {
        int legacyRows = rows.Count(entry =>
            entry.ContentID.StartsWith(StoryManager.LegacyLevelIDPrefix, StringComparison.Ordinal));
        AssertThat(legacyRows > 0)
            .OverrideFailureMessage("The manifest carries no Level 4A variant rows at all.").IsTrue();
        return SharedCampaignLevelCount + legacyRows;
    }

    // === Route shape ===

    [TestCase]
    public void TheCampaignRouteIsSeventeenSlotsWithLevelFourABetweenParisAndTheTitanic() {
        IReadOnlyList<CampaignLevel> route = StoryManager.CampaignRoute;
        AssertThat(route.Count).IsEqual(CampaignRouteLength);

        // The enum value is an identity, never a position: 4A is 16 and plays sixth.
        AssertThat((int)CampaignLevel.LegacyNexus).IsEqual(16);
        int legacyIndex = StoryManager.RouteIndexOf(CampaignLevel.LegacyNexus);
        AssertThat(legacyIndex).IsEqual(5);
        AssertThat(route[legacyIndex - 1]).IsEqual(CampaignLevel.Paris);
        AssertThat(route[legacyIndex + 1]).IsEqual(CampaignLevel.Titanic);

        // Nothing renumbered: every shared slot keeps the value it shipped with.
        AssertThat((int)CampaignLevel.Tutorial).IsEqual(0);
        AssertThat((int)CampaignLevel.Paris).IsEqual(4);
        AssertThat((int)CampaignLevel.Titanic).IsEqual(5);
        AssertThat((int)CampaignLevel.Alexandria).IsEqual(15);

        // Every slot appears exactly once, and the finale is last.
        AssertThat(route.Distinct().Count()).IsEqual(CampaignRouteLength);
        AssertThat(route[route.Count - 1]).IsEqual(CampaignLevel.Alexandria);
    }

    [TestCase]
    public void TheLegacyRouteResolvesPerHeroAndRefusesWhenNoCharacterIsLocked() {
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, ExemplarHero))
            .IsEqual("res://scenes/campaign/Level_04A_einstein.tscn");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, "Pocahontas"))
            .IsEqual("res://scenes/campaign/Level_04A_pocahontas.tscn");
        AssertString(StoryManager.LegacyLevelID("einstein")).IsEqual("level_04a_einstein");

        // No explicit hero, no session character and no save slot: the route is
        // refused rather than pointed at a scene that cannot exist. StoryManager
        // validates a path before changing scene, so "" is a clean refusal.
        using var session = new ScratchSession(character: "");
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus)).IsEqual("");

        // With a session character, the parameterless overload resolves it.
        session.SetCharacter(ExemplarHero);
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus))
            .IsEqual("res://scenes/campaign/Level_04A_einstein.tscn");
    }

    // === Manifest agreement ===

    [TestCase]
    public void EveryManifestStoryLevelRowMatchesTheStoryManagerScenePath() {
        using var session = new ScratchSession(ExemplarHero);
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(ExpectedStoryLevelRowCount(rows));

        for (int index = 0; index < SharedCampaignLevelCount; index++) {
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

        // The Legacy rows are keyed by hero, not by slot number. B1-B3 add the other
        // eight; every one present must agree with the per-hero resolver.
        List<ContentManifestEntry> legacyRows = rows
            .Where(row => row.ContentID.StartsWith(StoryManager.LegacyLevelIDPrefix, StringComparison.Ordinal))
            .ToList();
        AssertThat(legacyRows.Count >= 1).OverrideFailureMessage(
            "No Level 4A StoryLevel row in the manifest.").IsTrue();
        foreach (ContentManifestEntry row in legacyRows) {
            string hero = row.ContentID.Substring(StoryManager.LegacyLevelIDPrefix.Length);
            string routed = StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, hero);
            AssertThat(row.ResourcePath == routed).OverrideFailureMessage(
                $"Manifest row '{row.ContentID}' points at '{row.ResourcePath}' but the Legacy " +
                $"resolver routes hero '{hero}' to '{routed}'.").IsTrue();
        }
    }

    [TestCase]
    public void EveryCampaignRouteSlotResolvesToADistinctAuthoredScenePath() {
        using var session = new ScratchSession(ExemplarHero);
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
    /// the whole campaign; A12 extends it to all seventeen route slots for the
    /// exemplar hero. A route pointing at a scene that is missing, unloadable, or not
    /// a campaign level is a hard failure, never a silently skipped index.
    /// </summary>
    [TestCase]
    public void EveryRoutedSceneExistsLoadsAndInstantiates() {
        using var session = new ScratchSession(ExemplarHero);
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
            "Every campaign route slot is authored for the exemplar hero; these resolve to " +
            "nothing: " + string.Join(", ", missing)).IsTrue();
    }

    /// <summary>
    /// Package 5 C1: the manifest's own StoryLevel paths must all be on disk too.
    /// The route test above walks StoryManager; this walks the manifest, so a row
    /// edited to point somewhere plausible-but-absent cannot hide behind the
    /// path-agreement assertion alone.
    ///
    /// <para>The state assertion is "not <c>Planned</c>, and <c>Valid</c>", not
    /// "<c>Implemented</c>". Levels 2-15 and 4A are <c>Implemented</c>; the Tutorial
    /// and Florence are deliberately still <c>Prototype</c> — they predate Package 5,
    /// were built before the <c>StoryLevelControllerBase</c> convention, and were not
    /// retrofitted to it. What matters here is that no campaign level claims to be
    /// unbuilt when its scene is on disk.</para>
    /// </summary>
    [TestCase]
    public void EveryManifestStoryLevelRowPointsAtAnAuthoredScene() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        List<ContentManifestEntry> rows = manifest.ForCategory(ContentCategory.StoryLevel).ToList();
        AssertThat(rows.Count).IsEqual(ExpectedStoryLevelRowCount(rows));

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
    public void SequentialAdvanceWalksTheWholeSeventeenSlotRouteAndCapsAtAlexandria() {
        AssertObject(StoryManager.Instance)
            .OverrideFailureMessage("StoryManager autoload is missing.").IsNotNull();

        using var session = new ScratchSession(ExemplarHero);
        var manager = new StoryManager();
        try {
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Tutorial);
            // Package 5 C1: the old form was `Exists(path) || path.Length > 0`, which
            // any non-empty string satisfied. Every slot is authored now, so each
            // step of the walk must land on a scene that is really there.
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
    /// The A12 acceptance criterion in one case: a campaign advancing from Level 4
    /// lands on the active hero's 4A, and advancing from 4A lands on Level 5.
    /// </summary>
    [TestCase]
    public void AdvancingFromParisLandsOnTheHeroLegacyLevelAndThenOnTheTitanic() {
        using var session = new ScratchSession(ExemplarHero);
        var manager = new StoryManager();
        try {
            for (int step = 0; step < 4; step++) manager.AdvanceToNextLevel();
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.Paris);

            manager.AdvanceToNextLevel();
            AssertThat(manager.CurrentLevel).IsEqual(CampaignLevel.LegacyNexus);
            AssertString(manager.GetCurrentLevelPath())
                .IsEqual("res://scenes/campaign/Level_04A_einstein.tscn");

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

    // === Helpers ===

    /// <summary>
    /// Borrows the session's locked character (the Legacy route reads it) and puts it
    /// back. The session is shared autoload state; a test that leaves a character
    /// behind changes every later 4A resolution.
    /// </summary>
    private sealed class ScratchSession : IDisposable {
        private readonly string _originalCharacter;
        private readonly int _originalSlot;

        public ScratchSession(string character) {
            SessionData session = GameManager.Instance.CurrentSession;
            _originalCharacter = session.SelectedCharacterID;
            _originalSlot = session.ActiveSaveSlot;
            session.SelectedCharacterID = character;
            // No slot: the save fallback inside the resolver must not find one.
            session.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession = session;
        }

        public void SetCharacter(string character) {
            SessionData session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = character;
            GameManager.Instance.CurrentSession = session;
        }

        public void Dispose() {
            SessionData session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _originalCharacter;
            session.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession = session;
        }
    }
}
