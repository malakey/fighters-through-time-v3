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
/// Package 5 Wave C: Level 14 - Neo-Earth, the Apex Archive core laboratory.
///
/// What is pinned here is the contract the level cannot silently drift out of:
/// the manifest scene path and level id, the three checkpoint ids, the three-beat
/// dialogue set, the DUST_ECONOMY-locked 14/2/1/3 budget on a cultist-only roster
/// (Act III has no brainwashed locals), the three-phase Archive Prime fitting its
/// core - and both halves of the era identity as *mechanics*:
///
/// - every laser grid telegraphs before it fires, and every grid can actually be
///   walked by the slowest character in the roster (a grid with no solution is a
///   damage tax, not a puzzle);
/// - the anti-gravity containment pockets never overlap, every shaft is climbable
///   at its own pocket's scale and impossible without it, and no resume ever wakes
///   the player inside a live beam or a pocket that has changed under them.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level14ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_14_NeoEarth.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_14_dialogue.tres";
    private const int ScratchSlot = 2;

    /// <summary>The nine playable characters; every one of them has to fit the level.</summary>
    private static readonly string[] RosterIDs = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };

    // === Scene and identity ===

    [TestCase]
    public void TheAuthoredSceneLoadsInstantiatesAndFreesCleanly() {
        // Mirrors the smoke-test contract: instantiate without entering the tree, so
        // this proves the .tscn resolves its script and template instances without
        // also running the whole level build.
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level14Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();
        AssertObject(instance.GetNodeOrNull<PathMovingPlatform>("BulkheadCargoLift"))
            .OverrideFailureMessage("The bulkhead cargo lift is the only way back west; it must exist.")
            .IsNotNull();
        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheCampaignSlot() {
        var level = new Level14Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_14_neo_earth");
            AssertThat(level.Level).IsEqual(CampaignLevel.NeoEarth);
            AssertString(level.LevelTitleKey).IsEqual("neo_earth_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            // The scene must sit exactly where StoryManager routes the campaign.
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_14.entrance");
            AssertString(level.BossIntroDialogueID).IsEqual("level_14.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_14.exit");
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new NeoEarthFixture(null);
        AssertThat(Level14Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_14_neo_earth_checkpoint_{index}";
            AssertString(Level14Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    [TestCase]
    public void EveryRoomConfinesTheCameraToAtLeastTheReferenceViewport() {
        using var fixture = new NeoEarthFixture(null);
        AssertThat(fixture.Level.RoomTriggers.Count).IsGreater(3);
        foreach (RoomTransitionTrigger trigger in fixture.Level.RoomTriggers) {
            AssertThat(trigger.CameraBounds.Size.X)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines the camera below 1920 px wide.")
                .IsGreaterEqual(1920f);
            AssertThat(trigger.CameraBounds.Size.Y).IsGreaterEqual(1080f);
            AssertThat(trigger.CameraBounds.Position.X).IsGreaterEqual(-200f);
            AssertThat(trigger.CameraBounds.End.X).IsLessEqual(Level14Controller.LevelWidth + 200f);
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesTheFourAuthoredBeats() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_14");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        // Package 11 A6: the V7.5 Extraction Hall reveal fires on entering the
        // hall, before the final approach to the Forge core.
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_14.entrance", "level_14.extraction_hall",
            "level_14.boss_intro", "level_14.exit");
    }

    [TestCase]
    public void EveryDialogueLineSpeakerAndLevelKeyResolvesInTheEnglishTable() {
        HashSet<string> keys = LocalizationKeys();
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);

        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.LineKeys.Length).IsGreater(0);
            // A speaker/emotion array that drifts out of step silently mislabels lines.
            AssertThat(sequence.SpeakerNameKeys.Length).IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).IsEqual(sequence.LineKeys.Length);

            foreach (string key in sequence.LineKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Line key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
                AssertThat(key.StartsWith("dlg_l14_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "neo_earth_level_title",
            "neo_earth_room_breach_gallery", "neo_earth_room_containment_wing",
            "neo_earth_room_core_approach", "neo_earth_room_security_core",
            "neo_earth_objective_breach", "neo_earth_objective_containment",
            "neo_earth_objective_core_approach", "neo_earth_objective_security_core",
            "neo_earth_objective_defeat_boss", "neo_earth_objective_complete",
            "neo_earth_pocket_alpha", "neo_earth_pocket_beta", "neo_earth_pocket_gamma"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }

        // Every authored pocket label really is one of the localized keys above.
        foreach ((string id, Rect2 _, float _, string labelKey) in Level14Controller.ContainmentPockets) {
            AssertThat(keys.Contains(labelKey))
                .OverrideFailureMessage($"Pocket '{id}' names an unlocalized label key '{labelKey}'.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheExitBeatKeepsTheCradlesHeldAndHandsTheCampaignToTheFiringChannel() {
        // V7.5: severing a charged cradle consumes the captive, so breaking the
        // intake frees nobody. The exit has to say that out loud - the retired
        // copy claimed there was "nobody down there to bring home", which is the
        // exact opposite of the Extraction Cradles canon.
        string cradles = LocalizationValue("dlg_l14_exit_2").ToLowerInvariant();
        AssertString(cradles)
            .OverrideFailureMessage("The Bastion is full of captives; the exit must not say it is empty.")
            .Contains("cradles are still holding");
        AssertThat(cradles.Contains("nobody down there"))
            .OverrideFailureMessage("The retired 'nobody down there' line contradicts the cradles.")
            .IsFalse();

        // No mid-campaign power loss: the hero leaves carrying everything taken back.
        string carried = LocalizationValue("dlg_l14_exit_3").ToLowerInvariant();
        AssertString(carried).Contains("still carrying");

        // The only route to Level 15 is the Forge's own firing channel.
        string handoff = LocalizationValue("dlg_l14_exit_4").ToLowerInvariant();
        AssertString(handoff).Contains("forge is firing");
        AssertString(handoff).Contains("channel");
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedFourteenTwoOneThreeBudget() {
        AssertThat(Level14Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 14 is locked at 14 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(14);
        AssertThat(Level14Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 14 is locked at 2 elites by DUST_ECONOMY.md.")
            .IsEqual(2);
        AssertThat(Level14Controller.SpawnTable.Length).IsEqual(16);
        AssertThat(Level14Controller.ExtractorPlacements.Length)
            .OverrideFailureMessage("Level 14 is locked at 3 Chronal Extractors.")
            .IsEqual(3);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 _) in Level14Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }

        using var fixture = new NeoEarthFixture(null);
        AssertThat(fixture.Level.Extractors.Count).IsEqual(3);
    }

    [TestCase]
    public void TheGarrisonIsCultistOnlyBecauseActThreeHasNoLocalsToPressIntoService() {
        // The Apex Archive's home timeline: no era-altered locals, by design
        // (plan section 2.4). A future roster edit that mixes in a historical enemy
        // has to fail here.
        foreach ((string enemyID, int wave, Vector2 _) in Level14Controller.SpawnTable) {
            AssertThat(enemyID is Level14Controller.SlasherEnemyID
                            or Level14Controller.DroneEnemyID
                            or Level14Controller.EliteEnemyID)
                .OverrideFailureMessage($"'{enemyID}' is not a Future Cultist; Level 14 is cultist-only.")
                .IsTrue();
            AssertThat(wave >= 1 && wave <= 4).IsTrue();
        }

        var slasher = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level14Controller.SlasherEnemyID}.tres");
        var drone = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level14Controller.DroneEnemyID}.tres");
        var elite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level14Controller.EliteEnemyID}.tres");
        AssertObject(slasher).IsNotNull();
        AssertObject(drone).IsNotNull();
        AssertObject(elite).IsNotNull();
        AssertThat(slasher.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(drone.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(elite.Tier).IsEqual(EnemyTier.Elite);

        // The drone is the laboratory's own security unit, and it flies - which is
        // why it is the standard that belongs in a room full of vertical shafts.
        AssertThat(drone.Behavior)
            .OverrideFailureMessage("The hologram drone must stay a flyer; the level posts it in mid-air.")
            .IsEqual(DefaultBehavior.Flying);

        // Every airborne post really is airborne, and every grounded post is on the deck.
        foreach ((string enemyID, int _, Vector2 position) in Level14Controller.SpawnTable) {
            if (enemyID == Level14Controller.DroneEnemyID) continue;
            AssertFloat(position.Y)
                .OverrideFailureMessage($"Ground enemy '{enemyID}' is posted off the lab deck at {position}.")
                .IsEqualApprox(Level14Controller.DeckY - 50f, 1f);
        }
    }

    [TestCase]
    public void TheWholeGarrisonFitsInsideTheLevelPoolWarmCounts() {
        // level_14_pool_config warms 18 standard_enemy and 5 elite_enemy - the
        // largest standard budget in the campaign. Nothing is killed in a worst-case
        // run, so the whole level's authored total is the concurrency to check.
        AssertThat(Level14Controller.StandardEnemyCount)
            .OverrideFailureMessage("More standards authored than the pool warms.")
            .IsLessEqual(18);
        AssertThat(Level14Controller.EliteEnemyCount).IsLessEqual(5);

        var perWave = new Dictionary<int, int>();
        foreach ((string _, int wave, Vector2 _) in Level14Controller.SpawnTable) {
            perWave.TryGetValue(wave, out int running);
            perWave[wave] = running + 1;
        }
        AssertThat(perWave.Count).IsEqual(4);
    }

    // === Boss ===

    [TestCase]
    public void ArchivePrimeResolvesWithThreePhasesAndItsRangedBandFitsTheCore() {
        var boss = AuthoredResources.Load<BossData>(Level14Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("archive_prime");
        // Two thresholds = three phases, and the arena grid has a tuning per phase.
        AssertThat(boss.PhaseThresholds.Length)
            .OverrideFailureMessage("Archive Prime is authored with three phases.").IsEqual(2);
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

        // BossController converts range thresholds at 60 px per world unit; the core
        // has to be wide enough for the boss to actually use its ranged band.
        const float pixelsPerUnit = 60f;
        float arenaWidth = Level14Controller.ArenaEndX - Level14Controller.ArenaStartX;
        AssertThat(boss.RangedRangeThreshold * pixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {boss.RangedRangeThreshold * pixelsPerUnit} px does not fit a {arenaWidth} px arena.")
            .IsTrue();
        AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();
    }

    [TestCase]
    public void TheBossEncounterIsWiredIntoTheSecurityCore() {
        using var fixture = new NeoEarthFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("archive_prime");
        AssertFloat(encounter.Position.X).IsGreater(Level14Controller.ArenaStartX);
        AssertFloat(encounter.Position.X).IsLess(Level14Controller.ArenaEndX);
    }

    // === Era identity: the laser security grids ===

    [TestCase]
    public void TheAuthoredSceneBeamsMatchTheGridTablesPositionForPosition() {
        // The solvability proof is only worth anything if the table it reasons about
        // is the geometry the player actually walks (the Level 11 precedent).
        Node instance = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate();
        try {
            foreach (LaserGridPattern pattern in AllPatterns()) {
                foreach ((string beamName, float x, float _) in pattern.Beams) {
                    var beam = instance.GetNodeOrNull<StoryCyclicHazard>(beamName);
                    AssertObject(beam)
                        .OverrideFailureMessage($"Beam '{beamName}' of '{pattern.GridID}' is not in the scene.")
                        .IsNotNull();
                    AssertFloat(beam.Position.X)
                        .OverrideFailureMessage($"Beam '{beamName}' drifted from its authored grid position.")
                        .IsEqualApprox(x, 0.5f);
                    AssertFloat(beam.Position.Y).IsEqualApprox(Level14Controller.DeckY, 0.5f);
                }
            }

            // The Security Core grid stays dark until Archive Prime turns it on.
            foreach ((string beamName, float _, float _) in Level14Controller.ArenaGridPhase2.Beams) {
                AssertThat(instance.GetNodeOrNull<StoryCyclicHazard>(beamName).Enabled)
                    .OverrideFailureMessage($"Arena beam '{beamName}' must be authored disabled.")
                    .IsFalse();
            }
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheCorridorGridsEscalateAsThePlayerGoesDeeper() {
        LaserGridPattern[] grids = Level14Controller.CorridorGrids;
        AssertThat(grids.Length).IsEqual(3);
        for (int index = 1; index < grids.Length; index++) {
            AssertThat(grids[index].BeamCount)
                .OverrideFailureMessage(
                    $"'{grids[index].GridID}' has fewer beams than '{grids[index - 1].GridID}'; grids must deepen.")
                .IsGreater(grids[index - 1].BeamCount);
            AssertThat(grids[index].CycleSeconds < grids[index - 1].CycleSeconds)
                .OverrideFailureMessage(
                    $"'{grids[index].GridID}' runs no faster than '{grids[index - 1].GridID}'.")
                .IsTrue();
        }

        // Beam phase offsets step by exactly the time the reference walker spends
        // covering one beam spacing, which is what makes the travelling safe window
        // line up with a walking player at all.
        foreach (LaserGridPattern grid in grids) {
            for (int index = 1; index < grid.Beams.Length; index++) {
                AssertFloat(grid.Beams[index].X - grid.Beams[index - 1].X)
                    .OverrideFailureMessage($"'{grid.GridID}' beam spacing is not the authored stride.")
                    .IsEqualApprox(Level14Controller.BeamSpacing, 0.5f);
                AssertFloat(grid.Beams[index].PhaseOffsetSeconds - grid.Beams[index - 1].PhaseOffsetSeconds)
                    .IsEqualApprox(Level14Controller.BeamSpacing / Level14Controller.ReferenceWalkSpeed, 0.01f);
            }
        }
    }

    [TestCase]
    public void EveryGridLeavesASurvivablePathForTheSlowestCharacterInTheRoster() {
        // The reference speed is not a taste call: it is the slowest ground speed on
        // the roster, so re-tuning a character to be slower than Lincoln fails the
        // level instead of quietly making a corridor unwalkable.
        float slowest = float.MaxValue;
        string slowestID = "";
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            float speed = data.MaxMoveSpeed * 60f;
            if (speed >= slowest) continue;
            slowest = speed;
            slowestID = id;
        }
        AssertFloat(Level14Controller.ReferenceWalkSpeed)
            .OverrideFailureMessage(
                $"'{slowestID}' walks at {slowest} px/s but the grids are timed for " +
                $"{Level14Controller.ReferenceWalkSpeed} px/s.")
            .IsEqualApprox(slowest, 0.5f);

        foreach (LaserGridPattern pattern in AllPatterns()) {
            float entry = Level14Controller.FindSafeWalkEntryTime(pattern, slowest);
            AssertThat(entry >= 0f)
                .OverrideFailureMessage(
                    $"Grid '{pattern.GridID}' ({pattern.BeamCount} beams, {pattern.CycleSeconds}s cycle) " +
                    "has no entry beat that clears every beam - it is a damage tax, not a puzzle.")
                .IsTrue();

            // ...and the solution is real: every beam is provably dark while the
            // walker is inside its footprint.
            float approach = pattern.Beams[0].X - 400f;
            for (int index = 0; index < pattern.Beams.Length; index++) {
                float centre = entry + (pattern.Beams[index].X - approach) / slowest;
                AssertThat(Level14Controller.BeamIsActiveAt(pattern, index, centre))
                    .OverrideFailureMessage(
                        $"Grid '{pattern.GridID}' beam {index} is live when the walker is standing in it.")
                    .IsFalse();
            }
        }
    }

    [TestCase]
    public void EveryBeamTelegraphsBeforeItFiresAndTheTelegraphItselfIsFree() {
        foreach (LaserGridPattern pattern in AllPatterns()) {
            AssertThat(pattern.WarningSeconds >= Level14Controller.MinTelegraphSeconds)
                .OverrideFailureMessage(
                    $"Grid '{pattern.GridID}' telegraphs for only {pattern.WarningSeconds}s.")
                .IsTrue();
            AssertThat(pattern.ActiveSeconds < pattern.CooldownSeconds + pattern.WarningSeconds)
                .OverrideFailureMessage($"Grid '{pattern.GridID}' is live more often than it is safe.")
                .IsTrue();
        }

        // Reading the exports is not enough - a component that fired straight out of
        // cooldown would pass that. Drive the real cycle a frame at a time.
        using var fixture = new NeoEarthFixture(null);
        StoryCyclicHazard beam = fixture.Level.BeamsOf(Level14Controller.PerimeterGrid.GridID)[0];
        PlayerController player = fixture.Level.Player;
        AssertObject(beam).IsNotNull();
        AssertObject(player).IsNotNull();

        beam.ForcePhase(HazardPhase.Cooldown, 0.5f);
        AssertThat(beam.ApplyToPlayer(player))
            .OverrideFailureMessage("A beam in cooldown must not damage.").IsFalse();

        StepFrames(beam, 31);
        AssertThat(beam.Phase)
            .OverrideFailureMessage("A beam must warn before it fires, never jump cooldown -> active.")
            .IsEqual(HazardPhase.Warning);
        AssertThat(beam.ApplyToPlayer(player))
            .OverrideFailureMessage("The telegraph must be a free read.").IsFalse();

        StepFrames(beam, Mathf.FloorToInt(beam.WarningDuration * 60f) - 4);
        AssertThat(beam.Phase)
            .OverrideFailureMessage("The telegraph is shorter than the authored warning duration.")
            .IsEqual(HazardPhase.Warning);

        StepFrames(beam, 8);
        AssertThat(beam.Phase).IsEqual(HazardPhase.Active);
        AssertThat(beam.ApplyToPlayer(player))
            .OverrideFailureMessage("An active beam must damage.").IsTrue();
    }

    [TestCase]
    public void TheSecurityCoreGridAnswersToArchivePrimesPhasesAndGoesDarkWithIt() {
        using var fixture = new NeoEarthFixture(null);
        Level14Controller level = fixture.Level;
        IReadOnlyList<StoryCyclicHazard> beams = level.BeamsOf(Level14Controller.ArenaGridPhase2.GridID);
        AssertThat(beams.Count).IsEqual(4);

        // Phase 1 is a clean duel: the core has not started defending itself yet.
        AssertThat(level.ArenaGridArmedPhase).IsEqual(0);
        foreach (StoryCyclicHazard beam in beams) AssertThat(beam.Enabled).IsFalse();

        // BossController.CurrentPhase is 0-based, so 1 is the second phase.
        EventBus.Instance.RaiseBossPhaseChanged(1);
        AssertThat(level.ArenaGridArmedPhase).IsEqual(2);
        foreach (StoryCyclicHazard beam in beams) {
            AssertThat(beam.Enabled).IsTrue();
            AssertFloat(beam.CooldownDuration)
                .IsEqualApprox(Level14Controller.ArenaGridPhase2.CooldownSeconds, 0.001f);
        }

        EventBus.Instance.RaiseBossPhaseChanged(2);
        AssertThat(level.ArenaGridArmedPhase).IsEqual(3);
        foreach (StoryCyclicHazard beam in beams) {
            AssertFloat(beam.CooldownDuration)
                .IsEqualApprox(Level14Controller.ArenaGridPhase3.CooldownSeconds, 0.001f);
            AssertThat(beam.WarningDuration >= Level14Controller.MinTelegraphSeconds)
                .OverrideFailureMessage("Even the tightened arena grid has to stay readable.")
                .IsTrue();
        }

        // A phase never runs backwards, so a stray repeat cannot loosen the grid.
        EventBus.Instance.RaiseBossPhaseChanged(1);
        AssertThat(level.ArenaGridArmedPhase).IsEqual(3);

        // ...and with the core dead the grid is dead too, so the exit beat is safe.
        level.DisarmArenaGrid();
        AssertThat(level.ArenaGridArmedPhase).IsEqual(0);
        foreach (StoryCyclicHazard beam in beams) AssertThat(beam.Enabled).IsFalse();
    }

    [TestCase]
    public void ARewindRestoresTheGridPatternInsteadOfCollapsingEveryBeamOntoOnePhase() {
        using var fixture = new NeoEarthFixture(null);
        Level14Controller level = fixture.Level;
        IReadOnlyList<StoryCyclicHazard> beams = level.BeamsOf(Level14Controller.CoreGrid.GridID);
        AssertThat(beams.Count).IsEqual(5);

        // Scramble the grid the way a live run would, then rewind.
        foreach (StoryCyclicHazard beam in beams) beam.ForcePhase(HazardPhase.Active, 0.4f);
        EventBus.Instance.RaiseRewindTriggered(level.Player.Position);

        // The component's own reset would put every beam in the same phase, which
        // would destroy the travelling window the player is learning. The level
        // re-applies the authored offsets on top of it.
        for (int index = 0; index < beams.Count; index++) {
            AssertThat(beams[index].Phase)
                .OverrideFailureMessage("A rewound beam must restart from its authored cooldown offset.")
                .IsEqual(HazardPhase.Cooldown);
        }
        float entry = Level14Controller.FindSafeWalkEntryTime(
            Level14Controller.CoreGrid, Level14Controller.ReferenceWalkSpeed);
        AssertThat(entry >= 0f)
            .OverrideFailureMessage("The rewound grid must still be the solvable authored pattern.")
            .IsTrue();
    }

    // === Era identity: the anti-gravity containment pockets ===

    [TestCase]
    public void TheContainmentPocketsNeverOverlapAndAreAllRealLowGravity() {
        using var fixture = new NeoEarthFixture(null);
        Level14Controller level = fixture.Level;
        AssertThat(level.Pockets.Count).IsEqual(Level14Controller.ContainmentPockets.Length);
        AssertThat(level.Pockets.Count).IsEqual(3);

        float cursor = float.MinValue;
        for (int index = 0; index < Level14Controller.ContainmentPockets.Length; index++) {
            (string id, Rect2 area, float scale, string _) = Level14Controller.ContainmentPockets[index];
            GravityFieldZone pocket = level.Pockets[index];
            AssertObject(pocket).IsNotNull();
            AssertString(pocket.FieldID).IsEqual(id);
            AssertFloat(pocket.GravityScale).IsEqualApprox(scale, 0.0001f);

            // EnvironmentPlayerModifiers publishes the PRODUCT of every live source,
            // so two pockets over one player would multiply into a scale nobody
            // authored (Levels 5, 8 and 12 all hit this). Strictly disjoint in X.
            AssertThat(area.Position.X > cursor)
                .OverrideFailureMessage($"Pocket '{id}' overlaps the pocket before it at x={cursor}.")
                .IsTrue();
            cursor = area.End.X;

            // These are anti-gravity fields, not suppressors: a heavier-than-normal
            // pocket could drop a player into a shaft they cannot climb out of.
            AssertFloat(scale)
                .OverrideFailureMessage($"Pocket '{id}' is not low gravity.").IsLess(1f);
            AssertFloat(scale).IsGreater(0f);

            // Localized, not level-wide: this is the Level 12 contrast.
            AssertThat(area.Size.X)
                .OverrideFailureMessage($"Pocket '{id}' is wide enough to be a level-wide field.")
                .IsLess(Level14Controller.LevelWidth / 4f);
        }
    }

    [TestCase]
    public void EveryContainmentShaftIsClimbableAtItsOwnScaleAndImpossibleWithoutIt() {
        float bestSingleOnEarth = BestSingleJumpRise(1f);
        float bestTotalOnEarth = BestTotalJumpRise(1f);

        foreach ((string pocketID, float footY, (float X, float Y, float Width)[] rungs)
                 in Level14Controller.PocketShafts) {
            float scale = PocketScale(pocketID);
            Rect2 area = PocketArea(pocketID);
            float reach = HeaviestSingleJumpRise(scale);
            AssertThat(rungs.Length).IsGreater(0);

            float standing = footY;
            foreach ((float x, float y, float width) in rungs) {
                float rise = standing - y;

                // Reachable at THIS pocket's scale, by the heaviest character.
                AssertThat(rise <= reach)
                    .OverrideFailureMessage(
                        $"Shaft '{pocketID}' asks for a {rise} px rise at x={x}, but the heaviest character " +
                        $"only clears {reach} px at scale {scale}.")
                    .IsTrue();

                // ...and impossible for the MOST mobile character without the field,
                // so the pocket is the route rather than a convenience.
                AssertThat(rise > bestSingleOnEarth)
                    .OverrideFailureMessage(
                        $"Shaft '{pocketID}' rung at x={x} rises {rise} px, inside the best Earth-normal " +
                        $"jump ({bestSingleOnEarth} px) - the containment field is not load-bearing here.")
                    .IsTrue();

                AssertThat(area.HasPoint(new Vector2(x, y)))
                    .OverrideFailureMessage($"Shaft '{pocketID}' has a rung at ({x},{y}) outside its own field.")
                    .IsTrue();
                AssertThat(width).IsGreater(120f);
                standing = y;
            }

            // No multi-jump budget replaces the shaft either.
            float climb = footY - standing;
            AssertThat(climb > bestTotalOnEarth)
                .OverrideFailureMessage(
                    $"Shaft '{pocketID}' climbs {climb} px but the best Earth-normal multi-jump reaches " +
                    $"{bestTotalOnEarth} px.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheBreachGalleryScaffoldIsAnOrdinaryClimbSoTheContrastIsLegible() {
        // If every climb in the level needed a field, the fields would read as level
        // geometry rather than as a mechanic. The portal scaffold is deliberately
        // clearable by the heaviest character at Earth-normal gravity, and it stands
        // outside every pocket.
        float reach = HeaviestSingleJumpRise(1f);
        float standing = Level14Controller.DeckY;
        foreach ((float x, float y, float width) in Level14Controller.PortalScaffold) {
            float rise = standing - y;
            AssertThat(rise > 0f).IsTrue();
            AssertThat(rise <= reach)
                .OverrideFailureMessage(
                    $"The portal scaffold rung at x={x} rises {rise} px; the heaviest character clears {reach} px.")
                .IsTrue();
            foreach ((string id, Rect2 area, float _, string _) in Level14Controller.ContainmentPockets) {
                AssertThat(area.HasPoint(new Vector2(x, y)))
                    .OverrideFailureMessage($"The portal scaffold has drifted inside pocket '{id}'.")
                    .IsFalse();
            }
            standing = y;
        }
    }

    [TestCase]
    public void LowGravityIsAppliedInsideAPocketAndFullyReleasedOutsideIt() {
        using var fixture = new NeoEarthFixture(null);
        Level14Controller level = fixture.Level;
        PlayerController player = level.Player;
        AssertObject(player).IsNotNull();

        // The level opens on the lab deck, outside every pocket: Earth normal.
        AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);

        foreach ((string id, Rect2 area, float scale, string _) in Level14Controller.ContainmentPockets) {
            player.Position = area.Position + area.Size / 2f;
            level.SyncContainmentPocketToPlayer();
            AssertFloat(player.EnvironmentGravityScale)
                .OverrideFailureMessage($"Standing inside pocket '{id}' did not apply its scale.")
                .IsEqualApprox(scale, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player))
                .OverrideFailureMessage($"Exactly one pocket may own the player; '{id}' overlaps another.")
                .IsEqual(1);
        }

        // Stepping back onto the deck restores Earth normal exactly, with nothing
        // left over from the pocket that just released them.
        player.Position = new Vector2(260f, Level14Controller.DeckY - 60f);
        level.SyncContainmentPocketToPlayer();
        AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);

        // A Chronal Rewind teleports the player without a physics step in between,
        // so the level re-registers on the rewind event as well as on entry.
        Rect2 beta = PocketArea("level_14.containment_beta");
        player.Position = beta.Position + beta.Size / 2f;
        EventBus.Instance.RaiseRewindTriggered(player.Position);
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("A rewind into a containment pocket must restore its low gravity.")
            .IsEqualApprox(PocketScale("level_14.containment_beta"), 0.001f);

        foreach (GravityFieldZone pocket in level.Pockets) pocket.RemovePlayer(player);
        AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);
    }

    // === Resume safety ===

    [TestCase]
    public void NoCheckpointResumeWakesThePlayerInALiveBeamOrAStrandingPocket() {
        var anchors = new List<(string ID, Vector2 Position)>();
        using (var probe = new NeoEarthFixture(null)) {
            anchors.Add(("spawn", probe.Level.PlayerSpawnPosition));
            foreach (string checkpointID in Level14Controller.CheckpointIDs) {
                probe.Level.Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn);
                anchors.Add((checkpointID, respawn));
            }
        }

        float safeMargin = Level14Controller.BeamContactHalfWidth + 100f;
        foreach ((string id, Vector2 position) in anchors) {
            foreach (LaserGridPattern pattern in AllPatterns()) {
                foreach ((string beamName, float beamX, float _) in pattern.Beams) {
                    AssertThat(Mathf.Abs(position.X - beamX) > safeMargin)
                        .OverrideFailureMessage(
                            $"Respawn '{id}' at x={position.X} stands in (or beside) beam '{beamName}' at x={beamX}.")
                        .IsTrue();
                }
            }
            foreach ((string pocketID, Rect2 area, float _, string _) in Level14Controller.ContainmentPockets) {
                AssertThat(area.HasPoint(position))
                    .OverrideFailureMessage(
                        $"Respawn '{id}' sits inside containment pocket '{pocketID}'; a resumed player " +
                        "would wake in a gravity state the physics callbacks never announced.")
                    .IsFalse();
            }
        }
    }

    [TestCase]
    public void EveryCheckpointResumesEarthNormalWithItsEarlierWavesAlreadyCleared() {
        foreach (string checkpointID in Level14Controller.CheckpointIDs) {
            using var fixture = new NeoEarthFixture(new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.NeoEarth),
                LastCheckpointID = checkpointID,
                CurrentHP = 70,
                CurrentUltimateMeter = 25f
            });
            Level14Controller level = fixture.Level;

            AssertThat(level.ResumedMidLevel)
                .OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            // No checkpoint stands in a pocket, so a resume is Earth-normal with no
            // stale source hanging off the player - the frame-zero registration ran.
            AssertObject(level.PocketAt(level.Player.Position))
                .OverrideFailureMessage($"'{checkpointID}' resumes inside a containment pocket.").IsNull();
            AssertFloat(level.Player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(level.Player)).IsEqual(0);

            // The arena grid never resumes armed: only Archive Prime turns it on.
            AssertThat(level.ArenaGridArmedPhase).IsEqual(0);

            if (checkpointID == Level14Controller.Checkpoint0) {
                AssertThat(level.HasWaveSpawned(2)).IsFalse();
            } else {
                AssertThat(level.HasWaveSpawned(1)).IsTrue();
                AssertThat(level.HasWaveSpawned(2)).IsTrue();
            }
            if (checkpointID == Level14Controller.Checkpoint2) AssertThat(level.HasWaveSpawned(3)).IsTrue();
            else AssertThat(level.HasWaveSpawned(3)).IsFalse();
            AssertThat(level.HasWaveSpawned(4)).IsFalse();
        }
    }

    [TestCase]
    public void TheBulkheadIsOnlyPassableOverTheTopAndTheLiftIsTheWayBack() {
        // The sealed bulkhead is what makes the beta containment pocket the route
        // rather than a detour, and the cargo lift is what keeps the wing's own
        // extractor reachable after the player has crossed.
        using var fixture = new NeoEarthFixture(null);
        int bulkheadWalls = 0;
        Godot.Collections.Array<Node> children = fixture.Level.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is StaticBody2D wall &&
                Mathf.Abs(wall.Position.X - Level14Controller.BulkheadX) < 1f) bulkheadWalls++;
        }
        AssertThat(bulkheadWalls)
            .OverrideFailureMessage("The containment wing must be sealed by a bulkhead at deck level.")
            .IsEqual(1);

        var lift = fixture.Level.GetNodeOrNull<PathMovingPlatform>("BulkheadCargoLift");
        AssertObject(lift).IsNotNull();
        AssertThat(lift.Waypoints.Length).IsEqual(2);
        float travel = Mathf.Abs(lift.Waypoints[1].Y - lift.Waypoints[0].Y);
        AssertThat(travel >= Level14Controller.DeckY - Level14Controller.HighDeckY - 40f)
            .OverrideFailureMessage($"The cargo lift only travels {travel} px; it must reach the high deck.")
            .IsTrue();
        // It has to be east of the bulkhead, or it would be a way to skip the pocket.
        AssertFloat(lift.Position.X).IsGreater(Level14Controller.BulkheadX);
    }

    // === Helpers ===

    private static IEnumerable<LaserGridPattern> AllPatterns() {
        foreach (LaserGridPattern pattern in Level14Controller.CorridorGrids) yield return pattern;
        yield return Level14Controller.ArenaGridPhase2;
        yield return Level14Controller.ArenaGridPhase3;
    }

    private static void StepFrames(StoryCyclicHazard beam, int frames) {
        for (int frame = 0; frame < frames; frame++) beam._PhysicsProcess(1.0 / 60.0);
    }

    private static float PocketScale(string pocketID) {
        foreach ((string id, Rect2 _, float scale, string _) in Level14Controller.ContainmentPockets) {
            if (id == pocketID) return scale;
        }
        return 1f;
    }

    private static Rect2 PocketArea(string pocketID) {
        foreach ((string id, Rect2 area, float _, string _) in Level14Controller.ContainmentPockets) {
            if (id == pocketID) return area;
        }
        return new Rect2();
    }

    /// <summary>
    /// Closed-form jump apex for PlayerController's authored physics (the Level 12
    /// derivation): PerformJump sets vy = -MaxJumpForce * 54, ApplyGravity
    /// accelerates at BaseGravity(18) * (0.8 + 0.4 * Weight) * EnvironmentGravityScale
    /// * 60 px/s^2 while the jump is held, so a held full jump rises v^2 / 2a.
    /// </summary>
    private static float SingleJumpRise(CharacterData data, float gravityScale) {
        float launch = data.MaxJumpForce * 54f;
        float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f * gravityScale;
        return launch * launch / (2f * acceleration);
    }

    private static float HeaviestSingleJumpRise(float gravityScale) {
        float lowest = float.MaxValue;
        foreach (string id in RosterIDs) {
            lowest = Mathf.Min(lowest, SingleJumpRise(LoadCharacter(id), gravityScale));
        }
        return lowest;
    }

    private static float BestSingleJumpRise(float gravityScale) {
        float best = 0f;
        foreach (string id in RosterIDs) {
            best = Mathf.Max(best, SingleJumpRise(LoadCharacter(id), gravityScale));
        }
        return best;
    }

    private static float BestTotalJumpRise(float gravityScale) {
        float best = 0f;
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            best = Mathf.Max(best, SingleJumpRise(data, gravityScale) * Mathf.Max(1, data.MaxJumpCount));
        }
        return best;
    }

    private static CharacterData LoadCharacter(string characterID) {
        var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
        AssertObject(data).OverrideFailureMessage($"Character '{characterID}' did not resolve.").IsNotNull();
        return data;
    }

    private static HashSet<string> LocalizationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        AssertThat(keys.Count).OverrideFailureMessage("localization/en.csv did not read.").IsGreater(500);
        return keys;
    }

    private static string LocalizationValue(string key) {
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0 && line[..comma] == key) return line[(comma + 1)..];
        }
        return "";
    }

    /// <summary>
    /// Builds the authored scene in the runner tree against an optional scratch
    /// save, then hands every shared singleton back untouched: session slot,
    /// character, save row, pooled enemies, and the gameplay pause flag. A leaked
    /// pause would freeze GdUnit's own transport node (CLAUDE.md signature 4).
    /// </summary>
    private sealed class NeoEarthFixture : IDisposable {
        public readonly Level14Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public NeoEarthFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level14Controller>();
            Level.Name = "Level_14_NeoEarth_Test";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            // Pooled enemies are children of the level; hand them back before the
            // level frees them, or the pool keeps freed nodes in its Active list.
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
