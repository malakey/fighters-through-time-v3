using Godot;

namespace FTT.Combat {

    public static class DamageCalculator {
        public static float CalculateDamage(float baseDamage, float damageMultiplier = 1.0f, float targetDefense = 0f) {
            return baseDamage * damageMultiplier * (1f - targetDefense);
        }

        public static Vector2 CalculateKnockback(Vector2 baseKnockback, float targetWeight, bool attackerFacingRight) {
            float weightResistance = 1f / Mathf.Max(targetWeight, 0.1f);
            var knockback = baseKnockback * weightResistance * 60f;
            if (!attackerFacingRight) knockback.X = -knockback.X;
            return knockback;
        }

        public static float CalculateComboFinisherDamage(float baseDamage) {
            return baseDamage * 1.5f;
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
