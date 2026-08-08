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
            "Level 8 is authored as four camera-confined rooms across two zones.").IsEqual(4);

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
            AssertThat(extractor.DustReward).IsEqual(15);
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
            "Level 8 ships four beats: entrance, boss_intro, postboss, exit.").IsEqual(4);

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
            "level_08.entrance", "level_08.boss_intro", "level_08.postboss", "level_08.exit" }) {
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

        // The authored Cleopatra scene is a canonical campaign beat: four lines,
        // alternating Cleopatra and the player (design-godot.md 3368-3374).
        DialogueSequenceData postBoss = set.Find("level_08.postboss");
        AssertThat(postBoss.LineCount).IsEqual(4);
        AssertString(postBoss.GetSpeakerKey(0)).IsEqual("speaker_cleopatra");
        AssertString(postBoss.GetSpeakerKey(1)).IsEqual("speaker_player");
        AssertString(postBoss.GetSpeakerKey(2)).IsEqual("speaker_cleopatra");
        AssertString(postBoss.GetSpeakerKey(3)).IsEqual("speaker_player");

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
