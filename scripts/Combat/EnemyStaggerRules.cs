namespace FTT.Combat {

    /// <summary>
    /// Enemy Stagger Discipline (V7.4 — design Section 4, Defense Mechanics;
    /// PvE only). Playtesting proved any Story enemy could be chained from
    /// first hit to death: the victim-agency verbs (DI, tech, hit-2 escape)
    /// are player-only, the basic-attack stun floor guarantees every jab
    /// re-stuns every enemy regardless of StunResistance, and stun expired
    /// straight into Chase with no protection. Four rules close it:
    /// <list type="bullet">
    /// <item><b>Armored getup recovery</b> (all tiers): a naturally-expiring
    /// hitstun grants <see cref="GetupArmorFrames"/> of armor — damage lands
    /// in full, but no hitstun and no knockback, and the enemy acts freely.
    /// Hits landed <i>during</i> stun still refresh it normally.</item>
    /// <item><b>Stagger budget → Armored Recovery</b> (elites and bosses):
    /// applied post-resistance stun accumulates; on exceeding the tier budget
    /// the enemy turns flinch/knockback-proof for the recovery window and
    /// immediately commits its signature telegraphed attack.</item>
    /// <item><b>Diminishing special stun</b> (per target): a special-sourced
    /// stun inside <see cref="SpecialStunDiminishWindowSeconds"/> of the
    /// previous one applies full damage but stun ×
    /// <see cref="SpecialStunDiminishFactor"/>. Basics are untouched.</item>
    /// <item><b>Pressure-exit</b>: stun or armored recovery ending with the
    /// target in attack range prefers an immediate attack over Chase.</item>
    /// </list>
    /// <b>Scope guard:</b> these rules exist only in Story Mode. Fighter Mode
    /// PvP keeps the three escape verbs as its entire answer (the amended No
    /// Juggling pillar) — nothing in scripts/FighterSim may consume this class.
    /// </summary>
    public static class EnemyStaggerRules {

        /// <summary>Armored getup window after a naturally-expiring hitstun
        /// (all enemy tiers), in 60 Hz frames: 0.6 s.</summary>
        public const int GetupArmorFrames = 36;

        /// <summary>The getup armor window in seconds (36 frames at 60 Hz).</summary>
        public const float GetupArmorSeconds = GetupArmorFrames / 60f;

        /// <summary>Accumulated post-resistance stun that triggers an elite's
        /// Armored Recovery.</summary>
        public const float EliteStaggerBudgetSeconds = 2.0f;

        /// <summary>Accumulated post-resistance stun that triggers a boss's
        /// Armored Recovery (where a boss flinches at all — the authored
        /// bosses are flinch-proof and simply compliant).</summary>
        public const float BossStaggerBudgetSeconds = 1.5f;

        /// <summary>Stagger-budget decay in seconds of credit per real second,
        /// ticking only while the enemy is not stunned.</summary>
        public const float StaggerDecayPerSecond = 1.0f;

        /// <summary>Elite Armored Recovery duration: flinch- and
        /// knockback-proof (damage still lands) with the committed answer.</summary>
        public const float EliteArmoredRecoverySeconds = 1.5f;

        /// <summary>Boss Armored Recovery duration.</summary>
        public const float BossArmoredRecoverySeconds = 1.0f;

        /// <summary>Window after a special-sourced stun during which the next
        /// special-sourced stun on the same target is diminished. Refreshes on
        /// each special-sourced stun.</summary>
        public const float SpecialStunDiminishWindowSeconds = 4.0f;

        /// <summary>Stun multiplier for a special-sourced stun landing inside
        /// the diminish window. Damage is never reduced.</summary>
        public const float SpecialStunDiminishFactor = 0.5f;
    }
}
