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
/// Package 5 Wave B - Level 8, Cleopatra's Palace (Alexandria 30 BC). Pins the
/// contracts the campaign flow depends on: the scene lives at the exact path
/// <c>StoryManager</c> routes to, the controller reports the manifest identity and
/// the three locked checkpoint IDs, the authored encounter table matches the locked
/// dust-economy row (10 standards / 0 elites / 3 extractors), both halves of the era
/// identity are mechanically real (deep sand actually slows the player; the
/// hieroglyph seal actually requires the carved order), and every authored string -
/// including the FOUR-beat dialogue set with the authored Cleopatra scene - resolves.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level08ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_08_Egypt.tscn";
    private const int ScratchSlot = 2;
    /// <summary>BossController converts range thresholds at 60 px per world unit.</summary>
    private const float PixelsPerUnit = 60f;

    // === Scene ===

    [TestCase]
    public void TheSceneLoadsAtTheRoutedPathInstantiatesAndFreesCleanly() {
        // The path is a hard contract: StoryManager.LevelScenePaths[8] names it and
        // refuses to change scenes when it cannot resolve.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Egypt)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level08Controller).IsTrue();
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndAllFourDialogueBeats() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual("level_08_egypt");
        AssertThat(level.Level).IsEqual(CampaignLevel.Egypt);
        AssertString(level.LevelTitleKey).IsEqual("egypt_level_title");
        AssertString(level.DialogueSetPath).IsEqual("res://resources/Dialogue/level_08_dialogue.tres");
        AssertString(level.EntranceDialogueID).IsEqual("level_08.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_08.boss_intro");
        // The fourth beat is the authored Cleopatra scene (design-godot.md 3368-3374).
        AssertString(level.PostBossDialogueID).IsEqual("level_08.postboss");
        AssertString(level.ExitDialogueID).IsEqual("level_08.exit");

        // The LevelManager node name is what CheckpointTrigger resolves by literal path.
        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual("level_08_egypt");
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new Level08Fixture(null);
        foreach (string id in new[] {
            Level08Controller.Checkpoint0, Level08Controller.Checkpoint1, Level08Controller.Checkpoint2 }) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }

        int checkpoints = 0;
        foreach (Node child in Children(fixture.Level)) {
            if (child is CheckpointTrigger) checkpoints++;
        }
        AssertThat(checkpoints).OverrideFailureMessage(
            "Level 8 must ship exactly three checkpoints (entry anchor, midpoint, pre-boss).")
            .IsEqual(3);
    }

    [TestCase]
    public void TheSurfaceAndTombZonesAreBothCameraConfinedAndTheLevelIsFlorenceScale() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;

        AssertThat(level.RoomTriggers.Count).OverrideFailureMessage(
            "Level 8 is authored as four camera-confined rooms across two zones, and GR-LV3's Dune Crossing.").IsEqual(5);

        bool hasSurfaceRoom = false;
        bool hasUndergroundRoom = false;
        foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
            // A1 back-fills CameraPath after the player spawns; without it room
            // confinement silently does nothing.
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' has no camera path.").IsTrue();
            AssertThat(trigger.CameraBounds.Size.X).OverrideFailureMessage(
                $"Room '{trigger.RoomID}' is narrower than the 1920 px reference viewport.")
                .IsGreaterEqual(1920f);
            AssertThat(trigger.CameraBounds.Size.Y).OverrideFailureMessage(
                $"Room '{trigger.RoomID}' is shorter than the 1080 px reference viewport.")
                .IsGreaterEqual(1080f);
            if (trigger.CameraBounds.End.Y <= 1080f) hasSurfaceRoom = true;
            if (trigger.CameraBounds.Position.Y >= 720f) hasUndergroundRoom = true;
        }
        AssertThat(hasSurfaceRoom).OverrideFailureMessage(
            "No room confines to the surface band; the dune zone would scroll into the tombs.").IsTrue();
        AssertThat(hasUndergroundRoom).OverrideFailureMessage(
            "No room confines to the underground band; the tomb zone is not separated.").IsTrue();

        // Plan §5.1 sizes campaign levels at Florence scale.
        float width = level.LevelBounds.Size.X;
        AssertThat(width).IsEqual(Level08Controller.LevelWidth);
        AssertThat(width).IsGreaterEqual(9000f);
        AssertThat(width).IsLessEqual(11000f);
        // Stacked zones: the level has to be taller than one viewport.
        AssertThat(level.LevelBounds.Size.Y).IsEqual(Level08Controller.LevelHeight);
        AssertThat(level.LevelBounds.Size.Y).IsGreater(1080f);

        // The required vertical section: drop-through ledges spanning surface to tombs.
        var descent = new List<OneWayPlatform>();
        foreach (Node child in Children(level)) {
            if (child is OneWayPlatform ledge) descent.Add(ledge);
        }
        AssertThat(descent.Count).OverrideFailureMessage(
            "The burial-shaft descent needs drop-through ledges.").IsGreaterEqual(3);
        float highest = float.MaxValue;
        float lowest = float.MinValue;
        foreach (OneWayPlatform ledge in descent) {
            highest = Mathf.Min(highest, ledge.Position.Y);
            lowest = Mathf.Max(lowest, ledge.Position.Y);
        }
        AssertThat(lowest - highest).OverrideFailureMessage(
            "The descent barely drops; it is not a vertical section.").IsGreater(400f);
    }

    // === Era identity 1: shifting dunes ===

    [TestCase]
    public void DeepSandDriftsHalveMovementAndCoverTheWalkableSurface() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;

        AssertThat(level.DeepSandZones.Count).OverrideFailureMessage(
            "The surface zone is defined by its sand drifts; they must be authored in the scene.")
            .IsEqual(Level08Controller.DeepSandZoneCount);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var spans = new List<(float Left, float Right)>();
        foreach (MovementDampenerZone drift in level.DeepSandZones) {
            AssertThat(ids.Add(drift.ZoneID)).OverrideFailureMessage(
                $"Duplicate sand drift id '{drift.ZoneID}'.").IsTrue();
            AssertThat(drift.ZoneID.StartsWith("level_08.")).IsTrue();
            AssertFloat(drift.MoveMultiplier).OverrideFailureMessage(
                "The design quantifies deep sand as a ~50% slow.")
                .IsEqualApprox(Level08Controller.DeepSandMultiplier, 0.001f);
            AssertThat(drift.Enabled).IsTrue();
            AssertThat(drift.CollisionMask).IsEqual(CollisionLayers.Player);

            // A drift that is not on the surface the player walks is decorative.
            AssertThat(drift.GlobalPosition.X).IsGreater(0f);
            AssertThat(drift.GlobalPosition.X).IsLess(Level08Controller.SurfaceEndX);
            float half = HalfWidth(drift);
            AssertThat(half).IsGreater(0f);
            AssertThat(drift.GlobalPosition.Y + HalfHeight(drift))
                .OverrideFailureMessage("The drift does not reach the desert floor.")
                .IsGreaterEqual(Level08Controller.SurfaceGroundY - 40f);
            spans.Add((drift.GlobalPosition.X - half, drift.GlobalPosition.X + half));
        }

        // EnvironmentPlayerModifiers publishes the PRODUCT of every live source, so
        // two overlapping drifts would compound to a 0.25x crawl (Wave A, L05).
        spans.Sort((a, b) => a.Left.CompareTo(b.Left));
        for (int index = 1; index < spans.Count; index++) {
            AssertThat(spans[index].Left).OverrideFailureMessage(
                "Two sand drifts overlap; their multipliers would compound.")
                .IsGreaterEqual(spans[index - 1].Right);
        }

        PlayerController player = level.Player;
        AssertObject(player).IsNotNull();
        AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
        MovementDampenerZone sand = level.DeepSandZones[0];
        AssertThat(sand.AddPlayer(player)).IsTrue();
        AssertFloat(player.EnvironmentMoveMultiplier).OverrideFailureMessage(
            "Standing in deep sand must actually slow the player.")
            .IsEqualApprox(Level08Controller.DeepSandMultiplier, 0.001f);
        AssertThat(sand.RemovePlayer(player)).IsTrue();
        AssertFloat(player.EnvironmentMoveMultiplier).IsEqualApprox(1f, 0.001f);
    }

    // === Era identity 2: the hieroglyph seal ===

    [TestCase]
    public void TheHieroglyphSealOpensOnlyInTheCarvedOrderAndResetsOnAWrongPick() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;

        SequenceLock seal = level.HieroglyphLock;
        PuzzleManager puzzle = level.HieroglyphPuzzle;
        AssertObject(seal).IsNotNull();
        AssertObject(puzzle).IsNotNull();
        AssertString(puzzle.PuzzleID).IsEqual(Level08Controller.HieroglyphPuzzleID);
        AssertThat(puzzle.PersistCompletionToSave).OverrideFailureMessage(
            "The vault seal must survive a checkpoint resume.").IsTrue();
        AssertThat(seal.GlyphCount).IsEqual(Level08Controller.GlyphCount);
        AssertThat(level.VaultDoorOpen).OverrideFailureMessage(
            "The vault must start sealed or the puzzle is decorative.").IsFalse();

        // The order carved on the alcove relief is NOT the wall's left-to-right order:
        // if it were, the puzzle would solve itself by walking east.
        var wallOrder = new List<SequenceGlyph>(seal.Glyphs);
        wallOrder.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        bool wallMatchesAnswer = true;
        for (int index = 0; index < wallOrder.Count; index++) {
            if (wallOrder[index].OrderIndex != index) wallMatchesAnswer = false;
        }
        AssertThat(wallMatchesAnswer).OverrideFailureMessage(
            "The glyphs are mounted in their solution order, so the relief is pointless.")
            .IsFalse();

        SequenceGlyph first = level.GlyphForOrder(0);
        SequenceGlyph last = level.GlyphForOrder(Level08Controller.GlyphCount - 1);
        AssertObject(first).IsNotNull();
        AssertObject(last).IsNotNull();

        AssertThat(seal.Activate(first)).IsTrue();
        AssertThat(seal.Progress).IsEqual(1);
        AssertThat(first.IsLit).IsTrue();

        // A wrong pick unlights everything and restarts the sequence.
        AssertThat(seal.Activate(last)).IsFalse();
        AssertThat(seal.Progress).IsEqual(0);
        AssertThat(first.IsLit).IsFalse();
        AssertThat(seal.IsCompleted).IsFalse();
        AssertThat(level.VaultDoorOpen).IsFalse();

        // The carved order, pressed the way four player interactions would.
        AssertThat(level.SolveHieroglyphLockForTest()).IsTrue();
        AssertThat(seal.IsCompleted).IsTrue();
        AssertThat(puzzle.IsCompleted).IsTrue();
        AssertThat(level.VaultDoorOpen).OverrideFailureMessage(
            "Completing the seal must actually drop the vault door.").IsTrue();

        // The completion handler is idempotent: PuzzleManager re-emits PuzzleCompleted
        // one deferred frame after _Ready for a save-restored completion, so a resumed
        // run runs it twice (Wave A integration).
        puzzle.EmitSignal(PuzzleManager.SignalName.PuzzleCompleted, puzzle.PuzzleID);
        AssertThat(level.VaultDoorOpen).IsTrue();
    }

    [TestCase]
    public void ResumingAtThePreBossCheckpointNeverLeavesThePlayerWalledIn() {
        // The pre-boss checkpoint stands past the vault door. A save taken there must
        // not reload behind a sealed seal, whether or not the puzzle flag survived.
        using var fixture = new Level08Fixture(new StorySaveData {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Egypt),
            LastCheckpointID = Level08Controller.Checkpoint2,
            CurrentHP = 70,
            CurrentUltimateMeter = 25f
        });
        Level08Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual(Level08Controller.Checkpoint2);
        AssertThat(level.VaultDoorOpen).OverrideFailureMessage(
            "A run resumed at the pre-boss checkpoint must find the vault already open.")
            .IsTrue();
        AssertThat(level.Player.GlobalPosition.X).IsGreater(Level08Controller.VaultGateX);
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredEncounterTableMatchesTheLockedEconomyRow() {
        // docs/DUST_ECONOMY.md / DustEconomyTests row 8: 10 standards, 0 elites,
        // 1 boss, 3 extractors. These numbers are locked; content conforms to them.
        AssertThat(Level08Controller.AuthoredStandardCount).OverrideFailureMessage(
            "Level 8 authors exactly 10 standard enemies at Normal.").IsEqual(10);
        AssertThat(Level08Controller.AuthoredEliteCount).OverrideFailureMessage(
            "The locked row says E = 0, so Level 8 places NO elites.").IsEqual(0);
        AssertThat(Level08Controller.AuthoredExtractorCount).IsEqual(3);

        var eraRoster = new HashSet<string>(StringComparer.Ordinal) { "plasma_spear_ward", "chrono_slasher" };
        var extractorIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, Vector2 _) in Level08Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id)).OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }

        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in AllAuthoredSpawns()) {
            AssertThat(eraRoster.Contains(enemyID)).OverrideFailureMessage(
                $"'{enemyID}' is not on the Alexandria roster (plasma_spear_ward + chrono_slasher).").IsTrue();
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.Tier).OverrideFailureMessage(
                $"'{enemyID}' must be a Standard-tier enemy; Level 8 places no elites.")
                .IsEqual(EnemyTier.Standard);
        }
    }

    [TestCase]
    public void ThePlacedExtractorsAndBossEncounterMatchTheAuthoredTables() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;

        AssertThat(level.Extractors.Count).IsEqual(Level08Controller.AuthoredExtractorCount);
        foreach (ChronalExtractor extractor in level.Extractors) {
            // 15 dust each is resource-owned (docs/DUST_ECONOMY.md) and must not be
            // re-specified by level code.
            AssertThat(extractor.DustReward).IsEqual(3);
        }

        AssertThat(level.BossEncounters.Count).IsEqual(1);
        AssertString(level.BossEncounters[0].Data.BossID).IsEqual("jackal_priest");

        // Live standards never exceed the biggest single wave, inside the level_08
        // pool config's standard_enemy warm count of 12.
        int largestWave = 0;
        foreach (var wave in AllAuthoredWaves()) largestWave = Math.Max(largestWave, wave.Length);
        AssertThat(largestWave).IsLessEqual(12);
    }

    [TestCase]
    public void TheBossResourceResolvesAndItsRangedBandFitsTheArena() {
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>(Level08Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("jackal_priest");
        AssertThat(boss.PhaseThresholds.Length).OverrideFailureMessage(
            "The Jackal Priest is authored as a two-phase fight.").IsEqual(1);

        // Asserted against the resource, never a hardcoded width: re-tuning the boss
        // then cannot silently outgrow the sarcophagus chamber.
        float rangedPixels = boss.RangedRangeThreshold * PixelsPerUnit;
        float arenaWidth = Level08Controller.BossArenaWidth;
        AssertThat(arenaWidth).OverrideFailureMessage(
            $"Boss arena is {arenaWidth} px but the ranged band alone needs {rangedPixels} px.")
            .IsGreater(rangedPixels);
        AssertThat(arenaWidth).OverrideFailureMessage(
            "A teleporting summoner needs more than one ranged band of open floor.")
            .IsGreater(rangedPixels * 2f);
    }

    // === Localization ===

    [TestCase]
    public void TheFourDialogueBeatsResolveAndEveryAuthoredKeyIsLocalized() {
        var set = FTT.Core.AuthoredResources.Load<DialogueSetData>(Level08Controller.DialogueResourcePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_08");
        AssertThat(set.Sequences.Length).OverrideFailureMessage(
            "Level 8 ships five beats (Package 13 W4 added the throne-room absence) and the " +
            "two N03 Cleopatra variants; the Package 12 M14 list variants are now the " +
            "{MissingPlaces} token.").IsEqual(7);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        TranslationServer.SetLocale("en");

        var required = new List<string> {
            "egypt_level_title", "egypt_room_dunes", "egypt_room_siege", "egypt_room_tombs",
            "egypt_room_chamber", "egypt_objective_dunes", "egypt_objective_tombs",
            "egypt_objective_seal", "egypt_objective_chamber", "egypt_objective_defeat_boss",
            "egypt_objective_complete", "egypt_deep_sand", "egypt_vault_sealed",
            "egypt_relief_order", "interaction_press_glyph", "speaker_cleopatra"
        };
        foreach ((string _, string labelKey, int _) in Level08Controller.GlyphPlan) required.Add(labelKey);

        foreach (string sequenceID in new[] {
            "level_08.entrance", "level_08.absence", "level_08.boss_intro", "level_08.postboss",
            "level_08.exit" }) {
            DialogueSequenceData sequence = set.Find(sequenceID);
            AssertObject(sequence).OverrideFailureMessage(
                $"Dialogue sequence '{sequenceID}' is missing from the set.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).OverrideFailureMessage(
                $"'{sequenceID}' has a speaker/line count mismatch.")
                .IsEqual(sequence.SpeakerNameKeys.Length);
            for (int index = 0; index < sequence.LineCount; index++) {
                required.Add(sequence.GetLineKey(index));
                required.Add(sequence.GetSpeakerKey(index));
                required.Add(sequence.GetEmotionKey(index));
            }
        }

        // V7.5 Mystery Thread: Cleopatra is a captive, so the default post-boss
        // beat is the player and Sarah on the radio finding an empty throne
        // (design-godot.md section 16, Level 8 post-boss). The hero-is-Cleopatra
        // branch is the N03 recognition variant.
        DialogueSequenceData postBoss = set.Find("level_08.postboss");
        AssertThat(postBoss.LineCount).IsEqual(4);
        AssertString(postBoss.GetSpeakerKey(0)).IsEqual("speaker_player");
        AssertString(postBoss.GetSpeakerKey(1)).IsEqual("speaker_sarah");
        AssertString(postBoss.GetSpeakerKey(2)).IsEqual("speaker_player");
        AssertString(postBoss.GetSpeakerKey(3)).IsEqual("speaker_sarah");

        foreach (string variant in new[] {
            "level_08.absence@cleopatra", "level_08.postboss@cleopatra" }) {
            DialogueSequenceData branch = set.Find(variant);
            AssertObject(branch).OverrideFailureMessage(
                $"The N03 recognition variant '{variant}' is missing.").IsNotNull();
            AssertString(branch.HeroConditionCharacterID).IsEqual("cleopatra");
            for (int index = 0; index < branch.LineCount; index++) {
                required.Add(branch.GetLineKey(index));
                required.Add(branch.GetSpeakerKey(index));
                required.Add(branch.GetEmotionKey(index));
            }
        }

        foreach (string key in required) {
            AssertThat(keys.Contains(key)).OverrideFailureMessage(
                $"'{key}' is missing from localization/en.csv.").IsTrue();
            // Node.Tr() reads the compiled translation, not the CSV: if they drift,
            // the level renders raw keys.
            AssertString(TranslationServer.Translate(key).ToString()).OverrideFailureMessage(
                $"'{key}' does not resolve through localization/en.en.translation. " +
                "Re-import localization/en.csv.").IsNotEqual(key);
        }
    }

    // === GU-L8 (2026-10-12): the four encounter locks ===

    [TestCase]
    public void TheFourFightsAreEncounterLocksOneWavePerManifestRow() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;
        try {
            AssertThat(level.EncounterLocks.Count).IsEqual(4);
            EncounterLock dunes = level.DunesLock;
            EncounterLock siegeLine = level.SiegeLineLock;
            EncounterLock tomb = level.TombLock;
            EncounterLock vault = level.VaultLock;
            AssertString(dunes.LockID).IsEqual(Level08Controller.LockDunes);
            AssertString(siegeLine.LockID).IsEqual(Level08Controller.LockSiegeLine);
            AssertString(tomb.LockID).IsEqual(Level08Controller.LockTomb);
            AssertString(vault.LockID).IsEqual(Level08Controller.LockVault);

            // The areas: the dunes 300-2000 (GR-LV3's carve), the siege line 3220-5460 (short of the
            // shaft lip), the tomb 5720-7000 (the shaft and the west corridor, full
            // height) and the vault 7020-8310 (the corridor band only, from the
            // ceiling's underside down).
            AssertThat(dunes.Area.Position.X).IsEqual(300f);
            AssertThat(dunes.Area.End.X).IsEqual(2000f);
            AssertThat(siegeLine.Area.Position.X).IsEqual(3220f);
            AssertThat(siegeLine.Area.End.X).IsEqual(5460f);
            AssertThat(tomb.Area.Position.X).IsEqual(5720f);
            AssertThat(tomb.Area.End.X).IsEqual(7000f);
            AssertThat(tomb.Area.Position.Y).IsEqual(StoryGateRules.ColumnTopY);
            AssertThat(vault.Area.Position.X).IsEqual(7020f);
            AssertThat(vault.Area.End.X).IsEqual(8310f);
            AssertThat(vault.Area.Position.Y).IsEqual(Level08Controller.VaultLockTopY);
            AssertThat(vault.Area.End.Y).IsEqual(Level08Controller.LevelHeight);

            // The shaft is no escape: the siege line's exit barrier stands on the
            // surface short of the lip, and the tomb's entry on the shaft's west wall.
            AssertThat(siegeLine.ExitBarrier.Footprint.End.X).IsLess(Level08Controller.SurfaceEndX);
            AssertThat(siegeLine.ExitBarrier.BottomY).IsEqual(Level08Controller.SurfaceGroundY);
            AssertThat(tomb.EntryBarrier.Footprint.Position.X).IsEqual(Level08Controller.SurfaceEndX);
            AssertThat(tomb.EntryBarrier.BottomY).IsEqual(Level08Controller.TombFloorY);
            // The vault's exit fills the sealed door under its gate column.
            AssertThat(vault.ExitBarrier.CenterX).IsEqual(Level08Controller.VaultGateX);
            AssertThat(vault.ExitBarrier.TopY).IsEqual(1000f);
            AssertThat(vault.ExitBarrier.BottomY).IsEqual(Level08Controller.TombFloorY);
            AssertThat(string.Join(",", vault.Spec.RequiredPuzzleIDs)).IsEqual(Level08Controller.HieroglyphPuzzleID);

            // One wave per manifest row, the authored tables, the triggers.
            (EncounterLock Lock, string Row, int Length, EncounterWaveTrigger Trigger)[] waves = {
                (dunes, Level08Controller.DuneRowID, Level08Controller.DuneSpawns.Length, EncounterWaveTrigger.OnBuild),
                (siegeLine, Level08Controller.SiegeLineRowID, Level08Controller.SiegeLineSpawns.Length, EncounterWaveTrigger.OnEngage),
                (tomb, Level08Controller.TombRowID, Level08Controller.TombSpawns.Length, EncounterWaveTrigger.OnBuild),
                (vault, Level08Controller.VaultRowID, Level08Controller.VaultSpawns.Length, EncounterWaveTrigger.OnBuild)
            };
            foreach ((EncounterLock encounterLock, string row, int length, EncounterWaveTrigger trigger) in waves) {
                // GR-LV3: the siege line also takes the swarm row, after its authored wave.
                AssertThat(encounterLock.Spec.Waves.Length).IsEqual(encounterLock == siegeLine ? 2 : 1);
                AssertString(encounterLock.Spec.Waves[0].RowID).IsEqual(row);
                AssertThat(encounterLock.Spec.Waves[0].Spawns.Length).IsEqual(length);
                AssertThat(encounterLock.Spec.Waves[0].Trigger).IsEqual(trigger);
                AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
                AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
                AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
            }
            AssertThat(siegeLine.LivingMemberCount).IsEqual(0);
            AssertThat(vault.LivingMemberCount).IsEqual(vault.PlannedTotal);
            foreach (EnemyController member in vault.LivingMembers) AssertThat(member.IsEncounterDormant).IsTrue();

            // Every later anchor lists the locks before it.
            IReadOnlyDictionary<string, string[]> baselines = level.AuthoredEncounterBaselines;
            AssertThat(baselines[Level08Controller.Checkpoint0].Length).IsEqual(0);
            AssertThat(string.Join(",", baselines[Level08Controller.Checkpoint1]))
                .IsEqual($"{Level08Controller.LockDunes},{Level08Controller.LockSiegeLine}");
            AssertThat(string.Join(",", baselines[Level08Controller.Checkpoint2]))
                .IsEqual($"{Level08Controller.LockDunes},{Level08Controller.LockSiegeLine},"
                    + $"{Level08Controller.LockTomb},{Level08Controller.LockVault}");
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    [TestCase]
    public void TheMiddleAndPreBossAnchorsStandOutsideEveryLockBeyondSwingReach() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;
        try {
            var middle = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level08Controller.Checkpoint1}");
            var preBoss = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level08Controller.Checkpoint2}");
            AssertThat(middle.Role).IsEqual(CheckpointRole.Middle);
            AssertThat(middle.GlobalPosition.X).IsEqual(Level08Controller.MiddleCheckpointX);
            AssertThat(preBoss.GlobalPosition.X).IsEqual(Level08Controller.PreBossCheckpointX);
            // The Middle stands on the surface at the shaft lip, between the siege
            // line's exit barrier and the shaft.
            AssertThat(middle.GlobalPosition.Y).IsLess(Level08Controller.SurfaceGroundY);
            AssertThat(middle.GlobalPosition.X + 30f).IsLess(Level08Controller.SurfaceEndX);
            foreach (CheckpointTrigger anchor in new[] { middle, preBoss }) {
                foreach (EncounterLock encounterLock in level.EncounterLocks) {
                    bool inside = anchor.GlobalPosition.X + 30f > encounterLock.Area.Position.X
                        && anchor.GlobalPosition.X - 30f < encounterLock.Area.End.X;
                    AssertThat(inside).OverrideFailureMessage(
                        $"{anchor.CheckpointID} stands inside '{encounterLock.LockID}'.").IsFalse();
                }
            }
            // Out of swing reach from inside the lock each one comes after.
            float fromSiege = LevelLockSwingReach.EastwardReachFrom(level.SiegeLineLock);
            AssertThat(fromSiege < LevelLockSwingReach.StrikeSurfaceWestEdge(middle.GlobalPosition.X))
                .OverrideFailureMessage($"A swing from the siege line reaches x {fromSiege:0}.").IsTrue();
            float fromVault = LevelLockSwingReach.EastwardReachFrom(level.VaultLock);
            AssertThat(fromVault < LevelLockSwingReach.StrikeSurfaceWestEdge(preBoss.GlobalPosition.X))
                .OverrideFailureMessage($"A swing from the vault reaches x {fromVault:0}.").IsTrue();
            // Both are held until the locks their baselines list are cleared.
            AssertThat(middle.IsHeldByEncounterLock).IsTrue();
            AssertThat(preBoss.IsHeldByEncounterLock).IsTrue();
            // The absence beat's trigger moved east of the PreBoss respawn point.
            AssertThat(level.AbsenceTrigger.Position.X - 30f).IsGreater(preBoss.GlobalPosition.X + 20f);
            // The carved relief is in the vault lock, west of the glyph wall.
            Vector2 relief = Level08Controller.CarvedReliefPosition;
            AssertThat(relief.X).IsGreater(level.VaultLock.Area.Position.X);
            AssertThat(relief.X).IsLess(level.GlyphForOrder(2).GlobalPosition.X);
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// GU-L8: the tomb corridor used to end in a cliff at the shaft's west edge
    /// (5700), with nothing under the surface west of it; a wall now closes it from
    /// the surface top down past the corridor floor.
    /// </summary>
    [TestCase]
    public void TheTombCorridorIsClosedAtItsWestEnd() {
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;
        try {
            bool closed = false;
            foreach (Node child in Children(level)) {
                if (child is not StaticBody2D body || child is EncounterLockBarrier) continue;
                if (body.GetChildCount() == 0 || body.GetChild(0) is not CollisionShape2D { Shape: RectangleShape2D rect } shape) continue;
                Vector2 centre = body.Position + shape.Position;
                var box = new Rect2(centre - rect.Size / 2f, rect.Size);
                if (box.Position.X <= Level08Controller.ShaftWestWallX + 0.5f
                    && box.End.X >= Level08Controller.SurfaceEndX - 0.5f
                    && box.Position.Y <= Level08Controller.SurfaceGroundY + 0.5f
                    && box.End.Y >= Level08Controller.TombFloorY + 1f) {
                    closed = true;
                }
            }
            AssertThat(closed).OverrideFailureMessage(
                "Nothing closes the tomb corridor's west end under the surface.").IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    /// <summary>
    /// GU-L8, the real scene in real physics frames: a hero walking the corridor's
    /// roof over the vault engages nothing (the vault's area is the corridor band);
    /// in the corridor it engages the vault, which holds after its ward detail is
    /// down until the hieroglyph seal is solved, then opens both barriers.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task TheVaultClearsOnlyOnceItsSealIsSolvedAsWellAsItsWardDown() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new Level08Fixture(null);
        Level08Controller level = fixture.Level;
        PlayerController player = level.Player;
        try {
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            DialogueManager dialogue = DialogueManager.Instance;
            for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
            AssertThat(tree.Paused).IsFalse();
            EncounterLock vault = level.VaultLock;

            // On the corridor roof over the vault: no lock engages.
            player.GlobalPosition = new Vector2(7500f, 1000f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(vault.State).IsEqual(EncounterLockState.Armed);
            AssertObject(level.ActiveEncounterLock).IsNull();

            // In the corridor: the vault engages and shuts its entry.
            player.GlobalPosition = new Vector2(7500f, Level08Controller.TombFloorY - 1f);
            player.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(vault.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(vault.EntryBarrier.IsClosed).IsTrue();
            AssertThat(vault.LivingMemberCount).IsGreater(0);

            foreach (EnemyController member in vault.LivingMembers) member.TakeDamage(99999);
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(vault.State)
                .OverrideFailureMessage("The vault must hold while the hieroglyph seal is unsolved.")
                .IsEqual(EncounterLockState.Engaged);

            AssertThat(level.SolveHieroglyphLockForTest()).IsTrue();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(vault.State).IsEqual(EncounterLockState.Cleared);
            AssertThat(vault.EntryBarrier.IsClosed).IsFalse();
            AssertThat(vault.ExitBarrier.IsClosed).IsFalse();
            AssertThat(level.VaultDoorOpen).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(level);
        }
    }

    // === Helpers ===

    private static float HalfWidth(Node2D zone) {
        var shape = zone.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        return shape?.Shape is RectangleShape2D rect ? rect.Size.X * 0.5f * Mathf.Abs(zone.Scale.X) : 0f;
    }

    private static float HalfHeight(Node2D zone) {
        var shape = zone.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        return shape?.Shape is RectangleShape2D rect ? rect.Size.Y * 0.5f * Mathf.Abs(zone.Scale.Y) : 0f;
    }

    private static IEnumerable<(string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[]> AllAuthoredWaves() {
        yield return Level08Controller.DuneSpawns;
        yield return Level08Controller.SiegeLineSpawns;
        yield return Level08Controller.TombSpawns;
        yield return Level08Controller.VaultSpawns;
    }

    private static IEnumerable<(string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)> AllAuthoredSpawns() {
        foreach (var wave in AllAuthoredWaves()) {
            foreach (var spawn in wave) yield return spawn;
        }
    }

    private static List<Node> Children(Node parent) {
        var collected = new List<Node>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) collected.Add(child);
        return collected;
    }

    /// <summary>
    /// Instantiates the authored scene in the runner tree against an optional scratch
    /// save, then hands back every shared singleton it touched. The persisted vault
    /// flag is forced off on setup and restored on dispose - it is authored
    /// <c>PersistCompletionToSave</c>, so one case solving the seal would otherwise
    /// open the vault for every later case (Wave A, L03). The gameplay pause is
    /// sampled and restored too: a leaked <c>SceneTree.Paused</c> freezes GdUnit's own
    /// transport node and hangs the session (CLAUDE.md failure signature 4).
    /// </summary>
    private sealed class Level08Fixture : IDisposable {
        public readonly Level08Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPuzzleCompleted;
        private readonly bool _originalPaused;

        public Level08Fixture(StorySaveData save) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            SessionData session = GameManager.Instance.CurrentSession;
            _originalSlot = session.ActiveSaveSlot;
            _originalCharacter = session.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            _originalPuzzleCompleted =
                SaveManager.Instance.IsPuzzleCompleted(Level08Controller.HieroglyphPuzzleID);

            session.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            session.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession = session;
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            SaveManager.Instance.SetPuzzleCompleted(Level08Controller.HieroglyphPuzzleID, false);

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level08Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SetPuzzleCompleted(
                Level08Controller.HieroglyphPuzzleID, _originalPuzzleCompleted);
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = _originalSlot;
            session.SelectedCharacterID = _originalCharacter;
            GameManager.Instance.CurrentSession = session;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
