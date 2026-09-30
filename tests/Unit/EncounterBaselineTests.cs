using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W2 — GAP-13, authoritative encounter baselines (F10:
/// "rebuild ordinary encounters from an authored checkpoint baseline", bound to
/// stable encounter IDs).
///
/// <para>Package 11 derived every anchor's baseline as <c>{LevelID}_wave_1 …
/// _wave_(i+1)</c> from the anchor's position in the authoring order. That rule
/// matched only six of the fourteen campaign controllers — the rest name their
/// encounters (rooms, siege lines, galleries) — it mis-indexed the PreBoss anchor
/// behind Hard's inert middle fracture, and nothing on the resume path ever read
/// the record. Every campaign controller now authors an explicit map and the
/// resume path applies it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterBaselineTests {

    private const int ScratchSlot = 2;
    private const string GlobeScenePath = "res://scenes/campaign/Level_10_Globe.tscn";
    private const string GlobeID = Level10Controller.ID;

    /// <summary>Every campaign controller class that extends the shared base.</summary>
    private static IEnumerable<Type> CampaignControllerTypes() {
        for (int level = 2; level <= 15; level++) {
            Type type = typeof(StoryLevelControllerBase).Assembly.GetType($"FTT.Environment.Level{level:00}Controller");
            AssertObject(type).OverrideFailureMessage($"No Level{level:00}Controller type.").IsNotNull();
            yield return type;
        }
    }

    [TestCase]
    public void EveryCampaignControllerAuthorsAnExplicitNestedMapForEachOfItsAnchors() {
        var issues = new List<string>();
        int checkedControllers = 0;
        foreach (Type type in CampaignControllerTypes()) {
            var controller = (StoryLevelControllerBase)Activator.CreateInstance(type);
            try {
                checkedControllers++;
                string levelID = controller.LevelID;
                IReadOnlyDictionary<string, string[]> map = controller.AuthoredEncounterBaselines;
                if (map == null) { issues.Add($"{type.Name}: no explicit encounter-baseline map"); continue; }

                const int anchors = 3;
                var previous = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < anchors; index++) {
                    string checkpointID = $"{levelID}_checkpoint_{index}";
                    if (!map.TryGetValue(checkpointID, out string[] ids) || ids == null) {
                        issues.Add($"{type.Name}: anchor '{checkpointID}' has no baseline entry");
                        continue;
                    }
                    if (index == 0 && ids.Length != 0) issues.Add($"{type.Name}: the entry anchor clears encounters");
                    var current = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string id in ids) {
                        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(levelID, StringComparison.Ordinal)) {
                            issues.Add($"{type.Name}: '{id}' is not a stable {levelID} encounter ID");
                        }
                        if (!current.Add(id)) issues.Add($"{type.Name}: '{id}' repeats under {checkpointID}");
                    }
                    // A later anchor restores everything an earlier one did.
                    foreach (string id in previous) {
                        if (!current.Contains(id)) issues.Add($"{type.Name}: {checkpointID} drops '{id}'");
                    }
                    previous = current;
                }
                if (map.Count != anchors) {
                    issues.Add($"{type.Name}: map has {map.Count} anchors, the level authors {anchors}");
                }
            } finally {
                controller.Free();
            }
        }
        AssertThat(checkedControllers)
            .OverrideFailureMessage("Fourteen shared-base levels (S27 retired the nine Level 4A variants).")
            .IsEqual(14);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ReloadingBeforeAndAfterACheckpointAppliesTheNamedNonstandardBaseline() {
        // The Globe names its waves by room — exactly the case the derived
        // "{LevelID}_wave_N" IDs could never describe.
        using (var fixture = new GlobeFixture($"{GlobeID}_checkpoint_1", Difficulty.Normal)) {
            AssertThat(fixture.Level.ResumedMidLevel).IsTrue();
            AssertThat(string.Join(",", fixture.Level.AppliedEncounterBaseline))
                .IsEqual(Level10Controller.Room2WaveEncounter);
            AssertThat(fixture.Level.Room2WaveLatched).IsTrue();
            AssertThat(fixture.Level.Room3WaveLatched)
                .OverrideFailureMessage("A reload BEFORE the room-3 checkpoint must leave that wave live.")
                .IsFalse();
        }
        using (var fixture = new GlobeFixture($"{GlobeID}_checkpoint_2", Difficulty.Normal)) {
            AssertThat(string.Join(",", fixture.Level.AppliedEncounterBaseline))
                .IsEqual($"{Level10Controller.Room2WaveEncounter},{Level10Controller.Room3WaveEncounter}");
            AssertThat(fixture.Level.Room2WaveLatched).IsTrue();
            AssertThat(fixture.Level.Room3WaveLatched).IsTrue();
        }
    }

    [TestCase]
    public void HardsInertMiddleNoLongerShiftsThePreBossBaseline() {
        // On Hard the Acts I-II middle fracture is inert and registers nothing,
        // so the retired index derivation handed the PreBoss anchor the middle
        // anchor's baseline. The explicit map is keyed by stable ID instead.
        using var fixture = new GlobeFixture($"{GlobeID}_checkpoint_2", Difficulty.Hard);
        AssertThat(StoryManager.Instance.HasCheckpointRole($"{GlobeID}_checkpoint_1")).IsTrue();
        AssertThat(string.Join(",", StoryManager.Instance.GetEncounterBaseline($"{GlobeID}_checkpoint_2")))
            .IsEqual($"{Level10Controller.Room2WaveEncounter},{Level10Controller.Room3WaveEncounter}");
        AssertThat(fixture.Level.Room3WaveLatched).IsTrue();
    }

    [TestCase]
    public void TheResumeReaderUsesTheCommittedRecordAndRejectsAStaleLayoutWhileClaimsAreRetained() {
        // A Package 11 (version 1) record carries derived IDs that name nothing
        // in the Globe; the reader must fall back to the current map rather than
        // apply them. The collected reward claims ride the same load untouched.
        StorySaveData save = GlobeFixture.NewSave($"{GlobeID}_checkpoint_1");
        save.AttemptState = StoryAttemptState.CreateFresh(GlobeID, 0);
        save.AttemptState.CheckpointRecord.AnchorID = $"{GlobeID}_checkpoint_1";
        save.AttemptState.CheckpointRecord.BaselineVersion = 1;
        save.AttemptState.CheckpointRecord.BaselineEncounterIDs = new List<string> { $"{GlobeID}_wave_1", $"{GlobeID}_wave_2" };
        save.ClaimedRewardSourceIDs = new List<string> { "level_10.claimed_for_test#0" };
        using (var fixture = new GlobeFixture(save, Difficulty.Normal)) {
            AssertThat(string.Join(",", fixture.Level.AppliedEncounterBaseline))
                .OverrideFailureMessage("A stale-layout record must not be applied.")
                .IsEqual(Level10Controller.Room2WaveEncounter);
            AssertThat(LevelRewardDirectory.IsClaimed("level_10.claimed_for_test#0"))
                .OverrideFailureMessage("A reload must retain collected reward claims.")
                .IsTrue();
        }

        // A record committed against the CURRENT layout is authoritative.
        StorySaveData current = GlobeFixture.NewSave($"{GlobeID}_checkpoint_1");
        current.AttemptState = StoryAttemptState.CreateFresh(GlobeID, 0);
        current.AttemptState.CheckpointRecord.AnchorID = $"{GlobeID}_checkpoint_1";
        current.AttemptState.CheckpointRecord.BaselineVersion = StoryLevelControllerBase.ExplicitEncounterBaselineVersion;
        current.AttemptState.CheckpointRecord.BaselineEncounterIDs =
            new List<string> { Level10Controller.Room2WaveEncounter, Level10Controller.Room3WaveEncounter };
        using (var fixture = new GlobeFixture(current, Difficulty.Normal)) {
            AssertThat(string.Join(",", fixture.Level.AppliedEncounterBaseline))
                .IsEqual($"{Level10Controller.Room2WaveEncounter},{Level10Controller.Room3WaveEncounter}");
            AssertThat(fixture.Level.Room3WaveLatched).IsTrue();
        }
    }

    [TestCase]
    public void ActivationCommitsTheAnchorsBaselineWithItsLayoutVersion() {
        using var fixture = new GlobeFixture($"{GlobeID}_checkpoint_0", Difficulty.Normal);
        StoryManager story = StoryManager.Instance;
        story.TryActivateCheckpoint($"{GlobeID}_checkpoint_2");
        StoryCheckpointRecord record = story.CurrentAttempt.CheckpointRecord;
        AssertThat(record.AnchorID).IsEqual($"{GlobeID}_checkpoint_2");
        AssertThat(record.BaselineVersion).IsEqual(StoryLevelControllerBase.ExplicitEncounterBaselineVersion);
        AssertThat(string.Join(",", record.BaselineEncounterIDs))
            .IsEqual($"{Level10Controller.Room2WaveEncounter},{Level10Controller.Room3WaveEncounter}");
    }

    /// <summary>
    /// Builds the real Globe scene against a scratch save parked at a checkpoint,
    /// with the story's attempt restored from that save exactly as a resume does.
    /// Always resumes (checkpoint 0 included): a fresh entry defers a
    /// <c>PausesGameplay</c> entrance dialogue (CLAUDE.md signature 4).
    /// </summary>
    private sealed class GlobeFixture : IDisposable {
        public readonly Level10Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly CampaignLevel _originalLevel;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public GlobeFixture(string checkpointID, Difficulty difficulty) : this(NewSave(checkpointID), difficulty) { }

        public GlobeFixture(StorySaveData save, Difficulty difficulty) {
            var tree = (SceneTree)Engine.GetMainLoop();
            StoryManager story = StoryManager.Instance;
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalLevel = story.CurrentLevel;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            story.PrepareDirectLevel(CampaignLevel.London, "einstein", difficulty);
            save.Difficulty = difficulty;
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            story.BeginLevelRun(resumeAttempt: true);

            Level = ResourceLoader.Load<PackedScene>(GlobeScenePath).Instantiate<Level10Controller>();
            Level.Name = "EncounterBaselineGlobe";
            tree.Root.AddChild(Level);
        }

        public static StorySaveData NewSave(string checkpointID) => new() {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.London),
            LastCheckpointID = checkpointID,
            CurrentHP = 100,
            CurrentUltimateMeter = 0f
        };

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            StoryManager story = StoryManager.Instance;
            story.PrepareDirectLevel(_originalLevel,
                string.IsNullOrEmpty(_originalCharacter) ? "einstein" : _originalCharacter, _originalDifficulty);
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = _originalDifficulty;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
