using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Per-character accent colours for the shared placeholder VFX taxonomy
    /// (Package 8 B6).
    ///
    /// <para>The 36 ability resources point at six shared scenes; what makes
    /// Joan's cast read differently from Tesla's is the tint applied at spawn. The
    /// character colour is <em>not</em> re-authored here — it is read from
    /// <see cref="FTT.Characters.CharacterFactory.GetCharacterColor"/>, which is
    /// already the canonical per-character identity colour used by the placeholder
    /// character visuals. A second table would be a second canonical value.</para>
    ///
    /// <para>The raw body colour is lifted toward white before use: several of the
    /// nine are deliberately dark (Lincoln's near-black navy) and a dark additive
    /// spark is invisible.</para>
    /// </summary>
    public static class VfxAccentPalette {
        /// <summary>How far a body colour is pulled toward white for an effect.</summary>
        public const float LiftTowardWhite = 0.35f;

        public const float CastAlpha = 0.85f;
        public const float ImpactAlpha = 1f;

        /// <summary>Accent for a character ID. Unknown IDs fall back to neutral.</summary>
        public static Color ForCharacter(string characterID, float alpha = CastAlpha) {
            Color body = FTT.Characters.CharacterFactory.GetCharacterColor(characterID ?? "");
            Color lifted = body.Lerp(Colors.White, LiftTowardWhite);
            return new Color(lifted.R, lifted.G, lifted.B, Mathf.Clamp(alpha, 0f, 1f));
        }

        /// <summary>Accent for the character that owns an ability.</summary>
        public static Color ForAbility(AbilityData data, float alpha = CastAlpha) =>
            ForCharacter(data?.CharacterID, alpha);

        /// <summary>
        /// Accent for a roster source. Bosses read hotter than ordinary enemies so a
        /// boss telegraph is distinguishable from a mob's at a glance, and the phase
        /// shifts value rather than hue so the source identity survives.
        /// </summary>
        public static Color ForRoster(bool isBoss, FTT.Core.EnemyPresentationPhase phase) {
            Color baseColor = isBoss ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.72f, 0.3f);
            return phase switch {
                FTT.Core.EnemyPresentationPhase.Telegraph =>
                    new Color(baseColor.R, baseColor.G, baseColor.B, 0.55f),
                FTT.Core.EnemyPresentationPhase.Active =>
                    new Color(baseColor.R, baseColor.G, baseColor.B, 0.95f),
                FTT.Core.EnemyPresentationPhase.Recovery =>
                    new Color(0.6f, 0.7f, 0.9f, 0.45f),
                _ => new Color(0.85f, 0.35f, 0.9f, 0.9f)
            };
        }
    }
}
