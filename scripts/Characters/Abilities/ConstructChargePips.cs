using Godot;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Lingering-kit pass (KL-SCON): the small row of charge pips that hangs under a Story
    /// construct's HP bar. Code-drawn placeholder (no text, so no localization): one gold diamond
    /// per remaining charge, a dim outline per spent one. The owning node calls <see cref="SetCharges"/>
    /// whenever its charges change; the row redraws only then.
    /// </summary>
    public partial class ConstructChargePips : Node2D {
        private const float PipSize = 5f;
        private const float PipGap = 3f;
        private static readonly Color Lit = new(1f, 0.83f, 0.36f);
        private static readonly Color Spent = new(0.45f, 0.4f, 0.3f, 0.6f);

        public int Charges { get; private set; }
        public int MaxCharges { get; private set; }

        public void SetCharges(int charges, int max) {
            charges = Mathf.Clamp(charges, 0, max);
            if (charges == Charges && max == MaxCharges) return;
            Charges = charges;
            MaxCharges = max;
            QueueRedraw();
        }

        public override void _Draw() {
            float width = MaxCharges * PipSize + Mathf.Max(0, MaxCharges - 1) * PipGap;
            for (int i = 0; i < MaxCharges; i++) {
                float cx = -width * 0.5f + i * (PipSize + PipGap) + PipSize * 0.5f;
                var diamond = new Vector2[] {
                    new(cx, -PipSize * 0.5f), new(cx + PipSize * 0.5f, 0f),
                    new(cx, PipSize * 0.5f), new(cx - PipSize * 0.5f, 0f)
                };
                if (i < Charges) DrawColoredPolygon(diamond, Lit);
                else DrawPolyline(new Vector2[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, Spent, 1f);
            }
        }
    }
}
