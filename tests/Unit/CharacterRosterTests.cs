using System.Collections.Generic;
using System.Linq;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A6b — the A9 roster mandate, pinned.
///
/// <para>design-godot.md §2: <i>"the roster will grow — narrative and spec text
/// must never hardcode roster size or enumerate the cast in load-bearing
/// ways."</i> Before this suite, five identical nine-element literal arrays and
/// a dozen <c>== 9</c> assertions stood between the content manifest and a
/// tenth character. These cases exist so that regressing to a literal cast list
/// is caught rather than merely discouraged.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CharacterRosterTests {

    /// <summary>
    /// The roster IS the manifest — same IDs, same order, no additions, no
    /// omissions. This is the property the whole de-hardcoding rests on.
    /// </summary>
    [TestCase]
    public void TheRosterIsExactlyTheManifestCharacterRows() {
        ContentManifest manifest = ContentManifest.LoadDefault();
        var expected = new List<string>();
        foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.Character)) {
            expected.Add(entry.ContentID);
        }

        AssertThat(expected.Count > 0).IsTrue();
        AssertThat(string.Join(",", CharacterRoster.IDs)).IsEqual(string.Join(",", expected));
        AssertThat(CharacterRoster.Count).IsEqual(expected.Count);

        // The lookup helpers agree with the list they are derived from.
        for (int index = 0; index < expected.Count; index++) {
            AssertThat(CharacterRoster.Contains(expected[index])).IsTrue();
            AssertThat(CharacterRoster.At(index)).IsEqual(expected[index]);
            AssertThat(CharacterRoster.IndexOf(expected[index])).IsEqual(index);
        }

        // Out-of-range and unknown IDs degrade rather than throwing: selection
        // grids index straight into the roster from a restored session pick.
        AssertThat(CharacterRoster.At(-1)).IsEqual("");
        AssertThat(CharacterRoster.At(expected.Count)).IsEqual("");
        AssertThat(CharacterRoster.IndexOf("hawking")).IsEqual(-1);
        AssertThat(CharacterRoster.Contains("hawking")).IsFalse();
        AssertThat(CharacterRoster.Contains("")).IsFalse();
        AssertThat(CharacterRoster.Contains(null)).IsFalse();
    }

    /// <summary>
    /// Every production consumer of the roster agrees with it. These five were
    /// the identical hardcoded arrays: the story character grid, the Fighter
    /// select tiles, the Holodeck CPU list, the persisted unlock set, and the
    /// ability-VFX resolution gate (reached here through
    /// <c>CharacterFactory.IsKnownCharacter</c>, which shares its source).
    /// </summary>
    [TestCase]
    public void EveryRosterConsumerAgreesWithTheCanonicalList() {
        var issues = new List<string>();

        // The save default: all characters available from the start. Granted
        // by SaveManager.EnsureRosterUnlocked, deliberately NOT by the field
        // initializer or by GlobalSaveData.Normalize — reading the manifest is
        // Godot file I/O, and both of those also run in the pure-C# test host,
        // where that call is an access violation that kills the host. Every
        // SaveManager path that hands out a global payload calls the grant.
        var global = new GlobalSaveData();
        SaveManager.EnsureRosterUnlocked(global);
        foreach (string id in CharacterRoster.IDs) {
            if (!global.UnlockedCharacters.Contains(id)) issues.Add($"save default missing {id}");
        }
        if (global.UnlockedCharacters.Count != CharacterRoster.Count) {
            issues.Add($"save default has {global.UnlockedCharacters.Count} entries, roster has {CharacterRoster.Count}");
        }

        // The same grant rescues a payload written before a roster row existed
        // — otherwise a new character would be permanently unavailable on every
        // save that already exists.
        var stale = new GlobalSaveData { UnlockedCharacters = new List<string> { CharacterRoster.At(0) } };
        SaveManager.EnsureRosterUnlocked(stale);
        foreach (string id in CharacterRoster.IDs) {
            if (!stale.UnlockedCharacters.Contains(id)) issues.Add($"the grant did not backfill {id}");
        }
        // And it is idempotent — no duplicates on a payload that already has them.
        SaveManager.EnsureRosterUnlocked(stale);
        if (stale.UnlockedCharacters.Count != CharacterRoster.Count) {
            issues.Add($"the grant is not idempotent: {stale.UnlockedCharacters.Count} entries");
        }

        // The factory gate and the captive roster read the same source.
        foreach (string id in CharacterRoster.IDs) {
            if (!FTT.Characters.CharacterFactory.IsKnownCharacter(id)) {
                issues.Add($"CharacterFactory does not know {id}");
            }
        }
        if (FTT.Characters.CharacterFactory.IsKnownCharacter("hawking")) {
            issues.Add("CharacterFactory accepted a non-roster ID");
        }
        AssertThat(string.Join(",", CampaignCaptiveRoster.Roster()))
            .IsEqual(string.Join(",", CharacterRoster.IDs));

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// The point of the exercise: a roster row that did not exist when the code
    /// was written still flows through. Simulated against the pure overloads —
    /// the real manifest is content, so growing it here would be authoring, not
    /// testing — plus a real check that every authored roster member resolves
    /// its own per-character resources, which is what an added row would need.
    /// </summary>
    [TestCase]
    public void AddingARosterRowFlowsThroughWithNoCodeChange() {
        var grown = new List<string>(CharacterRoster.IDs) { "hypatia" };

        // The captive pool — the one roster consumer with a pure overload —
        // widens by exactly one and keeps the "everyone but the hero" rule.
        IReadOnlyList<string> captives = CampaignCaptiveRoster.For("einstein", grown);
        AssertThat(captives.Count).IsEqual(grown.Count - 1);
        AssertThat(captives.Contains("hypatia")).IsTrue();
        AssertThat(captives.Contains("einstein")).IsFalse();

        // A hero the code has never heard of is still a valid hero: the filter
        // is set membership, not a switch.
        IReadOnlyList<string> newHeroCaptives = CampaignCaptiveRoster.For("hypatia", grown);
        AssertThat(newHeroCaptives.Count).IsEqual(grown.Count - 1);
        AssertThat(newHeroCaptives.Contains("hypatia")).IsFalse();

        // And every authored member resolves the per-character resources a new
        // row would have to bring with it — no code path enumerates the cast.
        var issues = new List<string>();
        foreach (string id in CharacterRoster.IDs) {
            string dataPath = $"res://resources/Characters/{id}_data.tres";
            if (!ResourceLoader.Exists(dataPath)) { issues.Add($"{id}: no {dataPath}"); continue; }
            var data = AuthoredResources.Load<FTT.Characters.CharacterData>(dataPath);
            if (data == null) { issues.Add($"{id}: data failed to load"); continue; }
            if (data.CharacterID != id) issues.Add($"{id}: resource says '{data.CharacterID}'");
            // A6b moved the placeholder identity colours onto the resource so a
            // new character brings its own; an unauthored one would fall back to
            // grey and be invisible as a bug.
            if (data.PlaceholderBodyColor.A <= 0f) issues.Add($"{id}: no authored PlaceholderBodyColor");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
