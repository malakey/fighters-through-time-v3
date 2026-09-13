using System;

namespace FTT.Core {

    /// <summary>
    /// Package 11 A8 / C01a. The single read point for the
    /// <b>Reduced Temporal Effects</b> comfort preset.
    ///
    /// <para>The preset is one Boolean, but it is consumed by roughly eight
    /// presentation surfaces (the rewind overlay, the glow arbiter, the VFX
    /// emitter, the loading screen, the Fighter presentation overlay, the camera
    /// shake service, the Relativity Rift zone visual, the Fighter KO beat).
    /// Every one of them needs the answer before <see cref="SaveManager"/> has
    /// necessarily finished loading — the first loading/portal effect is built by
    /// <c>GameManager.LoadScene</c>, which can run earlier than a settings read —
    /// so the value is cached in a static that <see cref="SaveManager"/> pushes at
    /// boot and the Settings screen pushes live.</para>
    ///
    /// <para><b>It is presentation only.</b> COMFORT_SETTINGS.md C01a: the preset
    /// is stored and applied locally, outside match snapshots and hashes, and
    /// cannot influence hit eligibility, target selection, random draws,
    /// simulation results or any gameplay timer. Nothing in
    /// <c>scripts/FighterSim/</c> may read this class.</para>
    ///
    /// <para>A consumer either polls <see cref="ReducedTemporalEffects"/> when it
    /// next draws, or subscribes to <see cref="Changed"/> to drop the decorative
    /// state it has already emitted. Turning the preset <b>off</b> resumes only
    /// the remaining authored presentation of the event currently running; it must
    /// never replay a flash that has already been suppressed.</para>
    /// </summary>
    public static class ComfortSettings {

        private static bool _reducedTemporalEffects;

        /// <summary>
        /// True while the reduced treatment is selected. Default <c>false</c>:
        /// a missing legacy save value keeps the authored standard presentation,
        /// and the value is never inferred from Screen Shake.
        /// </summary>
        public static bool ReducedTemporalEffects => _reducedTemporalEffects;

        /// <summary>
        /// Raised when the preset flips. Consumers that have already emitted
        /// decorative after-images or opened a suppressed overlay use this to
        /// clear them immediately rather than waiting for the next event.
        /// </summary>
        public static event Action<bool> Changed;

        /// <summary>
        /// Pushes the persisted value. Called by <see cref="SaveManager"/> right
        /// after the global payload loads — before the first loading screen — and
        /// by the Settings screen on every toggle, so the change is live.
        /// Re-applying the same value raises nothing.
        /// </summary>
        public static void Apply(bool reducedTemporalEffects) {
            if (_reducedTemporalEffects == reducedTemporalEffects) return;
            _reducedTemporalEffects = reducedTemporalEffects;
            Changed?.Invoke(_reducedTemporalEffects);
        }

        /// <summary>Reads the live global payload, defaulting Off when none is loaded.</summary>
        public static void ApplyFromSave() =>
            Apply(SaveManager.Instance?.GlobalData?.ReducedTemporalEffects ?? false);

        /// <summary>
        /// Test seam: forgets the cached value and every subscriber. Suites that
        /// flip the preset restore it in a <c>finally</c>; this exists so a suite
        /// that also subscribed cannot leak a handler into the rest of the run.
        /// </summary>
        internal static void ResetForTesting() {
            _reducedTemporalEffects = false;
            Changed = null;
        }

        // ---- Derived treatment helpers -------------------------------------
        //
        // Named so a call site reads as the rule it is honouring rather than as a
        // bare Boolean. The reduced treatment table in COMFORT_SETTINGS.md is the
        // authority; these are the four shapes the repo's presentation code
        // actually branches on.

        /// <summary>
        /// False while reduced: chromatic aberration, RGB split, lens/refraction
        /// warp and high-frequency scanlines are disabled, including the
        /// Relativity Rift, Death Rewind and portal/loading passes. A clean,
        /// stable boundary for an active zone is kept.
        /// </summary>
        public static bool DistortionAllowed => !_reducedTemporalEffects;

        /// <summary>
        /// False while reduced: decorative temporal ghost trails and repeated
        /// after-images are removed. The single fixed Echo Step destination
        /// indicator, movement-path readability and real interactive decoys are
        /// <b>not</b> covered by this and stay on.
        /// </summary>
        public static bool GhostTrailsAllowed => !_reducedTemporalEffects;

        /// <summary>
        /// False while reduced: full-screen KO / Ultimate / rewind / transition
        /// brightness bursts, rapid polarity changes and repeated flashes are
        /// replaced by a restrained stable overlay or a smooth fade plus the
        /// existing text/glyph/local impact cue — never by a differently coloured
        /// full-screen flash.
        /// </summary>
        public static bool FullScreenFlashAllowed => !_reducedTemporalEffects;

        /// <summary>
        /// False while reduced: full-scene desaturation, pulsing vignette and
        /// flickering environmental overlays are disabled. Readable crack lines,
        /// platform warnings and static danger areas survive.
        /// </summary>
        public static bool ScreenTintPulseAllowed => !_reducedTemporalEffects;

        /// <summary>
        /// Pulse speed a glow/aura may use. While reduced, armor / status / spawn
        /// / Defy feedback becomes a steady glow with a smooth expiry fade instead
        /// of repeated flashing, so every authored pulse collapses to 0.
        /// </summary>
        public static float ResolvePulseSpeed(float authoredPulseSpeed) =>
            _reducedTemporalEffects ? 0f : authoredPulseSpeed;
    }
}
