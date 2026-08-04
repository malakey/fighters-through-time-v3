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
