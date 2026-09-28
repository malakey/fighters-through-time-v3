using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 8 C1: the reverse localization sweep — keys that exist and nothing uses.
///
/// <para><b>Why this suite exists.</b> Every other localization gate in the repository
/// runs in one direction: it takes something the game references and proves the key
/// resolves. None of them can see the opposite failure, which is what a table
/// accumulates over eight packages — a key whose only consumer was deleted, a key
/// authored for a screen that was then designed differently, a key renamed in code and
/// left behind in the CSV. Those are invisible in review (the CSV row looks fine) and
/// they cost real money once the table goes to a translator, who is paid per string and
/// cannot tell a live key from a dead one.</para>
///
/// <para><b>The rule is asymmetric, deliberately.</b> A key on
/// <see cref="RecordedOrphans"/> may disappear at any time — retiring an orphan is
/// always an improvement and must never fail a build. A key that goes dead and is
/// <i>not</i> on the list fails immediately, naming itself. So the list can only ever
/// shrink without a deliberate edit, which is the property a threshold alone does not
/// give you: a count-only cap passes happily while one orphan is fixed and a different
/// one is created in the same change.</para>
///
/// <para><b>What "used" means here.</b> A literal appearance of the key anywhere under
/// <c>scripts/</c>, <c>scenes/</c> or <c>resources/</c>. That is deliberately generous —
/// the goal is to find keys nothing could possibly reach, not to audit call graphs. Keys
/// assembled at runtime cannot be found that way at all, so
/// <see cref="DynamicKeyPrefixes"/> exempts the families that are genuinely built from
/// an enum name in code, and the second test case pins each exemption to the code that
/// justifies it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UnusedTranslationKeyTests {
    private static readonly string[] SearchRoots = { "scripts", "scenes", "resources" };
    private static readonly string[] SearchExtensions = { ".cs", ".tscn", ".tres", ".csv", ".gd", ".gdshader" };

    /// <summary>
    /// The content manifest is a CSV of content ids, not of translation keys; letting it
    /// into the corpus would make any key that happens to share a substring with a
    /// content id look referenced.
    /// </summary>
    private const string ExcludedFromCorpus = "resources/Content/content_manifest.csv";

    /// <summary>
    /// Key families assembled at runtime from an enum name, so no literal exists to find.
    /// Every prefix here is pinned to its builder by
    /// <see cref="EveryDynamicPrefixIsStillBuiltInCode"/>.
    /// </summary>
    private static readonly (string Prefix, string BuiltBy, string Marker)[] DynamicKeyPrefixes = {
        ("status_", "scripts/UI/HudAbilityIndicatorModel.cs", "\"status_\" + type.ToString().ToLowerInvariant()"),
        ("status_", "scripts/UI/FighterHudModel.cs", "status_{status.ToString().ToLowerInvariant()}"),
        // Package 11 Phase C. A6 authored the captive spoken names and recorded
        // all three as orphans "until the resolver merges"; A5's resolver has
        // merged, and it builds the key from the manifest roster ID, so the
        // family belongs here rather than on the orphan roster.
        ("captive_name_", "scripts/Core/CampaignCaptiveRoster.cs", "$\"captive_name_{characterID}\""),
        // Package 12 W7 (M35): the hero name table, built from the saved hero ID
        // (or the fallback row) by the render-time token pass.
        ("hero_name_", "scripts/Core/DialogueTokens.cs", "$\"hero_name_{heroID}\""),
        ("hero_address_", "scripts/Core/DialogueTokens.cs", "$\"hero_address_{heroID}\""),
        ("hero_possessive_", "scripts/Core/DialogueTokens.cs", "$\"hero_possessive_{heroID}\""),
        ("hero_home_era_", "scripts/Core/DialogueTokens.cs", "$\"hero_home_era_{heroID}\"")
    };

    /// <summary>
    /// Orphans as of the Package 8 closeout (2026-08-08), all of them pre-dating this
    /// package. Each names a surface that was specified and never built, or a HUD/menu
    /// affordance whose implementation went a different way; none has a consumer anywhere
    /// in the repository. They are recorded rather than deleted because deciding whether
    /// a boss HP readout or an in-level save affordance is coming back is content scope,
    /// not closeout scope — but they are now visible and capped.
    ///
    /// <para>Two Package-8-caused orphans were deleted instead of recorded:
    /// <c>settings_difficulty</c> (A4 removed the dead global difficulty dropdown;
    /// campaign difficulty is per-slot and locked at creation, so the key can have no
    /// future consumer) and <c>settings_fullscreen</c> (A4 replaced the boolean fullscreen
    /// toggle with the three-value <c>settings_window_*</c> family).</para>
    /// </summary>
    private static readonly HashSet<string> RecordedOrphans = new(StringComparer.Ordinal) {
        // menu_save and menu_restart left this roster with the audit M-2 pause
        // pass (2026-08-10): the Story pause menu now uses both keys.
        "menu_exit",
        "hud_ultimate",
        "hud_block_charges",
        "match_player_wins",
        "save_new_game",
        "save_load_game",
        "tutorial_objective_wave_one",
        "tutorial_controls",
        "boss_hp",
        "boss_defeated",
        // V7.3 LAN de-scope (2026-08-26): the main menu's code-built LanButton
        // was removed; the key stays for the Package 7 netcode entry point.
        "menu_lan_match",
        // Package 11 A6 (V7.5 narrative). Cleopatra is a captive from the first
        // strike onward, so the Level 8 post-boss scene no longer has her present
        // as an NPC; in the N03 hero-is-Cleopatra branch she is the player and
        // renders through speaker_player. The key is retained per the plan's
        // legacy-identifier rule rather than deleted.
        "speaker_cleopatra",
        // Package 12 W5 (H05 per D4(a), M25): the Local Versus select lost its
        // CPU toggle and CPU-pick flow, and the Fighter HUD's Ultimate cooldown
        // plate became the Echo Step radial. The four keys are recorded rather
        // than deleted because en.csv is append-only for workstreams; Phase C
        // retires them.
        "fighter_local_human",
        "fighter_cpu_selection",
        "fighter_pick_cpu_prompt",
        "fighter_hud_cooldown_ultimate"
        // The three captive_name_* rows A6 recorded here left the roster at the
        // Package 11 Phase C closeout: A5's CampaignCaptiveRoster shipped, so
        // they are a runtime-built family and moved to DynamicKeyPrefixes.
    };

    /// <summary>The cap stated in the Package 8 closeout, lowered as orphans are
    /// retired (12 → 10 with the audit M-2 pause pass; 10 → 11 with the V7.3 LAN
    /// de-scope, which orphaned <c>menu_lan_match</c> until Package 7; 11 → 15 with
    /// Package 11 A6’s V7.5 narrative pass, which orphaned <c>speaker_cleopatra</c>
    /// and added the three runtime-resolved <c>captive_name_*</c> rows; 15 → 12 at
    /// the Package 11 Phase C closeout, which moved those three onto
    /// <see cref="DynamicKeyPrefixes"/> now that their resolver has shipped; 12 → 16
    /// with Package 12 W5's Versus CPU split and M25 cooldown rework).
    /// Informational
    /// alongside the roster rule above: if both ever disagree, the roster is the
    /// authority.</summary>
    private const int RecordedOrphanCeiling = 16;

    [TestCase]
    public void NoTranslationKeyGoesUnusedBeyondTheRecordedOrphans() {
        string[] keys = EnglishTranslationKeys();
        AssertThat(keys.Length).OverrideFailureMessage(
            $"Only {keys.Length} keys parsed out of localization/en.csv; the parser is broken.")
            .IsGreaterEqual(900);

        string corpus = ReadCorpus(out int fileCount);
        AssertThat(fileCount).OverrideFailureMessage(
            $"Only {fileCount} content files were read; the corpus walk is broken, which would " +
            "report the entire translation table as unused.")
            .IsGreaterEqual(400);

        string[] unused = keys
            .Where(key => !DynamicKeyPrefixes.Any(family => key.StartsWith(family.Prefix, StringComparison.Ordinal)))
            .Where(key => !corpus.Contains(key, StringComparison.Ordinal))
            .ToArray();

        string[] undocumented = unused.Where(key => !RecordedOrphans.Contains(key)).ToArray();

        AssertThat(undocumented.Length).OverrideFailureMessage(
            $"{undocumented.Length} translation key(s) in localization/en.csv are referenced " +
            "nowhere under scripts/, scenes/ or resources/. Either wire them up, delete them, " +
            "or - if the key is built at runtime - add its family to " +
            "UnusedTranslationKeyTests.DynamicKeyPrefixes. Unused: " +
            string.Join(", ", undocumented) +
            $"\nFull unused report ({unused.Length} keys): " + string.Join(", ", unused))
            .IsEqual(0);

        AssertThat(unused.Length).OverrideFailureMessage(
            $"The unused-key count is {unused.Length}, above the recorded ceiling of " +
            $"{RecordedOrphanCeiling}. Unused: " + string.Join(", ", unused))
            .IsLessEqual(RecordedOrphanCeiling);
    }

    /// <summary>
    /// An exemption whose builder was deleted or renamed is a blanket suppression over a
    /// whole key prefix. This pins each one to the line that justifies it.
    /// </summary>
    [TestCase]
    public void EveryDynamicPrefixIsStillBuiltInCode() {
        var broken = new List<string>();
        foreach ((string prefix, string builtBy, string marker) in DynamicKeyPrefixes) {
            if (!File.Exists(builtBy)) { broken.Add($"{prefix}: {builtBy} does not exist"); continue; }
            if (!File.ReadAllText(builtBy).Contains(marker, StringComparison.Ordinal)) {
                broken.Add($"{prefix}: {builtBy} no longer contains `{marker}`");
            }
        }

        AssertThat(broken.Count).OverrideFailureMessage(
            "UnusedTranslationKeyTests.DynamicKeyPrefixes exempts key families that are no " +
            "longer built where it claims: " + string.Join("; ", broken))
            .IsEqual(0);
    }

    /// <summary>
    /// A recorded orphan that has since been wired up should leave the list; this reports
    /// it without failing, because retiring an orphan must never break a build. The
    /// assertion is only that the roster has not been padded with keys that are not even
    /// in the table.
    /// </summary>
    [TestCase]
    public void EveryRecordedOrphanIsStillAKeyInTheTable() {
        var present = new HashSet<string>(EnglishTranslationKeys(), StringComparer.Ordinal);
        string[] ghosts = RecordedOrphans.Where(key => !present.Contains(key)).ToArray();

        AssertThat(ghosts.Length).OverrideFailureMessage(
            "UnusedTranslationKeyTests.RecordedOrphans names keys that are not in " +
            "localization/en.csv at all; remove them from the roster: " + string.Join(", ", ghosts))
            .IsEqual(0);
    }

    private static string[] EnglishTranslationKeys() {
        var keys = new List<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.StartsWith('#')) continue;
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        // The header row is not a key.
        keys.Remove("key");
        return keys.ToArray();
    }

    private static string ReadCorpus(out int fileCount) {
        var builder = new System.Text.StringBuilder(4 * 1024 * 1024);
        int count = 0;
        foreach (string root in SearchRoots) {
            if (!Directory.Exists(root)) continue;
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
                if (!SearchExtensions.Contains(Path.GetExtension(path))) continue;
                if (path.Replace('\\', '/').EndsWith(ExcludedFromCorpus, StringComparison.Ordinal)) continue;
                builder.Append(File.ReadAllText(path)).Append('\n');
                count++;
            }
        }
        fileCount = count;
        return builder.ToString();
    }
}
