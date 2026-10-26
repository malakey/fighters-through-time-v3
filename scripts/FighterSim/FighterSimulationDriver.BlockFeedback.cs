using FTT.Characters;
using Godot;

namespace FTT.FighterSim {

    // FU-BLOCK (feel and UI update 2026-10-25; ledger USER-BLOCK-SUCCESS-EFFECT):
    // the Fighter half of the block-success effect. The driver edge-detects an
    // ordinary block (a block-charge drop outside a fresh daze), a perfect block
    // (a rise of component 323's perfect-block count) and a guard break (a fresh
    // daze) and calls ShowBlockSpark from those three branches. Presentation
    // only: it reads nothing new and writes only a pooled Godot node. The sim
    // exposes no contact point, and blocking is front-facing, so the effect sits
    // at the proxy's front at chest height (the damage number's convention).
    public partial class FighterSimulationDriver {

        /// <summary>Forward offset from the proxy's origin, in pixels.</summary>
        public const float BlockSparkForwardPixels = 30f;
        /// <summary>Height above the proxy's feet, in pixels.</summary>
        public const float BlockSparkRisePixels = 46f;
        /// <summary>Effect scale in Fighter Mode (one knob if the camera framing wants it larger).</summary>
        public const float BlockSparkScale = 1f;

        private void ShowBlockSpark(PlayerController player, FTT.Combat.BlockSparkKind kind) {
            if (player == null) return;
            float facing = player.IsFacingRight ? 1f : -1f;
            FTT.Combat.BlockSparkEffect.Show(
                kind,
                player.GlobalPosition + new Vector2(facing * BlockSparkForwardPixels, -BlockSparkRisePixels),
                player.IsFacingRight,
                GetParent(),
                BlockSparkScale);
        }
    }
}
