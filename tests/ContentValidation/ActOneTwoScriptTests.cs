using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W4 (design §16 Acts I–II, story review S12, S14, S17–S38): the
/// Levels 1–12 script as rewritten to the design, plus the renames that ride with
/// it — the Severed and the Linked in every owned row, the Governor's Guard at the
/// Bastille, Hakata Bay for the retired Berlin level, the Gettysburg battle in July
/// 1863 and the Globe c. 1601. Identifiers are retained everywhere (D6); only
/// English values move, so the pins read values.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ActOneTwoScriptTests {

    [TestCase]
    public void FlorenceHasTheLeonardoEntranceAndTheBorgiaExchangeForHimAlone() {
        var set = AuthoredResources.Load<DialogueSetData>("res://resources/Dialogue/level_01_dialogue.tres");
        Dictionary<string, string> english = EnglishRows();

        // S28: one line for everyone, the "Maestro" exchange for Leonardo.
        DialogueSequenceData intro = set.FindSequence("level_01.boss_intro", "einstein");
        AssertString(intro.DialogueID).IsEqual("level_01.boss_intro");
        AssertThat(intro.LineCount).IsEqual(1);
        AssertString(english[intro.LineKeys[0]]).Contains("maestro's war machines");

        DialogueSequenceData leonardoIntro = set.FindSequence("level_01.boss_intro", "leonardo");
        AssertString(leonardoIntro.DialogueID).IsEqual("level_01.boss_intro@leonardo");
        AssertThat(leonardoIntro.SpeakerNameKeys).ContainsExactly("speaker_borgia", "speaker_player");
        AssertString(english[leonardoIntro.LineKeys[1]]).Contains("I left his service");

        // S20: the neutral entrance, and Leonardo's own.
        AssertString(english[set.FindSequence("level_01.entrance", "joan").LineKeys[0]])
            .Contains("machinery here that doesn't belong to this century");
        AssertString(english[set.FindSequence("level_01.entrance", "leonardo").LineKeys[0]])
            .Contains("my Florence");

        // N03: the apprentice's recognition replaces "Where is he?" for Leonardo only.
        DialogueSequenceData exit = set.FindSequence("level_01.exit", "leonardo");
        AssertThat(exit.LineKeys.Contains("dlg_l01_recognition_leonardo")).IsTrue();
        AssertThat(exit.LineKeys.Contains("dlg_l01_exit_3")).IsFalse();
        AssertString(english["dlg_l01_exit_3_leonardo"]).Contains("only what it held");
    }

    [TestCase]
    public void TheBastilleBossIsTheGovernorsGuardUnderItsRetainedIdentifiers() {
        TranslationServer.SetLocale("en");
        var boss = AuthoredResources.Load<BossData>("res://resources/Bosses/revolutionary_tribunal.tres");
        AssertString(boss.BossID).IsEqual("revolutionary_tribunal");
        AssertString(boss.DisplayName).IsEqual("The Governor's Guard");
        AssertString(Tr(boss.DisplayNameKey)).IsEqual("The Governor's Guard");
        AssertString(Tr("speaker_tribunal")).IsEqual("Governor de Launay");
        AssertString(Tr("paris_room_courtyard")).Contains("GOVERNOR'S GUARD");
        var names = new List<string>();
        foreach (EnemyAbilityData ability in boss.BossAbilities) names.Add(Tr(ability.DisplayNameKey));
        AssertThat(names.Contains("Call the Garrison")).IsTrue();
        foreach (string name in names) {
            AssertThat(name.Contains("Tribunal") || name.Contains("Mob") || name.Contains("Verdict") || name.Contains("Guillotine"))
                .OverrideFailureMessage($"'{name}' still reads as the retired Revolutionary Tribunal.").IsFalse();
        }
        AssertString(Tr("dlg_l04_boss_1")).IsEqual("This fortress has never fallen. With these gifts, it never will.");
    }

    [TestCase]
    public void NassausFirstSaltedEraserBarksOnceWithoutPausingTheAmbush() {
        var level = new Level07Controller();
        try {
            AssertThat(level.FirstEraserBarkShown).IsFalse();
            AssertThat(level.PostFirstEraserBark()).IsTrue();
            AssertThat(level.FirstEraserBarkShown).IsTrue();
            AssertThat(level.PostFirstEraserBark()).IsFalse();
        } finally {
            level.Free();
        }
        // The bark is a non-blocking notice, never a dialogue sequence.
        var set = AuthoredResources.Load<DialogueSetData>("res://resources/Dialogue/level_07_dialogue.tres");
        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.LineKeys.Contains(Level07Controller.FirstEraserBarkKey)).IsFalse();
        }
        AssertString(Tr(Level07Controller.FirstEraserBarkKey))
            .IsEqual("Another hunter. It isn't guarding the siphons — it's looking for you.");
        AssertThat(Level07Controller.EraserCount > 0).OverrideFailureMessage("Level 7 salts an Eraser.").IsTrue();
    }

    [TestCase]
    public void TheDatesAndPlacesFollowS30S31S32AndS34() {
        TranslationServer.SetLocale("en");
        AssertString(Tr("campaign_level_berlin")).StartsWith("Hakata Bay 1281");
        AssertString(Tr("berlin_level_title")).StartsWith("Hakata Bay, 1281");
        AssertString(Tr("campaign_level_globe")).Contains("c. 1601");
        AssertString(Tr("globe_level_title")).Contains("c. 1601");
        AssertString(Tr("campaign_level_gettysburg")).Contains("July 1863");
        AssertString(Tr("gettysburg_level_title")).Contains("JULY 1863");
        AssertString(Tr("void_shard_berlin")).StartsWith("HAKATA BAY");

        Dictionary<string, string> english = EnglishRows();
        // S30: the level is the battle - no dedication platform, no address.
        foreach ((string key, string value) in english) {
            if (!key.StartsWith("dlg_l11_", StringComparison.Ordinal)) continue;
            AssertThat(value.Contains("platform", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("address", StringComparison.OrdinalIgnoreCase))
                .OverrideFailureMessage($"{key} still speaks of the November dedication.").IsFalse();
        }
        // S31: Nassau never declared independence; its end was the King's pardon.
        foreach ((string key, string value) in english) {
            if (!key.StartsWith("dlg_l07_", StringComparison.Ordinal)) continue;
            AssertThat(value.Contains("independence", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
        AssertString(english["dlg_l07_exit_3"]).Contains("King's pardon");
        // S23: the Chronal Inventor is a Linked direct-current rival.
        AssertString(english["dlg_l03_boss_1"]).StartsWith("Alternating current kills.");
    }

    [TestCase]
    public void EveryOwnedRowSaysSeveredAndNeverTheRetiredFactionName() {
        // S14: "Unbound" -> "the Severed" in every Levels 1-12 row this workstream
        // owns. The input-binding sense (controls_binding_unbound) is not a faction
        // name and is never in scope.
        string[] ownedPrefixes = {
            "dlg_l01_", "dlg_l02_", "dlg_l03_", "dlg_l04_", "dlg_l05_", "dlg_l06_",
            "dlg_l07_", "dlg_l08_", "dlg_l09_", "dlg_l10_", "dlg_l11_", "dlg_l12_",
            "florence_", "orleans_", "chicago_", "paris_", "titanic_", "pompeii_", "nassau_",
            "egypt_", "berlin_", "globe_", "gettysburg_", "lunar_", "stage_"
        };
        var survivors = new List<string>();
        int owned = 0;
        foreach ((string key, string value) in EnglishRows()) {
            if (!ownedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))) continue;
            owned++;
            if (value.Contains("Unbound", StringComparison.Ordinal)) survivors.Add(key);
        }
        AssertThat(owned).IsGreater(150);
        if (survivors.Count > 0) AssertThat(string.Join(" | ", survivors)).IsEqual("");
    }

    private static string Tr(string key) => TranslationServer.Translate(key).ToString();

    private static Dictionary<string, string> EnglishRows() {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            if (line.Length == 0 || line[0] == '#') continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            string value = line[(comma + 1)..];
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
                value = value[1..^1].Replace("\"\"", "\"");
            }
            rows[line[..comma]] = value;
        }
        return rows;
    }
}
