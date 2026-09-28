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
            // Package 12 W1 (R03 / GAP-06): while the Story world is held — the
            // Post-Landing Hold or a boss's T01b suspension — frozen actors are
            // invulnerable and give no credit. Many kit abilities deliver through
            // shape queries straight into TakeHit rather than a Hitbox, so this is
            // the chokepoint that covers them all: nothing reaches OnHit, so there
            // is no damage, stagger, status, mark, meter, Rally reclaim, checkpoint
            // strike or extractor credit. Side-effect-free (a group lookup).
            if (IsInsideTree() && FTT.Environment.ChronalRewindManager.IsWorldHeld(GetTree())) return 0f;
            // Package 12 W8: the V7.6 Time Freeze escape-only discard for the same
            // shape-query deliveries (Hitbox.OnAreaEntered covers Hitbox contacts).
            // Now that those deliveries reach PersistentObject strike surfaces, this
            // keeps a frozen world's checkpoints, extractors and enemies inert to a
            // player-sourced hit. Pure rule first, so the group lookup only runs
            // for player-on-non-player hits.
            if (Hitbox.PlayerHitIsDiscardedWhileFrozen(payload.AttackerIndex, OwnerPlayerIndex, worldFrozen: true)
                && IsWorldTimeFrozen()) {
                return 0f;
            }
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

        private bool IsWorldTimeFrozen() =>
            IsInsideTree()
            && GetTree()?.GetFirstNodeInGroup(FTT.Environment.TimeFreezeController.ControllerGroup)
                is FTT.Environment.TimeFreezeController controller
            && IsInstanceValid(controller)
            && controller.IsFrozen;

        /// <summary>A hurtbox with no live receiver cannot take a hit.</summary>
        public bool IsAlive => OnHit != null;
    }
}
