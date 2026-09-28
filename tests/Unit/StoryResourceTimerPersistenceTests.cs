using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W2 — GAP-01, ordinary loads preserve combat resources
/// (docs/design-contracts/STORY_PERSISTENCE.md, <c>attemptState.playerResourceTimers</c>).
///
/// <para>Before this, <c>StoryPlayerResourceTimers</c> declared block charges,
/// the regen/shatter-lockout timers, ability and Echo Step cooldowns, the
/// uncredited Rally meter and the D02d Wardenclyffe delay — and the only writer
/// was the Time Freeze cooldown. A load rebuilt the hero through
/// <c>CharacterFactory</c>, so every one of those resources silently came back
/// full. These cases drive a <b>real, partially depleted</b> hero through the
/// checkpoint and durable-snapshot writers, a JSON round trip identical to the
/// disk payload, and a fresh level construction, and pin that nothing is
/// refilled and Rally settles exactly once — across repeated reloads and an
/// Anchor Snap.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryResourceTimerPersistenceTests {

    private const int ScratchSlot = 2;
    private const string LevelID = FrameworkTestLevelController.TestLevelID;
    private static readonly string Checkpoint = $"{LevelID}_checkpoint_1";

    private sealed record Depleted(
        int Charges, float Lockout, float Regen, float Special1, float Special2, float Movement,
        int EchoFrames, float Rally, float Meter, float Wardenclyffe);

    [TestCase]
    public void ARealDepletedHeroSurvivesSaveReloadAndRepeatedReloadWithRallySettledOnce() {
        using var session = new SessionScope();
        StoryManager story = StoryManager.Instance;
        story.PrepareDirectLevel(FrameworkTestLevelController.TestLevel, "einstein", Difficulty.Normal);
        StorySaveData save = ParkedSave();
        session.UseSlot(save);
        story.BeginLevelRun();

        Depleted before;
        FrameworkTestLevelController first = session.BuildLevel();
        try {
            before = Deplete(first.Player, shatter: true);
            // The checkpoint writer: the real SaveManager site.
            SaveManager.Instance.SaveCheckpoint(Checkpoint);
        } finally {
            session.FreeLevel(first);
        }

        StoryPlayerResourceTimers captured = save.AttemptState.PlayerResourceTimers;
        AssertThat(captured.BlockCharges).IsEqual(before.Charges);
        AssertThat(captured.RallyUncreditedMeter).IsEqualApprox(before.Rally, 0.001f);
        AssertThat(before.Rally > 0f)
            .OverrideFailureMessage("The fixture must leave real uncredited Rally meter to settle.")
            .IsTrue();

        // --- First ordinary load: a JSON round trip, exactly as the disk sees it.
        string durableJson = JsonConvert.SerializeObject(save);
        StorySaveData reloaded = Reload(session, durableJson);
        FrameworkTestLevelController second = session.BuildLevel();
        float settled = Mathf.Min(UltimateMeter.MaxValue, before.Meter + before.Rally);
        try {
            AssertThat(second.ResumedMidLevel).IsTrue();
            AssertRestored(second.Player, before, settled, "first reload");
            AssertThat(story.CurrentAttempt.PlayerResourceTimers.RallyUncreditedMeter)
                .OverrideFailureMessage("The settlement must clear the attempt's uncredited record.")
                .IsEqual(0f);

            // --- The durable snapshot writer, from the rebuilt hero.
            story.CaptureDurableSnapshot();
        } finally {
            session.FreeLevel(second);
        }

        // --- Repeated reload AFTER the settlement was made durable: no second payment.
        Reload(session, JsonConvert.SerializeObject(reloaded));
        FrameworkTestLevelController third = session.BuildLevel();
        try {
            AssertRestored(third.Player, before, settled, "reload after the settled write");
        } finally {
            session.FreeLevel(third);
        }

        // --- Repeated reload of the ORIGINAL durable record (a crash before the
        // settled write landed): it settles once from that record, never twice.
        Reload(session, durableJson);
        FrameworkTestLevelController fourth = session.BuildLevel();
        try {
            AssertRestored(fourth.Player, before, settled, "reload of the unsettled record");
        } finally {
            session.FreeLevel(fourth);
        }
    }

    [TestCase]
    public void AnAnchorSnapReconstructsTheHeroWithItsActualResourcesAndNoRefill() {
        using var session = new SessionScope();
        StoryManager story = StoryManager.Instance;
        // Act III, so the Snap has an anchor to spend.
        story.PrepareDirectLevel(CampaignLevel.ChronalVoid, "einstein", Difficulty.Normal);
        StorySaveData save = ParkedSave();
        session.UseSlot(save);
        story.SuppressSceneLoadsForTesting = true;
        story.BeginLevelRun();

        Depleted before;
        FrameworkTestLevelController live = session.BuildLevel();
        try {
            before = Deplete(live.Player, shatter: false);
            // The Snap commits the attempt (capturing the live hero) BEFORE its
            // presentation, then reconstructs the level in place.
            story.BeginAnchorSnap(Checkpoint);
        } finally {
            session.FreeLevel(live);
        }
        AssertThat(story.AttemptStatus).IsEqual(StoryAttemptStatus.Active);

        FrameworkTestLevelController snapped = session.BuildLevel();
        try {
            AssertThat(snapped.ResumedMidLevel).IsTrue();
            float settled = Mathf.Min(UltimateMeter.MaxValue, before.Meter + before.Rally);
            AssertRestored(snapped.Player, before, settled, "Anchor Snap");
            AssertThat(snapped.Player.CurrentHP)
                .OverrideFailureMessage("The Snap's full-HP recovery still applies.")
                .IsEqual(snapped.Player.MaximumHP);
        } finally {
            session.FreeLevel(snapped);
        }
    }

    [TestCase]
    public void AFreshEntryNeverInheritsAnotherAttemptsDepletedRecord() {
        using var session = new SessionScope();
        StoryManager story = StoryManager.Instance;
        story.PrepareDirectLevel(FrameworkTestLevelController.TestLevel, "einstein", Difficulty.Normal);
        StorySaveData save = ParkedSave();
        save.LastCheckpointID = "";
        session.UseSlot(save);
        story.BeginLevelRun();
        // A stale, heavily depleted record on the live attempt.
        story.CurrentAttempt.PlayerResourceTimers.BlockCharges = 0;
        story.CurrentAttempt.PlayerResourceTimers.BlockLockoutSeconds = 4f;
        story.CurrentAttempt.PlayerResourceTimers.AbilityCooldowns[LegacyUnlockSchedule.SpecialOneKey] = 9f;

        FrameworkTestLevelController level = session.BuildLevel();
        try {
            AssertThat(level.ResumedMidLevel).IsFalse();
            AssertThat(level.Player.CurrentBlockCharges).IsEqual(level.Player.MaximumBlockCharges);
            AssertThat(level.Player.SpecialOneCooldownTimer).IsEqual(0f);
        } finally {
            session.FreeLevel(level);
        }
    }

    // === Fixture ============================================================

    private static StorySaveData ParkedSave() => new() {
        SelectedCharacterID = "einstein",
        Difficulty = Difficulty.Normal,
        CurrentLevelID = StoryManager.GetLevelScenePath(FrameworkTestLevelController.TestLevel),
        LastCheckpointID = Checkpoint,
        CurrentHP = 90,
        CurrentUltimateMeter = 10f
    };

    /// <summary>
    /// Depletes a live hero through its real verbs: the block system spends and
    /// ticks, the cooldown fields are what the abilities arm, and the Rally echo
    /// comes from an actual environmental hit.
    /// </summary>
    private static Depleted Deplete(PlayerController player, bool shatter) {
        BlockSystem block = player.GetNode<BlockSystem>("BlockSystem");
        if (shatter) {
            block.DepleteCharges(block.CurrentCharges);           // shatter → 5 s lockout
            block._PhysicsProcess(1.25);                          // 3.75 s left
        } else {
            block.DepleteCharges(1);                              // 2 charges left
            block._PhysicsProcess(1.5);                           // 1.5 s regen progress
        }
        player.SpecialOneCooldownTimer = 6.5f;
        player.SpecialTwoCooldownTimer = 3.25f;
        player.MovementAbilityCooldownTimer = 1.5f;
        typeof(PlayerController)
            .GetField("_echoStepCooldownFrames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(player, 77);
        player.WardenclyffeDamageDelaySeconds = 2.25f;
        player.ApplyEnvironmentalDamage(30);
        return new Depleted(
            block.CurrentCharges, block.LockoutRemainingSeconds, block.RegenProgressSeconds,
            player.SpecialOneCooldownTimer, player.SpecialTwoCooldownTimer, player.MovementAbilityCooldownTimer,
            player.EchoStepCooldownFramesRemaining, player.RallyUncreditedMeter, player.CurrentUltimateMeter,
            player.WardenclyffeDamageDelaySeconds);
    }

    private static void AssertRestored(PlayerController player, Depleted expected, float settledMeter, string when) {
        BlockSystem block = player.GetNode<BlockSystem>("BlockSystem");
        AssertThat(block.CurrentCharges)
            .OverrideFailureMessage($"{when}: block charges were refilled.").IsEqual(expected.Charges);
        AssertThat(player.CurrentBlockCharges).IsEqual(expected.Charges);
        AssertThat(block.LockoutRemainingSeconds)
            .OverrideFailureMessage($"{when}: the shatter lockout was not preserved.")
            .IsEqualApprox(expected.Lockout, 0.001f);
        AssertThat(block.RegenProgressSeconds).IsEqualApprox(expected.Regen, 0.001f);
        AssertThat(player.SpecialOneCooldownTimer).IsEqualApprox(expected.Special1, 0.001f);
        AssertThat(player.SpecialTwoCooldownTimer).IsEqualApprox(expected.Special2, 0.001f);
        AssertThat(player.MovementAbilityCooldownTimer).IsEqualApprox(expected.Movement, 0.001f);
        AssertThat(player.EchoStepCooldownFramesRemaining).IsEqual(expected.EchoFrames);
        AssertThat(player.WardenclyffeDamageDelaySeconds).IsEqualApprox(expected.Wardenclyffe, 0.001f);
        AssertThat(player.EchoPool)
            .OverrideFailureMessage($"{when}: the Rally pool must be discarded on reconstruction.")
            .IsEqual(0f);
        AssertThat(player.CurrentUltimateMeter)
            .OverrideFailureMessage($"{when}: Rally must settle into the meter exactly once.")
            .IsEqualApprox(settledMeter, 0.001f);
    }

    private static StorySaveData Reload(SessionScope session, string json) {
        StorySaveData reloaded = SaveSchemaMigrator.DeserializeStory(json);
        session.UseSlot(reloaded);
        StoryManager.Instance.BeginLevelRun(resumeAttempt: true);
        return reloaded;
    }

    /// <summary>
    /// Owns every shared singleton a case touches and hands each one back: the
    /// scratch save slot, the session, the scene-load seam and the run state.
    /// </summary>
    private sealed class SessionScope : IDisposable {
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly CampaignLevel _originalLevel;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public SessionScope() {
            _originalPaused = ((SceneTree)Engine.GetMainLoop()).Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalLevel = StoryManager.Instance.CurrentLevel;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
        }

        public void UseSlot(StorySaveData save) {
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
        }

        public FrameworkTestLevelController BuildLevel() {
            var level = new FrameworkTestLevelController { Name = "ResourceTimerTestLevel" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(level);
            return level;
        }

        public void FreeLevel(FrameworkTestLevelController level) {
            if (!GodotObject.IsInstanceValid(level)) return;
            level.GetParent()?.RemoveChild(level);
            level.Free();
        }

        public void Dispose() {
            StoryManager story = StoryManager.Instance;
            story.SuppressSceneLoadsForTesting = false;
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? "einstein" : _originalCharacter, _originalDifficulty);
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = _originalDifficulty;
            // SaveCheckpoint refreshes the session marker; a test is not a session.
            SessionExitGuard.ClearMarker();
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
