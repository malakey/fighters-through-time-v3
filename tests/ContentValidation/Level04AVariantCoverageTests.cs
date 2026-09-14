using System;
using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 11 B3, cross-cutting — <b>every roster hero has a Level 4A</b>.
///
/// <para>Level 4A is the one campaign slot whose scene depends on the locked
/// character (§2.4). A hero with no variant is not a missing nice-to-have: the
/// campaign route hits <see cref="CampaignLevel.LegacyNexus"/> for every playthrough,
/// and <c>StoryManager.GetLevelScenePath</c> would resolve that hero to a path with
/// nothing behind it. Nine per-hero content suites cannot catch that, because a hero
/// nobody authored has no suite to fail.</para>
///
/// <para>The roster is read from <c>resources/Content/content_manifest.csv</c>'s
/// <see cref="ContentCategory.Character"/> rows rather than from a literal list here,
/// so adding a tenth character makes this suite demand a tenth Legacy Level on its
/// own. Every assertion names the hero it is missing.</para>
///
/// <para><b>Expected red on the B3 branch alone.</b> B3 authors Mozart and
/// Pocahontas; B1 (joan, leonardo, lincoln) and B2 (cleopatra, tesla, shakespeare)
/// author theirs in parallel worktrees. Until all three B branches merge, these cases
/// fail naming the six heroes that branch has not seen — which is exactly the report
/// they exist to produce.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04AVariantCoverageTests {

    /// <summary>The roster, from the manifest. Never a literal list.</summary>
    private static IReadOnlyList<string> RosterHeroes() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        AssertObject(manifest).IsNotNull();
        var heroes = manifest.ForCategory(ContentCategory.Character)
            .Select(entry => entry.ContentID)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        AssertThat(heroes.Count > 0)
            .OverrideFailureMessage("The manifest lists no Character rows; the roster sweep would pass vacuously.")
            .IsTrue();
        return heroes;
    }

    private static string ControllerTypeName(string hero) =>
        $"FTT.Environment.Level04A{char.ToUpperInvariant(hero[0])}{hero.Substring(1)}Controller";

    [TestCase]
    public void EveryRosterHeroHasAnAuthoredLevel4ASceneThatInstantiatesAsALegacyController() {
        var missing = new List<string>();
        foreach (string hero in RosterHeroes()) {
            string scenePath = $"res://scenes/campaign/Level_04A_{hero}.tscn";

            // The resolver and the authored path must be the same string, or a
            // playthrough locked to this hero routes somewhere that does not exist.
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.LegacyNexus, hero))
                .OverrideFailureMessage(
                    $"Hero '{hero}': the route resolver does not point at {scenePath}.")
                .IsEqual(scenePath);

            if (!ResourceLoader.Exists(scenePath)) { missing.Add($"{hero} (no scene)"); continue; }

            Type controller = typeof(LegacyLevelControllerBase).Assembly.GetType(ControllerTypeName(hero));
            if (controller == null || !typeof(LegacyLevelControllerBase).IsAssignableFrom(controller)) {
                missing.Add($"{hero} (no {ControllerTypeName(hero)} deriving LegacyLevelControllerBase)");
                continue;
            }

            var packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed == null) { missing.Add($"{hero} (scene will not load)"); continue; }
            Node instance = packed.Instantiate();
            try {
                if (!controller.IsInstanceOfType(instance)) {
                    missing.Add($"{hero} (scene script is not {controller.Name})");
                }
            } finally {
                instance.Free();
            }
        }
        AssertNoneMissing("Level 4A scene/controller", missing);
    }

    [TestCase]
    public void EveryRosterHeroHasADialogueSetAPoolConfigAndALegacyBossResource() {
        var missing = new List<string>();
        foreach (string hero in RosterHeroes()) {
            string levelID = $"level_04a_{hero}";
            string dialogue = $"res://resources/Dialogue/{levelID}_dialogue.tres";
            string poolConfig = $"res://resources/Pools/level_pool_configs/{levelID}_pool_config.tres";
            // Legacy bosses live in the legacy/ subdirectory so the non-recursive
            // fifteen-boss roster sweeps keep their counts.
            string boss = $"res://resources/Bosses/legacy/{hero}_legacy_boss.tres";

            if (!ResourceLoader.Exists(dialogue)) missing.Add($"{hero} (no dialogue set at {dialogue})");
            if (!ResourceLoader.Exists(poolConfig)) missing.Add($"{hero} (no pool config at {poolConfig})");
            if (!ResourceLoader.Exists(boss)) { missing.Add($"{hero} (no legacy boss at {boss})"); continue; }

            var data = AuthoredResources.Load<BossData>(boss);
            if (data == null) { missing.Add($"{hero} (legacy boss will not load)"); continue; }
            if (data.ChronalDustDrop != LegacyLevelControllerBase.BossDust) {
                missing.Add($"{hero} (legacy boss pays {data.ChronalDustDrop} dust, not " +
                    $"{LegacyLevelControllerBase.BossDust})");
            }
            if (data.PhaseThresholds == null || data.PhaseThresholds.Length != 1) {
                missing.Add($"{hero} (legacy boss is not two phases)");
            }
        }
        AssertNoneMissing("Level 4A dialogue/pool/boss resources", missing);
    }

    [TestCase]
    public void EveryRosterHeroHasItsStoryLevelAndDialogueSetManifestRows() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var levelIDs = new HashSet<string>(
            manifest.ForCategory(ContentCategory.StoryLevel).Select(entry => entry.ContentID),
            StringComparer.Ordinal);
        var dialogueIDs = new HashSet<string>(
            manifest.ForCategory(ContentCategory.DialogueSet).Select(entry => entry.ContentID),
            StringComparer.Ordinal);

        var missing = new List<string>();
        foreach (string hero in RosterHeroes()) {
            if (!levelIDs.Contains($"level_04a_{hero}")) missing.Add($"{hero} (no StoryLevel row)");
            if (!dialogueIDs.Contains($"dialogue_level_04a_{hero}")) missing.Add($"{hero} (no DialogueSet row)");
        }
        AssertNoneMissing("Level 4A manifest rows", missing);
    }

    [TestCase]
    public void EveryRosterHerosLevel4ASceneRoutesToItsOwnPoolBudget() {
        // A mis-pointed catalog row silently warms another variant's budget, and a
        // 4A scene with no row at all warms nothing — which strands the Eraser
        // ambush behind an empty elite_enemy pool.
        ScenePoolCatalog catalog = ScenePoolCatalog.LoadDefault();
        AssertObject(catalog).IsNotNull();

        var missing = new List<string>();
        foreach (string hero in RosterHeroes()) {
            string scenePath = $"res://scenes/campaign/Level_04A_{hero}.tscn";
            ScenePoolConfig config = catalog.Find(scenePath);
            if (config == null) { missing.Add($"{hero} (no scene_pool_catalog row)"); continue; }
            if (config.ConfigID != $"level_04a_{hero}_pools") {
                missing.Add($"{hero} (catalog row points at '{config.ConfigID}')");
            }
        }
        AssertNoneMissing("Level 4A pool-catalog routing", missing);
    }

    /// <summary>
    /// One failure line listing every hero that is short, rather than nine suites
    /// that cannot exist yet. Uses the repository idiom instead of an empty
    /// OverrideFailureMessage, which throws on the passing path.
    /// </summary>
    private static void AssertNoneMissing(string what, IReadOnlyList<string> missing) {
        if (missing.Count == 0) return;
        AssertString($"{what} missing for: {string.Join(" | ", missing)}").IsEqual("");
    }
}
