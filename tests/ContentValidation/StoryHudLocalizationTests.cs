using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 B1. Every string the Story HUD, the results overlay and the Timeline
/// Collapse panel put on screen resolves through the <b>compiled</b>
/// <c>localization/en.en.translation</c>, not merely through <c>en.csv</c>.
///
/// That distinction is the whole point: <c>Node.Tr()</c> reads the compiled
/// resource, and <c>--headless --quit</c> does not regenerate it. An agent who
/// adds a key, runs the import <i>check</i>, and commits ships a HUD that renders
/// raw keys at runtime while the CSV reviews perfectly. Package 8 §2.8 leaves the
/// compiled artifact to the orchestrator, so these cases go green once the local
/// <c>--headless --import</c> has run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudLocalizationTests {

    /// <summary>Every key B1 introduced or newly renders.</summary>
    private static readonly string[] StorySurfaceKeys = {
        // Ability indicator row and the reused status family.
        "hud_slot_special1", "hud_slot_special2", "hud_slot_movement", "hud_slot_ultimate",
        "status_none", "status_timedilation", "status_venom",
        "status_staticcharge", "status_radiantburn", "status_root",
        // Existing HUD copy this surface renders.
        "hud_hp", "hud_rewinds", "hud_checkpoint_reached", "hub_carried_dust",
        // Results overlay, including the two new stat lines.
        "results_level_complete", "results_dust_earned", "results_return_hub",
        "results_completion_time", "results_rewinds_used",
        // Timeline Collapse panel, including the new confirmation prompt.
        "timeline_collapse_title", "timeline_restart_anchor", "timeline_restart_level",
        "timeline_restart_level_confirm", "common_back", "common_cancel"
    };

    /// <summary>Keys whose English value must carry a format placeholder.</summary>
    private static readonly string[] FormattedKeys = {
        "hud_rewinds", "hub_carried_dust", "results_dust_earned",
        "results_completion_time", "results_rewinds_used"
    };

    [TestCase]
    public void EveryStorySurfaceKeyResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        var unresolved = new List<string>();
        foreach (string key in StorySurfaceKeys) {
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }
        if (unresolved.Count > 0) {
            AssertThat(
                "Story HUD keys that do not resolve through the compiled " +
                "localization/en.en.translation (run `--headless --import`): " +
                string.Join(", ", unresolved))
                .IsEqual("");
        }
    }

    [TestCase]
    public void FormattedKeysStillCarryTheirPlaceholder() {
        TranslationServer.SetLocale("en");
        var broken = new List<string>();
        foreach (string key in FormattedKeys) {
            // string.Format on a placeholder-less string silently drops the number,
            // which reads as "Rewinds:" with no count rather than as a bug.
            if (!TranslationServer.Translate(key).ToString().Contains("{0}")) broken.Add(key);
        }
        if (broken.Count > 0) {
            AssertThat("Keys missing their {0} placeholder: " + string.Join(", ", broken)).IsEqual("");
        }
    }

    [TestCase]
    public void EveryIndicatorSlotAndStatusNamesAKeyThatExistsInTheCsv() {
        // The model builds status keys by lowercasing the enum name, so a new
        // StatusType would silently render a raw key without this sweep.
        HashSet<string> csvKeys = ReadCsvKeys();
        var missing = new List<string>();

        foreach (AbilitySlot slot in System.Enum.GetValues<AbilitySlot>()) {
            string key = HudAbilityIndicatorModel.SlotLabelKey(slot);
            if (!csvKeys.Contains(key)) missing.Add(key);
        }
        foreach (StatusType status in System.Enum.GetValues<StatusType>()) {
            string key = HudAbilityIndicatorModel.StatusLabelKey(status);
            if (!csvKeys.Contains(key)) missing.Add(key);
        }

        if (missing.Count > 0) {
            AssertThat("Keys absent from localization/en.csv: " + string.Join(", ", missing)).IsEqual("");
        }
    }

    [TestCase]
    public void TheB1MarkerIsACommaLessLineSoTheImporterSkipsIt() {
        // Godot's CSV translation importer and every test-side parser here skip a
        // line with no delimiter, which is what keeps the workstream marker from
        // becoming a translation key.
        string[] lines = File.ReadAllLines(
            ProjectSettings.GlobalizePath("res://localization/en.csv"));
        var marker = "";
        foreach (string line in lines) {
            if (line.StartsWith("# Package 8 B1")) marker = line;
        }
        AssertString(marker).IsEqual("# Package 8 B1");
        AssertThat(marker.Contains(",")).IsFalse();
    }

    private static HashSet<string> ReadCsvKeys() {
        var keys = new HashSet<string>();
        string path = ProjectSettings.GlobalizePath("res://localization/en.csv");
        foreach (string line in File.ReadAllLines(path)) {
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            keys.Add(line[..comma]);
        }
        return keys;
    }
}
