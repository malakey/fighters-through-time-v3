using System;

namespace FTT.Core {

    /// <summary>
    /// GR-AUDIO (gameplay review ruling 13, 2026-11-03). The single read point for the
    /// <b>Mute placeholder cues</b> preference: the player's separate switch for the eight
    /// audible combat placeholders in <see cref="CoreMechanicCueIDs.AudiblePlaceholders"/>.
    ///
    /// <para>Same shape as <see cref="ComfortSettings"/>: the value is cached in a static that
    /// <see cref="SaveManager"/> pushes at boot (after the global payload loads) and the
    /// Settings screen pushes live, so a cue never has to wait on a settings read. It is local
    /// presentation state and stays out of match snapshots and hashes; nothing in
    /// <c>scripts/FighterSim/</c> may read it.</para>
    /// </summary>
    public static class PlaceholderCueSettings {

        private static bool _muted;

        /// <summary>True while the audible placeholder cues are muted. Default <c>false</c>.</summary>
        public static bool Muted => _muted;

        /// <summary>Raised when the preference flips.</summary>
        public static event Action<bool> Changed;

        /// <summary>
        /// Pushes the persisted value (boot) or the live toggle (Settings). Re-applying the
        /// same value raises nothing.
        /// </summary>
        public static void Apply(bool muted) {
            if (_muted == muted) return;
            _muted = muted;
            Changed?.Invoke(_muted);
        }

        /// <summary>Reads the live global payload, defaulting Off when none is loaded.</summary>
        public static void ApplyFromSave() =>
            Apply(SaveManager.Instance?.GlobalData?.PlaceholderCuesMuted ?? false);

        /// <summary>Test seam: forgets the cached value and every subscriber.</summary>
        internal static void ResetForTesting() {
            _muted = false;
            Changed = null;
        }
    }
}
