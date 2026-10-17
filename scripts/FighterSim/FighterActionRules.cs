namespace FTT.FighterSim {

    /// <summary>
    /// The verbs whose acceptance passes <see cref="FighterActionRules.OnActionAccepted"/>
    /// (combat and movement update 2026-10-16, plan §1.5). Presentation and
    /// tests may read it; nothing serializes it, so the ordinals are free to
    /// grow, but never reorder them: Phase B rules switch on them.
    /// </summary>
    public enum FighterActionKind {
        None = 0,
        /// <summary>Any basic swing start: each chain hit, the air strike, the Up-Attack, the Down-Air.</summary>
        Swing = 1,
        /// <summary>An accepted Special 1 or Special 2 press (the cost is spent here).</summary>
        Special = 2,
        /// <summary>An accepted Ultimate press (the meter is spent here; the activation strike begins).</summary>
        Ultimate = 3,
        /// <summary>A grab's startup begins (the chord or the direct bind).</summary>
        Grab = 4,
        /// <summary>A ground jump.</summary>
        Jump = 5,
        /// <summary>An air jump (spends one of the airtime's jumps).</summary>
        AirJump = 6,
        /// <summary>The grounded evasive roll starts.</summary>
        Roll = 7,
        /// <summary>The A03 air dodge starts.</summary>
        AirDodge = 8,
        /// <summary>The movement ability is accepted (its cooldown is armed).</summary>
        MovementAbility = 9,
        /// <summary>Echo Step is accepted (meter, cooldown and the wind-up armed).</summary>
        EchoStep = 10,
        /// <summary>A ledge hang latches.</summary>
        LedgeGrab = 11,
        /// <summary>A ledge get-up starts (no site in Phase A; CM-MOVE adds the get-ups).</summary>
        LedgeGetUp = 12
    }

    /// <summary>
    /// The one action chokepoint (combat and movement update 2026-10-16, plan
    /// §1.5, Phase A0): every site in the deterministic sim that ACCEPTS a
    /// fighter verb calls <see cref="OnActionAccepted"/> once, after the verb is
    /// committed, with the fighter's components 300 (state), 310 (verb layer),
    /// 312 (defence) and 323 (action state).
    ///
    /// <para>The body was empty in Phase A. Phase B's CM-HIT filled it: any
    /// action leaves a post-hitstun tumble (310 <c>Tumble</c>, decision 3.2),
    /// and respawn protection (323 <c>ActionFlags</c> bit 0, 4.2) and the D04
    /// Defy protection (312, 4.3) end on the first OFFENSIVE action
    /// (<see cref="IsOffensive"/>).</para>
    ///
    /// <para><b>Call sites (Phase A0).</b> The swing start (the
    /// <c>FighterMovementSystem.ProcessBasicAttackPhase</c> return), the Special
    /// acceptance (<c>FighterAbilityEntitySystem.Update</c>), the Ultimate
    /// acceptance (<c>FighterCombatSystem.TryCharacterUltimate</c>), the grab
    /// start (<c>FighterCombatSystem.AdvanceGrabs</c>), the ground jump and the
    /// air jump (<c>FighterMovementSystem.ApplyNormalMovement</c>), the roll and
    /// the air dodge (<c>FighterMovementSystem.TryStartUniversalMovement</c>),
    /// the Echo Step start (<c>FighterMovementSystem.TryStartEchoStep</c>), the
    /// movement ability (<c>FighterAbilityEntitySystem.Update</c>, after
    /// <c>ApplyMovement</c>) and the ledge grab
    /// (<c>FighterMovementSystem.TryGrabLedge</c>). Pure rule helpers the
    /// suites drive directly (<c>FighterAirDodgeRules.TryStart</c>,
    /// <c>FighterLedgeRules.Grab</c>, <c>FighterBasicAttackRules.StartSwing</c>)
    /// keep their signatures; the call sits at their caller.</para>
    /// </summary>
    public static class FighterActionRules {
        /// <summary>The offensive kinds: a swing, a Special, the Ultimate and a grab.</summary>
        public static bool IsOffensive(FighterActionKind kind) =>
            kind is FighterActionKind.Swing
                or FighterActionKind.Special
                or FighterActionKind.Ultimate
                or FighterActionKind.Grab;

        /// <summary>
        /// Called once per accepted verb, after it is committed (CM-HIT, Phase B):
        /// <list type="bullet">
        /// <item><b>3.2</b> — every kind leaves a tumble whose hitstun is over
        /// (<see cref="FighterVerbRules.ExitTumbleOnAction"/>): a launched
        /// fighter that acts out of tumble lands plainly;</item>
        /// <item><b>4.2</b> — an offensive kind ends the respawn protection
        /// (<see cref="FighterMatchFlowRules.EndRespawnProtectionOnOffence"/>);</item>
        /// <item><b>4.3</b> — an offensive kind ends the D04 Defy protection
        /// (<see cref="FighterDefenseRules.EndOnOffence"/>).</item>
        /// </list>
        /// A site that grants its own invulnerability (a ledge get-up's climb
        /// frames) calls this BEFORE it max-assigns that grant, so the grant
        /// survives the respawn protection's zeroing.
        /// </summary>
        public static void OnActionAccepted(
            ref FighterStateComponent fighter,
            ref FighterVerbComponent verb,
            ref FighterDefenseComponent defense,
            ref FighterActionStateComponent action,
            FighterActionKind kind) {
            FighterVerbRules.ExitTumbleOnAction(in fighter, ref verb);
            if (!IsOffensive(kind)) return;
            FighterMatchFlowRules.EndRespawnProtectionOnOffence(ref fighter, ref action);
            FighterDefenseRules.EndOnOffence(ref defense);
        }
    }
}
