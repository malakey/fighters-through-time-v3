using System.Collections.Generic;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;
using xpTURN.Klotho.Deterministic.Random;

namespace FTT.Tests.Determinism;

/// <summary>
/// Audit §4 Fighter Low: a same-tick contested Chronal Orb used to resolve by
/// <c>EntityID % 2</c> — with the first orb entity ID fixed by the spawn order,
/// that parity always awarded the same slot. Design ~3134 specifies a
/// seeded-PRNG pick. These tests pin the replacement draw,
/// <see cref="FighterOrbSystem.ResolveContestedPicker"/>: it must stay inside
/// {0, 1}, actually produce both outcomes, thread the snapshotted
/// <see cref="FighterMatchComponent.RandomState0"/>/<c>RandomState1</c> pair like
/// every other match draw (so rollback resimulation replays it bit-identically),
/// and diverge across different world seeds.
/// </summary>
[TestSuite]
public class FighterOrbContestTests {

    [TestCase]
    public void ContestedPickStaysInRangeAndProducesBothOutcomes() {
        FighterMatchComponent match = SeededMatch(2026);
        var seen = new HashSet<int>();
        for (int draw = 0; draw < 64; draw++) {
            int picker = FighterOrbSystem.ResolveContestedPicker(ref match);
            AssertThat(picker == 0 || picker == 1).IsTrue();
            seen.Add(picker);
        }
        // EntityID%2 with a fixed first-orb ID could never favour both slots
        // from the same starting point without alternating deterministically;
        // a fair seeded coin must reach both within 64 draws.
        AssertThat(seen.Contains(0)).IsTrue();
        AssertThat(seen.Contains(1)).IsTrue();
    }

    [TestCase]
    public void ContestedPickAdvancesTheSnapshottedRandomState() {
        FighterMatchComponent match = SeededMatch(4117);
        ulong state0Before = match.RandomState0;
        ulong state1Before = match.RandomState1;

        FighterOrbSystem.ResolveContestedPicker(ref match);

        // The draw must consume the match stream: leaving the state untouched
        // would hand every subsequent draw (orb effect, hazard anchor, the next
        // contest) the identical sequence and break rollback hash identity
        // against a resimulation that replayed the contest.
        AssertThat(match.RandomState0 != state0Before || match.RandomState1 != state1Before).IsTrue();
    }

    [TestCase]
    public void IdenticalStatesReplayTheIdenticalPickSequence() {
        FighterMatchComponent first = SeededMatch(909);
        FighterMatchComponent second = SeededMatch(909);
        for (int draw = 0; draw < 32; draw++) {
            AssertThat(FighterOrbSystem.ResolveContestedPicker(ref first))
                .IsEqual(FighterOrbSystem.ResolveContestedPicker(ref second));
        }
        AssertThat(first.RandomState0).IsEqual(second.RandomState0);
        AssertThat(first.RandomState1).IsEqual(second.RandomState1);
    }

    [TestCase]
    public void DifferentWorldSeedsDivergeSomewhereInThePickSequence() {
        FighterMatchComponent first = SeededMatch(1);
        FighterMatchComponent second = SeededMatch(2);
        bool diverged = false;
        for (int draw = 0; draw < 64 && !diverged; draw++) {
            diverged = FighterOrbSystem.ResolveContestedPicker(ref first)
                != FighterOrbSystem.ResolveContestedPicker(ref second);
        }
        AssertThat(diverged).IsTrue();
    }

    /// <summary>
    /// Mirrors <c>FighterWorldSystem.OnInit</c>: the match component's random
    /// state pair starts as the full state of a <see cref="DeterministicRandom"/>
    /// seeded with the world seed.
    /// </summary>
    private static FighterMatchComponent SeededMatch(int worldSeed) {
        var random = new DeterministicRandom(worldSeed);
        (ulong state0, ulong state1) = random.GetFullState();
        return new FighterMatchComponent {
            WorldSeed = worldSeed,
            RandomState0 = state0,
            RandomState1 = state1
        };
    }
}
