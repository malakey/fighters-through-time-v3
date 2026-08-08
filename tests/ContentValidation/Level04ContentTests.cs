using System;
using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 5 Wave A: content contracts for Level 4 - Paris, 1789.
/// <para>
/// Covers the per-level gate from docs/PACKAGE5_CAMPAIGN_PLAN.md §5: the scene
/// resolves at the exact <see cref="StoryManager"/> path, the controller reports
/// the manifest level id and its three checkpoints, the dialogue set and every
/// authored line key are localized, the authored encounter table matches the
/// locked 10/0/1/3 economy row, and both era mechanics are driven for real -
/// a searchlight draining the Ultimate meter, and freeing both prisoners
/// opening the inner-courtyard gate.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level04ContentTests {
    private const string ScenePath = "res://scenes/campaign/Level_04_Paris.tscn";
    private const string LevelID = "level_04_paris";
    private const string DialoguePath = "res://resources/Dialogue/level_04_dialogue.tres";
    private const string BossPath = "res://resources/Bosses/revolutionary_tribunal.tres";
    private const string PrisonerPuzzleID = "level_04.free_prisoners";

    /// <summary>Story world units to pixels, matching the boss-roster range tests.</summary>
    private const float PixelsPerUnit = 60f;

    private const int ScratchSlot = 2;

    // === Scene and identity ===

    [TestCase]
    public void TheParisSceneLoadsInstantiatesAndFreesCleanly() {
        // StoryManager must route here; a renamed scene silently breaks the campaign.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Paris)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).OverrideFailureMessage($"{ScenePath} did not load.").IsNotNull();

        var instance = scene.Instantiate<Level04Controller>();
        AssertObject(instance).IsNotNull();
        AssertString(instance.LevelID).IsEqual(LevelID);
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndRegistersExactlyThreeCheckpoints() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual(LevelID);
        AssertThat(level.Level).IsEqual(CampaignLevel.Paris);
        AssertString(level.LevelTitleKey).IsEqual("paris_level_title");
        AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(level.DialoguePrefix).IsEqual("level_04");
        AssertString(level.EntranceDialogueID).IsEqual("level_04.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_04.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_04.exit");

        // CheckpointTrigger resolves the manager by literal node name.
        AssertObject(level.GetNodeOrNull<LevelManager>("LevelManager")).IsNotNull();

        for (int index = 0; index < 3; index++) {
            string id = $"{LevelID}_checkpoint_{index}";
            AssertThat(level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
        AssertThat(level.Levels.TryGetCheckpointPosition($"{LevelID}_checkpoint_3", out Vector2 _)).IsFalse();
    }

    [TestCase]
    public void TheLevelIsFourConfinedRoomsInsideTheAuthoredBounds() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level04Controller.LevelWidth)).IsTrue();
        // Plan §5: 8000-14000 px, Florence scale.
        AssertThat(level.LevelBounds.Size.X >= 9000f && level.LevelBounds.Size.X <= 11000f).IsTrue();
        AssertThat(level.RoomTriggers.Count).IsEqual(4);

        foreach (RoomTransitionTrigger room in level.RoomTriggers) {
            AssertThat(string.IsNullOrWhiteSpace(room.RoomID)).IsFalse();
            AssertThat(room.CameraBounds.Size.X > 0f).IsTrue();
            AssertThat(level.LevelBounds.Encloses(room.CameraBounds))
                .OverrideFailureMessage($"Room '{room.RoomID}' escapes the level bounds.").IsTrue();
            // A1 back-fills CameraPath; an unlinked room silently loses confinement.
            AssertThat(room.CameraPath != null && !room.CameraPath.IsEmpty).IsTrue();
            AssertThat(room.GetNodeOrNull<StoryCameraConfiner>(room.CameraPath) == level.Camera).IsTrue();
        }
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsInTheEnglishTable() {
        var dialogue = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(dialogue).OverrideFailureMessage($"{DialoguePath} did not load.").IsNotNull();
        AssertString(dialogue.DialogueSetID).IsEqual("dialogue_level_04");

        HashSet<string> keys = EnglishKeys();
        string[] expectedSequences = { "level_04.entrance", "level_04.boss_intro", "level_04.exit" };
        AssertThat(dialogue.Sequences.Length).IsEqual(expectedSequences.Length);

        foreach (string dialogueID in expectedSequences) {
            DialogueSequenceData sequence = dialogue.Find(dialogueID);
            AssertObject(sequence).OverrideFailureMessage($"Sequence '{dialogueID}' missing.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).IsEqual(sequence.SpeakerNameKeys.Length);

            for (int index = 0; index < sequence.LineCount; index++) {
                string lineKey = sequence.GetLineKey(index);
                AssertThat(lineKey.StartsWith("dlg_l04_", StringComparison.Ordinal))
                    .OverrideFailureMessage($"'{lineKey}' does not follow the dlg_lNN_ convention.").IsTrue();
                AssertThat(keys.Contains(lineKey))
                    .OverrideFailureMessage($"Line key '{lineKey}' is not in localization/en.csv.").IsTrue();
                AssertThat(keys.Contains(sequence.GetSpeakerKey(index)))
                    .OverrideFailureMessage($"Speaker key '{sequence.GetSpeakerKey(index)}' is not in localization/en.csv.").IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryVisibleParisStringHasAnEnglishEntry() {
        HashSet<string> keys = EnglishKeys();
        string[] required = {
            "paris_level_title",
            "paris_room_gatehouse", "paris_room_searchlights", "paris_room_cells", "paris_room_courtyard",
            "paris_objective_breach", "paris_objective_searchlight_alarm", "paris_objective_free_prisoners",
            "paris_objective_reach_courtyard", "paris_objective_defeat_boss", "paris_objective_complete",
            "paris_gate_locked", "speaker_tribunal",
            // Reused shared keys the level depends on.
            "interaction_rescue", "checkpoint", "campaign_level_paris"
        };
        TranslationServer.SetLocale("en");
        foreach (string key in required) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"'{key}' is missing from localization/en.csv.").IsTrue();
            // The compiled en.en.translation must be in step with the CSV or Node.Tr()
            // renders the raw key. `--headless --quit` does NOT rebuild it; only
            // `--headless --import` does.
            AssertString(TranslationServer.Translate(key).ToString())
                .OverrideFailureMessage(
                    $"'{key}' does not resolve through localization/en.en.translation. " +
                    "Re-import localization/en.csv.")
                .IsNotEqual(key);
        }
    }

    // === Locked encounter economy: 10 standards / 0 elites / 1 boss / 3 extractors ===

    [TestCase]
    public void TheAuthoredSpawnTableIsExactlyTenStandardsAndNoElites() {
        var byTier = new Dictionary<EnemyTier, int>();
        var byID = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0;

        foreach ((string enemyID, Vector2 _) in Level04Controller.AllStandardSpawns) {
            total++;
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).OverrideFailureMessage($"Enemy resource missing for '{enemyID}'.").IsNotNull();
            AssertString(data.EnemyID).IsEqual(enemyID);
            byTier[data.Tier] = byTier.GetValueOrDefault(data.Tier) + 1;
            byID[enemyID] = byID.GetValueOrDefault(enemyID) + 1;
        }

        // docs/DUST_ECONOMY.md level 4 row: S=10, E=0.
        AssertThat(total).IsEqual(10);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Standard)).IsEqual(10);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Elite)).IsEqual(0);

        // Era roster: the Paris standard mixed with the cultist standard, nothing else.
        AssertThat(byID.Count).IsEqual(2);
        AssertThat(byID["chrono_rioter"]).IsEqual(6);
        AssertThat(byID["chrono_slasher"]).IsEqual(4);

        // Per-room concurrency must fit level_04_pool_config's standard_enemy warm 12.
        AssertThat(Level04Controller.Room1Spawns.Length).IsEqual(3);
        AssertThat(Level04Controller.Room2Spawns.Length).IsEqual(3);
        AssertThat(Level04Controller.Room3Spawns.Length).IsEqual(4);
    }

    [TestCase]
    public void ThreeChronalExtractorsArePlacedFromTheAuthoredTable() {
        AssertThat(Level04Controller.ExtractorPlacements.Length).IsEqual(3);

        using var fixture = new ParisFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(3);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChronalExtractor extractor in fixture.Level.Extractors) {
            AssertThat(extractor.ObjectID.StartsWith("level_04.", StringComparison.Ordinal)).IsTrue();
            AssertThat(ids.Add(extractor.ObjectID))
                .OverrideFailureMessage($"Duplicate extractor id '{extractor.ObjectID}'.").IsTrue();
            // Dust stays resource-owned at the locked 15; level code must not override it.
            AssertThat(extractor.DustReward).IsEqual(15);
        }
    }

    [TestCase]
    public void TheRevolutionaryTribunalIsWiredAndItsRangeBandFitsTheCourtyard() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("revolutionary_tribunal");
        AssertThat(boss.ChronalDustDrop).IsEqual(50);

        using var fixture = new ParisFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // The arena has to be wider than the boss's ranged band or it can never
        // reach its ranged attacks and the pattern deadlocks against the wall.
        Rect2 arena = fixture.Level.RoomTriggers[3].CameraBounds;
        AssertThat(arena.Size.X > boss.RangedRangeThreshold * PixelsPerUnit)
            .OverrideFailureMessage(
                $"Courtyard is {arena.Size.X} px but the boss ranged band is " +
                $"{boss.RangedRangeThreshold * PixelsPerUnit} px.").IsTrue();
        AssertThat(arena.HasPoint(encounter.Position))
            .OverrideFailureMessage("The boss anchor sits outside the courtyard camera bounds.").IsTrue();
    }

    // === Era mechanic 1: searchlights drain the Ultimate meter ===

    [TestCase]
    public void TheSearchlightCorridorsDrainTheUltimateMeterWhileThePlayerIsExposed() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;

        // Design (design-godot.md 69-72): neural-dampening beams drain the Ultimate
        // Meter. Plan §4 asks for 2-3 beams in UltimateDrain mode.
        AssertThat(level.Searchlights.Count).IsEqual(3);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SearchlightZone light in level.Searchlights) {
            AssertThat(light.Mode).IsEqual(SearchlightMode.UltimateDrain);
            AssertFloat(light.UltimateDrainPerSecond).IsEqualApprox(5f, 0.001f);
            AssertThat(light.CollisionMask).IsEqual(CollisionLayers.Player);
            AssertThat(ids.Add(light.SearchlightID))
                .OverrideFailureMessage($"Duplicate searchlight id '{light.SearchlightID}'.").IsTrue();
        }

        SearchlightZone beam = level.Searchlights[0];
        level.Player.RestoreStoryCheckpoint(level.Player.Position, level.Player.MaximumHP, 50f);

        AssertThat(beam.AddPlayer(level.Player)).IsTrue();
        AssertThat(level.SearchlightAlarmCount).IsEqual(1);

        beam.TickExposure(1f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(45f, 0.001f);
        beam.TickExposure(2f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(35f, 0.001f);

        // Leaving the beam stops the drain; the meter is never damage.
        int hpBefore = level.Player.CurrentHP;
        AssertThat(beam.RemovePlayer(level.Player)).IsTrue();
        beam.TickExposure(3f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(35f, 0.001f);
        AssertThat(level.Player.CurrentHP).IsEqual(hpBefore);
    }

    // === Era mechanic 2: the prisoner gate ===

    [TestCase]
    public void FreeingBothPrisonersOpensTheInnerCourtyardGate() {
        using var fixture = new ParisFixture($"{LevelID}_checkpoint_2");
        Level04Controller level = fixture.Level;

        AssertThat(level.Prisoners.Count).IsEqual(2);
        AssertThat(level.CellLocks.Count).IsEqual(2);
        AssertObject(level.PrisonerPuzzle).IsNotNull();
        AssertString(level.PrisonerPuzzle.PuzzleID).IsEqual(PrisonerPuzzleID);
        AssertThat(level.PrisonerPuzzle.PersistCompletionToSave)
            .OverrideFailureMessage("The gate condition must persist or a checkpoint resume soft-locks.").IsTrue();

        // The lock mechanisms are the blow-up-the-lock beat: player hitboxes only.
        foreach (DestructibleBlock cellLock in level.CellLocks) {
            AssertThat(cellLock.CollisionLayer).IsEqual(CollisionLayers.Environment);
            AssertThat(cellLock.HitsToBreak).IsEqual(3);
            AssertThat(cellLock.IsDestroyed).IsFalse();
        }
        level.CellLocks[0].TakeEnvironmentDamage(999f);
        AssertThat(level.CellLocks[0].IsDestroyed).IsFalse();
        level.CellLocks[0].TakeEnvironmentDamage(999f);
        level.CellLocks[0].TakeEnvironmentDamage(999f);
        AssertThat(level.CellLocks[0].IsDestroyed).IsTrue();

        AssertThat(level.CourtyardGateOpen).IsFalse();
        AssertObject(level.GetNodeOrNull<StaticBody2D>("CourtyardGate")).IsNotNull();

        // One prisoner is not enough: the gate needs both.
        AssertThat(level.Prisoners[0].Rescue()).IsTrue();
        AssertThat(level.PrisonersFreed).IsEqual(1);
        AssertThat(level.PrisonerPuzzle.IsCompleted).IsFalse();
        AssertThat(level.CourtyardGateOpen).IsFalse();

        AssertThat(level.Prisoners[1].Rescue()).IsTrue();
        AssertThat(level.PrisonersFreed).IsEqual(2);
        AssertThat(level.PrisonerPuzzle.IsConditionSatisfied("prisoner_a_freed")).IsTrue();
        AssertThat(level.PrisonerPuzzle.IsConditionSatisfied("prisoner_b_freed")).IsTrue();
        AssertThat(level.PrisonerPuzzle.IsCompleted).IsTrue();
        AssertThat(level.CourtyardGateOpen).IsTrue();
    }

    [TestCase]
    public void ResumingPastTheCellsWithTheGateAlreadyEarnedDoesNotSoftLock() {
        // The PuzzleManager reads its saved completion but never re-raises
        // PuzzleCompleted, so without the controller's resume check the gate would
        // still be standing with no way left to open it.
        var save = ParisFixture.NewSave($"{LevelID}_checkpoint_2");
        save.CompletedPuzzleIDs.Add(PrisonerPuzzleID);

        using var fixture = new ParisFixture(save);
        Level04Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual($"{LevelID}_checkpoint_2");
        AssertThat(level.PrisonerPuzzle.IsCompleted).IsTrue();
        AssertThat(level.CourtyardGateOpen).IsTrue();
        // OpenDoor queues the gate; it is still addressable until the frame ends.
        var gate = level.GetNodeOrNull<StaticBody2D>("CourtyardGate");
        AssertThat(gate == null || gate.IsQueuedForDeletion())
            .OverrideFailureMessage("The courtyard gate is still solid after a completed resume.").IsTrue();
    }

    // === Helpers ===

    private static HashSet<string> EnglishKeys() {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int rows = 0;
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma <= 0) continue;
            keys.Add(line[..comma]);
            rows++;
        }
        AssertThat(rows).OverrideFailureMessage(
            $"Only {rows} rows were read from localization/en.csv.").IsGreater(500);
        return keys;
    }

    /// <summary>
    /// Builds the real Paris scene in the runner tree against a scratch save, then
    /// hands every shared singleton back on dispose.
    /// <para>
    /// The fixture always resumes at a checkpoint. That is deliberate: a fresh
    /// entry defers the entrance dialogue, which is authored
    /// <c>PausesGameplay = true</c>, and a leaked <c>SceneTree.Paused</c> stops
    /// GdUnit4's transport node and hangs the whole session
    /// (docs/PACKAGE5_CAMPAIGN_PLAN.md §9). The pause flag is restored anyway.
    /// </para>
    /// </summary>
    private sealed class ParisFixture : IDisposable {
        public readonly Level04Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public ParisFixture() : this(NewSave($"{LevelID}_checkpoint_0")) { }

        public ParisFixture(string resumeCheckpointID) : this(NewSave(resumeCheckpointID)) { }

        public ParisFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            PackedScene packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level04Controller>();
            Level.Name = "Level04ParisFixture";
            tree.Root.AddChild(Level);
        }

        public static StorySaveData NewSave(string checkpointID) => new() {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Paris),
            LastCheckpointID = checkpointID,
            CurrentHP = 100,
            CurrentUltimateMeter = 50f
        };

        public void Dispose() {
            // Pooled enemies outlive their parent in PoolManager.Active; hand them
            // back before the level goes away or the pool keeps freed references.
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");

            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }

            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
