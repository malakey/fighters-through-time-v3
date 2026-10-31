using FTT.Characters;

namespace FTT.Combat {

    public static class StoryCombatRules {
        public const int ComboBufferFrames = BasicComboRules.ChainHoldFrames;
        public const int DropThroughFrames = 15;
        /// <summary>
        /// The double-tap Down window. Since CM-MOVE (decision 6.3, 2026-10-16) both
        /// modes read <see cref="FTT.Core.UniversalMovementRules.DropThroughDoubleTapFrames"/>;
        /// this forwards to it.
        /// </summary>
        public const int DownDoubleTapFrames = FTT.Core.UniversalMovementRules.DropThroughDoubleTapFrames;
        public const float CrouchHurtboxScale = 0.7f;

        /// <summary>
        /// The Story hero's standing hurtbox width in pixels (the 40 x 64 box
        /// <c>CharacterFactory.BuildHurtbox</c> builds). One named copy, so the
        /// Story melee-reach rules that measure a mob's swing against the hero
        /// (<see cref="StoryEnemyCombatRules.MeleePivotReachPixels"/>: D8's Punish
        /// whiff "near", and D6c's mob commit range) read the same number the
        /// factory builds. Story only; the Fighter sim has its own fixed-point
        /// hurtbox.
        /// </summary>
        public const float HeroHurtboxWidthPixels = 40f;

        /// <summary>The Story hero's standing hurtbox height in pixels (see <see cref="HeroHurtboxWidthPixels"/>).</summary>
        public const float HeroHurtboxHeightPixels = 64f;

        // === D5 ===
        // The Story meter GAIN scale (user direction 2026-10-11, ledger
        // USER-STORY-METER-SCALE; the agent-chosen details are
        // PROVISIONAL-STORY-METER-SCALE-RULES). Campaign hero only. Never read by
        // scripts/FighterSim/ or by the shared InfluenceMeterRules (source-scanned,
        // InfluenceMeterSourceScanTests).

        /// <summary>D5 (user direction): the campaign hero's Story meter-gain scale (0.35 until the meter and Defy update 2026-10-31, then 0.5).</summary>
        public const float MeterGainScale = 0.5f;

        /// <summary>
        /// The scale Story's FLAT meter effects take (an Extractor discharge, the searchlight,
        /// the Siphon Snare, Shield of Orléans, a MeterBoost orb): the D5 scale alone, never
        /// Mozart's build-rate lane (meter and Defy update 2026-10-31, ledger
        /// USER-STORY-FLAT-EFFECTS-SCALED). Applied at the call sites, never inside
        /// DrainUltimateMeter.
        /// </summary>
        public const float FlatEffectScale = MeterGainScale;

        /// <summary>
        /// The factor a flat meter effect is multiplied by: <see cref="FlatEffectScale"/> on
        /// the campaign hero only, 1 on every other build (the Mirror clone, drills, Fighter).
        /// </summary>
        public static float FlatMeterScale(bool storyScaled) => storyScaled ? FlatEffectScale : 1f;

        /// <summary>
        /// The one Story meter-gain factor: D5's scale on the campaign hero only,
        /// times the Story build-rate lane on every build (Mozart's Minor
        /// Resonance; 1.0 when unbought — the Hard Mirror clone mirrors it under
        /// F20). Flat grants, direct sets and spends never read it.
        /// </summary>
        public static float MeterGainFactor(bool storyScaled, float buildRateLane) =>
            (storyScaled ? MeterGainScale : 1f) * System.MathF.Max(0f, buildRateLane);

        public static bool IsDropThroughAllowed(CharacterState state) => state is
            CharacterState.Idle or
            CharacterState.Running or
            CharacterState.Crouching or
            CharacterState.Blocking or
            CharacterState.Attacking;

        // === CM-STORY (combat and movement update 2026-10-16) ===
        // Decision 2.4 with the Q2 ruling ("startup only"): the Story hero's
        // Ultimate is armoured while its own cast clock is in Startup, so a paid
        // Ultimate is never lost to a mob or boss stun before it fires. A Story
        // rule, not the .tres GrantsHyperArmor flag: that flag armours Startup
        // AND Active (84-210 frames per Ultimate) and the nine Ultimate resources
        // keep it false, so nothing here reaches the Fighter sim (which reads the
        // flag for the movement ability only). Ledger PROVISIONAL-STORY-ULTIMATE-ARMOR.

        /// <summary>
        /// CM-STORY (2.4, Q2): true while a Story Ultimate in
        /// <paramref name="phase"/> shrugs off the interruption of an incoming
        /// <paramref name="incomingAttackClass"/>: during its Startup, against
        /// anything but another Ultimate. Keyed to the cast's own phase clock, so
        /// hitstop and Time Freeze, which pause that clock, pause the armour too.
        /// </summary>
        public static bool UltimateStartupArmorPreventsInterruption(
            AbilityPhase phase,
            AttackClass incomingAttackClass) =>
            phase == AbilityPhase.Startup && incomingAttackClass != AttackClass.Ultimate;

        public static bool HyperArmorPreventsInterruption(
            bool grantsHyperArmor,
            AbilityPhase phase,
            AttackClass incomingAttackClass) =>
            grantsHyperArmor &&
            phase is AbilityPhase.Startup or AbilityPhase.Active &&
            incomingAttackClass != AttackClass.Ultimate;

        // === D3 ===
        // The on-hit Special recovery cancel (user direction 2026-10-11, ledger
        // USER-SPECIAL-RECOVERY-CANCEL; the agent-chosen details are
        // PROVISIONAL-SPECIAL-RECOVERY-CANCEL-RULES). A Special whose execution
        // LANDED a direct hit (the D1a predicate; BaseSpecial
        // .LandedDirectHitThisExecution) may cancel its recovery into a roll or
        // a jump from recovery frame 0, and into a basic attack (hit 1, the
        // Up-Attack or the Down-Air by held direction, per the 2026-10-11
        // addendum) or the movement ability from recovery frame 6. A whiff keeps
        // its whole recovery (only the existing grounded Block cancel), and Special -> Special stays
        // lockout-gated (neither a Special nor the Ultimate is polled in a
        // Special's recovery). CM-CANCEL (combat and movement update 2026-10-16,
        // decision 11.1): D3 is one rule in both modes now, so the table lives in
        // BasicComboRules (its D3 section) and these Story names forward to it;
        // the Fighter sim reads the same table (FighterSpecialCancelRules).

        /// <summary>D3 (user direction): the recovery frame a landed Special first allows a roll or a jump on.</summary>
        public const int SpecialRecoveryEvadeCancelFrame = BasicComboRules.SpecialRecoveryEvadeCancelFrame;

        /// <summary>D3 (user direction): the recovery frame a landed Special first allows a basic attack or the movement ability on.</summary>
        public const int SpecialRecoveryActionCancelFrame = BasicComboRules.SpecialRecoveryActionCancelFrame;

        /// <summary>
        /// D3 (provisional): how many of the controller's unfrozen ticks a cancel
        /// press stays buffered in a Special (one slot, the latest press wins;
        /// hitstop ticks feed it but never age it). A press at recovery frame 0 is
        /// exactly this old at <see cref="SpecialRecoveryActionCancelFrame"/>.
        /// </summary>
        public const int SpecialCancelBufferFrames = BasicComboRules.SpecialCancelBufferFrames;

        /// <summary>D3: true when a Special's recovery may cancel into a roll (or air dodge) or a jump.</summary>
        public static bool SpecialRecoveryAllowsEvade(bool landed, int recoveryFrame) =>
            BasicComboRules.SpecialRecoveryAllowsEvade(landed, recoveryFrame);

        /// <summary>D3: true when a Special's recovery may cancel into a basic attack or the movement ability.</summary>
        public static bool SpecialRecoveryAllowsAction(bool landed, int recoveryFrame) =>
            BasicComboRules.SpecialRecoveryAllowsAction(landed, recoveryFrame);

        /// <summary>D3: true while a buffered cancel press of this age may still fire.</summary>
        public static bool SpecialCancelBufferHolds(int ageFrames) =>
            BasicComboRules.SpecialCancelBufferHolds(ageFrames);
    }
}
