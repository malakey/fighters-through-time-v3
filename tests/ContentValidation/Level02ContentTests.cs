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
/// Package 5 Wave A - Level 2, Orléans 1429. Pins the contracts the campaign flow
/// depends on: the scene lives at the exact path <c>StoryManager</c> routes to, the
/// controller reports the manifest identity and the three locked checkpoint IDs, the
/// authored encounter table matches the locked dust-economy row (8 standards /
/// 0 elites / 3 extractors), the era identity is mechanically real (two destructible
/// generators actually holding barriers up), and every authored string resolves.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level02ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_02_Orleans.tscn";
    private const int ScratchSlot = 2;

    // === Scene ===

    [TestCase]
    public void TheSceneLoadsAtTheRoutedPathInstantiatesAndFreesCleanly() {
        // The path is a hard contract: StoryManager.LevelScenePaths[2] names it and
        // refuses to change scenes when it cannot resolve.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Orleans)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level02Controller).IsTrue();
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndDialogueWiring() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual("level_02_orleans");
        AssertThat(level.Level).IsEqual(CampaignLevel.Orleans);
        AssertString(level.LevelTitleKey).IsEqual("orleans_level_title");
        AssertString(level.DialogueSetPath).IsEqual("res://resources/Dialogue/level_02_dialogue.tres");
        AssertString(level.EntranceDialogueID).IsEqual("level_02.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_02.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_02.exit");

        // The LevelManager node name is what CheckpointTrigger resolves by literal path.
        var manager = level.GetNodeOrNull<LevelManager>("LevelManager");
        AssertObject(manager).IsNotNull();
        AssertString(manager.LevelID).IsEqual("level_02_orleans");
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new Level02Fixture(null);
        foreach (string id in new[] {
            Level02Controller.Checkpoint0, Level02Controller.Checkpoint1, Level02Controller.Checkpoint2 }) {
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered with the LevelManager.")
                .IsTrue();
        }

        int checkpoints = 0;
        foreach (Node child in Children(fixture.Level)) {
            if (child is CheckpointTrigger) checkpoints++;
        }
        AssertThat(checkpoints).OverrideFailureMessage(
            "Level 2 must ship exactly three checkpoints (entry anchor, midpoint, pre-boss).")
            .IsEqual(3);
    }

    [TestCase]
    public void RoomConfinementCoversTheWholeLevelAndTheBossArenaFitsAViewport() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertThat(level.RoomTriggers.Count).OverrideFailureMessage(
            "Level 2 is authored as four camera-confined rooms.").IsEqual(4);
        foreach (RoomTransitionTrigger trigger in level.RoomTriggers) {
            // A1 back-fills CameraPath after the player spawns; without it room
            // confinement silently does nothing.
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' has no camera path.").IsTrue();
            AssertThat(trigger.CameraBounds.Size.X).OverrideFailureMessage(
                $"Room '{trigger.RoomID}' is narrower than the 1920 px reference viewport.")
                .IsGreaterEqual(1920f);
        }

        // Plan §5.1 sizes campaign levels at Florence scale.
        float width = level.LevelBounds.Size.X;
        AssertThat(width).IsEqual(Level02Controller.LevelWidth);
        AssertThat(width).IsGreaterEqual(9000f);
        AssertThat(width).IsLessEqual(11000f);
    }

    // === Era identity: kinetic shield generators ===

    [TestCase]
    public void TwoDestructibleGeneratorsHoldTheGateBarriersUpUntilTheyAreBroken() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        ShieldGeneratorTower[] towers = { level.ShieldTowerA, level.ShieldTowerB };
        float[] gates = { Level02Controller.GateAX, Level02Controller.GateBX };
        float[] anchors = { Level02Controller.TowerAX, Level02Controller.TowerBX };

        AssertThat(level.TowersDestroyed).IsEqual(0);
        for (int index = 0; index < towers.Length; index++) {
            ShieldGeneratorTower tower = towers[index];
            AssertObject(tower).OverrideFailureMessage(
                $"Shield generator {index} is missing from the scene.").IsNotNull();
            // The template's barrier hangs at a fixed local offset, so the generator
            // has to stand exactly that far west of the gate it seals.
            AssertThat(Mathf.IsEqualApprox(tower.Position.X, anchors[index])).OverrideFailureMessage(
                $"Generator {index} is at x={tower.Position.X}, but its barrier only covers " +
                $"gate x={gates[index]} when it stands at x={anchors[index]}.").IsTrue();
            AssertThat(tower.ActiveBarrierCount).OverrideFailureMessage(
                $"Generator {index} must hold a live barrier while it stands.").IsEqual(1);

            foreach (ForcefieldBarrier barrier in tower.Barriers()) {
                AssertThat(Mathf.IsEqualApprox(barrier.GlobalPosition.X, gates[index])).IsTrue();
                AssertThat(barrier.CollisionLayer).IsEqual(CollisionLayers.Environment);
            }
        }

        // Breaking a generator is how the player advances: the barrier must drop.
        level.ShieldTowerA.TakeEnvironmentDamage(level.ShieldTowerA.MaxHP);
        AssertThat(level.ShieldTowerA.IsDestroyed).IsTrue();
        AssertThat(level.ShieldTowerA.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.TowersDestroyed).IsEqual(1);

        level.ShieldTowerB.TakeEnvironmentDamage(level.ShieldTowerB.MaxHP);
        AssertThat(level.ShieldTowerB.ActiveBarrierCount).IsEqual(0);
        AssertThat(level.TowersDestroyed).IsEqual(Level02Controller.TowerCount);
    }

    [TestCase]
    public void TheMortarLanesAreTelegraphedCyclicHazards() {
        using var fixture = new Level02Fixture(null);
        var hazards = new List<StoryCyclicHazard>();
        foreach (Node child in Children(fixture.Level)) {
            if (child is StoryCyclicHazard hazard) hazards.Add(hazard);
        }

        AssertThat(hazards.Count).OverrideFailureMessage(
            "The siege needs an authored mortar lane, not a single emplacement.").IsGreaterEqual(4);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (StoryCyclicHazard hazard in hazards) {
            AssertThat(ids.Add(hazard.HazardID)).OverrideFailureMessage(
                $"Duplicate hazard id '{hazard.HazardID}'.").IsTrue();
            AssertThat(hazard.HazardID.StartsWith("level_02.")).IsTrue();
            // Warning frames are the whole point: a mortar with no telegraph is
            // unreactable, and the design calls for dashing past cannon fire.
            AssertThat(hazard.WarningDuration).IsGreater(0f);
            AssertThat(hazard.ActiveDuration).IsGreater(0f);
            AssertThat(hazard.CooldownDuration).IsGreater(hazard.ActiveDuration);
            AssertThat(hazard.Phase).IsEqual(HazardPhase.Cooldown);
        }
    }

    // === Locked encounter economy ===

    [TestCase]
    public void TheAuthoredEncounterTableMatchesTheLockedEconomyRow() {
        // docs/DUST_ECONOMY.md / DustEconomyTests row 2: 8 standards, 0 elites,
        // 1 boss, 3 extractors. These numbers are locked; content conforms to them.
        AssertThat(Level02Controller.AuthoredStandardCount).OverrideFailureMessage(
            "Level 2 authors exactly 8 standard enemies at Normal.").IsEqual(8);
        AssertThat(Level02Controller.AuthoredEliteCount).OverrideFailureMessage(
            "The locked row says E = 0, so Level 2 places NO elites.").IsEqual(0);
        AssertThat(Level02Controller.AuthoredExtractorCount).IsEqual(3);

        var eraRoster = new HashSet<string>(StringComparer.Ordinal) { "laser_archer", "chrono_slasher" };
        var extractorIDs = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, Vector2 _) in Level02Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id)).OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }

        foreach ((string enemyID, Vector2 _, Vector2? _, Vector2? _) in AllAuthoredSpawns()) {
            AssertThat(eraRoster.Contains(enemyID)).OverrideFailureMessage(
                $"'{enemyID}' is not on the Orléans roster (laser_archer + chrono_slasher).").IsTrue();
            EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).IsNotNull();
            AssertThat(data.Tier).OverrideFailureMessage(
                $"'{enemyID}' must be a Standard-tier enemy; Level 2 places no elites.")
                .IsEqual(EnemyTier.Standard);
        }
    }

    [TestCase]
    public void ThePlacedExtractorsAndBossEncounterMatchTheAuthoredTables() {
        using var fixture = new Level02Fixture(null);
        Level02Controller level = fixture.Level;

        AssertThat(level.Extractors.Count).IsEqual(Level02Controller.AuthoredExtractorCount);
        foreach (ChronalExtractor extractor in level.Extractors) {
            // 15 dust each is resource-owned (docs/DUST_ECONOMY.md) and must not be
            // re-specified by level code.
            AssertThat(extractor.DustReward).IsEqual(15);
        }

        AssertThat(level.BossEncounters.Count).IsEqual(1);
        AssertString(level.BossEncounters[0].Data.BossID).IsEqual("siegemaster_duke");

        // Live standards never exceed the biggest single wave, well inside the
        // level_02 pool config's standard_enemy warm count of 10.
        int largestWave = Math.Max(Level02Controller.Room1Spawns.Length,
            Math.Max(Level02Controller.Room2Spawns.Length, Level02Controller.Room3Spawns.Length));
        AssertThat(largestWave).IsLessEqual(10);
    }

    [TestCase]
    public void TheBossResourceResolvesAndItsRangedBandFitsTheArena() {
        BossData boss = FTT.Core.AuthoredResources.Load<BossData>(Level02Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("siegemaster_duke");
        AssertThat(boss.PhaseThresholds.Length).OverrideFailureMessage(
            "The Siegemaster Duke is authored as a two-phase fight.").IsEqual(1);

        // BossController converts range thresholds at 60 px per world unit; the
        // arena has to be wide enough for the 9.0 m band to be reachable.
        const float pixelsPerUnit = 60f;
        float rangedPixels = boss.RangedRangeThreshold * pixelsPerUnit;
        float arenaWidth = Level02Controller.LevelWidth - 8960f;
        AssertThat(arenaWidth).OverrideFailureMessage(
            $"Boss arena is {arenaWidth} px but the ranged band alone needs {rangedPixels} px.")
            .IsGreater(rangedPixels * 2f);
    }

    // === Localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsLocalized() {
        var set = FTT.Core.AuthoredResources.Load<DialogueSetData>(Level02Controller.DialogueResourcePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_02");
        AssertThat(set.Sequences.Length).IsEqual(3);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        TranslationServer.SetLocale("en");

        var required = new List<string> {
            "orleans_level_title", "orleans_room_vanguard", "orleans_room_siege_line",
            "orleans_room_battlement", "orleans_room_boss", "orleans_objective_advance",
            "orleans_objective_towers", "orleans_objective_breach", "orleans_objective_defeat_boss",
            "orleans_objective_complete", "orleans_gate_shielded"
        };

        foreach (string sequenceID in new[] { "level_02.entrance", "level_02.boss_intro", "level_02.exit" }) {
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

    private static IEnumerable<(string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)> AllAuthoredSpawns() {
        foreach (var spawn in Level02Controller.Room1Spawns) yield return spawn;
        foreach (var spawn in Level02Controller.Room2Spawns) yield return spawn;
        foreach (var spawn in Level02Controller.Room3Spawns) yield return spawn;
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
    /// save, then hands back every shared singleton it touched. The gameplay pause is
    /// sampled and restored too: a leaked <c>SceneTree.Paused</c> freezes GdUnit's own
    /// transport node and hangs the session (CLAUDE.md failure signature 4).
    /// </summary>
    private sealed class Level02Fixture : IDisposable {
        public readonly Level02Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public Level02Fixture(StorySaveData save) {
            var tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level02Controller>();
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
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
