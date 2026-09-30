using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Level 5 test double: records dialogue starts instead of opening the box, and
/// exposes the protected defeat path the boss encounter normally drives.
/// </summary>
internal partial class TitanicTakingTestController : Level05Controller {
    public readonly List<string> StartedDialogues = new();

    protected override float ExitDialogueDelaySeconds => 0f;

    protected override bool StartDialogue(string dialogueID) {
        StartedDialogues.Add(dialogueID);
        return true;
    }

    public void DefeatOverseerForTest() =>
        OnBossDefeated(null, new BossDefeatedPayload { BossID = "tidal_eraser", ChronalDustDrop = 25 });
}

/// <summary>
/// Package 13 W4 (S18/S19, design §16 Level 5): Captain Smith's taking and the
/// sealing-anchor exchange.
///
/// <para>As the Tidal Overseer falls, a cold extraction column takes the captain
/// at the wheel (a faint gold flicker smothered grey, the column closing like a
/// cradle); the post-boss exchange plays before the player uses the anchor —
/// "If we seal this, the ship goes down with them" / "Not the ship…"; and the
/// restoration vignette after the seal is the lifeboats rowing clear. It replaces
/// the old cargo-uplink clue.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CaptainSmithTakingTests {

    [TestCase]
    public void TheCaptainStandsAtTheWheelUntilTheOverseerFallsThenIsTakenOnce() {
        using var fixture = new TakingFixture();
        TitanicTakingTestController level = fixture.Level;
        ExtractionBeamPresentation captain = level.CaptainTaking;
        AssertObject(captain).OverrideFailureMessage("Level 5 must place Captain Smith.").IsNotNull();
        AssertThat(captain.Position).IsEqual(Level05Controller.CaptainPosition);
        AssertThat(captain.IsTaken).IsFalse();
        AssertThat(captain.Figure.Visible).IsTrue();
        // The beat plays under the pausing post-boss dialogue, like Level 0's fracture.
        AssertThat(captain.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

        level.DefeatOverseerForTest();
        AssertThat(captain.IsTaken).IsTrue();
        AssertThat(captain.BeatPlayed).IsTrue();
        // Idempotent: a duplicate defeat or a reload never takes him twice.
        AssertThat(captain.Play()).IsFalse();
    }

    [TestCase]
    public void TheExchangePlaysBeforeTheSealAndTheLifeboatsVignetteFollowsIt() {
        using var fixture = new TakingFixture();
        TitanicTakingTestController level = fixture.Level;
        AssertString(level.PostBossDialogueID).IsEqual("level_05.postboss");

        level.DefeatOverseerForTest();
        AssertThat(level.StartedDialogues).ContainsExactly("level_05.postboss");
        AssertThat(level.LifeboatsVignetteShown).IsFalse();

        // The exchange ends at the sealing anchor: the choice is the player's Interact.
        EventBus.Instance.RaiseDialogueComplete("level_05.postboss");
        AssertThat(level.StartedDialogues).ContainsExactly("level_05.postboss");
        AssertThat(level.LevelComplete).IsFalse();

        AssertThat(level.AcceptSeal()).IsTrue();
        AssertThat(level.LifeboatsVignetteShown).IsTrue();
        AssertThat(level.StartedDialogues).ContainsExactly("level_05.postboss", "level_05.exit");
    }

    [TestCase]
    public void APreSealReconstructionFindsTheCaptainAlreadyGone() {
        var beat = new ExtractionBeamPresentation();
        try {
            beat.MarkTaken();
            AssertThat(beat.IsTaken).IsTrue();
            AssertThat(beat.BeatPlayed).IsFalse();
            AssertThat(beat.Figure.Visible).IsFalse();
            AssertThat(beat.ColdColumn.Visible).IsFalse();
            AssertThat(beat.Play()).IsFalse();
        } finally {
            beat.Free();
        }
    }

    [TestCase]
    public void TheScriptIsTheDesignsAndTheCargoClueIsRetired() {
        var set = AuthoredResources.Load<DialogueSetData>("res://resources/Dialogue/level_05_dialogue.tres");
        DialogueSequenceData postboss = set.Find("level_05.postboss");
        AssertObject(postboss).IsNotNull();
        AssertThat(postboss.SpeakerNameKeys).ContainsExactly(
            "speaker_player", "speaker_sarah", "speaker_player", "speaker_sarah", "speaker_player", "speaker_sarah");
        AssertString(postboss.EmotionKeys[0]).IsEqual("emotion_shocked");
        AssertString(postboss.EmotionKeys[5]).IsEqual("emotion_injured");

        Dictionary<string, string> english = EnglishRows();
        AssertString(english[postboss.LineKeys[0]]).Contains("they took him");
        AssertString(english[postboss.LineKeys[2]]).Contains("If we seal this, the ship goes down with them");
        AssertString(english[postboss.LineKeys[5]]).StartsWith("Not the ship.");
        AssertThat(english.ContainsKey("dlg_l05_exit_4")).OverrideFailureMessage(
            "The cargo-uplink clue (dlg_l05_exit_4) is retired by S18/S19.").IsFalse();
        AssertString(english[Level05Controller.LifeboatsNoticeKey]).Contains("lifeboats");

        // The Tidal Overseer's single intro line (S23).
        DialogueSequenceData intro = set.Find("level_05.boss_intro");
        AssertThat(intro.LineCount).IsEqual(1);
        AssertString(english[intro.LineKeys[0]]).Contains("The one that got away");
    }

    private sealed class TakingFixture : IDisposable {
        public readonly TitanicTakingTestController Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly bool _originalPaused;

        public TakingFixture() {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            Level = new TitanicTakingTestController { Name = "TitanicTakingTestLevel" };
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
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
