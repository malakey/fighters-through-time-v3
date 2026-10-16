using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Klotho component <b>323</b> (combat and movement update 2026-10-16, plan
    /// §1.5; allocated by CM-SIM in Phase A0 and by nobody else): the per-fighter
    /// action state the update's sim rules need. Nine ints = <b>36 bytes per
    /// fighter, 72 per snapshot</b>. Snapshot and hash state like every other
    /// component; all-zero is idle in every field, <see cref="BlockTiming"/>
    /// included (a zero lowered-age reads as "never raised this stock").
    ///
    /// <para><b>Lifecycle.</b> Added in <c>FighterWorldSystem.SpawnFighter</c>
    /// beside component 322. Held at zero in every field while
    /// <c>FighterMatchFlowRules.IsOnRespawnPlatform</c> is true (the stock-loss
    /// reset: <c>FighterSimulationRules.ApplyStockLoss</c> has no Frame, so the
    /// movement system zeroes it while the platform holds the fighter, the way
    /// <c>AirDodgeUsed</c> is refreshed). The F22 Sudden Death setup reset in
    /// <c>FighterMatchSystem</c> zeroes it. CM-HIT sets
    /// <see cref="ActionFlagRespawnProtection"/> at the platform drop, after the
    /// hold. Read seam: <c>FighterSimulation.TryGetFighterActionState</c>.</para>
    ///
    /// <para><b>Owners.</b> Each field names the workstream that writes it. No
    /// other workstream allocates a component ID, grows 312 or 320, or adds a
    /// field anywhere else; a need for more state uses only the reserved
    /// <see cref="ActionFlags"/> bits 4–31.</para>
    ///
    /// <para><b>The zero-byte reuses reserved beside it (plan §1.5).</b>
    /// Component 305 <c>AttackFlags</c> 256 = <c>FighterBasicAttackRules.FlagActiveFacingRight</c>
    /// (the string box facing latched at active start; CM-SIM) and 512 =
    /// <c>FlagLedgeAttack</c> (CM-MOVE). Component 305's universal-movement slot
    /// values 8, 9 and 10 = <c>LedgeClimb</c>, <c>LedgeRoll</c>,
    /// <c>LedgeAttackClimb</c> (CM-MOVE; 8–15 are free, the air dodge owns 5–7
    /// and the kit phases 16–31). Component 310 <c>Tumble</c> value 2 = tumbling
    /// after hitstun ended (CM-HIT).</para>
    /// </summary>
    [KlothoComponent(323, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterActionStateComponent : IComponent {
        /// <summary>The Special being cast: 0 none, 1 Special 1, 2 Special 2. (CM-SIM)</summary>
        public int SpecialSlot;
        /// <summary>
        /// The cast phase: 0 none, 1 Startup, 2 Active, 3 Recovery, 4 Delegated
        /// (Tubman's Foresight runs its own timeline on component 322; this
        /// records the cast so the cast lock, the CPU and D3 have one owner).
        /// See <c>FighterSpecialCastRules</c>. (CM-SIM)
        /// </summary>
        public int SpecialPhase;
        /// <summary>Frames left in <see cref="SpecialPhase"/>. (CM-SIM)</summary>
        public int SpecialPhaseFrames;
        /// <summary>Frames elapsed in <see cref="SpecialPhase"/>, 0 on its first frame (the recovery frame index). (CM-SIM)</summary>
        public int SpecialPhaseElapsed;
        /// <summary>
        /// Cast flags: 1 StartedAerial, 2 EffectFired, 4 MeleeResolved (CM-SIM),
        /// 8 LandedDirectHit (CM-CANCEL), 16 FallbackTiming (CM-SIM: the
        /// contract carried no authored frames and runs as 0 / 1 / 0). Bits above
        /// 16 are free.
        /// </summary>
        public int SpecialFlags;
        /// <summary>
        /// The D3 cancel buffer (CM-CANCEL, Phase B): bits 0–2 the verb (0 none,
        /// 1 roll, 2 jump, 3 basic, 4 movement), bits 3–6 its age, bit 7 Up held
        /// at the press, bit 8 Down held at the press. Zero in Phase A.
        /// </summary>
        public int SpecialCancelBuffer;
        /// <summary>
        /// The perfect-block clock (CM-BLOCK, Phase B): bits 0–7 perfect-block
        /// frames left; bits 8–15 frames since the stance last dropped (0 = never
        /// raised this stock, otherwise 1..255 saturating); bit 16 the stance was
        /// up last tick. Zero in Phase A.
        /// </summary>
        public int BlockTiming;
        /// <summary>The double-tap Down window, 0..18 (CM-MOVE, Phase B). Zero in Phase A.</summary>
        public int DownTapFrames;
        /// <summary>
        /// Bit 0 <see cref="ActionFlagRespawnProtection"/> (CM-HIT); bit 1
        /// <see cref="ActionFlagJumpReleasedSincePress"/> (CM-MOVE); bit 2
        /// <see cref="ActionFlagLedgeStickRearmed"/> (CM-MOVE); bit 3
        /// <see cref="ActionFlagRiseFromOwnJump"/> (CM-MOVE, the Q5 ruling); bits
        /// 4–31 reserved, 0. Zero in Phase A.
        /// </summary>
        public int ActionFlags;

        /// <summary><see cref="ActionFlags"/> bit 0: Fighter respawn protection is live (CM-HIT, 2.4.2).</summary>
        public const int ActionFlagRespawnProtection = 1;
        /// <summary><see cref="ActionFlags"/> bit 1: Jump was released since its last press (CM-MOVE, the short hop).</summary>
        public const int ActionFlagJumpReleasedSincePress = 2;
        /// <summary><see cref="ActionFlags"/> bit 2: the ledge stick re-armed (CM-MOVE, the get-ups).</summary>
        public const int ActionFlagLedgeStickRearmed = 4;
        /// <summary><see cref="ActionFlags"/> bit 3: the current rise began with this airtime's own Jump press (CM-MOVE, Q5).</summary>
        public const int ActionFlagRiseFromOwnJump = 8;
    }
}
