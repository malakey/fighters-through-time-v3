using System;
using System.Collections.Generic;
using System.IO;
using FTT.Characters;
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
    public void TheLevelIsFiveConfinedRoomsInsideTheAuthoredBounds() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level04Controller.LevelWidth)).IsTrue();
        // Plan §5: 8000-14000 px, Florence scale.
        AssertThat(level.LevelBounds.Size.X >= 9000f && level.LevelBounds.Size.X <= 11000f).IsTrue();
        AssertThat(level.RoomTriggers.Count).IsEqual(5);

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
        // Package 13 W4 (S26): the Desmoulins absence beat plays for every hero.
        string[] expectedSequences = { "level_04.entrance", "level_04.absence", "level_04.boss_intro", "level_04.exit" };
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
            AssertThat(extractor.DustReward).IsEqual(3);
        }
    }

    [TestCase]
    public void TheRevolutionaryTribunalIsWiredAndItsRangeBandFitsTheCourtyard() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("revolutionary_tribunal");
        AssertThat(boss.ChronalDustDrop).IsEqual(25);

        using var fixture = new ParisFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // The arena has to be wider than the boss's ranged band or it can never
        // reach its ranged attacks and the pattern deadlocks against the wall.
        Rect2 arena = fixture.Level.RoomTriggers[4].CameraBounds;
        AssertThat(arena.Size.X > boss.RangedRangeThreshold * PixelsPerUnit)
            .OverrideFailureMessage(
                $"Courtyard is {arena.Size.X} px but the boss ranged band is " +
                $"{boss.RangedRangeThreshold * PixelsPerUnit} px.").IsTrue();
        AssertThat(arena.HasPoint(encounter.Position))
            .OverrideFailureMessage("The boss anchor sits outside the courtyard camera bounds.").IsTrue();
    }

    /// <summary>
    /// Package 12 W9 (M19): "P2 triggers on the first member's defeat: the
    /// survivor absorbs the fallen's ability set and the floor's central section
    /// collapses into the level's signature pit." The pit is deliberately
    /// non-lethal (lethal Story pits are held, plan D11): no kill boundary, and a
    /// real floor under it.
    /// </summary>
    [TestCase]
    public void TheFirstTribunalMemberFallingCollapsesTheCourtyardCentreIntoANonLethalPit() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;
        BossEncounterController encounter = level.TribunalEncounter;
        AssertObject(encounter).IsNotNull();
        AssertThat(encounter.Members.Count).IsEqual(2);
        AssertThat(level.TribunalPitOpen).IsFalse();
        AssertThat(level.TribunalPitLid.Visible).IsTrue();

        encounter.Members[0].TakeDamage(99999);
        AssertThat(encounter.IsDefeated).IsFalse();
        AssertThat(level.TribunalPitOpen).IsTrue();
        AssertThat(level.TribunalPitLid.Visible).IsFalse();
        foreach (Node child in level.TribunalPitLid.GetChildren()) {
            if (child is CollisionShape2D shape) AssertThat(shape.Disabled).IsTrue();
        }
        var pitFloor = level.GetNodeOrNull<StaticBody2D>("TribunalPitFloor");
        AssertObject(pitFloor).OverrideFailureMessage("The pit must have a floor under it.").IsNotNull();
        AssertThat(level.GetTree().GetNodesInGroup("story_kill_boundary").Count)
            .OverrideFailureMessage("The Tribunal pit is non-lethal; no kill boundary may be authored.")
            .IsEqual(0);
        // The ramps stay under the 45-degree walkable limit so bodies can climb out.
        float slope = Mathf.RadToDeg(Mathf.Atan2(Level04Controller.TribunalPitDepth, Level04Controller.TribunalPitRampRun));
        AssertThat(slope < 45f).IsTrue();
    }

    /// <summary>
    /// VERIFY-L04-PIT-FALL-NET (2026-10-09), in real physics frames. The pit floor
    /// (<see cref="Level04Controller.TribunalPitFloorY"/>, y 1120) sits 40 px under
    /// the courtyard arena's bottom edge (y 1080), and it is walkable on purpose.
    /// The arena fall net — which returns a leashed boss whose feet have left the
    /// world — used to fire on it: a survivor standing on the pit floor was put
    /// back on member 1's spawn every time it walked in. (A body dropped from that
    /// spawn rests on the east ramp at about y 1079, just above the old line, so
    /// the drop half passed even then; the pit-floor half is the one that failed.)
    /// Neither a body dropped from member 1's spawn into the open pit nor the
    /// survivor standing on the pit floor is ever returned.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheSquadSurvivorStandsInTheOpenPitWithoutTheFallNetReturningIt() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new ParisFixture();
        // Real frames: go slotless so no snapshot or checkpoint write reaches a slot file.
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level04Controller level = fixture.Level;
        BossEncounterController encounter = level.TribunalEncounter;
        AssertObject(encounter).IsNotNull();
        AssertThat(encounter.Members.Count).IsEqual(2);
        BossController survivor = encounter.Members[1];
        Vector2 spawn = survivor.GlobalPosition;
        AssertThat(spawn.X > Level04Controller.TribunalPitLeftX && spawn.X < Level04Controller.TribunalPitRightX)
            .OverrideFailureMessage($"Member 1 spawns at {spawn}, no longer over the pit.")
            .IsTrue();
        AssertThat(survivor.HasArenaLeash).IsTrue();
        AssertFloat(Level04Controller.TribunalPitFloorY).IsGreater(survivor.ArenaBounds.End.Y);

        encounter.Members[0].TakeDamage(99999);
        AssertThat(level.TribunalPitOpen).IsTrue();
        // The hit woke the squad into its reveal: play out the boss-intro beat
        // (it pauses gameplay) and the name-card hold, as the player would.
        DialogueManager dialogue = DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) {
            dialogue.AdvanceLine();
        }
        AssertThat(tree.Paused).IsFalse();
        for (int frame = 0; frame < 240 && (encounter.IsIntroBeatRunning || survivor.IsStoryRewindFrozen); frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(survivor.IsStoryRewindFrozen).IsFalse();
        // Hold the survivor still: this pins the net, not the AI's chase.
        survivor.IsEngaged = false;
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        // Member 1 drops from its spawn into the open pit.
        survivor.GlobalPosition = spawn;
        survivor.Velocity = Vector2.Zero;
        float deepest = survivor.GlobalPosition.Y;
        for (int frame = 0; frame < 120; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            deepest = Mathf.Max(deepest, survivor.GlobalPosition.Y);
        }
        AssertThat(survivor.ArenaFallRecoveries)
            .OverrideFailureMessage($"Dropped from its spawn {spawn} into the pit (deepest y {deepest:0.0}), " +
                $"the survivor was returned {survivor.ArenaFallRecoveries} time(s) by the arena fall net.")
            .IsEqual(0);
        AssertFloat(survivor.GlobalPosition.Y).IsGreater(Level04Controller.BossArenaBounds.End.Y - 40f);

        // The survivor on the pit's flat floor, for three seconds.
        float floorX = (Level04Controller.TribunalPitLeftX + Level04Controller.TribunalPitRightX) * 0.5f;
        float floorY = Level04Controller.TribunalPitFloorY;
        survivor.GlobalPosition = new Vector2(floorX, floorY - 1f);
        survivor.Velocity = Vector2.Zero;
        float lowest = survivor.GlobalPosition.Y;
        float minX = floorX;
        float maxX = floorX;
        for (int frame = 0; frame < 180; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            lowest = Mathf.Max(lowest, survivor.GlobalPosition.Y);
            minX = Mathf.Min(minX, survivor.GlobalPosition.X);
            maxX = Mathf.Max(maxX, survivor.GlobalPosition.X);
        }
        AssertThat(survivor.ArenaFallRecoveries)
            .OverrideFailureMessage($"Standing on the pit floor at x {floorX}, y {floorY}, the survivor was returned " +
                $"{survivor.ArenaFallRecoveries} time(s) by the arena fall net (last to {survivor.LastArenaFallAnchor}).")
            .IsEqual(0);
        AssertFloat(lowest).IsEqualApprox(floorY, 2f);
        AssertFloat(survivor.GlobalPosition.Y).IsEqualApprox(floorY, 2f);
        AssertThat(minX >= floorX - 2f && maxX <= floorX + 2f)
            .OverrideFailureMessage($"The survivor drifted over x {minX:0.0}..{maxX:0.0} on the pit floor.")
            .IsTrue();
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

        // The authored 5 a second is halved on the campaign hero (Momentum update 2026-10-31).
        beam.TickExposure(1f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(47.5f, 0.001f);
        beam.TickExposure(2f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(42.5f, 0.001f);

        // Leaving the beam stops the drain; the meter is never damage.
        int hpBefore = level.Player.CurrentHP;
        AssertThat(beam.RemovePlayer(level.Player)).IsTrue();
        beam.TickExposure(3f);
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(42.5f, 0.001f);
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

    // === GU-L4 (2026-10-12): the encounter locks ===

    /// <summary>
    /// The three approach rooms are encounter locks, one wave per manifest row:
    /// IDs, areas, barriers, triggers, the cell block's puzzle condition, and the
    /// baseline of every anchor. The walkway rioter now stands on the corridor
    /// floor; every member spawns inside its lock on the ground floor; the cell-B
    /// rioter stays sealed in cell B.
    /// </summary>
    [TestCase]
    public void TheThreeApproachRoomsAreEncounterLocksOneWavePerManifestRow() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;
        AssertThat(level.EncounterLocks.Count).IsEqual(3);

        var expected = new (EncounterLock Lock, string ID, float MinX, float MaxX, string Row,
            (string EnemyID, Vector2 Position)[] Table, EncounterWaveTrigger Trigger, string[] Puzzles)[] {
            (level.GatehouseLock, Level04Controller.LockGatehouse, 300f, 2000f, "level_04.room1",
                Level04Controller.Room1Spawns, EncounterWaveTrigger.OnBuild, Array.Empty<string>()),
            (level.CorridorLock, Level04Controller.LockCorridor, 3220f, 4760f, "level_04.room2",
                Level04Controller.Room2Spawns, EncounterWaveTrigger.OnEngage, Array.Empty<string>()),
            (level.CellsLock, Level04Controller.LockCells, 6120f, 8100f, "level_04.room3",
                Level04Controller.Room3Spawns, EncounterWaveTrigger.OnBuild, new[] { PrisonerPuzzleID })
        };
        foreach ((EncounterLock encounterLock, string id, float minX, float maxX, string row,
                     (string EnemyID, Vector2 Position)[] table, EncounterWaveTrigger trigger, string[] puzzles) in expected) {
            AssertObject(encounterLock).OverrideFailureMessage($"'{id}' was not built.").IsNotNull();
            AssertString(encounterLock.LockID).IsEqual(id);
            AssertThat(encounterLock.LockID.StartsWith(LevelID, StringComparison.Ordinal)).IsTrue();
            AssertFloat(encounterLock.Area.Position.X).IsEqualApprox(minX, 0.5f);
            AssertFloat(encounterLock.Area.End.X).IsEqualApprox(maxX, 0.5f);
            AssertFloat(encounterLock.EntryBarrier.CenterX).IsEqualApprox(minX - 10f, 0.5f);
            AssertFloat(encounterLock.ExitBarrier.CenterX).IsEqualApprox(maxX + 10f, 0.5f);
            // GR-LV2: the gatehouse also carries the swarm row, read by Level04TraversalStretchTests.
            AssertThat(encounterLock.Spec.Waves.Length).IsEqual(encounterLock == level.GatehouseLock ? 2 : 1);
            EncounterLockWave wave = encounterLock.Spec.Waves[0];
            AssertString(wave.RowID).IsEqual(row);
            AssertThat(wave.Trigger).IsEqual(trigger);
            AssertThat(wave.Scaled).IsTrue();
            AssertThat(wave.Spawns.Length).IsEqual(table.Length);
            for (int index = 0; index < table.Length; index++) {
                AssertString(wave.Spawns[index].EnemyID).IsEqual(table[index].EnemyID);
                Vector2 spawn = wave.Spawns[index].Position;
                AssertThat(spawn.X > minX && spawn.X < maxX)
                    .OverrideFailureMessage($"'{id}' spawns {table[index].EnemyID} at {spawn}, outside {minX}..{maxX}.")
                    .IsTrue();
                // Every Paris member stands on the ground floor (y 850 over the 900 floor).
                AssertFloat(spawn.Y).IsEqualApprox(850f, 0.5f);
                // Their patrols stay inside the lock.
                AssertThat(wave.Spawns[index].PatrolA.Value.X >= minX && wave.Spawns[index].PatrolB.Value.X <= maxX).IsTrue();
            }
            AssertThat(string.Join(",", encounterLock.Spec.RequiredPuzzleIDs)).IsEqual(string.Join(",", puzzles));
        }

        // The rioter that held the upper walkway (5600, 250) is on the corridor floor.
        foreach ((string _, Vector2 position) in Level04Controller.Room2Spawns) {
            AssertFloat(position.Y).IsEqualApprox(850f, 0.5f);
        }
        // The cell-B rioter stands in cell B (between lock B's east face, 7668,
        // and the far wall, 8020), part of the prisoner puzzle.
        AssertThat(Level04Controller.Room3Spawns[2].Position.X > 7668f && Level04Controller.Room3Spawns[2].Position.X < 8020f)
            .OverrideFailureMessage("The cell-B rioter no longer stands inside cell B.").IsTrue();

        // Every anchor lists every lock whose area ends before it.
        IReadOnlyDictionary<string, string[]> map = level.AuthoredEncounterBaselines;
        AssertThat(map[$"{LevelID}_checkpoint_0"].Length).IsEqual(0);
        AssertString(string.Join(",", map[$"{LevelID}_checkpoint_1"]))
            .IsEqual($"{Level04Controller.LockGatehouse},{Level04Controller.LockCorridor}");
        AssertString(string.Join(",", map[$"{LevelID}_checkpoint_2"]))
            .IsEqual($"{Level04Controller.LockGatehouse},{Level04Controller.LockCorridor},{Level04Controller.LockCells}");

        // The old wave triggers are gone: the locks spawn every member.
        AssertObject(level.GetNodeOrNull("Room2WaveTrigger")).IsNull();
        AssertObject(level.GetNodeOrNull("Room3WaveTrigger")).IsNull();
    }

    /// <summary>
    /// The Middle and PreBoss anchors and the Restoration Font stand outside every
    /// lock area, and each anchor's strike surface lies beyond the longest basic
    /// swing from inside the locks its baseline lists (FIX-E authoring rule).
    /// </summary>
    [TestCase]
    public void TheAnchorsAndTheFontStandOutsideEveryLockAndBeyondASwingFromInside() {
        using var fixture = new ParisFixture();
        Level04Controller level = fixture.Level;
        float swing = EncounterLockReach.LongestBasicSwingPixels();
        AssertThat(level.Levels.TryGetCheckpointPosition($"{LevelID}_checkpoint_1", out Vector2 _)).IsTrue();

        float middleSurfaceWest = Level04Controller.MiddleCheckpointX - EncounterLockReach.StrikeSurfaceHalfWidthPixels;
        float corridorReach = EncounterLockReach.EastReachFromInside(Level04Controller.CorridorLockMaxX, swing);
        AssertThat(corridorReach < middleSurfaceWest)
            .OverrideFailureMessage($"A swing from inside the corridor lock reaches x {corridorReach:0}; the Middle anchor's surface starts at {middleSurfaceWest:0}.")
            .IsTrue();
        float preBossSurfaceWest = Level04Controller.PreBossCheckpointX - EncounterLockReach.StrikeSurfaceHalfWidthPixels;
        float cellsReach = EncounterLockReach.EastReachFromInside(Level04Controller.CellsLockMaxX, swing);
        AssertThat(cellsReach < preBossSurfaceWest)
            .OverrideFailureMessage($"A swing from inside the cell block reaches x {cellsReach:0}; the PreBoss anchor's surface starts at {preBossSurfaceWest:0}.")
            .IsTrue();

        AssertThat(level.AuthoredRestorationFontPosition.HasValue).IsTrue();
        float fontX = level.AuthoredRestorationFontPosition.Value.X;
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            AssertThat(fontX + 30f <= encounterLock.Area.Position.X || fontX - 30f >= encounterLock.Area.End.X)
                .OverrideFailureMessage($"The font at {fontX} stands inside '{encounterLock.LockID}'.").IsTrue();
        }
        // The font sits between the cell block's barrier and the PreBoss anchor.
        AssertThat(fontX > Level04Controller.CellsLockMaxX && fontX < Level04Controller.PreBossCheckpointX).IsTrue();
    }

    /// <summary>
    /// The ruling for the cell block (plan §5): the courtyard gate still opens the
    /// moment both prisoners are free, but the lock's barrier opens only on
    /// "freed AND cleared" — killing the guards alone leaves it shut, and so does
    /// freeing the prisoners with a guard alive. In real frames on the real scene,
    /// resumed at the Middle anchor (the gatehouse and corridor built Cleared).
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheCellBlockBarrierOpensOnlyOnceThePrisonersAreFreeAndItsGuardsAreDown() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new ParisFixture($"{LevelID}_checkpoint_1");
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level04Controller level = fixture.Level;
        AssertThat(level.GatehouseLock.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(level.GatehouseLock.RestoredCleared).IsTrue();
        AssertThat(level.CorridorLock.State).IsEqual(EncounterLockState.Cleared);
        EncounterLock cells = level.CellsLock;
        AssertThat(cells.State).IsEqual(EncounterLockState.Armed);
        AssertThat(cells.ExitBarrier.IsClosed).IsTrue();
        // The PreBoss fracture is held while the cell block holds.
        var preBoss = level.GetNode<CheckpointTrigger>($"Checkpoint_{LevelID}_checkpoint_2");
        AssertThat(preBoss.IsHeldByEncounterLock).IsTrue();

        PlayerController hero = level.Player;
        hero.GlobalPosition = new Vector2(6400f, 880f);
        hero.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 30 && cells.State != EncounterLockState.Engaged; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(cells.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(cells.EntryBarrier.IsClosed).IsTrue();
        AssertThat(cells.LivingMemberCount).IsGreater(0);

        // Every guard down, the cell-B rioter included: the puzzle still holds the barrier.
        foreach (EnemyController member in cells.LivingMembers) member.TakeDamage(99999);
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(cells.LivingMemberCount).IsEqual(0);
        AssertThat(cells.State)
            .OverrideFailureMessage("The cell block cleared with both prisoners still locked up.")
            .IsEqual(EncounterLockState.Engaged);
        AssertThat(cells.ExitBarrier.IsClosed).IsTrue();
        AssertThat(level.CourtyardGateOpen).IsFalse();

        // Both prisoners free: the gate opens at once, the barrier on the lock's next tick.
        AssertThat(level.Prisoners[0].Rescue()).IsTrue();
        AssertThat(level.Prisoners[1].Rescue()).IsTrue();
        AssertThat(level.CourtyardGateOpen).IsTrue();
        for (int frame = 0; frame < 3 && cells.State != EncounterLockState.Cleared; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
        AssertThat(cells.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(cells.ForceCleared).IsFalse();
        AssertThat(cells.EntryBarrier.IsClosed).IsFalse();
        AssertThat(cells.ExitBarrier.IsClosed).IsFalse();
        AssertThat(preBoss.IsHeldByEncounterLock).IsFalse();
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
