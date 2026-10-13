using FTT.Characters;

namespace FTT.Combat {

    public static class StoryCombatRules {
        public const int ComboBufferFrames = BasicComboRules.ChainHoldFrames;
        public const int DropThroughFrames = 15;
        public const int DownDoubleTapFrames = 18;
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

        /// <summary>D5 (user direction): the campaign hero's Story meter-gain scale.</summary>
        public const float MeterGainScale = 0.35f;

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
        // its whole recovery (only the existing grounded Block cancel), Echo Step
        // never comes out of a Special (A06), and Special -> Special stays
        // lockout-gated (neither a Special nor the Ultimate is polled in a
        // Special's recovery). Story only until DEFER-SIM-SPECIAL-PHASES closes:
        // the Fighter sim resolves Specials on the press tick, and must adopt the
        // same table when it gains Special phases.

        /// <summary>D3 (user direction): the recovery frame a landed Special first allows a roll or a jump on.</summary>
        public const int SpecialRecoveryEvadeCancelFrame = 0;

        /// <summary>D3 (user direction): the recovery frame a landed Special first allows a basic attack or the movement ability on.</summary>
        public const int SpecialRecoveryActionCancelFrame = 6;

        /// <summary>
        /// D3 (provisional): how many of the controller's unfrozen ticks a cancel
        /// press stays buffered in a Special (one slot, the latest press wins;
        /// hitstop ticks feed it but never age it). A press at recovery frame 0 is
        /// exactly this old at <see cref="SpecialRecoveryActionCancelFrame"/>.
        /// </summary>
        public const int SpecialCancelBufferFrames = 6;

        /// <summary>D3: true when a Special's recovery may cancel into a roll (or air dodge) or a jump.</summary>
        public static bool SpecialRecoveryAllowsEvade(bool landed, int recoveryFrame) =>
            landed && recoveryFrame >= SpecialRecoveryEvadeCancelFrame;

        /// <summary>D3: true when a Special's recovery may cancel into a basic attack or the movement ability.</summary>
        public static bool SpecialRecoveryAllowsAction(bool landed, int recoveryFrame) =>
            landed && recoveryFrame >= SpecialRecoveryActionCancelFrame;

        /// <summary>D3: true while a buffered cancel press of this age may still fire.</summary>
        public static bool SpecialCancelBufferHolds(int ageFrames) =>
            ageFrames >= 0 && ageFrames <= SpecialCancelBufferFrames;
    }
}
