using System;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// F12 — the Level 4A Eraser debut route trigger (V7.6, Package 11 A12).
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

        // Its timing depends on neither difficulty nor a middle checkpoint — 4A has
        // no middle checkpoint to depend on, and the trigger reads neither.
        AssertString(fixture.Trigger.TriggerID).IsEqual("level_04a_test_eraser_debut");
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
        NexusResonanceSource.WorldTimeSuspendedProbe = static () => true;
        try {
            AssertThat(fixture.Trigger.SpawnEraserDebut())
                .OverrideFailureMessage("Time Freeze cannot activate the debut encounter.").IsFalse();
            AssertThat(fixture.Trigger.SpawnCount).IsEqual(0);
        } finally {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
        }
        AssertThat(fixture.Trigger.SpawnEraserDebut()).IsTrue();
    }

    [TestCase]
    public void TheInterimBodyIsAnAuthoredEliteAndTheEraserIDIsReserved() {
        // A7a (Wave 2) ships resources/Enemies/eraser.tres; until then the debut
        // runs against an authored elite that exists today, and B3 re-points all
        // nine variants in one change.
        AssertString(EraserDebutTrigger.EraserEnemyID).IsEqual("eraser");
        AssertString(EraserDebutTrigger.PlaceholderEnemyID).IsEqual("chrono_guard_elite");
        AssertThat(FileAccess.FileExists(
            $"res://resources/Enemies/{EraserDebutTrigger.PlaceholderEnemyID}.tres"))
            .OverrideFailureMessage("The interim Eraser body must be an enemy resource that exists.")
            .IsTrue();

        TranslationServer.SetLocale("en");
        AssertThat(TranslationServer.Translate(EraserDebutTrigger.DebutBarkKey).ToString())
            .IsNotEqual(EraserDebutTrigger.DebutBarkKey);
    }

    // === Fixture ===

    private sealed class TriggerFixture : IDisposable {
        public readonly EraserDebutTrigger Trigger;

        public TriggerFixture() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            Trigger = new EraserDebutTrigger {
                Name = "TestEraserDebut",
                TriggerID = "level_04a_test_eraser_debut"
            };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(Trigger);
        }

        public void Dispose() {
            NexusResonanceSource.WorldTimeSuspendedProbe = static () => false;
            if (GodotObject.IsInstanceValid(Trigger)) {
                FTT.Core.PoolManager.Instance?.ReleaseActiveUnder(Trigger);
                Trigger.Free();
            }
        }
    }
}
