using System;
using System.Collections.Generic;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// F12 — the Eraser debut route trigger (V7.6, Package 11 A12). Built for the
/// per-character Level 4A; Package 13 W2 (S27) moved it to Level 5, where the
/// Level 0 watcher drops onto the Titanic's listing boat deck.
///
/// <para>The trigger's whole authority is "spawn the ambush and raise the bark". The
/// three rules worth pinning are the ones a later change could quietly violate: it
/// grants no checkpoint benefit of any kind, a reconstruction rebuilds it exactly
/// once without duplicate live waves, and the <b>presentation</b> flag
/// (<c>BarkSeen</c>) is kept strictly separate from encounter state — a persistent
/// "trigger seen" flag that suppressed required enemies would strand the route gate
/// forever.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EraserDebutTriggerTests {

    [TestCase]
    public void TheDebutGrantsNoCheckpointMendingRewindRefillOrIntegrityLock() {
        using var fixture = new TriggerFixture();

        // The type carries no checkpoint surface at all: it is a route trigger, not
        // a fracture. Nothing on it registers a respawn anchor, heals, refills the
        // rewind pool or freezes the Integrity clock.
        AssertThat(typeof(CheckpointTrigger).IsAssignableFrom(fixture.Trigger.GetType()))
            .OverrideFailureMessage("The Eraser debut must not be a checkpoint.").IsFalse();
        AssertObject(fixture.Trigger.GetNodeOrNull<Node>("StrikeSurface")).IsNull();

        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(1);

        // Its timing depends on neither difficulty nor a middle checkpoint — the
        // trigger reads neither.
        AssertString(fixture.Trigger.TriggerID).IsEqual(Level05Controller.EraserDebutTriggerID);
    }

    [TestCase]
    public void RepeatedCrossingsNeverSpawnDuplicateLiveWaves() {
        using var fixture = new TriggerFixture();

        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(1);

        // Walking back and forth across a live ambush spawns nothing more.
        for (int crossing = 0; crossing < 4; crossing++) {
            AssertThat(fixture.Trigger.SpawnEraserDebut())
                .OverrideFailureMessage("A live ambush must never be duplicated.").IsFalse();
        }
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(1);

        // And a cleared encounter never respawns.
        fixture.Trigger.MarkClearedForTest();
        AssertThat(fixture.Trigger.EncounterCleared).IsTrue();
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsFalse();
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(1);
    }

    [TestCase]
    public void APreBossReconstructionCompletesTheApproachWhileAnEarlierOneRebuildsIt() {
        using var fixture = new TriggerFixture();
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
        AssertThat(fixture.Trigger.TryClaimReward()).IsTrue();

        // Reloading at Entry: the ambush is rebuilt and must remain triggerable —
        // a stranded gate here is unrecoverable without a Restart Level.
        fixture.Trigger.RestoreFromCheckpoint(approachComplete: false, rewardAlreadyClaimed: true);
        AssertThat(fixture.Trigger.EncounterCleared).IsFalse();
        AssertThat(fixture.Trigger.EncounterLive).IsFalse();
        AssertThat(fixture.Trigger.SpawnEraserDebut())
            .OverrideFailureMessage("A reconstructed ambush must still be triggerable.").IsTrue();
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(2);

        // Already-collected rewards remain claimed across the rebuild.
        AssertThat(fixture.Trigger.RewardClaimed).IsTrue();
        AssertThat(fixture.Trigger.TryClaimReward())
            .OverrideFailureMessage("A claimed reward is never re-issued.").IsFalse();

        // After PreBoss the saved baseline treats the whole required approach as
        // complete, so the ambush does not come back at all.
        fixture.Trigger.RestoreFromCheckpoint(approachComplete: true, rewardAlreadyClaimed: true);
        AssertThat(fixture.Trigger.EncounterCleared).IsTrue();
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsFalse();
        AssertThat(fixture.Trigger.SpawnCount).IsEqual(2);
    }

    [TestCase]
    public void TheFirstViewPresentationFlagIsSeparateFromEncounterState() {
        using var fixture = new TriggerFixture();
        AssertThat(fixture.Trigger.BarkSeen).IsFalse();

        fixture.Trigger.SpawnEraserDebut();
        AssertThat(fixture.Trigger.BarkSeen).IsTrue();

        // A reconstruction resets the ENCOUNTER but leaves the presentation history
        // alone in both directions: the line has been heard, the ambush has not been
        // fought, and neither fact may imply the other.
        fixture.Trigger.RestoreFromCheckpoint(approachComplete: false, rewardAlreadyClaimed: false);
        AssertThat(fixture.Trigger.BarkSeen)
            .OverrideFailureMessage("Presentation history is not encounter state.").IsTrue();
        AssertThat(fixture.Trigger.EncounterCleared)
            .OverrideFailureMessage("A seen bark must never suppress the required encounter.").IsFalse();
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
    }

    [TestCase]
    public void TimeFreezeCannotActivateTheEncounter() {
        using var fixture = new TriggerFixture();
        EraserDebutTrigger.WorldTimeSuspendedProbe = static () => true;
        try {
            AssertThat(fixture.Trigger.SpawnEraserDebut())
                .OverrideFailureMessage("Time Freeze cannot activate the debut encounter.").IsFalse();
            AssertThat(fixture.Trigger.SpawnCount).IsEqual(0);
        } finally {
            EraserDebutTrigger.WorldTimeSuspendedProbe = static () => false;
        }
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
    }

    [TestCase]
    public void TheDebutSpawnsTheRealEraserNowThatA7aHasLanded() {
        // A12 reserved this constant against a placeholder body; A7a (Wave 2)
        // shipped the enemy and re-pointed it. The spelling is unbound_eraser, not
        // the bare "eraser" originally reserved: two BOSSES already carry that
        // word (tidal_eraser at L5, apex_eraser at L15), and a bare ID would make
        // every grep and the VFX library's owner parse ambiguous against them.
        AssertString(EraserDebutTrigger.EraserEnemyID).IsEqual("unbound_eraser");
        AssertThat(FileAccess.FileExists(
            $"res://resources/Enemies/{EraserDebutTrigger.EraserEnemyID}.tres"))
            .OverrideFailureMessage("The debut's enemy must be an authored resource.")
            .IsTrue();

        // The DEFAULT is the real Eraser, so Level 5 gets it with no override.
        // The old interim body survives only as a documented fallback an
        // authored scene may still select.
        var defaultTrigger = AutoFree(new EraserDebutTrigger())!;
        AssertString(defaultTrigger.EnemyID).IsEqual(EraserDebutTrigger.EraserEnemyID);
        AssertString(EraserDebutTrigger.PlaceholderEnemyID).IsEqual("chrono_guard_elite");

        // And it really is the elite the design describes: Story-only, 190 HP,
        // stun resistance 0.5, with the Suppression lance and the Siphon channel.
        var eraser = FTT.Core.AuthoredResources.Load<FTT.Enemies.EnemyData>(
            $"res://resources/Enemies/{EraserDebutTrigger.EraserEnemyID}.tres");
        AssertObject(eraser).IsNotNull();
        AssertThat(eraser.Tier).IsEqual(FTT.Enemies.EnemyTier.Elite);
        AssertThat(eraser.MaxHP).IsEqual(190);
        AssertThat(eraser.EliteAbilities.Length).IsEqual(2);
        AssertThat(eraser.EliteAbilities[0].AppliedStatus).IsEqual(FTT.Core.StatusType.Suppression);
        AssertThat(eraser.EliteAbilities[1].Archetype)
            .IsEqual(FTT.Enemies.EnemyAbilityArchetype.SiphonTether);

        TranslationServer.SetLocale("en");
        AssertThat(TranslationServer.Translate(EraserDebutTrigger.DebutBarkKey).ToString())
            .IsNotEqual(EraserDebutTrigger.DebutBarkKey);
    }

    // === Level 5 placement (Package 13 W2, S27) ===

    [TestCase]
    public void TheTitanicPlacesTheDebutOnTheBoatDeckBetweenTheMiddleAndPreBossAnchors() {
        using var fixture = new TitanicFixture(null);
        Level05Controller level = fixture.Level;
        EraserDebutTrigger debut = level.EraserDebut;
        AssertObject(debut).OverrideFailureMessage("Level 5 builds no Eraser debut.").IsNotNull();
        AssertString(debut.TriggerID).IsEqual($"{Level05Controller.TitanicLevelID}_eraser_debut");
        AssertString(debut.EnemyID).IsEqual(EraserDebutTrigger.EraserEnemyID);

        // An independent route encounter between the Middle and PreBoss anchors,
        // never a checkpoint benefit and never timed by difficulty.
        AssertThat(level.Levels.TryGetCheckpointPosition(Level05Controller.Checkpoint1, out Vector2 middle)).IsTrue();
        AssertThat(level.Levels.TryGetCheckpointPosition(Level05Controller.Checkpoint2, out Vector2 preBoss)).IsTrue();
        float x = debut.GlobalPosition.X;
        AssertThat(x > middle.X && x < preBoss.X)
            .OverrideFailureMessage($"The debut at x={x} is not between Middle ({middle.X}) and PreBoss ({preBoss.X}).")
            .IsTrue();
        // The watcher drops in from above, onto the deck ahead of the player.
        AssertThat(debut.SpawnOffset.Y < 0f).IsTrue();
        AssertThat(debut.GlobalPosition.X + debut.SpawnOffset.X < preBoss.X).IsTrue();

        // Encounter baselines: the Middle anchor precedes it (still live); the
        // PreBoss anchor restores it as fought.
        IReadOnlyDictionary<string, string[]> map = level.AuthoredEncounterBaselines;
        AssertThat(new List<string>(map[Level05Controller.Checkpoint1]).Contains(debut.TriggerID)).IsFalse();
        AssertThat(new List<string>(map[Level05Controller.Checkpoint2]).Contains(debut.TriggerID)).IsTrue();
    }

    [TestCase]
    public void ResumingAtTheTitanicsAnchorsRebuildsOrCompletesTheDebutPerTheBaseline() {
        foreach (string checkpointID in new[] { Level05Controller.Checkpoint1, Level05Controller.Checkpoint2 }) {
            using var fixture = new TitanicFixture(new FTT.Core.StorySaveData {
                SelectedCharacterID = "einstein",
                CurrentLevelID = FTT.Core.StoryManager.GetLevelScenePath(FTT.Core.CampaignLevel.Titanic),
                LastCheckpointID = checkpointID,
                CurrentHP = 70
            });
            Level05Controller level = fixture.Level;
            AssertThat(level.ResumedMidLevel).OverrideFailureMessage($"'{checkpointID}' did not restore.").IsTrue();
            bool preBoss = checkpointID == Level05Controller.Checkpoint2;
            AssertThat(level.EraserDebut.EncounterCleared)
                .OverrideFailureMessage(preBoss
                    ? "The PreBoss baseline must restore the debut as fought."
                    : "A reload at the Middle anchor must leave the debut triggerable.")
                .IsEqual(preBoss);
            AssertThat(level.EraserDebut.RewardClaimed).IsEqual(preBoss);
            AssertThat(level.EraserDebut.SpawnEraserDebut()).IsEqual(!preBoss);
        }
    }

    // === Fixtures ===

    /// <summary>The authored Titanic, slotless unless a save is supplied (scratch slot 2).</summary>
    private sealed class TitanicFixture : IDisposable {
        private const int ScratchSlot = 2;
        public readonly Level05Controller Level;
        private readonly int _originalSlot;
        private readonly string _originalCharacter;
        private readonly FTT.Core.StorySaveData _originalSave;
        private readonly bool _originalPaused;

        public TitanicFixture(FTT.Core.StorySaveData save) {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = FTT.Core.GameManager.Instance.CurrentSession.ActiveSaveSlot;
            _originalCharacter = FTT.Core.GameManager.Instance.CurrentSession.SelectedCharacterID;
            _originalSave = FTT.Core.SaveManager.Instance.SaveSlots[ScratchSlot];
            EraserDebutTrigger.WorldTimeSuspendedProbe = static () => false;

            FTT.Core.GameManager.Instance.CurrentSession.ActiveSaveSlot = save == null ? -1 : ScratchSlot;
            FTT.Core.GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            if (save != null) FTT.Core.SaveManager.Instance.SaveSlots[ScratchSlot] = save;

            Level = ResourceLoader.Load<PackedScene>("res://scenes/campaign/Level_05_Titanic.tscn")
                .Instantiate<Level05Controller>();
            Level.Name = "Level_05_Titanic_DebutTest";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            FTT.Core.PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            FTT.Core.SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            FTT.Core.GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            FTT.Core.GameManager.Instance.CurrentSession.SelectedCharacterID = _originalCharacter;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }

    private sealed class TriggerFixture : IDisposable {
        public readonly EraserDebutTrigger Trigger;

        public TriggerFixture() {
            EraserDebutTrigger.WorldTimeSuspendedProbe = static () => false;
            Trigger = new EraserDebutTrigger {
                Name = "TestEraserDebut",
                TriggerID = Level05Controller.EraserDebutTriggerID
            };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Trigger);
        }

        public void Dispose() {
            EraserDebutTrigger.WorldTimeSuspendedProbe = static () => false;
            if (GodotObject.IsInstanceValid(Trigger)) {
                FTT.Core.PoolManager.Instance?.ReleaseActiveUnder(Trigger);
                Trigger.Free();
            }
        }
    }
}
