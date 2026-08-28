using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.3 mid-level resume persistence: a checkpoint save carries the whole
/// attempt (activated checkpoints, destroyed extractors, found secrets, font
/// uses, live Timeline Integrity), a resume restores it, a fresh entry or
/// Restart clears it — and the scene-side restore pays nothing twice (no
/// dust from a re-broken extractor, no double secret count).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LevelAttemptPersistenceTests {

    [TestCase]
    public void TheAttemptStateRoundTripsThroughTheSaveBlock() {
        StoryManager story = StoryManager.Instance;
        try {
            story.BeginLevelRun();
            AssertThat(story.TryActivateCheckpoint("attempt_checkpoint_1")).IsTrue();
            AssertThat(story.TryActivateCheckpoint("attempt_checkpoint_1"))
                .OverrideFailureMessage("A checkpoint activates once per attempt.")
                .IsFalse();
            story.RecordExtractorDestroyed("attempt_extractor");
            story.RegisterSecretFound("attempt_secret", isSpecialSecret: false);
            story.RecordFontUse("Orleans:attempt_font");
            story.DrainTimelineIntegrityAmount(10f);
            float integrity = story.TimelineIntegrityPercent;

            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            story.WriteAttemptStateToSave(save);
            AssertThat(save.ActivatedCheckpointIDs).Contains("attempt_checkpoint_1");
            AssertThat(save.DestroyedExtractorIDs).Contains("attempt_extractor");
            AssertThat(save.FoundSecretIDs).Contains("attempt_secret");
            AssertThat(save.FontUsesConsumed["Orleans:attempt_font"]).IsEqual(1);
            AssertThat(save.LevelIntegrityPercent).IsEqual(integrity);

            // Clear (a Restart), then restore (a resume): everything returns.
            story.ClearLevelAttemptState();
            AssertThat(story.IsCheckpointActivated("attempt_checkpoint_1")).IsFalse();
            AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);

            story.RestoreAttemptStateFromSave(save);
            AssertThat(story.IsCheckpointActivated("attempt_checkpoint_1")).IsTrue();
            AssertThat(story.IsExtractorDestroyed("attempt_extractor")).IsTrue();
            AssertThat(story.IsSecretFound("attempt_secret")).IsTrue();
            AssertThat(story.GetFontUsesConsumed("Orleans:attempt_font")).IsEqual(1);
            AssertThat(story.TimelineIntegrityPercent).IsEqual(integrity);
            AssertThat(story.LevelSecretsFound).IsEqual(1);

            // The restored checkpoint cannot pay its Mending again.
            AssertThat(story.TryActivateCheckpoint("attempt_checkpoint_1")).IsFalse();
            // And a restored secret cannot double count or double restore.
            AssertThat(story.RegisterSecretFound("attempt_secret", isSpecialSecret: false)).IsFalse();
            AssertThat(story.LevelSecretsFound).IsEqual(1);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
        }
    }

    [TestCase]
    public void AFreshEntryClearsWhatAMidLevelResumeRestores() {
        const int scratchSlot = 2;
        StoryManager story = StoryManager.Instance;
        SaveManager saveManager = SaveManager.Instance;
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        int originalRewinds = story.ChronalRewindsRemaining;
        try {
            var parked = new StorySaveData {
                SelectedCharacterID = "einstein",
                LastCheckpointID = "parked_checkpoint_1",
                LevelIntegrityPercent = 76.5f
            };
            parked.ActivatedCheckpointIDs.Add("parked_checkpoint_1");
            parked.DestroyedExtractorIDs.Add("parked_extractor");
            parked.FoundSecretIDs.Add("parked_secret");
            saveManager.SaveSlots[scratchSlot] = parked;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;

            // Resume: registries and Integrity come back; the pool stays parked.
            story.SetRewinds(1);
            story.BeginLevelRun(resumeAttempt: true);
            AssertThat(story.IsCheckpointActivated("parked_checkpoint_1")).IsTrue();
            AssertThat(story.IsExtractorDestroyed("parked_extractor")).IsTrue();
            AssertThat(story.IsSecretFound("parked_secret")).IsTrue();
            AssertThat(story.TimelineIntegrityPercent).IsEqual(76.5f);
            AssertThat(story.ChronalRewindsRemaining)
                .OverrideFailureMessage("A mid-level resume keeps the parked rewind pool.")
                .IsEqual(1);

            // Fresh entry: everything resets and the pool refills (V7.3).
            story.BeginLevelRun();
            AssertThat(story.IsCheckpointActivated("parked_checkpoint_1")).IsFalse();
            AssertThat(story.IsExtractorDestroyed("parked_extractor")).IsFalse();
            AssertThat(story.IsSecretFound("parked_secret")).IsFalse();
            AssertThat(story.TimelineIntegrityPercent).IsEqual(100f);
            AssertThat(story.ChronalRewindsRemaining).IsEqual(
                ChronalRewindManager.GetMaximumRewinds(GameManager.Instance.CurrentSession.Difficulty));
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            story.SetRewinds(originalRewinds);
            saveManager.SaveSlots[scratchSlot] = original;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
        }
    }

    [TestCase]
    public void ARestoredExtractorEntersItsDestroyedStateWithoutDustOrRestoration() {
        StoryManager story = StoryManager.Instance;
        var extractor = new ChronalExtractor {
            Name = "ResumeRestoreExtractor",
            ObjectID = "resume_restore_extractor",
            SafeWindowSeconds = 100000f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(extractor);
        int awards = 0;
        void OnDust(int amount) => awards++;
        EventBus.Instance.OnChronalDustCollected += OnDust;
        try {
            story.BeginLevelRun();
            story.DrainTimelineIntegrityAmount(10f);
            float integrityBefore = story.TimelineIntegrityPercent;

            extractor.RestoreDestroyedState();
            AssertThat(extractor.IsDestroyed).IsTrue();
            AssertThat(awards)
                .OverrideFailureMessage("A resume-restored extractor must pay no dust a second time.")
                .IsEqual(0);
            AssertThat(story.TimelineIntegrityPercent)
                .OverrideFailureMessage("A resume-restored extractor must not re-pay its +3% restoration.")
                .IsEqual(integrityBefore);
            AssertThat(story.IsExtractorDestroyed("resume_restore_extractor"))
                .OverrideFailureMessage("RestoreDestroyedState must not re-enter the registry.")
                .IsFalse();
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            extractor.Free();
        }
    }

    [TestCase]
    public void AnAlreadyFoundSecretNeverRecountsOrRestores() {
        StoryManager story = StoryManager.Instance;
        var cache = new SecretCache { Name = "ResumeSecret", SecretID = "resume_secret" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(cache);
        try {
            story.BeginLevelRun();
            story.DrainTimelineIntegrityAmount(10f);
            float integrityBefore = story.TimelineIntegrityPercent;
            int foundBefore = story.LevelSecretsFound;

            cache.MarkAlreadyFound();
            AssertThat(cache.Found).IsTrue();
            AssertThat(cache.Discover())
                .OverrideFailureMessage("A resume-marked secret must refuse a second discovery.")
                .IsFalse();
            AssertThat(story.LevelSecretsFound).IsEqual(foundBefore);
            AssertThat(story.TimelineIntegrityPercent).IsEqual(integrityBefore);
            AssertThat(cache.IsInGroup(SecretCache.Group))
                .OverrideFailureMessage("The resume pass collects caches through the secret_cache group.")
                .IsTrue();
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            cache.Free();
        }
    }
}
