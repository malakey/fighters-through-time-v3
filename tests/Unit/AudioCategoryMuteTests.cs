using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6 (M27 + G11). Per-category mutes act only on the four user
/// category buses and never mute, re-route or bypass C01b's clear path; the
/// "UI &amp; Dialogue" category reaches the Dialogue bus through its send; Mute
/// When Unfocused silences Master only while focus is lost.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AudioCategoryMuteTests {

    [TestCase]
    public void MutingACategoryNeverMutesOrReroutesTheCriticalCuePath() {
        AudioManager audio = AudioManager.Instance;
        AssertObject(audio).IsNotNull();
        int sfx = AudioServer.GetBusIndex(AudioBuses.SFX);
        int critical = AudioServer.GetBusIndex(AudioBuses.CriticalCues);
        try {
            audio.SetCategoryMuted(AudioBuses.SFX, true);
            AssertThat(AudioServer.IsBusMute(sfx)).IsTrue();
            // The clear path is untouched: never muted itself, still sending into
            // SFX, so it follows the player's SFX choice exactly as C01b requires.
            AssertThat(AudioServer.IsBusMute(critical)).IsFalse();
            AssertThat(AudioServer.GetBusSend(critical).ToString()).IsEqual(AudioBuses.SFX);

            // Only category buses are accepted.
            audio.SetCategoryMuted(AudioBuses.CriticalCues, true);
            AssertThat(AudioServer.IsBusMute(critical)).IsFalse();
            audio.SetCategoryMuted(AudioBuses.Dialogue, true);
            AssertThat(AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioBuses.Dialogue))).IsFalse();
        } finally {
            audio.ApplySavedMutes();
        }
    }

    [TestCase]
    public void TheUiAndDialogueCategoryReachesDialogueThroughItsSend() {
        int dialogue = AudioServer.GetBusIndex(AudioBuses.Dialogue);
        AssertThat(dialogue >= 0).IsTrue();
        AssertThat(AudioServer.GetBusSend(dialogue).ToString()).IsEqual(AudioBuses.UI);
    }

    [TestCase]
    public void MuteWhenUnfocusedSilencesMasterOnlyWhileFocusIsLost() {
        AssertThat(AudioManager.ResolveMasterMute(false, true, false)).IsTrue();
        AssertThat(AudioManager.ResolveMasterMute(false, true, true)).IsFalse();
        AssertThat(AudioManager.ResolveMasterMute(false, false, false)).IsFalse();
        AssertThat(AudioManager.ResolveMasterMute(true, false, true)).IsTrue();

        AudioManager audio = AudioManager.Instance;
        int master = AudioServer.GetBusIndex(AudioBuses.Master);
        try {
            audio.SetMuteWhenUnfocused(true);
            audio.HandleWindowFocusChanged(false);
            AssertThat(AudioServer.IsBusMute(master)).IsTrue();
            audio.HandleWindowFocusChanged(true);
            AssertThat(AudioServer.IsBusMute(master)).IsFalse();
            audio.SetMuteWhenUnfocused(false);
            audio.HandleWindowFocusChanged(false);
            AssertThat(AudioServer.IsBusMute(master)).IsFalse();
        } finally {
            audio.HandleWindowFocusChanged(true);
            audio.ApplySavedMutes();
        }
        AssertThat(new GlobalSaveData().MuteWhenUnfocused).IsTrue();
    }
}
