using Godot;

namespace FTT.Combat {

    public static class DamageCalculator {
        public static float CalculateDamage(float baseDamage, float damageMultiplier = 1.0f) {
            return Mathf.Max(0f, baseDamage) * Mathf.Max(0f, damageMultiplier);
        }

        public static Vector2 CalculateKnockback(Vector2 baseKnockback, float targetWeight, bool attackerFacingRight) {
            float divisor = 1f + Mathf.Max(0f, targetWeight);
            Vector2 knockback = baseKnockback / divisor;
            knockback.X = Mathf.Abs(knockback.X) * (attackerFacingRight ? 1f : -1f);
            return knockback;
        }

        /// <summary>
        /// Story's half of the low-health knockback scaling (gameplay-feel plan
        /// §2.5): the impulse is multiplied by <c>1 + missingHPFraction</c> of
        /// the victim, measured *after* the hit's damage has been applied —
        /// a linear 1x at full HP to 2x at 0 HP. The Fighter sim applies the
        /// identical ratio in fixed point inside
        /// <c>FighterDamageRules.ApplyFighterHit</c>; the scale itself lives in
        /// <see cref="BasicComboRules.LowHealthKnockbackScale"/> so neither mode
        /// owns a second copy. Call sites pass post-damage HP.
        /// </summary>
        public static Vector2 CalculateKnockback(
            Vector2 baseKnockback,
            float targetWeight,
            bool attackerFacingRight,
            float targetCurrentHP,
            float targetMaxHP) => CalculateKnockback(
                baseKnockback * BasicComboRules.LowHealthKnockbackScale(targetCurrentHP, targetMaxHP),
                targetWeight,
                attackerFacingRight);

        public static float CalculateComboFinisherDamage(float baseDamage) {
            return CalculateDamage(baseDamage, 1.5f);
        }
    }

    public struct PersistentObjectData {
        public string ObjectName;
        public string ObjectTypeID;
        public float CurrentHP;
        public float MaxHP;
        public float BaseDamage;
        public float AttackRange;
        public float ActionCooldown;
        public int MaxDeployLimit;
        public float ActiveDuration;
        public float CurrentLifetime;
    }
}
