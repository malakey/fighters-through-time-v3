using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.6 F12 checkpoint-role contract (Package 11 A3).
///
/// The whole point of the change is that <b>nothing reads the ID suffix any
/// more</b>. Self-activation, the Hard "middle inactive" rule and the
/// Timeline Integrity freeze all read the authored
/// <see cref="CheckpointRole"/>, so a saved <c>_checkpoint_1</c> can mean
/// Middle on a shared campaign level and PreBoss on Level 4A without either
/// one guessing — which is precisely the failure F12 calls out, since Hard's
/// middle rule would otherwise disable 4A's pre-boss anchor outright.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CheckpointRoleTests {

    [TestCase]
    public void TheRoleDrivesSelfActivationNotTheIdSuffix() {
        var entry = NewCheckpoint("alias_level_checkpoint_7", CheckpointRole.Entry);
        var middle = NewCheckpoint("alias_level_checkpoint_0", CheckpointRole.Middle);
        FTT.Characters.PlayerController player = FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        StoryManager.Instance.BeginLevelRun();
        try {
            // An Entry role self-activates even though its ID ends `_7`...
            entry.HandleBodyEntered(player);
            AssertThat(entry.IsActivated)
                .OverrideFailureMessage("An Entry-role fracture self-activates regardless of its ID.")
                .IsTrue();
            // ...and a Middle role does NOT, even though its ID ends `_0`.
            middle.HandleBodyEntered(player);
            AssertThat(middle.IsActivated)
                .OverrideFailureMessage("A `_checkpoint_0` spelling must not self-activate a Middle role.")
                .IsFalse();
        } finally {
            StoryManager.Instance.ClearLevelAttemptState();
            StoryManager.Instance.StopLevelRun();
            player.Free();
            Cleanup(entry, middle);
        }
    }

    [TestCase]
    public void OnlyThePreBossRoleLocksTheIntegrityClock() {
        StoryManager story = StoryManager.Instance;
        var preBoss = NewCheckpoint("role_preboss_checkpoint_1", CheckpointRole.PreBoss);
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(600f, startingExtractors: 0);
            TimelineIntegrityTests.Pump(story, seconds: 30f);
            float atStrike = story.TimelineIntegrityPercent;

            // The `_1` suffix would read as "middle" to the old rule; the role
            // says PreBoss, so the clock locks.
            preBoss.Activate();
            AssertThat(story.IsPreBossLocked)
                .OverrideFailureMessage("A `_1` suffix with role PreBoss must still lock the clock.")
                .IsTrue();
            AssertThat(story.CheckpointIntegrityPercent).IsEqualApprox(atStrike, 0.0001f);

            TimelineIntegrityTests.Pump(story, seconds: 60f);
            AssertThat(story.TimelineIntegrityPercent).IsEqualApprox(atStrike, 0.0001f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            Cleanup(preBoss);
        }
    }

    [TestCase]
    public void EntryAndMiddleBankTheAllowanceButNeverLockTheClock() {
        StoryManager story = StoryManager.Instance;
        var entry = NewCheckpoint("role_entry_checkpoint_0", CheckpointRole.Entry);
        var middle = NewCheckpoint("role_middle_checkpoint_1", CheckpointRole.Middle);
        try {
            story.BeginLevelRun();
            story.BeginIntegrityClock(600f, startingExtractors: 0);

            TimelineIntegrityTests.Pump(story, seconds: 20f);
            entry.Activate();
            AssertThat(story.IsPreBossLocked)
                .OverrideFailureMessage("Entry can never lock the boss clock.")
                .IsFalse();
            float bankedAtEntry = story.CheckpointIntegrityPercent;
            AssertThat(bankedAtEntry).IsEqualApprox(story.TimelineIntegrityPercent, 0.0001f);

            // The live gauge keeps draining after a checkpoint; the banked
            // allowance is a separate, frozen number until the next strike.
            TimelineIntegrityTests.Pump(story, seconds: 20f);
            AssertThat(story.CheckpointIntegrityPercent).IsEqualApprox(bankedAtEntry, 0.0001f);
            AssertThat(story.TimelineIntegrityPercent).IsLess(bankedAtEntry);

            middle.Activate();
            AssertThat(story.IsPreBossLocked)
                .OverrideFailureMessage("Middle can never lock the boss clock either.")
                .IsFalse();
            AssertThat(story.CheckpointIntegrityPercent).IsLess(bankedAtEntry);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            Cleanup(entry, middle);
        }
    }

    [TestCase]
    public void AnInertAnchorActivatesNothingAndAnchorsNothing() {
        StoryManager story = StoryManager.Instance;
        var inert = NewCheckpoint("role_inert_checkpoint_1", CheckpointRole.Middle);
        inert.Inert = true;
        int reached = 0;
        void Count(string _) => reached++;
        EventBus.Instance.OnCheckpointReached += Count;
        try {
            story.BeginLevelRun();
            inert.Activate();
            AssertThat(inert.IsActivated)
                .OverrideFailureMessage("An inert anchor is not a recovery destination.")
                .IsFalse();
            AssertThat(reached)
                .OverrideFailureMessage("An inert Hard middle saves nothing and refreshes nothing.")
                .IsEqual(0);
            AssertThat(story.IsCheckpointActivated("role_inert_checkpoint_1")).IsFalse();
        } finally {
            EventBus.Instance.OnCheckpointReached -= Count;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            Cleanup(inert);
        }
    }

    [TestCase]
    public void HardMakesTheMiddleInertInActsOneAndTwoOnly() {
        // Built for real, at Hard, through the shared base controller: the
        // Acts I-II middle fracture comes out inert while Entry and PreBoss do
        // not. Act III (13-15) keeps its Hard middle active; A3b owns that half.
        SessionData session = GameManager.Instance.CurrentSession;
        Difficulty original = session.Difficulty;
        var level = new FrameworkTestLevelController { Name = "HardMiddleInertLevel" };
        try {
            session.Difficulty = Difficulty.Hard;
            GameManager.Instance.CurrentSession = session;
            StoryManager.Instance.BeginLevelRun();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(level);

            AssertThat(level.MidCheckpoint.Role).IsEqual(CheckpointRole.Middle);
            AssertThat(level.MidCheckpoint.Inert)
                .OverrideFailureMessage("Hard's middle fracture is inert in Acts I-II.")
                .IsTrue();

            CheckpointTrigger entry = FindCheckpoint(level, CheckpointRole.Entry);
            CheckpointTrigger preBoss = FindCheckpoint(level, CheckpointRole.PreBoss);
            AssertThat(entry.Inert)
                .OverrideFailureMessage("Hard never disables the entrance.")
                .IsFalse();
            AssertThat(preBoss.Inert)
                .OverrideFailureMessage("Hard never disables the pre-boss anchor.")
                .IsFalse();

            // An inert anchor is not a recovery destination, so it registers
            // neither a respawn position nor an F11 route budget.
            AssertThat(level.Levels.TryGetCheckpointPosition(level.MidCheckpoint.CheckpointID, out _))
                .OverrideFailureMessage("An inert Hard middle must not anchor a respawn.")
                .IsFalse();
            AssertThat(StoryManager.Instance.GetRecoveryRouteSeconds(level.MidCheckpoint.CheckpointID))
                .IsEqual(0f);
            AssertThat(StoryManager.Instance.GetRecoveryRouteSeconds(entry.CheckpointID))
                .OverrideFailureMessage("The earlier real anchor keeps its longer remaining route.")
                .IsGreater(0f);
        } finally {
            level.Free();
            StoryManager.Instance.ClearLevelAttemptState();
            StoryManager.Instance.StopLevelRun();
            session.Difficulty = original;
            GameManager.Instance.CurrentSession = session;
        }
    }

    private static CheckpointTrigger FindCheckpoint(Node root, CheckpointRole role) {
        foreach (Node child in root.GetChildren()) {
            if (child is CheckpointTrigger checkpoint && checkpoint.Role == role) return checkpoint;
        }
        return null;
    }

    [TestCase]
    public void TheAliasMapMigratesBySavedIdAndDefaultsToEntryWhenUnknown() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            // An unregistered ID must never be guessed into PreBoss — Entry is
            // the safe answer because it cannot lock the boss clock.
            AssertThat(story.HasCheckpointRole("level_harness_two_anchor_checkpoint_1")).IsFalse();
            AssertThat(story.GetCheckpointRole("level_harness_two_anchor_checkpoint_1"))
                .OverrideFailureMessage("F12: never reinterpret a saved `_1` blindly as PreBoss.")
                .IsEqual(CheckpointRole.Entry);

            // A two-anchor level (as the retired 4A was) registers `_checkpoint_1`
            // as PreBoss; a shared level registers
            // the same spelling as Middle. Both resolve correctly by alias.
            story.RegisterCheckpointRole("level_harness_two_anchor_checkpoint_1", CheckpointRole.PreBoss);
            story.RegisterCheckpointRole("level_02_orleans_checkpoint_1", CheckpointRole.Middle);
            AssertThat(story.GetCheckpointRole("level_harness_two_anchor_checkpoint_1"))
                .IsEqual(CheckpointRole.PreBoss);
            AssertThat(story.GetCheckpointRole("level_02_orleans_checkpoint_1"))
                .IsEqual(CheckpointRole.Middle);

            // A resumed attempt that had already struck its PreBoss anchor
            // re-locks the gauge when the scene re-registers the role — the
            // reload retains the lock without replaying checkpoint benefits.
            story.BeginIntegrityClock(600f, startingExtractors: 0);
            AssertThat(story.IsPreBossLocked).IsFalse();
            story.TryActivateCheckpoint("level_15_alexandria_checkpoint_2");
            story.RegisterCheckpointRole("level_15_alexandria_checkpoint_2", CheckpointRole.PreBoss);
            AssertThat(story.IsPreBossLocked)
                .OverrideFailureMessage("A resume past the PreBoss strike keeps the clock locked.")
                .IsTrue();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    // === Fixtures =======================================================

    private static CheckpointTrigger NewCheckpoint(string id, CheckpointRole role) {
        var checkpoint = new CheckpointTrigger {
            Name = $"RoleTest_{id}",
            CheckpointID = id,
            Role = role,
            SelfActivating = role == CheckpointRole.Entry
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(checkpoint);
        return checkpoint;
    }

    private static Node2D NewPlayerStub() =>
        FTT.Characters.CharacterFactory.CreateCharacter("einstein");

    private static void Cleanup(params CheckpointTrigger[] checkpoints) {
        foreach (CheckpointTrigger checkpoint in checkpoints) {
            if (GodotObject.IsInstanceValid(checkpoint)) checkpoint.Free();
        }
    }
}
