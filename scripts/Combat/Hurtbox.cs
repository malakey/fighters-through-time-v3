using Godot;
using System;

namespace FTT.Combat {

    /// <summary>
    /// Query-only damage receiver. Lives in its own file so authored scenes can
    /// attach it directly (Godot C# resolves a script class by file name).
    /// </summary>
    public partial class Hurtbox : Area2D {
        [Export] public int OwnerPlayerIndex = -1;

        /// <summary>
        /// The single adjacent combat receiver returns actual HP damage dealt. That
        /// result drives Influence gain, so blocked or invulnerable hits grant none.
        /// </summary>
        public event Func<HitPayload, float> OnHit;

        public float TakeHit(HitPayload payload) {
            payload.TargetIndex = OwnerPlayerIndex;
            return OnHit?.Invoke(payload) ?? 0f;
        }
    }
}
