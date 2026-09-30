using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W4 (design §16, the Mystery Thread absence beats — S23, S26, S35,
/// S37): the era "holds its breath" for its missing figure in seven levels. Five
/// are roster legends whose own hero hears the N03 recognition line instead
/// (Orléans/Joan, Chicago/Tesla, Alexandria/Cleopatra, the Globe/Shakespeare,
/// Gettysburg/Lincoln); two are non-roster takings every hero hears (Desmoulins at
/// Paris, Pliny at Pompeii). Each level fires its beat from one trigger placed
/// where the design puts it, ahead of its boss reveal.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbsenceBeatContentTests {

    private sealed record Beat(
        string Level, string Scene, string Speaker, string LineKey, string Hero, string RecognitionKey,
        float BossX, float RevealDistance);

    private static readonly Beat[] Beats = {
        new("02", "res://scenes/campaign/Level_02_Orleans.tscn", "speaker_captain", "dlg_l02_absence_1",
            "joan", "dlg_l02_recognition_joan", 10200f, 900f),
        new("03", "res://scenes/campaign/Level_03_Chicago.tscn", "speaker_engineer", "dlg_l03_absence_1",
            "tesla", "dlg_l03_recognition_tesla", 9800f, 800f),
        new("04", "res://scenes/campaign/Level_04_Paris.tscn", "speaker_parisian", "dlg_l04_absence_1",
            null, null, 9400f, 800f),
        new("06", "res://scenes/campaign/Level_06_Pompeii.tscn", "speaker_roman_sailor", "dlg_l06_absence_1",
            null, null, 10150f, 900f),
        new("08", "res://scenes/campaign/Level_08_Egypt.tscn", "speaker_guard", "dlg_l08_absence_1",
            "cleopatra", "dlg_l08_recognition_cleopatra", 9800f, 900f),
        new("10", "res://scenes/campaign/Level_10_Globe.tscn", "speaker_player_company", "dlg_l10_absence_1",
            "shakespeare", "dlg_l10_recognition_shakespeare", 9900f, 900f),
        new("11", "res://scenes/campaign/Level_11_Gettysburg.tscn", "speaker_union_officer", "dlg_l11_absence_1",
            "lincoln", "dlg_l11_recognition_lincoln", 10900f, 900f),
    };

    private static DialogueSetData SetFor(Beat beat) =>
        AuthoredResources.Load<DialogueSetData>($"res://resources/Dialogue/level_{beat.Level}_dialogue.tres");

    [TestCase]
    public void EveryAbsenceBeatIsOneLocalizedLineFromItsEraWitness() {
        TranslationServer.SetLocale("en");
        var issues = new List<string>();
        foreach (Beat beat in Beats) {
            DialogueSequenceData absence = SetFor(beat)?.Find($"level_{beat.Level}.absence");
            if (absence == null) { issues.Add($"level_{beat.Level}.absence is missing"); continue; }
            if (absence.LineCount != 1) issues.Add($"level_{beat.Level}.absence has {absence.LineCount} lines");
            if (absence.GetLineKey(0) != beat.LineKey) issues.Add($"level_{beat.Level}.absence plays {absence.GetLineKey(0)}");
            if (absence.GetSpeakerKey(0) != beat.Speaker) issues.Add($"level_{beat.Level}.absence is voiced by {absence.GetSpeakerKey(0)}");
            if (!string.IsNullOrEmpty(absence.HeroConditionCharacterID)) issues.Add($"level_{beat.Level}.absence is hero-conditional");
            foreach (string key in new[] { beat.LineKey, beat.Speaker }) {
                if (TranslationServer.Translate(key).ToString() == key) issues.Add($"{key} does not resolve");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ARosterLegendsOwnHeroHearsRecognitionAndEveryOtherHeroHearsTheAbsence() {
        var issues = new List<string>();
        foreach (Beat beat in Beats) {
            if (beat.Hero == null) continue;
            DialogueSetData set = SetFor(beat);
            string baseID = $"level_{beat.Level}.absence";
            foreach (string hero in CharacterRoster.IDs) {
                DialogueSequenceData chosen = set.FindSequence(baseID, hero);
                if (hero == beat.Hero) {
                    if (chosen?.DialogueID != $"{baseID}@{hero}") issues.Add($"{hero} does not get {baseID}@{hero}");
                    else if (chosen.GetLineKey(0) != beat.RecognitionKey) issues.Add($"{baseID}@{hero} plays {chosen.GetLineKey(0)}");
                } else if (chosen?.DialogueID != baseID) {
                    issues.Add($"{hero} hears {chosen?.DialogueID} instead of {baseID}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheNonRosterTakingsHaveNoSwapVariantAndSeedTheLevelFiveTaking() {
        // S26/S37: Desmoulins and Pliny are not playable legends; every hero hears
        // the beat, and the exits voice the darker read the Captain Smith taking
        // (S19) then shows on screen.
        foreach (Beat beat in Beats) {
            if (beat.Hero != null) continue;
            DialogueSetData set = SetFor(beat);
            foreach (DialogueSequenceData sequence in set.Sequences) {
                AssertThat(string.IsNullOrEmpty(sequence.HeroConditionCharacterID))
                    .OverrideFailureMessage($"level_{beat.Level} carries a hero variant: {sequence.DialogueID}").IsTrue();
            }
        }
        Dictionary<string, string> english = EnglishRows();
        AssertString(english["dlg_l04_absence_1"]).Contains("Desmoulins");
        AssertString(english["dlg_l04_exit_2"]).Contains("wasn't a legend");
        AssertString(english["dlg_l06_absence_1"]).Contains("admiral");
        AssertString(english["dlg_l06_exit_2"]).Contains("not only taking legends");
    }

    [TestCase]
    public void EachAbsenceLevelArmsItsBeatFromOneTriggerAheadOfTheBossReveal() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
        var issues = new List<string>();
        try {
            foreach (Beat beat in Beats) {
                var level = ResourceLoader.Load<PackedScene>(beat.Scene).Instantiate<StoryLevelControllerBase>();
                try {
                    tree.Root.AddChild(level);
                    AssertString(level.AbsenceDialogueID).IsEqual($"level_{beat.Level}.absence");
                    Area2D trigger = level.AbsenceTrigger;
                    if (trigger == null) { issues.Add($"level_{beat.Level} built no AbsenceTrigger"); continue; }
                    if (trigger.GetParent() != level) issues.Add($"level_{beat.Level}'s trigger is not a level child");
                    // Before the boss reveal band, so the absence and the boss intro
                    // can never contend for the dialogue box.
                    if (trigger.Position.X >= beat.BossX - beat.RevealDistance) {
                        issues.Add($"level_{beat.Level}'s trigger at {trigger.Position.X} is inside the boss reveal");
                    }
                    if (level.AbsenceBeatShown) issues.Add($"level_{beat.Level} played its beat on load");
                } finally {
                    PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
                    if (GodotObject.IsInstanceValid(level)) {
                        level.GetParent()?.RemoveChild(level);
                        level.Free();
                    }
                }
            }
        } finally {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            tree.Paused = originalPaused;
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

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
