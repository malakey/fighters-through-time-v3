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
/// Package 5 Wave B: content contracts for Level 9 - Berlin, 1961.
/// <para>
/// Covers the per-level gate from docs/PACKAGE5_CAMPAIGN_PLAN.md §5: the scene
/// resolves at the exact <see cref="StoryManager"/> path, the controller reports
/// the manifest level id and its three checkpoints, the dialogue set and every
/// authored line key are localized, the authored encounter table matches the
/// locked 12/1/1/3 economy row, the Iron Chancellor's ranged band fits the bunker
/// street, and both halves of the era identity are driven for real - a beam that
/// strikes only after its grace period, and a surveillance relay that permanently
/// darkens the beam it feeds.
/// </para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level09ContentTests {
    private const string ScenePath = "res://scenes/campaign/Level_09_Berlin.tscn";
    private const string LevelID = "level_09_berlin";
    private const string DialoguePath = "res://resources/Dialogue/level_09_dialogue.tres";
    private const string BossPath = "res://resources/Bosses/iron_chancellor.tres";
    private const string FeedPuzzleID = "level_09.cut_feeds";

    /// <summary>Story world units to pixels; matches BossController.PixelsPerUnit.</summary>
    private const float PixelsPerUnit = 60f;

    private const int ScratchSlot = 2;

    // === Scene and identity ===

    [TestCase]
    public void TheBerlinSceneLoadsInstantiatesAndFreesCleanly() {
        // StoryManager must route here; a renamed scene silently breaks the campaign.
        AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Berlin)).IsEqual(ScenePath);

        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).OverrideFailureMessage($"{ScenePath} did not load.").IsNotNull();

        var instance = scene.Instantiate<Level09Controller>();
        AssertObject(instance).IsNotNull();
        AssertString(instance.LevelID).IsEqual(LevelID);
        instance.Free();
    }

    [TestCase]
    public void TheControllerReportsTheManifestIdentityAndRegistersExactlyThreeCheckpoints() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        AssertString(level.LevelID).IsEqual(LevelID);
        AssertThat(level.Level).IsEqual(CampaignLevel.Berlin);
        AssertString(level.LevelTitleKey).IsEqual("berlin_level_title");
        AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
        AssertString(level.DialoguePrefix).IsEqual("level_09");
        AssertString(level.EntranceDialogueID).IsEqual("level_09.entrance");
        AssertString(level.BossIntroDialogueID).IsEqual("level_09.boss_intro");
        AssertString(level.ExitDialogueID).IsEqual("level_09.exit");

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
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        AssertThat(Mathf.IsEqualApprox(level.LevelBounds.Size.X, Level09Controller.LevelWidth)).IsTrue();
        // Agent brief: 9,000-11,000 px, Florence scale.
        AssertThat(level.LevelBounds.Size.X >= 9000f && level.LevelBounds.Size.X <= 11000f).IsTrue();
        // Taller than the 1080 reference so the guard towers are real verticality.
        AssertThat(level.LevelBounds.Size.Y > 1080f).IsTrue();
        AssertThat(level.RoomTriggers.Count).IsEqual(4);

        foreach (RoomTransitionTrigger room in level.RoomTriggers) {
            AssertThat(string.IsNullOrWhiteSpace(room.RoomID)).IsFalse();
            // A camera window narrower than the reference viewport is a useless clamp.
            AssertThat(room.CameraBounds.Size.X >= 1920f)
                .OverrideFailureMessage($"Room '{room.RoomID}' confines to {room.CameraBounds.Size.X} px.").IsTrue();
            AssertThat(room.CameraBounds.Size.Y >= 1080f).IsTrue();
            AssertThat(level.LevelBounds.Encloses(room.CameraBounds))
                .OverrideFailureMessage($"Room '{room.RoomID}' escapes the level bounds.").IsTrue();
            // A1 back-fills CameraPath; an unlinked room silently loses confinement.
            AssertThat(room.CameraPath != null && !room.CameraPath.IsEmpty).IsTrue();
            AssertThat(room.GetNodeOrNull<StoryCameraConfiner>(room.CameraPath) == level.Camera).IsTrue();
        }
    }

    [TestCase]
    public void TheGuardTowerClimbsAreRealOneWayVerticality() {
        using var fixture = new BerlinFixture();

        // Indexed rather than GetChildren(): an engine-returned Godot array left to
        // the finalizer is exactly the teardown hazard AGENTS.md warns about.
        var oneWays = new List<OneWayPlatform>();
        for (int index = 0; index < fixture.Level.GetChildCount(); index++) {
            if (fixture.Level.GetChild(index) is OneWayPlatform platform) oneWays.Add(platform);
        }

        int watchtowerRungs = 0;
        int radarMastRungs = 0;
        float highest = float.MaxValue;
        foreach (OneWayPlatform platform in oneWays) {
            AssertThat(platform.CollisionLayer).IsEqual(CollisionLayers.OneWayPlatform);
            highest = Mathf.Min(highest, platform.Position.Y);
            float x = platform.Position.X;
            if (x >= Level09Controller.Room2StartX && x < Level09Controller.Room3StartX) watchtowerRungs++;
            if (x >= Level09Controller.Room3StartX && x < Level09Controller.Room4StartX) radarMastRungs++;
        }

        // Two guard towers, both real drop-through climbs.
        AssertThat(watchtowerRungs)
            .OverrideFailureMessage($"The Death Strip watchtower only has {watchtowerRungs} rungs.")
            .IsGreaterEqual(3);
        AssertThat(radarMastRungs)
            .OverrideFailureMessage($"The radar mast only has {radarMastRungs} rungs.")
            .IsGreaterEqual(3);
        AssertThat(Level09Controller.GroundY - highest > 500f)
            .OverrideFailureMessage($"The highest drop-through rung is only at y={highest}; that is not a tower.")
            .IsTrue();

        // The two rooms that contain a climb use the full level height; the flat
        // rooms confine to the 1080 reference band.
        int fullHeightRooms = 0;
        foreach (RoomTransitionTrigger room in fixture.Level.RoomTriggers) {
            if (room.CameraBounds.Size.Y >= Level09Controller.LevelHeight) fullHeightRooms++;
        }
        AssertThat(fullHeightRooms).IsEqual(2);
    }

    // === Dialogue and localization ===

    [TestCase]
    public void TheDialogueSetResolvesAndEveryAuthoredKeyIsInTheEnglishTable() {
        var dialogue = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(dialogue).OverrideFailureMessage($"{DialoguePath} did not load.").IsNotNull();
        AssertString(dialogue.DialogueSetID).IsEqual("dialogue_level_09");

        HashSet<string> keys = EnglishKeys();
        string[] expectedSequences = { "level_09.entrance", "level_09.boss_intro", "level_09.exit" };
        AssertThat(dialogue.Sequences.Length).IsEqual(expectedSequences.Length);

        foreach (string dialogueID in expectedSequences) {
            DialogueSequenceData sequence = dialogue.Find(dialogueID);
            AssertObject(sequence).OverrideFailureMessage($"Sequence '{dialogueID}' missing.").IsNotNull();
            AssertThat(sequence.LineCount).IsGreater(0);
            AssertThat(sequence.LineKeys.Length).IsEqual(sequence.SpeakerNameKeys.Length);

            for (int index = 0; index < sequence.LineCount; index++) {
                string lineKey = sequence.GetLineKey(index);
                AssertThat(lineKey.StartsWith("dlg_l09_", StringComparison.Ordinal))
                    .OverrideFailureMessage($"'{lineKey}' does not follow the dlg_lNN_ convention.").IsTrue();
                AssertThat(keys.Contains(lineKey))
                    .OverrideFailureMessage($"Line key '{lineKey}' is not in localization/en.csv.").IsTrue();
                AssertThat(keys.Contains(sequence.GetSpeakerKey(index)))
                    .OverrideFailureMessage($"Speaker key '{sequence.GetSpeakerKey(index)}' is not in localization/en.csv.").IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryVisibleBerlinStringHasAnEnglishEntry() {
        HashSet<string> keys = EnglishKeys();
        string[] required = {
            "berlin_level_title",
            "berlin_room_checkpoint_charlie", "berlin_room_death_strip",
            "berlin_room_radar_yard", "berlin_room_bunker_street",
            "berlin_objective_infiltrate", "berlin_objective_exposed", "berlin_objective_cut_feeds",
            "berlin_objective_reach_bunker", "berlin_objective_defeat_boss", "berlin_objective_complete",
            "berlin_gate_locked", "speaker_iron_chancellor",
            // Reused shared keys the level depends on.
            "checkpoint", "campaign_level_berlin", "boss_iron_chancellor_name",
            "enemy_infrared_border_sentry_name", "enemy_neural_mech_walker_name"
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

    // === Locked encounter economy: 12 standards / 1 elite / 1 boss / 3 extractors ===

    [TestCase]
    public void TheAuthoredSpawnTableIsExactlyTwelveStandardsAndOneElite() {
        var byTier = new Dictionary<EnemyTier, int>();
        var byID = new Dictionary<string, int>(StringComparer.Ordinal);
        var perWave = new Dictionary<int, int>();

        foreach ((string enemyID, int wave, Vector2 _) in Level09Controller.SpawnTable) {
            EnemyData data = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
            AssertObject(data).OverrideFailureMessage($"Enemy resource missing for '{enemyID}'.").IsNotNull();
            AssertString(data.EnemyID).IsEqual(enemyID);
            byTier[data.Tier] = byTier.GetValueOrDefault(data.Tier) + 1;
            byID[enemyID] = byID.GetValueOrDefault(enemyID) + 1;
            perWave[wave] = perWave.GetValueOrDefault(wave) + 1;
        }

        // docs/DUST_ECONOMY.md level 9 row: S=12, E=1.
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Standard)).IsEqual(12);
        AssertThat(byTier.GetValueOrDefault(EnemyTier.Elite)).IsEqual(1);
        AssertThat(Level09Controller.StandardEnemyCount).IsEqual(12);
        AssertThat(Level09Controller.EliteEnemyCount).IsEqual(1);

        // Era roster: the Berlin standard mixed with the cultist standard, plus the
        // single Berlin elite. Nothing else.
        AssertThat(byID.Count).IsEqual(3);
        AssertThat(byID[Level09Controller.SentryEnemyID]).IsEqual(7);
        AssertThat(byID[Level09Controller.CultistEnemyID]).IsEqual(5);
        AssertThat(byID[Level09Controller.EliteEnemyID]).IsEqual(1);

        // Concurrency must fit level_09_pool_config: standard warm 14, elite warm 4.
        AssertThat(perWave.Count).IsEqual(3);
        foreach ((int wave, int count) in perWave) {
            AssertThat(count <= 14)
                .OverrideFailureMessage($"Wave {wave} spawns {count} bodies, past the warm cap.").IsTrue();
        }
    }

    [TestCase]
    public void ThreeChronalExtractorsArePlacedFromTheAuthoredTable() {
        AssertThat(Level09Controller.ExtractorPlacements.Length).IsEqual(3);

        using var fixture = new BerlinFixture();
        AssertThat(fixture.Level.Extractors.Count).IsEqual(3);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChronalExtractor extractor in fixture.Level.Extractors) {
            AssertThat(extractor.ObjectID.StartsWith("level_09.", StringComparison.Ordinal)).IsTrue();
            AssertThat(ids.Add(extractor.ObjectID))
                .OverrideFailureMessage($"Duplicate extractor id '{extractor.ObjectID}'.").IsTrue();
            // Dust stays resource-owned at the locked 15; level code must not override it.
            AssertThat(extractor.DustReward).IsEqual(15);
        }
    }

    [TestCase]
    public void TheIronChancellorIsWiredAndItsRangeBandFitsTheBunkerStreet() {
        var boss = AuthoredResources.Load<BossData>(BossPath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("iron_chancellor");
        AssertThat(boss.ChronalDustDrop).IsEqual(50);
        // The bunker defense battle: it never gets knocked off its feet.
        AssertThat(boss.IsKnockbackImmune).IsTrue();

        using var fixture = new BerlinFixture();
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertThat(encounter.Data == boss)
            .OverrideFailureMessage("The encounter must reuse the pinned authored BossData instance.").IsTrue();

        // Assert the band against the resource rather than a hardcoded width, so
        // re-tuning the boss cannot silently outgrow the arena.
        Rect2 arena = fixture.Level.RoomTriggers[3].CameraBounds;
        AssertThat(arena.Size.X > boss.RangedRangeThreshold * PixelsPerUnit)
            .OverrideFailureMessage(
                $"The bunker street is {arena.Size.X} px but the boss ranged band is " +
                $"{boss.RangedRangeThreshold * PixelsPerUnit} px.").IsTrue();
        AssertThat(arena.HasPoint(encounter.Position))
            .OverrideFailureMessage("The boss anchor sits outside the bunker-street camera bounds.").IsTrue();
    }

    // === Era mechanic 1: the delayed-strike stealth gauntlet ===

    [TestCase]
    public void TheSearchlightsAreDelayedStrikeBeamsWithDesyncedSweeps() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        // Berlin is the opposite mode from Paris: no meter drain, a drone strike.
        AssertThat(level.Searchlights.Count).IsEqual(3);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var periods = new HashSet<float>();
        foreach (SearchlightZone light in level.Searchlights) {
            AssertThat(light.Mode).IsEqual(SearchlightMode.DelayedStrike);
            AssertThat(light.Enabled).IsTrue();
            AssertFloat(light.ExposureGraceSeconds).IsEqualApprox(1.5f, 0.001f);
            AssertThat(light.StrikeDamage).IsGreater(0);
            AssertThat(light.CollisionMask).IsEqual(CollisionLayers.Player);
            AssertThat(ids.Add(light.SearchlightID))
                .OverrideFailureMessage($"Duplicate searchlight id '{light.SearchlightID}'.").IsTrue();
            AssertThat(periods.Add(light.SweepPeriodSeconds))
                .OverrideFailureMessage(
                    $"Two beams share a {light.SweepPeriodSeconds}s sweep period; the corridor " +
                    "needs desynced sweeps to be a timing problem.").IsTrue();
        }
    }

    [TestCase]
    public void ABeamStrikesOnlyAfterItsGracePeriodAndNeverInstantly() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;
        SearchlightZone beam = level.Searchlights[0];

        level.Player.RestoreStoryCheckpoint(level.Player.Position, level.Player.MaximumHP, 40f);
        int hpBefore = level.Player.CurrentHP;
        float meterBefore = level.Player.CurrentUltimateMeter;

        AssertThat(beam.AddPlayer(level.Player)).IsTrue();
        AssertThat(level.ExposureAlarmCount).IsEqual(1);
        // Entering is not a hit: the grace period is the whole mechanic.
        AssertThat(beam.StrikeCount).IsEqual(0);
        AssertThat(level.Player.CurrentHP).IsEqual(hpBefore);

        beam.TickExposure(0.5f);
        beam.TickExposure(0.9f);   // 1.4 s total, still inside the 1.5 s grace
        AssertFloat(beam.ExposureFor(level.Player)).IsEqualApprox(1.4f, 0.001f);
        AssertThat(beam.StrikeCount)
            .OverrideFailureMessage("The beam struck before its grace period elapsed.").IsEqual(0);
        AssertThat(level.Player.CurrentHP).IsEqual(hpBefore);

        beam.TickExposure(0.2f);   // 1.6 s: the drone fires
        AssertThat(beam.StrikeCount).IsEqual(1);
        AssertThat(level.Player.CurrentHP).IsEqual(hpBefore - beam.StrikeDamage);
        // Unlike Paris, a Berlin beam never touches the Ultimate meter.
        AssertFloat(level.Player.CurrentUltimateMeter).IsEqualApprox(meterBefore, 0.001f);

        // Leaving the beam ends the exposure clock outright.
        AssertThat(beam.RemovePlayer(level.Player)).IsTrue();
        int hpAfterStrike = level.Player.CurrentHP;
        beam.TickExposure(10f);
        AssertThat(beam.StrikeCount).IsEqual(1);
        AssertThat(level.Player.CurrentHP).IsEqual(hpAfterStrike);
    }

    [TestCase]
    public void EveryCheckpointAndCoverPocketSitsOutsideEverySweptBeam() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        var footprints = new List<Rect2>(level.SearchlightFootprints());
        AssertThat(footprints.Count).IsEqual(3);

        // A resume must never drop the player into a live beam.
        for (int index = 0; index < 3; index++) {
            string id = $"{LevelID}_checkpoint_{index}";
            AssertThat(level.Levels.TryGetCheckpointPosition(id, out Vector2 respawn)).IsTrue();
            foreach (Rect2 footprint in footprints) {
                AssertThat(footprint.HasPoint(respawn))
                    .OverrideFailureMessage(
                        $"Checkpoint '{id}' respawns at {respawn}, inside swept beam {footprint}.")
                    .IsFalse();
            }
        }

        // The cover pockets are only cover if the cones genuinely never reach them.
        foreach ((float centerX, float width) in Level09Controller.CoverPockets) {
            float left = centerX - width / 2f;
            float right = centerX + width / 2f;
            foreach (Rect2 footprint in footprints) {
                bool overlaps = right > footprint.Position.X && left < footprint.End.X;
                AssertThat(overlaps)
                    .OverrideFailureMessage(
                        $"Cover pocket [{left}, {right}] is swept by beam {footprint}; it is not cover.")
                    .IsFalse();
            }
        }
    }

    // === Era mechanic 2: cutting the surveillance feeds ===

    [TestCase]
    public void CuttingAFeedRelayPermanentlyDarkensTheBeamItPowers() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        AssertThat(level.FeedRelays.Count).IsEqual(3);
        AssertObject(level.FeedPuzzle).IsNotNull();
        AssertString(level.FeedPuzzle.PuzzleID).IsEqual(FeedPuzzleID);
        AssertThat(level.FeedPuzzle.PersistCompletionToSave)
            .OverrideFailureMessage("The gate condition must persist or a checkpoint resume soft-locks.").IsTrue();

        DestructibleBlock relay = level.FeedRelays[0];
        SearchlightZone beam = level.Searchlights[0];
        AssertThat(beam.Enabled).IsTrue();

        // The player is already caught in the beam when the relay blows.
        AssertThat(beam.AddPlayer(level.Player)).IsTrue();
        AssertThat(beam.TrackedPlayerCount).IsEqual(1);

        // Relays are cables, not walls: two hits.
        AssertThat(relay.HitsToBreak).IsEqual(2);
        relay.TakeEnvironmentDamage(999f);
        AssertThat(relay.IsDestroyed).IsFalse();
        AssertThat(beam.Enabled).IsTrue();

        relay.TakeEnvironmentDamage(999f);
        AssertThat(relay.IsDestroyed).IsTrue();

        AssertThat(beam.Enabled)
            .OverrideFailureMessage("Cutting the feed relay did not darken its searchlight.").IsFalse();
        AssertThat(beam.TrackedPlayerCount)
            .OverrideFailureMessage("A darkened beam must release the player it was counting down on.")
            .IsEqual(0);

        // A darkened beam can never strike again.
        int hpBefore = level.Player.CurrentHP;
        beam.TickExposure(30f);
        AssertThat(beam.StrikeCount).IsEqual(0);
        AssertThat(level.Player.CurrentHP).IsEqual(hpBefore);

        // The other beams are untouched: one relay, one light.
        AssertThat(level.Searchlights[1].Enabled).IsTrue();
        AssertThat(level.Searchlights[2].Enabled).IsTrue();

        AssertThat(level.FeedsCut).IsEqual(1);
        AssertThat(level.FeedPuzzle.IsConditionSatisfied("feed_west_cut")).IsTrue();
        AssertThat(level.FeedPuzzle.IsCompleted).IsFalse();
        AssertThat(level.RadarGateOpen).IsFalse();
    }

    [TestCase]
    public void CuttingAllThreeFeedsUnsealsTheRadarGate() {
        using var fixture = new BerlinFixture();
        Level09Controller level = fixture.Level;

        AssertThat(level.RadarGateOpen).IsFalse();
        AssertObject(level.GetNodeOrNull<StaticBody2D>("RadarGate")).IsNotNull();

        foreach (DestructibleBlock relay in level.FeedRelays) {
            relay.TakeEnvironmentDamage(999f);
            relay.TakeEnvironmentDamage(999f);
            AssertThat(relay.IsDestroyed).IsTrue();
        }

        AssertThat(level.FeedsCut).IsEqual(3);
        AssertThat(level.FeedPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RadarGateOpen).IsTrue();
        foreach (SearchlightZone light in level.Searchlights) AssertThat(light.Enabled).IsFalse();

        var gate = level.GetNodeOrNull<StaticBody2D>("RadarGate");
        AssertThat(gate == null || gate.IsQueuedForDeletion())
            .OverrideFailureMessage("The radar gate is still solid after every feed was cut.").IsTrue();
    }

    [TestCase]
    public void ResumingPastTheRadarGateDoesNotSoftLockOrLeaveALiveGauntlet() {
        // Checkpoint 2 stands east of the gate. A resume there must open the gate and
        // darken the strip whether or not the save still carries the puzzle flag.
        using var fixture = new BerlinFixture($"{LevelID}_checkpoint_2");
        Level09Controller level = fixture.Level;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual($"{LevelID}_checkpoint_2");
        AssertThat(level.RadarGateOpen).IsTrue();
        AssertThat(level.FeedsCut).IsEqual(3);
        foreach (SearchlightZone light in level.Searchlights) AssertThat(light.Enabled).IsFalse();

        var gate = level.GetNodeOrNull<StaticBody2D>("RadarGate");
        AssertThat(gate == null || gate.IsQueuedForDeletion())
            .OverrideFailureMessage("The radar gate is still solid after a pre-boss resume.").IsTrue();
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
    /// Builds the real Berlin scene in the runner tree against a scratch save, then
    /// hands every shared singleton back on dispose.
    /// <para>
    /// The fixture always resumes at a checkpoint. That is deliberate: a fresh entry
    /// defers the entrance dialogue, which is authored <c>PausesGameplay = true</c>,
    /// and a leaked <c>SceneTree.Paused</c> stops GdUnit4's transport node and hangs
    /// the whole session (docs/PACKAGE5_CAMPAIGN_PLAN.md §9). The pause flag is
    /// restored anyway. The scratch save also clears the persisted feed-puzzle flag,
    /// because <c>PersistCompletionToSave</c> is on and one case completing the puzzle
    /// would otherwise open the gate for every later case.
    /// </para>
    /// </summary>
    private sealed class BerlinFixture : IDisposable {
        public readonly Level09Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public BerlinFixture() : this(NewSave($"{LevelID}_checkpoint_0")) { }

        public BerlinFixture(string resumeCheckpointID) : this(NewSave(resumeCheckpointID)) { }

        public BerlinFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            PackedScene packed = ResourceLoader.Load<PackedScene>(ScenePath);
            Level = packed.Instantiate<Level09Controller>();
            Level.Name = "Level09BerlinFixture";
            tree.Root.AddChild(Level);
        }

        public static StorySaveData NewSave(string checkpointID) => new() {
            SelectedCharacterID = "einstein",
            CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Berlin),
            LastCheckpointID = checkpointID,
            CurrentHP = 100,
            CurrentUltimateMeter = 40f
        };

        public void Dispose() {
            // Redundant with StoryLevelControllerBase._ExitTree, but harmless and it
            // keeps the fixture honest if the level ever fails to enter the tree.
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
