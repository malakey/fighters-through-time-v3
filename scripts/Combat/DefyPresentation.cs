using Godot;

namespace FTT.Combat {

    /// <summary>
    /// MM-FX (plan section 2.11): the one place the Defy's world-side presentation is composed,
    /// shared by Story (<c>CoreMechanicCuePresenter</c> on <c>EventBus.OnDefyExecuted</c>) and
    /// Fighter (<c>FighterSimulationDriver.DefyFeedback</c>): the gold ring, the "DEFIED" label,
    /// the desaturating pulse, a small camera shake, and the audio cue with the
    /// <c>DefyOrScripted</c> snapshot. Presentation only; reads and writes no simulation state.
    /// </summary>
    public static class DefyPresentation {
        /// <summary>Translation key of the world label.</summary>
        public const string LabelKey = "defy_world_label";
        /// <summary>The label's scale (plan section 2.11: GoldBright at 1.25x).</summary>
        public const float LabelScale = 1.25f;
        /// <summary>Pixels above the burst centre where the label starts.</summary>
        public const float LabelRisePixels = 60f;
        public const float ShakeIntensity = 3f;
        public const float ShakeSeconds = 0.10f;

        /// <summary>
        /// Plays the burst at <paramref name="center"/> (the Defier's body centre) under
        /// <paramref name="parent"/>. Returns the ring (null when it could not spawn).
        /// </summary>
        public static DefyBurstEffect Play(Vector2 center, Node parent, float radiusPixels) {
            if (parent == null) return null;
            DefyBurstEffect ring = DefyBurstEffect.Show(center, parent, radiusPixels);
            FTT.UI.FloatingDamageNumber.ShowText(
                TranslationServer.Translate(LabelKey),
                center + new Vector2(0f, -LabelRisePixels), parent, FTT.UI.UIPalette.GoldBright, LabelScale);
            FTT.UI.DefyScreenPulse.Show(parent);
            FTT.Core.CameraShake.Instance?.Shake(ShakeIntensity, ShakeSeconds);
            FTT.Core.AudioManager.Instance?.CoreCues?.PlayDefyBurst();
            return ring;
        }
    }
}
