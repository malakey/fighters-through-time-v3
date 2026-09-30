using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W1 — A04 (Fighter only). When a fighter loses a stock, every
/// persistent object and attack zone it owns despawns on the same tick, with
/// no final attack and no pending damage; projectiles already in flight finish,
/// and the opponent's objects are untouched. Each case drives the owner through
/// the legacy flat arena's drop-through floor into the bottom blast zone.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterStockLossDespawnTests {

    [TestCase]
    public void AFallenOwnersConstructsDespawnOnTheKoTickAndTheOpponentsSurvive() {
        FighterLoadout tesla = Load("tesla");
        var simulation = new FighterSimulation(tesla, tesla, seed: 1311, spawnDistance: 6,
            rules: FighterMatchRules.Disabled);
        // Both Teslas plant a coil.
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.Special1));
        AssertThat(OwnedObjects(simulation, 0)).IsEqual(1);
        AssertThat(OwnedObjects(simulation, 1)).IsEqual(1);

        int koTick = DropOwnerOffTheBottom(simulation, 1, player: 0);
        AssertThat(koTick > 0).IsTrue();
        AssertThat(OwnedObjects(simulation, 0))
            .OverrideFailureMessage("A04: the fallen owner's coil despawns on the KO tick.")
            .IsEqual(0);
        AssertThat(OwnedObjects(simulation, 1))
            .OverrideFailureMessage("The opponent's coil is untouched.")
            .IsEqual(1);
    }

    [TestCase]
    public void AFallenOwnersZoneDespawnsOnTheKoTick() {
        var simulation = new FighterSimulation(Load("einstein"), Load("joan"), seed: 1312, spawnDistance: 6,
            rules: FighterMatchRules.Disabled);
        // Relativity Rift is Einstein's Area special (a component 309 zone).
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        AssertThat(simulation.ZoneCount)
            .OverrideFailureMessage("setup: Relativity Rift must open a zone.")
            .IsEqual(1);
        DropOwnerOffTheBottom(simulation, 1, player: 0);
        AssertThat(simulation.ZoneCount)
            .OverrideFailureMessage("A04: the fallen owner's zone despawns on the KO tick.")
            .IsEqual(0);
    }

    [TestCase]
    public void AProjectileAlreadyInFlightFinishes() {
        var simulation = new FighterSimulation(Load("mozart"), Load("joan"), seed: 1313, spawnDistance: 10,
            rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.ProjectileCount)
            .OverrideFailureMessage("setup: Requiem Chord must be in flight.")
            .IsEqual(1);
        int projectilesAtKo = -1;
        DropOwnerOffTheBottom(simulation, 1, player: 0, onKo: () => projectilesAtKo = simulation.ProjectileCount);
        AssertThat(projectilesAtKo)
            .OverrideFailureMessage("A04: projectiles already in flight are not despawned by the owner's KO.")
            .IsEqual(1);
    }

    private static FighterLoadout Load(string characterID) =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres"));

    private static int OwnedObjects(FighterSimulation simulation, int owner) {
        var objects = new List<FighterPersistentObjectComponent>();
        simulation.CopyPersistentObjectsTo(objects);
        int count = 0;
        foreach (FighterPersistentObjectComponent persistent in objects) {
            if (persistent.OwnerPlayerID == owner) count++;
        }
        return count;
    }

    /// <summary>
    /// Taps Down+Jump to drop through the legacy flat floor and rides the fall
    /// past the bottom blast zone. Returns the KO tick (the tick whose Advance
    /// raised the owner's KnockoutsSuffered); every assertion reads the state
    /// straight after that same Advance.
    /// </summary>
    private static int DropOwnerOffTheBottom(
        FighterSimulation simulation, int startTick, int player, System.Action onKo = null) {
        simulation.TryGetFighterRuntime(player, out FighterRuntimeComponent before);
        int kos = before.KnockoutsSuffered;
        for (int tick = startTick; tick < startTick + 240; tick++) {
            PlayerInputFrame falling = tick == startTick
                ? Frame(tick, GameplayButtons.Jump | GameplayButtons.Down)
                : Frame(tick, GameplayButtons.Down);
            simulation.Advance(
                player == 0 ? falling : Frame(tick, GameplayButtons.None),
                player == 1 ? falling : Frame(tick, GameplayButtons.None));
            simulation.TryGetFighterRuntime(player, out FighterRuntimeComponent runtime);
            if (runtime.KnockoutsSuffered > kos) {
                onKo?.Invoke();
                return tick;
            }
        }
        AssertThat(false).OverrideFailureMessage("The owner never fell through the bottom blast zone.").IsTrue();
        return -1;
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
