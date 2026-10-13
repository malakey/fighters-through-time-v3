using Godot;

namespace FTT.Combat {

    public static class DamageCalculator {
        public static float CalculateDamage(float baseDamage, float damageMultiplier = 1.0f) {
            return Mathf.Max(0f, baseDamage) * Mathf.Max(0f, damageMultiplier);
        }

        /// <summary>
        /// Story's one knockback rule: <c>baseKnockback / (1 + weight)</c>, its
        /// horizontal pushed the attacker's way. It is <b>static</b> — the
        /// victim's HP never enters it (user direction 2026-10-12, request 5:
        /// "the health effects of the knockback should only apply to fighting
        /// mode"), so every Story victim — the hero, the Level 13 Mirror clone,
        /// mobs, elites, a thrown or bowled mob, bosses — takes the same impulse
        /// from the same hit at any HP and on any difficulty. The missing-HP
        /// scaling is Fighter Mode's alone, in fixed point inside
        /// <c>FighterDamageRules.ApplyFighterHit</c>
        /// (<see cref="BasicComboRules.LowHealthKnockbackScale"/> is its float
        /// statement). The attacker-side Story knockback lane is applied to the
        /// payload before it gets here.
        /// </summary>
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
