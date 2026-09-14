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
/// Package 5 Wave C: Level 15 - the Library of Alexandria Restoration, the final
/// level of the campaign.
///
/// The structural half of this suite is the usual per-level contract: the manifest
/// scene path and level id, three checkpoints, a dialogue set whose every line key
/// resolves, the DUST_ECONOMY-locked 12/2/1/3 cultist-only budget, and a
/// three-phase Apex Eraser that fits its rotunda.
///
/// The other half is the part no other level has: <b>the endgame</b>. Defeating the
/// boss is not the end - design-godot.md 3389 gates the ending on the Temporal Core
/// being inserted into the Alexandria Anchor, and 2772-2778 replaces the results
/// overlay and the hub return with credits, the campaign-completion save write, and
/// the Main Menu. Those cases drive the whole chain programmatically and pin its
/// ordering, its exactly-once behaviour, that a resumed run can still reach it, and
/// that <c>SceneTree.Paused</c> is clear when it ends - a pause leaked out of the
/// finale would freeze the Main Menu it hands off to (CLAUDE.md signature 4).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level15ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_15_Alexandria.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_15_dialogue.tres";
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
        AssertThat(instance is Level15Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();

        // The library burning down around the player: cyclic fire in all three
        // approach rooms plus the advancing firestorm in the collapsing hall.
        foreach (string name in new[] {
            "FirePortico", "FireStacksWest", "FireScriptorium",
            "FireStacksEast", "FireHallWest", "FireHallEast"
        }) {
            AssertObject(instance.GetNodeOrNull<StoryCyclicHazard>(name))
                .OverrideFailureMessage($"Authored fire hazard '{name}' is missing.").IsNotNull();
        }
        AssertObject(instance.GetNodeOrNull<EscapeSequenceController>("Firestorm")).IsNotNull();

        // And the thing the whole campaign ends on.
        var anchor = instance.GetNodeOrNull<TemporalCoreAnchor>(Level15Controller.PrimeAnchorNodeName);
        AssertObject(anchor)
            .OverrideFailureMessage("The Prime Anchor must be authored in the scene.").IsNotNull();
        AssertString(anchor.AnchorID).IsEqual(Level15Controller.PrimeAnchorID);
        AssertObject(anchor.GetNodeOrNull<InteractionArea>("InteractionArea"))
            .OverrideFailureMessage("The Core insertion must go through the shared interaction path.")
            .IsNotNull();

        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheFinalCampaignSlot() {
        var level = new Level15Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_15_alexandria");
            AssertThat(level.Level).IsEqual(CampaignLevel.Alexandria);
            AssertString(level.LevelTitleKey).IsEqual("alexandria_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.Alexandria)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_15.entrance");
            AssertString(level.PreBossDialogueID).IsEqual("level_15.preboss");
            AssertString(level.BossIntroDialogueID).IsEqual("level_15.boss_intro");
            AssertString(level.EndingDialogueID).IsEqual("level_15.ending");

            // There is no exit beat and no hub return: the base's
            // "exit dialogue completed -> results overlay" route must be unreachable.
            AssertString(level.ExitDialogueID)
                .OverrideFailureMessage("Level 15 must not declare an exit beat; the campaign ends here.")
                .IsEqual("");
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new AlexandriaFixture(null);
        AssertThat(Level15Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_15_alexandria_checkpoint_{index}";
            AssertString(Level15Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesAllBeatsIncludingThePreBossAndBothEndings() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_15");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        // postboss carries the Core prompt; ending is the authored finale script.
        // Package 11 A6: N05 authors a second closing narration for a campaign
        // average below 50% - the Prime Anchor's visible scar. A3b owns the
        // unrounded 750-point selection between the two.
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_15.entrance", "level_15.preboss", "level_15.boss_intro",
            "level_15.postboss", "level_15.ending", "level_15.ending_scarred");
        // No exit beat, deliberately - see ExitDialogueID above.
        AssertThat(ids.Contains("level_15.exit")).IsFalse();
    }

    [TestCase]
    public void EveryDialogueLineSpeakerAndLevelKeyResolvesInTheEnglishTable() {
        HashSet<string> keys = LocalizationKeys();
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);

        foreach (DialogueSequenceData sequence in set.Sequences) {
            AssertThat(sequence.LineKeys.Length).IsGreater(0);
            AssertThat(sequence.SpeakerNameKeys.Length).IsEqual(sequence.LineKeys.Length);
            AssertThat(sequence.EmotionKeys.Length).IsEqual(sequence.LineKeys.Length);

            foreach (string key in sequence.LineKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Line key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
                AssertThat(key.StartsWith("dlg_l15_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "alexandria_level_title",
            "alexandria_room_portico", "alexandria_room_stacks",
            "alexandria_room_collapse", "alexandria_room_rotunda",
            "alexandria_objective_reach_anchor", "alexandria_objective_stacks",
            "alexandria_objective_escape", "alexandria_objective_rotunda",
            "alexandria_objective_defeat_boss", "alexandria_objective_restore",
            "alexandria_objective_complete",
            // The restoration's own copy: prompt plus both anchor states.
            "alexandria_interaction_insert_core",
            "alexandria_anchor_dormant", "alexandria_anchor_ready"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }

        // The compiled table is what Tr() actually reads; en.csv alone is not enough.
        TranslationServer.SetLocale("en");
        foreach (string key in new[] {
            "alexandria_level_title", "alexandria_interaction_insert_core", "alexandria_anchor_ready"
        }) {
            AssertThat(TranslationServer.Translate(key).ToString() != key)
                .OverrideFailureMessage(
                    $"'{key}' is in en.csv but not in the compiled en.en.translation - " +
                    "run --headless --import after editing en.csv.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheEndingBeatIsTheAuthoredFinaleScriptFromTheDesignDocument() {
        // design-godot.md 3389-3399. This is the last thing anyone who finishes the
        // game reads, so its content is pinned, not just its shape.
        DialogueSequenceData ending = SequenceNamed("level_15.ending");
        AssertObject(ending).IsNotNull();
        AssertThat(ending.LineKeys.Length).IsEqual(6);
        AssertThat(ending.SpeakerNameKeys).ContainsExactly(
            "speaker_sarah", "speaker_narration", "speaker_player",
            "speaker_sarah", "speaker_player", "speaker_narration");

        // Sarah on the radio: the anchor takes the feedback, the siphons die.
        string sarahsCall = LocalizationValue("dlg_l15_ending_1").ToLowerInvariant();
        AssertString(sarahsCall).Contains("forge");
        AssertString(sarahsCall).Contains("cradles are opening");

        // The montage names the three eras the design calls out by name.
        string montage = LocalizationValue("dlg_l15_ending_2").ToLowerInvariant();
        foreach (string era in new[] { "florence", "pompeii", "gettysburg" }) {
            AssertString(montage)
                .OverrideFailureMessage($"The restoration montage must show {era}.").Contains(era);
        }

        // V7.5: the single, voluntary surrender of the charge - the only power
        // loss in the game, and it happens by choice at the very end.
        string release = LocalizationValue("dlg_l15_ending_3").ToLowerInvariant();
        AssertString(release).Contains("resonance");
        AssertString(release).Contains("held in trust");

        // Sarah's farewell.
        string farewell = LocalizationValue("dlg_l15_ending_4").ToLowerInvariant();
        AssertString(farewell).Contains("rifts are closed");
        AssertString(farewell).Contains("timelines are sealed");

        // The closing line.
        AssertString(LocalizationValue("dlg_l15_ending_5").ToLowerInvariant()).Contains("ours to write");

        // Plan section 2.6 defers character-specific variants, so the farewell must
        // stay generic: no character's home is named in the ending.
        foreach (string place in new[] { "princeton", "orleans", "orléans", "palace" }) {
            AssertThat(farewell.Contains(place))
                .OverrideFailureMessage(
                    $"The ending names '{place}'; character-specific writing is deferred (plan 2.6).")
                .IsFalse();
        }
    }

    [TestCase]
    public void TheEndingBeatDoesNotTakeTheGameplayPause() {
        // Every other beat in the campaign pauses, and should. This one hands off to
        // the credits and then to a scene change, and it is the single place where a
        // leaked SceneTree.Paused would freeze the Main Menu instead of a level.
        DialogueSequenceData ending = SequenceNamed("level_15.ending");
        AssertThat(ending.PausesGameplay)
            .OverrideFailureMessage(
                "level_15.ending must not pause: the completion chain changes scene behind it.")
            .IsFalse();

        // The in-level beats keep the ordinary convention.
        foreach (string id in new[] {
            "level_15.entrance", "level_15.preboss", "level_15.boss_intro", "level_15.postboss"
        }) {
            AssertThat(SequenceNamed(id).PausesGameplay)
                .OverrideFailureMessage($"'{id}' should pause gameplay like every other level beat.")
                .IsTrue();
        }
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedTwelveTwoOneThreeBudget() {
        AssertThat(Level15Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 15 is locked at 12 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(12);
        AssertThat(Level15Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 15 is locked at 2 elites by DUST_ECONOMY.md.")
            .IsEqual(2);
        AssertThat(Level15Controller.SpawnTable.Length).IsEqual(14);
        AssertThat(Level15Controller.ExtractorPlacements.Length)
            .OverrideFailureMessage("Level 15 is locked at 3 Chronal Extractors.")
            .IsEqual(3);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 position) in Level15Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
            AssertThat(position.X > 0f && position.X < Level15Controller.ArenaStartX)
                .OverrideFailureMessage($"Extractor '{id}' is not on the approach route.").IsTrue();
        }
    }

    [TestCase]
    public void ActThreeIsCultistOnlyAndEveryTierResolves() {
        // Plan section 2.4: Titanic and Act III (13-15) draw from the Future Cultist
        // roster only - no era locals are left to brainwash at the fracture point.
        foreach ((string enemyID, int wave, Vector2 _) in Level15Controller.SpawnTable) {
            AssertThat(enemyID is Level15Controller.CultistEnemyID
                            or Level15Controller.TechEliteEnemyID
                            or Level15Controller.GuardEliteEnemyID)
                .OverrideFailureMessage($"'{enemyID}' is not on the Act III cultist roster.").IsTrue();
            AssertThat(wave >= 1 && wave <= 4).IsTrue();
        }

        var cultist = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level15Controller.CultistEnemyID}.tres");
        var techElite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level15Controller.TechEliteEnemyID}.tres");
        var guardElite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level15Controller.GuardEliteEnemyID}.tres");
        AssertObject(cultist).IsNotNull();
        AssertObject(techElite).IsNotNull();
        AssertObject(guardElite).IsNotNull();
        AssertThat(cultist.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(techElite.Tier).IsEqual(EnemyTier.Elite);
        AssertThat(guardElite.Tier).IsEqual(EnemyTier.Elite);

        // Both authored elites are used exactly once: the finale shows the cult's
        // full hand rather than doubling one archetype.
        AssertThat(CountInSpawnTable(Level15Controller.TechEliteEnemyID)).IsEqual(1);
        AssertThat(CountInSpawnTable(Level15Controller.GuardEliteEnemyID)).IsEqual(1);
    }

    [TestCase]
    public void EveryWaveFitsInsideTheLevelPoolWarmCounts() {
        // level_15_alexandria_pools warms 18 standards and 6 elites; warm counts are
        // concurrency caps (plan section 2.5), totals arrive across waves.
        var standardsPerWave = new Dictionary<int, int>();
        var elitesPerWave = new Dictionary<int, int>();
        foreach ((string enemyID, int wave, Vector2 _) in Level15Controller.SpawnTable) {
            bool isElite = enemyID != Level15Controller.CultistEnemyID;
            Dictionary<int, int> table = isElite ? elitesPerWave : standardsPerWave;
            table.TryGetValue(wave, out int running);
            table[wave] = running + 1;
        }
        foreach (KeyValuePair<int, int> wave in standardsPerWave) {
            AssertThat(wave.Value)
                .OverrideFailureMessage($"Wave {wave.Key} spawns {wave.Value} standards at once, over the warm count.")
                .IsLessEqual(18);
        }
        foreach (KeyValuePair<int, int> wave in elitesPerWave) {
            AssertThat(wave.Value).IsLessEqual(6);
        }
    }

    // === Boss ===

    [TestCase]
    public void TheApexEraserResolvesWithThreePhasesAndItsRangedBandFitsTheRotunda() {
        var boss = AuthoredResources.Load<BossData>(Level15Controller.BossResourcePath);
        AssertObject(boss).IsNotNull();
        AssertString(boss.BossID).IsEqual("apex_eraser");
        // The plan's roster table is authority over the design doc's stale 3000.
        AssertThat(boss.MaxHP)
            .OverrideFailureMessage("apex_eraser is authored at 1200 HP (plan section 4.1), not 3000.")
            .IsEqual(1200);
        AssertThat(boss.PhaseThresholds.Length)
            .OverrideFailureMessage("The final boss is authored with three phases.").IsEqual(2);
        AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

        // BossController converts range thresholds at 60 px per world unit; the
        // rotunda has to be wide enough for the Eraser to use its ranged band.
        const float pixelsPerUnit = 60f;
        float arenaWidth = Level15Controller.ArenaEndX - Level15Controller.ArenaStartX;
        AssertThat(boss.RangedRangeThreshold * pixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {boss.RangedRangeThreshold * pixelsPerUnit} px does not fit a {arenaWidth} px arena.")
            .IsTrue();
        AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();
    }

    [TestCase]
    public void TheBossEncounterAndThePrimeAnchorShareTheRotunda() {
        using var fixture = new AlexandriaFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count).IsEqual(1);
        BossEncounterController encounter = fixture.Level.BossEncounters[0];
        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("apex_eraser");
        AssertFloat(encounter.Position.X).IsGreater(Level15Controller.ArenaStartX);
        AssertFloat(encounter.Position.X).IsLess(Level15Controller.ArenaEndX);

        // The anchor is the reason the fight happens here; it must be in the same
        // room, east of the boss, and inside the walls.
        TemporalCoreAnchor anchor = fixture.Level.PrimeAnchor;
        AssertObject(anchor).IsNotNull();
        AssertFloat(anchor.Position.X).IsGreater(encounter.Position.X);
        AssertFloat(anchor.Position.X).IsLess(Level15Controller.ArenaEndX);
    }

    // === Structure ===

    [TestCase]
    public void EveryRoomConfinesTheCameraToAtLeastTheReferenceViewport() {
        using var fixture = new AlexandriaFixture(null);
        AssertThat(fixture.Level.RoomTriggers.Count).IsGreaterEqual(3);
        foreach (RoomTransitionTrigger trigger in fixture.Level.RoomTriggers) {
            AssertThat(trigger.CameraBounds.Size.X)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines the camera below 1920 px wide.")
                .IsGreaterEqual(1920f);
            AssertThat(trigger.CameraBounds.Size.Y).IsGreaterEqual(1080f);
            AssertThat(trigger.CameraBounds.Position.X).IsGreaterEqual(-200f);
            AssertThat(trigger.CameraBounds.End.X).IsLessEqual(Level15Controller.LevelWidth + 200f);
            // Level agents must not set CameraPath themselves (plan A1 deviation);
            // the base back-fills it once the confiner exists.
            AssertThat(trigger.CameraPath != null && !trigger.CameraPath.IsEmpty).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredRungIsASingleJumpForTheHeaviestCharacter() {
        // This level has no gravity mechanic, so the galleries are sized against
        // Lincoln - single-jump only, and by some margin the heaviest.
        float heaviestJump = float.MaxValue;
        string heaviest = "";
        foreach (string id in RosterIDs) {
            CharacterData data = LoadCharacter(id);
            float rise = SingleJumpRise(data);
            if (rise >= heaviestJump) continue;
            heaviestJump = rise;
            heaviest = id;
        }
        AssertThat(heaviest).IsNotEqual("");

        float tallestClimb = 0f;
        foreach ((string galleryID, (float X, float Y, float Width)[] rungs) in Level15Controller.Galleries) {
            AssertThat(rungs.Length).IsGreater(0);
            float standingY = Level15Controller.FloorY;
            float climbed = 0f;
            foreach ((float x, float y, float width) in rungs) {
                float rise = standingY - y;
                AssertThat(rise >= 0f)
                    .OverrideFailureMessage($"Gallery '{galleryID}' descends at x={x}; rungs must climb.")
                    .IsTrue();
                AssertThat(rise <= heaviestJump)
                    .OverrideFailureMessage(
                        $"Gallery '{galleryID}' asks for a {rise} px rise at x={x}, but '{heaviest}' " +
                        $"only clears {heaviestJump} px.")
                    .IsTrue();
                AssertThat(width).IsGreater(120f);
                climbed += rise;
                standingY = y;
            }
            tallestClimb = Mathf.Max(tallestClimb, climbed);
        }

        // Plan section 5.2 wants a real vertical section; the scriptorium is it.
        AssertThat(tallestClimb > 3f * heaviestJump)
            .OverrideFailureMessage(
                $"The tallest climb is only {tallestClimb} px - there is no vertical section here.")
            .IsTrue();
    }

    [TestCase]
    public void TheFirestormRunsBetweenTheStacksAndTheRotundaDoor() {
        using var fixture = new AlexandriaFixture(null);
        EscapeSequenceController firestorm = fixture.Level.Firestorm;
        AssertObject(firestorm)
            .OverrideFailureMessage("The collapsing hall must run an authored escape sequence.").IsNotNull();
        AssertString(firestorm.SequenceID).IsEqual(Level15Controller.EscapeSequenceID);
        AssertFloat(firestorm.StartX).IsEqualApprox(Level15Controller.EscapeStartX, 0.01f);
        AssertFloat(firestorm.FinishX).IsEqualApprox(Level15Controller.EscapeFinishX, 0.01f);

        // The finish line has to sit west of checkpoint 2, or a run that survives the
        // firestorm still saves behind it and gets a fresh front on resume.
        fixture.Level.Levels.TryGetCheckpointPosition(Level15Controller.Checkpoint2, out Vector2 checkpoint2);
        AssertFloat(checkpoint2.X)
            .OverrideFailureMessage("Checkpoint 2 must be east of the firestorm's finish line.")
            .IsGreater(Level15Controller.EscapeFinishX);
        // ...and west of the rotunda, so a resume can still reach the boss and anchor.
        AssertFloat(checkpoint2.X).IsLess(Level15Controller.ArenaStartX);

        // Being caught costs HP, never a life: Chronal Rewind stays the failure state.
        AssertThat(firestorm.CatchDamage).IsGreater(0);
        AssertThat(firestorm.IsRunning).IsFalse();
        AssertThat(fixture.Level.FirestormTriggered).IsFalse();
    }

    // === The endgame ===

    [TestCase]
    public void TheAnchorIsInertUntilTheApexEraserIsDown() {
        using var fixture = new AlexandriaFixture(EntrySave());
        Level15Controller level = fixture.Level;
        TemporalCoreAnchor anchor = level.PrimeAnchor;
        AssertObject(anchor).IsNotNull();

        AssertThat(level.IsBossDefeated).IsFalse();
        AssertThat(anchor.IsArmed)
            .OverrideFailureMessage("The anchor must be sealed while the Eraser lives.").IsFalse();
        AssertThat(anchor.CanInteract(level.Player)).IsFalse();

        // Even a direct call must refuse: the ending cannot be reached early.
        AssertThat(anchor.InsertCore()).IsFalse();
        AssertThat(anchor.IsInserted).IsFalse();
        AssertObject(level.Completion)
            .OverrideFailureMessage("The completion chain started before the boss died.").IsNull();
        AssertThat(level.LevelComplete).IsFalse();
    }

    [TestCase]
    public void TheEndingDoesNotFireWhenTheBossDiesOnlyWhenTheCoreGoesIn() {
        using var fixture = new AlexandriaFixture(EntrySave());
        Level15Controller level = fixture.Level;

        DefeatTheApexEraser();

        // The base kept its own bookkeeping (Level 8's lesson) ...
        AssertThat(level.IsBossDefeated).IsTrue();
        // V7.3 Single Icon Rule: the defeat spawns a physical pickup; the
        // wallet (and the boss line) is paid when it is collected.
        AssertThat(level.DustEarnedThisLevel).IsEqual(0);
        ChronalDustPickup bossDust = null;
        for (int index = 0; index < level.GetChildCount() && bossDust == null; index++) {
            bossDust = level.GetChild(index) as ChronalDustPickup;
        }
        AssertObject(bossDust)
            .OverrideFailureMessage("The Eraser's defeat spawned no dust pickup.")
            .IsNotNull();
        bossDust.Collect();
        AssertThat(level.DustEarnedThisLevel).IsGreaterEqual(50);
        // ... the gate opened on the defeat itself, not at the end of a beat chain ...
        AssertThat(level.PrimeAnchor.IsArmed)
            .OverrideFailureMessage("The anchor must arm the instant the Eraser dies.").IsTrue();
        AssertThat(level.PrimeAnchor.CanInteract(level.Player)).IsTrue();
        // ... and nothing about the ending has started.
        AssertObject(level.Completion)
            .OverrideFailureMessage(
                "Design 3389 gates the ending on the Core insertion, not on the boss defeat.")
            .IsNull();
        AssertThat(level.LevelComplete)
            .OverrideFailureMessage("Killing the boss must not complete the level on its own.").IsFalse();

        // The player's action is what advances it.
        AssertThat(level.PrimeAnchor.InsertCore()).IsTrue();
        AssertObject(level.Completion).IsNotNull();
        AssertThat(level.LevelComplete).IsTrue();
    }

    [TestCase]
    public void TheFullEndgameChainRunsBossThenCoreThenEndingThenCreditsThenCompletionExactlyOnce() {
        var tree = (SceneTree)Engine.GetMainLoop();
        var order = new List<string>();
        int levelCompletes = 0;
        int chainFinishes = 0;
        bool pausedAtHandOff;

        void OnLevelComplete(string id) { levelCompletes++; order.Add("level_complete"); }
        EventBus.Instance.OnLevelComplete += OnLevelComplete;

        using (var fixture = new AlexandriaFixture(EntrySave())) {
            Level15Controller level = fixture.Level;
            // Production default is true; the runner cannot survive a real
            // ChangeSceneToPacked, so the request is asserted rather than executed.
            AssertThat(level.ReturnToMainMenuOnCompletion)
                .OverrideFailureMessage("Level 15 must return to the Main Menu, not the hub.").IsTrue();
            level.ReturnToMainMenuOnCompletion = false;

            try {
                DefeatTheApexEraser();
                order.Add("boss_defeated");

                AssertThat(level.PrimeAnchor.InsertCore()).IsTrue();
                order.Add("core_inserted");

                CampaignCompletionSequence chain = level.Completion;
                AssertObject(chain).IsNotNull();
                chain.SequenceFinished += () => { chainFinishes++; order.Add("chain_finished"); };

                // The chain owns the ending beat (its endingDialogueID route), so the
                // credits must not have started yet - and it must be the ONLY owner:
                // level_15.ending is not a post-boss beat.
                AssertString(chain.EndingDialogueID).IsEqual("level_15.ending");
                AssertThat(chain.IsEndingDialogueActive)
                    .OverrideFailureMessage("The ending beat did not start.").IsTrue();
                AssertObject(chain.Credits)
                    .OverrideFailureMessage("Credits must wait for the ending dialogue.").IsNull();
                order.Add("ending_started");

                // An unrelated sequence completing must not skip the ending.
                EventBus.Instance.RaiseDialogueComplete("level_15.postboss");
                AssertObject(chain.Credits).IsNull();

                EventBus.Instance.RaiseDialogueComplete("level_15.ending");
                AssertThat(chain.IsEndingDialogueActive).IsFalse();
                AssertObject(chain.Credits).IsNotNull();
                order.Add("credits_rolling");

                // Package 5 C1 closed the window this pair used to pin: the flag lands
                // as the credits START. A quit during the roll now keeps a completion
                // the save's advanced level pointer already implies.
                AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted)
                    .OverrideFailureMessage(
                        "Quitting during the credits must not lose the campaign completion.")
                    .IsTrue();
                AssertThat(chain.CampaignMarkedCompleted).IsTrue();

                chain.Credits.Skip();
                AssertThat(chain.IsFinished).IsTrue();
                AssertThat(chain.CampaignMarkedCompleted)
                    .OverrideFailureMessage("The campaign completion flag was never written.").IsTrue();
                AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();
                order.Add("completed_flag");

                // A leaked pause here would freeze the Main Menu this hands off to.
                pausedAtHandOff = tree.Paused;

                // Exactly once: a second skip and a second insertion change nothing.
                chain.SkipToEnd();
                AssertThat(level.PrimeAnchor.InsertCore()).IsFalse();
                AssertObject(level.ShowCompletionResults())
                    .OverrideFailureMessage("Completion must be one-shot.").IsNull();
            } finally {
                EventBus.Instance.OnLevelComplete -= OnLevelComplete;
            }
        }

        AssertThat(order).ContainsExactly(
            "boss_defeated", "level_complete", "core_inserted", "ending_started",
            "credits_rolling", "chain_finished", "completed_flag");
        AssertThat(levelCompletes)
            .OverrideFailureMessage("The level advanced more than once.").IsEqual(1);
        AssertThat(chainFinishes)
            .OverrideFailureMessage("The completion chain finished more than once.").IsEqual(1);
        AssertThat(pausedAtHandOff)
            .OverrideFailureMessage("The endgame left SceneTree.Paused set; the Main Menu would load frozen.")
            .IsFalse();
    }

    [TestCase]
    public void TheEndgameSkipsStraightToCompletionWhenThePlayerSkipsEverything() {
        // Design 2772-2778: the credits are skippable at any time, and the save write
        // happens anyway.
        using var fixture = new AlexandriaFixture(EntrySave());
        Level15Controller level = fixture.Level;
        level.ReturnToMainMenuOnCompletion = false;

        DefeatTheApexEraser();
        level.PrimeAnchor.InsertCore();
        CampaignCompletionSequence chain = level.Completion;
        EventBus.Instance.RaiseDialogueComplete("level_15.ending");

        AssertThat(chain.Credits.IsFinished).IsFalse();
        AssertThat(chain.CreditsRolling).IsTrue();

        chain.SkipToEnd();

        AssertThat(chain.Credits.IsFinished).IsTrue();
        AssertThat(chain.IsFinished).IsTrue();
        AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();
        // The overlay the other fourteen levels show must never appear here.
        AssertObject(FindDescendant<LevelResultsPanel>(level))
            .OverrideFailureMessage("Level 15 must not show the hub results overlay.").IsNull();
        AssertString(CampaignCompletionSequence.MainMenuScenePath)
            .IsEqual("res://scenes/menus/MainMenu.tscn");
    }

    [TestCase]
    public void ResumingAtTheFinalCheckpointAfterTheBossStillReachesTheEnding() {
        // Nothing persists "the Eraser is dead" - deliberately. A run that quits
        // between the defeat and the restoration resumes at checkpoint 2 with the
        // boss alive, refights it, and reaches the same ending. The finale is never
        // gated on a flag the save did not keep.
        using var fixture = new AlexandriaFixture(CheckpointSave(Level15Controller.Checkpoint2));
        Level15Controller level = fixture.Level;
        level.ReturnToMainMenuOnCompletion = false;

        AssertThat(level.ResumedMidLevel).IsTrue();
        AssertString(level.ResumedCheckpointID).IsEqual(Level15Controller.Checkpoint2);
        AssertThat(level.HasWaveSpawned(3))
            .OverrideFailureMessage("Waves behind checkpoint 2 must stay cleared.").IsTrue();
        AssertThat(level.HasWaveSpawned(4)).IsFalse();
        // The firestorm is already survived; a resume must not re-arm it behind them.
        AssertThat(level.FirestormTriggered).IsTrue();
        AssertThat(level.Firestorm.IsCompleted).IsTrue();
        AssertThat(level.Firestorm.IsRunning).IsFalse();
        // Sarah's last briefing still lands: it sits east of checkpoint 2.
        AssertThat(level.PreBossBeatPlayed).IsFalse();

        // The boss is back, and the anchor is sealed again with it.
        AssertThat(level.IsBossDefeated).IsFalse();
        AssertThat(level.BossEncounters[0].IsDefeated).IsFalse();
        AssertThat(level.PrimeAnchor.IsArmed).IsFalse();

        // ...and the whole ending is still reachable from here.
        DefeatTheApexEraser();
        AssertThat(level.PrimeAnchor.IsArmed).IsTrue();
        AssertThat(level.PrimeAnchor.InsertCore()).IsTrue();
        AssertObject(level.Completion).IsNotNull();
        EventBus.Instance.RaiseDialogueComplete("level_15.ending");
        level.Completion.SkipToEnd();
        AssertThat(level.Completion.CampaignMarkedCompleted).IsTrue();
        AssertThat(SaveManager.Instance.SaveSlots[ScratchSlot].IsCompleted).IsTrue();
    }

    [TestCase]
    public void ARewindAfterTheRestorationCannotUndoIt() {
        using var fixture = new AlexandriaFixture(EntrySave());
        Level15Controller level = fixture.Level;
        level.ReturnToMainMenuOnCompletion = false;

        DefeatTheApexEraser();
        level.PrimeAnchor.InsertCore();

        // The ending is already rolling; a rewind must not re-seal the anchor and
        // strand the player in a finished arena.
        EventBus.Instance.RaiseRewindTriggered(level.Player.Position);
        AssertThat(level.PrimeAnchor.IsInserted).IsTrue();
        AssertThat(level.PrimeAnchor.IsArmed).IsTrue();
        AssertThat(level.LevelComplete).IsTrue();
    }

    // === Helpers ===

    /// <summary>
    /// Kills the Apex Eraser the way <see cref="BossEncounterController"/> hears it.
    /// </summary>
    private static void DefeatTheApexEraser() =>
        EventBus.Instance.RaiseBossDefeated(new BossDefeatedPayload {
            BossID = "apex_eraser",
            ChronalDustDrop = 25
        });

    /// <summary>
    /// A save parked on this level's entry checkpoint. Used instead of a null save
    /// for the endgame cases so the base skips its deferred entrance dialogue: that
    /// beat is <c>PausesGameplay = true</c>, and a landed deferred call would make
    /// the pause assertions nondeterministic.
    /// </summary>
    private static StorySaveData EntrySave() => CheckpointSave(Level15Controller.Checkpoint0);

    private static StorySaveData CheckpointSave(string checkpointID) => new() {
        SelectedCharacterID = "einstein",
        CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.Alexandria),
        LastCheckpointID = checkpointID,
        CurrentHP = 80,
        CurrentUltimateMeter = 30f
    };

    private static int CountInSpawnTable(string enemyID) {
        int total = 0;
        foreach ((string id, int _, Vector2 _) in Level15Controller.SpawnTable) {
            if (id == enemyID) total++;
        }
        return total;
    }

    private static T FindDescendant<T>(Node root) where T : Node {
        for (int index = 0; index < root.GetChildCount(); index++) {
            Node child = root.GetChild(index);
            if (child is T match) return match;
            T nested = FindDescendant<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// Closed-form jump apex for PlayerController's authored physics at Earth
    /// gravity: PerformJump sets vy = -MaxJumpForce * 54 and ApplyGravity
    /// accelerates at BaseGravity(18) * (0.8 + 0.4 * Weight) * 60 px/s^2.
    /// </summary>
    private static float SingleJumpRise(CharacterData data) {
        float launch = data.MaxJumpForce * 54f;
        float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f;
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
    private sealed class AlexandriaFixture : IDisposable {
        public readonly Level15Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public AlexandriaFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level15Controller>();
            Level.Name = "Level_15_Alexandria_Test";
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
            // The endgame cases write IsCompleted to the scratch slot; put the row
            // back in memory and on disk so a developer's real save is untouched.
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            if (_originalSave == null) SaveManager.Instance.DeleteStorySlot(ScratchSlot);
            else SaveManager.Instance.SaveStorySlot(ScratchSlot);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            // StoryManager.CurrentLevel is deliberately NOT restored: its setter is
            // private and both public paths (ResumeCampaign, RestartCollapsedLevel)
            // trigger a scene load, which would pull the runner's own scene. This is
            // the only level suite that reaches OnLevelComplete, and nothing in the
            // suite reads the campaign pointer.
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
