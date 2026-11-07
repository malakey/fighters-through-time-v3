using System;

namespace FTT.Enemies {

    /// <summary>
    /// GR-LV4 (ruling 14): an arena rule's seam, the Siege Cannon's "relocates along its track
    /// after every volley". A level sets <see cref="AbilityAfterVolley"/> when a phase opens the
    /// rule. Nothing else in the attack cycle changes, and a boss with no rule set behaves as it
    /// did.
    /// </summary>
    public partial class BossController {
        /// <summary>When set, this ability follows every ability that fired projectiles, ahead of the rest window.</summary>
        public EnemyAbilityData AbilityAfterVolley { get; set; }

        /// <summary>A projectile ability has run its course. Raised whether or not an ability follows it.</summary>
        public event Action<BossController> VolleyEnded;

        /// <summary>
        /// Called as an attack ends. True when <see cref="AbilityAfterVolley"/> has started, so the
        /// boss stays in its attack. The follow-up does not chain to itself, and it answers no
        /// target outside the arena (the arena-bound rule <see cref="SelectAbilityIndex(float, bool)"/>
        /// reads). It arms its own cooldown, so the ordinary roll does not cast it again at once.
        /// </summary>
        private bool TryBeginAbilityAfterVolley() {
            EnemyAbilityData finished = SelectedAbility;
            if (finished == null || !finished.SpawnsProjectiles) return false;
            VolleyEnded?.Invoke(this);
            EnemyAbilityData next = AbilityAfterVolley;
            if (next == null || next == finished) return false;
            if (TargetOutsideArena && IsArenaBoundArchetype(next.Archetype)) return false;

            int index = Array.IndexOf(ActiveAbilityArray, next);
            if (index >= 0) ArmAbilityCooldown(index);
            SelectedAbilityIndex = index;
            SelectedAbility = next;
            Godot.Vector2 targetPosition = _target?.GlobalPosition
                ?? GlobalPosition + new Godot.Vector2(_facingRight ? 200f : -200f, 0f);
            Executor.Begin(next, targetPosition, _facingRight,
                guardCrush: next.IsGuardCrushing, unblockable: next.IsUnblockable);
            PlayAnimation("melee_attack");
            return true;
        }
    }
}
