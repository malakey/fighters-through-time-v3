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
/// Package 5 Wave C: Level 13 - The Chronal Void, the Act III opener.
///
/// What is pinned here is everything the level cannot silently drift out of: the
/// manifest scene path and level id, the three checkpoint ids, the three-beat
/// dialogue set, the DUST_ECONOMY-locked 8/1/0/2 encounter budget (including the
/// deliberate absence of a boss row), and the two era mechanics.
///
/// The load-bearing assertions are the traversal contract and the arena contract.
/// Gravity here <b>changes during play</b>, so every authored climb is re-derived
/// from the nine characters' authored jump physics against <b>every scale in every
/// cycle</b> - a retune that strands the heaviest character has to fail the level,
/// not the player. And because the encounter is a mirror match against the
/// player's own character driven by the real Hard Fighter CPU, the arena is
/// asserted to be a fair Fighter-style stage: symmetric platforms, symmetric spawn
/// marks, an unbroken floor, and no shifting gravity or rift pocket anywhere
/// inside it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level13ContentTests {

    private const string ScenePath = "res://scenes/campaign/Level_13_ChronalVoid.tscn";
    private const string DialoguePath = "res://resources/Dialogue/level_13_dialogue.tres";
    private const int ScratchSlot = 2;

    /// <summary>BossController's world-unit conversion; the Mirror reuses the same BossData ranges.</summary>
    private const float PixelsPerUnit = 60f;

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
        // Mirrors the smoke-test contract: instantiate without entering the tree, so
        // this proves the .tscn resolves its script and template instances without
        // also running the whole level build.
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertObject(scene).IsNotNull();

        Node instance = scene.Instantiate();
        AssertObject(instance).IsNotNull();
        AssertThat(instance is Level13Controller).IsTrue();
        AssertObject(instance.GetNodeOrNull<StoryDropSystem>("StoryDropSystem")).IsNotNull();

        // The four era fragments still adrift in the rift.
        foreach ((string nodeName, string _, string _, string _, string _) in Level13Controller.Shards) {
            AssertObject(instance.GetNodeOrNull<PathMovingPlatform>(nodeName))
                .OverrideFailureMessage($"Drifting era shard '{nodeName}' is missing from the scene.")
                .IsNotNull();
        }
        // The three pockets of pooled time on the shelf.
        foreach (string nodeName in Level13Controller.RiftPocketNodeNames) {
            AssertObject(instance.GetNodeOrNull<ChronalRiftZone>(nodeName))
                .OverrideFailureMessage($"Rift pocket '{nodeName}' is missing from the scene.")
                .IsNotNull();
        }
        instance.Free();
    }

    [TestCase]
    public void TheControllerCarriesTheManifestIdentityAndTheCampaignSlot() {
        var level = new Level13Controller();
        try {
            AssertString(level.LevelID).IsEqual("level_13_chronal_void");
            AssertThat(level.Level).IsEqual(CampaignLevel.ChronalVoid);
            AssertString(level.LevelTitleKey).IsEqual("chronal_void_level_title");
            AssertString(level.DialogueSetPath).IsEqual(DialoguePath);
            // The scene must sit exactly where StoryManager routes the campaign.
            AssertString(StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid)).IsEqual(ScenePath);

            AssertString(level.EntranceDialogueID).IsEqual("level_13.entrance");
            AssertString(level.BossIntroDialogueID).IsEqual("level_13.boss_intro");
            AssertString(level.ExitDialogueID).IsEqual("level_13.exit");
        } finally {
            level.Free();
        }
    }

    [TestCase]
    public void ExactlyThreeCheckpointsRegisterUnderTheLockedIDs() {
        using var fixture = new VoidFixture(null);
        AssertThat(Level13Controller.CheckpointIDs.Length).IsEqual(3);
        for (int index = 0; index < 3; index++) {
            string id = $"level_13_chronal_void_checkpoint_{index}";
            AssertString(Level13Controller.CheckpointIDs[index]).IsEqual(id);
            AssertThat(fixture.Level.Levels.TryGetCheckpointPosition(id, out Vector2 _))
                .OverrideFailureMessage($"Checkpoint '{id}' never registered.").IsTrue();
        }
    }

    // === Dialogue ===

    [TestCase]
    public void TheDialogueSetCarriesTheFourAuthoredBeats() {
        var set = AuthoredResources.Load<DialogueSetData>(DialoguePath);
        AssertObject(set).IsNotNull();
        AssertString(set.DialogueSetID).IsEqual("dialogue_level_13");

        var ids = new List<string>();
        foreach (DialogueSequenceData sequence in set.Sequences) ids.Add(sequence.DialogueID);
        // No preboss beat here: plan section 2.3 gives that only to 5, 12 and 15.
        // Package 13 W3 (S47): the Eraser-pair line joins the three original beats.
        AssertThat(ids).ContainsExactlyInAnyOrder(
            "level_13.entrance", "level_13.eraser_pair", "level_13.boss_intro", "level_13.exit");
        AssertString(Level13Controller.EraserPairDialogueID).IsEqual("level_13.eraser_pair");
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
                AssertThat(key.StartsWith("dlg_l13_", StringComparison.Ordinal)).IsTrue();
            }
            foreach (string key in sequence.SpeakerNameKeys) {
                AssertThat(keys.Contains(key))
                    .OverrideFailureMessage($"Speaker key '{key}' is missing from localization/en.csv.")
                    .IsTrue();
            }
        }

        foreach (string key in new[] {
            "chronal_void_level_title",
            "void_room_threshold", "void_room_drift", "void_room_mirror_arena",
            "void_objective_cross_rift", "void_objective_drift", "void_objective_face_mirror",
            "void_objective_defeat_mirror", "void_objective_complete"
        }) {
            AssertThat(keys.Contains(key))
                .OverrideFailureMessage($"Level key '{key}' is missing from localization/en.csv.")
                .IsTrue();
        }

        // Every era caption the level paints onto its geometry, settled and adrift.
        foreach ((string motifID, float _, float _, float _, string labelKey, Color _) in Level13Controller.EraMotifs) {
            AssertThat(keys.Contains(labelKey))
                .OverrideFailureMessage($"Motif '{motifID}' caption '{labelKey}' is missing from en.csv.")
                .IsTrue();
        }
        foreach ((string nodeName, string _, string _, string _, string labelKey) in Level13Controller.Shards) {
            AssertThat(keys.Contains(labelKey))
                .OverrideFailureMessage($"Shard '{nodeName}' caption '{labelKey}' is missing from en.csv.")
                .IsTrue();
        }
    }

    [TestCase]
    public void TheMirrorSpeaksAsThePlayersOwnCostAndTheExitAdmitsTheDoubt() {
        // This is the campaign's identity beat - the player fights themself - so its
        // content is pinned, not just its shape. A rewrite that turns the Mirror into
        // a generic taunting boss, or that lets the player walk away untroubled, has
        // to fail here.
        DialogueSequenceData intro = SequenceNamed("level_13.boss_intro");
        AssertObject(intro).IsNotNull();
        // Package 13 W3 (S45/S47): Sarah names it over the Beacon and the hero answers.
        AssertThat(intro.SpeakerNameKeys).ContainsExactly("speaker_sarah", "speaker_player");

        // S45: the Mirror is the Void's reflection — the hero's own resonance
        // thrown back — not a model anyone built. V7.5 still holds: it is not the
        // resonance the hero shed, because there is no mid-campaign power loss.
        string mirror = LocalizationValue("dlg_l13_boss_intro_1").ToLowerInvariant();
        AssertString(mirror).Contains("your own resonance, thrown back at you");
        AssertString(mirror).Contains("mirror");
        AssertString(LocalizationValue("dlg_l13_boss_intro_2").ToLowerInvariant())
            .Contains("how it fights");

        // The exit hands off to Level 14 at the Bastion's gate rather than to a
        // restored era: nothing living was ever caught in the Void.
        AssertString(LocalizationValue("dlg_l13_exit_1").ToLowerInvariant()).Contains("fortress");
        AssertString(LocalizationValue("dlg_l13_exit_2").ToLowerInvariant()).Contains("where i'm going");
        AssertString(LocalizationValue("dlg_l13_eraser_1").ToLowerInvariant()).Contains("erasers");
    }

    // === Encounter economy (locked by docs/DUST_ECONOMY.md) ===

    [TestCase]
    public void TheAuthoredSpawnTableMatchesTheLockedEightOneZeroTwoBudget() {
        AssertThat(Level13Controller.StandardEnemyCount)
            .OverrideFailureMessage("Level 13 is locked at 8 standard enemies by DUST_ECONOMY.md.")
            .IsEqual(8);
        // V7.6 (Package 11 A7a): Act III opens with the Eraser PAIR - the design's
        // "Level 13: a pair, scripted ambush, with the naming line". They sit on
        // top of the pre-F05 locked row; F05 (A10) replaces the flat per-tier
        // award with a per-level required-encounter pool, which is the model under
        // which a salted hunter costs nothing to add.
        AssertThat(Level13Controller.EliteEnemyCount)
            .OverrideFailureMessage("Level 13 authors 1 era elite plus the Eraser pair.")
            .IsEqual(3);
        AssertThat(Level13Controller.EraserCount)
            .OverrideFailureMessage("Act III opens with an Eraser PAIR, not a single hunter.")
            .IsEqual(2);
        AssertThat(Level13Controller.SpawnTable.Length).IsEqual(11);
        AssertThat(Level13Controller.ExtractorPlacements.Length)
            .OverrideFailureMessage("Level 13 is locked at 2 Chronal Extractors.")
            .IsEqual(2);

        var extractorIDs = new HashSet<string>();
        foreach ((string id, Vector2 _) in Level13Controller.ExtractorPlacements) {
            AssertThat(extractorIDs.Add(id))
                .OverrideFailureMessage($"Duplicate extractor id '{id}'.").IsTrue();
        }
    }

    [TestCase]
    public void TheLevelHasNoBossRowAtAllAndNeverWiresABossEncounterController() {
        // The economy row is B = 0 and the Mirror's 50 dust is already inside it. A
        // BossEncounterController appearing here would both double-count the dust and
        // replace a CPU mirror match with a scripted attack-pattern boss.
        using var fixture = new VoidFixture(null);
        AssertThat(fixture.Level.BossEncounters.Count)
            .OverrideFailureMessage("Level 13 must not register a BossEncounterController.")
            .IsEqual(0);
        AssertThat(CountOfType<BossEncounterController>(fixture.Level))
            .OverrideFailureMessage("A BossEncounterController is present somewhere in Level 13.")
            .IsEqual(0);
        AssertThat(CountOfType<BossController>(fixture.Level))
            .OverrideFailureMessage("A scripted BossController is present somewhere in Level 13.")
            .IsEqual(0);
    }

    [TestCase]
    public void TheRosterMixesTheRiftPhantomWithCultistsAndEveryTierResolves() {
        foreach ((string enemyID, int wave, Vector2 _) in Level13Controller.SpawnTable) {
            AssertThat(enemyID is Level13Controller.PhantomEnemyID
                            or Level13Controller.CultistEnemyID
                            or Level13Controller.EliteEnemyID
                            or Level13Controller.EraserEnemyID)
                .OverrideFailureMessage($"'{enemyID}' is not on the Chronal Void roster.").IsTrue();
            AssertThat(wave >= 1 && wave <= 3).IsTrue();
        }

        var phantom = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level13Controller.PhantomEnemyID}.tres");
        var cultist = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level13Controller.CultistEnemyID}.tres");
        var elite = AuthoredResources.Load<EnemyData>(
            $"res://resources/Enemies/{Level13Controller.EliteEnemyID}.tres");
        AssertObject(phantom).IsNotNull();
        AssertObject(cultist).IsNotNull();
        AssertObject(elite).IsNotNull();
        AssertThat(phantom.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(cultist.Tier).IsEqual(EnemyTier.Standard);
        AssertThat(elite.Tier).IsEqual(EnemyTier.Elite);

        // The phantom is why the Alexandria standard belongs in a shattered rift: it
        // flies and it walks through the geometry, so the drifting shards never trap
        // it and the void never has to explain how it got somewhere.
        AssertThat(phantom.Behavior)
            .OverrideFailureMessage("The Void's era standard must be the flying phantom.")
            .IsEqual(DefaultBehavior.Flying);
        AssertThat(phantom.PhasesThroughWalls)
            .OverrideFailureMessage("The rift phantom must phase through walls.").IsTrue();
    }

    [TestCase]
    public void EveryWaveFitsInsideTheLevelPoolWarmCounts() {
        // Warm counts are concurrency caps (plan section 2.5): standard_enemy 10,
        // elite_enemy 4 (raised from 3 by A7a for the Eraser pair - wave 3 now
        // fields three elites at once and a RecycleOldest pool with zero headroom
        // would silently recycle a live one), enemy_projectile 12.
        var standardsPerWave = new Dictionary<int, int>();
        var elitesPerWave = new Dictionary<int, int>();
        foreach ((string enemyID, int wave, Vector2 _) in Level13Controller.SpawnTable) {
            bool isElite = enemyID is Level13Controller.EliteEnemyID or Level13Controller.EraserEnemyID;
            Dictionary<int, int> table = isElite ? elitesPerWave : standardsPerWave;
            table.TryGetValue(wave, out int running);
            table[wave] = running + 1;
        }
        foreach (KeyValuePair<int, int> wave in standardsPerWave) {
            AssertThat(wave.Value)
                .OverrideFailureMessage($"Wave {wave.Key} spawns {wave.Value} standards at once, over the warm count.")
                .IsLessEqual(10);
        }
        foreach (KeyValuePair<int, int> wave in elitesPerWave) {
            AssertThat(wave.Value).IsLessEqual(4);
        }
    }

    // === The Mirror Paradox ===

    [TestCase]
    public void TheMirrorEncounterIsWiredAndItsCloneCarriesTheAuthoredThousandHP() {
        using var fixture = new VoidFixture(null);
        MirrorParadoxEncounterController encounter = fixture.Level.MirrorEncounter;
        AssertObject(encounter)
            .OverrideFailureMessage("Level 13 must wire a MirrorParadoxEncounterController.").IsNotNull();

        AssertObject(encounter.Data).IsNotNull();
        AssertString(encounter.Data.BossID).IsEqual("mirror_paradox");
        AssertThat(encounter.Data.MaxHP)
            .OverrideFailureMessage("The Mirror Paradox is authored at 1000 HP.").IsEqual(1000);
        // Section 4.1: single phase, and the one roster entry that is NOT
        // knockback-immune, because it fights like a player.
        AssertThat(encounter.Data.PhaseThresholds.Length).IsEqual(0);
        AssertThat(encounter.Data.IsKnockbackImmune).IsFalse();
        AssertThat(encounter.Data.ChronalDustDrop).IsEqual(25);

        MirrorParadoxController mirror = encounter.Mirror;
        AssertObject(mirror).OverrideFailureMessage("The Mirror clone never spawned.").IsNotNull();
        AssertThat(mirror.PhaseCount).IsEqual(1);
        AssertThat(mirror.ScaledMaxHP)
            .OverrideFailureMessage("At Normal difficulty the clone carries the authored 1000 HP.")
            .IsEqual(1000);
        AssertObject(mirror.Clone).IsNotNull();
        AssertThat(mirror.Clone.EncounterMaxHPOverride).IsEqual(1000);
        // It mirrors whoever the base class actually spawned, not a fixed character
        // and not a second independent read of the session.
        AssertString(mirror.MirroredCharacterID).IsEqual(fixture.Level.Player.Data.CharacterID);
        AssertString(mirror.MirroredCharacterID).IsEqual("einstein");
        // Inert until reveal: an unrevealed clone must not simulate into the level.
        AssertThat(encounter.IsRevealed).IsFalse();
        AssertThat(mirror.IsEncounterActive).IsFalse();

        // The clone stands inside its own arena, east of the seam.
        AssertFloat(mirror.GlobalPosition.X).IsGreater(Level13Controller.ArenaStartX);
        AssertFloat(mirror.GlobalPosition.X).IsLess(Level13Controller.ArenaEndX);
    }

    [TestCase]
    public void TheMirrorArenaReadsAsAFairFighterStage() {
        using var fixture = new VoidFixture(null);
        Level13Controller level = fixture.Level;

        // Symmetric spawn marks about the centre line: a mirror match starts even.
        AssertFloat(Level13Controller.MirrorSpawnX - Level13Controller.ArenaCenterX)
            .IsEqualApprox(Level13Controller.ArenaCenterX - Level13Controller.PlayerArenaMarkX, 0.001f);

        // Two side platforms, mirrored, identical, and reachable by every character
        // at the arena's Earth-normal gravity - Fighter-stage furniture, not a climb.
        var arenaPlatforms = new List<OneWayPlatform>();
        Godot.Collections.Array<Node> children = level.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is OneWayPlatform platform && platform.Position.X >= Level13Controller.ArenaStartX) {
                arenaPlatforms.Add(platform);
            }
        }
        AssertThat(arenaPlatforms.Count)
            .OverrideFailureMessage("The Mirror arena is authored with exactly two symmetric platforms.")
            .IsEqual(2);
        foreach (OneWayPlatform platform in arenaPlatforms) {
            AssertFloat(platform.Position.Y).IsEqualApprox(Level13Controller.ArenaPlatformY, 0.001f);
            AssertFloat(Mathf.Abs(platform.Position.X - Level13Controller.ArenaCenterX))
                .OverrideFailureMessage("An arena platform is off the mirror line.")
                .IsEqualApprox(Level13Controller.ArenaPlatformOffsetX, 0.001f);
        }
        AssertFloat(arenaPlatforms[0].Position.X + arenaPlatforms[1].Position.X)
            .IsEqualApprox(Level13Controller.ArenaCenterX * 2f, 0.001f);

        // 10% of headroom absorbs the platform's own half-thickness, which the
        // centre-to-centre convention used throughout this level ignores.
        float earthReach = WorstRosterJumpRise(1f);
        float platformRise = Level13Controller.ShelfY - Level13Controller.ArenaPlatformY;
        AssertThat(platformRise <= earthReach * 0.9f)
            .OverrideFailureMessage(
                $"An arena platform asks for a {platformRise} px rise but the heaviest character clears " +
                $"{earthReach} px at the arena's Earth-normal gravity - a duelling platform nobody can " +
                "reach is dead geometry.")
            .IsTrue();

        // The floor under the duel is one unbroken slab across the whole arena.
        AssertObject(level.GetNodeOrNull<StaticBody2D>(
                $"Floor_{Mathf.RoundToInt(Level13Controller.ArenaStartX)}_{Mathf.RoundToInt(Level13Controller.ShelfY)}"))
            .OverrideFailureMessage("The Mirror arena needs one unbroken floor.").IsNotNull();

        // The clone reuses BossData ranges, so its band has to fit the arena.
        var data = AuthoredResources.Load<BossData>(Level13Controller.MirrorResourcePath);
        float arenaWidth = Level13Controller.ArenaEndX - Level13Controller.ArenaStartX;
        AssertThat(data.RangedRangeThreshold * PixelsPerUnit < arenaWidth)
            .OverrideFailureMessage(
                $"Ranged band {data.RangedRangeThreshold * PixelsPerUnit} px does not fit a {arenaWidth} px arena.")
            .IsTrue();
        AssertThat(data.MeleeRangeThreshold < data.RangedRangeThreshold).IsTrue();
    }

    [TestCase]
    public void NoShiftingGravityAndNoRiftPocketReachesIntoTheMirrorArena() {
        using var fixture = new VoidFixture(null);
        Level13Controller level = fixture.Level;

        // The void stops interfering at the seam. A cycling field or a time pocket in
        // here would make the mirror match unfair in one direction or the other.
        bool sawArenaField = false;
        for (int index = 0; index < Level13Controller.GravityFieldSpans.Length; index++) {
            var span = Level13Controller.GravityFieldSpans[index];
            bool overlapsArena = span.EndX > Level13Controller.ArenaStartX;
            if (!overlapsArena) continue;
            sawArenaField = true;
            GravityFieldZone field = level.GravityFields[index];
            AssertThat(field.IsCycling)
                .OverrideFailureMessage($"Gravity field '{span.ID}' cycles inside the Mirror arena.")
                .IsFalse();
            AssertFloat(field.CurrentScale)
                .OverrideFailureMessage("The Mirror arena must run at Earth-normal gravity.")
                .IsEqualApprox(1f, 0.001f);
            AssertFloat(span.StartX)
                .OverrideFailureMessage("The stabilised pocket must begin exactly at the arena seam.")
                .IsEqualApprox(Level13Controller.ArenaStartX, 0.001f);
        }
        AssertThat(sawArenaField)
            .OverrideFailureMessage("The arena has no authored gravity state at all.").IsTrue();

        AssertThat(level.RiftPockets.Count).IsEqual(Level13Controller.RiftPocketNodeNames.Length);
        foreach (ChronalRiftZone rift in level.RiftPockets) {
            AssertThat(rift.Position.X + 240f < Level13Controller.ArenaStartX)
                .OverrideFailureMessage($"Rift pocket '{rift.RiftID}' reaches into the Mirror arena.")
                .IsTrue();
        }
    }

    // === Era identity: the rift pockets ===

    [TestCase]
    public void TheRiftPocketsTaxTheLowRoadWithTheAuthoredTimeLoopSnap() {
        using var fixture = new VoidFixture(null);
        AssertThat(fixture.Level.RiftPockets.Count).IsEqual(3);

        var seenIDs = new HashSet<string>();
        foreach (ChronalRiftZone rift in fixture.Level.RiftPockets) {
            AssertThat(seenIDs.Add(rift.RiftID))
                .OverrideFailureMessage($"Duplicate rift id '{rift.RiftID}'.").IsTrue();
            AssertThat(rift.RiftID.StartsWith("level_13.", StringComparison.Ordinal)).IsTrue();
            // The Package 1 contract: more than two seconds inside snaps the player
            // roughly three seconds into their own past, with damage.
            AssertFloat(rift.SnapDelay).IsEqualApprox(2f, 0.001f);
            AssertThat(rift.HistoryFramesAgo).IsEqual(180);
            AssertThat(rift.SnapDamage).IsGreater(0);
            // They sit on the shelf: the low road is the one that costs.
            AssertFloat(rift.Position.Y).IsGreater(Level13Controller.ShelfY - 200f);
        }
    }

    // === Era identity: gravity that changes during play ===

    [TestCase]
    public void TheGravityFieldsTileTheLevelWithoutOverlapping() {
        using var fixture = new VoidFixture(null);
        Level13Controller level = fixture.Level;

        AssertThat(level.GravityFields.Count).IsEqual(Level13Controller.GravityFieldSpans.Length);

        float cursor = 0f;
        for (int index = 0; index < Level13Controller.GravityFieldSpans.Length; index++) {
            var span = Level13Controller.GravityFieldSpans[index];
            GravityFieldZone field = level.GravityFields[index];
            AssertObject(field).IsNotNull();
            AssertString(field.FieldID).IsEqual(span.ID);

            // EnvironmentPlayerModifiers publishes the PRODUCT of every live source,
            // so two fields over one player would multiply into a scale nobody
            // authored (Levels 5, 8 and 12 all hit this). Contiguous, exactly.
            AssertFloat(span.StartX)
                .OverrideFailureMessage($"Gravity span '{span.ID}' overlaps or leaves a gap at x={cursor}.")
                .IsEqualApprox(cursor, 0.0001f);
            AssertThat(span.EndX > span.StartX).IsTrue();
            cursor = span.EndX;
        }
        AssertFloat(cursor)
            .OverrideFailureMessage("The gravity fields must cover the level end to end.")
            .IsEqualApprox(Level13Controller.LevelWidth, 0.0001f);

        // Every checkpoint and the fresh spawn resolve to exactly one field.
        var anchors = new List<float> { level.PlayerSpawnPosition.X };
        foreach (string checkpointID in Level13Controller.CheckpointIDs) {
            level.Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn);
            anchors.Add(respawn.X);
        }
        foreach (float x in anchors) {
            AssertObject(level.GravityFieldFor(x))
                .OverrideFailureMessage($"No gravity field covers x={x}.").IsNotNull();
        }
    }

    [TestCase]
    public void TheTwoRiftFieldsReallyCycleAndReallyTelegraphOnDistinctPeriods() {
        using var fixture = new VoidFixture(null);
        Level13Controller level = fixture.Level;

        var periods = new HashSet<float>();
        int cyclingFields = 0;
        for (int index = 0; index < Level13Controller.GravityFieldSpans.Length; index++) {
            var span = Level13Controller.GravityFieldSpans[index];
            GravityFieldZone field = level.GravityFields[index];
            if (span.CycleScales is not { Length: >= 2 }) continue;
            cyclingFields++;

            AssertThat(field.IsCycling)
                .OverrideFailureMessage($"Field '{span.ID}' was authored to cycle and does not.").IsTrue();
            // A shift the player cannot see coming is a coin flip, not a mechanic.
            AssertFloat(field.TelegraphSeconds)
                .OverrideFailureMessage($"Field '{span.ID}' shifts with no telegraph.").IsGreater(0.5f);
            AssertThat(field.TelegraphSeconds < field.CycleSeconds).IsTrue();
            // Distinct periods, so the level is two interleaved rhythms and not one
            // metronome (the L09 searchlight precedent).
            AssertThat(periods.Add(field.CycleSeconds))
                .OverrideFailureMessage($"Field '{span.ID}' shares its period with another field.").IsTrue();

            // Every scale is distinct and lighter than Earth: a cycle that could get
            // heavier than 1.0 could not satisfy the reachability contract below.
            var scales = new HashSet<float>();
            foreach (float scale in span.CycleScales) {
                AssertThat(scales.Add(scale))
                    .OverrideFailureMessage($"Field '{span.ID}' repeats the scale {scale}.").IsTrue();
                AssertFloat(scale).IsGreater(0f);
                AssertFloat(scale)
                    .OverrideFailureMessage($"Field '{span.ID}' reaches {scale}, heavier than Earth.")
                    .IsLess(1f);
            }

            // Driving the component proves the shift really lands rather than just
            // reading back the exports.
            float opening = field.CurrentScale;
            field.AdvanceCycle();
            AssertFloat(field.CurrentScale)
                .OverrideFailureMessage($"Field '{span.ID}' did not change scale on a cycle advance.")
                .IsNotEqual(opening);
        }
        AssertThat(cyclingFields)
            .OverrideFailureMessage("The Void's signature mechanic is two cycling gravity fields.")
            .IsEqual(2);
    }

    [TestCase]
    public void EveryAuthoredClimbIsMakeableAtEveryScaleInTheGravityCycle() {
        // The contract that keeps a gravity retune honest. Gravity here changes
        // *during* play, so a climb is only real if it is makeable at every scale the
        // containing field can publish - checked against the weakest of the nine
        // characters, derived from their .tres stats and PlayerController's authored
        // jump physics rather than from a magic number.
        foreach ((string ladderID, float startY, string[] motifIDs) in Level13Controller.Ladders) {
            AssertThat(motifIDs.Length).IsGreater(0);
            float standingY = startY;
            foreach (string motifID in motifIDs) {
                float y = Level13Controller.MotifY(motifID);
                AssertThat(float.IsNaN(y))
                    .OverrideFailureMessage($"Ladder '{ladderID}' names an unauthored motif '{motifID}'.")
                    .IsFalse();
                float rise = standingY - y;
                AssertThat(rise > 0f)
                    .OverrideFailureMessage($"Ladder '{ladderID}' does not climb at '{motifID}'.").IsTrue();
                AssertReachable(rise, Level13Controller.MotifX(motifID),
                    $"ladder '{ladderID}' rung '{motifID}'");
                standingY = y;
            }
        }

        // The drifting shards are checked the same way, but their deck altitudes come
        // from the scene's own waypoints, so re-authoring a drift re-checks the hop
        // onto it and the hop off it.
        PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
        Node instance = scene.Instantiate();
        try {
            foreach ((string nodeName, string shardID, string boardMotif, string landMotif, string _)
                     in Level13Controller.Shards) {
                var shard = instance.GetNodeOrNull<PathMovingPlatform>(nodeName);
                AssertObject(shard).IsNotNull();
                AssertString(shard.PlatformID).IsEqual(shardID);
                AssertThat(shard.Waypoints.Length).IsGreaterEqual(2);

                float lowDeckY = float.MinValue;
                float highDeckY = float.MaxValue;
                foreach (Vector2 waypoint in shard.Waypoints) {
                    float deckY = shard.Position.Y + waypoint.Y;
                    lowDeckY = Mathf.Max(lowDeckY, deckY);
                    highDeckY = Mathf.Min(highDeckY, deckY);
                }

                float boardY = Level13Controller.MotifY(boardMotif);
                float landY = Level13Controller.MotifY(landMotif);
                AssertThat(float.IsNaN(boardY) || float.IsNaN(landY))
                    .OverrideFailureMessage($"Shard '{nodeName}' names an unauthored motif.").IsFalse();

                // Boarding it at its lowest, and stepping off it at its highest.
                AssertReachable(boardY - lowDeckY, shard.Position.X, $"boarding shard '{nodeName}'");
                AssertReachable(highDeckY - landY, shard.Position.X, $"leaving shard '{nodeName}'");
            }
        } finally {
            instance.Free();
        }
    }

    [TestCase]
    public void TheShiftingGravityIsLoadBearingAndNotDecoration() {
        // The mirror of the contract above: if every climb also worked at Earth-normal
        // then the void's gravity would be scenery. The tallest authored rung must be
        // out of the heaviest character's ordinary reach.
        float tallestRise = 0f;
        foreach ((string _, float startY, string[] motifIDs) in Level13Controller.Ladders) {
            float standingY = startY;
            foreach (string motifID in motifIDs) {
                float y = Level13Controller.MotifY(motifID);
                tallestRise = Mathf.Max(tallestRise, standingY - y);
                standingY = y;
            }
        }
        float earthReach = WorstRosterJumpRise(1f);
        AssertThat(tallestRise > earthReach)
            .OverrideFailureMessage(
                $"The tallest rung is {tallestRise} px and the heaviest character clears {earthReach} px " +
                "on Earth - the Void's gravity is not load-bearing here.")
            .IsTrue();

        // And the shelf is always there: no climb is ever the only way forward, so a
        // heavy phase is a delay and never a soft-lock.
        using var fixture = new VoidFixture(null);
        foreach (float x in new[] { 0f, Level13Controller.Room2StartX, Level13Controller.ArenaStartX }) {
            AssertObject(fixture.Level.GetNodeOrNull<StaticBody2D>(
                    $"Floor_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(Level13Controller.ShelfY)}"))
                .OverrideFailureMessage($"The sediment shelf is missing its slab at x={x}.")
                .IsNotNull();
        }
    }

    [TestCase]
    public void EveryCheckpointResumeWakesThePlayerInTheRightGravityState() {
        foreach (string checkpointID in Level13Controller.CheckpointIDs) {
            using var fixture = new VoidFixture(new StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = StoryManager.GetLevelScenePath(CampaignLevel.ChronalVoid),
                LastCheckpointID = checkpointID,
                CurrentHP = 70,
                CurrentUltimateMeter = 25f
            });
            Level13Controller level = fixture.Level;

            AssertThat(level.ResumedMidLevel)
                .OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            AssertString(level.ResumedCheckpointID).IsEqual(checkpointID);

            // An Area2D reports its authored overlaps on the first physics frame and a
            // resume teleports before one runs, so the level registers the player
            // explicitly. The resumed scale must be the containing field's *current*
            // cycle scale, not its static default and not Earth-normal.
            GravityFieldZone expected = level.GravityFieldFor(level.Player.Position.X);
            AssertObject(expected)
                .OverrideFailureMessage($"'{checkpointID}' resumes outside every gravity field.").IsNotNull();
            AssertFloat(level.Player.EnvironmentGravityScale)
                .OverrideFailureMessage($"'{checkpointID}' resumed at the wrong gravity.")
                .IsEqualApprox(expected.CurrentScale, 0.001f);
            AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(level.Player))
                .OverrideFailureMessage("Exactly one gravity field may own the player at a time.")
                .IsEqual(1);

            // Waves behind the checkpoint stay cleared instead of replaying.
            if (checkpointID == Level13Controller.Checkpoint0) {
                AssertThat(level.HasWaveSpawned(2)).IsFalse();
            } else {
                AssertThat(level.HasWaveSpawned(1)).IsTrue();
                AssertThat(level.HasWaveSpawned(2)).IsTrue();
            }
            if (checkpointID == Level13Controller.Checkpoint2) {
                AssertThat(level.HasWaveSpawned(3)).IsTrue();
            }
            AssertThat(level.MirrorIntroShown).IsFalse();
        }
    }

    [TestCase]
    public void TheGravityFollowsThePlayerAcrossTheSeamsAndReleasesCleanly() {
        using var fixture = new VoidFixture(null);
        Level13Controller level = fixture.Level;
        PlayerController player = level.Player;
        AssertObject(player).IsNotNull();

        GravityFieldZone threshold = level.GravityFieldFor(500f);
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("Level 13 must open inside the Threshold field.")
            .IsEqualApprox(threshold.CurrentScale, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

        // Walking into the arena hands the player to the stabilised pocket and to that
        // pocket only - never to both, which would multiply.
        player.Position = new Vector2(Level13Controller.ArenaCenterX, Level13Controller.ShelfY - 60f);
        level.SyncGravityFieldToPlayer();
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("The Mirror arena must be Earth-normal.").IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(1);

        // A Chronal Rewind teleports without a physics step in between.
        player.Position = new Vector2(4200f, Level13Controller.ShelfY - 60f);
        EventBus.Instance.RaiseRewindTriggered(player.Position);
        GravityFieldZone drift = level.GravityFieldFor(4200f);
        AssertFloat(player.EnvironmentGravityScale)
            .OverrideFailureMessage("A rewind back into the rift must restore the drift field's scale.")
            .IsEqualApprox(drift.CurrentScale, 0.001f);

        // Leaving every field restores Earth-normal exactly, with nothing left over.
        foreach (GravityFieldZone field in level.GravityFields) field.RemovePlayer(player);
        AssertFloat(player.EnvironmentGravityScale).IsEqualApprox(1f, 0.001f);
        AssertThat(EnvironmentPlayerModifiers.GravitySourceCount(player)).IsEqual(0);
    }

    // === Era identity: the mashup ===

    [TestCase]
    public void TheGeometryRemixesMotifsFromAtLeastEightEarlierEras() {
        using var fixture = new VoidFixture(null);

        // Every settled fragment is a distinct era caption, and every one of them is
        // really built. This is the level's whole visual thesis; a pass that collapses
        // it into anonymous grey slabs has to fail.
        var captions = new HashSet<string>();
        foreach ((string motifID, float x, float y, float _, string labelKey, Color _) in Level13Controller.EraMotifs) {
            AssertThat(captions.Add(labelKey))
                .OverrideFailureMessage($"Motif '{motifID}' repeats the era caption '{labelKey}'.").IsTrue();
            AssertObject(fixture.Level.GetNodeOrNull<Label>($"MotifLabel_{motifID}"))
                .OverrideFailureMessage($"Motif '{motifID}' was never built.").IsNotNull();
            AssertObject(fixture.Level.GetNodeOrNull<OneWayPlatform>(
                    $"OneWay_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}"))
                .OverrideFailureMessage($"Motif '{motifID}' has no platform at ({x}, {y}).").IsNotNull();
        }
        AssertThat(captions.Count)
            .OverrideFailureMessage("The Void should remix motifs from at least eight earlier levels.")
            .IsGreaterEqual(8);

        // ...plus the four fragments that have not settled yet.
        AssertThat(fixture.Level.DriftingShards.Count).IsEqual(4);
        foreach (PathMovingPlatform shard in fixture.Level.DriftingShards) {
            AssertThat(shard.Waypoints.Length).IsGreaterEqual(2);
            AssertFloat(shard.Speed).IsGreater(0f);
        }
    }

    [TestCase]
    public void EveryRoomConfinesTheCameraToAtLeastTheReferenceViewport() {
        using var fixture = new VoidFixture(null);
        AssertThat(fixture.Level.RoomTriggers.Count).IsGreater(2);
        foreach (RoomTransitionTrigger trigger in fixture.Level.RoomTriggers) {
            AssertThat(trigger.CameraBounds.Size.X)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines the camera below 1920 px wide.")
                .IsGreaterEqual(1920f);
            AssertThat(trigger.CameraBounds.Size.Y)
                .OverrideFailureMessage($"Room '{trigger.RoomID}' confines the camera below 1080 px tall.")
                .IsGreaterEqual(1080f);
            AssertThat(trigger.CameraBounds.Position.X).IsGreaterEqual(-200f);
            AssertThat(trigger.CameraBounds.End.X).IsLessEqual(Level13Controller.LevelWidth + 200f);
            AssertThat(trigger.CameraBounds.End.Y).IsLessEqual(Level13Controller.LevelHeight + 1f);
        }
    }

    // === Helpers ===

    /// <summary>
    /// Fails unless <paramref name="rise"/> is inside the weakest of the nine
    /// characters' reach at EVERY gravity scale the field containing
    /// <paramref name="atX"/> can publish. A descent (negative rise) always passes.
    /// </summary>
    private static void AssertReachable(float rise, float atX, string what) {
        foreach (float scale in ScalesAt(atX)) {
            float reach = WorstRosterJumpRise(scale);
            AssertThat(rise <= reach)
                .OverrideFailureMessage(
                    $"The {what} asks for a {rise} px rise, but at gravity scale {scale} the heaviest " +
                    $"character only clears {reach} px.")
                .IsTrue();
        }
    }

    /// <summary>Every scale the authored field covering <paramref name="x"/> can publish.</summary>
    private static float[] ScalesAt(float x) {
        for (int index = 0; index < Level13Controller.GravityFieldSpans.Length; index++) {
            var span = Level13Controller.GravityFieldSpans[index];
            bool isLast = index == Level13Controller.GravityFieldSpans.Length - 1;
            if (x >= span.StartX && (isLast ? x <= span.EndX : x < span.EndX)) {
                return Level13Controller.ScalesOf(span);
            }
        }
        AssertThat(false).OverrideFailureMessage($"No authored gravity field covers x={x}.").IsTrue();
        return new[] { 1f };
    }

    /// <summary>Reach of the worst-served character on the roster at a given gravity scale.</summary>
    private static float WorstRosterJumpRise(float gravityScale) {
        float worst = float.MaxValue;
        foreach (string id in RosterIDs) worst = Mathf.Min(worst, SingleJumpRise(LoadCharacter(id), gravityScale));
        return worst;
    }

    /// <summary>
    /// Closed-form jump apex for PlayerController's authored physics (the L12
    /// derivation): PerformJump sets vy = -MaxJumpForce * 54, ApplyGravity
    /// accelerates at BaseGravity(18) * (0.8 + 0.4 * Weight) *
    /// EnvironmentGravityScale * 60 px/s^2 while the jump is held, so a held full
    /// jump rises v^2 / 2a.
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

    private static int CountOfType<T>(Node root) where T : Node {
        int total = root is T ? 1 : 0;
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) total += CountOfType<T>(child);
        return total;
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
    /// Builds the authored scene in the runner tree against an optional scratch save,
    /// then hands every shared singleton back untouched: session slot, character,
    /// difficulty, save row, pooled enemies, and the gameplay pause flag. A leaked
    /// pause would freeze GdUnit's own transport node (CLAUDE.md signature 4), and
    /// difficulty is pinned to Normal because the Mirror's HP is scaled from it.
    /// </summary>
    private sealed class VoidFixture : IDisposable {
        public readonly Level13Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly Difficulty _originalDifficulty;
        private readonly StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public VoidFixture(StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];

            GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
            if (save != null) SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level13Controller>();
            Level.Name = "Level_13_ChronalVoid_Test";
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
            GameManager.Instance.CurrentSession.Difficulty = _originalDifficulty;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }
}
