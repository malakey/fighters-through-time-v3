namespace FTT.Combat {

    /// <summary>
    /// Which authored effect owns the live Story HP barrier. One instance per
    /// recipient (V7.6 D02a), and the ID is half of the grant identity: a repeat
    /// grant of the <b>same</b> effect refills and restarts, a grant of a
    /// different effect replaces.
    ///
    /// <para>The design deliberately leaves "one recipient holding two different
    /// finite HP-barrier types" unauthored (DEFENSIVE_EFFECTS, "Different
    /// HP-barrier types: current authoring boundary"). No shipped kit can reach
    /// that state — each character owns exactly one barrier perk — so this enum
    /// carries a single slot rather than inventing a stacking or overflow
    /// policy that design has not chosen.</para>
    /// </summary>
    public enum StoryShieldEffect {
        None = 0,
        HenrysBastion = 1,
        RoyalAegis = 2,
        LeafBarrier = 3,
        Wardenclyffe = 4
    }

    /// <summary>
    /// One live Story HP barrier (V7.6 D02a/D02c/D02d, Package 11 A1b).
    ///
    /// <para><see cref="RemainingFrames"/> is the D02c eight-second lifetime in
    /// <b>active</b> ticks. <see cref="NoExpiry"/> marks the Wardenclyffe
    /// shield, which D02c explicitly excludes — it uses range, damage delay and
    /// depletion instead of a timer.</para>
    ///
    /// <para><see cref="GrantEventId"/> is D02a's unique grant-event identity.
    /// A refused input, polling the active condition, a duplicate animation or
    /// collision callback, a recreated visual and restored state all reuse the
    /// same ID and therefore grant nothing.</para>
    /// </summary>
    public struct StoryShieldInstance {
        public const int NoExpiry = -1;

        public StoryShieldEffect EffectId;
        public float Points;
        public float Capacity;
        public int RemainingFrames;
        public int GrantEventId;

        public readonly bool IsActive => EffectId != StoryShieldEffect.None && Points > 0f;
    }

    /// <summary>
    /// The V7.6 defensive contract's Story-side constants (Package 11 A1b).
    /// Numbers live here once; no ability script, UI script or document may
    /// carry a second copy.
    /// </summary>
    public static class StoryDefenseRules {
        /// <summary>
        /// D02c: Henry's Bastion, Royal Aegis and Leaf Barrier each last eight
        /// seconds of <b>live gameplay</b> — 480 active ticks at 60 Hz — or
        /// until their absorption reaches zero, whichever comes first. Time
        /// Freeze, menus, world-frozen recovery and boss-rewind presentations
        /// do not consume lifetime; there is no wall-clock substitution and no
        /// catch-up tick.
        /// </summary>
        public const int GrantedShieldLifetimeFrames = 480;

        /// <summary>The three granted shields share a 10%-max-HP capacity.</summary>
        public const float GrantedShieldCapacityShare = 0.10f;

        /// <summary>D02d: Wardenclyffe's absorption cap is 15% of max HP.</summary>
        public const float WardenclyffeCapacityShare = 0.15f;

        /// <summary>
        /// D02d: 2.5% of Tesla's <b>current</b> maximum HP per live second while
        /// in range and past the delay. Empty to full is therefore exactly six
        /// seconds of eligible recharge, at any MaxHP.
        /// </summary>
        public const float WardenclyffeRechargeSharePerSecond = 0.025f;

        /// <summary>
        /// D02d: three live seconds (180 ticks) whenever damage actually reduces
        /// Tesla's HP or Wardenclyffe absorption. It counts down in or out of
        /// range; new qualifying damage restarts it.
        /// </summary>
        public const float WardenclyffeDamageDelaySeconds = 3.0f;

        /// <summary>
        /// D04: 60 active gameplay ticks (one second at 60 Hz) of hit
        /// invulnerability, starting at the survivor's first resumed
        /// normal-control tick. Ticks 1–60 are protected; tick 61 is not.
        /// </summary>
        public const int DefyProtectionFrames = 60;

        /// <summary>
        /// D02c / D02d share the suspended-effect clock with every other
        /// combat timer: a frozen world consumes no lifetime, no recharge and
        /// no protected tick.
        /// </summary>
        public static bool ClockSuspended(bool inHitstop, bool timeFrozen) => inHitstop || timeFrozen;
    }
}
