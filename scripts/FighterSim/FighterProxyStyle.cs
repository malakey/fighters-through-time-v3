using Godot;

namespace FTT.FighterSim {

    /// <summary>
    /// Shape and pulse identity for the Fighter driver's presentation proxies
    /// (Package 8 B6).
    ///
    /// <para>The driver renders every simulation entity — projectiles, constructs,
    /// hazards, orbs, zones — as one of 112 pooled <c>ColorRect</c>s. Before this,
    /// each category was a single flat colour: a healing orb and a meter orb were
    /// two rectangles that differed only in hue, and a hazard's warning phase was
    /// the same rectangle at a lower alpha. This adds a rotation and a pulse per
    /// category so the read is silhouette-first.</para>
    ///
    /// <para><b>Determinism boundary.</b> Every function here is a pure function of
    /// a presentation frame counter and an entity's type id. Nothing reads or writes
    /// simulation state, nothing feeds back into <c>FighterSimulation</c>, and the
    /// counter is presentation-owned. The existing hash and convergence suites are
    /// untouched and remain the proof.</para>
    /// </summary>
    public static class FighterProxyStyle {
        /// <summary>Pixels-per-unit scale factor is the driver's; this is frames.</summary>
        public const int PulsePeriodFrames = 48;

        /// <summary>Warning-phase hazards blink faster than anything else.</summary>
        public const int WarningPulsePeriodFrames = 12;

        /// <summary>Orbs are drawn as diamonds: a 45 degree square reads as a pickup.</summary>
        public static readonly float OrbRotation = Mathf.Pi / 4f;

        /// <summary>Zones breathe slowly so a persistent field does not read as a hit.</summary>
        public const int ZonePulsePeriodFrames = 96;

        /// <summary>Normalised 0..1 triangle wave for a period in frames.</summary>
        public static float Pulse(int frame, int periodFrames) {
            if (periodFrames <= 0) return 1f;
            int phase = ((frame % periodFrames) + periodFrames) % periodFrames;
            float half = periodFrames / 2f;
            return phase < half ? phase / half : 2f - phase / half;
        }

        /// <summary>
        /// Orb tint. Hue comes from <see cref="FTT.Combat.ChronalOrbItem.EffectColor"/>
        /// so a green orb means "health" in Story and Fighter alike; the pulse is
        /// added on top of the shared value rather than replacing it.
        /// </summary>
        public static Color OrbColor(int effectType, int frame) {
            Color baseColor = FTT.Combat.ChronalOrbItem.EffectColor((FTT.Combat.OrbEffect)effectType);
            float pulse = 0.78f + 0.22f * Pulse(frame, PulsePeriodFrames);
            return new Color(baseColor.R, baseColor.G, baseColor.B, pulse);
        }

        /// <summary>
        /// Orb silhouette scale. Breathes a few percent so a pickup sitting still on
        /// a platform still catches the eye.
        /// </summary>
        public static float OrbScale(int frame) => 0.92f + 0.12f * Pulse(frame, PulsePeriodFrames);

        /// <summary>
        /// Warning-phase alpha for a hazard. A telegraph that blinks reads as "about
        /// to hurt"; the previous flat 0.3 read as "already hurting, but faint".
        /// </summary>
        public static float HazardWarningAlpha(int frame) =>
            0.18f + 0.34f * Pulse(frame, WarningPulsePeriodFrames);

        /// <summary>
        /// Hazards that read better tilted: the falling/cutting identities. Type ids
        /// match <c>FighterHazardComponent.HazardTypeID</c>.
        /// </summary>
        public static float HazardRotation(int hazardTypeID, int frame) => hazardTypeID switch {
            // Rotating blade / gear identities get a continuous spin.
            4 => Mathf.Tau * (frame % 120) / 120f,
            7 => Mathf.Tau * (frame % 90) / 90f,
            _ => 0f
        };

        /// <summary>Zone alpha: a slow swell between a floor and a ceiling.</summary>
        public static float ZoneAlpha(int frame) => 0.24f + 0.16f * Pulse(frame, ZonePulsePeriodFrames);
    }
}
