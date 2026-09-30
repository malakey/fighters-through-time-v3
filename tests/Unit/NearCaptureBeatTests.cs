using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W4 (S36, design §6 "The near-capture" + §16 Level 9): the scripted
/// Eraser near-capture in Hakata Bay's night stealth.
///
/// <para>Pinned: the phase chain (Sarah's line → the "Hold away from the light"
/// struggle → the two after-lines); that it cannot be failed (the snare releases on
/// its own) and that only the away direction counts; that it holds the Integrity
/// clock through <see cref="IntegrityClockPause.ScriptedBeat"/> for the whole beat
/// and hands it — and any tree pause and the aura smother — back on teardown; that
/// it costs no HP or meter; and where Level 9 arms it.</para>
///
/// <para>Every case releases the clock flag and restores the pause flag in a
/// <c>finally</c> (CLAUDE.md failure signature 4).</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NearCaptureBeatTests {

    [TestCase]
    public void TheBeatRunsSnaredThenStruggleThenReleasedAndHoldsTheClockThroughout() {
        var started = new List<string>();
        var beat = new NearCaptureBeat { StartDialogue = id => { started.Add(id); return true; } };
        try {
            AssertThat(beat.Begin(null)).IsTrue();
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Snared);
            AssertThat(started).ContainsExactly(NearCaptureBeat.SnaredDialogueID);
            AssertThat(beat.IsHoldingClock).IsTrue();
            AssertThat((StoryManager.Instance.IntegrityClockPauseScopes & IntegrityClockPause.ScriptedBeat) != 0).IsTrue();
            AssertThat(beat.Begin(null)).IsFalse();

            beat.OnDialogueComplete(NearCaptureBeat.SnaredDialogueID);
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Struggle);
            AssertThat(beat.Prompt.Visible).IsTrue();
            AssertString(beat.Prompt.Text).IsEqual("Hold away from the light");

            // Toward the Eraser counts for nothing; away accumulates.
            AssertThat(beat.AdvanceStruggle(0.5f, 0f)).IsFalse();
            AssertThat(beat.AdvanceStruggle(0.4f, 1f)).IsFalse();
            AssertThat(beat.AdvanceStruggle(0.4f, 1f)).IsTrue();
            AssertThat(beat.ToreFreeByInput).IsTrue();
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Released);
            AssertThat(started).ContainsExactly(NearCaptureBeat.SnaredDialogueID, NearCaptureBeat.ReleasedDialogueID);
            AssertThat(beat.IsHoldingClock).OverrideFailureMessage("The clock stays paused through the after-lines.").IsTrue();

            beat.OnDialogueComplete(NearCaptureBeat.ReleasedDialogueID);
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Done);
            AssertThat(beat.IsHoldingClock).IsFalse();
            AssertThat((StoryManager.Instance.IntegrityClockPauseScopes & IntegrityClockPause.ScriptedBeat) != 0).IsFalse();
        } finally {
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.ScriptedBeat, false);
            beat.Free();
        }
    }

    [TestCase]
    public void TheSnareCannotBeFailedItLetsGoOnItsOwn() {
        var beat = new NearCaptureBeat();
        try {
            // No dialogue service: the struggle starts at once and the after-lines are
            // skipped, so a headless or broken box can never strand the player.
            AssertThat(beat.Begin(null)).IsTrue();
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Struggle);
            float elapsed = 0f;
            while (beat.Phase == NearCaptureBeat.NearCapturePhase.Struggle && elapsed < 10f) {
                beat.AdvanceStruggle(0.25f, 0f);
                elapsed += 0.25f;
            }
            AssertThat(elapsed).IsEqualApprox(NearCaptureBeat.AutoReleaseSeconds, 0.001f);
            AssertThat(beat.ToreFreeByInput).IsFalse();
            AssertThat(beat.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Done);
            AssertThat(beat.IsHoldingClock).IsFalse();
        } finally {
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.ScriptedBeat, false);
            beat.Free();
        }
    }

    [TestCase]
    public void InHakataBayItSmothersTheAuraCostsNothingAndHandsEverythingBackOnTeardown() {
        using var fixture = new HakataFixture();
        Level09Controller level = fixture.Level;
        NearCaptureBeat beat = level.NearCapture;
        AssertObject(beat).OverrideFailureMessage("Level 9 must build the near-capture beat.").IsNotNull();
        AssertObject(level.Player).IsNotNull();

        level.Player.RestoreStoryCheckpoint(level.Player.Position, level.Player.MaximumHP, 40f);
        int hpBefore = level.Player.CurrentHP;
        float meterBefore = level.Player.CurrentUltimateMeter;

        try {
            AssertThat(level.BeginNearCapture()).IsTrue();
            AssertThat(beat.Target == level.Player).IsTrue();
            AssertThat(level.Player.Glow.IsAuraSmothered)
                .OverrideFailureMessage("The Siphon Snare smother presentation must play on the hero.").IsTrue();
            AssertThat(level.BeginNearCapture()).IsFalse();

            beat.OnDialogueComplete(NearCaptureBeat.SnaredDialogueID);
            beat.AdvanceStruggle(NearCaptureBeat.TearFreeHoldSeconds + 0.01f, 1f);
            AssertThat(level.Player.Glow.IsAuraSmothered).IsFalse();

            // No HP, no meter.
            AssertThat(level.Player.CurrentHP).IsEqual(hpBefore);
            AssertThat(level.Player.CurrentUltimateMeter).IsEqualApprox(meterBefore, 0.0001f);
            AssertThat(beat.IsHoldingClock).IsTrue();
        } finally {
            // Tear the level down mid-beat: the clock and the pause come back.
            fixture.Dispose();
        }
        AssertThat((StoryManager.Instance.IntegrityClockPauseScopes & IntegrityClockPause.ScriptedBeat) != 0)
            .OverrideFailureMessage("A level torn down mid-beat must hand the Integrity clock back.").IsFalse();
    }

    [TestCase]
    public void TheBeatIsArmedInTheStormAnchorShipBeforeItsWaveAndIsNotAnEnemy() {
        using var fixture = new HakataFixture();
        Level09Controller level = fixture.Level;
        var trigger = level.GetNodeOrNull<Area2D>("NearCaptureTrigger");
        var wave = level.GetNodeOrNull<Area2D>("Room3WaveTrigger");
        AssertObject(trigger).IsNotNull();
        AssertObject(wave).IsNotNull();
        AssertThat(trigger.Position.X).IsEqual(Level09Controller.NearCaptureTriggerX);
        AssertThat(trigger.Position.X > Level09Controller.Room3StartX).IsTrue();
        AssertThat(trigger.Position.X < wave.Position.X).IsTrue();
        // A presentation silhouette: the locked 12/1/1/3 economy row has no Eraser.
        foreach ((string enemyID, int _, Vector2 _) in Level09Controller.SpawnTable) {
            AssertThat(enemyID == "unbound_eraser").IsFalse();
        }
        AssertThat(level.NearCapture.Phase).IsEqual(NearCaptureBeat.NearCapturePhase.Idle);
    }

    private sealed class HakataFixture : IDisposable {
        public readonly Level09Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly bool _originalPaused;
        private bool _disposed;

        public HakataFixture() {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            Level = ResourceLoader.Load<PackedScene>("res://scenes/campaign/Level_09_Berlin.tscn")
                .Instantiate<Level09Controller>();
            Level.Name = "Level09NearCaptureFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.Dialogue, false);
        }
    }
}
