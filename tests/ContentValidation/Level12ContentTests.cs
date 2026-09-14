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
/// Package 5 Wave B: Level 12 - the Lunar Landing, the Act II finale.
///
/// What is pinned here is the contract the level cannot silently drift out of:
/// the manifest scene path and level id, the three checkpoint ids, the four-beat
/// dialogue set (the extra `preboss` beat carries the design's authored Sarah
/// scene and the campaign's thesis), the DUST_ECONOMY-locked 14/2/1/4 encounter
/// budget, the three-phase Gravity Overseer fitting its pad - and the era
/// mechanic itself: gravity fields that tile the level without overlapping, low
/// gravity that is really applied and really released, a resume that wakes the
/// player in the right gravity state, and a traversal plan that low gravity makes
/// possible without letting anyone skip the spire.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level12ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_12_Lunar.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_12_dialogue.tres";
    private const int ScratchSlot = 2;

    /// <summary>
    /// Package 11 A6b: the roster is the content manifest, never a literal
    /// cast list. design-godot.md §2 forbids enumerating the cast in
    /// load-bearing ways, and a duplicated array here is exactly the thing
    /// that blocks a roster addition — the content would be complete and the
    /// test suite would still fail.
    /// </summary>
    private static readonly IReadOnlyList<string> RosterIDs = FTT.Core.CharacterRoster.IDs;

    // === Scene and identity ===

    [TestCase]
    public void TheAuthoredSceneLoadsInstantiatesAndFreesCleanly() {
        // Mirrors the smoke-test contract: instantiate without entering the tree,
        // so this proves the .tscn resolves its script and template instances
        // without also running the whole level build.
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level12Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();

        // Vacuum vents: the era's cyclic hazards, on the surface and the mast head.
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("VentRegolith")).IsNotNull();
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("VentCacheEast")).IsNotNull();
        AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>("VentMastHead")).IsNotNull();

        // High-altitude platforming: two ferries and two lifts.
        foreach (string name in new[] { "FerryMare", "LiftPocket", "LiftSpire", "FerryHighline" }) {
            AssertObject(instance.GetNodeOrNull<PathMovingPlatform>(name))
                .OverrideFailureMessage($"Authored moving platform '{name}' is missing.").IsNotNull();
        }
        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheCampaignSlot() {
        var level = new Level12Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_12_lunar");
            AssertThat(level.Level).IsEqual(CampaignLevel.Lunar);
            AssertString(level.LevelTitleKey).IsEqual("lunar_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            // The scene must sit exactly where StoryManager routes the campaign.
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Lunar)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_12.entrance");
            AssertString(level.PreBossDialogueID).IsEqual("level_12.preboss");
            AssertString(level.BossIntroDialogueID).IsEqual("level_12.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_12.exit");
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new LunarFixture(null);
        AssertThat(Level12Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_12_lunar_checkpoint_{index}";
            AssertString(Level12Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesAllFourBeatsIncludingThePreBossSequence() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_12");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        // preboss is the Act-finale extra beat (plan section 2.3: levels 5, 12, 15).
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_12.entrance", "level_12.preboss", "level_12.boss_intro", "level_12.exit");
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
                AssertThat(key.StartsWith("dlg_l12_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "lunar_level_title", "lunar_objective_reach_outpost", "lunar_objective_vent_field",
            "lunar_objective_climb_spire", "lunar_objective_landing_pad",
            "lunar_objective_defeat_boss", "lunar_objective_complete",
            "lunar_room_tranquility_base", "lunar_room_vent_field",
            "lunar_room_relay_spire", "lunar_room_landing_pad"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }
    }

    [TestCase]
    public void ThePreBossBeatIsTheAuthoredSarahSceneAndStillCarriesTheCampaignsThesis() {
        // design-godot.md section 16 (Level 12 pre-boss) is the authored V7.5
        // script: five lines - Player / Sarah / a narrated stage beat where the
        // nexus siphon dies and the trace completes / Sarah (Shocked) / Player
        // (Determined). This is the Act II "where" reveal, so its content is
        // pinned, not just its shape.
        DialogueSequenceData preboss = SequenceNamed("level_12.preboss");
        AssertObject(preboss).IsNotNull();
        AssertThat(preboss.LineKeys.Length).IsEqual(5);
        AssertThat(preboss.SpeakerNameKeys).ContainsExactly(
            "speaker_player", "speaker_sarah", "speaker_narration", "speaker_sarah", "speaker_player");
        AssertString(preboss.EmotionKeys[3]).IsEqual("emotion_shocked");
        AssertString(preboss.EmotionKeys[4]).IsEqual("emotion_determined");

        string sarahsTrace = LocalizationValue("dlg_l12_preboss_2").ToLowerInvariant();
        AssertString(sarahsTrace).Contains("siphon nexus");
        AssertString(sarahsTrace).Contains("trace");
        AssertString(sarahsTrace).Contains("flowing somewhere");

        // N04: the reveal names two captives through the substitution tokens and
        // states location, captivity and draining - never the Forge, the deficit
        // or the rewrite. NarrativeKnowledgeBoundaryTests pins the other half.
        string theReveal = LocalizationValue("dlg_l12_preboss_4");
        AssertString(theReveal).Contains("{CaptiveName1}");
        AssertString(theReveal).Contains("{CaptiveName2}");
        string revealLower = theReveal.ToLowerInvariant();
        AssertString(revealLower).Contains("fortress in the space between timelines");
        AssertString(revealLower).Contains("resonance signatures");
        AssertString(revealLower).Contains("draining");

        // It has to reach the player before the fight, so it cannot be wired to the
        // boss: the level arms it from a room trigger and the base class keeps its
        // own defeat -> exit chain intact.
        using var fixture = new LunarFixture(null);
        AssertThat(fixture.Level.PreBossBeatPlayed).IsFalse();
        AssertObject(fixture.Level.GetNodeOrNull<Area2D>("PreBossTrigger"))
            .OverrideFailureMessage("The pre-boss beat must be armed by a level trigger.").IsNotNull();
    }

    [TestCase]
    public void TheEntranceBeatPaysOffTheOrbitalRelayLeadPlantedOnTheTitanic() {
        // Level 5's exit hands the player "an orbital communications relay" as the
        // reason the campaign goes to the Moon. If this line stops acknowledging
        // it, the Act I hook lands nowhere.
        string briefing = LocalizationValue("dlg_l12_entrance_2").ToLowerInvariant();
        AssertString(briefing).Contains("relay");
        AssertString(briefing).Contains("titanic");
        AssertString(briefing).Contains("broadcast");
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedFourteenTwoOneFourBudget() {
        AssertThat(Level12Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 12 is locked at 14 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(14);
        AssertThat(Level12Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 12 is locked at 2 elites by DUST_ECONOMY.md.")
            .IsEqual(2);
        AssertThat(Level12Controller.SpawnTable.Length).IsEqual(16);
        AssertThat(Level12Controller.ExtractorPlacements.Length)
            .OverrideFailureMessage("Level 12 is locked at 4 Chronal Extractors.")
            .IsEqual(4);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 _) in Level12Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }
    }

    [TestCase]
    public void TheGarrisonMixesTheLunarDiggerWithCultistsAndEveryTierResolves() {
        foreach ((string enemyID, int wave, Vector2 _) in Level12Controller.SpawnTable) {
            AssertThat(enemyID is Level12Controller.DiggerEnemyID
                            or Level12Controller.CultistEnemyID
                            or Level12Controller.EliteEnemyID)
                .OverrideFailureMessage($"'{enemyID}' is not on the Lunar roster.").IsTrue();
            AssertThat(wave >= 1 && wave <= 4).IsTrue();
        }

        var digger = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level12Controller.DiggerEnemyID}.tres");
        var cultist = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level12Controller.CultistEnemyID}.tres");
        var elite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level12Controller.EliteEnemyID}.tres");
        AssertObject(digger).IsNotNull();
        AssertObject(cultist).IsNotNull();
        AssertObject(elite).IsNotNull();
        AssertThat(digger.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(cultist.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(elite.Tier).IsEqual(EnemyTier.Elite);

        // The digger's grav-beam is why the era enemy belongs on the Moon: it pulls.
        AssertObject(digger.PrimaryAttack).IsNotNull();
        AssertFloat(digger.PrimaryAttack.KnockbackForce.X)
            .OverrideFailureMessage("The vacuum digger's grav-beam must PULL (negative horizontal knockback).")
            .IsLess(0f);
    }

    [TestCase]
    public void EveryWaveFitsInsideTheLevelPoolWarmCounts() {
        // Warm counts are concurrency caps (plan section 2.5): standard_enemy 16,
        // elite_enemy 5 for level_12_lunar_pools.
        var standardsPerWave = new Dictionary<int, int>();
        var elitesPerWave = new Dictionary<int, int>();
        foreach ((string enemyID, int wave, Vector2 _) in Level12Controller.SpawnTable) {
            bool isElite = enemyID == Level12Controller.EliteEnemyID;
            Dictionary<int, int> table = isElite ? elitesPerWave : standardsPerWave;
            table.TryGetValue(wave, out int running);
            table[wave] = running + 1;
        }
        foreach (KeyValuePair<int, int> wave in standardsPerWave) {
            AssertThat(wave.Value)
                .OverrideFailureMessage($"Wave {wave.Key} spawns {wave.Value} standards at once, over the warm count.")
                .IsLessEqual(16);
        }
        foreach (KeyValuePair<int, int> wave in elitesPerWave) {
            AssertThat(wave.Value).IsLessEqual(5);
        }
    }

    // === Boss ===

    [TestCase]
    public void TheGravityOverseerResolvesWithThreePhasesAndItsRangedBandFitsThePad() {
        var boss = AuthoredResources.Load<BossData>(Level12Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("gravity_overseer");
        // Two thresholds = three phases: the campaign's first three-phase boss.
        AssertThat(boss.PhaseThresholds.Length)
            .OverrideFailureMessage("The Act II finale boss is authored with three phases.").IsEqual(2);
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

        // BossController converts range thresholds at 60 px per world unit; the
        // pad has to be wide enough for the boss to actually use its ranged band.
        const float pixelsPerUnit = 60f;
        float arenaWidth = Level12Controller.ArenaEndX - Level12Controller.ArenaStartX;
        AssertThat(boss.RangedRangeThreshold * pixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {boss.RangedRangeThreshold * pixelsPerUnit} px does not fit a {arenaWidth} px arena.")
            .IsTrue();
        AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();

        // The phase-3 gravity well pulls; it must never pull the player anywhere
        // unrecoverable, so the pad floor is unbroken and the pad runs at a firmer
        // gravity than the regolith.
        AssertFloat(Level12Controller.PadGravityScale)
            .IsGreater(Level12Controller.RegolithGravityScale);
        AssertFloat(Level12Controller.PadGravityScale).IsLess(1f);
    }

    [TestCase]
    public void TheBossEncounterIsWiredIntoTheLandingPad() {
        using var fixture = new LunarFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("gravity_overseer");
        AssertFloat(encounter.Position.X).IsGreater(Level12Controller.ArenaStartX);
        AssertFloat(encounter.Position.X).IsLess(Level12Controller.ArenaEndX);
    }

    // === Era identity: the low gravity ===

    [TestCase]
    public void TheGravityFieldsTileTheWholeLevelWithoutOverlapping() {
        using var fixture = new LunarFixture(null);
        Level12Controller level = fixture.Level;

        AssertThat(level.GravityFields.Count).IsEqual(Level12Controller.GravityFieldSpans.Length);
        AssertThat(level.GravityFields.Count).IsGreater(0);

        float cursor = 0f;
        for (int index = 0; index < Level12Controller.GravityFieldSpans.Length; index++) {
            (string id, float startX, float endX, float scale) = Level12Controller.GravityFieldSpans[index];
            GravityFieldZone field = level.GravityFields[index];
            AssertObject(field).IsNotNull();
            AssertString(field.FieldID).IsEqual(id);
            AssertFloat(field.GravityScale).IsEqualApprox(scale, 0.0001f);

            // EnvironmentPlayerModifiers publishes the PRODUCT of every live
            // source, so two gravity fields over one player would multiply into a
            // scale nobody authored. Contiguous and non-overlapping, exactly.
            AssertFloat(startX)
                .OverrideFailureMessage($"Gravity span '{id}' overlaps or leaves a gap at x={cursor}.")
                .IsEqualApprox(cursor, 0.0001f);
            AssertThat(endX > startX).IsTrue();
            cursor = endX;

            // Low gravity everywhere: no span may be Earth-normal or heavier.
            AssertFloat(scale)
                .OverrideFailureMessage($"Gravity span '{id}' is not low gravity.").IsLess(1f);
            AssertFloat(scale).IsGreater(0f);
        }
        AssertFloat(cursor)
            .OverrideFailureMessage("The gravity fields must cover the level end to end.")
            .IsEqualApprox(Level12Controller.LevelWidth, 0.0001f);

        // Every checkpoint and the fresh spawn resolve to exactly one field.
        var anchors = new List<float> { level.PlayerSpawnPosition.X };
        foreach (string checkpointID in Level12Controller.CheckpointIDs) {
            level.Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn);
            anchors.Add(respawn.X);
        }
        foreach (float x in anchors) {
            AssertObject(level.GravityFieldFor(x))
                .OverrideFailureMessage($"No gravity field covers x={x}.").IsNotNull();
        }
    }

    [TestCase]
    public void LowGravityIsAppliedToThePlayerAndRestoresToNormalWhenTheFieldsReleaseThem() {
        using var fixture = new LunarFixture(null);
        Level12Controller level = fixture.Level;
        PlayerController player = level.Player;
        AssertObject(player).IsNotNull();

        // The level opens with the player already on the Moon: the field is
        // registered explicitly rather than waiting for a physics frame.
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("Level 12 must open in low gravity.")
            .IsEqualApprox(Level12Controller.RegolithGravityScale, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player))
            .OverrideFailureMessage("Exactly one gravity field may own the player at a time.").IsEqual(1);

        // Walking onto the landing pad hands the player to the firmer field and to
        // that field only - never to both, which would multiply.
        player.Position = new Vector2(Level12Controller.ArenaStartX + 800f, Level12Controller.SurfaceY - 60f);
        level.SyncGravityFieldToPlayer();
        AssertFloat(player.EnvironmentGravityScale)
            .IsEqualApprox(Level12Controller.PadGravityScale, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

        // A Chronal Rewind teleports the player without a physics step in between;
        // the level re-registers so gravity always follows the body.
        player.Position = new Vector2(1200f, Level12Controller.SurfaceY - 60f);
        EventBus.Instance.RaiseRewindTriggered(player.Position);
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("A rewind back onto the regolith must restore lunar gravity.")
            .IsEqualApprox(Level12Controller.RegolithGravityScale, 0.001f);

        // Leaving every field restores Earth-normal exactly, with nothing left over.
        foreach (GravityFieldZone field in level.GravityFields) field.RemovePlayer(player);
        AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);
    }

    [TestCase]
    public void EveryCheckpointResumeWakesThePlayerInTheRightGravityState() {
        foreach (string checkpointID in Level12Controller.CheckpointIDs) {
            using var fixture = new LunarFixture(new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Lunar),
                LastCheckpointID = checkpointID,
                CurrentHP = 70,
                CurrentUltimateMeter = 25f
            });
            Level12Controller level = fixture.Level;

            AssertThat(level.ResumedMidLevel)
                .OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            // The whole point: a level designed around low gravity must never hand
            // a resumed player Earth-normal gravity (or the wrong field's scale).
            GravityFieldZone expected = level.GravityFieldFor(level.Player.Position.X);
            AssertObject(expected)
                .OverrideFailureMessage($"'{checkpointID}' resumes outside every gravity field.").IsNotNull();
            AssertFloat(level.Player.EnvironmentGravityScale)
                .OverrideFailureMessage($"'{checkpointID}' resumed at the wrong gravity.")
                .IsEqualApprox(expected.GravityScale, 0.001f);
            AssertFloat(level.Player.EnvironmentGravityScale).IsLess(1f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(level.Player)).IsEqual(1);

            // Waves behind the checkpoint stay cleared instead of replaying, and the
            // thesis beat is still armed no matter where the run resumes.
            if (checkpointID == Level12Controller.Checkpoint0) {
                AssertThat(level.HasWaveSpawned(2)).IsFalse();
            } else {
                AssertThat(level.HasWaveSpawned(1)).IsTrue();
                AssertThat(level.HasWaveSpawned(2)).IsTrue();
            }
            if (checkpointID == Level12Controller.Checkpoint2) {
                AssertThat(level.HasWaveSpawned(3)).IsTrue();
            }
            AssertThat(level.PreBossBeatPlayed).IsFalse();
        }
    }

    // === Era identity: what low gravity does to traversal ===

    [TestCase]
    public void EveryAuthoredRungIsReachableOnTheMoonAndWouldNotBeOnEarth() {
        float heaviestLunarJump = float.MaxValue;
        float heaviestEarthJump = float.MaxValue;
        string heaviest = "";
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            float lunar = SingleJumpRise(data, Level12Controller.RegolithGravityScale);
            if (lunar >= heaviestLunarJump) continue;
            heaviestLunarJump = lunar;
            heaviestEarthJump = SingleJumpRise(data, 1f);
            heaviest = id;
        }
        AssertThat(heaviest).IsNotEqual("");

        float tallestRung = 0f;
        foreach ((string ladderID, (float X, float Y, float Width)[] rungs) in Level12Controller.Ladders) {
            AssertThat(rungs.Length).IsGreater(0);
            float standingY = Level12Controller.SurfaceY;
            foreach ((float x, float y, float width) in rungs) {
                float rise = standingY - y;
                AssertThat(rise >= 0f)
                    .OverrideFailureMessage($"Ladder '{ladderID}' descends at x={x}; rungs must climb.").IsTrue();
                AssertThat(rise <= heaviestLunarJump)
                    .OverrideFailureMessage(
                        $"Ladder '{ladderID}' asks for a {rise} px rise at x={x}, but '{heaviest}' " +
                        $"only clears {heaviestLunarJump} px on the Moon.")
                    .IsTrue();
                AssertThat(width).IsGreater(120f);
                tallestRung = Mathf.Max(tallestRung, rise);
                standingY = y;
            }
        }

        // Low gravity is the mechanic, not decoration: the same climb is impossible
        // for the heaviest character at Earth-normal gravity.
        AssertThat(tallestRung > heaviestEarthJump)
            .OverrideFailureMessage(
                $"The tallest rung is {tallestRung} px and '{heaviest}' clears {heaviestEarthJump} px on " +
                "Earth - low gravity is not load-bearing here.")
            .IsTrue();

        // The boss pad runs firmer, so its gantries are sized against that scale.
        float padJump = heaviestLunarJump * Level12Controller.RegolithGravityScale / Level12Controller.PadGravityScale;
        AssertThat(170f <= padJump)
            .OverrideFailureMessage($"The pad gantries out-reach '{heaviest}' under the pad dampers.").IsTrue();
    }

    [TestCase]
    public void NeitherLiftCanBeJumpedAndTheSpireLiftCannotBeSkippedAtAll() {
        float bestSingleJump = 0f;
        float bestTotalJump = 0f;
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            float single = SingleJumpRise(data, Level12Controller.RegolithGravityScale);
            bestSingleJump = Mathf.Max(bestSingleJump, single);
            bestTotalJump = Mathf.Max(bestTotalJump, single * Mathf.Max(1, data.MaxJumpCount));
        }

        foreach ((string platformID, float bottomY, float topY) in Level12Controller.Lifts) {
            float rise = bottomY - topY;
            AssertThat(rise > bestSingleJump)
                .OverrideFailureMessage(
                    $"Lift '{platformID}' rises {rise} px, inside the best single lunar jump ({bestSingleJump} px).")
                .IsTrue();
        }

        // The mast lift is the critical path, so no jump budget at all replaces it.
        float spireRise = 0f;
        foreach ((string platformID, float bottomY, float topY) in Level12Controller.Lifts) {
            if (platformID == Level12Controller.SpireLiftID) spireRise = bottomY - topY;
        }
        AssertThat(spireRise > bestTotalJump)
            .OverrideFailureMessage(
                $"The spire lift rises {spireRise} px but the best multi-jump reaches {bestTotalJump} px.")
            .IsTrue();
    }

    [TestCase]
    public void TheOutpostCurtainCanOnlyBePassedOnTheHighline() {
        float bestTotalJump = 0f;
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            bestTotalJump = Mathf.Max(bestTotalJump,
                SingleJumpRise(data, Level12Controller.RegolithGravityScale) * Mathf.Max(1, data.MaxJumpCount));
        }

        // The curtain is a slot, not a wall with a top: the only opening sits at the
        // highline altitude and is out of reach of every jump budget from the
        // regolith, so the spire climb cannot be bypassed no matter how mobile the
        // character is.
        AssertThat(Level12Controller.CurtainSlotRise > bestTotalJump)
            .OverrideFailureMessage(
                $"The curtain slot floor needs a {Level12Controller.CurtainSlotRise} px rise but the best " +
                $"multi-jump reaches {bestTotalJump} px.")
            .IsTrue();

        // ...and the ferry deck really does fit through the slot with the player on
        // it (the player body is 64 px tall and the deck is 32 px thick).
        const float deckHalfThickness = 16f;
        const float playerHeight = 64f;
        float feetY = Level12Controller.HighDeckY - deckHalfThickness;
        AssertThat(feetY < Level12Controller.CurtainSlotBottomY)
            .OverrideFailureMessage("The highline deck does not pass through the curtain slot.").IsTrue();
        AssertThat(feetY - playerHeight > Level12Controller.CurtainSlotTopY)
            .OverrideFailureMessage("A rider on the highline clips the curtain's upper segment.").IsTrue();

        using var fixture = new LunarFixture(null);
        // Both curtain segments are authored, so nobody can quietly delete one.
        int curtainWalls = 0;
        Godot.Collections.Array<Node> children = fixture.Level.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is StaticBody2D wall &&
                Mathf.Abs(wall.Position.X - Level12Controller.CurtainWallX) < 1f) curtainWalls++;
        }
        AssertThat(curtainWalls)
            .OverrideFailureMessage("The curtain must be two segments with a cargo slot between them.")
            .IsEqual(2);
    }

    [TestCase]
    public void EveryRoomConfinesTheCameraToAtLeastTheReferenceViewport() {
        using var fixture = new LunarFixture(null);
        AssertThat(fixture.Level.RoomTriggers.Count).IsGreater(3);
        foreach (RoomTransitionTrigger trigger in fixture.Level.RoomTriggers) {
            AssertThat(trigger.CameraBounds.Size.X)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines the camera below 1920 px wide.")
                .IsGreaterEqual(1920f);
            AssertThat(trigger.CameraBounds.Size.Y).IsGreaterEqual(1080f);
            // Rooms narrow the level; none of them may reach outside it.
            AssertThat(trigger.CameraBounds.Position.X).IsGreaterEqual(-200f);
            AssertThat(trigger.CameraBounds.End.X).IsLessEqual(Level12Controller.LevelWidth + 200f);
        }
    }

    // === Helpers ===

    /// <summary>
    /// Closed-form jump apex for PlayerController's authored physics:
    /// PerformJump sets vy = -MaxJumpForce * 54, ApplyGravity accelerates at
    /// BaseGravity(18) * (0.8 + 0.4 * Weight) * EnvironmentGravityScale * 60 px/s^2
    /// while the jump is held. A held full jump therefore rises v^2 / 2a.
    /// </summary>
    private static float SingleJumpRise(CharacterData data, float gravityScale) {
        float launch = data.MaxJumpForce * 54f;
        float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f * gravityScale;
        return launch * launch / (2f * acceleration);
    }

    private static CharacterData LoadCharacter(string characterID) {
        var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
        AssertObject(data).OverrideFailureMessage($"Character '{characterID}' did not resolve.").IsNotNull();
        return data;
    }

    private static DialogueSequenceData SequenceNamed(string dialogueID) {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        foreach (DialogueSequenceData sequence in set.Sequences) {
            if (sequence.DialogueID == dialogueID) return sequence;
        }
        return null;
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
    private sealed class LunarFixture : IDisposable {
        public readonly Level12Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public LunarFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level12Controller>();
            Level.Name = "Level_12_Lunar_Test";
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
