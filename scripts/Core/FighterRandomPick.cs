using xpTURN.Klotho.Deterministic.Random;

namespace FTT.Core {

    /// <summary>
    /// Package 12 W5 (G15a). Resolves the select screens' <b>Random</b> character
    /// and stage tiles from the per-match seed — the same seed the match then runs
    /// on (<see cref="SessionData.PendingMatchSeed"/>) — rather than from an
    /// unseeded source, so a given seed always reveals the same picks.
    ///
    /// <para>Each pick draws from its own stream keyed by a fixed salt, so resolving
    /// the stage never shifts which character P1 got. Engine-free: pure-C# suites
    /// may call it.</para>
    /// </summary>
    public static class FighterRandomPick {

        /// <summary>Salt for Player 1's Random character tile.</summary>
        public const int PlayerOneCharacterSalt = 0x5031;
        /// <summary>Salt for Player 2's (or the CPU's) Random character tile.</summary>
        public const int PlayerTwoCharacterSalt = 0x5032;
        /// <summary>Salt for the Random stage tile.</summary>
        public const int StageSalt = 0x5354;

        /// <summary>
        /// A uniform index in <c>[0, count)</c> drawn from <paramref name="matchSeed"/>
        /// and <paramref name="salt"/>; 0 when <paramref name="count"/> is not positive.
        /// </summary>
        public static int Index(int matchSeed, int salt, int count) {
            if (count <= 1) return 0;
            var random = new DeterministicRandom(unchecked(matchSeed * 31 + salt));
            return random.NextInt(0, count);
        }
    }
}
