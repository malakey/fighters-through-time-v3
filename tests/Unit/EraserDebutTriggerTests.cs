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

        // The DEFAULT is the real Eraser, so every Level 4A variant gets it with
        // no per-variant change. The old interim body survives only as a
        // documented fallback an authored scene may still select.
        AssertString(new EraserDebutTrigger().EnemyID).IsEqual(EraserDebutTrigger.EraserEnemyID);
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
