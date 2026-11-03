namespace FTT.Combat {

    /// <summary>
    /// Gameplay review 2026-11-03, rulings 2b and 2b+: the soft-counter traits a
    /// Standard mob can carry (<c>EnemyData.Trait</c>). Each one makes a universal
    /// verb much better against that mob and none is ever strictly required: no
    /// trait refuses a hit, and every carrier can still be beaten with basics.
    /// </summary>
    public enum EnemyTrait {
        None = 0,
        /// <summary>Takes half from Basic and Special hits until it is thrown.</summary>
        Armoured = 1,
        /// <summary>String hits 1 and 2 do not stun it, and the second earns a counter.</summary>
        Catcher = 2,
        /// <summary>A rising strike punishes a hero descending into its reach.</summary>
        AntiAir = 3
    }

    /// <summary>
    /// The traits' pure rules and numbers (gameplay review plan section 1.12). No
    /// Godot types, so a pure-C# suite pins them. Story only: nothing under
    /// <c>scripts/FighterSim/</c> reads this. The Catcher's numbers live in
    /// <see cref="StoryEnemyCombatRules"/>; the mob's half is
    /// <c>EnemyController.Traits.cs</c>. Every number here is provisional
    /// (<c>PROVISIONAL-ENEMY-TRAIT-RULES</c>).
    /// </summary>
    public static class EnemyTraitRules {

        // === Armoured ===

        /// <summary>What an intact Armoured mob takes of a Basic or Special hit.</summary>
        public const float ArmouredDamageScale = 0.5f;

        /// <summary>
        /// The hits the armour cuts: Basic and Special class, from a Basic or
        /// Special source. An Ultimate (by class or by origin), a throw, the
        /// environment and a hazard delivery pass whole. Bowling, the wall impact,
        /// status ticks and hazards reach the mob without an intake at all.
        /// </summary>
        public static bool ArmourReduces(AttackClass attackClass, HitOrigin origin, HitDelivery delivery) =>
            attackClass is AttackClass.Basic or AttackClass.Special
            && origin is HitOrigin.Basic or HitOrigin.Special
            && delivery != HitDelivery.Hazard;

        /// <summary>The Armoured factor of one hit's intake.</summary>
        public static float ArmouredIntakeScale(
            bool armourIntact, AttackClass attackClass, HitOrigin origin, HitDelivery delivery) =>
            armourIntact && ArmourReduces(attackClass, origin, delivery) ? ArmouredDamageScale : 1f;

        // === Anti-air ===

        /// <summary>Frames from the trigger to the rising strike's active window (the diamond shows for them).</summary>
        public const int AntiAirTelegraphFrames = 10;

        /// <summary>The rising strike's active frames.</summary>
        public const int AntiAirActiveFrames = 8;

        /// <summary>The ruled recovery when the strike reaches nobody: the opening grounded pressure gets.</summary>
        public const int AntiAirMissRecoveryFrames = 40;

        /// <summary>The recovery when the strike reaches the hero.</summary>
        public const int AntiAirHitRecoveryFrames = 16;

        /// <summary>Own ticks before this mob can anti-air again.</summary>
        public const int AntiAirCooldownFrames = 150;

        /// <summary>The trigger's horizontal reach, pivot to pivot.</summary>
        public const float AntiAirReachHalfWidthPixels = 110f;

        /// <summary>
        /// The trigger's lowest height of the hero's feet over the mob's. A hero
        /// lower than this at the fall cap lands inside the telegraph.
        /// </summary>
        public const float AntiAirMinHeightPixels = 130f;

        /// <summary>The trigger's greatest height of the hero's feet over the mob's.</summary>
        public const float AntiAirMaxHeightPixels = 280f;

        /// <summary>The strike's box: its forward offset from the mob's pivot.</summary>
        public const float AntiAirBoxOffsetXPixels = 30f;

        /// <summary>The strike's box: its centre height, negative is up from the mob's feet.</summary>
        public const float AntiAirBoxOffsetYPixels = -135f;

        /// <summary>The strike's box width.</summary>
        public const float AntiAirBoxWidthPixels = 110f;

        /// <summary>The strike's box height; its lower edge clears a hero standing on the mob's floor.</summary>
        public const float AntiAirBoxHeightPixels = 130f;

        /// <summary>The strike's hitstun on the hero.</summary>
        public const float AntiAirHitstunSeconds = 0.3f;

        /// <summary>The strike's upward knockback force, in the mob's knockback units.</summary>
        public const float AntiAirKnockbackUp = -3f;

        /// <summary>
        /// A jump-in: the caller has already said the hero is in <c>Airborne</c> or
        /// an aerial swing (never Rolling, so never the air dodge, and never a
        /// movement ability), off the floor and descending.
        /// </summary>
        public static bool HeroIsJumpingIn(bool heroInJumpInState, bool heroOnFloor, float heroVelocityY) =>
            heroInJumpInState && !heroOnFloor && heroVelocityY > 0f;

        /// <summary>The hero's feet are in the band the strike can meet.</summary>
        public static bool AntiAirInReach(float absDx, float heightAboveMobFeet) =>
            absDx <= AntiAirReachHalfWidthPixels
            && heightAboveMobFeet >= AntiAirMinHeightPixels
            && heightAboveMobFeet <= AntiAirMaxHeightPixels;

        /// <summary>The whole trigger, for a mob free to act.</summary>
        public static bool AntiAirFires(
            bool jumpingIn, float absDx, float heightAboveMobFeet, int cooldownFrames, bool attackInFlight) =>
            jumpingIn && cooldownFrames <= 0 && !attackInFlight && AntiAirInReach(absDx, heightAboveMobFeet);
    }

    /// <summary>
    /// Observation-only running totals for the bot reports' trait block
    /// (gameplay review plan 2b+). Nothing in the game reads them, and a playtest
    /// runs one level per process, so they need no reset.
    /// </summary>
    public static class EnemyTraitTally {
        public static int ArmourStrips;
        public static int ArmourDamagePrevented;
        public static int CatcherCatches;
        public static int CatcherCounters;
        public static int CatcherCountersConnected;
        public static int AntiAirStrikes;
        public static int AntiAirStrikesConnected;
    }
}
