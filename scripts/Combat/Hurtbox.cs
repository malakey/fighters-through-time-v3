using Godot;
using System;

namespace FTT.Combat {

    /// <summary>
    /// Query-only damage receiver. Lives in its own file so authored scenes can
    /// attach it directly (Godot C# resolves a script class by file name).
    /// </summary>
    public partial class Hurtbox : Area2D, IDamageable {
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

        /// <summary>
        /// M08 (Package 12 W3) <see cref="IDamageable"/>. A hurtbox is the Story
        /// receiver every <see cref="Hitbox"/> contact lands on — including the
        /// <c>EnvironmentHurtboxAdapter</c> strike surfaces (checkpoint fractures,
        /// extractors, kit gates), which inherit this — so it forwards to the
        /// adjacent owner exactly as <see cref="TakeHit"/> does.
        /// </summary>
        public float TakeDamage(in HitPayload hit) => TakeHit(hit);

        /// <summary>A hurtbox with no live receiver cannot take a hit.</summary>
        public bool IsAlive => OnHit != null;
    }
}
