using System.Collections.Generic;
using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A3: the shared particle cap. AGENTS.md budgets 500 simultaneously
/// active particles; before this the baseline runner measured the number but
/// nothing enforced it.
/// </summary>
[TestSuite]
public class ParticleBudgetRegistryTests {

    [TestCase]
    public void ReservationsAccumulateUntilTheBudgetIsReached() {
        var registry = new ParticleBudgetRegistry(100);
        AssertThat(registry.TryReserve(1UL, 40)).IsTrue();
        AssertThat(registry.TryReserve(2UL, 40)).IsTrue();
        AssertThat(registry.ActiveParticles).IsEqual(80);
        AssertThat(registry.ActiveEmitters).IsEqual(2);
    }

    [TestCase]
    public void ReleaseReturnsTheReservedParticlesToTheBudget() {
        var registry = new ParticleBudgetRegistry(100);
        registry.TryReserve(1UL, 60);
        AssertThat(registry.Release(1UL)).IsTrue();
        AssertThat(registry.ActiveParticles).IsEqual(0);
        AssertThat(registry.IsReserved(1UL)).IsFalse();
        // A second release is a no-op rather than a negative balance.
        AssertThat(registry.Release(1UL)).IsFalse();
        AssertThat(registry.ActiveParticles).IsEqual(0);
    }

    [TestCase]
    public void TheDefaultPolicyStealsTheOldestReservationsToMakeRoom() {
        var registry = new ParticleBudgetRegistry(100);
        registry.TryReserve(1UL, 50);
        registry.TryReserve(2UL, 40);

        AssertThat(registry.TryReserve(3UL, 50, out IReadOnlyList<ulong> evicted)).IsTrue();
        AssertThat(evicted.Count).IsEqual(1);
        AssertThat(evicted[0]).IsEqual(1UL);
        AssertThat(registry.IsReserved(1UL)).IsFalse();
        AssertThat(registry.IsReserved(2UL)).IsTrue();
        AssertThat(registry.ActiveParticles).IsEqual(90);
    }

    [TestCase]
    public void TheRefusePolicyDropsTheNewRequestInstead() {
        var registry = new ParticleBudgetRegistry(100, ParticleBudgetPolicy.Refuse);
        registry.TryReserve(1UL, 80);
        AssertThat(registry.TryReserve(2UL, 40, out IReadOnlyList<ulong> evicted)).IsFalse();
        AssertThat(evicted.Count).IsEqual(0);
        AssertThat(registry.IsReserved(1UL)).IsTrue();
        AssertThat(registry.ActiveParticles).IsEqual(80);
    }

    [TestCase]
    public void AnEmitterLargerThanTheWholeBudgetIsAlwaysRefused() {
        var registry = new ParticleBudgetRegistry(100);
        registry.TryReserve(1UL, 50);
        AssertThat(registry.TryReserve(2UL, 500, out IReadOnlyList<ulong> evicted)).IsFalse();
        // Nothing is evicted for a request that could never fit.
        AssertThat(evicted.Count).IsEqual(0);
        AssertThat(registry.IsReserved(1UL)).IsTrue();
    }

    [TestCase]
    public void ReReservingAnIdReplacesItsPreviousCost() {
        var registry = new ParticleBudgetRegistry(100);
        registry.TryReserve(1UL, 30);
        registry.TryReserve(1UL, 70);
        AssertThat(registry.ActiveParticles).IsEqual(70);
        AssertThat(registry.ActiveEmitters).IsEqual(1);
    }

    [TestCase]
    public void ZeroOrNegativeCostsAreNotReservations() {
        var registry = new ParticleBudgetRegistry(100);
        AssertThat(registry.TryReserve(1UL, 0)).IsFalse();
        AssertThat(registry.TryReserve(2UL, -5)).IsFalse();
        AssertThat(registry.ActiveEmitters).IsEqual(0);
    }

    [TestCase]
    public void TheSharedRegistryCarriesTheProjectWideFiveHundredBudget() {
        AssertThat(ParticleBudgetRegistry.DefaultMaxParticles).IsEqual(500);
        AssertThat(ParticleBudget.Shared.MaxParticles).IsEqual(500);
        AssertThat(ParticleBudget.Shared.Policy).IsEqual(ParticleBudgetPolicy.StealOldest);
    }

    [TestCase]
    public void ClearEmptiesEveryReservation() {
        var registry = new ParticleBudgetRegistry(100);
        registry.TryReserve(1UL, 20);
        registry.TryReserve(2UL, 20);
        registry.Clear();
        AssertThat(registry.ActiveParticles).IsEqual(0);
        AssertThat(registry.ActiveEmitters).IsEqual(0);
        AssertThat(registry.TryReserve(3UL, 100)).IsTrue();
    }
}
